using System.Windows;
using System.Windows.Media;

namespace KeyOverlay.Visual.Presentation;

/// <summary>Окружность через три точки (дуга умного карандаша) или прямая, если точки почти на одной линии.</summary>
public readonly record struct ArcFit(Point Center, double Radius, bool LargeArc, SweepDirection Sweep, bool Straight);

/// <summary>
/// Геометрия рисования (Phase 4I): дуги по трём точкам, подгонка окружности, величина угла, ограничения построения фигур
/// клавишами Shift и Alt, преобразования выделенного объекта. Чистые функции без окон — проверяются тестами.
/// </summary>
public static class DrawingGeometry
{
    /// <summary>
    /// Окружность через начало, промежуточную точку и конец. Почти совпадающие или коллинеарные точки дают «прямую» —
    /// без деления на ноль и без скачка к огромному радиусу (дуга плавно становится отрезком).
    /// </summary>
    public static ArcFit Arc(Point a, Point m, Point b)
    {
        var ab = b - a;
        double len2 = Math.Max(ab.LengthSquared, Math.Max((m - a).LengthSquared, (b - m).LengthSquared));
        double cross = Vector.CrossProduct(m - a, b - m);
        // |cross| / len² — синус «изгиба»: меньше 1,5 % — отрезок (радиус был бы больше ~30 длин хорды).
        if (len2 < 1 || Math.Abs(cross) / len2 < 0.015)
        {
            return new ArcFit(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), double.PositiveInfinity, false, SweepDirection.Clockwise, true);
        }

        double d = 2 * ((a.X * (m.Y - b.Y)) + (m.X * (b.Y - a.Y)) + (b.X * (a.Y - m.Y)));
        double a2 = (a.X * a.X) + (a.Y * a.Y), m2 = (m.X * m.X) + (m.Y * m.Y), b2 = (b.X * b.X) + (b.Y * b.Y);
        var c = new Point(((a2 * (m.Y - b.Y)) + (m2 * (b.Y - a.Y)) + (b2 * (a.Y - m.Y))) / d, ((a2 * (b.X - m.X)) + (m2 * (a.X - b.X)) + (b2 * (m.X - a.X))) / d);
        double r = (a - c).Length;
        // Большая дуга — если центр по ту же сторону хорды, что и промежуточная точка.
        bool large = Math.Sign(Vector.CrossProduct(ab, m - a)) == Math.Sign(Vector.CrossProduct(ab, c - a));
        // Ось Y вниз: положительный поворот a→m→b виден на экране как поворот по часовой стрелке.
        var sweep = cross > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        return new ArcFit(c, r, large, sweep, false);
    }

    /// <summary>Геометрия дуги через три точки (или отрезка a–b при почти прямой дуге).</summary>
    public static Geometry ArcGeometry(Point a, Point m, Point b)
    {
        var f = Arc(a, m, b);
        if (f.Straight)
        {
            return new LineGeometry(a, b);
        }

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(a, false, false);
            c.ArcTo(b, new Size(f.Radius, f.Radius), 0, f.LargeArc, f.Sweep, true, true);
        }

        g.Freeze();
        return g;
    }

    /// <summary>
    /// Окружность по точкам методом наименьших квадратов (Каса). null — точки вырождены (на одной прямой или их меньше трёх).
    /// </summary>
    public static (Point Center, double Radius)? FitCircle(IReadOnlyList<Point> pts)
    {
        if (pts.Count < 3)
        {
            return null;
        }

        // Сдвиг к среднему для устойчивости, затем нормальные уравнения для x²+y² + D·x + E·y + F = 0.
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
        double sxx = 0, syy = 0, sxy = 0, sx = 0, sy = 0, sxz = 0, syz = 0, sz = 0;
        foreach (var p in pts)
        {
            double x = p.X - mx, y = p.Y - my, z = (x * x) + (y * y);
            sxx += x * x;
            syy += y * y;
            sxy += x * y;
            sx += x;
            sy += y;
            sxz += x * z;
            syz += y * z;
            sz += z;
        }

        int n = pts.Count;
        // | sxx sxy sx | |D|   |-sxz|
        // | sxy syy sy | |E| = |-syz|
        // | sx  sy  n  | |F|   |-sz |
        double det = Det(sxx, sxy, sx, sxy, syy, sy, sx, sy, n);
        if (Math.Abs(det) < 1e-9)
        {
            return null;
        }

        double dD = Det(-sxz, sxy, sx, -syz, syy, sy, -sz, sy, n) / det;
        double dE = Det(sxx, -sxz, sx, sxy, -syz, sy, sx, -sz, n) / det;
        double dF = Det(sxx, sxy, -sxz, sxy, syy, -syz, sx, sy, -sz) / det;
        double cx = -dD / 2, cy = -dE / 2, r2 = (cx * cx) + (cy * cy) - dF;
        return r2 <= 0 ? null : (new Point(cx + mx, cy + my), Math.Sqrt(r2));

        static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
            (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));
    }

    /// <summary>
    /// Величина угла при вершине <paramref name="v"/> между лучами к <paramref name="a"/> и <paramref name="b"/>, градусы 0…180:
    /// всегда меньший угол между сторонами (ориентация обхода не важна).
    /// </summary>
    public static double AngleDegrees(Point a, Point v, Point b)
    {
        var u = a - v;
        var w = b - v;
        if (u.Length < 1e-9 || w.Length < 1e-9)
        {
            return 0;
        }

        return Math.Abs(Vector.AngleBetween(u, w));
    }

    /// <summary>
    /// Место подписи угла: на биссектрисе внутри угла на расстоянии <paramref name="distance"/> от вершины; если там указатель
    /// (ближе <paramref name="avoid"/>), — по другую сторону вершины. Подпись размера <paramref name="label"/> не выходит за
    /// <paramref name="area"/>. Возвращает левый верхний угол подписи.
    /// </summary>
    public static Point AngleLabelPlace(Point a, Point v, Point b, Point pointer, Size label, Rect area, double distance = 34, double avoid = 44)
    {
        var u = a - v;
        var w = b - v;
        if (u.Length > 0)
        {
            u.Normalize();
        }

        if (w.Length > 0)
        {
            w.Normalize();
        }

        var bis = u + w;
        if (bis.Length < 1e-6)
        {
            bis = new Vector(-u.Y, u.X); // развёрнутый угол — перпендикуляр
        }

        bis.Normalize();
        var center = v + (bis * (distance + (Math.Max(label.Width, label.Height) / 2)));
        var rect = new Rect(center.X - (label.Width / 2), center.Y - (label.Height / 2), label.Width, label.Height);
        var near = rect;
        near.Inflate(avoid / 2, avoid / 2);
        if (near.Contains(pointer))
        {
            center = v - (bis * (distance + (Math.Max(label.Width, label.Height) / 2)));
        }

        double x = Math.Clamp(center.X - (label.Width / 2), area.Left, Math.Max(area.Left, area.Right - label.Width));
        double y = Math.Clamp(center.Y - (label.Height / 2), area.Top, Math.Max(area.Top, area.Bottom - label.Height));
        return new Point(x, y);
    }

    /// <summary>Устойчивое показание угла: целые градусы, новое значение — только при изменении больше 0,6° (без дрожания).</summary>
    public static int StableDegrees(double degrees, int? shown) =>
        shown is { } s && Math.Abs(degrees - s) <= 0.6 ? s : (int)Math.Round(degrees, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Прямоугольник фигуры при построении от <paramref name="anchor"/> к <paramref name="pointer"/>: <paramref name="square"/>
    /// (Shift) — равные стороны (больший размах), <paramref name="fromCenter"/> (Alt) — точка нажатия остаётся центром. Исходная
    /// точка не сдвигается при нажатии и отпускании клавиш — меняется только способ построения.
    /// </summary>
    public static Rect Constrain(Point anchor, Point pointer, bool square, bool fromCenter)
    {
        double dx = pointer.X - anchor.X, dy = pointer.Y - anchor.Y;
        if (square)
        {
            double s = Math.Max(Math.Abs(dx), Math.Abs(dy));
            dx = s * (dx < 0 ? -1 : 1);
            dy = s * (dy < 0 ? -1 : 1);
        }

        return fromCenter
            ? new Rect(new Point(anchor.X - dx, anchor.Y - dy), new Point(anchor.X + dx, anchor.Y + dy))
            : new Rect(anchor, new Point(anchor.X + dx, anchor.Y + dy));
    }

    /// <summary>Конец отрезка с шагом направления <paramref name="stepDegrees"/> (Shift у линии и стрелки).</summary>
    public static Point SnapDirection(Point from, Point to, double stepDegrees = 15)
    {
        var v = to - from;
        if (v.Length < 1e-9)
        {
            return to;
        }

        double a = Math.Atan2(v.Y, v.X), step = stepDegrees * Math.PI / 180;
        a = Math.Round(a / step) * step;
        return from + new Vector(Math.Cos(a) * v.Length, Math.Sin(a) * v.Length);
    }

    /// <summary>
    /// Матрица изменения размера в повёрнутой рамке. <paramref name="frame"/> — рамка без поворота (в осях объекта),
    /// <paramref name="angle"/> — её поворот вокруг центра, градусы; <paramref name="handle"/> — ручка (−1, 0, 1 по осям X и Y;
    /// 0 — сторона без изменения по этой оси); <paramref name="keepAspect"/> — пропорции сохраняются (Shift), <paramref name="fromCenter"/>
    /// — от центра (Alt). Противоположная ручка (или центр) остаётся на месте. Размер не становится меньше 4 DIP и не выворачивается.
    /// </summary>
    public static Matrix Resize(Rect frame, double angle, (int X, int Y) handle, Point pointer, bool keepAspect, bool fromCenter)
    {
        var center = new Point(frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2));
        var toLocal = Matrix.Identity;
        toLocal.RotateAt(-angle, center.X, center.Y);
        var p = toLocal.Transform(pointer);
        double hx = handle.X, hy = handle.Y;
        var fixedPoint = fromCenter ? center : new Point(center.X - (hx * frame.Width / 2), center.Y - (hy * frame.Height / 2));
        double w0 = fromCenter ? frame.Width / 2 : frame.Width, h0 = fromCenter ? frame.Height / 2 : frame.Height;
        double sx = hx == 0 ? 1 : Math.Max(4, (p.X - fixedPoint.X) * hx) / Math.Max(1e-6, w0);
        double sy = hy == 0 ? 1 : Math.Max(4, (p.Y - fixedPoint.Y) * hy) / Math.Max(1e-6, h0);
        if (keepAspect)
        {
            double s = hx == 0 ? sy : hy == 0 ? sx : Math.Max(sx, sy);
            sx = sy = s;
        }

        var m = Matrix.Identity;
        m.RotateAt(-angle, center.X, center.Y);
        m.ScaleAt(sx, sy, fixedPoint.X, fixedPoint.Y);
        m.RotateAt(angle, center.X, center.Y);
        return m;
    }

    /// <summary>Поворот вокруг <paramref name="center"/> от направления на <paramref name="grab"/> к <paramref name="pointer"/>; Shift — шаг 15°.</summary>
    public static double RotationDegrees(Point center, Point grab, Point pointer, bool snap)
    {
        double a = Vector.AngleBetween(grab - center, pointer - center);
        return snap ? Math.Round(a / 15) * 15 : a;
    }

    /// <summary>Равномерный масштаб матрицы (корень из модуля определителя) — для толщины и размера текста.</summary>
    public static double UniformScale(Matrix m) => Math.Sqrt(Math.Abs(m.Determinant));
}
