using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Presentation;

/// <summary>Итог снимка экрана в режиме рисования.</summary>
public sealed record ScreenshotResult(bool Ok, string? FilePath, bool Copied, string? Error);

/// <summary>
/// Снимок области экрана по явной команде пользователя (кнопка или горячая клавиша в режиме рисования): GDI <c>BitBlt</c> с
/// <c>CAPTUREBLT</c> (включает layered-окна, то есть рисунки), координаты — физические пиксели (процесс Per-Monitor V2), поэтому
/// масштабирование Windows не искажает размер; отрицательные координаты мониторов слева/сверху поддерживаются.
/// Изображение существует в памяти, пока не сохранено в файл и/или не скопировано в буфер обмена; больше нигде не хранится.
/// </summary>
public static partial class ScreenCapture
{
    public static BitmapSource Capture(PixelRect r)
    {
        nint screen = GetDC(0);
        if (screen == 0)
        {
            throw new InvalidOperationException("GetDC failed");
        }

        nint mem = CreateCompatibleDC(screen);
        nint bmp = CreateCompatibleBitmap(screen, r.Width, r.Height);
        nint old = SelectObject(mem, bmp);
        try
        {
            if (!BitBlt(mem, 0, 0, r.Width, r.Height, screen, r.Left, r.Top, 0x00CC0020 /* SRCCOPY */ | 0x40000000 /* CAPTUREBLT */))
            {
                throw new InvalidOperationException("BitBlt failed: " + Marshal.GetLastPInvokeError());
            }

            SelectObject(mem, old);
            var source = Imaging.CreateBitmapSourceFromHBitmap(bmp, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(0, screen);
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int w, int h);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint dst, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);
}

/// <summary>Сохранение и копирование снимка.</summary>
public static class ScreenshotSaver
{
    /// <summary>Имя по дате и времени: Backpack_ГГГГ-ММ-ДД_ЧЧ-ММ-СС.png; при совпадении — суффикс _2, _3…</summary>
    public static string UniquePath(string folder, DateTime now)
    {
        string stem = "Backpack_" + now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
        string path = Path.Combine(folder, stem + ".png");
        for (int i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{stem}_{i}.png");
        }

        return path;
    }

    public static ScreenshotResult Deliver(BitmapSource image, ScreenshotAction action, string folder, DateTime now, Action<BitmapSource>? copy = null)
    {
        string? path = null;
        bool copied = false;
        try
        {
            if (action is ScreenshotAction.Save or ScreenshotAction.SaveAndCopy)
            {
                Directory.CreateDirectory(folder);
                path = UniquePath(folder, now);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(image));
                using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    enc.Save(fs);
                }
            }

            if (action is ScreenshotAction.Copy or ScreenshotAction.SaveAndCopy)
            {
                (copy ?? CopyToClipboard)(image);
                copied = true;
            }

            return new ScreenshotResult(true, path, copied, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException or NotSupportedException or ArgumentException)
        {
            return new ScreenshotResult(false, path, copied, ex.GetType().Name);
        }
    }

    /// <summary>Буфер обмена может быть занят другой программой — несколько коротких попыток.</summary>
    private static void CopyToClipboard(BitmapSource image)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (ExternalException) when (attempt < 4)
            {
                Thread.Sleep(60);
            }
        }
    }
}
