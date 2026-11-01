using System.Runtime.InteropServices;

namespace Backpack.Visual.Layout;

/// <summary>Откуда взят HKL: поток окна с фокусом клавиатуры, поток активного окна или (запасной путь) поток-владелец окна переднего плана.</summary>
public enum HklSource { None, FocusThread, ActiveThread, OwnerThread }

/// <summary>
/// Один опрос. Нули — «нет данных». <see cref="ThreadsDiffer"/> — поток окна с фокусом отличается от потока-владельца окна
/// переднего плана; <see cref="OwnerHkl"/> — HKL потока-владельца (для диагностики: старый способ 2B).
/// </summary>
public readonly record struct LayoutSample(nint Hwnd, uint ProcessId, nint Hkl, HklSource Source = HklSource.OwnerThread, bool ThreadsDiffer = false, nint OwnerHkl = 0);

public interface ILayoutProbe
{
    LayoutSample Sample();

    /// <summary>Установленные методы ввода (для различения нескольких раскладок одного языка).</summary>
    IReadOnlyList<nint> InstalledLayouts();
}

/// <summary>
/// Только чтение, без сообщений чужим окнам и без активации раскладок:
/// <list type="number">
/// <item><c>GetForegroundWindow</c> — окно переднего плана и процесс-владелец (свои окна отсекает детектор);</item>
/// <item><c>GetGUIThreadInfo(0)</c> — для потока переднего плана («If this parameter is NULL, the function returns information
///       for the foreground thread») даёт <c>hwndFocus</c> — окно с фокусом клавиатуры, которое может принадлежать другому
///       потоку (окна разных потоков с общей очередью ввода);</item>
/// <item><c>GetKeyboardLayout(поток окна с фокусом)</c> — «input locale identifier for the thread». Раскладку меняет поток,
///       который обрабатывает ввод, поэтому поток окна с фокусом точнее потока-владельца верхнего окна;</item>
/// <item>нет окна с фокусом → поток <c>hwndActive</c>; нет и его → поток-владелец окна переднего плана (способ Phase 2B).</item>
/// </list>
/// Документация: GetGUIThreadInfo, GUITHREADINFO, GetKeyboardLayout (learn.microsoft.com, winuser.h).
/// </summary>
public sealed partial class Win32LayoutProbe : ILayoutProbe
{
    public LayoutSample Sample()
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0)
        {
            return default;
        }

        uint ownerTid = GetWindowThreadProcessId(hwnd, out uint pid);
        if (ownerTid == 0)
        {
            return new LayoutSample(hwnd, pid, 0, HklSource.None);
        }

        nint ownerHkl = GetKeyboardLayout(ownerTid);
        var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
        if (GetGUIThreadInfo(0, ref info))
        {
            foreach (var (candidate, source) in new[] { (info.hwndFocus, HklSource.FocusThread), (info.hwndActive, HklSource.ActiveThread) })
            {
                if (candidate == 0)
                {
                    continue;
                }

                uint tid = GetWindowThreadProcessId(candidate, out _);
                if (tid != 0)
                {
                    return new LayoutSample(hwnd, pid, GetKeyboardLayout(tid), source, tid != ownerTid, ownerHkl);
                }
            }
        }

        return new LayoutSample(hwnd, pid, ownerHkl, HklSource.OwnerThread, false, ownerHkl);
    }

    public IReadOnlyList<nint> InstalledLayouts()
    {
        int n = GetKeyboardLayoutList(0, null);
        if (n <= 0)
        {
            return [];
        }

        var buffer = new nint[n];
        n = GetKeyboardLayoutList(n, buffer);
        return n <= 0 ? [] : buffer[..n];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int cbSize;
        public int flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public int left, top, right, bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll")]
    private static partial nint GetKeyboardLayout(uint idThread);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

    [LibraryImport("user32.dll")]
    private static partial int GetKeyboardLayoutList(int nBuff, [Out] nint[]? lpList);
}
