using System.Windows;
using System.Windows.Media;
using Backpack.Visual.Presentation;

namespace Backpack.Visual.Tests;

/// <summary>Phase 4I, п. 5–7: распознавание дуг и углов, дуга по трём точкам, модификаторы построения, матрицы преобразования.</summary>
public class Phase4IDrawingGeometryTests
{
    private static List<Point> ArcStroke(Point c, double r, double fromDeg, double toDeg, double noise, int seed = 1)
    {
        var rnd = new Random(seed);
        int n = 80;
        return Enumerable.Range(0, n).Select(i =>
        {
            double a = (fromDeg + ((toDeg - fromDeg) * i / (n - 1))) * Math.PI / 180;
            double rr = r + ((rnd.NextDouble() - 0.5) * 2 * noise * r);
            return new Point(c.X + (rr * Math.Cos(a)), c.Y + (rr * Math.Sin(a)));
        }).ToList();
    }

    private static List<Point> Polyline(double noise, int seed, params Point[] corners)
    {
        var rnd = new Random(seed);
        var pts = new List<Point>();
        for (int s = 0; s < corners.Length - 1; s++)
        {
            for (int i = 0; i < 40; i++)
            {
                var p = corners[s] + ((corners[s + 1] - corners[s]) * (i / 40.0));
                pts.Add(new Point(p.X + ((rnd.NextDouble() - 0.5) * noise), p.Y + ((rnd.NextDouble() - 0.5) * noise)));
            }
        }

        pts.Add(corners[^1]);
        return pts;
    }

    [Theory]
    [InlineData(0, 60, 0.01)]
    [InlineData(10, 120, 0.02)]
    [InlineData(-30, 150, 0.015)]
    [InlineData(200, 470, 0.01)]
    [InlineData(90, 40, 0.01)]   // против часовой стрелки
    public void Arcs_are_recognized_with_three_points_on_the_circle(double from, double to, double noise)
    {
        var c = new Point(400, 300);
        var r = ShapeRecognizer.Recognize(ArcStroke(c, 150, from, to, noise));
        Assert.NotNull(r);
        Assert.Equal(RecognizedKind.Arc, r!.Kind);
        foreach (var p in new[] { r.From, r.Mid, r.To })
        {
            Assert.InRange((p - c).Length, 140, 160);
        }

        var fit = DrawingGeometry.Arc(r.From, r.Mid, r.To);
        Assert.False(fit.Straight);
        Assert.InRange(fit.Radius, 140, 160);
    }

    [Fact]
    public void Wavy_lines_full_circles_and_tiny_bends_are_not_forced_into_arcs()
    {
        var wave = Enumerable.Range(0, 80).Select(i => new Point(100 + (i * 5), 300 + (40 * Math.Sin(i / 6.0)))).ToList();
        Assert.NotEqual(RecognizedKind.Arc, ShapeRecognizer.Recognize(wave)?.Kind);
        var circle = ArcStroke(new Point(300, 300), 120, 0, 358, 0.01);
        Assert.Equal(RecognizedKind.Ellipse, ShapeRecognizer.Recognize(circle)?.Kind);
        var almostStraight = ArcStroke(new Point(300, 3000), 2700, 260, 280, 0.0005);
        Assert.Equal(RecognizedKind.Line, ShapeRecognizer.Recognize(almostStraight)?.Kind);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(45)]
    [InlineData(30)]
    [InlineData(135)]
    public void Two_connected_rays_are_an_angle_and_the_smaller_angle_is_measured(double degrees)
    {
        var v = new Point(300, 400);
        var a = new Point(v.X + 220, v.Y);
        double rad = -degrees * Math.PI / 180;
        var b = new Point(v.X + (200 * Math.Cos(rad)), v.Y + (200 * Math.Sin(rad)));
        var r = ShapeRecognizer.Recognize(Polyline(3, 7, a, v, b));
        Assert.NotNull(r);
        Assert.Equal(RecognizedKind.Angle, r!.Kind);
        Assert.InRange(r.AngleDegrees, degrees - 3, degrees + 3);
        Assert.InRange((r.Vertices![1] - v).Length, 0, 8);
        // Обход в обратную сторону — тот же меньший угол.
        Assert.InRange(DrawingGeometry.AngleDegrees(b, v, a), degrees - 0.01, degrees + 0.01);
    }

    [Fact]
    public void Three_point_arc_handles_coincident_and_collinear_points_without_jumps()
    {
        var a = new Point(0, 0);
        var b = new Point(200, 0);
        var straight = DrawingGeometry.Arc(a, new Point(100, 0), b);
        Assert.True(straight.Straight);
        Assert.True(DrawingGeometry.Arc(a, a, a).Straight);                     // совпадающие точки
        Assert.True(DrawingGeometry.Arc(a, new Point(100, 0.5), b).Straight);   // почти прямая — отрезок, не огромный радиус
        Assert.IsType<LineGeometry>(DrawingGeometry.ArcGeometry(a, new Point(100, 0.2), b));

        // Плавность: небольшое смещение промежуточной точки меняет геометрию немного.
        var g1 = DrawingGeometry.ArcGeometry(a, new Point(100, 40), b).Bounds;
        var g2 = DrawingGeometry.ArcGeometry(a, new Point(101, 41), b).Bounds;
        Assert.True(Math.Abs(g1.Height - g2.Height) < 3 && Math.Abs(g1.Width - g2.Width) < 3);

        // Большая дуга: промежуточная точка за центром.
        var major = DrawingGeometry.Arc(new Point(0, 0), new Point(50, 120), new Point(100, 0));
        Assert.True(major.LargeArc);
        var minor = DrawingGeometry.Arc(new Point(0, 0), new Point(50, 20), new Point(100, 0));
        Assert.False(minor.LargeArc);
        // Ось Y вниз: середина ниже хорды — по часовой стрелке на экране.
        Assert.Equal(System.Windows.Media.SweepDirection.Counterclockwise, minor.Sweep);
    }

    [Fact]
    public void Angle_label_avoids_the_pointer_and_stays_on_screen_and_is_stable()
    {
        var area = new Rect(0, 0, 800, 600);
        var size = new Size(48, 24);
        var v = new Point(400, 300);
        var a = new Point(600, 300);
        var b = new Point(400, 100);
        var place = DrawingGeometry.AngleLabelPlace(a, v, b, new Point(700, 500), size, area);
        Assert.True(place.X > v.X && place.Y < v.Y); // внутри угла, на биссектрисе
        var avoided = DrawingGeometry.AngleLabelPlace(a, v, b, new Point(place.X + 24, place.Y + 12), size, area);
        Assert.True(avoided.X < v.X && avoided.Y > v.Y); // указатель на месте подписи — по другую сторону вершины
        var corner = DrawingGeometry.AngleLabelPlace(new Point(10, 0), new Point(0, 0), new Point(0, 10), new Point(500, 500), size, area);
        Assert.True(area.Contains(new Rect(corner, size)));

        Assert.Equal(45, DrawingGeometry.StableDegrees(45.4, null));
        Assert.Equal(45, DrawingGeometry.StableDegrees(45.55, 45));             // без дрожания на границе округления
        Assert.Equal(46, DrawingGeometry.StableDegrees(45.7, 45));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Shift_makes_equal_sides_Alt_builds_from_the_centre_and_the_anchor_does_not_move(bool shift, bool alt)
    {
        var anchor = new Point(100, 100);
        var pointer = new Point(160, 130);
        var r = DrawingGeometry.Constrain(anchor, pointer, shift, alt);
        if (shift)
        {
            Assert.Equal(r.Width, r.Height, 6);
        }
        else
        {
            Assert.NotEqual(r.Width, r.Height);
        }

        if (alt)
        {
            Assert.Equal(anchor, new Point(r.X + (r.Width / 2), r.Y + (r.Height / 2)));
        }
        else
        {
            Assert.Equal(anchor, r.TopLeft);
        }

        // Влево-вверх тоже работает (отрицательные размахи).
        var back = DrawingGeometry.Constrain(anchor, new Point(40, 70), shift, alt);
        Assert.True(back.Width > 0 && back.Height > 0);
        Assert.Equal(new Point(100, 100), alt ? new Point(back.X + (back.Width / 2), back.Y + (back.Height / 2)) : back.BottomRight);
    }

    [Fact]
    public void Resize_keeps_the_opposite_handle_and_rotation_keeps_the_centre()
    {
        var frame = new Rect(100, 100, 200, 100);
        // Правый нижний угол → противоположный левый верхний неподвижен.
        var m = DrawingGeometry.Resize(frame, 0, (1, 1), new Point(500, 300), keepAspect: false, fromCenter: false);
        Assert.Equal(new Point(100, 100), m.Transform(new Point(100, 100)));
        Assert.Equal(new Point(500, 300), m.Transform(new Point(300, 200)));
        // Пропорции: масштаб одинаковый.
        var k = DrawingGeometry.Resize(frame, 0, (1, 1), new Point(500, 220), keepAspect: true, fromCenter: false);
        Assert.Equal(k.M11, k.M22, 6);
        // От центра: центр неподвижен.
        var c = DrawingGeometry.Resize(frame, 0, (1, 0), new Point(400, 150), keepAspect: false, fromCenter: true);
        Assert.Equal(new Point(200, 150), c.Transform(new Point(200, 150)));
        // Повёрнутая рамка: противоположная ручка неподвижна и после поворота.
        var rot = Matrix.Identity;
        rot.RotateAt(30, 200, 150);
        var opposite = rot.Transform(new Point(100, 100));
        var r = DrawingGeometry.Resize(frame, 30, (1, 1), rot.Transform(new Point(350, 230)), false, false);
        var moved = r.Transform(opposite);
        Assert.True((moved - opposite).Length < 1e-6);
        // Не выворачивается и не схлопывается.
        var tiny = DrawingGeometry.Resize(frame, 0, (1, 1), new Point(0, 0), false, false);
        Assert.True(tiny.M11 > 0 && tiny.M22 > 0);

        Assert.Equal(90, DrawingGeometry.RotationDegrees(new Point(0, 0), new Point(10, 0), new Point(0, 10), snap: false), 6);
        Assert.Equal(45, DrawingGeometry.RotationDegrees(new Point(0, 0), new Point(10, 0), new Point(10, 9), snap: true), 6);
    }
}
