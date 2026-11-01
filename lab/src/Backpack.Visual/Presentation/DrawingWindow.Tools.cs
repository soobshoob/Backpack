using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Presentation;

/// <summary>
/// Phase 4E: предварительное распознавание умного карандаша, скруглённый прямоугольник и правильный многоугольник, выбор и
/// перемещение нарисованного, полная и компактная панели на иконках Lucide, произвольный цвет (HEX и прозрачность),
/// вложенное радиальное меню.
/// </summary>
public sealed partial class DrawingWindow
{
    private static readonly Brush ToolInk = Frozen(Color.FromRgb(0xF2, 0xF5, 0xF3));
    private static readonly Brush SelectionInk = Frozen(Color.FromRgb(0x31, 0xC9, 0x9A));

    private readonly ShapeStabilizer _stabilizer = new(3);
    private Shape? _candidate;
    private int _recognizeCountdown;
    private DrawnShape? _selected;
    private Point _selectGrab;
    private bool _selectDragging;
    private Popup? _flyout;

    /// <summary>Выбор на панели или в меню изменил инструмент, цвет, толщину и т. п. (приложение запоминает последние значения).</summary>
    public event Action<DrawSettings>? SettingsChanged;

    /// <summary>Пользователь переключил компактную панель (сохраняется в профиле).</summary>
    public event Action<bool>? CompactChanged;

    /// <summary>Каталог радиального меню от приложения (команды реестра); null — только пункты рисования.</summary>
    public Func<IReadOnlyList<RadialNode>>? RadialCatalog { get; set; }

    /// <summary>Устойчивый кандидат предварительного распознавания (null — показывается только штрих).</summary>
    public RecognizedShape? Candidate => _stabilizer.Current;

    /// <summary>Сколько раз сменился показанный кандидат за последний штрих (проверка отсутствия мигания).</summary>
    public int CandidateSwitches => _stabilizer.Switches;

    /// <summary>Штрих по шагам, как движение мыши (тесты предварительного распознавания).</summary>
    internal void BeginStrokeForTest(Point p) => Begin(p);

    internal void ExtendStrokeForTest(Point p) => Extend(p);

    internal void EndStrokeForTest() => End();

    /// <summary>
    /// Иллюстрация справки: настоящее окно рисования (не показывается) в момент штриха умного карандаша — исходный штрих
    /// приглушён, кандидат фигуры показан пунктиром до отпускания кнопки.
    /// </summary>
    public static FrameworkElement PreviewSmartPen()
    {
        var w = new DrawingWindow(new PixelRect(-32000, -32000, 520, 300), 1.0, DrawSettings.Default, DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly });
        var pts = Enumerable.Range(0, 100).Select(i =>
        {
            double a = 2 * Math.PI * i / 112;
            double k = 1 + (0.03 * Math.Sin(a * 5));
            return new Point(260 + (170 * k * Math.Cos(a)), 150 + (95 * k * Math.Sin(a)));
        }).ToList();
        w.ApplyProfile(w.Profile); // только радиальное меню: панели на иллюстрации нет
        w.Begin(pts[0]);
        foreach (var p in pts.Skip(1))
        {
            w.Extend(p);
        }

        w._chrome.Children.Remove(w._hint);
        var root = w._root;
        w.Content = null;
        w.Close();
        root.Width = 520;
        root.Height = 300;
        return root;
    }

    /// <summary>Элемент предварительного показа (не входит в рисунок и историю).</summary>
    internal Shape? CandidateElement => _candidate;

    public FrameworkElement? SelectedElement => _selected?.Element;

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // ── фигуры ───────────────────────────────────────────────────────────────

    /// <summary>Вершины правильного многоугольника: центр, одна вершина (задаёт радиус и поворот), число сторон 3–12.</summary>
    public static PointCollection RegularPolygon(Point center, Point vertex, int sides)
    {
        sides = Math.Clamp(sides, DrawSettings.SidesRange.Min, DrawSettings.SidesRange.Max);
        var v = vertex - center;
        double r = v.Length, a0 = Math.Atan2(v.Y, v.X);
        var pts = new PointCollection(sides);
        for (int i = 0; i < sides; i++)
        {
            double a = a0 + (2 * Math.PI * i / sides);
            pts.Add(new Point(center.X + (r * Math.Cos(a)), center.Y + (r * Math.Sin(a))));
        }

        return pts;
    }

    /// <summary>Скругление, ограниченное половиной меньшей стороны (большие значения не ломают фигуру).</summary>
    public static double EffectiveRadius(double radius, double w, double h) => Math.Clamp(radius, 0, Math.Min(w, h) / 2);

    /// <summary>Редактируемая WPF-фигура для распознанного штриха (без оформления).</summary>
    internal static Shape CreateRecognized(RecognizedShape r)
    {
        Shape shape = r.Kind switch
        {
            RecognizedKind.Line => new Line { X1 = r.From.X, Y1 = r.From.Y, X2 = r.To.X, Y2 = r.To.Y },
            RecognizedKind.Rectangle => new Rectangle { Width = r.Bounds.Width, Height = r.Bounds.Height },
            RecognizedKind.Polygon => new Polygon { Points = new PointCollection(r.Vertices!) },
            RecognizedKind.Arc => MakeArc(r.From, r.Mid, r.To),                                  // Phase 4I: дуга по трём точкам
            RecognizedKind.Angle => new Polyline { Points = new PointCollection(r.Vertices!) },   // Phase 4I: два луча с вершиной
            _ => new Ellipse { Width = r.Bounds.Width, Height = r.Bounds.Height },
        };
        if (r.Kind is RecognizedKind.Rectangle or RecognizedKind.Ellipse)
        {
            Canvas.SetLeft(shape, r.Bounds.X);
            Canvas.SetTop(shape, r.Bounds.Y);
        }

        return shape;
    }

    // ── предварительное распознавание ────────────────────────────────────────

    /// <summary>
    /// Во время штриха умного карандаша: распознавание по уже нарисованным точкам (не на каждое движение), устойчивый кандидат
    /// показывается пунктиром поверх штриха (штрих приглушается, но остаётся виден). При низкой уверенности — только штрих.
    /// Показ не добавляет ничего в рисунок и историю.
    /// </summary>
    private void UpdateCandidate(Polyline stroke)
    {
        if (--_recognizeCountdown > 0)
        {
            return;
        }

        _recognizeCountdown = 3;
        var candidate = _stabilizer.Feed(ShapeRecognizer.Recognize(stroke.Points.ToList()));
        ShowCandidate(candidate, stroke);
        ShowAngleLabel(candidate, stroke.Points.Count > 0 ? stroke.Points[^1] : _lastPointer);
    }

    private void ShowCandidate(RecognizedShape? c, Polyline stroke)
    {
        if (_candidate is not null)
        {
            _chrome.Children.Remove(_candidate);
            _candidate = null;
        }

        stroke.Opacity = c is null ? 1 : 0.45;
        if (c is null)
        {
            return;
        }

        var shape = CreateRecognized(c);
        shape.Stroke = stroke.Stroke;
        shape.StrokeThickness = Math.Max(2, stroke.StrokeThickness * 0.8);
        shape.StrokeDashArray = [2.2, 1.6];
        shape.StrokeDashCap = PenLineCap.Round;
        shape.StrokeLineJoin = PenLineJoin.Round;
        shape.IsHitTestVisible = false;
        shape.Opacity = 0.9;
        shape.Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.75 };
        _candidate = shape;
        _chrome.Children.Insert(0, shape);
    }

    private void ClearCandidate(Polyline? stroke)
    {
        if (_candidate is not null)
        {
            _chrome.Children.Remove(_candidate);
            _candidate = null;
        }

        if (stroke is not null)
        {
            stroke.Opacity = 1;
        }

        _recognizeCountdown = 0;
    }

    // ── выбор и перемещение ──────────────────────────────────────────────────

    /// <summary>Верхняя нарисованная фигура под точкой (с допуском по толщине линии).</summary>
    internal DrawnShape? HitTest(Point p)
    {
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            var s = _shapes[i];
            if (Contains(s.Element, p))
            {
                return s;
            }
        }

        return null;
    }

    private static bool Contains(FrameworkElement e, Point p)
    {
        if (e is Shape shape && GeometryOf(shape) is { } g)
        {
            var pen = new Pen(Brushes.Black, Math.Max(shape.StrokeThickness, 0) + 14);
            return g.StrokeContains(pen, p) || (shape is Rectangle or Ellipse or Polygon && g.FillContains(p));
        }

        // Текст и номер: точка переводится в координаты элемента (учитывает поворот и масштаб, Phase 4I).
        var m = PlacementOf(e);
        if (!m.HasInverse)
        {
            return false;
        }

        m.Invert();
        var local = m.Transform(p);
        var size = SizeOf(e);
        var r = new Rect(0, 0, size.Width, size.Height);
        r.Inflate(6, 6);
        return r.Contains(local);
    }

    /// <summary>Смещение фигуры на холсте (Canvas.Left/Top; у линий и ломаных по умолчанию — 0).</summary>
    private static Vector Offset(FrameworkElement e)
    {
        double l = Canvas.GetLeft(e), t = Canvas.GetTop(e);
        return new Vector(double.IsNaN(l) ? 0 : l, double.IsNaN(t) ? 0 : t);
    }

    /// <summary>
    /// Геометрия фигуры в координатах холста — из её собственных данных (точки, размеры, смещение), а не из последней
    /// раскладки: попадание и рамка выбора верны сразу после рисования и перемещения.
    /// </summary>
    internal static Geometry? GeometryOf(Shape s)
    {
        var o = Offset(s);
        Geometry? g = s switch
        {
            Line l => new LineGeometry(new Point(l.X1, l.Y1), new Point(l.X2, l.Y2)),
            Polyline pl when pl.Points.Count > 0 => Poly(pl.Points, closed: false),
            Polygon pg when pg.Points.Count > 0 => Poly(pg.Points, closed: true),
            Rectangle r => new RectangleGeometry(new Rect(0, 0, Size(r.Width), Size(r.Height)), r.RadiusX, r.RadiusY),
            Ellipse el => new EllipseGeometry(new Rect(0, 0, Size(el.Width), Size(el.Height))),
            Path path => path.Data?.Clone(),
            _ => null,
        };
        if (g is null)
        {
            return null;
        }

        // Собственное преобразование геометрии (повёрнутый контур, Phase 4I) сохраняется, смещение — после него.
        var placed = g.Transform?.Value ?? Matrix.Identity;
        placed.Translate(o.X, o.Y);
        g.Transform = new MatrixTransform(placed);
        return g;

        static double Size(double v) => double.IsNaN(v) ? 0 : v;
    }

    private static Geometry Poly(PointCollection pts, bool closed)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], closed, closed);
            c.PolyLineTo(pts.Skip(1).ToList(), true, true);
        }

        return g;
    }

    /// <summary>Границы нарисованного объекта на холсте (фигура — по геометрии с толщиной линии, текст и отметка — по размеру).</summary>
    internal static Rect BoundsOf(FrameworkElement e)
    {
        if (e is Shape s && GeometryOf(s) is { } g)
        {
            var b = g.GetRenderBounds(new Pen(Brushes.Black, Math.Max(1, s.StrokeThickness)));
            return b.IsEmpty ? g.Bounds : b;
        }

        var size = SizeOf(e);
        var bounds = new Rect(0, 0, size.Width, size.Height);
        bounds.Transform(PlacementOf(e)); // поворот и масштаб текста (Phase 4I) — границы повёрнутого прямоугольника
        return bounds;
    }

    /// <summary>Выбрать фигуру в точке (или снять выбор). Возвращает, выбрано ли что-нибудь.</summary>
    public bool SelectAt(Point p)
    {
        _selected = HitTest(p);
        UpdateSelectionBox();
        return _selected is not null;
    }

    public void ClearSelection()
    {
        _selected = null;
        UpdateSelectionBox();
    }

    /// <summary>
    /// Переместить выбранную фигуру — шаг истории: прежняя фигура заменяется такой же копией на новом месте (отмена
    /// возвращает исходное положение). Работает для всех фигур, текста и отметок.
    /// </summary>
    public bool MoveSelected(Vector delta)
    {
        if (_selected is not { } old || delta.Length < 1)
        {
            return false;
        }

        // Phase 4I: перемещение — то же преобразование, что размер и поворот (поворот текста и метаданные дуги сохраняются).
        var shift = Matrix.Identity;
        shift.Translate(delta.X, delta.Y);
        var moved = new DrawnShape(Transformed(old.Element, shift), old.FadeAt);
        Remove(old);
        Add(moved);
        _undo.Push([old, moved]);
        _redo.Clear();
        _selected = moved;
        UpdateSelectionBox();
        return true;
    }

    /// <summary>Удалить выбранную фигуру — шаг истории (отмена возвращает её).</summary>
    public bool DeleteSelected()
    {
        if (_selected is not { } s)
        {
            return false;
        }

        Remove(s);
        _undo.Push([s]);
        _redo.Clear();
        _selected = null;
        UpdateSelectionBox();
        return true;
    }

    private static FrameworkElement Clone(FrameworkElement e)
    {
        if (e is MarkerElement m)
        {
            return new MarkerElement(m.Number, m.Series, m.Size, m.ColorValue, m.Center);
        }

        var copy = (FrameworkElement)XamlReader.Parse(XamlWriter.Save(e));
        copy.IsHitTestVisible = false;
        CopyMeta(e, copy);
        return copy;
    }

    /// <summary>Рамка выделения с ручками размера, поворота и точек дуги (Phase 4I, <see cref="DrawHandles"/>).</summary>
    private void UpdateSelectionBox()
    {
        if (_selected is not null && !_shapes.Contains(_selected))
        {
            _selected = null;
        }

        DrawHandles();
    }

    private Transform? _dragRestore;

    private void BeginSelect(Point p)
    {
        if (SelectAt(p))
        {
            _selectGrab = p;
            _selectDragging = true;
            _dragRestore = _selected!.Element.RenderTransform;
            CaptureMouse();
        }
    }

    /// <summary>Предпросмотр перемещения: сдвиг поверх собственного размещения элемента (поворот текста сохраняется).</summary>
    private void DragSelect(Point p)
    {
        if (_selected is { } s && _selectDragging)
        {
            var m = _dragRestore?.Value ?? Matrix.Identity;
            m.Translate(p.X - _selectGrab.X, p.Y - _selectGrab.Y);
            s.Element.RenderTransform = new MatrixTransform(m);
            var shift = new TranslateTransform(p.X - _selectGrab.X, p.Y - _selectGrab.Y);
            foreach (var h in _handles)
            {
                h.RenderTransform = shift;
            }
        }
    }

    private void EndSelect(Point p)
    {
        _selectDragging = false;
        if (_selected is { } s)
        {
            s.Element.RenderTransform = _dragRestore;
            _dragRestore = null;
            if (!MoveSelected(p - _selectGrab))
            {
                UpdateSelectionBox();
            }
        }
    }

    /// <summary>Отменить перетаскивание (Esc во время перемещения): объект возвращается на место, история не меняется.</summary>
    private bool CancelDrag()
    {
        if (!_selectDragging || _selected is not { } s)
        {
            return false;
        }

        _selectDragging = false;
        s.Element.RenderTransform = _dragRestore;
        _dragRestore = null;
        ReleaseMouseCapture();
        UpdateSelectionBox();
        return true;
    }

    // ── панель ───────────────────────────────────────────────────────────────

    /// <summary>Кнопки панели: квадрат 34×34, скругление 8, состояния наведения, нажатия и выбранного инструмента.</summary>
    internal static Style ToolStyle => s_toolStyle ??= (Style)XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
          <Setter Property="Width" Value="34" />
          <Setter Property="Height" Value="34" />
          <Setter Property="Margin" Value="2" />
          <Setter Property="FontSize" Value="13" />
          <Setter Property="Foreground" Value="#F2F5F3" />
          <Setter Property="Background" Value="#14FFFFFF" />
          <Setter Property="Focusable" Value="False" />
          <Setter Property="Cursor" Value="Hand" />
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="Button">
                <Border x:Name="Bd" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" CornerRadius="8" Background="{TemplateBinding Background}" BorderThickness="1" BorderBrush="Transparent">
                  <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True">
                    <Setter TargetName="Bd" Property="BorderBrush" Value="#66FFFFFF" />
                    <Setter TargetName="Bd" Property="Background" Value="#33FFFFFF" />
                  </Trigger>
                  <Trigger Property="IsPressed" Value="True">
                    <Setter TargetName="Bd" Property="Background" Value="#55FFFFFF" />
                  </Trigger>
                  <Trigger Property="Tag" Value="selected">
                    <Setter TargetName="Bd" Property="Background" Value="#FF0B6B4D" />
                    <Setter TargetName="Bd" Property="BorderBrush" Value="#FF31C99A" />
                  </Trigger>
                  <Trigger Property="IsEnabled" Value="False">
                    <Setter Property="Opacity" Value="0.4" />
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """);

    private static Button Tool(string icon, string tip, bool selected, Action click)
    {
        var b = new Button { Content = Icons.Lucide.Icon(icon, 18, ToolInk), ToolTip = tip, Style = ToolStyle, Tag = selected ? "selected" : null };
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private static Button Swatch(ThemeColor c, string tip, bool selected, Action click)
    {
        var b = Tool("circle", tip, selected, click);
        b.Content = new Ellipse { Width = 16, Height = 16, Fill = c.ToFrozenBrush(), Stroke = Frozen(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1 };
        return b;
    }

    private static TextBlock Value(string text) => new()
    {
        Text = text,
        Foreground = ToolInk,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(4, 0, 4, 0),
        MinWidth = 18,
        TextAlignment = TextAlignment.Center,
    };

    /// <summary>Изменение из панели или меню: применить и сообщить приложению (запоминаются последние значения).</summary>
    private void ChangeSettings(DrawSettings s)
    {
        SetSettings(s.Sanitized());
        SettingsChanged?.Invoke(Settings);
    }

    private void BuildToolbar()
    {
        CloseFlyout();
        _toolbar.Children.Clear();
        if (_profile.ToolbarMode == DrawToolbarMode.Docked)
        {
            var grip = new Thumb { Width = 16, Height = 34, Cursor = Cursors.SizeAll, ToolTip = Loc.T("Draw.MoveToolbar"), Margin = new Thickness(0, 2, 2, 2), Template = GripTemplate("grip-vertical") };
            grip.DragStarted += (_, _) => _dragging = true;
            grip.DragDelta += (_, e) =>
            {
                double w = ActualWidth, h = ActualHeight;
                Canvas.SetLeft(_toolbarHost, Math.Clamp(Canvas.GetLeft(_toolbarHost) + e.HorizontalChange, 0, Math.Max(0, w - _toolbarHost.ActualWidth)));
                Canvas.SetTop(_toolbarHost, Math.Clamp(Canvas.GetTop(_toolbarHost) + e.VerticalChange, 0, Math.Max(0, h - _toolbarHost.ActualHeight)));
            };
            grip.DragCompleted += (_, _) =>
            {
                _dragging = false;
                double cx = (Canvas.GetLeft(_toolbarHost) + (_toolbarHost.ActualWidth / 2)) / Math.Max(1, ActualWidth);
                double cy = (Canvas.GetTop(_toolbarHost) + (_toolbarHost.ActualHeight / 2)) / Math.Max(1, ActualHeight);
                _profile = _profile with { ToolbarX = Math.Clamp(cx, 0, 1), ToolbarY = Math.Clamp(cy, 0, 1) };
                ToolbarMoved?.Invoke(_profile.ToolbarX!.Value, _profile.ToolbarY!.Value);
            };
            _toolbar.Children.Add(grip);
        }

        if (_profile.Compact)
        {
            BuildCompactTools();
        }
        else
        {
            BuildFullTools();
        }

        _toolbar.Children.Add(Separator());
        _toolbar.Children.Add(Tool("undo-2", Loc.T("Draw.Undo"), false, Undo));
        _toolbar.Children.Add(Tool("redo-2", Loc.T("Draw.Redo"), false, Redo));
        if (!_profile.Compact)
        {
            _toolbar.Children.Add(Tool("eraser", Loc.T("Draw.Clear"), false, Clear));
            _toolbar.Children.Add(Tool(Settings.FadeSec > 0 ? "timer" : "pin", Settings.FadeSec > 0 ? Loc.F("Draw.FadeOn", Settings.FadeSec) : Loc.T("Draw.FadeOff"), Settings.FadeSec > 0,
                () => ChangeSettings(Settings with { FadeSec = Settings.FadeSec > 0 ? 0 : 5 })));
            _toolbar.Children.Add(Separator());
            _toolbar.Children.Add(Tool("camera", Loc.T("Draw.Screenshot"), false, () => ScreenshotRequested?.Invoke()));
        }
        else
        {
            _toolbar.Children.Add(FlyoutButton("ellipsis", Loc.T("Draw.More"), false, MoreItems));
        }

        _toolbar.Children.Add(Tool(_profile.Compact ? "maximize-2" : "minimize-2", _profile.Compact ? Loc.T("Draw.Expand") : Loc.T("Draw.Compact"), false, () => SetCompact(!_profile.Compact)));
        _toolbar.Children.Add(Tool("x", Loc.T("Draw.Exit"), false, () => ExitRequested?.Invoke()));
    }

    /// <summary>Полная панель: все инструменты по категориям, цвета, толщина, линия, параметры текущей фигуры.</summary>
    private void BuildFullTools()
    {
        DrawCategory? last = null;
        foreach (var (tool, cat, _, icon) in DrawTools.All)
        {
            if (last is not null && last != cat)
            {
                _toolbar.Children.Add(Separator());
            }

            last = cat;
            var t = tool;
            _toolbar.Children.Add(Tool(icon, Loc.T("Draw.Tool." + t), Settings.Tool == t, () => ChangeSettings(Settings with { Tool = t })));
        }

        AddShapeOptions(_toolbar);
        _toolbar.Children.Add(Separator());
        bool neon = PaletteFor(Settings.Color) == NeonPalette;
        foreach (var c in PaletteFor(Settings.Color))
        {
            var color = c with { A = Settings.Color.A };
            _toolbar.Children.Add(Swatch(c, Loc.T("Draw.Color"), Settings.Color with { A = 0xFF } == c, () => ChangeSettings(Settings with { Color = color })));
        }

        // Переключатель набора: неоновые ↔ классические (выбирается первый цвет другого набора).
        _toolbar.Children.Add(Tool("sparkles", neon ? Loc.T("Draw.Palette.Classic") : Loc.T("Draw.Palette.Neon"), neon,
            () => ChangeSettings(Settings with { Color = (neon ? ClassicPalette[0] : NeonPalette[0]) with { A = Settings.Color.A } })));
        bool custom = !Palette.Contains(Settings.Color with { A = 0xFF });
        _toolbar.Children.Add(FlyoutButton("pipette", Loc.T("Draw.CustomColor"), custom, ColorItems));
        _toolbar.Children.Add(FlyoutButton("blend", Loc.F("Draw.OpacityValue", OpacityPercent(Settings.Color)), Settings.Color.A != 0xFF, OpacityItems));
        _toolbar.Children.Add(Separator());
        AddStroke(_toolbar);
    }

    /// <summary>
    /// Компактная панель: три кнопки категорий (показывают текущий инструмент своей категории, выбранная подсвечена), кнопка
    /// цвета с образцом текущего цвета и кнопка линии; остальное — во всплывающих группах.
    /// </summary>
    private void BuildCompactTools()
    {
        foreach (var cat in Enum.GetValues<DrawCategory>())
        {
            var c = cat;
            bool active = DrawTools.CategoryOf(Settings.Tool) == c;
            string icon = active ? DrawTools.IconOf(Settings.Tool) : DrawTools.CategoryIcon(c);
            string tip = active ? Loc.T("Draw.Category." + c) + ": " + Loc.T("Draw.Tool." + Settings.Tool) : Loc.T("Draw.Category." + c);
            _toolbar.Children.Add(FlyoutButton(icon, tip, active, panel =>
            {
                foreach (var (tool, tc, _, ic) in DrawTools.All.Where(x => x.Category == c))
                {
                    var t = tool;
                    panel.Children.Add(MenuRow(ic, Loc.T("Draw.Tool." + t), Settings.Tool == t, () => ChangeSettings(Settings with { Tool = t })));
                }

                if (c == DrawCategory.Shapes)
                {
                    var options = new WrapPanel { Margin = new Thickness(4, 4, 4, 0) };
                    AddShapeOptions(options, always: true);
                    panel.Children.Add(options);
                }
            }));
        }

        _toolbar.Children.Add(Separator());
        var current = Settings.Color;
        var colorButton = FlyoutButton("droplet", Loc.T("Draw.Color") + " " + current, true, ColorItems);
        colorButton.Tag = null;
        colorButton.Content = new Ellipse { Width = 18, Height = 18, Fill = current.ToFrozenBrush(), Stroke = ToolInk, StrokeThickness = 1.5 };
        _toolbar.Children.Add(colorButton);
        _toolbar.Children.Add(FlyoutButton("sliders-horizontal", Loc.T("Draw.Stroke"), false, panel =>
        {
            var row = new WrapPanel { Margin = new Thickness(4) };
            AddStroke(row);
            panel.Children.Add(row);
        }));
    }

    /// <summary>Параметры текущей фигуры: скругление прямоугольника, число вершин многоугольника.</summary>
    private void AddShapeOptions(Panel target, bool always = false)
    {
        if (always || Settings.Tool == DrawTool.SmartPen)
        {
            // Phase 4I: постоянная подпись величины угла (по умолчанию — только пока кнопка мыши нажата).
            target.Children.Add(Tool("triangle-right", Loc.T(Settings.AngleLabels ? "Draw.AngleLabels.On" : "Draw.AngleLabels.Off"), Settings.AngleLabels,
                () => ChangeSettings(Settings with { AngleLabels = !Settings.AngleLabels })));
        }

        if (always || Settings.Tool == DrawTool.RoundedRectangle)
        {
            target.Children.Add(Tool("minus", Loc.T("Draw.RadiusLess"), false, () => ChangeSettings(Settings with { CornerRadius = Math.Max(DrawSettings.CornerRange.Min, Settings.CornerRadius - 4) })));
            var v = Value(Settings.CornerRadius.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            v.ToolTip = Loc.T("Draw.Radius");
            target.Children.Add(v);
            target.Children.Add(Tool("plus", Loc.T("Draw.RadiusMore"), false, () => ChangeSettings(Settings with { CornerRadius = Math.Min(DrawSettings.CornerRange.Max, Settings.CornerRadius + 4) })));
        }

        if (always || Settings.Tool == DrawTool.Polygon)
        {
            target.Children.Add(Tool("minus", Loc.T("Draw.SidesLess"), false, () => ChangeSettings(Settings with { PolygonSides = Math.Max(DrawSettings.SidesRange.Min, Settings.PolygonSides - 1) })));
            var v = Value(Settings.PolygonSides.ToString(System.Globalization.CultureInfo.InvariantCulture));
            v.ToolTip = Loc.T("Draw.Sides");
            target.Children.Add(v);
            target.Children.Add(Tool("plus", Loc.T("Draw.SidesMore"), false, () => ChangeSettings(Settings with { PolygonSides = Math.Min(DrawSettings.SidesRange.Max, Settings.PolygonSides + 1) })));
        }
    }

    private void AddStroke(Panel target)
    {
        target.Children.Add(Tool("minus", Loc.T("Draw.Thinner"), false, () => ChangeSettings(Settings.Tool == DrawTool.Text
            ? Settings with { TextSize = Math.Max(DrawSettings.TextSizeRange.Min, Settings.TextSize - 4) }
            : Settings with { Thickness = Math.Max(DrawSettings.ThicknessRange.Min, Settings.Thickness - 1) })));
        target.Children.Add(Value((Settings.Tool == DrawTool.Text ? Settings.TextSize : Settings.Thickness).ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
        target.Children.Add(Tool("plus", Loc.T("Draw.Thicker"), false, () => ChangeSettings(Settings.Tool == DrawTool.Text
            ? Settings with { TextSize = Math.Min(DrawSettings.TextSizeRange.Max, Settings.TextSize + 4) }
            : Settings with { Thickness = Math.Min(DrawSettings.ThicknessRange.Max, Settings.Thickness + 1) })));
        var dash = Tool("minus", Loc.T("Draw.Dash"), false, () => ChangeSettings(Settings with { Dash = (DrawDash)(((int)Settings.Dash + 1) % 3) }));
        dash.Content = DashGlyph(Settings.Dash);
        target.Children.Add(dash);
    }

    private static FrameworkElement DashGlyph(DrawDash d) => new Line
    {
        X1 = 0,
        X2 = 18,
        Y1 = 1,
        Y2 = 1,
        Stroke = ToolInk,
        StrokeThickness = 2.4,
        StrokeDashArray = d switch { DrawDash.Dashed => [2.5, 1.5], DrawDash.Dotted => [0.01, 1.6], _ => null },
        StrokeDashCap = PenLineCap.Round,
        Height = 3,
    };

    private void MoreItems(StackPanel panel)
    {
        panel.Children.Add(MenuRow("eraser", Loc.T("Draw.Clear"), false, Clear));
        panel.Children.Add(MenuRow(Settings.FadeSec > 0 ? "timer" : "pin", Settings.FadeSec > 0 ? Loc.F("Draw.FadeOn", Settings.FadeSec) : Loc.T("Draw.FadeOff"), Settings.FadeSec > 0,
            () => ChangeSettings(Settings with { FadeSec = Settings.FadeSec > 0 ? 0 : 5 })));
        panel.Children.Add(MenuRow("camera", Loc.T("Draw.Screenshot"), false, () => ScreenshotRequested?.Invoke()));
    }

    /// <summary>Цвет: готовые цвета, прозрачность и свободный выбор (палитра, HEX #RRGGBB или #RRGGBBAA).</summary>
    private void ColorItems(StackPanel panel)
    {
        foreach (var (title, set) in new[] { (Loc.T("Draw.Palette.Neon"), NeonPalette), (Loc.T("Draw.Palette.Classic"), ClassicPalette) })
        {
            panel.Children.Add(new TextBlock { Text = title, Foreground = ToolInk, FontSize = 12, Margin = new Thickness(8, 4, 8, 0) });
            var swatches = new WrapPanel { Margin = new Thickness(4) };
            foreach (var c in set)
            {
                var color = c with { A = Settings.Color.A };
                swatches.Children.Add(Swatch(c, c.ToString(), Settings.Color with { A = 0xFF } == c, () => ChangeSettings(Settings with { Color = color })));
            }

            panel.Children.Add(swatches);
        }

        OpacityItems(panel);

        var host = new Border { Padding = new Thickness(8), CornerRadius = new CornerRadius(8), Background = Frozen(Color.FromArgb(0xFF, 0xF4, 0xF6, 0xF5)) };
        host.Resources.MergedDictionaries.Add(UiTheme.Resources);
        var field = new ColorField(UiTheme.Resources, Settings.Color, true, c => ChangeSettingsQuiet(Settings with { Color = c }));
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = Loc.T("Draw.CustomColor"), Margin = new Thickness(0, 0, 0, 6), Foreground = Brushes.Black });
        stack.Children.Add(field);
        stack.Children.Add(new ColorPicker(Settings.Color, true, c =>
        {
            field.SetFromPicker(c);
        }) { Margin = new Thickness(0, 8, 0, 0) });
        host.Child = stack;
        panel.Children.Add(host);
    }

    public static int OpacityPercent(ThemeColor c) => (int)Math.Round(c.A * 100 / 255.0);

    /// <summary>
    /// Прозрачность линий — ползунком (10–100 %, шаг 5) и быстрыми значениями. Изменение применяется сразу к следующим линиям;
    /// группа не закрывается, пока тянется ползунок.
    /// </summary>
    private void OpacityItems(StackPanel panel)
    {
        var value = new TextBlock { Foreground = ToolInk, Margin = new Thickness(8, 6, 8, 2), Text = Loc.F("Draw.OpacityValue", OpacityPercent(Settings.Color)) };
        panel.Children.Add(value);
        var slider = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            SmallChange = 5,
            LargeChange = 10,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Value = Math.Max(10, OpacityPercent(Settings.Color)),
            Width = 220,
            Margin = new Thickness(8, 0, 8, 6),
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, Loc.T("Draw.Opacity"));
        slider.ValueChanged += (_, e) =>
        {
            byte a = (byte)Math.Round(e.NewValue * 255 / 100);
            value.Text = Loc.F("Draw.OpacityValue", (int)Math.Round(e.NewValue));
            ChangeSettingsQuiet(Settings with { Color = Settings.Color with { A = a } });
        };
        panel.Children.Add(slider);
        var quick = new WrapPanel { Margin = new Thickness(4, 0, 4, 4) };
        foreach (int pct in new[] { 100, 75, 50, 25 })
        {
            int v = pct;
            var b = Tool("circle", v + " %", OpacityPercent(Settings.Color) == v, () => slider.Value = v);
            b.Width = double.NaN;
            b.Content = new TextBlock { Text = v + " %", Foreground = ToolInk, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            quick.Children.Add(b);
        }

        panel.Children.Add(quick);
    }

    /// <summary>Изменение из открытой группы (свободный цвет): панель не перестраивается, чтобы группа не закрывалась.</summary>
    private void ChangeSettingsQuiet(DrawSettings s)
    {
        Settings = s.Sanitized();
        if (_editorBox is not null)
        {
            _editorBox.Foreground = Settings.Color.ToFrozenBrush();
        }

        SettingsChanged?.Invoke(Settings);
    }

    private Border MenuRow(string icon, string text, bool selected, Action click)
    {
        var b = Tool(icon, text, selected, () =>
        {
            CloseFlyout();
            click();
        });
        b.Width = double.NaN;
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 10, 0) };
        row.Children.Add(Icons.Lucide.Icon(icon, 18, ToolInk));
        row.Children.Add(new TextBlock { Text = text, Foreground = ToolInk, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        b.Content = row;
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new Border { Child = b };
    }

    /// <summary>Кнопка со всплывающей группой (компактная панель, свободный цвет, «ещё»). Группа закрывается щелчком вне её и Esc.</summary>
    private Button FlyoutButton(string icon, string tip, bool selected, Action<StackPanel> fill)
    {
        Button? button = null;
        button = Tool(icon, tip, selected, () =>
        {
            if (_flyout is { IsOpen: true } open && ReferenceEquals(open.PlacementTarget, button))
            {
                CloseFlyout();
                return;
            }

            CloseFlyout();
            var panel = new StackPanel { Margin = new Thickness(6), MinWidth = 180 };
            fill(panel);
            _flyout = new Popup
            {
                PlacementTarget = button,
                Placement = _toolbar.Orientation == Orientation.Vertical ? PlacementMode.Right : PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = new Border
                {
                    Background = Frozen(Color.FromArgb(0xF2, 0x1E, 0x22, 0x21)),
                    BorderBrush = Frozen(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Child = panel,
                },
                IsOpen = true,
            };
        });
        return button;
    }

    private void CloseFlyout()
    {
        if (_flyout is not null)
        {
            _flyout.IsOpen = false;
            _flyout = null;
        }
    }

    /// <summary>Открыта всплывающая группа панели (для проверок).</summary>
    public bool FlyoutOpen => _flyout is { IsOpen: true };

    private void SetCompact(bool compact)
    {
        _profile = _profile with { Compact = compact };
        BuildToolbar();
        PlaceToolbar();
        CompactChanged?.Invoke(compact);
    }

    private static ControlTemplate GripTemplate(string icon)
    {
        var t = new ControlTemplate(typeof(Thumb));
        var f = new FrameworkElementFactory(typeof(Border));
        f.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        f.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        // Иконка Lucide фабриками шаблона (готовый визуальный элемент в шаблон передать нельзя).
        var box = new FrameworkElementFactory(typeof(Viewbox));
        box.SetValue(WidthProperty, 16.0);
        box.SetValue(HeightProperty, 16.0);
        box.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        box.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var canvas = new FrameworkElementFactory(typeof(Canvas));
        canvas.SetValue(WidthProperty, 24.0);
        canvas.SetValue(HeightProperty, 24.0);
        foreach (var g in Icons.Lucide.Geometries(icon))
        {
            var path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, g);
            path.SetValue(Shape.StrokeProperty, Frozen(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)));
            path.SetValue(Shape.StrokeThicknessProperty, 2.0);
            path.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Round);
            path.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Round);
            path.SetValue(Shape.StrokeLineJoinProperty, PenLineJoin.Round);
            canvas.AppendChild(path);
        }

        box.AppendChild(canvas);
        f.AppendChild(box);
        t.VisualTree = f;
        return t;
    }

    // ── радиальное меню ──────────────────────────────────────────────────────

    /// <summary>
    /// Каталог радиального меню: категории рисования (инструменты, фигуры, текст и отметки, цвет, правка) и действия; приложение
    /// добавляет категории общего реестра команд (Фокус, показ, плагины) через <see cref="RadialCatalog"/>.
    /// </summary>
    internal IReadOnlyList<RadialNode> DrawingCatalog()
    {
        RadialNode ToolNode(DrawTool t, RadialItem? legacy = null) =>
            new("tool." + t, Loc.T("Draw.Tool." + t), DrawTools.IconOf(t), legacy is null ? () => ChangeSettings(Settings with { Tool = t }) : null,
                IsChecked: () => Settings.Tool == t, Legacy: legacy);
        RadialItem? LegacyOf(DrawTool t) => t switch
        {
            DrawTool.Pen => RadialItem.Pen,
            DrawTool.Line => RadialItem.Line,
            DrawTool.Arrow => RadialItem.Arrow,
            DrawTool.Rectangle => RadialItem.Rectangle,
            DrawTool.Ellipse => RadialItem.Ellipse,
            DrawTool.Text => RadialItem.Text,
            _ => null,
        };
        RadialNode Category(DrawCategory c) => new("cat." + c.ToString().ToLowerInvariant(), Loc.T("Draw.Category." + c), DrawTools.CategoryIcon(c),
            Children: DrawTools.All.Where(x => x.Category == c).Select(x => ToolNode(x.Tool, LegacyOf(x.Tool))).ToList());
        var colors = Palette.Select(c => new RadialNode("color." + c, c.ToString(), "circle", () => ChangeSettings(Settings with { Color = c with { A = Settings.Color.A } }),
            IsChecked: () => Settings.Color with { A = 0xFF } == c, Swatch: Color.FromRgb(c.R, c.G, c.B))).ToList();
        colors.Add(new RadialNode("color.next", Loc.T("Draw.NextColor"), "palette", Legacy: RadialItem.Color));
        var list = new List<RadialNode>
        {
            Category(DrawCategory.Draw),
            Category(DrawCategory.Shapes),
            Category(DrawCategory.Annotate),
            new("cat.color", Loc.T("Draw.Color"), "palette", Children: colors),
            new("cat.edit", Loc.T("Draw.Category.Edit"), "undo-2", Children:
            [
                new("edit.undo", Loc.T("Draw.Undo"), "undo-2", Legacy: RadialItem.Undo, IsAvailable: () => CanUndo),
                new("edit.redo", Loc.T("Draw.Redo"), "redo-2", Legacy: RadialItem.Redo, IsAvailable: () => CanRedo),
                new("edit.clear", Loc.T("Draw.Clear"), "eraser", Legacy: RadialItem.Clear, IsAvailable: () => ShapeCount > 0),
                new("edit.markers", Loc.T("Commands.MarkersReset"), "list-restart", () => ResetMarkers(1)),
            ]),
        };
        if (RadialCatalog?.Invoke() is { } extra)
        {
            list.AddRange(extra);
        }

        list.Add(new RadialNode("app.screenshot", Loc.T("Draw.Screenshot"), "camera", Legacy: RadialItem.Screenshot));
        list.Add(new RadialNode("app.exit", Loc.T("Draw.Exit"), "power", Legacy: RadialItem.Exit));
        return list;
    }

    private RadialMenu CreateRadial()
    {
        var r = new RadialMenu(DrawingCatalog());
        r.Chosen += item =>
        {
            HideRadial();
            switch (item)
            {
                case RadialItem.Pen: ChangeSettings(Settings with { Tool = DrawTool.Pen }); break;
                case RadialItem.Line: ChangeSettings(Settings with { Tool = DrawTool.Line }); break;
                case RadialItem.Arrow: ChangeSettings(Settings with { Tool = DrawTool.Arrow }); break;
                case RadialItem.Rectangle: ChangeSettings(Settings with { Tool = DrawTool.Rectangle }); break;
                case RadialItem.Ellipse: ChangeSettings(Settings with { Tool = DrawTool.Ellipse }); break;
                case RadialItem.Text: ChangeSettings(Settings with { Tool = DrawTool.Text }); break;
                case RadialItem.Color:
                    int i = Array.FindIndex(Palette, p => p == (Settings.Color with { A = 0xFF }));
                    ChangeSettings(Settings with { Color = Palette[(i + 1) % Palette.Length] with { A = Settings.Color.A } });
                    break;
                case RadialItem.Undo: Undo(); break;
                case RadialItem.Redo: Redo(); break;
                case RadialItem.Clear: Clear(); break;
            }

            RadialClosed?.Invoke();
            if (item == RadialItem.Screenshot)
            {
                ScreenshotRequested?.Invoke();
            }
            else if (item == RadialItem.Exit)
            {
                ExitRequested?.Invoke();
            }
        };
        r.Executed += _ =>
        {
            HideRadial();
            RadialClosed?.Invoke();
        };
        r.Dismissed += () =>
        {
            HideRadial();
            RadialClosed?.Invoke();
        };
        r.Visibility = Visibility.Collapsed;
        _chrome.Children.Add(r);
        return r;
    }

    /// <summary>Показать радиальное меню вокруг точки окна (DIP); у края — сдвиг внутрь, на маленькой области — уменьшение.</summary>
    public void ShowRadial(Point center)
    {
        CommitText();
        CloseFlyout();
        if (_radial is not null)
        {
            _chrome.Children.Remove(_radial); // каталог зависит от контекста (доступность, отметки) — строится при каждом открытии
        }

        _radial = CreateRadial();
        var (c, scale) = RadialMenu.Fit(center, new Size(ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height));
        _radial.Place(c, scale);
        _radial.ResetLevel();
        _radial.Visibility = Visibility.Visible;
        if (!TestNoActivate)
        {
            Activate();
            _radial.Focus();
            Keyboard.Focus(_radial);
        }
    }

    public void HideRadial()
    {
        if (_radial is { Visibility: Visibility.Visible })
        {
            _radial.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Esc при открытом внешнем кольце закрывает только его (true); иначе решает контроллер.</summary>
    public bool RadialBack()
    {
        if (_radial is { Visibility: Visibility.Visible, SubRingOpen: true } r)
        {
            r.Back();
            return true;
        }

        return false;
    }
}
