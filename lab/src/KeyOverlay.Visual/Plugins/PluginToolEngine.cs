using Backpack.Sdk;
using KeyOverlay.Visual.Mouse;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Plugins;

/// <summary>
/// Движок инструментов хоста для плагинов (SDK 1.1). Сессии — по паре (плагин, ID инструмента):
/// <list type="bullet">
/// <item>увеличение Follow/Pinned — <see cref="CursorMagnifier"/>;</item>
/// <item>увеличение Region (линза с отдельной областью источника) и затемнение — свой <see cref="FocusController"/> на сессию
///       (выделение, ручки по наведению, перенос между мониторами, Esc).</item>
/// </list>
/// Плагину не передаются ни изображение, ни координаты: только событие закрытия. Повторный запрос с тем же ID обновляет
/// параметры открытой сессии (без второго окна). Не более <see cref="PluginToolParser.MaxToolsPerPlugin"/> сессий на плагин.
/// </summary>
public sealed class PluginToolEngine : IPluginToolHost, IDisposable
{
    private readonly Func<IReadOnlyList<MonitorDesc>> _monitors;
    private readonly Func<IEnumerable<nint>> _ownWindows;
    private readonly Action<bool, int>? _iconInteraction;
    private readonly List<Session> _sessions = new();
    private long _order;

    public PluginToolEngine(Func<IReadOnlyList<MonitorDesc>> monitors, Func<IEnumerable<nint>>? ownWindows = null, Action<bool, int>? iconInteraction = null)
    {
        _monitors = monitors;
        _ownWindows = ownWindows ?? (() => []);
        _iconInteraction = iconInteraction;
    }

    public event Action<string, string, string>? Closed;

    /// <summary>Открытые сессии изменились (для статуса команд «инструмент активен»).</summary>
    public event Action? Changed;

    /// <summary>Анимации разрешены (управление по наведению, появление затемнения).</summary>
    public Func<bool> Animate { get; set; } = () => System.Windows.SystemParameters.ClientAreaAnimation;

    public int Count => _sessions.Count;

    /// <summary>Сессии плагина (для проверок).</summary>
    public IReadOnlyList<(string Tool, CursorMagnifier? Cursor, FocusController? Focus)> SessionsOf(string pluginId) =>
        _sessions.Where(s => s.Plugin == pluginId).Select(s => (s.Tool, s.Cursor, s.Focus)).ToList();

    public bool IsOpen(string pluginId, string toolId) => Find(pluginId, toolId) is not null;

    /// <summary>Холст рисования хоста (SDK 1.2, overlay.draw); null — недоступен (запрос закрывается с причиной unavailable).</summary>
    public IPluginDrawHost? Drawing
    {
        get => _drawing;
        set
        {
            _drawing = value;
            if (value is null)
            {
                return;
            }

            value.Closed += (plugin, tool, reason) =>
            {
                if (Find(plugin, tool) is { Draw: not null } s)
                {
                    s.DrawClosed = true; // холст уже закрыт хостом рисования — не закрывать повторно
                    Remove(s, reason, notify: true);
                }
            };
            value.StateChanged += (plugin, tool, values) => ToolState?.Invoke(plugin, tool, values);
        }
    }

    private IPluginDrawHost? _drawing;

    public event Action<string, string, System.Text.Json.Nodes.JsonObject>? ToolState;

    /// <summary>Плагины, у которых сейчас открыт хотя бы один инструмент (для сочетаний whileTool).</summary>
    public IReadOnlySet<string> PluginsWithOpenTools => _sessions.Select(s => s.Plugin).ToHashSet(StringComparer.Ordinal);

    public void Action(string pluginId, string toolId, string action)
    {
        if (Find(pluginId, toolId) is { Draw: not null })
        {
            _drawing?.Action(pluginId, toolId, action);
        }
    }

    public void Open(string pluginId, PluginToolRequest request)
    {
        if (request.Draw is { } draw)
        {
            OpenDrawing(pluginId, request.ToolId, draw);
            return;
        }

        if (Find(pluginId, request.ToolId) is { } existing)
        {
            existing.Magnifier = request.Magnifier ?? existing.Magnifier;
            existing.Dim = request.Dim ?? existing.Dim;
            if (existing.Cursor is not null && request.Magnifier is { } m)
            {
                existing.Cursor.SetRequest(m);
            }

            existing.Focus?.ApplySettings();
            return;
        }

        if (_sessions.Count(s => s.Plugin == pluginId) >= PluginToolParser.MaxToolsPerPlugin)
        {
            return;
        }

        var session = new Session(pluginId, request.ToolId) { Magnifier = request.Magnifier, Dim = request.Dim, Order = ++_order };
        if (request.Magnifier is { Mode: MagnifierMode.Follow or MagnifierMode.Pinned } mag)
        {
            session.Cursor = new CursorMagnifier(mag, _monitors, OwnWindows, iconInteraction: _iconInteraction);
            _sessions.Add(session);
            session.Cursor.Show();
        }
        else
        {
            var focus = new FocusController(() => ToFocus(session), _monitors, OwnWindows) { Animate = Animate };
            session.Focus = focus;
            _sessions.Add(session);
            focus.StateChanged += state =>
            {
                if (state == FocusState.Off && _sessions.Contains(session))
                {
                    // Выделение отменено пользователем (Esc в выделении, правая кнопка) или закрыто Esc из движка.
                    Remove(session, session.ClosingReason ?? "cancelled", notify: true);
                }
            };
            focus.Start(request.Magnifier is not null ? FocusMode.Lens : FocusMode.Dim);
            if (focus.State == FocusState.Off)
            {
                Remove(session, "cancelled", notify: true); // нет монитора под указателем
                return;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Холст рисования: один на всех; повторный запрос владельца обновляет параметры, чужой — отклоняется (busy).</summary>
    private void OpenDrawing(string pluginId, string toolId, DrawRequest draw)
    {
        var existing = Find(pluginId, toolId);
        if (_drawing is null)
        {
            Closed?.Invoke(pluginId, toolId, "unavailable");
            return;
        }

        if (existing is null && _sessions.Count(s => s.Plugin == pluginId) >= PluginToolParser.MaxToolsPerPlugin)
        {
            return;
        }

        if (!_drawing.Open(pluginId, toolId, draw))
        {
            Closed?.Invoke(pluginId, toolId, "busy");
            return;
        }

        if (existing is not null)
        {
            existing.Draw = draw;
            return;
        }

        _sessions.Add(new Session(pluginId, toolId) { Draw = draw, Order = ++_order });
        Changed?.Invoke();
    }

    public void Close(string pluginId, string toolId)
    {
        if (Find(pluginId, toolId) is { } s)
        {
            Remove(s, null, notify: false);
        }
    }

    public void Edit(string pluginId, string toolId) => Find(pluginId, toolId)?.Focus?.RevealControls();

    public void CloseAll(string pluginId)
    {
        foreach (var s in _sessions.Where(s => s.Plugin == pluginId).ToList())
        {
            Remove(s, null, notify: false);
        }
    }

    /// <summary>
    /// Esc (наблюдение, ввод не подавляется): последняя открытая сессия — из выделения или редактирования на шаг назад, из просмотра
    /// закрывается; плагину — «escape». false — открытых сессий нет.
    /// </summary>
    public bool Escape()
    {
        // Холст рисования обрабатывает Esc сам (текст, радиальное меню, выход) — здесь только увеличение и затемнение.
        var top = _sessions.Where(s => s.Draw is null).OrderByDescending(s => s.Order).FirstOrDefault();
        if (top is null)
        {
            return false;
        }

        if (top.Focus is { State: FocusState.Editing } f)
        {
            f.Escape();
            return true;
        }

        top.ClosingReason = "escape";
        Remove(top, "escape", notify: true);
        return true;
    }

    /// <summary>Блокировка, смена рабочего стола, сон, выход: всё закрывается, плагинам — «system».</summary>
    public void CloseAllSystem(string reason = "system")
    {
        foreach (var s in _sessions.ToList())
        {
            Remove(s, reason, notify: true);
        }
    }

    private Session? Find(string plugin, string tool) => _sessions.FirstOrDefault(s => s.Plugin == plugin && s.Tool == tool);

    private void Remove(Session s, string? reason, bool notify)
    {
        if (!_sessions.Remove(s))
        {
            return;
        }

        s.Cursor?.Dispose();
        if (s.Draw is not null && !s.DrawClosed)
        {
            _drawing?.Close(s.Plugin, s.Tool);
        }

        if (s.Focus is { } f)
        {
            s.ClosingReason ??= reason ?? "host";
            f.Dispose();
        }

        Changed?.Invoke();
        if (notify)
        {
            Closed?.Invoke(s.Plugin, s.Tool, reason ?? "host");
        }
    }

    /// <summary>Окна всех сессий исключаются из изображения всех увеличений (линза не увеличивает другую линзу и себя).</summary>
    private IEnumerable<nint> OwnWindows() => _ownWindows().Concat(_sessions.SelectMany(s => s.Windows()));

    /// <summary>Параметры сессии → настройки механизма «Фокуса».</summary>
    internal static FocusSettings ToFocus(Session s)
    {
        if (s.Magnifier is { } m)
        {
            return new FocusSettings(FocusMode.Lens, Zoom: m.Zoom, Radius: m.Radius, BorderWidth: m.BorderWidth, BorderColor: m.BorderColor,
                Shadow: m.Shadow, Glow: m.Glow, AreaWidth: m.Width, AreaHeight: m.Height, ShowSource: m.ShowSource, Preset: FocusPreset.Custom).Sanitized();
        }

        var d = s.Dim ?? new DimRequest();
        var monitors = d.Monitors switch { "primary" => MonitorMode.Primary, "all" => MonitorMode.Multiple, _ => MonitorMode.Selected };
        return new FocusSettings(FocusMode.Dim, DimOpacity: d.Opacity, Radius: d.Radius, BorderWidth: d.BorderWidth, BorderColor: d.BorderColor,
            Glow: d.Glow, EdgeSoftness: d.Softness, AppearMs: d.AppearMs, DimMonitors: monitors, AreaWidth: d.Width, AreaHeight: d.Height, Preset: FocusPreset.Custom).Sanitized();
    }

    public void Dispose()
    {
        CloseAllSystem("host");
        Closed = null;
        Changed = null;
    }

    internal sealed class Session(string plugin, string tool)
    {
        public string Plugin { get; } = plugin;

        public string Tool { get; } = tool;

        public MagnifierRequest? Magnifier { get; set; }

        public DimRequest? Dim { get; set; }

        public DrawRequest? Draw { get; set; }

        public bool DrawClosed { get; set; }

        public CursorMagnifier? Cursor { get; set; }

        public FocusController? Focus { get; set; }

        public long Order { get; set; }

        public string? ClosingReason { get; set; }

        public IEnumerable<nint> Windows()
        {
            if (Cursor?.Surface is { } cs)
            {
                yield return cs.HostHandle;
                yield return cs.FrameHandle;
            }

            if (Focus is { } f)
            {
                if (f.Surface is { } fs)
                {
                    yield return fs.HostHandle;
                    yield return fs.FrameHandle;
                }

                foreach (var d in f.DimWindows)
                {
                    yield return d.Hwnd;
                }

                foreach (var g in f.Grips)
                {
                    yield return g.Hwnd;
                }

                if (f.SourceOutlineWindow is { Hwnd: not 0 } o)
                {
                    yield return o.Hwnd;
                }
            }
        }
    }
}
