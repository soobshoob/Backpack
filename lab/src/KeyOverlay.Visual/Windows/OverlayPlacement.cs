using System.Windows;
using System.Windows.Controls;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Windows;

/// <summary>
/// Положение группы на экране — две разные величины:
/// <list type="bullet">
/// <item><b>Расстояние от края</b> (EdgeX/EdgeY) — отступ от той стороны рабочей области, к которой прижат якорь.
///       По оси, где якорь по центру, не действует (UI это показывает).</item>
/// <item><b>Смещение от якоря</b> (OffsetX/OffsetY) — сдвиг из вычисленной точки: X вправо, Y вниз, отрицательные — влево/вверх.
///       Действует для всех девяти якорей, в том числе для центрального.</item>
/// </list>
/// Окно оверлея — полоса во всю ширину рабочей области (высота — по группе), поэтому по горизонтали группу двигает
/// <see cref="AnchorPanel"/> внутри полосы, а по вертикали — положение полосы. Группа не выходит за рабочую область.
/// </summary>
public static class OverlayPlacement
{
    /// <summary>Полоса окна оверлея в координатах рабочей области (DIP).</summary>
    public static Rect Band(Rect workArea, PlacementSettings p, AppearanceSettings a)
    {
        var m = KeycapMetrics.From(a);
        double height = m.GroupHeight(a.BackgroundEnabled) + (2 * m.OuterMargin);
        double top = AxisStart(Vertical(p.Anchor), workArea.Top, workArea.Height, height, p.EdgeY) + p.OffsetY;
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return new Rect(workArea.Left, top, workArea.Width, height);
    }

    /// <summary>Левый край группы внутри полосы ширины <paramref name="bandWidth"/>.</summary>
    public static double GroupLeft(double bandWidth, double groupWidth, PlacementSettings p)
    {
        double x = AxisStart(HorizontalAxis(p.Anchor), 0, bandWidth, groupWidth, p.EdgeX) + p.OffsetX;
        return Math.Clamp(x, 0, Math.Max(0, bandWidth - groupWidth));
    }

    /// <summary>Прямоугольник группы на экране (для превью положения и индикатора над оверлеем).</summary>
    public static Rect GroupRect(Rect workArea, PlacementSettings p, AppearanceSettings a, double groupWidth)
    {
        var band = Band(workArea, p, a);
        var m = KeycapMetrics.From(a);
        double h = m.GroupHeight(a.BackgroundEnabled);
        return new Rect(band.Left + GroupLeft(band.Width, groupWidth, p), band.Top + m.OuterMargin, groupWidth, h);
    }

    /// <summary>Действует ли расстояние от края по оси X / Y для этого якоря (по центральной оси — нет).</summary>
    public static (bool X, bool Y) EdgeApplies(ScreenAnchor a) => ((int)a % 3 != 1, (int)a / 3 != 1);

    public static HorizontalAlignment Horizontal(ScreenAnchor a) => ((int)a % 3) switch
    {
        0 => HorizontalAlignment.Left,
        1 => HorizontalAlignment.Center,
        _ => HorizontalAlignment.Right,
    };

    public static VerticalAlignment Vertical(ScreenAnchor a) => ((int)a / 3) switch
    {
        0 => VerticalAlignment.Top,
        1 => VerticalAlignment.Center,
        _ => VerticalAlignment.Bottom,
    };

    /// <summary>Точка размещения прямоугольника размера <paramref name="size"/> по якорю внутри <paramref name="area"/>.</summary>
    public static Point Place(Rect area, Size size, ScreenAnchor anchor, double edgeX, double edgeY, double offsetX, double offsetY)
    {
        double x = AxisStart(HorizontalAxis(anchor), area.Left, area.Width, size.Width, edgeX) + offsetX;
        double y = AxisStart(Vertical(anchor), area.Top, area.Height, size.Height, edgeY) + offsetY;
        return new Point(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    }

    private static VerticalAlignment HorizontalAxis(ScreenAnchor a) => ((int)a % 3) switch
    {
        0 => VerticalAlignment.Top,
        1 => VerticalAlignment.Center,
        _ => VerticalAlignment.Bottom,
    };

    private static double AxisStart(VerticalAlignment side, double start, double length, double size, double edge) => side switch
    {
        VerticalAlignment.Top => start + edge,
        VerticalAlignment.Center => start + ((length - size) / 2),
        _ => start + length - edge - size,
    };
}

/// <summary>Панель полосы оверлея: размещает единственный дочерний элемент (группу) по <see cref="OverlayPlacement.GroupLeft"/>.</summary>
public sealed class AnchorPanel : Panel
{
    private PlacementSettings _placement = PlacementSettings.Default;

    public PlacementSettings Placement
    {
        get => _placement;
        set
        {
            _placement = value;
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement c in InternalChildren)
        {
            c.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement c in InternalChildren)
        {
            var d = c.DesiredSize;
            double x = OverlayPlacement.GroupLeft(finalSize.Width, d.Width, _placement);
            c.Arrange(new Rect(x, (finalSize.Height - d.Height) / 2, d.Width, d.Height));
        }

        return finalSize;
    }
}
