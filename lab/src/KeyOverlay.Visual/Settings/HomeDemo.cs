using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using KeyOverlay.Visual.Mouse;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Rendering;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Приветственная демонстрация на «Главной» из настоящих элементов Backpack на искусственных данных (без настоящего ввода
/// и без захвата рабочего стола). Основа и акценты — по теме окна (светлая сцена в светлой теме, тёмная — в тёмной); цвета
/// стиля кейкапов и мыши не меняются. Скруглённая рамка действительно обрезает движущееся содержимое (<see cref="ClipBorder"/>).
/// Цикл из четырёх фаз:
/// <list type="number">
/// <item>кейкапы (тот же <see cref="OverlayPresenter"/>) и значок мыши со щелчками и прокруткой — всё время;</item>
/// <item>стрелка (геометрия режима рисования) и нумерованная отметка (<see cref="MarkerElement"/>);</item>
/// <item>штрих умного карандаша, который настоящий <see cref="ShapeRecognizer"/> превращает в эллипс;</item>
/// <item>«Фокус: затемнение» вокруг кейкапов;</item>
/// <item>«Фокус: линза» и круг увеличения у курсора (VisualBrush этой же сцены).</item>
/// </list>
/// Один таймер 120 мс; останавливается паузой, уходом со страницы, закрытием окна; не запускается при «меньше движения»
/// и выключенных анимациях Windows. Без звука.
/// </summary>
public sealed class HomeDemo : ClipBorder, IDisposable
{
    private const double SceneWidth = 640;
    private const double SceneHeight = 210;
    public const int CycleSteps = 120;

    private static readonly SolidColorBrush Red = Frozen(Color.FromRgb(0xFF, 0x3B, 0x30));

    private readonly IAnimationClock _clock = new StopwatchClock();
    private readonly OverlayPresenter _keys;
    private readonly DemoPlayer _player;
    private readonly MouseCursorVisual _mouse = new();
    private readonly Canvas _canvas = new() { Width = SceneWidth, Height = SceneHeight };
    private readonly Path _arrow;
    private readonly Polyline _stroke;
    private readonly Ellipse _shape;
    private readonly MarkerElement _marker;
    private readonly Path _dim;
    private readonly Border _lens;
    private readonly VisualBrush _lensBrush;
    private readonly Ellipse _zoom;
    private readonly VisualBrush _zoomBrush;
    private readonly Grid _scene = new() { Width = SceneWidth, Height = SceneHeight };
    private readonly DispatcherTimer _timer;
    private readonly List<Point> _strokePoints;
    private int _step;
    private bool _disposed;

    public HomeDemo(LabSettings settings, bool animate)
    {
        CornerRadius = new CornerRadius(16);
        Height = 200;
        SetResourceReference(BackgroundProperty, "Ko.StageBrush");
        _scene.Children.Add(_canvas);

        _keys = new OverlayPresenter(_clock);
        _keys.Apply(settings.Appearance with { Size = 26 }, new RowOptions(TrailMode.None, MouseLabelMode.Graphic, HorizontalAlignment.Center));
        Canvas.SetLeft(_keys.Root, 30);
        Canvas.SetTop(_keys.Root, 40);
        _canvas.Children.Add(_keys.Root);
        _player = new DemoPlayer(_keys, _clock, scenario: DemoScenario.Chords);
        if (!animate)
        {
            _player.Dispose();
        }

        Canvas.SetLeft(_mouse, 330);
        Canvas.SetTop(_mouse, 46);
        _canvas.Children.Add(_mouse);

        _arrow = new Path { Stroke = Red, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        _canvas.Children.Add(_arrow);
        _marker = new MarkerElement(1, 0, 30, new ThemeColor(0xFF, 0x3B, 0x30), new Point(470, 150));
        _canvas.Children.Add(_marker);

        // Штрих «от руки» вокруг значка мыши: распознаётся настоящим ShapeRecognizer.
        _strokePoints = Enumerable.Range(0, 49).Select(i =>
        {
            double a = 2 * Math.PI * i / 48;
            double k = 1 + (0.05 * Math.Sin(a * 5));
            return new Point(368 + (62 * k * Math.Cos(a)), 92 + (44 * k * Math.Sin(a)));
        }).ToList();
        _stroke = new Polyline { StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        _stroke.SetResourceReference(Shape.StrokeProperty, "Ko.LogoBrush");
        _canvas.Children.Add(_stroke);
        var r = ShapeRecognizer.Recognize(_strokePoints);
        var b = r?.Bounds ?? new Rect(306, 48, 124, 88);
        _shape = new Ellipse { StrokeThickness = 3, Width = b.Width, Height = b.Height, Visibility = Visibility.Collapsed };
        _shape.SetResourceReference(Shape.StrokeProperty, "Ko.LogoBrush");
        Canvas.SetLeft(_shape, b.X);
        Canvas.SetTop(_shape, b.Y);
        _canvas.Children.Add(_shape);

        // «Затемнение»: чёрный слой вне скруглённого окна вокруг кейкапов.
        _dim = new Path
        {
            Fill = Brushes.Black,
            Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, SceneWidth, SceneHeight)), new RectangleGeometry(new Rect(20, 28, 270, 80), 16, 16)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
            StrokeThickness = 2,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _canvas.Children.Add(_dim);

        var overlayLayer = new Canvas();
        _lensBrush = new VisualBrush(_canvas) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
        _lens = new Border
        {
            Width = 150,
            Height = 92,
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(2),
            Background = _lensBrush,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x31, 0xC9, 0x9A), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.7 },
            Visibility = Visibility.Collapsed,
        };
        _lens.SetResourceReference(BorderBrushProperty, "Ko.LogoBrush");
        Canvas.SetLeft(_lens, 470);
        Canvas.SetTop(_lens, 14);
        overlayLayer.Children.Add(_lens);
        _zoomBrush = new VisualBrush(_canvas) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
        _zoom = new Ellipse { Width = 96, Height = 96, Fill = _zoomBrush, StrokeThickness = 2, Visibility = Visibility.Collapsed, Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.5 } };
        _zoom.SetResourceReference(Shape.StrokeProperty, "Ko.LogoBrush");
        Canvas.SetLeft(_zoom, 312);
        Canvas.SetTop(_zoom, 104);
        overlayLayer.Children.Add(_zoom);
        _scene.Children.Add(overlayLayer);

        // Свечение — на всю сцену (а не только на область рисунка внутри Viewbox), поэтому у него нет видимых краёв.
        var glow = new Border { IsHitTestVisible = false };
        glow.SetResourceReference(BackgroundProperty, "Ko.StageGlowBrush");
        var layers = new Grid();
        layers.Children.Add(glow);
        layers.Children.Add(new Viewbox { Child = _scene, Stretch = Stretch.Uniform, Margin = new Thickness(8) });
        Child = layers;
        System.Windows.Automation.AutomationProperties.SetName(this, "Backpack demo");

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) => Step();
        Step();
        if (animate)
        {
            _timer.Start();
        }
    }

    public bool IsPlaying => _timer.IsEnabled;

    /// <summary>Текущая фаза (0–3) — для проверок.</summary>
    public int Phase => ((_step - 1) % CycleSteps) / 30;

    /// <summary>Пауза и продолжение (кнопка на «Главной»).</summary>
    public void SetPlaying(bool play)
    {
        if (_disposed)
        {
            return;
        }

        if (play)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _player.Dispose();
        }
    }

    /// <summary>Шаг демонстрации (таймер; тесты вызывают напрямую).</summary>
    internal void Step()
    {
        int s = _step++ % CycleSteps;
        int phase = s / 30, local = s % 30;
        var (pressed, wheel) = (s / 6 % 10) switch
        {
            0 => (MouseButtons.Left, 0),
            2 => (MouseButtons.Right, 0),
            4 => (MouseButtons.None, 1),
            5 => (MouseButtons.None, -1),
            _ => (MouseButtons.None, 0),
        };
        _mouse.Apply(MouseCursorSettings.Default with { Enabled = true, ShowCircle = pressed != MouseButtons.None, OffsetX = 18, OffsetY = 18 },
            MouseVisual.Default with { IconSize = 52 }, new MouseButtonState(pressed, MouseButtons.None, wheel, 0));

        // 1: стрелка и нумерованная отметка.
        double t = phase == 0 ? Math.Clamp((local - 2) / 14.0, 0, 1) : 0;
        _arrow.Data = t > 0.02 ? DrawingWindow.ArrowGeometry(new Point(560, 180), new Point(560 - (70 * t), 180 - (26 * t)), 4) : null;
        _marker.Visibility = phase == 0 && local >= 16 ? Visibility.Visible : Visibility.Collapsed;

        // 2: штрих «от руки» прорисовывается и превращается в эллипс.
        int n = phase == 1 ? Math.Clamp(local * 3, 0, _strokePoints.Count) : 0;
        _stroke.Points = new PointCollection(_strokePoints.Take(phase == 1 && local < 20 ? n : 0));
        _shape.Visibility = phase == 1 && local >= 20 ? Visibility.Visible : Visibility.Collapsed;

        // 3: затемнение вокруг кейкапов (появление).
        _dim.Opacity = phase == 2 ? 0.7 * Math.Clamp(local / 4.0, 0, 1) : 0;

        // 4: линза и увеличение у курсора.
        _lens.Visibility = _zoom.Visibility = phase == 3 ? Visibility.Visible : Visibility.Collapsed;
        double lx = 40 + (110 * (0.5 + (0.5 * Math.Sin(_step / 12.0))));
        _lensBrush.Viewbox = new Rect(lx, 50, 150 / 1.8, 92 / 1.8);
        _zoomBrush.Viewbox = new Rect(336, 60, 96 / 1.5, 96 / 1.5);
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _player.Dispose();
        _keys.Dispose();
    }
}
