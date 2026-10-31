using System.Diagnostics;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Mouse;

/// <summary>
/// Геометрия увеличения у курсора — чистые функции (тесты): размер окна вывода в физических пикселях монитора под
/// указателем, сглаживание следования, удержание внутри монитора, источник с центром в реальной точке указателя.
/// </summary>
public static class CursorZoomGeometry
{
    /// <summary>Коэффициент экспоненциального сглаживания за шаг <paramref name="dtMs"/> при постоянной времени <paramref name="tauMs"/>.</summary>
    public static double Alpha(double dtMs, double tauMs) => tauMs <= 0 ? 1 : 1 - Math.Exp(-Math.Max(0, dtMs) / tauMs);

    /// <summary>Окно вывода размера <paramref name="sizePx"/> с центром (cx, cy), сдвинутое внутрь границ монитора.</summary>
    public static PixelRect Output(double cx, double cy, int sizePx, PixelRect monitor) =>
        new PixelRect((int)Math.Round(cx - (sizePx / 2.0)), (int)Math.Round(cy - (sizePx / 2.0)), sizePx, sizePx).ClampInto(monitor);

    /// <summary>
    /// Источник: центр — реальная точка указателя (не сглаженная), размер = вывод / увеличение, внутри монитора. Поэтому центр
    /// увеличенного изображения всегда соответствует точке, где сейчас произойдёт клик.
    /// </summary>
    public static PixelRect Source(int x, int y, PixelRect output, double zoom, PixelRect monitor) =>
        MagnifierSurface.SourceAround(x, y, output, zoom).ClampInto(monitor);

    public static int SizePx(double diameterDip, double scale) => Math.Max(16, (int)Math.Round(diameterDip * scale));
}

/// <summary>
/// Одна сессия увеличения у указателя (Phase 4G; движок возможности overlay.magnifier): <see cref="MagnifierMode.Follow"/> —
/// следует за указателем со сглаживанием; <see cref="MagnifierMode.Pinned"/> — закреплено там, где был указатель при открытии
/// (изображение живое). Режимов внутри нет: удержание, следование и закрепление — разные сессии разных команд плагина, они
/// не мешают друг другу. Окно увеличения прозрачно для мыши; свои окна Backpack в изображение не попадают. Значок мыши у
/// курсора на время увеличения сдвигается или скрывается и затем возвращается.
/// </summary>
public sealed class CursorMagnifier : IDisposable
{
    private readonly Func<IReadOnlyList<MonitorDesc>> _monitors;
    private readonly Func<IEnumerable<nint>> _ownWindows;
    private readonly Func<(int X, int Y)> _cursor;
    private readonly Action<bool, int>? _iconInteraction;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Backpack.Sdk.MagnifierRequest _request;
    private MagnifierSurface? _surface;
    private TimeSpan _lastTick;
    private (double X, double Y) _center;
    private bool _disposed;

    public CursorMagnifier(Backpack.Sdk.MagnifierRequest request, Func<IReadOnlyList<MonitorDesc>> monitors, Func<IEnumerable<nint>>? ownWindows = null,
        Func<(int X, int Y)>? cursor = null, Action<bool, int>? iconInteraction = null)
    {
        _request = request;
        _monitors = monitors;
        _ownWindows = ownWindows ?? (() => []);
        _cursor = cursor ?? Screens.Cursor;
        _iconInteraction = iconInteraction;
        if (request.Mode == Backpack.Sdk.MagnifierMode.Pinned)
        {
            PinnedAt = _cursor();
        }
    }

    public Backpack.Sdk.MagnifierRequest Request => _request;

    public bool Visible => _surface?.IsOpen == true;

    /// <summary>Закреплённая точка (физические пиксели); null — следует за указателем.</summary>
    public (int X, int Y)? PinnedAt { get; }

    public MagnifierSurface? Surface => _surface;

    public void Show()
    {
        if (_disposed || _surface is not null)
        {
            return;
        }

        var p = PinnedAt ?? _cursor();
        _center = (p.X, p.Y);
        _lastTick = _clock.Elapsed;
        _surface = new MagnifierSurface(Shape(_request), Style(_request), _ownWindows);
        _surface.Tick += Step;
        var (source, output, scale) = Compute(p, _center);
        _surface.Open(source, output, scale);
        UpdateIcon(output);
    }

    /// <summary>Новые параметры (размер, увеличение, оформление) — на открытой сессии, без пересоздания окна.</summary>
    public void SetRequest(Backpack.Sdk.MagnifierRequest request)
    {
        _request = request with { Mode = _request.Mode };
        if (_surface is not null)
        {
            _surface.SetStyle(Shape(_request), Style(_request));
            Step();
        }
    }

    private static MagnifierShape Shape(Backpack.Sdk.MagnifierRequest r) => r.Round ? MagnifierShape.Ellipse : MagnifierShape.RoundedRect;

    private static MagnifierFrameStyle Style(Backpack.Sdk.MagnifierRequest r) =>
        new(Math.Min(r.Radius, Math.Min(r.Width, r.Height) / 2), r.BorderWidth, r.BorderColor, r.Shadow, r.Glow, true);

    private (PixelRect Source, PixelRect Output, double Scale) Compute((int X, int Y) pointer, (double X, double Y) center)
    {
        var monitors = _monitors();
        var m = MonitorResolver.UnderCursor(monitors, pointer)
            ?? new MonitorDesc("virtual", "virtual", new PixelRect(pointer.X - 2000, pointer.Y - 2000, 4000, 4000), default, 1, true);
        int w = CursorZoomGeometry.SizePx(_request.Width, m.Scale), h = CursorZoomGeometry.SizePx(_request.Height, m.Scale);
        var output = new PixelRect((int)Math.Round(center.X - (w / 2.0)), (int)Math.Round(center.Y - (h / 2.0)), w, h).ClampInto(m.Bounds);
        var source = CursorZoomGeometry.Source(pointer.X, pointer.Y, output, _request.Zoom, m.Bounds);
        return (source, output, m.Scale);
    }

    /// <summary>Кадр: сглаженное следование (у закреплённой — неподвижно), перенос между мониторами по DPI монитора под точкой.</summary>
    private void Step()
    {
        if (_surface is null || _disposed)
        {
            return;
        }

        var now = _clock.Elapsed;
        var p = PinnedAt ?? _cursor();
        double a = PinnedAt is null ? CursorZoomGeometry.Alpha((now - _lastTick).TotalMilliseconds, _request.SmoothingMs) : 1;
        _lastTick = now;
        _center = (_center.X + ((p.X - _center.X) * a), _center.Y + ((p.Y - _center.Y) * a));
        var (source, output, scale) = Compute(p, _center);
        _surface.SetGeometry(source, output, scale);
        UpdateIcon(output);
    }

    private void UpdateIcon(PixelRect output)
    {
        if (PinnedAt is not null)
        {
            return; // закреплённая область не мешает значку у указателя
        }

        if (_request.HideMouseIcon)
        {
            _iconInteraction?.Invoke(true, 0);
        }
        else
        {
            var p = _cursor();
            _iconInteraction?.Invoke(false, Math.Max(0, output.Right - p.X + 8));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _surface?.Dispose();
        _surface = null;
        if (PinnedAt is null)
        {
            _iconInteraction?.Invoke(false, 0);
        }
    }
}
