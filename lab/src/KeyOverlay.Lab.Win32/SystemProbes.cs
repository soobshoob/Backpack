using KeyOverlay.Lab.Core.Model;
using KeyOverlay.Lab.Win32.Interop;

namespace KeyOverlay.Lab.Win32;

/// <summary>
/// Зонд GetAsyncKeyState и проверки контекста. Вызывается из UI-потока по таймеру, не из callback хука.
/// Любая неудача проверки возвращает null (= недоверенный контекст).
/// </summary>
public sealed unsafe class KeyStateProbe : IKeyStateProbe
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int UoiName = 2;
    private const uint DesktopReadObjects = 0x0001;

    private readonly Func<bool> _sessionLocked;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly uint? _ownIntegrity;

    public KeyStateProbe(Func<bool> sessionLocked)
    {
        _sessionLocked = sessionLocked;
        _ownIntegrity = IntegrityOf(Native.GetCurrentProcess(), closeProcess: false);
    }

    public uint? OwnIntegrity => _ownIntegrity;

    public ProbeContext GetContext() => new(_sessionLocked(), InputDesktopIsOurs(), ForegroundIntegrityNotHigher());

    public bool IsDown(ushort vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    public static uint? IdleMilliseconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)sizeof(LASTINPUTINFO) };
        return Native.GetLastInputInfo(&info) ? unchecked((uint)Environment.TickCount - info.dwTime) : null;
    }

    private static bool? InputDesktopIsOurs()
    {
        nint input = Native.OpenInputDesktop(0, 0, DesktopReadObjects);
        if (input == 0)
        {
            return false; // нет доступа к входному рабочему столу (например, secure desktop)
        }

        try
        {
            string? a = DesktopName(input);
            string? b = DesktopName(Native.GetThreadDesktop(Native.GetCurrentThreadId()));
            return a is null || b is null ? null : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Native.CloseDesktop(input);
        }
    }

    private static string? DesktopName(nint desktop)
    {
        if (desktop == 0)
        {
            return null;
        }

        char* buffer = stackalloc char[128];
        uint needed;
        return Native.GetUserObjectInformation(desktop, UoiName, buffer, 128 * sizeof(char), &needed) ? new string(buffer) : null;
    }

    private bool? ForegroundIntegrityNotHigher()
    {
        if (_ownIntegrity is null)
        {
            return null;
        }

        nint fg = Native.GetForegroundWindow();
        if (fg == 0)
        {
            return null;
        }

        uint pid;
        Native.GetWindowThreadProcessId(fg, &pid);
        if (pid == 0)
        {
            return null;
        }

        if (pid == _ownPid)
        {
            return true;
        }

        nint process = Native.OpenProcess(ProcessQueryLimitedInformation, 0, pid);
        if (process == 0)
        {
            return null;
        }

        uint? level = IntegrityOf(process, closeProcess: true);
        return level is null ? null : level <= _ownIntegrity;
    }

    private static uint? IntegrityOf(nint process, bool closeProcess)
    {
        nint token = 0;
        try
        {
            if (!Native.OpenProcessToken(process, TokenQuery, &token))
            {
                return null;
            }

            byte* buffer = stackalloc byte[128];
            uint returned;
            if (!Native.GetTokenInformation(token, TokenIntegrityLevel, buffer, 128, &returned))
            {
                return null;
            }

            nint sid = *(nint*)buffer; // TOKEN_MANDATORY_LABEL.Label.Sid
            byte count = *Native.GetSidSubAuthorityCount(sid);
            return count == 0 ? null : *Native.GetSidSubAuthority(sid, (uint)(count - 1));
        }
        finally
        {
            if (token != 0)
            {
                Native.CloseHandle(token);
            }

            if (closeProcess)
            {
                Native.CloseHandle(process);
            }
        }
    }
}

public readonly record struct OverlayWindowState(bool Visible, bool Topmost, bool Minimized, bool OnMonitor);

/// <summary>Стили и проверки окна оверлея (только для оверлея; окно диагностики — обычное).</summary>
public static class OverlayWindowTools
{
    public static void ApplyNoActivateClickThrough(nint hwnd)
    {
        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)ex);
    }

    public static bool HasStyle(nint hwnd, long style) => (Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE) & style) == style;

    public static bool HasClickThroughStyles(nint hwnd) =>
        HasStyle(hwnd, Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED);

    /// <summary>Только чтение. «Другое topmost-окно поверх нас» отказом не считается и здесь не проверяется.</summary>
    public static OverlayWindowState Check(nint hwnd) => new(
        Native.IsWindowVisible(hwnd),
        HasStyle(hwnd, Native.WS_EX_TOPMOST),
        Native.IsIconic(hwnd),
        Native.MonitorFromWindow(hwnd, 0) != 0);

    /// <summary>Вернуть флаг topmost (поднимает окно в верх слоя topmost) — только по подтверждённой потере.</summary>
    public static bool RestoreTopmost(nint hwnd) =>
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

    public static void ShowNoActivate(nint hwnd) => Native.ShowWindow(hwnd, Native.SW_SHOWNOACTIVATE);

    /// <summary>
    /// Наблюдение (Phase 4I): над прямоугольником <paramref name="area"/> (физические пиксели экрана) выше окна оверлея в порядке
    /// Z есть видимое, не скрытое DWM topmost-окно, не пропускающее щелчки. Только стили, прямоугольники и PID — без заголовков и
    /// содержимого. Прозрачность чужого окна неизвестна, поэтому это не доказательство, что кейкапы не видны.
    /// </summary>
    public static (bool Covered, bool Own) CoveredAbove(nint hwnd, int left, int top, int right, int bottom)
    {
        uint self = (uint)Environment.ProcessId;
        int guard = 0;
        for (nint w = Native.GetWindow(hwnd, 3 /* GW_HWNDPREV */); w != 0 && guard < 512; w = Native.GetWindow(w, 3), guard++)
        {
            if (!Native.IsWindowVisible(w) || Native.IsIconic(w) || !HasStyle(w, Native.WS_EX_TOPMOST) || HasStyle(w, Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED))
            {
                continue;
            }

            if (Native.DwmGetWindowAttribute(w, 14 /* DWMWA_CLOAKED */, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                continue;
            }

            if (!Native.GetWindowRect(w, out var r) || r.Right <= left || r.Left >= right || r.Bottom <= top || r.Top >= bottom)
            {
                continue;
            }

            uint pid;
            unsafe
            {
                Native.GetWindowThreadProcessId(w, &pid);
            }

            return (true, pid == self);
        }

        return (false, false);
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct NativeRect
{
    public int Left, Top, Right, Bottom;
}

/// <summary>Регистрация уведомлений сессии и событий WinEvent (вызывается в UI-потоке).</summary>
public static unsafe class SystemNotifications
{
    public const uint EventSystemForeground = 0x0003;
    public const uint EventSystemDesktopSwitch = 0x0020;

    private static Action<uint>? s_handler;

    public static bool RegisterSession(nint hwnd) => Native.WTSRegisterSessionNotification(hwnd, 0);

    public static bool UnregisterSession(nint hwnd) => Native.WTSUnRegisterSessionNotification(hwnd);

    /// <summary>Out-of-context WinEvent: callback приходит в поток, где установлен хук (UI, у него есть цикл сообщений).</summary>
    public static nint[] HookWinEvents(Action<uint> handler)
    {
        s_handler = handler;
        return
        [
            Native.SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, &OnWinEvent, 0, 0, 0),
            Native.SetWinEventHook(EventSystemDesktopSwitch, EventSystemDesktopSwitch, 0, &OnWinEvent, 0, 0, 0),
        ];
    }

    public static void UnhookWinEvents(nint[] hooks)
    {
        foreach (var h in hooks)
        {
            if (h != 0)
            {
                Native.UnhookWinEvent(h);
            }
        }

        s_handler = null;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            s_handler?.Invoke(evt);
        }
        catch
        {
            // не пропускать исключение через native-границу
        }
    }
}
