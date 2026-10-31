using System.Windows;

namespace KeyOverlay.Visual.Presentation;

public enum RecognizedKind { Line, Ellipse, Rectangle, Polygon, Arc, Angle }

/// <summary>
/// Распознанная фигура: прямая (From → To), фигура в прямоугольнике <see cref="Bounds"/>, многоугольник с вершинами
/// <see cref="Vertices"/> (в порядке обхода штриха), дуга (From → <see cref="Mid"/> → To) или угол (Vertices: конец первого луча,
/// вершина, конец второго); ошибка — меньше лучше.
/// </summary>
public sealed record RecognizedShape(RecognizedKind Kind, Rect Bounds, Point From, Point To, double Error, IReadOnlyList<Point>? Vertices = null, Point Mid = default)
{
    /// <summary>Величина угла (только <see cref="RecognizedKind.Angle"/>): меньший угол между сторонами, градусы.</summary>
    public double AngleDegrees => Kind == RecognizedKind.Angle && Vertices is [var a, var v, var b] ? DrawingGeometry.AngleDegrees(a, v, b) : 0;
}

/// <summary>
/// «Умный карандаш»: распознаёт в штрихе прямую, окружность/эллипс, прямоугольник и многоугольник (3–8 вершин, в том
/// числе повёрнутые четырёхугольники и треугольники — это распознавание нарисованного, а не построение правильного
/// многоугольника: вершины берутся из штриха). Локальная
/// геометрия, без отправки штрихов куда-либо. При низкой уверенности возвращает null — остаётся исходная линия.
/// Произвольные рисунки и стрелки не распознаются (стрелка — отдельный инструмент). Пороги — в долях размера штриха,
/// поэтому не зависят от масштаба; минимальная длина задаётся в DIP (учитывает DPI окна).
/// </summary>
public static class ShapeRecognizer
{
    public const double MinLengthDip = 28;
    private const double LineTolerance = 0.06;
    private const double EllipseTolerance = 0.16;
    private const double RectTolerance = 0.075;

    public static RecognizedShape? Recognize(IReadOnlyList<Point> raw)
    {
        var pts = Dedup(raw);
        if (pts.Count < 3)
        {
            return null;
        }

        double length = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            length += (pts[i] - pts[i - 1]).Length;
        }

        var bounds = Bounds(pts);
        double diag = Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height));
        if (length < MinLengthDip || diag < MinLengthDip * 0.7)
        {
            return null;
        }

        var first = pts[0];
        var last = pts[^1];
        double gap = (last - first).Length;
        bool closed = gap <= Math.Max(18, length * 0.18);

        if (!closed)
        {
            // Прямая: все точки близко к хорде, а сам штрих почти равен хорде (без петель и возвратов).
            double chord = gap;
            if (chord < MinLengthDip)
            {
                return null;
            }

            double maxDev = pts.Max(p => DistanceToSegment(p, first, last));
            double straightness = length / chord;
            double err = maxDev / chord;
            if (err <= LineTolerance && straightness <= 1.12)
            {
                return new RecognizedShape(RecognizedKind.Line, bounds, first, last, err);
            }

            // Phase 4I: угол (два прямых луча с общей вершиной), затем дуга. Не уверены — остаётся штрих.
            // Дуга, упрощённая до трёх точек, похожа на угол (прогиб хорд ~7 %): если штрих ложится на окружность, угол
            // выбирается, только когда оба луча действительно прямые (прогиб ≤ 4 %).
            var arc = Arc(pts, bounds, chord, length);
            return Angle(pts, bounds, diag, arc is null ? 0.12 : 0.04) ?? arc;
        }

        double minSide = Math.Min(bounds.Width, bounds.Height);
        if (minSide < MinLengthDip * 0.5 || Math.Max(bounds.Width, bounds.Height) / Math.Max(1, minSide) > 12)
        {
            return null; // вытянутая петля — не фигура
        }

        // Эллипс в прямоугольнике штриха: средняя невязка нормированного уравнения.
        double cx = bounds.X + (bounds.Width / 2), cy = bounds.Y + (bounds.Height / 2), rx = bounds.Width / 2, ry = bounds.Height / 2;
        double ellipseErr = pts.Average(p =>
        {
            double u = (p.X - cx) / rx, v = (p.Y - cy) / ry;
            return Math.Abs(Math.Sqrt((u * u) + (v * v)) - 1);
        });

        // Прямоугольник: точки близко к сторонам и все четыре угла пройдены.
        double rectErr = pts.Average(p => Math.Min(Math.Min(Math.Abs(p.X - bounds.Left), Math.Abs(p.X - bounds.Right)), Math.Min(Math.Abs(p.Y - bounds.Top), Math.Abs(p.Y - bounds.Bottom)))) / minSide;
        double cornerReach = minSide * 0.22;
        bool corners = new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight }.All(c => pts.Any(p => (p - c).Length <= cornerReach));

        bool isRect = corners && rectErr <= RectTolerance;
        bool isEllipse = ellipseErr <= EllipseTolerance;
        if (isRect && (!isEllipse || rectErr * 2.2 < ellipseErr))
        {
            return new RecognizedShape(RecognizedKind.Rectangle, bounds, first, last, rectErr);
        }

        // Многоугольник: упрощение замкнутого штриха (Рамер — Дуглас — Пекер) до немногих вершин с острыми углами; точки штриха
        // должны лежать близко к сторонам. Окружность упрощается в много вершин и сюда не попадает.
        if (ellipseErr >= RoundError && Polygon(pts, diag) is { } poly && (!isEllipse || poly.Error * 2.4 < ellipseErr))
        {
            return new RecognizedShape(RecognizedKind.Polygon, bounds, first, last, poly.Error, poly.Vertices);
        }

        return isEllipse ? new RecognizedShape(RecognizedKind.Ellipse, bounds, first, last, ellipseErr) : null;
    }

    /// <summary>
    /// Угол: незамкнутый штрих из двух прямых отрезков с общей вершиной (лучи). Каждый отрезок прямой (точки близко к нему),
    /// оба не короче половины минимальной длины, угол между ними 8…172° (почти развёрнутый — это прямая, почти нулевой —
    /// возврат по той же линии).
    /// </summary>
    private static RecognizedShape? Angle(List<Point> pts, Rect bounds, double diag, double maxSag)
    {
        var simple = Rdp(pts, diag * 0.06);
        if (simple.Count != 3)
        {
            return null;
        }

        var (a, v, b) = (simple[0], simple[1], simple[2]);
        double la = (a - v).Length, lb = (b - v).Length;
        if (Math.Min(la, lb) < MinLengthDip * 0.5)
        {
            return null;
        }

        // Вершина — пересечение прямых, подогнанных к точкам каждого луча (рука скругляет угол).
        var refined = Refine(pts, [a, v, b]);
        v = refined.Count == 3 ? refined[1] : v;
        double deg = DrawingGeometry.AngleDegrees(a, v, b);
        if (deg is < 8 or > 172)
        {
            return null;
        }

        double err = pts.Average(p => Math.Min(DistanceToSegment(p, a, v), DistanceToSegment(p, v, b))) / diag;
        double sag = pts.Max(p => Math.Min(DistanceToSegment(p, a, v), DistanceToSegment(p, v, b))) / Math.Min(la, lb);
        return err <= 0.03 && sag <= maxSag ? new RecognizedShape(RecognizedKind.Angle, bounds, a, b, err, [a, v, b]) : null;
    }

    /// <summary>
    /// Дуга: незамкнутый штрих, который хорошо ложится на окружность (средняя невязка ≤ 4,5 % радиуса, наибольшая ≤ 12 %),
    /// проходится монотонно по углу (без возвратов) на 25…300°, заметно изогнут (прогиб ≥ 8 % хорды — иначе это почти прямая)
    /// и почти без пересечений. Три точки дуги: начало и конец штриха и середина пути, спроецированные на окружность.
    /// </summary>
    private static RecognizedShape? Arc(List<Point> pts, Rect bounds, double chord, double length)
    {
        if (pts.Count < 6 || DrawingGeometry.FitCircle(pts) is not var (c, r) || r > chord * 8)
        {
            return null;
        }

        double mean = pts.Average(p => Math.Abs((p - c).Length - r)) / r;
        double worst = pts.Max(p => Math.Abs((p - c).Length - r)) / r;
        if (mean > 0.045 || worst > 0.12)
        {
            return null;
        }

        double sagitta = pts.Max(p => DistanceToSegment(p, pts[0], pts[^1])) / chord;
        if (sagitta < 0.08)
        {
            return null;
        }

        // Монотонный ход по углу: суммарный поворот и отсутствие заметных возвратов.
        double total = 0, back = 0, prev = Math.Atan2(pts[0].Y - c.Y, pts[0].X - c.X);
        foreach (var p in pts.Skip(1))
        {
            double a = Math.Atan2(p.Y - c.Y, p.X - c.X);
            double d = Math.Atan2(Math.Sin(a - prev), Math.Cos(a - prev));
            total += d;
            prev = a;
        }

        double dir = Math.Sign(total);
        prev = Math.Atan2(pts[0].Y - c.Y, pts[0].X - c.X);
        foreach (var p in pts.Skip(1))
        {
            double a = Math.Atan2(p.Y - c.Y, p.X - c.X);
            double d = Math.Atan2(Math.Sin(a - prev), Math.Cos(a - prev));
            if (d * dir < 0)
            {
                back += Math.Abs(d);
            }

            prev = a;
        }

        double span = Math.Abs(total) * 180 / Math.PI;
        if (span is < 25 or > 300 || back * 180 / Math.PI > 6)
        {
            return null;
        }

        Point On(Point p)
        {
            var v = p - c;
            return v.Length < 1e-9 ? p : c + (v * (r / v.Length));
        }

        // Середина пути штриха.
        double half = length / 2, walked = 0;
        var mid = pts[^1];
        for (int i = 1; i < pts.Count; i++)
        {
            double seg = (pts[i] - pts[i - 1]).Length;
            if (walked + seg >= half)
            {
                double t = seg <= 0 ? 0 : (half - walked) / seg;
                mid = pts[i - 1] + ((pts[i] - pts[i - 1]) * t);
                break;
            }

            walked += seg;
        }

        return new RecognizedShape(RecognizedKind.Arc, bounds, On(pts[0]), On(pts[^1]), mean, null, On(mid));
    }

    public const int MaxPolygonVertices = 8;
    private const double PolygonTolerance = 0.035;

    /// <summary>Доля поворота штриха, приходящаяся на окрестности вершин (±4 % длины штриха), — не меньше этой.</summary>
    private const double MinCornerTurnShare = 0.62;

    /// <summary>Штрих, который так близко к эллипсу, — круглый (в том числе неровная окружность), а не многоугольник.</summary>
    private const double RoundError = 0.04;

    private static double CornerTurnShare(List<Point> pts, IReadOnlyList<Point> vertices)
    {
        int n = pts.Count;
        if (n < 12)
        {
            return 1;
        }

        var s = new double[n];
        for (int i = 1; i < n; i++)
        {
            s[i] = s[i - 1] + (pts[i] - pts[i - 1]).Length;
        }

        double perimeter = s[^1];
        int w = Math.Max(2, n / 60); // сглаживание дрожания: направление по хорде через несколько точек
        var heading = new double[n];
        for (int i = 0; i < n; i++)
        {
            var d = pts[Math.Min(n - 1, i + w)] - pts[Math.Max(0, i - w)];
            heading[i] = Math.Atan2(d.Y, d.X);
        }

        var turn = new double[n];
        double total = 0;
        for (int i = 1; i < n; i++)
        {
            double t = heading[i] - heading[i - 1];
            t = Math.Abs(Math.Atan2(Math.Sin(t), Math.Cos(t)));
            turn[i] = t;
            total += t;
        }

        if (total <= 0)
        {
            return 0;
        }

        var near = new bool[n];
        foreach (var v in vertices)
        {
            int k = 0;
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double d = (pts[i] - v).Length;
                if (d < best)
                {
                    best = d;
                    k = i;
                }
            }

            for (int i = 0; i < n; i++)
            {
                double ds = Math.Abs(s[i] - s[k]);
                if (Math.Min(ds, perimeter - ds) <= perimeter * 0.04)
                {
                    near[i] = true;
                }
            }
        }

        double atCorners = 0;
        for (int i = 0; i < n; i++)
        {
            if (near[i])
            {
                atCorners += turn[i];
            }
        }

        return atCorners / total;
    }

    /// <summary>Наибольшее расстояние от вершины до штриха (доля диагонали): рука может слегка скруглить угол.</summary>
    private const double MaxCornerGap = 0.035;

    /// <summary>Наибольший допустимый прогиб стороны многоугольника (доля длины стороны).</summary>
    private const double MaxEdgeSag = 0.085;

    private static (IReadOnlyList<Point> Vertices, double Error)? Polygon(List<Point> pts, double diag)
    {
        var closedPath = new List<Point>(pts) { pts[0] };
        var simple = Rdp(closedPath, diag * 0.055);
        if (simple.Count > 1 && (simple[^1] - simple[0]).Length < diag * 0.12)
        {
            simple.RemoveAt(simple.Count - 1);
        }

        // Склеить близкие вершины и убрать почти прямые углы (больше 155°).
        bool changed = true;
        while (changed && simple.Count >= 3)
        {
            changed = false;
            for (int i = 0; i < simple.Count; i++)
            {
                var prev = simple[(i - 1 + simple.Count) % simple.Count];
                var cur = simple[i];
                var next = simple[(i + 1) % simple.Count];
                double angle = Vector.AngleBetween(prev - cur, next - cur);
                if ((cur - prev).Length < diag * 0.1 || Math.Abs(angle) > 155)
                {
                    simple.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }

        if (simple.Count is < 3 or > MaxPolygonVertices)
        {
            return null;
        }

        simple = Refine(pts, simple);

        // Углы пройдены: штрих проходит рядом с каждой вершиной. У окружности вершины пересечения касательных лежат снаружи.
        if (simple.Any(v => pts.Min(p => (p - v).Length) > diag * MaxCornerGap))
        {
            return null;
        }

        // Поворот сосредоточен у вершин: у многоугольника направление штриха меняется в углах, а вдоль сторон почти постоянно;
        // у окружности (в том числе неровной) — равномерно по всей длине.
        if (CornerTurnShare(pts, simple) < MinCornerTurnShare)
        {
            return null;
        }
        double err = pts.Average(p => Enumerable.Range(0, simple.Count).Min(i => DistanceToSegment(p, simple[i], simple[(i + 1) % simple.Count]))) / diag;

        // Стороны должны быть прямыми: наибольший прогиб точек своей стороны — малая доля её длины. У окружности, упрощённой до
        // нескольких вершин, прогиб дуги ≈ 13 % стороны и больше — она остаётся эллипсом, а не «многоугольником».
        var sag = new double[simple.Count];
        foreach (var p in pts)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < simple.Count; i++)
            {
                double d = DistanceToSegment(p, simple[i], simple[(i + 1) % simple.Count]);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            sag[best] = Math.Max(sag[best], bestD);
        }

        for (int i = 0; i < simple.Count; i++)
        {
            double edge = (simple[(i + 1) % simple.Count] - simple[i]).Length;
            if (edge <= 0 || sag[i] / edge > MaxEdgeSag)
            {
                return null;
            }
        }
        // Каждая сторона должна быть пройдена штрихом (иначе «многоугольником» стала бы любая петля).
        bool sidesCovered = Enumerable.Range(0, simple.Count).All(i =>
        {
            var a = simple[i];
            var b = simple[(i + 1) % simple.Count];
            var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            return pts.Any(p => (p - mid).Length <= diag * 0.12);
        });
        return err <= PolygonTolerance && sidesCovered ? (simple, err) : null;
    }

    /// <summary>
    /// Уточнение вершин: упрощение ставит вершину в одну из точек штриха, не обязательно в угол. Каждая сторона аппроксимируется
    /// прямой по своим точкам (без участков у концов, где рука скругляет угол), вершина — пересечение соседних прямых.
    /// </summary>
    private static List<Point> Refine(IReadOnlyList<Point> pts, List<Point> v)
    {
        int n = v.Count;
        var groups = Enumerable.Range(0, n).Select(_ => new List<Point>()).ToArray();
        foreach (var p in pts)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double d = DistanceToSegment(p, v[i], v[(i + 1) % n]);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            groups[best].Add(p);
        }

        var lines = new (Point C, Vector D)?[n];
        for (int i = 0; i < n; i++)
        {
            var a = v[i];
            var b = v[(i + 1) % n];
            var dir = b - a;
            double len = dir.Length;
            var inner = groups[i].Where(p =>
            {
                double t = Vector.Multiply(p - a, dir) / Math.Max(1e-9, len * len);
                return t is > 0.15 and < 0.85;
            }).ToList();
            if (inner.Count < 3)
            {
                continue;
            }

            double cx = inner.Average(p => p.X), cy = inner.Average(p => p.Y);
            double sxx = inner.Sum(p => (p.X - cx) * (p.X - cx)), syy = inner.Sum(p => (p.Y - cy) * (p.Y - cy)), sxy = inner.Sum(p => (p.X - cx) * (p.Y - cy));
            double angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
            lines[i] = (new Point(cx, cy), new Vector(Math.Cos(angle), Math.Sin(angle)));
        }

        var result = new List<Point>(n);
        for (int i = 0; i < n; i++)
        {
            // Вершина i — между стороной i−1 и стороной i.
            if (lines[(i - 1 + n) % n] is { } l1 && lines[i] is { } l2)
            {
                double cross = (l1.D.X * l2.D.Y) - (l1.D.Y * l2.D.X);
                if (Math.Abs(cross) > 0.2)
                {
                    var w = l2.C - l1.C;
                    double t = ((w.X * l2.D.Y) - (w.Y * l2.D.X)) / cross;
                    var x = l1.C + (l1.D * t);
                    if ((x - v[i]).Length < Math.Max(12, (v[i] - v[(i + 1) % n]).Length * 0.35))
                    {
                        result.Add(x);
                        continue;
                    }
                }
            }

            result.Add(v[i]);
        }

        return result;
    }

    /// <summary>Упрощение ломаной Рамера — Дугласа — Пекера (без рекурсии).</summary>
    private static List<Point> Rdp(IReadOnlyList<Point> pts, double eps)
    {
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int A, int B)>();
        stack.Push((0, pts.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            double best = 0;
            int index = -1;
            for (int i = a + 1; i < b; i++)
            {
                double d = DistanceToSegment(pts[i], pts[a], pts[b]);
                if (d > best)
                {
                    best = d;
                    index = i;
                }
            }

            if (index >= 0 && best > eps)
            {
                keep[index] = true;
                stack.Push((a, index));
                stack.Push((index, b));
            }
        }

        return pts.Where((_, i) => keep[i]).ToList();
    }

    private static List<Point> Dedup(IReadOnlyList<Point> raw)
    {
        var list = new List<Point>(raw.Count);
        foreach (var p in raw)
        {
            if (list.Count == 0 || (p - list[^1]).Length > 0.5)
            {
                list.Add(p);
            }
        }

        return list;
    }

    private static Rect Bounds(IReadOnlyList<Point> pts)
    {
        double l = pts.Min(p => p.X), t = pts.Min(p => p.Y), r = pts.Max(p => p.X), b = pts.Max(p => p.Y);
        return new Rect(l, t, Math.Max(0.001, r - l), Math.Max(0.001, b - t));
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        double len2 = ab.LengthSquared;
        if (len2 <= 0)
        {
            return (p - a).Length;
        }

        double t = Math.Clamp(Vector.Multiply(p - a, ab) / len2, 0, 1);
        return (p - (a + (ab * t))).Length;
    }
}

/// <summary>
/// Устойчивость предварительного показа: кандидат появляется, только если одна и та же фигура распознана несколько раз
/// подряд, и сменяется другой (или исчезает) тоже только после нескольких подряд — без мигания между фигурами при каждом
/// новом движении мыши. Сам показ ничего не добавляет в историю рисования.
/// </summary>
public sealed class ShapeStabilizer
{
    private readonly int _confirm;
    private RecognizedKind? _pendingKind;
    private int _pendingCount;
    private int _missCount;

    public ShapeStabilizer(int confirm = 3) => _confirm = Math.Max(1, confirm);

    /// <summary>Текущий устойчивый кандидат (null — показывать только штрих).</summary>
    public RecognizedShape? Current { get; private set; }

    /// <summary>Сколько раз кандидат сменился (для проверки отсутствия мигания).</summary>
    public int Switches { get; private set; }

    public void Reset()
    {
        Current = null;
        _pendingKind = null;
        _pendingCount = _missCount = 0;
    }

    /// <summary>Очередной результат распознавания по штриху на данный момент.</summary>
    public RecognizedShape? Feed(RecognizedShape? r)
    {
        if (r is null)
        {
            _pendingKind = null;
            _pendingCount = 0;
            if (Current is not null && ++_missCount >= _confirm)
            {
                Current = null;
                _missCount = 0;
                Switches++;
            }

            return Current;
        }

        _missCount = 0;
        if (Current is { } c && c.Kind == r.Kind && SameVertexCount(c, r))
        {
            Current = r; // та же фигура — обновить геометрию
            _pendingKind = null;
            _pendingCount = 0;
            return Current;
        }

        if (_pendingKind == r.Kind)
        {
            _pendingCount++;
        }
        else
        {
            _pendingKind = r.Kind;
            _pendingCount = 1;
        }

        if (_pendingCount >= _confirm)
        {
            Current = r;
            _pendingKind = null;
            _pendingCount = 0;
            Switches++;
        }

        return Current;
    }

    private static bool SameVertexCount(RecognizedShape a, RecognizedShape b) => (a.Vertices?.Count ?? 0) == (b.Vertices?.Count ?? 0);
}
