using System.Runtime.InteropServices;

namespace KeyOverlay.Visual.Presentation;

/// <summary>Прямоугольник в физических пикселях экрана (координаты могут быть отрицательными).</summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromPoints(int x1, int y1, int x2, int y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

    /// <summary>Ограничить прямоугольником <paramref name="bounds"/> со сдвигом внутрь (размер сохраняется, если помещается).</summary>
    public PixelRect ClampInto(PixelRect bounds)
    {
        int w = Math.Min(Width, bounds.Width), h = Math.Min(Height, bounds.Height);
        int x = Math.Clamp(Left, bounds.Left, bounds.Right - w);
        int y = Math.Clamp(Top, bounds.Top, bounds.Bottom - h);
        return new PixelRect(x, y, w, h);
    }

    public PixelRect CenteredAt(int cx, int cy) => this with { Left = cx - (Width / 2), Top = cy - (Height / 2) };
}

/// <summary>Мониторы, указатель и позиционирование окон в физических пикселях (процесс Per-Monitor V2).</summary>
public static partial class Screens
{
    public static (int X, int Y) Cursor() => GetCursorPos(out var p) ? (p.X, p.Y) : (0, 0);

    /// <summary>Монитор под точкой: вся область и масштаб (1.0 = 96 DPI).</summary>
    public static (PixelRect Bounds, PixelRect Work, double Scale) MonitorAt(int x, int y)
    {
        nint m = MonitorFromPoint(new Pt { X = x, Y = y }, 2 /* NEAREST */);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (m == 0 || !GetMonitorInfoW(m, ref info))
        {
            return (new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0);
        }

        double scale = GetDpiForMonitor(m, 0, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;
        return (Rect(info.Monitor), Rect(info.Work), scale);

        static PixelRect Rect(R r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Переместить окно в физических пикселях поверх остальных, без активации.</summary>
    public static void Place(nint hwnd, PixelRect r, bool activate = false) =>
        SetWindowPos(hwnd, -1 /* HWND_TOPMOST */, r.Left, r.Top, r.Width, r.Height, 0x0040 /* SHOWWINDOW */ | (activate ? 0u : 0x0010u /* NOACTIVATE */));

    public static bool IsWindow(nint hwnd) => hwnd != 0 && IsWindowNative(hwnd);

    public static long ExStyle(nint hwnd) => GetWindowLongPtrW(hwnd, -20);

    public static void SetExStyle(nint hwnd, long style) => SetWindowLongPtrW(hwnd, -20, (nint)style);

    public const long WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExTopmost = 0x8, WsExLayered = 0x80000, WsExNoActivate = 0x08000000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Pt
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct R
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public R Monitor;
        public R Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Pt p);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(Pt pt, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowNative(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}

/// <summary>
/// Magnification API (Magnification.dll, Windows 7+, только процесс той же разрядности — здесь x64). Элемент «Magnifier»
/// (WC_MAGNIFIER) — дочернее окно, которое система заполняет живым изображением области экрана; содержимое в памяти, на диск
/// и в сеть ничего не попадает. Исключение собственных окон — <c>MagSetWindowFilterList</c> (MW_FILTERMODE_EXCLUDE).
/// </summary>
internal static unsafe partial class MagnifierApi
{
    public const string WindowClass = "Magnifier";
    public const int FilterModeExclude = 0;

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagInitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagUninitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagSetWindowSource(nint hwnd, Screens.R rect);

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagSetWindowTransform(nint hwnd, float* matrix);

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagSetWindowFilterList(nint hwnd, int mode, int count, nint* windows);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint CreateWindowExW(uint exStyle, string className, string? windowName, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InvalidateRect(nint hwnd, nint rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveWindow(nint hwnd, int x, int y, int w, int h, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? name);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint GetProcAddress(nint module, string name);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSmall;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(in WndClassEx wc);

    [LibraryImport("gdi32.dll")]
    public static partial nint GetStockObject(int index);
}
