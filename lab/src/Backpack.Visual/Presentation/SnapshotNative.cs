using System.Runtime.InteropServices;
using System.Windows.Interop;
using Backpack.Visual.Hotkeys;

namespace Backpack.Visual.Presentation;

/// <summary>
/// Наблюдение для снимка (только чтение состояния, ввод не подавляется и не изменяется): кнопки мыши, активное окно и признак
/// ввода текста (системная каретка в активном окне).
/// </summary>
public static partial class SnapshotNative
{
    /// <summary>Нажата ли сейчас левая, правая или средняя кнопка мыши.</summary>
    public static bool AnyMouseButtonDown() =>
        (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0 || (GetAsyncKeyState(0x04) & 0x8000) != 0;

    public static nint Foreground() => GetForegroundWindow();

    /// <summary>
    /// В активном окне идёт ввод текста: у его потока есть системная каретка (поле ввода, редактор). Тогда Space над снимком не
    /// перехватывается — пробел остаётся тексту. Программы без системной каретки этот признак не дают (ограничение Windows).
    /// </summary>
    public static bool IsTyping()
    {
        nint fg = GetForegroundWindow();
        if (fg == 0)
        {
            return false;
        }

        uint thread = GetWindowThreadProcessId(fg, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        return thread != 0 && GetGUIThreadInfo(thread, ref info) && (info.Caret != 0 || (info.Flags & 0x1 /* GUI_CARETBLINKING */) != 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public int CaretLeft;
        public int CaretTop;
        public int CaretRight;
        public int CaretBottom;
    }

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vk);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
}

/// <summary>
/// Space над снимком: пока указатель над снимком (и нет ввода текста, кнопки мыши отпущены), регистрируется глобальное сочетание
/// Space (<c>RegisterHotKey</c>, <c>MOD_NOREPEAT</c>) на собственном окне сообщений; указатель ушёл — сочетание сразу снимается,
/// и пробел снова получают программы. Регистрация — тем же механизмом, что у сочетаний Backpack (<see cref="IHotkeyApi"/>).
/// Если Space занят другой программой, регистрация не удаётся и Space просто не действует (<see cref="Unavailable"/>).
/// </summary>
public sealed class SnapshotHoverKey : IDisposable
{
    private const int Id = 0x5B00;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private const int WmHotkey = 0x0312;

    private readonly IHotkeyApi _api;
    private readonly HwndSource? _source;
    private bool _disposed;

    public SnapshotHoverKey(IHotkeyApi? api = null, bool createWindow = true)
    {
        _api = api ?? new Win32HotkeyApi();
        if (createWindow)
        {
            var p = new HwndSourceParameters("Backpack.SnapshotSpace") { ParentWindow = new nint(-3) /* HWND_MESSAGE */, Width = 0, Height = 0, WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }
    }

    /// <summary>Space сейчас перехвачен (указатель над снимком).</summary>
    public bool Armed { get; private set; }

    /// <summary>Последняя попытка регистрации не удалась (Space занят другой программой).</summary>
    public bool Unavailable { get; private set; }

    public event Action? Pressed;

    private nint Handle => _source?.Handle ?? 1;

    /// <summary>Зарегистрировать или снять Space (повтор с тем же значением ничего не делает).</summary>
    public void SetArmed(bool armed)
    {
        if (_disposed || armed == Armed)
        {
            return;
        }

        if (armed)
        {
            Armed = _api.Register(Handle, Id, ModNoRepeat, VkSpace, out _);
            Unavailable = !Armed;
        }
        else
        {
            _api.Unregister(Handle, Id);
            Armed = false;
        }
    }

    /// <summary>Нажатие (окно сообщений или тесты).</summary>
    public void Raise()
    {
        if (Armed && !_disposed)
        {
            Pressed?.Invoke();
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == Id)
        {
            handled = true;
            Raise();
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SetArmed(false);
        _disposed = true;
        Pressed = null;
        _source?.Dispose();
    }
}
