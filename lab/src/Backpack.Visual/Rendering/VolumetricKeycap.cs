using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Rendering;

/// <summary>
/// Объёмный кейкап — собственный рендер Backpack (не перекрашенный lowprofile), вид спереди-сверху. Клавиша — выдавленный
/// контур в плоскости (u — ширина, v — глубина от задней кромки к передней): нижний контур на z = 0, верхняя грань уже на <c>t</c>
/// и выше на высоту корпуса. Проекция: X = u, Y = v·K − z. Видны верхняя грань, передняя грань и скошенные боковины (задняя
/// скрыта); корпус залит горизонтальным градиентом вторичного цвета (слева светлее), верхняя грань — основной цвет, контур —
/// цвет рамки. Подпись и символ стоят прямо и лежат в плоскости верхней грани (то же преобразование, лёгкое сокращение по
/// высоте). При нажатии верхняя грань опускается, передняя грань и боковины укорачиваются, подпись движется вместе с гранью.
/// Формы: квадратная (1 единица), компактная удлинённая (1.5 — служебные клавиши и пробел) и Г-образный Enter (1.5 с вырезом
/// сзади слева, если включено в стиле). Высота ряда не зависит от ширины клавиши.
/// </summary>
public sealed class VolumetricKeycap : KeycapControl
{
    private const int CornerSegments = 6;

    /// <summary>Наименьший вертикальный масштаб подписи на верхней грани.</summary>
    public const double LabelScaleY = 0.85;

    private readonly Path _body = new();
    private readonly Path _top = new();
    private readonly Path _outline = new() { StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Canvas _plane = new() { ClipToBounds = false };
    private readonly MatrixTransform _planeTransform = new();
    private readonly MatrixTransform _markerTransform = new();
    private double _units = 1;
    private bool _lShape;
    private Rect _contentRect;

    public VolumetricKeycap()
    {
        ContentHost.RenderTransform = _planeTransform;
        Marker.RenderTransform = _markerTransform;
        _plane.Children.Add(ContentHost);
        _plane.Children.Add(Marker);
        Children.Add(_body);
        Children.Add(_top);
        Children.Add(_outline);
        Children.Add(_plane);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.Volumetric;

    /// <summary>Ширина в единицах (1 или 1.5) и признак Г-образной формы — для тестов форм.</summary>
    public double Units => _units;

    public bool IsLShape => _lShape;

    /// <summary>Многоугольник верхней грани в координатах элемента (для тестов: подпись внутри грани).</summary>
    public IReadOnlyList<Point> TopPolygon { get; private set; } = [];

    public double TopZ { get; private set; }

    protected override double ContentWidth(KeycapItem item) => _contentRect.Width;

    protected override HorizontalAlignment SmallAlign(KeycapItem item) => HorizontalAlignment.Center;

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        var content = KeyLabels.Resolve(item, s.LabelSet, s.LabelMode, s.SideMarkers);
        _lShape = s.VolEnterShape && item.Key == "Enter" && !item.Numpad;
        _units = _lShape || item.Key == "Space" || content.Layout is CapLayout.TextOnly or CapLayout.GlyphAndText ? 1.5 : 1;

        double u = M.VolUnit, w = _units * u, d = u, t = u * 0.08, pad = M.S * 0.06;
        Width = w + (2 * pad) + (2 * BorderWidth);
        Height = M.Height;
        MinWidth = 0;

        // У Г-образного Enter подпись — в части клавиши на всю глубину (справа от выреза), а не в узкой полосе перед вырезом.
        double notchU = _lShape ? u * 0.5 : 0;
        double inner = M.S * 0.2;
        _contentRect = new Rect(notchU + t + inner, t + inner, Math.Max(1, w - notchU - (2 * t) - (2 * inner)), Math.Max(1, d - (2 * t) - (2 * inner)));
        ContentHost.Width = _contentRect.Width;
        ContentHost.Height = _contentRect.Height;

        _top.Fill = s.Primary.ToFrozenBrush();
        var light = ColorMath.Lighten(s.Secondary, 0.06);
        var dark = ColorMath.Darken(s.Secondary, 0.1);
        var bodyBrush = new LinearGradientBrush(light.ToWpf(), dark.ToWpf(), 0);
        bodyBrush.Freeze();
        _body.Fill = bodyBrush;
        _outline.Stroke = s.BorderEnabled ? s.BorderColor.ToFrozenBrush() : null;
        _outline.StrokeThickness = BorderWidth;
        Marker.Margin = new Thickness(0);
    }

    protected override void OnPressChanged(double offset)
    {
        if (M.S <= 0)
        {
            return;
        }

        double u = M.VolUnit, w = _units * u, d = u, t = u * 0.08, k = KeycapMetrics.VolK;
        double h = M.VolDepth, z = h - offset;
        double pad = (M.S * 0.06) + BorderWidth;
        double ox = pad;
        double projected = (d * k) + h;
        // Запас по вертикали не меньше половины обводки: нижняя кромка не обрезается ни в покое, ни при нажатии.
        double oy = Math.Max(BorderWidth / 2 + 0.5, (Height - projected) / 2) + h;
        Point P(Point p, double zz) => new(ox + p.X, oy + (p.Y * k) - zz);

        var outline = Outline(w, d, _lShape ? new Size(u * 0.5, u * 0.42) : Size.Empty);
        double rTop = Math.Max(M.S * 0.08, M.R * u * 0.28);
        var bottom = Fillet(outline, rTop + t);
        var top = Fillet(Inset(outline, t), rTop);
        var bottomScreen = bottom.Select(p => P(p, 0)).ToArray();
        var topScreen = top.Select(p => P(p, z)).ToArray();
        TopPolygon = topScreen;
        TopZ = z;

        // Корпус: нижний контур, верхний и четырёхугольники боковин — одна фигура с правилом Nonzero (объединение без швов).
        var body = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var g = body.Open())
        {
            Figure(g, bottomScreen, true);
            Figure(g, topScreen, true);
            for (int i = 0; i < bottom.Count; i++)
            {
                int j = (i + 1) % bottom.Count;
                // Выпуклая оболочка боковины: у внутреннего угла выреза Enter контуры разного скругления дают
                // самопересекающийся четырёхугольник («бабочку»), который при объединении оставлял щели.
                Figure(g, Hull([topScreen[i], topScreen[j], bottomScreen[j], bottomScreen[i]]), true);
            }
        }

        body.Freeze();
        _body.Data = body;

        var topGeometry = new StreamGeometry();
        using (var g = topGeometry.Open())
        {
            Figure(g, topScreen, true, keepWinding: true);
        }

        topGeometry.Freeze();
        _top.Data = topGeometry;

        // Контур: верхняя грань целиком, видимые нижние кромки и вертикали там, где боковина переходит из видимой в скрытую.
        var lines = new StreamGeometry();
        using (var g = lines.Open())
        {
            Figure(g, topScreen, true, keepWinding: true);
            for (int i = 0; i < bottom.Count; i++)
            {
                int j = (i + 1) % bottom.Count;
                int prev = (i - 1 + bottom.Count) % bottom.Count;
                bool visible = Faces(bottom[i], bottom[j]);
                bool prevVisible = Faces(bottom[prev], bottom[i]);
                // Кромки основания, заслонённые самим корпусом (стенка выреза Enter за передней левой частью), не рисуются.
                var mid = new Point((bottom[i].X + bottom[j].X) / 2, (bottom[i].Y + bottom[j].Y) / 2);
                if (visible && !Occluded(outline, mid, Normal(bottom[i], bottom[j]), z, k, u))
                {
                    Figure(g, [bottomScreen[i], bottomScreen[j]], false);
                }

                if (visible != prevVisible && !Occluded(outline, bottom[i], Normal(bottom[prev], bottom[i]) + Normal(bottom[i], bottom[j]), z, k, u))
                {
                    Figure(g, [bottomScreen[i], topScreen[i]], false);
                }
            }
        }

        lines.Freeze();
        _outline.Data = lines;

        var r = _contentRect;
        // Подпись лежит на верхней грани и движется с ней, но сжимается по высоте не сильнее чем до LabelScaleY:
        // при полном сокращении глубины мелкие подписи (Ctrl, Esc, коды языка) на малых размерах становятся нечитаемыми.
        double ky = Math.Max(k, LabelScaleY);
        double center = r.Height * (k - ky) / 2;
        _planeTransform.Matrix = new Matrix(1, 0, 0, ky, ox + r.X, oy + (r.Y * k) - z + center);
        double mx = (_lShape ? u * 0.5 : 0) + t + (M.S * 0.12), my = t + (M.S * 0.06);
        _markerTransform.Matrix = new Matrix(1, 0, 0, ky, ox + mx, oy + (my * k) - z);
    }

    /// <summary>Видна ли боковина над отрезком: при взгляде спереди-сверху скрыта только обращённая назад (нормаль (dy, −dx), n.v &lt; 0).</summary>
    private static bool Faces(Point a, Point b)
    {
        double dx = b.X - a.X;
        return -dx > -1e-9;
    }

    /// <summary>Прямоугольный контур по часовой стрелке (v вниз); с вырезом сзади слева для Г-образного Enter.</summary>
    private static List<Point> Outline(double w, double d, Size notch) => notch.IsEmpty
        ? [new(0, 0), new(w, 0), new(w, d), new(0, d)]
        : [new(notch.Width, 0), new(w, 0), new(w, d), new(0, d), new(0, notch.Height), new(notch.Width, notch.Height)];

    /// <summary>Сдвиг прямоугольного контура внутрь на <paramref name="t"/> (у прямых углов — по сумме нормалей сторон).</summary>
    private static List<Point> Inset(List<Point> poly, double t)
    {
        var result = new List<Point>(poly.Count);
        for (int i = 0; i < poly.Count; i++)
        {
            var prev = poly[(i - 1 + poly.Count) % poly.Count];
            var cur = poly[i];
            var next = poly[(i + 1) % poly.Count];
            var n1 = Normal(prev, cur);
            var n2 = Normal(cur, next);
            result.Add(new Point(cur.X - (t * (n1.X + n2.X)), cur.Y - (t * (n1.Y + n2.Y))));
        }

        return result;
    }

    private static Vector Normal(Point a, Point b)
    {
        var dir = b - a;
        dir.Normalize();
        return new Vector(dir.Y, -dir.X);
    }

    /// <summary>Скругление углов: одинаковое число точек на угол у обоих контуров, поэтому точки соответствуют друг другу.</summary>
    private static List<Point> Fillet(List<Point> poly, double radius)
    {
        var result = new List<Point>(poly.Count * (CornerSegments + 1));
        for (int i = 0; i < poly.Count; i++)
        {
            var prev = poly[(i - 1 + poly.Count) % poly.Count];
            var cur = poly[i];
            var next = poly[(i + 1) % poly.Count];
            var a = prev - cur;
            var b = next - cur;
            double r = Math.Min(radius, Math.Min(a.Length, b.Length) * 0.45);
            a.Normalize();
            b.Normalize();
            var t1 = cur + (a * r);
            var t2 = cur + (b * r);
            for (int s = 0; s <= CornerSegments; s++)
            {
                double q = (double)s / CornerSegments;
                // Квадратичная кривая через вершину: для прямого угла визуально совпадает с дугой.
                var p = new Point(
                    ((1 - q) * (1 - q) * t1.X) + (2 * (1 - q) * q * cur.X) + (q * q * t2.X),
                    ((1 - q) * (1 - q) * t1.Y) + (2 * (1 - q) * q * cur.Y) + (q * q * t2.Y));
                result.Add(p);
            }
        }

        return result;
    }

    private static void Figure(StreamGeometryContext g, IReadOnlyList<Point> pts, bool closed, bool keepWinding = false)
    {
        if (pts.Count < 2)
        {
            return;
        }

        IEnumerable<Point> ordered = pts;
        if (closed && !keepWinding && SignedArea(pts) < 0)
        {
            ordered = pts.Reverse();
        }

        var list = ordered.ToList();
        g.BeginFigure(list[0], isFilled: closed, isClosed: closed);
        g.PolyLineTo(list.Skip(1).ToList(), isStroked: true, isSmoothJoin: true);
    }

    /// <summary>
    /// Точка основания (план, z = 0) заслонена корпусом: на луче взгляда (v + z/K при 0 &lt; z ≤ h) есть точка внутри контура
    /// клавиши. Точка берётся чуть снаружи по нормали <paramref name="outward"/>, чтобы граница контура не считалась «внутри».
    /// </summary>
    internal static bool Occluded(IReadOnlyList<Point> plan, Point p, Vector outward, double h, double k, double unit)
    {
        if (outward.Length > 0)
        {
            outward.Normalize();
        }

        var q = p + (outward * unit * 0.02);
        for (int i = 1; i <= 12; i++)
        {
            double z = h * i / 12;
            if (Inside(plan, new Point(q.X, q.Y + (z / k))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Отрезок закрыт верхней гранью: его середина строго внутри многоугольника грани (с запасом 0.5 DIP от края).</summary>
    internal static bool Covered(IReadOnlyList<Point> top, Point a, Point b)
    {
        var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return Inside(top, m) && Inside(top, m + new Vector(0.5, 0)) && Inside(top, m - new Vector(0.5, 0))
            && Inside(top, m + new Vector(0, 0.5)) && Inside(top, m - new Vector(0, 0.5));
    }

    private static bool Inside(IReadOnlyList<Point> poly, Point p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                && p.X < ((poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y)) + poly[i].X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>Выпуклая оболочка (монотонная цепь Эндрю) — простой многоугольник с положительной площадью.</summary>
    internal static List<Point> Hull(IReadOnlyList<Point> points)
    {
        var p = points.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3)
        {
            return p;
        }

        static double Cross(Point o, Point a, Point b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
        var h = new List<Point>();
        foreach (var pass in new[] { p, Enumerable.Reverse(p).ToList() })
        {
            int start = h.Count;
            foreach (var q in pass)
            {
                while (h.Count >= start + 2 && Cross(h[^2], h[^1], q) <= 0)
                {
                    h.RemoveAt(h.Count - 1);
                }

                h.Add(q);
            }

            h.RemoveAt(h.Count - 1);
        }

        return h;
    }

    private static double SignedArea(IReadOnlyList<Point> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += (p.X * q.Y) - (q.X * p.Y);
        }

        return a / 2;
    }
}
