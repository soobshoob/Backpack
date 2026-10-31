using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace KeyOverlay.Visual.Hotkeys;

/// <summary>
/// Действия горячих клавиш KeyOverlay. <see cref="Screenshot"/> — сочетание режима рисования: регистрируется только пока режим
/// включён (поэтому по умолчанию допустима одиночная F2 — вне рисования она остаётся другим программам).
/// </summary>
public enum HotkeyAction { ToggleDisplay, Lens, Draw, Radial, Screenshot }

/// <summary>Модификаторы в значениях RegisterHotKey (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN).</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>Сочетание: модификаторы + одна обычная клавиша (виртуальный код). Запись — «Ctrl+Alt+Shift+O».</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int Vk)
{
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var mods = HotkeyModifiers.None;
        int vk = 0;
        foreach (var raw in text.Split('+'))
        {
            string part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl": mods |= HotkeyModifiers.Ctrl; continue;
                case "alt": mods |= HotkeyModifiers.Alt; continue;
                case "shift": mods |= HotkeyModifiers.Shift; continue;
                case "win": mods |= HotkeyModifiers.Win; continue;
            }

            if (vk != 0 || !TryKey(part, out vk))
            {
                return false;
            }
        }

        hotkey = new Hotkey(mods, vk);
        return vk != 0;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(Vk));
        return string.Join("+", parts);
    }

    /// <summary>Из события клавиатуры окна (запись сочетания). Только модификатор — ещё не сочетание.</summary>
    public static Hotkey? FromKey(Key key, ModifierKeys modifiers)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
        {
            return null;
        }

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            mods |= HotkeyModifiers.Ctrl;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            mods |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            mods |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            mods |= HotkeyModifiers.Win;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        return vk == 0 ? null : new Hotkey(mods, vk);
    }

    private static string KeyName(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F).ToString(CultureInfo.InvariantCulture),
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        0x08 => "Backspace",
        0x2E => "Delete",
        0x2D => "Insert",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x13 => "Pause",
        0x2C => "PrintScreen",
        0xC0 => "`",
        0xBD => "-",
        0xBB => "=",
        0xDB => "[",
        0xDD => "]",
        0xDC => "\\",
        0xBA => ";",
        0xDE => "'",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        >= 0x60 and <= 0x69 => "Num" + (vk - 0x60).ToString(CultureInfo.InvariantCulture),
        _ => "VK" + vk.ToString("X2", CultureInfo.InvariantCulture),
    };

    private static bool TryKey(string name, out int vk)
    {
        vk = 0;
        if (name.Length == 1 && char.ToUpperInvariant(name[0]) is var c && (c is >= 'A' and <= 'Z' || c is >= '0' and <= '9'))
        {
            vk = c;
            return true;
        }

        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int f) && f is >= 1 and <= 24)
        {
            vk = 0x6F + f;
            return true;
        }

        if (name.StartsWith("VK", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int raw) && raw is > 0 and < 0xFF)
        {
            vk = raw;
            return true;
        }

        for (int v = 1; v < 0xFF; v++)
        {
            string n = KeyName(v);
            if (!n.StartsWith("VK", StringComparison.Ordinal) && string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                vk = v;
                return true;
            }
        }

        return false;
    }
}

/// <summary>Почему сочетание недоступно или его состояние.</summary>
public enum HotkeyStatus { NotSet, Registered, Busy, Conflict, Reserved, Invalid, Failed, Suspended, WaitingForMode, Unavailable }

/// <summary>
/// Пять прежних действий (app.json v1–v3). С app.json v4 их сочетания — команды реестра (<c>AppPreferencesStore.LegacyMap</c>);
/// этот тип остался для чтения старых файлов и для проверок менеджера. Значения по умолчанию — Ctrl+Alt+Shift с буквой:
/// не системные сочетания Windows (Win+Space, Win+L и т. п. не назначаются).
/// </summary>
public sealed record HotkeySettings(Hotkey? ToggleDisplay, Hotkey? Lens, Hotkey? Draw, Hotkey? Radial, Hotkey? Screenshot = null)
{
    public static readonly Hotkey DefaultScreenshot = new(HotkeyModifiers.None, 0x71); // F2

    /// <summary>Без прежних действий: приложение (app.json v4) назначает все сочетания через команды реестра.</summary>
    public static HotkeySettings None { get; } = new(null, null, null, null, null);

    public static HotkeySettings Default { get; } = new(
        new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'O'),
        new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'Z'),
        new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'D'),
        new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'R'),
        DefaultScreenshot);

    /// <summary>Сочетание действует только в режиме рисования.</summary>
    public static bool IsDrawingScoped(HotkeyAction a) => a == HotkeyAction.Screenshot;

    public Hotkey? Get(HotkeyAction a) => a switch
    {
        HotkeyAction.ToggleDisplay => ToggleDisplay,
        HotkeyAction.Lens => Lens,
        HotkeyAction.Draw => Draw,
        HotkeyAction.Radial => Radial,
        _ => Screenshot,
    };

    public HotkeySettings With(HotkeyAction a, Hotkey? h) => a switch
    {
        HotkeyAction.ToggleDisplay => this with { ToggleDisplay = h },
        HotkeyAction.Lens => this with { Lens = h },
        HotkeyAction.Draw => this with { Draw = h },
        HotkeyAction.Radial => this with { Radial = h },
        _ => this with { Screenshot = h },
    };
}

/// <summary>Проверки сочетаний без регистрации.</summary>
public static class HotkeyRules
{
    /// <summary>
    /// Известные системные сочетания Windows и общеупотребительные команды: их нельзя назначить. Список не исчерпывающий —
    /// занятое другой программой сочетание обнаруживается при регистрации (состояние «занято»).
    /// </summary>
    private static readonly HashSet<Hotkey> Reserved = BuildReserved();

    public static bool IsReserved(Hotkey h) => Reserved.Contains(h);

    /// <summary>
    /// Допустимо как глобальное: нужна хотя бы одна из Ctrl, Alt, Win (иначе сочетание перехватывало бы обычный ввод: Shift+A,
    /// одиночные клавиши). F1 без модификаторов — локальная справка, глобально не назначается.
    /// </summary>
    public static bool IsAcceptable(Hotkey h) =>
        h.Vk is > 0 and < 0xFF && (h.Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0;

    /// <summary>
    /// Для действия режима рисования допустимы и функциональные клавиши F2–F24 без модификаторов: сочетание регистрируется
    /// только пока режим включён, обычный ввод в других программах вне режима не перехватывается.
    /// </summary>
    public static bool IsAcceptable(Hotkey h, HotkeyAction action) =>
        IsAcceptable(h) || (HotkeySettings.IsDrawingScoped(action) && h.Vk is >= 0x71 and <= 0x87 && (h.Modifiers & HotkeyModifiers.Win) == 0);

    private static HashSet<Hotkey> BuildReserved()
    {
        const HotkeyModifiers W = HotkeyModifiers.Win, A = HotkeyModifiers.Alt, C = HotkeyModifiers.Ctrl, S = HotkeyModifiers.Shift;
        var set = new HashSet<Hotkey>
        {
            new(W, 0x20), new(W | S, 0x20), new(W | C, 0x20), // переключение раскладки
            new(A, 0x09), new(A | S, 0x09), new(A, 0x73), new(A, 0x1B), new(C, 0x1B), new(C | S, 0x1B), new(C | A, 0x2E),
            new(W, 0x09), new(W | C, 0x44), new(W | C, 0x25), new(W | C, 0x27), new(W | C, 0x73),
            new(W | S, 'S'), new(W, 0x2C), new(A, 0x2C), new(W, 0xBE), new(W, 0xBA),
            new(W, 0x25), new(W, 0x26), new(W, 0x27), new(W, 0x28), new(W | S, 0x25), new(W | S, 0x27),
            new(C | A, 0x09),
        };
        foreach (char c in "ABDEGHIKLMNPRSTUVWX0123456789")
        {
            set.Add(new Hotkey(W, c));
        }

        return set;
    }
}

/// <summary>Вызовы Win32 (подменяются в тестах).</summary>
public interface IHotkeyApi
{
    bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error);

    bool Unregister(nint hwnd, int id);
}

public sealed partial class Win32HotkeyApi : IHotkeyApi
{
    public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
    {
        bool ok = RegisterHotKey(hwnd, id, modifiers, vk);
        error = ok ? 0 : Marshal.GetLastPInvokeError();
        return ok;
    }

    public bool Unregister(nint hwnd, int id) => UnregisterHotKey(hwnd, id);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hwnd, int id);
}

/// <summary>
/// Общий менеджер глобальных горячих клавиш (показ/скрытие, линза, рисование, радиальное меню). Работает в UI-потоке на
/// собственном message-only окне через <c>RegisterHotKey</c> с <c>MOD_NOREPEAT</c>: автоповтор не вызывает действие повторно.
/// <c>RegisterHotKey</c> не подавляет другие клавиши: перехватывается только само назначенное сочетание. Применение новых
/// сочетаний не трогает захват ввода (это отдельный механизм).
/// <list type="bullet">
/// <item>одинаковое сочетание у двух действий — внутренний конфликт: регистрируется только первое, у второго — «конфликт»;</item>
/// <item>известное системное сочетание — «зарезервировано», не регистрируется;</item>
/// <item>занятое другой программой (ERROR_HOTKEY_ALREADY_REGISTERED) — «занято»;</item>
/// <item><see cref="Suspend"/> снимает все регистрации на время записи нового сочетания в окне настроек.</item>
/// </list>
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private const uint ModNoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;

    private readonly IHotkeyApi _api;
    private readonly Dictionary<HotkeyAction, HotkeyStatus> _status = new();
    private readonly Dictionary<string, HotkeyStatus> _commandStatus = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _commandIds = new();
    private readonly HashSet<Commands.CommandTool> _activeTools = new();
    private IReadOnlyList<(string Id, Hotkey Hotkey, Commands.CommandScope Scope, Commands.CommandTool Tool, bool Available)> _commands = [];
    private readonly HashSet<int> _registered = new();
    private HwndSource? _source;
    private HotkeySettings _settings = new(null, null, null, null);
    private bool _suspended;
    private bool _drawingActive;
    private bool _disposed;

    public HotkeyManager(IHotkeyApi? api = null, bool createWindow = true)
    {
        _api = api ?? new Win32HotkeyApi();
        if (createWindow)
        {
            var p = new HwndSourceParameters("Backpack.Hotkeys") { ParentWindow = new nint(-3) /* HWND_MESSAGE */, Width = 0, Height = 0, WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        foreach (var a in Enum.GetValues<HotkeyAction>())
        {
            _status[a] = HotkeyStatus.NotSet;
        }
    }

    public event Action<HotkeyAction>? Pressed;

    /// <summary>Нажато сочетание команды реестра (ID команды).</summary>
    public event Action<string>? CommandPressed;

    /// <summary>Состояние сочетания команды реестра (нет записи — сочетание не назначено).</summary>
    public HotkeyStatus StatusOf(string commandId) => _commandStatus.GetValueOrDefault(commandId, HotkeyStatus.NotSet);

    /// <summary>
    /// Сочетания команд реестра (кроме пяти прежних действий). Проверка конфликтов — общая с прежними действиями;
    /// команды области «инструмент» регистрируются только пока их инструмент активен.
    /// </summary>
    public void ApplyCommands(IReadOnlyList<(string Id, Hotkey Hotkey, Commands.CommandScope Scope, Commands.CommandTool Tool, bool Available)> commands)
    {
        _commands = commands;
        Apply(_settings);
    }

    /// <summary>Инструмент активен/неактивен: его сочетания регистрируются или освобождаются.</summary>
    public void SetToolActive(Commands.CommandTool tool, bool active)
    {
        if (_disposed || (active ? !_activeTools.Add(tool) : !_activeTools.Remove(tool)))
        {
            return;
        }

        Apply(_settings);
    }

    private readonly HashSet<string> _activePlugins = new(StringComparer.Ordinal);

    /// <summary>
    /// У плагина открыт инструмент (SDK 1.2): регистрируются его команды с <c>whileTool</c> (например, F2 — снимок на холсте
    /// рисования); после закрытия инструмента сочетания освобождаются.
    /// </summary>
    public void SetPluginToolActive(string pluginId, bool active)
    {
        if (_disposed || (active ? !_activePlugins.Add(pluginId) : !_activePlugins.Remove(pluginId)))
        {
            return;
        }

        Apply(_settings);
    }

    /// <summary>Окно сообщений (в тестах без окна — 1).</summary>
    public nint Handle => _source?.Handle ?? 1;

    public HotkeySettings Settings => _settings;

    public int RegisteredCount => _registered.Count;

    public bool IsSuspended => _suspended;

    public HotkeyStatus StatusOf(HotkeyAction a) => _status[a];

    /// <summary>Применить новые сочетания: всё прежнее снимается, новое регистрируется (без перезапуска захвата).</summary>
    public void Apply(HotkeySettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _settings = settings;
        UnregisterAll();
        if (_suspended)
        {
            foreach (var a in Enum.GetValues<HotkeyAction>())
            {
                _status[a] = settings.Get(a) is null ? HotkeyStatus.NotSet : HotkeyStatus.Suspended;
            }

            RegisterCommands(new HashSet<Hotkey>());
            return;
        }

        var used = new HashSet<Hotkey>();
        foreach (var a in Enum.GetValues<HotkeyAction>())
        {
            _status[a] = RegisterOne(a, settings.Get(a), used);
        }

        RegisterCommands(used);
    }

    private void RegisterCommands(HashSet<Hotkey> used)
    {
        _commandStatus.Clear();
        _commandIds.Clear();
        int id = 100;
        foreach (var (cmd, h, scope, tool, available) in _commands)
        {
            id++;
            bool toolScoped = scope == Commands.CommandScope.Tool;
            bool acceptable = HotkeyRules.IsAcceptable(h) || (toolScoped && h.Vk is >= 0x71 and <= 0x87 && (h.Modifiers & HotkeyModifiers.Win) == 0);
            HotkeyStatus st;
            if (_suspended)
            {
                st = HotkeyStatus.Suspended;
            }
            else if (!available)
            {
                st = HotkeyStatus.Unavailable; // сочетание сохранено, команда сейчас выключена — не регистрируется
            }
            else if (!acceptable)
            {
                st = HotkeyStatus.Invalid;
            }
            else if (HotkeyRules.IsReserved(h))
            {
                st = HotkeyStatus.Reserved;
            }
            else if (!used.Add(h))
            {
                st = HotkeyStatus.Conflict;
            }
            else if (toolScoped && !(tool == Commands.CommandTool.Drawing ? _drawingActive
                : tool == Commands.CommandTool.Plugin ? _activePlugins.Any(p => cmd.StartsWith("plugin." + p + ".", StringComparison.Ordinal))
                : _activeTools.Contains(tool)))
            {
                st = HotkeyStatus.WaitingForMode;
            }
            else if (_api.Register(Handle, id, (uint)h.Modifiers | ModNoRepeat, (uint)h.Vk, out int error))
            {
                _registered.Add(id);
                _commandIds[id] = cmd;
                st = HotkeyStatus.Registered;
            }
            else
            {
                st = error == ErrorHotkeyAlreadyRegistered ? HotkeyStatus.Busy : HotkeyStatus.Failed;
            }

            _commandStatus[cmd] = st;
        }
    }

    public bool DrawingActive => _drawingActive;

    /// <summary>
    /// Режим рисования включён/выключен: сочетания режима (снимок) регистрируются только на это время. Вне режима F2 и т. п.
    /// не перехватываются и остаются другим программам.
    /// </summary>
    public void SetDrawingActive(bool active)
    {
        if (_drawingActive != active && !_disposed)
        {
            _drawingActive = active;
            Apply(_settings);
        }
    }

    /// <summary>Временно снять все регистрации (запись сочетания). Повторный вызов безопасен.</summary>
    public void Suspend()
    {
        _suspended = true;
        Apply(_settings);
    }

    public void Resume()
    {
        _suspended = false;
        Apply(_settings);
    }

    /// <summary>
    /// Самопроверка: то же, что приход WM_HOTKEY для зарегистрированного сочетания команды (без ввода в систему). false — у
    /// команды нет действующей регистрации.
    /// </summary>
    public bool SimulateCommand(string commandId) => RaiseCommand(commandId);

    /// <summary>Для тестов: имитация нажатия зарегистрированного сочетания команды.</summary>
    internal bool RaiseCommand(string commandId)
    {
        var pair = _commandIds.FirstOrDefault(x => x.Value == commandId);
        if (pair.Value is null)
        {
            return false;
        }

        Raise(pair.Key);
        return true;
    }

    /// <summary>Для тестов и самопроверки: имитация WM_HOTKEY (без ввода в систему).</summary>
    internal void Raise(int id)
    {
        if (!_disposed && _registered.Contains(id))
        {
            if (_commandIds.TryGetValue(id, out var cmd))
            {
                CommandPressed?.Invoke(cmd);
            }
            else
            {
                Pressed?.Invoke((HotkeyAction)(id - 1));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
        Pressed = null;
        CommandPressed = null;
    }

    private HotkeyStatus RegisterOne(HotkeyAction a, Hotkey? hotkey, HashSet<Hotkey> used)
    {
        if (hotkey is not { } h)
        {
            return HotkeyStatus.NotSet;
        }

        if (!HotkeyRules.IsAcceptable(h, a))
        {
            return HotkeyStatus.Invalid;
        }

        if (HotkeyRules.IsReserved(h))
        {
            return HotkeyStatus.Reserved;
        }

        if (!used.Add(h))
        {
            return HotkeyStatus.Conflict;
        }

        if (HotkeySettings.IsDrawingScoped(a) && !_drawingActive)
        {
            return HotkeyStatus.WaitingForMode;
        }

        int id = (int)a + 1;
        if (_api.Register(Handle, id, (uint)h.Modifiers | ModNoRepeat, (uint)h.Vk, out int error))
        {
            _registered.Add(id);
            return HotkeyStatus.Registered;
        }

        return error == ErrorHotkeyAlreadyRegistered ? HotkeyStatus.Busy : HotkeyStatus.Failed;
    }

    private void UnregisterAll()
    {
        foreach (int id in _registered)
        {
            _api.Unregister(Handle, id);
        }

        _registered.Clear();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            handled = true;
            Raise((int)wParam);
        }

        return 0;
    }
}
