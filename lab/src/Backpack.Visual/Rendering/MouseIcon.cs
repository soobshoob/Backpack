using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Rendering;

/// <summary>
/// Что показать на значке мыши: нажатые и неизвестные кнопки, направление прокрутки (−1, 0, +1) по осям.
/// Прокрутка — кратковременный импульс, а не нажатие: средняя кнопка и колесо подсвечиваются независимо.
/// </summary>
public readonly record struct MouseIconState(MouseButtons Pressed, MouseButtons Unknown, int WheelV, int WheelH);

/// <summary>
/// Значок мыши — собственная геометрия Backpack (без проводов, не из Icons8 или Keyviz). Сетка 40×56; корпус — овал
/// 28×48. Части рисуются отдельно, поэтому нажатие меняет только свою часть (левую, правую кнопку, колесо, боковую кнопку),
/// а не весь корпус. Четыре семейства — разные способы отрисовки; палитры одного семейства — пресеты, а не новые рендеры.
/// <list type="bullet">
/// <item><b>Контурный</b> — чёткий контур, перегородка кнопок, центральное колесо.</item>
/// <item><b>Сплошной</b> — залитые части с контрастными промежутками, отдельная область колеса.</item>
/// <item><b>Ретро</b> — мягкие заливки: светлые кнопки, более тёмная нижняя часть и колесо, округлый корпус.</item>
/// <item><b>Скетч</b> — «живой» рисованный контур с небольшой асимметрией и выраженным колесом с насечками.</item>
/// </list>
/// Боковые кнопки: скрыть, одна, две, слева или справа (ручная настройка). Верхняя — X2 («вперёд»), нижняя — X1 («назад»);
/// при одной показана X1, и она подсвечивается при нажатии любой из двух.
/// </summary>
public static class MouseIcon
{
    public const double GridWidth = 40;
    public const double GridHeight = 56;

    private static readonly Rect BodyRect = new(6, 4, 28, 48);
    private const double Split = 22;
    private static readonly Rect WheelRect = new(17, 8.5, 6, 11);

    public static Geometry Body { get; } = Frozen(new RectangleGeometry(BodyRect, 14, 17));

    /// <summary>Множитель толщины линий текущего значка (толщина контура стиля / 2,6); задаётся в <see cref="Create"/>.</summary>
    [ThreadStatic]
    private static double s_k;

    private static double K => s_k > 0 ? s_k : 1;

    /// <summary>Значок высотой <paramref name="height"/> DIP. <paramref name="outlineOverride"/> — цвет контура (кейкап в ряду берёт цвет текста).</summary>
    public static FrameworkElement Create(double height, MouseVisual look, MouseIconState state, ThemeColor? outlineOverride = null)
    {
        var canvas = new Canvas { Width = GridWidth, Height = GridHeight };
        var outline = (outlineOverride ?? look.IconForeground).ToFrozenBrush();
        var accent = look.IconAccent.ToFrozenBrush();
        s_k = Math.Clamp(look.OutlineWidth, Limits.MouseOutline.Min, Limits.MouseOutline.Max) / 2.6;
        switch (look.Renderer)
        {
            case MouseRenderer.Solid: Solid(canvas, look, state, outlineOverride, accent); break;
            case MouseRenderer.Retro: Retro(canvas, look, state, accent); break;
            case MouseRenderer.Sketch: Sketch(canvas, look, state, outline, accent); break;
            case MouseRenderer.Neon: Neon(canvas, look, state, outline, accent); break;
            default: Outline(canvas, look, state, outline, accent); break;
        }

        SideButtons(canvas, look, state, outline, accent);
        WheelArrow(canvas, look, state);
        return new Viewbox { Height = height, Width = height * GridWidth / GridHeight, Stretch = Stretch.Uniform, Child = canvas, IsHitTestVisible = false };
    }

    // ── части ────────────────────────────────────────────────────────────────

    public static Geometry LeftButton(double gap = 0) =>
        Frozen(new CombinedGeometry(GeometryCombineMode.Exclude,
            new CombinedGeometry(GeometryCombineMode.Intersect, Body, new RectangleGeometry(new Rect(0, 0, 20 - (gap / 2), Split - (gap / 2)))),
            gap > 0 ? new RectangleGeometry(Inflate(WheelRect, gap), 3 + gap, 3 + gap) : Geometry.Empty));

    public static Geometry RightButton(double gap = 0) =>
        Frozen(new CombinedGeometry(GeometryCombineMode.Exclude,
            new CombinedGeometry(GeometryCombineMode.Intersect, Body, new RectangleGeometry(new Rect(20 + (gap / 2), 0, 20, Split - (gap / 2)))),
            gap > 0 ? new RectangleGeometry(Inflate(WheelRect, gap), 3 + gap, 3 + gap) : Geometry.Empty));

    public static Geometry Lower(double gap = 0) =>
        Frozen(new CombinedGeometry(GeometryCombineMode.Intersect, Body, new RectangleGeometry(new Rect(0, Split + (gap / 2), GridWidth, GridHeight))));

    public static Geometry Wheel => Frozen(new RectangleGeometry(WheelRect, 3, 3));

    public static Rect SideButtonRect(bool upper, SideButtonsSide side)
    {
        double y = upper ? 24.5 : 33;
        return side == SideButtonsSide.Left ? new Rect(2.2, y, 4.6, 7) : new Rect(33.2, y, 4.6, 7);
    }

    // ── семейства ────────────────────────────────────────────────────────────

    private static void Outline(Canvas c, MouseVisual look, MouseIconState st, Brush outline, Brush accent)
    {
        if (look.BodyColor is { A: > 0 } body)
        {
            c.Children.Add(new Path { Data = Body, Fill = body.ToFrozenBrush() });
        }

        PressFill(c, st, MouseButtons.Left, LeftButton(), accent);
        PressFill(c, st, MouseButtons.Right, RightButton(), accent);
        c.Children.Add(new Path { Data = Body, Stroke = outline, StrokeThickness = 2.6 * K });
        c.Children.Add(Line(6, Split, 34, Split, outline, 2.4));
        c.Children.Add(Line(20, 4, 20, WheelRect.Top, outline, 2.4));
        c.Children.Add(Line(20, WheelRect.Bottom, 20, Split, outline, 2.4));
        WheelPart(c, st, outline, accent, 2.2, filled: false);
        UnknownMarks(c, st, outline);
    }

    /// <summary>
    /// Neon (Phase 4I, «Backpack Neon Emerald»): почти чёрный корпус, тонкий контур цвета контура и мягкое свечение того же
    /// цвета (отдельный слой под корпусом — линии и промежутки не размываются). Кнопки, нижняя часть и колесо — отдельные части
    /// с промежутком, поэтому их границы видны. Нажатая часть — умеренная заливка акцентом (60 %) и яркий контур; свечение
    /// сильнее, пока что-то нажато. Неизвестное состояние — пунктир, не заливка. Интенсивность — <see cref="MouseVisual.Glow"/>.
    /// </summary>
    private static void Neon(Canvas c, MouseVisual look, MouseIconState st, Brush outline, Brush accent)
    {
        const double gap = 1.8;
        var bodyFill = (look.BodyColor ?? new ThemeColor(0x0C, 0x12, 0x10)).ToFrozenBrush();
        var bright = ColorMath.Lighten(look.IconForeground, 0.3).ToFrozenBrush();
        bool any = st.Pressed != MouseButtons.None;
        if (look.Glow > 0)
        {
            double g = Math.Clamp(look.Glow, 0, 1);
            c.Children.Add(new Path
            {
                Data = Body,
                Stroke = outline,
                StrokeThickness = 2 * K,
                Tag = "neon-glow",
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = look.IconForeground.ToWpf(),
                    ShadowDepth = 0,
                    BlurRadius = ((any ? 8 : 6) * g) + 1,
                    Opacity = Math.Clamp(g * (any ? 1.6 : 1.2), 0, 1),
                },
            });
        }

        c.Children.Add(new Path { Data = Body, Fill = bodyFill });
        foreach (var (button, geometry) in new[] { (MouseButtons.Left, LeftButton(gap)), (MouseButtons.Right, RightButton(gap)) })
        {
            bool pressed = st.Pressed.HasFlag(button);
            PressFill(c, st, button, geometry, accent, 0.6);
            c.Children.Add(new Path { Data = geometry, Stroke = pressed ? bright : outline, StrokeThickness = (pressed ? 1.8 : 1.3) * K, Tag = "neon-" + button });
        }

        c.Children.Add(new Path { Data = Lower(gap), Stroke = outline, StrokeThickness = 1.3 * K, Opacity = 0.75 });
        bool middle = st.Pressed.HasFlag(MouseButtons.Middle);
        c.Children.Add(new Path { Data = Wheel, Fill = middle ? accent : bodyFill, Stroke = middle ? bright : outline, StrokeThickness = 1.5 * K, Tag = "neon-wheel" });
        c.Children.Add(new Path { Data = Body, Stroke = outline, StrokeThickness = 1.6 * K });
        UnknownMarks(c, st, outline);
    }

    private static void Solid(Canvas c, MouseVisual look, MouseIconState st, ThemeColor? outlineOverride, Brush accent)
    {
        var fill = (outlineOverride ?? look.IconForeground).ToFrozenBrush();
        const double gap = 2.2;
        foreach (var (button, geometry) in new[] { (MouseButtons.Left, LeftButton(gap)), (MouseButtons.Right, RightButton(gap)) })
        {
            c.Children.Add(new Path { Data = geometry, Fill = st.Pressed.HasFlag(button) ? accent : fill });
        }

        c.Children.Add(new Path { Data = Lower(gap), Fill = fill });
        var wheel = new Path { Data = Wheel, Fill = st.Pressed.HasFlag(MouseButtons.Middle) ? accent : fill };
        c.Children.Add(wheel);
        if (look.SolidOutline is { A: > 0 } contour)
        {
            // Контур сплошного значка (Phase 4G): поверх заливок, толщина — общая толщина контура стиля.
            c.Children.Add(new Path { Data = Body, Stroke = contour.ToFrozenBrush(), StrokeThickness = 2.6 * K, Tag = "solid-outline" });
        }
        UnknownMarks(c, st, look.Body.A > 0 ? look.Body.ToFrozenBrush() : Brushes.Gray);
    }

    private static void Retro(Canvas c, MouseVisual look, MouseIconState st, Brush accent)
    {
        var baseColor = look.BodyColor ?? new ThemeColor(0xB8, 0xC4, 0xD6);
        var top = ColorMath.Lighten(baseColor, 0.08).ToFrozenBrush();
        var lower = ColorMath.Darken(baseColor, 0.04).ToFrozenBrush();
        var line = look.IconForeground.ToFrozenBrush();
        c.Children.Add(new Path { Data = Lower(), Fill = lower });
        c.Children.Add(new Path { Data = LeftButton(), Fill = st.Pressed.HasFlag(MouseButtons.Left) ? accent : top });
        c.Children.Add(new Path { Data = RightButton(), Fill = st.Pressed.HasFlag(MouseButtons.Right) ? accent : top });
        c.Children.Add(new Path { Data = Body, Stroke = line, StrokeThickness = 2.4 * K });
        c.Children.Add(Line(7, Split, 33, Split, line, 2.6));
        c.Children.Add(Line(20, 5, 20, Split, line, 1.6));
        c.Children.Add(new Path
        {
            Data = Frozen(new RectangleGeometry(Inflate(WheelRect, 0.6), 3.6, 3.6)),
            Fill = st.Pressed.HasFlag(MouseButtons.Middle) ? accent : line,
        });
        UnknownMarks(c, st, line);
    }

    private static void Sketch(Canvas c, MouseVisual look, MouseIconState st, Brush outline, Brush accent)
    {
        // Неровный контур: точки корпуса со смещениями от детерминированной «руки» (без случайности — значок одинаков).
        var body = Wobbly(BodyRect, 0.9);
        if (look.BodyColor is { A: > 0 } fillColor)
        {
            c.Children.Add(new Path { Data = body, Fill = fillColor.ToFrozenBrush() });
        }

        PressFill(c, st, MouseButtons.Left, LeftButton(), accent, 0.85);
        PressFill(c, st, MouseButtons.Right, RightButton(), accent, 0.85);
        c.Children.Add(new Path { Data = body, Stroke = outline, StrokeThickness = 2.8 * K, StrokeLineJoin = PenLineJoin.Round });
        // Второй неполный штрих — «живая» линия.
        c.Children.Add(new Path { Data = Frozen(Geometry.Parse("M31.5,10 C35,17 35.5,30 34,40")), Stroke = outline, StrokeThickness = 1.4 * K, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        c.Children.Add(new Path { Data = Frozen(Geometry.Parse("M6.5,22.6 C14,21.4 26,23.4 33.6,21.8")), Stroke = outline, StrokeThickness = 2.4 * K, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        c.Children.Add(new Path { Data = Frozen(Geometry.Parse("M20.4,4.5 C19.6,6.5 20.2,7.4 19.8,8.4 M20.2,19.8 C20.6,20.8 19.8,21.6 20.3,22.4")), Stroke = outline, StrokeThickness = 2.2 * K, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        WheelPart(c, st, outline, accent, 2.4, filled: false, ribs: true);
        UnknownMarks(c, st, outline);
    }

    // ── общие элементы ───────────────────────────────────────────────────────

    private static void WheelPart(Canvas c, MouseIconState st, Brush outline, Brush accent, double thickness, bool filled, bool ribs = false)
    {
        bool pressed = st.Pressed.HasFlag(MouseButtons.Middle);
        c.Children.Add(new Path { Data = Wheel, Stroke = outline, StrokeThickness = thickness * K, Fill = pressed ? accent : filled ? outline : null });
        if (ribs)
        {
            for (int i = 0; i < 3; i++)
            {
                double y = WheelRect.Top + 3 + (i * 2.6);
                c.Children.Add(Line(WheelRect.Left + 1.4, y, WheelRect.Right - 1.4, y + 0.3, outline, 1.2));
            }
        }
    }

    private static void SideButtons(Canvas c, MouseVisual look, MouseIconState st, Brush outline, Brush accent)
    {
        if (look.SideButtons == SideButtonsMode.Hidden)
        {
            return;
        }

        var stroke = look.Renderer == MouseRenderer.Solid ? null : outline;
        var idle = look.Renderer switch
        {
            MouseRenderer.Solid => outline,
            MouseRenderer.Retro => ColorMath.Darken(look.BodyColor ?? new ThemeColor(0xB8, 0xC4, 0xD6), 0.1).ToFrozenBrush(),
            _ => look.BodyColor is { A: > 0 } b ? b.ToFrozenBrush() : null,
        };
        var buttons = look.SideButtons == SideButtonsMode.One
            ? new[] { (Upper: false, Pressed: (st.Pressed & (MouseButtons.X1 | MouseButtons.X2)) != 0, Unknown: (st.Unknown & (MouseButtons.X1 | MouseButtons.X2)) != 0) }
            : [(true, st.Pressed.HasFlag(MouseButtons.X2), st.Unknown.HasFlag(MouseButtons.X2)), (false, st.Pressed.HasFlag(MouseButtons.X1), st.Unknown.HasFlag(MouseButtons.X1))];
        foreach (var (upper, pressed, unknown) in buttons)
        {
            var r = SideButtonRect(upper, look.SideButtonsSide);
            var p = new Path
            {
                Data = Frozen(new RectangleGeometry(r, 1.6, 1.6)),
                Fill = pressed ? accent : idle,
                Stroke = stroke ?? (pressed ? accent : outline),
                StrokeThickness = 1.6 * K,
            };
            if (unknown && !pressed)
            {
                p.StrokeDashArray = new DoubleCollection([1.2, 1.2]);
            }

            c.Children.Add(p);
        }
    }

    /// <summary>
    /// Короткая стрелка прокрутки в области колеса: вверх/вниз по вертикали, влево/вправо по горизонтали (если источник сообщил).
    /// Узкая линия поверх колеса — подсветку кнопок не закрывает.
    /// </summary>
    private static void WheelArrow(Canvas c, MouseVisual look, MouseIconState st)
    {
        var brush = look.WheelColor.ToFrozenBrush();
        if (st.WheelV != 0)
        {
            double tip = st.WheelV > 0 ? 6 : 22, tail = st.WheelV > 0 ? 22 : 6, dir = st.WheelV > 0 ? 1 : -1;
            c.Children.Add(Arrow($"M20,{F(tail)} L20,{F(tip)} M15.5,{F(tip + (4.5 * dir))} L20,{F(tip)} L24.5,{F(tip + (4.5 * dir))}", brush));
        }

        if (st.WheelH != 0)
        {
            double tip = st.WheelH > 0 ? 30 : 10, tail = st.WheelH > 0 ? 10 : 30, dir = st.WheelH > 0 ? -1 : 1;
            c.Children.Add(Arrow($"M{F(tail)},14 L{F(tip)},14 M{F(tip + (4.5 * dir))},9.5 L{F(tip)},14 L{F(tip + (4.5 * dir))},18.5", brush));
        }
    }

    private static Path Arrow(string data, Brush brush) => new()
    {
        Data = Frozen(Geometry.Parse(data)),
        Stroke = brush,
        StrokeThickness = 3,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Tag = "wheel-arrow",
    };

    private static void PressFill(Canvas c, MouseIconState st, MouseButtons button, Geometry g, Brush accent, double opacity = 1)
    {
        if (st.Pressed.HasFlag(button))
        {
            c.Children.Add(new Path { Data = g, Fill = accent, Opacity = opacity, Tag = "pressed-" + button });
        }
    }

    /// <summary>Неизвестное состояние — пунктир по контуру части (не изображается нажатием).</summary>
    private static void UnknownMarks(Canvas c, MouseIconState st, Brush outline)
    {
        foreach (var (b, g) in new[] { (MouseButtons.Left, LeftButton(1.4)), (MouseButtons.Right, RightButton(1.4)), (MouseButtons.Middle, Wheel) })
        {
            if (st.Unknown.HasFlag(b) && !st.Pressed.HasFlag(b))
            {
                c.Children.Add(new Path { Data = g, Stroke = outline, StrokeThickness = 1.3, StrokeDashArray = new DoubleCollection([1.4, 1.4]), Tag = "unknown-" + b });
            }
        }
    }

    private static Geometry Wobbly(Rect r, double amount)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            const int n = 48;
            var pts = new List<Point>(n);
            double cx = r.X + (r.Width / 2), cy = r.Y + (r.Height / 2);
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n;
                // Суперэллипс (округлый прямоугольник) с небольшой асимметрией: верх чуть уже, правый бок чуть полнее.
                double cos = Math.Cos(a), sin = Math.Sin(a);
                double ex = Math.Sign(cos) * Math.Pow(Math.Abs(cos), 0.75), ey = Math.Sign(sin) * Math.Pow(Math.Abs(sin), 0.8);
                double jitter = amount * ((Math.Sin(i * 1.7) * 0.6) + (Math.Cos(i * 0.9) * 0.4));
                double rx = (r.Width / 2) + jitter + (cos > 0 ? 0.4 : 0) - (sin < 0 ? 0.6 * -sin : 0);
                double ry = (r.Height / 2) + jitter;
                pts.Add(new Point(cx + (ex * rx), cy + (ey * ry)));
            }

            ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
            ctx.PolyBezierTo(Smooth(pts), isStroked: true, isSmoothJoin: true);
        }

        g.Freeze();
        return g;
    }

    /// <summary>Сглаживание замкнутой ломаной кубическими кривыми (Catmull-Rom → Безье).</summary>
    private static List<Point> Smooth(List<Point> p)
    {
        var result = new List<Point>(p.Count * 3);
        for (int i = 0; i < p.Count; i++)
        {
            var p0 = p[(i - 1 + p.Count) % p.Count];
            var p1 = p[i];
            var p2 = p[(i + 1) % p.Count];
            var p3 = p[(i + 2) % p.Count];
            result.Add(p1 + ((p2 - p0) / 6));
            result.Add(p2 - ((p3 - p1) / 6));
            result.Add(p2);
        }

        return result;
    }

    private static Line Line(double x1, double y1, double x2, double y2, Brush b, double thickness) => new()
    {
        X1 = x1,
        Y1 = y1,
        X2 = x2,
        Y2 = y2,
        Stroke = b,
        StrokeThickness = thickness * K,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private static Rect Inflate(Rect r, double d)
    {
        r.Inflate(d, d);
        return r;
    }

    private static string F(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static Geometry Frozen(Geometry g)
    {
        if (g.CanFreeze)
        {
            g.Freeze();
        }

        return g;
    }
}
