using System.Text.Json.Nodes;
using System.Windows.Threading;
using Backpack.Visual.Commands;
using Backpack.Visual.Presentation;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Plugins;

/// <summary>
/// Жизненный цикл внешних плагинов: запуск включённых доверенных, команды в общем реестре (<c>plugin.&lt;id&gt;.&lt;команда&gt;</c>),
/// панели, нарисованные хостом, видимый ввод (после фильтра приватности, только с возможностью input.visible), перезапуск
/// после сбоя и автоматическое отключение после <see cref="PluginStore.CrashLimit"/> сбоев. Отключение закрывает панели,
/// снимает команды и их сочетания и завершает процесс. Безопасный запуск (<see cref="SafeMode"/>) не запускает ни одного плагина.
/// </summary>
public sealed class PluginManager : IDisposable
{
    private readonly PluginStore _store;
    private readonly CommandRegistry _registry;
    private readonly Dispatcher _dispatcher;
    private readonly string _hostExe;
    private readonly Func<string> _locale;
    private readonly Dictionary<string, PluginProcess> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PluginPanelHost> _panels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DispatcherTimer> _held = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sent = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Наибольшая длительность удержания команды: дальше хост сам считает её отпущенной.</summary>
    public static readonly TimeSpan MaxHold = TimeSpan.FromMinutes(5);

    public PluginManager(PluginStore store, CommandRegistry registry, Dispatcher dispatcher, string hostExe, Func<string> locale, bool safeMode, IPluginToolHost? tools = null)
    {
        _store = store;
        _registry = registry;
        _dispatcher = dispatcher;
        _hostExe = hostExe;
        _locale = locale;
        SafeMode = safeMode;
        Tools = tools;
        if (tools is not null)
        {
            tools.Closed += (plugin, tool, reason) =>
            {
                if (_running.TryGetValue(plugin, out var p))
                {
                    p.ToolClosed(tool, reason);
                }
            };
            tools.ToolState += ForwardToolState;
        }
    }

    /// <summary>Инструменты, которые хост рисует по запросам плагинов (увеличение, затемнение).</summary>
    public IPluginToolHost? Tools { get; }

    /// <summary>Назначенное сочетание команды (полный ID) всё ещё удерживается — для команд удержания.</summary>
    public Func<string, bool> IsHeld { get; set; } = _ => false;

    /// <summary>Мониторы для панелей плагинов.</summary>
    public Func<IReadOnlyList<MonitorDesc>> Monitors { get; set; } = DisplayMonitors.Enumerate;

    /// <summary>Анимации разрешены (управление панелей по наведению).</summary>
    public Func<bool>? Animate { get; set; }

    public bool SafeMode { get; }

    public PluginStore Store => _store;

    public IReadOnlyDictionary<string, PluginProcess> Running => _running;

    public IReadOnlyDictionary<string, PluginPanelHost> Panels => _panels;

    /// <summary>Сообщение для пользователя (автоотключение, ошибка запуска) — показывается значком трея.</summary>
    public event Action<string>? Notice;

    public event Action? Changed;

    public void StartEnabled()
    {
        if (SafeMode)
        {
            return;
        }

        foreach (var p in _store.List().Where(p => p.Enabled))
        {
            Start(p);
        }
    }

    /// <summary>Включить (доверие должно быть подтверждено заранее) и запустить.</summary>
    public bool Enable(string id)
    {
        if (!_store.SetEnabled(id, true))
        {
            return false;
        }

        if (!SafeMode && _store.Get(id) is { } p)
        {
            Start(p);
        }

        Changed?.Invoke();
        return true;
    }

    public void Disable(string id)
    {
        StopProcess(id);
        _store.SetEnabled(id, false);
        Changed?.Invoke();
    }

    public bool Remove(string id)
    {
        StopProcess(id);
        bool ok = _store.Remove(id);
        Changed?.Invoke();
        return ok;
    }

    /// <summary>Импорт (новый или обновление): работающая прежняя версия останавливается.</summary>
    /// <param name="bundledSha">SHA-256 проверенного пакета поставки (отметка «От создателя»); null — пакет выбран пользователем.</param>
    public ImportResult Import(string path, string? bundledSha = null)
    {
        var (m, _) = PluginStore.Inspect(path);
        if (m is not null)
        {
            StopProcess(m.Id);
        }

        var r = _store.Import(path, bundledSha);
        if (!SafeMode && r.Manifest is { } im && _store.Get(im.Id) is { Enabled: true } kept)
        {
            Start(kept); // обновление из поставки сохранило включение — запустить новую версию
        }

        Changed?.Invoke();
        return r;
    }

    public void ExecuteCommand(string pluginId, string commandId)
    {
        if (!_running.TryGetValue(pluginId, out var p))
        {
            return;
        }

        string full = $"plugin.{pluginId}.{commandId}";
        if (p.Manifest.Commands.FirstOrDefault(c => c.Id == commandId) is { Hold: true })
        {
            if (_held.ContainsKey(full))
            {
                return; // автоповтор клавиатуры во время удержания — не новое нажатие
            }

            p.Command(commandId);
            WatchRelease(pluginId, commandId, full);
            return;
        }

        p.Command(commandId);
    }

    /// <summary>
    /// Отпускание команды удержания: хост опрашивает назначенное сочетание (30 мс) и сообщает плагину OnCommandReleased.
    /// Вызов из меню или палитры (сочетание не удерживается) — отпускание сразу после нажатия.
    /// </summary>
    private void WatchRelease(string pluginId, string commandId, string full)
    {
        var started = DateTime.UtcNow;
        var t = new DispatcherTimer(DispatcherPriority.Input, _dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
        _held[full] = t;
        t.Tick += (_, _) =>
        {
            if (IsHeld(full) && DateTime.UtcNow - started < MaxHold && _running.ContainsKey(pluginId))
            {
                return;
            }

            ReleaseHeld(full);
            if (_running.TryGetValue(pluginId, out var p))
            {
                p.CommandReleased(commandId);
            }
        };
        t.Start();
    }

    private void ReleaseHeld(string full)
    {
        if (_held.Remove(full, out var t))
        {
            t.Stop();
        }
    }

    /// <summary>Команда удержания сейчас нажата (для проверок).</summary>
    public bool IsCommandHeld(string fullId) => _held.ContainsKey(fullId);

    public void SettingsChanged(string pluginId, JsonObject values)
    {
        if (_store.Get(pluginId) is { } p)
        {
            _store.SaveSettings(p.Manifest, values);
            if (_running.TryGetValue(pluginId, out var proc))
            {
                proc.SettingsChanged(_store.LoadSettings(p.Manifest));
            }
        }
    }

    /// <summary>Сменился активный профиль (или его настройки плагинов): работающие плагины получают свои значения заново.</summary>
    public void ProfileChanged()
    {
        foreach (var (id, proc) in _running)
        {
            if (_store.Get(id) is { } p && _store.LoadSettings(p.Manifest) is var values && _sent.GetValueOrDefault(id) != values.ToJsonString())
            {
                _sent[id] = values.ToJsonString();
                proc.SettingsChanged(values);
            }
        }
    }

    /// <summary>
    /// Плагин записывает свои настройки (SDK 1.2): только ключи схемы manifest, значения проверяются (<see cref="PluginStore.Sanitize"/>);
    /// неизвестные ключи и неверные значения пропускаются. Плагину значения не возвращаются (он их уже знает).
    /// </summary>
    private void UpdateSettingsFromPlugin(string pluginId, JsonObject values)
    {
        if (_store.Get(pluginId) is not { } p)
        {
            return;
        }

        var merged = _store.LoadSettings(p.Manifest);
        bool changed = false;
        foreach (var (key, value) in values)
        {
            if (p.Manifest.Settings.FirstOrDefault(f => f.Key == key) is { } f && PluginStore.Sanitize(f, value?.DeepClone()) is { } clean
                && merged[key]?.ToJsonString() != clean.ToJsonString())
            {
                merged[key] = clean;
                changed = true;
            }
        }

        if (changed)
        {
            _store.SaveSettings(p.Manifest, merged);
            _sent[pluginId] = _store.LoadSettings(p.Manifest).ToJsonString(); // не отправлять плагину его же значения
            SettingsWritten?.Invoke(pluginId);
        }
    }

    /// <summary>Плагин записал свои настройки (страница плагина обновляет значения).</summary>
    public event Action<string>? SettingsWritten;

    /// <summary>Индикатор, который рисует хост для плагинов (overlay.indicator).</summary>
    public IPluginIndicatorHost? Indicator { get; set; }

    /// <summary>Есть работающий плагин, которому нужны события раскладки (input.layout) — хост ведёт наблюдение только тогда.</summary>
    public bool WantsLayout => _running.Values.Any(p => p.Manifest.Has(Backpack.Sdk.Capabilities.InputLayout));

    /// <summary>Смена раскладки активного окна — плагинам с input.layout.</summary>
    public void ForwardLayout(string code, string? detail, string reason)
    {
        foreach (var p in _running.Values)
        {
            p.LayoutChanged(code, detail, reason);
        }
    }

    /// <summary>Пользователь изменил параметры инструмента на нём самом — плагину-владельцу.</summary>
    public void ForwardToolState(string pluginId, string toolId, JsonObject values)
    {
        if (_running.TryGetValue(pluginId, out var p))
        {
            p.ToolState(toolId, values);
        }
    }

    /// <summary>Видимое нажатие после фильтра приватности (скрытое приватностью сюда не передаётся вызывающим).</summary>
    public void ForwardVisibleKey(string key, bool down)
    {
        foreach (var p in _running.Values)
        {
            p.VisibleKey(key, down);
        }
    }

    private void Start(InstalledPlugin p)
    {
        if (_disposed || _running.ContainsKey(p.Id))
        {
            return;
        }

        var proc = new PluginProcess(p.Manifest, _dispatcher);
        _running[p.Id] = proc;
        // Команды — сразу (до готовности процесса): назначенные сочетания видны в списке как «недоступна», а не «не задано».
        RegisterCommands(p.Manifest);
        proc.Ready += () =>
        {
            _sent.Remove(p.Id);
            Changed?.Invoke();
        };
        proc.PanelChanged += panel => ShowPanel(p.Manifest, panel);
        proc.IconExists = Icons.Lucide.Has;
        if (Tools is { } tools)
        {
            proc.ToolRequested += r => tools.Open(p.Id, r);
            proc.ToolCloseRequested += t => tools.Close(p.Id, t);
            proc.ToolEditRequested += t =>
            {
                if (t == "panel")
                {
                    if (_panels.TryGetValue(p.Id, out var panel))
                    {
                        panel.Reveal();
                    }
                }
                else
                {
                    tools.Edit(p.Id, t);
                }
            };
            proc.ToolActionRequested += (t, action) => tools.Action(p.Id, t, action);
        }

        proc.IndicatorRequested += r => Indicator?.Show(p.Id, r);
        proc.SettingsUpdateRequested += values => UpdateSettingsFromPlugin(p.Id, values);

        proc.Crashed += reason => OnCrashed(p.Id, reason);
        if (!proc.Start(_hostExe, p.Directory, _locale(), _store.LoadSettings(p.Manifest)))
        {
            _running.Remove(p.Id);
            _registry.RemoveWhere(d => d.Source == p.Id);
            Notice?.Invoke(Localization.Loc.F("Plugins.StartFailed", p.Manifest.Name.For(_locale())));
        }
    }

    private void RegisterCommands(PluginManifest m)
    {
        string loc = _locale();
        foreach (var c in m.Commands)
        {
            string localId = c.Id;
            // SDK 1.2: whileTool — сочетание только пока открыт инструмент плагина; сочетание и значок по умолчанию из manifest.
            Hotkeys.Hotkey? hotkey = c.Hotkey is { } hk && Hotkeys.Hotkey.TryParse(hk, out var parsed) ? parsed : null;
            _registry.Register(new CommandDef($"plugin.{m.Id}.{c.Id}", c.Title.For(loc), "Commands.Category.Plugins", c.WhileTool ? CommandScope.Tool : CommandScope.Global,
                CommandKind.Action, () => ExecuteCommand(m.Id, localId), () => _running.TryGetValue(m.Id, out var r) && r.State == PluginRunState.Running,
                DefaultHotkey: hotkey, Tool: c.WhileTool ? CommandTool.Plugin : CommandTool.None,
                Icon: c.Icon is { } icon && Icons.Lucide.Has(icon) ? icon : "puzzle", Source: m.Id));
        }

        Changed?.Invoke();
    }

    private void ShowPanel(PluginManifest m, PluginPanelSpec? panel)
    {
        if (panel is null)
        {
            if (_panels.Remove(m.Id, out var old))
            {
                old.Dispose();
            }

            return;
        }

        if (!_panels.TryGetValue(m.Id, out var host))
        {
            // Без сохранённого места — стопкой у правого верхнего угла основного монитора.
            var saved = _store.LoadPanelBounds(m.Id) ?? StackSlot();
            host = new PluginPanelHost(Monitors, saved, Animate);
            string id = m.Id;
            host.ButtonPressed += b =>
            {
                if (_running.TryGetValue(id, out var r))
                {
                    r.PanelButton(b);
                }
            };
            host.BoundsChanged += b => _store.SavePanelBounds(id, b);
            _panels[m.Id] = host;
        }

        host.Set(panel, panel.Movable);
    }

    /// <summary>Место под последней панелью стопки; null — первая панель (правый верхний угол основного монитора).</summary>
    private PixelRect? StackSlot()
    {
        if (_panels.Count == 0 || Monitors().FirstOrDefault(x => x.IsPrimary) is not { } m)
        {
            return null;
        }

        var last = _panels.Values.OrderBy(p => p.Bounds.Bottom).Last().Bounds;
        return new PixelRect(last.Left, last.Bottom + (int)(12 * m.Scale), last.Width, last.Height);
    }

    private void OnCrashed(string id, string reason)
    {
        StopProcess(id);
        string name = _store.Get(id)?.Manifest.Name.For(_locale()) ?? id;
        if (_store.RecordCrash(id, DateTime.UtcNow))
        {
            Notice?.Invoke(Localization.Loc.F("Plugins.AutoDisabled", name));
            Changed?.Invoke();
            return;
        }

        // Перезапуск через 3 с (если плагин всё ещё включён).
        var t = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (!_disposed && _store.Get(id) is { Enabled: true } p)
            {
                Start(p);
            }
        };
        t.Start();
        Changed?.Invoke();
    }

    private void StopProcess(string id)
    {
        if (_running.Remove(id, out var p))
        {
            p.Dispose();
        }

        if (_panels.Remove(id, out var w))
        {
            w.Dispose();
        }

        foreach (var held in _held.Keys.Where(k => k.StartsWith($"plugin.{id}.", StringComparison.Ordinal)).ToList())
        {
            ReleaseHeld(held);
        }

        Tools?.CloseAll(id);
        Indicator?.Show(id, null); // индикатор плагина скрывается вместе с ним
        _registry.RemoveWhere(d => d.Source == id);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var id in _running.Keys.ToList())
        {
            StopProcess(id);
        }

        Notice = null;
        Changed = null;
    }
}
