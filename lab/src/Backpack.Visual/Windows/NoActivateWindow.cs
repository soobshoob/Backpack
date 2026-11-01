using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Backpack.Visual.Windows;

/// <summary>
/// Окно, которое никогда не становится активным и пропускает клики: layered + WS_EX_TRANSPARENT + WS_EX_NOACTIVATE +
/// WS_EX_TOOLWINDOW, ответ MA_NOACTIVATE на WM_MOUSEACTIVATE. Те же свойства, что у оверлея стенда
/// (lab/src/Backpack.Win32/SystemProbes.cs), но без зависимости от проекта захвата.
/// </summary>
public class NoActivateWindow : Window
{
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    public NoActivateWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            NativeWindow.ApplyNoActivateClickThrough(Hwnd);
            StylesApplied = NativeWindow.HasNoActivateClickThrough(Hwnd);
            HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
        };
        Closed += (_, _) => Hwnd = 0;
    }

    public nint Hwnd { get; private set; }

    public bool StylesApplied { get; private set; }

    private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            handled = true;
            return MaNoActivate;
        }

        return 0;
    }
}

internal static partial class NativeWindow
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;
    private const long Required = WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate;

    public static void ApplyNoActivateClickThrough(nint hwnd)
    {
        long ex = GetWindowLongPtrW(hwnd, GwlExStyle);
        SetWindowLongPtrW(hwnd, GwlExStyle, (nint)(ex | Required));
    }

    public static bool HasNoActivateClickThrough(nint hwnd) => (GetWindowLongPtrW(hwnd, GwlExStyle) & Required) == Required;

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}
