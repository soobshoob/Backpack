using Backpack.Visual.Presentation;

namespace Backpack.Visual.Tests;

/// <summary>
/// «Снимок поверх окон» (SDK 1.3): геометрия обычного и развёрнутого вида в физических пикселях — пропорции, пределы, углы,
/// развёртывание на 85–95 % рабочей области, изменение размера, перенос между мониторами с разным DPI, отрицательные координаты.
/// Чистые функции — без окон и экрана.
/// </summary>
public class SnapshotGeometryTests
{
    // Основной 100 %, справа 4K на 150 %, слева монитор на 125 % с отрицательными координатами.
    private static readonly PixelRect PrimaryWork = new(0, 0, 1920, 1040);
    private static readonly PixelRect HiDpiWork = new(1920, 0, 3840, 2100);
    private static readonly PixelRect LeftWork = new(-2560, -200, 2560, 1400);

    private static SnapshotLimits Limits(PixelRect work, double scale) => SnapshotLimits.For(work, scale, 120, 0.9);

    [Fact]
    public void Limits_scale_with_dpi_and_never_exceed_the_work_area_share()
    {
        var l = Limits(PrimaryWork, 1);
        Assert.Equal(new SnapshotLimits(120, 1728, 936), l);
        var hi = Limits(HiDpiWork, 1.5);
        Assert.Equal(180, hi.Min);                       // 120 DIP на 150 %
        Assert.Equal(3456, hi.MaxWidth);
        var tiny = SnapshotLimits.For(new PixelRect(0, 0, 100, 80), 2, 600, 1);
        Assert.Equal(80, tiny.Min);                      // максимум сильнее минимума
    }

    [Fact]
    public void Limit_keeps_the_aspect_ratio_and_respects_min_and_max()
    {
        var l = Limits(PrimaryWork, 1);
        var (w, h) = SnapshotGeometry.Limit(3000, 0, 1600, 900, true, l);
        Assert.True(w <= l.MaxWidth && h <= l.MaxHeight, $"{w}×{h}");
        Assert.Equal(SnapshotGeometry.HeightFor(w, 1600, 900), h);
        Assert.Equal(936, h);

        (w, h) = SnapshotGeometry.Limit(50, 0, 1600, 900, true, l);
        Assert.True(Math.Min(w, h) >= l.Min - 1, $"{w}×{h}");
        Assert.InRange(w / (double)h, 1.76, 1.79);

        // Свободные пропорции: по каждой оси отдельно.
        Assert.Equal((1728, 120), SnapshotGeometry.Limit(5000, 10, 1600, 900, false, l));
    }

    [Fact]
    public void New_snapshot_goes_to_the_chosen_corner_without_upscaling()
    {
        var l = Limits(PrimaryWork, 1);
        var r = SnapshotGeometry.Start(SnapshotPlace.Corner, "topRight", 1200, 800, new PixelRect(100, 100, 1200, 800), null, PrimaryWork, 1, 420, 24, true, l);
        Assert.Equal(new PixelRect(1476, 24, 420, 280), r);

        // Снимок меньше начального размера не растягивается (резкость 1:1), но не меньше минимума.
        var small = SnapshotGeometry.Start(SnapshotPlace.Corner, "topRight", 200, 100, new PixelRect(0, 0, 200, 100), null, PrimaryWork, 1, 420, 24, true, l);
        Assert.Equal(240, small.Width);
        Assert.Equal(120, small.Height);

        // 150 %: размер и отступ в DIP → физические пиксели монитора.
        var hi = SnapshotGeometry.Start(SnapshotPlace.Corner, "bottomLeft", 1200, 800, default, null, HiDpiWork, 1.5, 420, 24, true, Limits(HiDpiWork, 1.5));
        Assert.Equal(new PixelRect(1920 + 36, 2100 - 36 - 420, 630, 420), hi);

        // Отрицательные координаты монитора слева.
        var left = SnapshotGeometry.Start(SnapshotPlace.Corner, "topLeft", 1000, 1000, default, null, LeftWork, 1.25, 420, 24, true, Limits(LeftWork, 1.25));
        Assert.Equal(new PixelRect(-2560 + 30, -200 + 30, 525, 525), left);
    }

    [Fact]
    public void New_snapshot_can_appear_over_the_capture_or_where_the_previous_one_was()
    {
        var l = Limits(PrimaryWork, 1);
        var area = new PixelRect(300, 200, 800, 600);
        Assert.Equal(area, SnapshotGeometry.Start(SnapshotPlace.Capture, "topRight", 800, 600, area, null, PrimaryWork, 1, 420, 24, true, l));

        // Весь экран поверх себя — в пределах максимума, пропорции те же.
        var full = SnapshotGeometry.Start(SnapshotPlace.Capture, "topRight", 1920, 1040, PrimaryWork, null, PrimaryWork, 1, 420, 24, true, l);
        Assert.Equal(new PixelRect(0, 0, 1728, 936), full);

        // Замена: место и ширина прежние, высота — по пропорциям нового снимка.
        var last = new PixelRect(500, 300, 600, 400);
        Assert.Equal(new PixelRect(500, 300, 600, 300), SnapshotGeometry.Start(SnapshotPlace.Last, "topRight", 1000, 500, area, last, PrimaryWork, 1, 420, 24, true, l));
        // Без прежнего места — как «в углу».
        Assert.Equal(new PixelRect(1476, 24, 420, 280), SnapshotGeometry.Start(SnapshotPlace.Last, "topRight", 1200, 800, area, null, PrimaryWork, 1, 420, 24, true, l));
    }

    [Fact]
    public void Expanded_view_fills_85_to_95_percent_keeps_aspect_and_is_centered()
    {
        var normal = new PixelRect(1476, 24, 420, 280);
        var e = SnapshotGeometry.Expanded(normal, 1200, 800, PrimaryWork, 1, 48, upscale: true);
        Assert.Equal(new PixelRect(252, 48, 1416, 944), e);
        Assert.InRange(e.Height / (double)PrimaryWork.Height, 0.85, 0.95);
        Assert.Equal(PrimaryWork.Left + (PrimaryWork.Width / 2), e.Left + (e.Width / 2));
        Assert.Equal(SnapshotGeometry.HeightFor(e.Width, 1200, 800), e.Height);

        var hi = SnapshotGeometry.Expanded(new PixelRect(2000, 100, 600, 400), 1200, 800, HiDpiWork, 1.5, 48, upscale: true);
        Assert.Equal(72 + HiDpiWork.Top, hi.Top);                                    // отступ 48 DIP × 150 %
        Assert.InRange(hi.Height / (double)HiDpiWork.Height, 0.85, 0.95);
        Assert.True(hi.Left >= HiDpiWork.Left && hi.Right <= HiDpiWork.Right);

        // Широкий снимок: заполняет ширину.
        var wide = SnapshotGeometry.Expanded(normal, 4000, 1000, PrimaryWork, 1, 48, upscale: true);
        Assert.InRange(wide.Width / (double)PrimaryWork.Width, 0.85, 0.951);

        // Повтор не зависит от прежнего развёртывания (детерминированно от обычного вида).
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(e, SnapshotGeometry.Expanded(normal, 1200, 800, PrimaryWork, 1, 48, upscale: true));
        }
    }

    [Fact]
    public void Expanded_view_without_upscale_stops_at_the_original_size_but_never_shrinks()
    {
        var e = SnapshotGeometry.Expanded(new PixelRect(10, 10, 400, 300), 400, 300, PrimaryWork, 1, 48, upscale: false);
        Assert.Equal((400, 300), (e.Width, e.Height));
        var shown = SnapshotGeometry.Expanded(new PixelRect(10, 10, 800, 600), 400, 300, PrimaryWork, 1, 48, upscale: false);
        Assert.Equal((800, 600), (shown.Width, shown.Height));          // уже видно вдвое — не меньше
        Assert.Equal(PrimaryWork.Width / 2, shown.Left + (shown.Width / 2));
    }

    [Fact]
    public void Resize_by_the_corner_grip_keeps_aspect_limits_and_screen_edge()
    {
        var l = Limits(PrimaryWork, 1);
        var start = new PixelRect(100, 100, 400, 300);
        Assert.Equal(new PixelRect(100, 100, 464, 348), SnapshotGeometry.Resize(start, 100, 0, 400, 300, true, l, PrimaryWork));
        Assert.Equal(new PixelRect(100, 100, 464, 348), SnapshotGeometry.Resize(start, 0, 133, 400, 300, true, l, PrimaryWork)); // любое направление — по диагонали

        var tiny = SnapshotGeometry.Resize(start, -1000, -1000, 400, 300, true, l, PrimaryWork);
        Assert.Equal(120, Math.Min(tiny.Width, tiny.Height));
        Assert.Equal(start.Left, tiny.Left);
        Assert.Equal(start.Top, tiny.Top);

        var atEdge = SnapshotGeometry.Resize(new PixelRect(1700, 100, 200, 150), 1000, 0, 400, 300, true, l, PrimaryWork);
        Assert.Equal(1920, atEdge.Right);
        Assert.Equal(165, atEdge.Height);

        // Свободные пропорции.
        Assert.Equal(new PixelRect(100, 100, 450, 120), SnapshotGeometry.Resize(start, 50, -200, 400, 300, false, l, PrimaryWork));
    }

    [Fact]
    public void Move_stays_inside_the_desktop_and_dpi_rescale_keeps_the_dip_size()
    {
        var all = new PixelRect(LeftWork.Left, LeftWork.Top, HiDpiWork.Right - LeftWork.Left, 2300);
        var moved = SnapshotGeometry.Move(new PixelRect(1476, 24, 420, 280), 100000, -100000, all);
        Assert.Equal(all.Right, moved.Right);
        Assert.Equal(all.Top, moved.Top);
        Assert.Equal((420, 280), (moved.Width, moved.Height));

        // 100 % → 150 %: физический размер × 1.5 вокруг центра.
        var r = new PixelRect(2400, 400, 400, 300);
        var up = SnapshotGeometry.Rescale(r, 1.5, 400, 300, true, Limits(HiDpiWork, 1.5), HiDpiWork);
        Assert.Equal(new PixelRect(2300, 325, 600, 450), up);
        var back = SnapshotGeometry.Rescale(up, 1 / 1.5, 400, 300, true, Limits(PrimaryWork, 1), new PixelRect(0, 0, 6000, 3000));
        Assert.Equal((400, 300), (back.Width, back.Height));            // туда и обратно — без роста

        // Обычный вид после смены монитора — целиком в рабочей области.
        var fit = SnapshotGeometry.Fit(new PixelRect(1800, 900, 400, 300), 400, 300, true, Limits(PrimaryWork, 1), PrimaryWork);
        Assert.Equal(new PixelRect(1520, 740, 400, 300), fit);
    }

    [Fact]
    public void Contains_uses_half_open_bounds()
    {
        var r = new PixelRect(-10, -10, 20, 20);
        Assert.True(SnapshotGeometry.Contains(r, (-10, -10)));
        Assert.True(SnapshotGeometry.Contains(r, (9, 9)));
        Assert.False(SnapshotGeometry.Contains(r, (10, 0)));
    }
}
