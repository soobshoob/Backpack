using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4A: геометрия и центрирование подписей кейкапов во всех рендерах, размерах и состояниях (Ctrl/Shift/Win с меткой
/// стороны, Enter, Space, длинные подписи). Проверяется настоящий рендер элементов WPF без вывода на экран.
/// </summary>
public class KeycapGeometryTests
{
    private static readonly KeycapItem[] Items =
    [
        new(1, 1, "Ctrl", true, false, KeySide.Left),
        new(2, 2, "Shift", true, false, KeySide.Right),
        new(3, 3, "Win", true, false, KeySide.Left),
        new(4, 4, "Enter", false, false),
        new(5, 5, "Space", false, false),
        new(6, 6, "S", false, false),
        new(7, 7, "PrintScreen", false, false),
        new(8, 8, "Backspace", false, false),
    ];

    private static KeycapControl Render(KeycapRenderer r, double size, KeycapItem item, LabelMode mode = LabelMode.Mixed)
    {
        var a = AppearanceSettings.Default with { Size = size, Renderer = r, LabelMode = mode, VolEnterShape = true };
        var c = KeycapFactory.Create(r);
        c.Apply(a, item, showBadge: false);
        c.PressOffset = item.IsPressed ? KeycapMetrics.From(a).PressTravel : 0;
        c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        c.Arrange(new Rect(c.DesiredSize));
        c.UpdateLayout();
        return c;
    }

    /// <summary>Прямоугольник содержимого области подписи (группа «символ + текст» или единственный элемент) в её координатах.</summary>
    private static Rect GroupBounds(KeycapControl c)
    {
        var area = c.LabelArea;
        var child = (FrameworkElement)area.Children[0];
        var topLeft = child.TranslatePoint(new Point(0, 0), area);
        return new Rect(topLeft, child.RenderSize);
    }

    [Theory]
    [InlineData(KeycapRenderer.LowProfile)]
    [InlineData(KeycapRenderer.Laptop)]
    [InlineData(KeycapRenderer.Pbt)]
    [InlineData(KeycapRenderer.Volumetric)]
    public void Label_group_is_centered_in_the_label_area(KeycapRenderer r)
    {
        Sta.Run(() =>
        {
            foreach (double size in new[] { 16.0, 32, 64 })
            {
                foreach (var item in Items)
                {
                    foreach (bool pressed in new[] { false, true })
                    {
                        foreach (var mode in new[] { LabelMode.Mixed, LabelMode.Text, LabelMode.Symbol })
                        {
                            var c = Render(r, size, item with { IsPressed = pressed }, mode);
                            var area = c.LabelArea;
                            var g = GroupBounds(c);
                            double tol = Math.Max(1.0, size * 0.03);
                            string what = $"{r}/{size}/{item.Key}/{mode}/{(pressed ? "down" : "up")}";
                            // Подпись шире области (длинные подписи на малом размере) центрируется переполнением — допускается симметрично.
                            Assert.True(Math.Abs((g.Left + (g.Width / 2)) - (area.ActualWidth / 2)) <= tol, what + " X " + g + " in " + area.ActualWidth);
                            Assert.True(Math.Abs((g.Top + (g.Height / 2)) - (area.ActualHeight / 2)) <= tol, what + " Y " + g + " in " + area.ActualHeight);
                        }
                    }
                }
            }
        });
    }

    [Fact]
    public void Side_marker_does_not_shift_the_main_group()
    {
        Sta.Run(() =>
        {
            foreach (var r in new[] { KeycapRenderer.LowProfile, KeycapRenderer.Volumetric })
            {
                var left = GroupBounds(Render(r, 32, new KeycapItem(1, 1, "Shift", true, false, KeySide.Left)));
                var right = Render(r, 32, new KeycapItem(2, 2, "Shift", true, false, KeySide.Right));
                Assert.Equal("R", right.MarkerText);
                var rb = GroupBounds(right);
                Assert.Equal(left.Left, rb.Left, 3);
                Assert.Equal(left.Top, rb.Top, 3);
            }
        });
    }

    [Theory]
    [InlineData(KeycapRenderer.LowProfile)]
    [InlineData(KeycapRenderer.Laptop)]
    [InlineData(KeycapRenderer.Pbt)]
    [InlineData(KeycapRenderer.Minimal)]
    [InlineData(KeycapRenderer.Volumetric)]
    public void Drawing_stays_inside_the_element_in_every_press_state(KeycapRenderer r)
    {
        Sta.Run(() =>
        {
            foreach (double size in new[] { 16.0, 32, 96 })
            {
                foreach (var item in Items)
                {
                    var a = AppearanceSettings.Default with { Size = size, Renderer = r, VolEnterShape = true };
                    double travel = KeycapMetrics.From(a).PressTravel;
                    foreach (double f in new[] { 0, 0.5, 1 })
                    {
                        var c = Render(r, size, item);
                        c.PressOffset = travel * f;
                        c.UpdateLayout();
                        var b = VisualTreeHelper.GetDescendantBounds(c);
                        string what = $"{r}/{size}/{item.Key}/{f}";
                        // Рисунок кейкапа (корпус, грань, обводка) не выходит за элемент — иначе обрезается при анимации.
                        Assert.True(b.Top >= -0.51 && b.Bottom <= c.ActualHeight + 0.51, what + $" Y {b} vs {c.ActualHeight}");
                        Assert.True(b.Left >= -0.51 && b.Right <= c.ActualWidth + 0.51 || r == KeycapRenderer.Minimal, what + $" X {b} vs {c.ActualWidth}");
                    }
                }
            }
        });
    }

    [Fact]
    public void Volumetric_enter_label_moves_with_the_top_face()
    {
        Sta.Run(() =>
        {
            var a = AppearanceSettings.Default with { Size = 48, Renderer = KeycapRenderer.Volumetric, VolEnterShape = true };
            var c = (VolumetricKeycap)Render(KeycapRenderer.Volumetric, 48, new KeycapItem(4, 4, "Enter", false, false));
            Assert.True(c.IsLShape);
            double topBefore = c.TopPolygon.Min(p => p.Y);
            var labelBefore = c.LabelArea.TranslatePoint(new Point(0, 0), c);
            c.PressOffset = KeycapMetrics.From(a).PressTravel;
            c.UpdateLayout();
            double topAfter = c.TopPolygon.Min(p => p.Y);
            var labelAfter = c.LabelArea.TranslatePoint(new Point(0, 0), c);
            Assert.Equal(topAfter - topBefore, labelAfter.Y - labelBefore.Y, 3);
            Assert.Equal(labelBefore.X, labelAfter.X, 3);

            // Безопасная область подписи Enter — справа от выреза: левый край подписи правее выреза.
            double notchRight = c.TopPolygon.Where(p => p.Y < topAfter + 2).Min(p => p.X);
            Assert.True(labelAfter.X >= notchRight - 0.5, $"label {labelAfter.X} notch {notchRight}");
        });
    }

    [Fact]
    public void Hull_of_a_bowtie_is_a_simple_convex_polygon()
    {
        // Самопересекающийся четырёхугольник («бабочка») — прежняя причина щелей у внутреннего угла выреза.
        var hull = VolumetricKeycap.Hull([new Point(0, 0), new Point(10, 10), new Point(10, 0), new Point(0, 10)]);
        Assert.Equal(4, hull.Count);
        double area = 0;
        for (int i = 0; i < hull.Count; i++)
        {
            var p = hull[i];
            var q = hull[(i + 1) % hull.Count];
            area += (p.X * q.Y) - (q.X * p.Y);
        }

        Assert.Equal(100, Math.Abs(area / 2), 6);
    }
}
