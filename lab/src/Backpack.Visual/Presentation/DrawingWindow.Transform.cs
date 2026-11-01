using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Backpack.Visual.Presentation;

/// <summary>Вид жеста преобразования выбранного объекта.</summary>
public enum TransformGesture { None, Resize, Rotate, ArcPoint }

/// <summary>
/// Phase 4I, п. 5–7: дуги и углы умного карандаша, построение фигур с Shift/Alt, выделение с ручками изменения размера и
/// поворота, ручки трёх точек дуги.
/// <para>
/// <b>Правила преобразования.</b> Объект меняется заменой на преобразованную копию — одна запись истории на завершённый жест
/// (отмена возвращает исходный объект, Esc во время жеста ничего не меняет). У фигур (линии, стрелки, дуги, прямоугольники,
/// эллипсы, многоугольники, свободные штрихи) преобразуется геометрия, а толщина линии остаётся прежней — как задано на панели;
/// повёрнутый прямоугольник или эллипс становится контуром с той же геометрией. Текст масштабируется только равномерно вместе
/// с размером шрифта и поворачивается целиком. Номера масштабируются (размер круга), но не поворачиваются — цифра остаётся
/// прямой. Снимок экрана рисует те же элементы, поэтому экспорт совпадает с экраном.
/// </para>
/// <para>
/// Ручки трёх точек дуги меняют её форму (начало, середина, конец); рамка с ручками масштабирует и поворачивает дугу целиком.
/// </para>
/// </summary>
public sealed partial class DrawingWindow
{
    /// <summary>Три точки дуги (координаты элемента; на холсте — плюс смещение Canvas.Left/Top).</summary>
    internal sealed record ArcPoints(Point A, Point M, Point B);

    /// <summary>Концы стрелки (координаты элемента).</summary>
    internal sealed record ArrowEnds(Point From, Point To, double Thickness);

    private sealed class FrameTurn
    {
        public double Degrees;
    }

    private static readonly ConditionalWeakTable<FrameworkElement, ArcPoints> s_arcs = new();
    private static readonly ConditionalWeakTable<FrameworkElement, ArrowEnds> s_arrows = new();
    private static readonly ConditionalWeakTable<FrameworkElement, FrameTurn> s_orientation = new();

    private const double HandleSize = 9, HandleHit = 11, RotateArm = 28;

    private readonly List<FrameworkElement> _handles = new();
    private TransformGesture _gesture;
    private (int X, int Y) _handle;
    private int _arcPoint;
    private Point _gestureGrab, _gesturePointer;
    private FrameworkElement? _gesturePreview;
    private (Point Center, Size Size, double Angle) _gestureFrame;
    private Point _lastPointer;
    private TextBlock? _angleLabel;
    private int? _angleShown;

    /// <summary>Идёт жест изменения размера, поворота или точки дуги.</summary>
    public TransformGesture Gesture => _gesture;

    /// <summary>Подпись величины угла сейчас видна (для проверок).</summary>
    public string? AngleLabelText => _angleLabel is { Visibility: Visibility.Visible } l ? l.Text : null;

    internal static ArcPoints? ArcOf(FrameworkElement e) => s_arcs.TryGetValue(e, out var a) ? a : null;

    /// <summary>Поворот рамки объекта, градусы (0 — без поворота).</summary>
    internal static double OrientationOf(FrameworkElement e) => s_orientation.TryGetValue(e, out var o) ? o.Degrees : 0;

    private static void SetOrientation(FrameworkElement e, double degrees)
    {
        double d = degrees % 360;
        s_orientation.AddOrUpdate(e, new FrameTurn { Degrees = d < -180 ? d + 360 : d > 180 ? d - 360 : d });
    }

    // ── дуга и угол ──────────────────────────────────────────────────────────

    internal static Path MakeArc(Point a, Point m, Point b)
    {
        var p = new Path { Data = DrawingGeometry.ArcGeometry(a, m, b) };
        s_arcs.AddOrUpdate(p, new ArcPoints(a, m, b));
        return p;
    }

    internal static void RememberArrow(FrameworkElement e, Point from, Point to, double thickness) => s_arrows.AddOrUpdate(e, new ArrowEnds(from, to, thickness));

    /// <summary>Перенести сведения о дуге, стрелке и повороте на копию объекта (копирование через XAML их не переносит).</summary>
    private static void CopyMeta(FrameworkElement from, FrameworkElement to)
    {
        if (s_arcs.TryGetValue(from, out var a))
        {
            s_arcs.AddOrUpdate(to, a);
        }

        if (s_arrows.TryGetValue(from, out var r))
        {
            s_arrows.AddOrUpdate(to, r);
        }

        if (s_orientation.TryGetValue(from, out var o))
        {
            SetOrientation(to, o.Degrees);
        }
    }

    /// <summary>
    /// Подпись величины угла во время штриха (только пока кнопка мыши нажата): у вершины на биссектрисе, не под указателем и
    /// в пределах окна; целые градусы с защитой от дрожания. Меньший угол между сторонами.
    /// </summary>
    private void ShowAngleLabel(RecognizedShape? c, Point pointer)
    {
        if (c is not { Kind: RecognizedKind.Angle, Vertices: [var a, var v, var b] })
        {
            HideAngleLabel();
            return;
        }

        _angleShown = DrawingGeometry.StableDegrees(c.AngleDegrees, _angleShown);
        if (_angleLabel is null)
        {
            _angleLabel = AngleLabelBlock("");
            _angleLabel.Tag = "angle-preview";
            _chrome.Children.Add(_angleLabel);
        }

        _angleLabel.Text = _angleShown + "°";
        _angleLabel.Visibility = Visibility.Visible;
        _angleLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var place = DrawingGeometry.AngleLabelPlace(a, v, b, pointer, _angleLabel.DesiredSize, new Rect(0, 0, Math.Max(1, _ink.ActualWidth > 0 ? _ink.ActualWidth : Width), Math.Max(1, _ink.ActualHeight > 0 ? _ink.ActualHeight : Height)));
        Canvas.SetLeft(_angleLabel, place.X);
        Canvas.SetTop(_angleLabel, place.Y);
    }

    private void HideAngleLabel()
    {
        if (_angleLabel is not null)
        {
            _angleLabel.Visibility = Visibility.Collapsed;
        }

        _angleShown = null;
    }

    private static TextBlock AngleLabelBlock(string text) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x1C, 0x1C, 0x21)),
        Padding = new Thickness(8, 3, 8, 3),
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        FontFamily = new FontFamily("Segoe UI"),
        IsHitTestVisible = false,
    };

    // ── модификаторы построения ──────────────────────────────────────────────

    /// <summary>Состояние модификаторов для тестов (null — клавиатура).</summary>
    internal ModifierKeys? ModifiersForTest { get; set; }

    private bool ShiftDown => ((ModifiersForTest ?? Keyboard.Modifiers) & ModifierKeys.Shift) != 0;

    private bool AltDown => ((ModifiersForTest ?? Keyboard.Modifiers) & ModifierKeys.Alt) != 0;

    /// <summary>Тест: сменить модификаторы во время жеста (как нажатие или отпускание Shift/Alt).</summary>
    internal void SetModifiersForTest(ModifierKeys keys)
    {
        ModifiersForTest = keys;
        if (_current is not null)
        {
            Extend(_lastPointer);
        }
        else if (_gesture != TransformGesture.None)
        {
            UpdateGesture(_gesturePointer);
        }
    }

    /// <summary>Изменились Shift или Alt во время жеста: перестроить предпросмотр той же фигуры (новой фигуры нет, исходная точка та же).</summary>
    private bool OnModifierChanged(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is not (Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt))
        {
            return false;
        }

        if (_current is not null && System.Windows.Input.Mouse.LeftButton == MouseButtonState.Pressed)
        {
            Extend(_lastPointer);
        }
        else if (_gesture != TransformGesture.None)
        {
            UpdateGesture(_gesturePointer);
        }
        else
        {
            return key is Key.LeftAlt or Key.RightAlt && Settings.Tool != Backpack.Visual.Settings.DrawTool.Text; // Alt не открывает системное меню окна рисования
        }

        return true;
    }

    // ── рамка выделения и ручки ──────────────────────────────────────────────

    /// <summary>Рамка объекта: центр, размер в осях объекта и поворот (градусы).</summary>
    internal static (Point Center, Size Size, double Angle) FrameOf(FrameworkElement e)
    {
        double angle = OrientationOf(e);
        Rect local;
        if (e is Shape s && GeometryOf(s) is { } g)
        {
            var back = Matrix.Identity;
            back.Rotate(-angle);
            var copy = g.Clone();
            copy.Transform = new MatrixTransform(g.Transform.Value * back);
            local = copy.GetRenderBounds(new Pen(Brushes.Black, Math.Max(1, s.StrokeThickness)));
            if (local.IsEmpty)
            {
                local = copy.Bounds;
            }
        }
        else
        {
            var m = PlacementOf(e);
            var size = SizeOf(e);
            var back = Matrix.Identity;
            back.Rotate(-angle);
            local = Rect.Empty;
            foreach (var c in new[] { new Point(0, 0), new Point(size.Width, 0), new Point(0, size.Height), new Point(size.Width, size.Height) })
            {
                local.Union((m * back).Transform(c));
            }
        }

        var rot = Matrix.Identity;
        rot.Rotate(angle);
        var center = rot.Transform(new Point(local.X + (local.Width / 2), local.Y + (local.Height / 2)));
        return (center, new Size(Math.Max(1, local.Width), Math.Max(1, local.Height)), angle);
    }

    /// <summary>Размещение элемента-не фигуры (текст, номер) на холсте: RenderTransform, затем смещение.</summary>
    private static Matrix PlacementOf(FrameworkElement e)
    {
        var m = e.RenderTransform?.Value ?? Matrix.Identity;
        var o = Offset(e);
        m.Translate(o.X, o.Y);
        return m;
    }

    private static Size SizeOf(FrameworkElement e)
    {
        if (e.ActualWidth <= 0)
        {
            e.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return new Size(e.ActualWidth > 0 ? e.ActualWidth : e.DesiredSize.Width, e.ActualHeight > 0 ? e.ActualHeight : e.DesiredSize.Height);
    }

    private static Point FramePoint((Point Center, Size Size, double Angle) f, double hx, double hy, double extraY = 0)
    {
        var m = Matrix.Identity;
        m.Rotate(f.Angle);
        var v = m.Transform(new Vector(hx * f.Size.Width / 2, (hy * f.Size.Height / 2) + extraY));
        return f.Center + v;
    }

    private static readonly (int X, int Y)[] ResizeHandles = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)];

    /// <summary>Объект можно поворачивать (номер — нет: цифра остаётся прямой).</summary>
    private static bool CanRotate(FrameworkElement e) => e is not MarkerElement;

    /// <summary>У объекта сохраняются пропорции при любом изменении размера (текст, номер).</summary>
    private static bool KeepsAspect(FrameworkElement e) => e is TextBlock or MarkerElement;

    /// <summary>Рамка выделения с ручками: восемь ручек размера (у маленьких — только углы), ручка поворота, точки дуги.</summary>
    private void DrawHandles()
    {
        foreach (var h in _handles)
        {
            _chrome.Children.Remove(h);
        }

        _handles.Clear();
        if (_selected is null || _gesture != TransformGesture.None && _gesturePreview is null)
        {
            return;
        }

        var element = _gesturePreview ?? _selected.Element;
        var f = FrameOf(element);
        f = (f.Center, new Size(f.Size.Width + 12, f.Size.Height + 12), f.Angle);
        var outline = new Polygon
        {
            Points = [FramePoint(f, -1, -1), FramePoint(f, 1, -1), FramePoint(f, 1, 1), FramePoint(f, -1, 1)],
            Stroke = SelectionInk,
            StrokeThickness = 1.5,
            StrokeDashArray = [4, 3],
            IsHitTestVisible = false,
            Tag = "selection-frame",
        };
        Add(outline);
        bool small = f.Size.Width < 40 || f.Size.Height < 40;
        foreach (var (x, y) in ResizeHandles.Where(h => !small || (h.X != 0 && h.Y != 0)))
        {
            Add(HandleDot(FramePoint(f, x, y), square: true, $"resize:{x},{y}"));
        }

        if (CanRotate(element))
        {
            var top = FramePoint(f, 0, -1);
            var knob = FramePoint(f, 0, -1, -RotateArm);
            Add(new Line { X1 = top.X, Y1 = top.Y, X2 = knob.X, Y2 = knob.Y, Stroke = SelectionInk, StrokeThickness = 1.5, IsHitTestVisible = false, Tag = "rotate-arm" });
            Add(HandleDot(knob, square: false, "rotate"));
        }

        if (ArcOf(element) is { } arc)
        {
            var o = Offset(element);
            int i = 0;
            foreach (var p in new[] { arc.A, arc.M, arc.B })
            {
                Add(HandleDot(p + o, square: false, $"arc:{i++}", accent: true));
            }
        }

        void Add(FrameworkElement e)
        {
            _handles.Add(e);
            _chrome.Children.Insert(0, e);
        }
    }

    private static FrameworkElement HandleDot(Point at, bool square, string tag, bool accent = false)
    {
        var e = new Rectangle
        {
            Width = HandleSize,
            Height = HandleSize,
            RadiusX = square ? 2 : HandleSize / 2,
            RadiusY = square ? 2 : HandleSize / 2,
            Fill = accent ? SelectionInk : Brushes.White,
            Stroke = accent ? Brushes.White : SelectionInk,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            Tag = tag,
        };
        Canvas.SetLeft(e, at.X - (HandleSize / 2));
        Canvas.SetTop(e, at.Y - (HandleSize / 2));
        return e;
    }

    /// <summary>Ручка под точкой (или null).</summary>
    internal string? HandleAt(Point p)
    {
        foreach (var h in _handles.Where(h => h.Tag is string t && (t.StartsWith("resize:", StringComparison.Ordinal) || t is "rotate" || t.StartsWith("arc:", StringComparison.Ordinal))).Reverse())
        {
            var c = new Point(Canvas.GetLeft(h) + (HandleSize / 2), Canvas.GetTop(h) + (HandleSize / 2));
            if ((c - p).Length <= HandleHit)
            {
                return (string)h.Tag;
            }
        }

        return null;
    }

    /// <summary>Курсор над ручкой: стрелки размера по направлению ручки с учётом поворота, поворот — рука, точка дуги — перекрестие.</summary>
    private Cursor CursorFor(string handle)
    {
        if (handle == "rotate")
        {
            return Cursors.Hand;
        }

        if (handle.StartsWith("arc:", StringComparison.Ordinal))
        {
            return Cursors.Cross;
        }

        var (x, y) = ParseHandle(handle);
        double angle = (Math.Atan2(y, x) * 180 / Math.PI) + (_selected is { } s ? OrientationOf(s.Element) : 0);
        double a = ((angle % 180) + 180) % 180;
        return a < 22.5 || a >= 157.5 ? Cursors.SizeWE : a < 67.5 ? Cursors.SizeNWSE : a < 112.5 ? Cursors.SizeNS : Cursors.SizeNESW;
    }

    private static (int X, int Y) ParseHandle(string tag)
    {
        var parts = tag["resize:".Length..].Split(',');
        return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Наведение в режиме выбора: курсор ручки или перемещения.</summary>
    private void HoverSelect(Point p)
    {
        Cursor = HandleAt(p) is { } h ? CursorFor(h) : HitTest(p) is not null ? Cursors.SizeAll : Cursors.Arrow;
    }

    // ── жесты ────────────────────────────────────────────────────────────────

    /// <summary>Начать жест с ручки выбранного объекта. false — под точкой нет ручки.</summary>
    internal bool BeginGesture(Point p)
    {
        if (_selected is null || HandleAt(p) is not { } h)
        {
            return false;
        }

        _gestureGrab = _gesturePointer = p;
        _gestureFrame = FrameOf(_selected.Element);
        if (h == "rotate")
        {
            _gesture = TransformGesture.Rotate;
        }
        else if (h.StartsWith("arc:", StringComparison.Ordinal))
        {
            _gesture = TransformGesture.ArcPoint;
            _arcPoint = h[^1] - '0';
        }
        else
        {
            _gesture = TransformGesture.Resize;
            _handle = ParseHandle(h);
        }

        CaptureMouse();
        UpdateGesture(p);
        return true;
    }

    /// <summary>Предпросмотр жеста: преобразованная копия вместо оригинала (оригинал скрыт, история не меняется).</summary>
    internal void UpdateGesture(Point p)
    {
        if (_selected is not { } s || _gesture == TransformGesture.None)
        {
            return;
        }

        _gesturePointer = p;
        FrameworkElement next;
        if (_gesture == TransformGesture.ArcPoint && ArcOf(s.Element) is { } arc)
        {
            var o = Offset(s.Element);
            var pts = new[] { arc.A + o, arc.M + o, arc.B + o };
            pts[_arcPoint] = p;
            next = Styled(MakeArc(pts[0], pts[1], pts[2]), s.Element);
            SetOrientation(next, OrientationOf(s.Element));
        }
        else
        {
            next = Transformed(s.Element, GestureMatrix(p));
            if (_gesture == TransformGesture.Rotate)
            {
                SetOrientation(next, OrientationOf(s.Element) + Rotation(p));
            }
        }

        if (_gesturePreview is not null)
        {
            _ink.Children.Remove(_gesturePreview);
        }

        _gesturePreview = next;
        next.IsHitTestVisible = false;
        _ink.Children.Insert(_ink.Children.IndexOf(s.Element) + 1, next);
        s.Element.Visibility = Visibility.Hidden;
        DrawHandles();
    }

    private double Rotation(Point p) => DrawingGeometry.RotationDegrees(_gestureFrame.Center, _gestureGrab, p, snap: ShiftDown);

    private Matrix GestureMatrix(Point p)
    {
        var f = _gestureFrame;
        if (_gesture == TransformGesture.Rotate)
        {
            var m = Matrix.Identity;
            m.RotateAt(Rotation(p), f.Center.X, f.Center.Y);
            return m;
        }

        var frame = new Rect(f.Center.X - (f.Size.Width / 2), f.Center.Y - (f.Size.Height / 2), f.Size.Width, f.Size.Height);
        bool aspect = ShiftDown || (_selected is { } sel && KeepsAspect(sel.Element));
        return DrawingGeometry.Resize(frame, f.Angle, _handle, p, aspect, AltDown);
    }

    /// <summary>Завершить жест: одна запись истории (исходный объект ↔ преобразованный).</summary>
    internal void EndGesture()
    {
        if (_selected is not { } s || _gesture == TransformGesture.None)
        {
            return;
        }

        var preview = _gesturePreview;
        _gesture = TransformGesture.None;
        _gesturePreview = null;
        ReleaseMouseCapture();
        s.Element.Visibility = Visibility.Visible;
        if (preview is null || (_gesturePointer - _gestureGrab).Length < 1)
        {
            if (preview is not null)
            {
                _ink.Children.Remove(preview);
            }

            DrawHandles();
            return;
        }

        _ink.Children.Remove(preview);
        var changed = new DrawnShape(preview, s.FadeAt);
        Remove(s);
        Add(changed);
        _undo.Push([s, changed]);
        _redo.Clear();
        _selected = changed;
        UpdateSelectionBox();
    }

    /// <summary>Отменить текущий жест (Esc): предпросмотр убирается, объект и история не меняются.</summary>
    internal bool CancelGesture()
    {
        if (_gesture == TransformGesture.None)
        {
            return false;
        }

        _gesture = TransformGesture.None;
        if (_gesturePreview is not null)
        {
            _ink.Children.Remove(_gesturePreview);
            _gesturePreview = null;
        }

        ReleaseMouseCapture();
        if (_selected is { } s)
        {
            s.Element.Visibility = Visibility.Visible;
        }

        DrawHandles();
        return true;
    }

    /// <summary>Строящаяся фигура (тесты модификаторов).</summary>
    internal Shape? CurrentShapeForTest => _current;

    internal int UndoDepthForTest => _undo.Count;

    /// <summary>Видимые ручки выделения (тесты).</summary>
    internal IEnumerable<string> HandleTags => _handles.Select(h => h.Tag as string ?? "");

    internal void TypeTextForTest(Point at, string text)
    {
        BeginText(at);
        TextEditor!.Text = text;
        CommitText();
    }

    internal void PlaceMarkerForTest(Point at) => BeginMarker(at);

    /// <summary>Тест: жест от ручки <paramref name="handle"/> (как в <see cref="HandleAt"/>) к точке, с модификаторами из клавиатуры.</summary>
    internal void GestureForTest(string handle, Point to, bool commit = true)
    {
        var at = _handles.First(h => (string?)h.Tag == handle);
        BeginGesture(new Point(Canvas.GetLeft(at) + (HandleSize / 2), Canvas.GetTop(at) + (HandleSize / 2)));
        UpdateGesture(to);
        if (commit)
        {
            EndGesture();
        }
    }

    // ── преобразование объекта ───────────────────────────────────────────────

    /// <summary>Оформление новой фигуры — как у исходной (цвет, толщина, штрих, прозрачность, свечение).</summary>
    private static Shape Styled(Shape target, FrameworkElement source)
    {
        if (source is Shape s)
        {
            target.Stroke = s.Stroke;
            target.StrokeThickness = s.StrokeThickness;
            target.StrokeDashArray = s.StrokeDashArray;
            target.StrokeDashCap = s.StrokeDashCap;
            target.StrokeLineJoin = s.StrokeLineJoin;
            target.StrokeStartLineCap = s.StrokeStartLineCap;
            target.StrokeEndLineCap = s.StrokeEndLineCap;
            target.Fill = s.Fill;
            target.Effect = s.Effect;
        }

        target.Opacity = source.Opacity;
        target.IsHitTestVisible = false;
        return target;
    }

    private static PointCollection Map(PointCollection pts, Vector offset, Matrix t) => new(pts.Select(p => t.Transform(p + offset)));

    /// <summary>
    /// Копия объекта после преобразования <paramref name="t"/> (координаты холста). Геометрия фигур преобразуется, толщина линии
    /// не меняется; текст — равномерно вместе со шрифтом; номер — размер и место, без поворота.
    /// </summary>
    internal static FrameworkElement Transformed(FrameworkElement e, Matrix t)
    {
        var o = Offset(e);
        FrameworkElement result;
        switch (e)
        {
            case Line l:
                var a = t.Transform(new Point(l.X1, l.Y1) + o);
                var b = t.Transform(new Point(l.X2, l.Y2) + o);
                result = Styled(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y }, e);
                break;
            case Polyline pl:
                result = Styled(new Polyline { Points = Map(pl.Points, o, t) }, e);
                break;
            case Polygon pg:
                result = Styled(new Polygon { Points = Map(pg.Points, o, t) }, e);
                break;
            case Path when ArcOf(e) is { } arc:
                result = Styled(MakeArc(t.Transform(arc.A + o), t.Transform(arc.M + o), t.Transform(arc.B + o)), e);
                break;
            case Path when s_arrows.TryGetValue(e, out var arrow):
                var from = t.Transform(arrow.From + o);
                var to = t.Transform(arrow.To + o);
                var path = new Path { Data = ArrowGeometry(from, to, arrow.Thickness) };
                RememberArrow(path, from, to, arrow.Thickness);
                result = Styled(path, e);
                break;
            case Rectangle or Ellipse when t.M12 == 0 && t.M21 == 0 && t.M11 > 0 && t.M22 > 0 && OrientationOf(e) == 0:
                var r = new Rect(o.X, o.Y, ((Shape)e).Width, ((Shape)e).Height);
                r.Transform(t);
                Shape box = e is Rectangle rr ? new Rectangle { RadiusX = EffectiveRadius(rr.RadiusX, r.Width, r.Height), RadiusY = EffectiveRadius(rr.RadiusY, r.Width, r.Height) } : new Ellipse();
                box.Width = r.Width;
                box.Height = r.Height;
                Canvas.SetLeft(box, r.X);
                Canvas.SetTop(box, r.Y);
                result = Styled(box, e);
                break;
            case Shape shape when GeometryOf(shape) is { } g:
                // Повёрнутая или искажённая фигура — контур с той же геометрией (преобразование входит в геометрию, перо — нет).
                var data = g.Clone();
                data.Transform = new MatrixTransform(g.Transform.Value * t);
                result = Styled(new Path { Data = data }, e);
                break;
            case MarkerElement m:
                double k = DrawingGeometry.UniformScale(t);
                var center = t.Transform(m.Center);
                result = new MarkerElement(m.Number, m.Series, Math.Clamp(m.Size * k, 12, 400), m.ColorValue, center) { IsHitTestVisible = false };
                break;
            default:
                // Текст и прочие элементы: размещение целиком (RenderTransform), смещение входит в матрицу.
                var copy = Clone(e);
                var placement = PlacementOf(e) * t;
                copy.RenderTransform = new MatrixTransform(placement);
                Canvas.SetLeft(copy, 0);
                Canvas.SetTop(copy, 0);
                result = copy;
                break;
        }

        CopyMeta(e, result);
        return result;
    }
}
