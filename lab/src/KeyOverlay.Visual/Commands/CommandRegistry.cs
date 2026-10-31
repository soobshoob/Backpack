using KeyOverlay.Visual.Hotkeys;

namespace KeyOverlay.Visual.Commands;

/// <summary>
/// Где действует сочетание команды: <see cref="Global"/> — во всей системе (RegisterHotKey, нужен Ctrl/Alt/Win);
/// <see cref="Local"/> — только в окнах Backpack (например, F1); <see cref="Tool"/> — только пока активен инструмент
/// (регистрируется на время режима, вне его клавиша остаётся другим программам, например F2 при рисовании).
/// </summary>
public enum CommandScope { Global, Local, Tool }

/// <summary>Вид команды: действие, переключатель (есть отметка), выбор значения, открытие настройки.</summary>
public enum CommandKind { Action, Toggle, Choice, OpenSettings }

/// <summary>Инструмент, к которому относится команда области <see cref="CommandScope.Tool"/>.</summary>
public enum CommandTool { None, Drawing, Focus, CursorZoom, Plugin }

/// <summary>
/// Определение команды. Кнопки, меню трея и горячие клавиши вызывают одну реализацию — <see cref="Execute"/>.
/// <see cref="IsAvailable"/> — можно ли выполнить сейчас (выключенный инструмент — нет; его сочетания не регистрируются).
/// <see cref="IsChecked"/> — состояние переключателя (для меню и подписи).
/// </summary>
public sealed record CommandDef(
    string Id,
    string TitleKey,
    string CategoryKey,
    CommandScope Scope,
    CommandKind Kind,
    Action Execute,
    Func<bool>? IsAvailable = null,
    Func<bool>? IsChecked = null,
    Hotkey? DefaultHotkey = null,
    CommandTool Tool = CommandTool.None,
    string? Icon = null,
    string? Source = null)
{
    public bool Available => IsAvailable?.Invoke() ?? true;

    /// <summary>Сочетание можно назначить (локальные команды вроде F1 закреплены и не переназначаются).</summary>
    public bool Assignable => Scope != CommandScope.Local;

    /// <summary>Отображаемое название: ключ локализации или готовая строка расширения (<see cref="Source"/> задан).</summary>
    public string Title => Source is null ? Localization.Loc.T(TitleKey) : TitleKey;
}

/// <summary>
/// Единый реестр команд Backpack и расширений. Хранит определения и назначения сочетаний (из предпочтений компьютера —
/// импорт профиля их не меняет). Сам ничего не регистрирует: глобальные сочетания применяет <see cref="HotkeyManager"/>.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDef> _defs = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    public event Action? Changed;

    public IEnumerable<CommandDef> All => _order.Select(id => _defs[id]);

    public CommandDef? Get(string id) => _defs.GetValueOrDefault(id);

    /// <summary>Добавить или заменить команду (ID расширений — с префиксом <c>plugin.&lt;id&gt;.</c>).</summary>
    public void Register(CommandDef def)
    {
        if (string.IsNullOrWhiteSpace(def.Id) || def.Id.Length > 96)
        {
            throw new ArgumentException("command id", nameof(def));
        }

        if (!_defs.ContainsKey(def.Id))
        {
            _order.Add(def.Id);
        }

        _defs[def.Id] = def;
        Changed?.Invoke();
    }

    /// <summary>Убрать команды с префиксом (отключение или удаление расширения).</summary>
    public int RemoveWhere(Func<CommandDef, bool> predicate)
    {
        var gone = _defs.Values.Where(predicate).Select(d => d.Id).ToList();
        foreach (var id in gone)
        {
            _defs.Remove(id);
            _order.Remove(id);
        }

        if (gone.Count > 0)
        {
            Changed?.Invoke();
        }

        return gone.Count;
    }

    /// <summary>Выполнить команду, если она есть и доступна. Исключение расширения не выходит наружу.</summary>
    public bool Execute(string id)
    {
        if (!_defs.TryGetValue(id, out var d) || !d.Available)
        {
            return false;
        }

        d.Execute();
        return true;
    }

    /// <summary>
    /// Действующее сочетание команды: назначенное пользователем (пустая строка — снято), иначе — по умолчанию.
    /// Новые команды по умолчанию сочетаний не имеют (массового назначения глобальных клавиш нет).
    /// </summary>
    public Hotkey? HotkeyOf(string id, Settings.CommandBindings bindings)
    {
        if (bindings[id] is { } text)
        {
            return text.Length == 0 ? null : Hotkey.TryParse(text, out var h) ? h : Get(id)?.DefaultHotkey;
        }

        return Get(id)?.DefaultHotkey;
    }

    /// <summary>
    /// Назначения всех назначаемых команд с сочетанием. Недоступные (выключенный инструмент или плагин) тоже входят в список с
    /// <c>Available = false</c>: менеджер не регистрирует их, но показывает состояние «недоступна» вместо «не задано»
    /// (Phase 4G: назначенное сочетание не должно выглядеть пропавшим).
    /// </summary>
    public IReadOnlyList<(string Id, Hotkey Hotkey, CommandScope Scope, CommandTool Tool, bool Available)> Bindings(Settings.CommandBindings bindings) =>
        All.Where(d => d.Assignable)
            .Select(d => (d.Id, Hotkey: HotkeyOf(d.Id, bindings), d.Scope, d.Tool, d.Available))
            .Where(x => x.Hotkey is not null)
            .Select(x => (x.Id, x.Hotkey!.Value, x.Scope, x.Tool, x.Available))
            .ToList();
}
