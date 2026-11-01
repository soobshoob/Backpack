using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Presentation;

/// <summary>Нарисованный объект (фигура или текст) и его срок (null — до выхода из режима).</summary>
internal sealed record DrawnShape(FrameworkElement Element, DateTime? FadeAt);

/// <summary>
/// Логика автоскрытия панели (без окон — проверяется тестами). Панель появляется, только если указатель задержался у выбранного
/// края не меньше <see cref="RevealDelay"/> (защита от случайного открытия при быстром проходе мыши), и прячется, когда указатель
/// дольше <see cref="HideDelay"/> находится дальше <see cref="KeepDistance"/> от панели. Пока панель «удерживается» (меню, ввод
/// текста, перетаскивание), она не прячется.
/// </summary>
public sealed class AutoHideLogic
{
    public static readonly TimeSpan RevealDelay = TimeSpan.FromMilliseconds(140);
    public static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(700);
    public const double EdgeZone = 6;
    public const double KeepDistance = 48;

    private TimeSpan? _atEdgeSince;
    private TimeSpan? _awaySince;

    public AutoHideLogic(ScreenEdge edge) => Edge = edge;

    public ScreenEdge Edge { get; }

    public bool Shown { get; private set; }

    /// <summary>Шаг: положение указателя в окне (DIP), размер окна, прямоугольник панели (когда показана), время.</summary>
    public bool Update(Point cursor, Size area, Rect toolbar, TimeSpan now, bool hold = false)
    {
        bool atEdge = Edge switch
        {
            ScreenEdge.Top => cursor.Y <= EdgeZone,
            ScreenEdge.Bottom => cursor.Y >= area.Height - EdgeZone,
            ScreenEdge.Left => cursor.X <= EdgeZone,
            _ => cursor.X >= area.Width - EdgeZone,
        };

        if (!Shown)
        {
            _awaySince = null;
            if (!atEdge)
            {
                _atEdgeSince = null;
                return false;
            }

            _atEdgeSince ??= now;
            if (now - _atEdgeSince.Value >= RevealDelay)
            {
                Shown = true;
                _atEdgeSince = null;
            }

            return Shown;
        }

        var keep = toolbar;
        keep.Inflate(KeepDistance, KeepDistance);
        if (hold || atEdge || keep.Contains(cursor))
        {
            _awaySince = null;
            return true;
        }

        _awaySince ??= now;
        if (now - _awaySince.Value >= HideDelay)
        {
            Shown = false;
            _awaySince = null;
        }

        return Shown;
    }

    public void ForceShow() => Shown = true;
}

/// <summary>
/// Временное рисование поверх живого рабочего стола: свободная линия, прямая, стрелка, прямоугольник, эллипс, текст; цвет и
/// прозрачность, толщина, сплошная/штриховая/пунктирная линия; отмена, повтор, очистка; исчезновение через N секунд или «до выхода»;
/// снимок экрана с рисунками. Ничего не сохраняется, кроме снимка по явной команде.
/// Панель — постоянная (её можно перетащить за ручку), с автоскрытием у выбранного края или скрыта совсем (только радиальное меню).
/// Радиальное меню — горячей клавишей или правой кнопкой мыши; в нём все инструменты, отмена, повтор, снимок и выход, поэтому
/// без панели нельзя «застрять».
/// </summary>
public sealed partial class DrawingWindow : InteractiveOverlayWindow
{
    /// <summary>Классические цвета.</summary>
    public static readonly ThemeColor[] ClassicPalette =
    [
        new(0xFF, 0x3B, 0x30), new(0xFF, 0xCC, 0x00), new(0x34, 0xC7, 0x59), new(0x0A, 0x84, 0xFF), new(0xFF, 0xFF, 0xFF), new(0x11, 0x11, 0x11),
    ];

    /// <summary>
    /// Неоновые цвета (по умолчанию — неоновый изумрудный): яркие, насыщенные; линии этих цветов рисуются с мягким свечением
    /// своего цвета, поэтому заметны и на тёмном, и на светлом фоне.
    /// </summary>
    public static readonly ThemeColor[] NeonPalette =
    [
        new(0x00, 0xFF, 0x9C), new(0x00, 0xE5, 0xFF), new(0xFF, 0x2E, 0x97), new(0xB1, 0x4C, 0xFF), new(0xEA, 0xFF, 0x00), new(0xFF, 0x6B, 0x1A),
    ];

    /// <summary>Все готовые цвета (неоновые, затем классические) — для радиального меню и «следующего цвета».</summary>
    public static readonly ThemeColor[] Palette = [.. NeonPalette, .. ClassicPalette];

    public static bool IsNeon(ThemeColor c) => NeonPalette.Contains(c with { A = 0xFF });

    /// <summary>Набор образцов на панели — тот, к которому относится текущий цвет (свой цвет — неоновый набор).</summary>
    public static ThemeColor[] PaletteFor(ThemeColor current) => ClassicPalette.Contains(current with { A = 0xFF }) ? ClassicPalette : NeonPalette;

    /// <summary>Свечение неоновой линии (null — цвет не неоновый).</summary>
    public static System.Windows.Media.Effects.DropShadowEffect? NeonGlow(ThemeColor c, double thickness)
    {
        if (!IsNeon(c))
        {
            return null;
        }

        var glow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(c.R, c.G, c.B),
            BlurRadius = Math.Clamp(thickness * 3, 8, 28),
            ShadowDepth = 0,
            Opacity = 0.85 * (c.A / 255.0),
        };
        glow.Freeze();
        return glow;
    }

    private static Style? s_toolStyle;

    private readonly Canvas _ink = new() { ClipToBounds = true, Background = Brushes.Transparent };
    private readonly Canvas _chrome = new();
    private readonly Grid _root = new();
    private readonly List<DrawnShape> _shapes = new();
    private readonly Stack<List<DrawnShape>> _undo = new();
    private readonly Stack<List<DrawnShape>> _redo = new();
    private readonly DispatcherTimer _fade;
    private readonly DispatcherTimer _autoHideTimer;
    /// <summary>Кнопки переносятся в следующую строку или столбец, если не помещаются на мониторе (без обрезки).</summary>
    private readonly WrapPanel _toolbar = new() { Orientation = Orientation.Horizontal };
    private readonly Border _toolbarHost;
    private readonly TranslateTransform _toolbarSlide = new();
    private readonly Border _toast;
    private readonly TextBlock _toastText = new() { Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
    private readonly TextBlock _hint;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private DrawingProfile _profile;
    private AutoHideLogic? _autoHide;
    private RadialMenu? _radial;
    private Shape? _current;
    private Point _from;
    private Border? _editor;
    private TextBox? _editorBox;
    private bool _dragging;

    public DrawingWindow(PixelRect bounds, double scale, DrawSettings settings, DrawingProfile? profile = null) : base(bounds, scale)
    {
        Title = "Backpack — drawing";
        Settings = settings;
        _profile = profile ?? DrawingProfile.Default;
        Cursor = Cursors.Pen;
        _root.Children.Add(_ink);
        _root.Children.Add(_chrome);
        _toolbarHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEB, 0x1C, 0x1C, 0x21)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(5),
            Child = _toolbar,
            Cursor = Cursors.Arrow,
            RenderTransform = _toolbarSlide,
        };
        _chrome.Children.Add(_toolbarHost);
        _toast = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1C, 0x21)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8, 14, 8),
            Child = _toastText,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _chrome.Children.Add(_toast);
        _hint = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x1C, 0x1C, 0x21)),
            Padding = new Thickness(14, 8, 14, 8),
            FontSize = 13,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _chrome.Children.Add(_hint);
        Content = _root;
        BuildToolbar();
        _fade = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _fade.Tick += (_, _) => FadeTick();
        Deactivated += (_, _) => CloseFlyout();
        _autoHideTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(40) };
        _autoHideTimer.Tick += (_, _) => AutoHideTick();
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseRightButtonUp += OnRightUp;
        PreviewKeyDown += OnKey;
        PreviewKeyUp += (_, e) => e.Handled |= !IsEditingText && OnModifierChanged(e);
        Loaded += (_, _) => ApplyProfile(_profile);
        Closed += (_, _) =>
        {
            _fade.Stop();
            _autoHideTimer.Stop();
            _shapes.Clear();
            _undo.Clear();
            _redo.Clear();
        };
    }

    public DrawSettings Settings { get; private set; }

    public DrawingProfile Profile => _profile;

    public int ShapeCount => _shapes.Count;

    /// <summary>Элементы нарисованных объектов в порядке добавления (для проверок).</summary>
    internal IEnumerable<FrameworkElement> Shapes => _shapes.Select(s => s.Element);

    public int TextCount => _shapes.Count(s => s.Element is TextBlock);

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool RadialOpen => _radial is { Visibility: Visibility.Visible };

    public RadialMenu? Radial => _radial;

    public bool IsEditingText => _editor is not null;

    public TextBox? TextEditor => _editorBox;

    /// <summary>Панель видна сейчас (постоянная или раскрытая у края).</summary>
    public bool ToolbarShown => _toolbarHost.Visibility == Visibility.Visible && _toolbarHost.Opacity > 0.01;

    public Border ToolbarHost => _toolbarHost;

    public AutoHideLogic? AutoHide => _autoHide;

    public string? ToastText => _toast.Opacity > 0 ? _toastText.Text : null;

    /// <summary>Esc в окне (ввод текста, меню, выход — решает контроллер).</summary>
    public event Action? EscapeRequested;

    public event Action? ExitRequested;

    public event Action? RadialClosed;

    public event Action? RadialRequested;

    public event Action? ScreenshotRequested;

    /// <summary>Постоянная панель перетащена: центр панели в долях окна (сохраняется в профиле).</summary>
    public event Action<double, double>? ToolbarMoved;

    /// <summary>
    /// Та же панель, что в режиме рисования, для превью в настройках и снимков справки: окно создаётся, но не показывается;
    /// панель отсоединяется и не принимает ввод.
    /// </summary>
    public static Border PreviewToolbar(DrawSettings s, DrawingProfile p) => PreviewToolbar(s, p, new Size(1920, 1080));

    /// <summary>Панель для экрана размера <paramref name="area"/> (DIP): те же ограничения ширины и высоты, что у окна рисования на нём.</summary>
    public static Border PreviewToolbar(DrawSettings s, DrawingProfile p, Size area)
    {
        var w = new DrawingWindow(new PixelRect(-32000, -32000, (int)Math.Round(area.Width), (int)Math.Round(area.Height)), 1.0, s, p);
        w.ApplyProfile(p);
        w._autoHideTimer.Stop();
        var host = w._toolbarHost;
        w._chrome.Children.Remove(host);
        host.RenderTransform = null;
        host.BeginAnimation(OpacityProperty, null);
        host.Opacity = 1;
        host.Visibility = Visibility.Visible;
        host.IsHitTestVisible = false;
        w.Close();
        return host;
    }

    public void SetSettings(DrawSettings s)
    {
        if (s.Tool != DrawTool.Text)
        {
            CommitText();
        }

        bool toolChanged = Settings.Tool != s.Tool;
        Settings = s;
        if (s.Tool != DrawTool.Select)
        {
            ClearSelection();
        }

        if (toolChanged && IsLoaded)
        {
            ShowToolHint(s.Tool);
        }

        if (_editorBox is not null)
        {
            _editorBox.Foreground = s.Color.ToFrozenBrush();
            _editorBox.FontSize = s.TextSize;
        }

        BuildToolbar();
        PlaceToolbar();
    }

    // ── режимы панели ────────────────────────────────────────────────────────

    /// <summary>Режим панели и её положение (из профиля). Применяется сразу, без пересоздания окна.</summary>
    public void ApplyProfile(DrawingProfile p)
    {
        _profile = p;
        _autoHideTimer.Stop();
        _autoHide = null;
        _toolbarSlide.X = _toolbarSlide.Y = 0;
        bool vertical = p.ToolbarMode == DrawToolbarMode.AutoHide && p.AutoHideEdge is ScreenEdge.Left or ScreenEdge.Right;
        _toolbar.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        BuildToolbar();
        switch (p.ToolbarMode)
        {
            case DrawToolbarMode.Docked:
                _toolbarHost.Visibility = Visibility.Visible;
                _toolbarHost.Opacity = 1;
                break;
            case DrawToolbarMode.AutoHide:
                _autoHide = new AutoHideLogic(p.AutoHideEdge);
                _toolbarHost.Visibility = Visibility.Visible;
                SetToolbarShown(false, animate: false);
                _autoHideTimer.Start();
                break;
            default:
                _toolbarHost.Visibility = Visibility.Collapsed;
                ShowHint();
                break;
        }

        PlaceToolbar();
    }

    private void PlaceToolbar()
    {
        double w = ActualWidth > 0 ? ActualWidth : Width, h = ActualHeight > 0 ? ActualHeight : Height;
        if (_toolbar.Orientation == Orientation.Vertical)
        {
            _toolbar.MaxHeight = Math.Max(120, h - 40);
            _toolbar.MaxWidth = double.PositiveInfinity;
        }
        else
        {
            _toolbar.MaxWidth = Math.Max(160, w - 40);
            _toolbar.MaxHeight = double.PositiveInfinity;
        }

        // Ориентация и ограничения только что изменились: без явного сброса WPF вернёт размер прежней раскладки.
        _toolbar.InvalidateMeasure();
        _toolbarHost.InvalidateMeasure();

        _toolbarHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var origin = ToolbarOrigin(_profile, new Size(w, h), _toolbarHost.DesiredSize);
        Canvas.SetLeft(_toolbarHost, origin.X);
        Canvas.SetTop(_toolbarHost, origin.Y);
    }

    /// <summary>Отступ панели от края экрана: у края автоскрытия и у верхнего края постоянной панели по умолчанию.</summary>
    public const double EdgeMargin = 8, DockedTop = 16;

    /// <summary>
    /// Левый верхний угол панели (DIP окна рисования) для режима и края из профиля. Одна функция для окна рисования и превью
    /// в настройках — превью ставит панель ровно так же. Панель всегда целиком внутри экрана.
    /// </summary>
    public static Point ToolbarOrigin(DrawingProfile p, Size area, Size bar)
    {
        double w = area.Width, h = area.Height, x, y;
        if (p.ToolbarMode == DrawToolbarMode.AutoHide)
        {
            (x, y) = p.AutoHideEdge switch
            {
                ScreenEdge.Top => ((w - bar.Width) / 2, EdgeMargin),
                ScreenEdge.Bottom => ((w - bar.Width) / 2, h - bar.Height - EdgeMargin),
                ScreenEdge.Left => (EdgeMargin, (h - bar.Height) / 2),
                _ => (w - bar.Width - EdgeMargin, (h - bar.Height) / 2),
            };
        }
        else
        {
            double fx = p.ToolbarX ?? 0.5, fy = p.ToolbarY ?? 0;
            x = (fx * w) - (bar.Width / 2);
            y = p.ToolbarY is null ? DockedTop : (fy * h) - (bar.Height / 2);
        }

        return new Point(Math.Clamp(x, 0, Math.Max(0, w - bar.Width)), Math.Clamp(y, 0, Math.Max(0, h - bar.Height)));
    }

    private void AutoHideTick()
    {
        if (_autoHide is null || !IsLoaded)
        {
            return;
        }

        var c = CursorPoint();
        var rect = new Rect(Canvas.GetLeft(_toolbarHost), Canvas.GetTop(_toolbarHost), _toolbarHost.ActualWidth, _toolbarHost.ActualHeight);
        bool was = _autoHide.Shown;
        bool shown = _autoHide.Update(c, new Size(ActualWidth, ActualHeight), rect, _clock.Elapsed, hold: RadialOpen || IsEditingText || _dragging || _toolbarHost.IsMouseOver);
        if (shown != was)
        {
            SetToolbarShown(shown, animate: true);
        }
    }

    /// <summary>Показать панель автоскрытия раскрытой (снимки для справки и обзора; без ожидания указателя у края).</summary>
    public void RevealToolbarForPreview()
    {
        _autoHide?.ForceShow();
        SetToolbarShown(true, animate: false);
    }

    /// <summary>Подпись для снимков: напечатать текст в открытом поле ввода (как пользователь), без подтверждения.</summary>
    public void TypeForPreview(string text)
    {
        if (_editorBox is not null)
        {
            _editorBox.Text = text;
        }
    }

    /// <summary>Плавное появление и скрытие у края: сдвиг к краю и прозрачность, 180 мс.</summary>
    private void SetToolbarShown(bool shown, bool animate)
    {
        double off = 0;
        var edge = _profile.AutoHideEdge;
        _toolbarHost.InvalidateMeasure();
        _toolbarHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double extent = (edge is ScreenEdge.Left or ScreenEdge.Right ? _toolbarHost.DesiredSize.Width : _toolbarHost.DesiredSize.Height) + 12;
        if (!shown)
        {
            off = edge is ScreenEdge.Top or ScreenEdge.Left ? -extent : extent;
        }

        var prop = edge is ScreenEdge.Left or ScreenEdge.Right ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        _toolbarHost.IsHitTestVisible = shown;
        if (!animate)
        {
            _toolbarSlide.BeginAnimation(prop, null);
            _toolbarHost.BeginAnimation(OpacityProperty, null);
            _toolbarSlide.SetValue(prop, off);
            _toolbarHost.Opacity = shown ? 1 : 0;
            return;
        }

        var d = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _toolbarSlide.BeginAnimation(prop, new DoubleAnimation(off, d) { EasingFunction = ease });
        _toolbarHost.BeginAnimation(OpacityProperty, new DoubleAnimation(shown ? 1 : 0, d) { EasingFunction = ease });
    }

    private readonly HashSet<DrawTool> _hinted = new();

    /// <summary>
    /// Phase 4I: короткая подсказка о клавишах инструмента — один раз за сеанс рисования для каждого инструмента (без постоянных
    /// инструкций на экране). Полное описание — в справке.
    /// </summary>
    private void ShowToolHint(DrawTool tool)
    {
        string? key = tool switch
        {
            DrawTool.Rectangle or DrawTool.RoundedRectangle or DrawTool.Ellipse => "Draw.Hint.Box",
            DrawTool.Line or DrawTool.Arrow => "Draw.Hint.Line",
            DrawTool.Polygon => "Draw.Hint.Polygon",
            DrawTool.Select => "Draw.Hint.Select",
            DrawTool.SmartPen => "Draw.Hint.SmartPen",
            _ => null,
        };
        if (key is null || !_hinted.Add(tool))
        {
            return;
        }

        _hint.Text = Loc.T(key);
        _hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = ActualWidth > 0 ? ActualWidth : Width;
        Canvas.SetLeft(_hint, (w - _hint.DesiredSize.Width) / 2);
        Canvas.SetTop(_hint, 16);
        Flash(_hint, TimeSpan.FromSeconds(3.5));
    }

    /// <summary>Текст последней подсказки (для проверок).</summary>
    internal string HintText => _hint.Text;

    private void ShowHint()
    {
        _hint.Text = Loc.T("Draw.RadialOnlyHint");
        _hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = ActualWidth > 0 ? ActualWidth : Width;
        Canvas.SetLeft(_hint, (w - _hint.DesiredSize.Width) / 2);
        Canvas.SetTop(_hint, 16);
        Flash(_hint, TimeSpan.FromSeconds(4));
    }

    /// <summary>Короткое ненавязчивое уведомление внизу по центру (сохранение снимка, ошибка).</summary>
    public void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = ActualWidth > 0 ? ActualWidth : Width, h = ActualHeight > 0 ? ActualHeight : Height;
        Canvas.SetLeft(_toast, (w - _toast.DesiredSize.Width) / 2);
        Canvas.SetTop(_toast, h - _toast.DesiredSize.Height - 40);
        Flash(_toast, TimeSpan.FromSeconds(2.6));
    }

    private static void Flash(UIElement e, TimeSpan hold)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(hold)));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(hold + TimeSpan.FromMilliseconds(400))));
        e.Opacity = 1;
        e.BeginAnimation(OpacityProperty, a);
    }

    // ── рисование ────────────────────────────────────────────────────────────

    /// <summary>Нарисовать фигуру от точки к точке (DIP) — то же, что протягивание мышью (используется и тестами).</summary>
    public void Draw(Point from, Point to, IReadOnlyList<Point>? path = null)
    {
        Begin(from);
        if (path is not null)
        {
            foreach (var p in path)
            {
                Extend(p);
            }
        }

        Extend(to);
        End();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (RadialOpen || (e.OriginalSource is DependencyObject d && IsInChrome(d)))
        {
            return;
        }

        var p = e.GetPosition(_ink);
        if (Settings.Tool == DrawTool.Text)
        {
            CommitText();
            BeginText(p);
            e.Handled = true;
            return;
        }

        CommitText();
        if (Settings.Tool == DrawTool.Marker)
        {
            BeginMarker(p);
            e.Handled = true;
            return;
        }

        if (Settings.Tool == DrawTool.Select)
        {
            if (!BeginGesture(p)) // ручки размера, поворота и точек дуги — раньше перемещения
            {
                BeginSelect(p);
            }

            e.Handled = true;
            return;
        }

        ClearSelection();
        Begin(p);
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_current is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            Extend(e.GetPosition(_ink));
        }
        else if (_movingMarker is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            MoveMarkerPreview(e.GetPosition(_ink));
        }
        else if (_selectDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            DragSelect(e.GetPosition(_ink));
        }
        else if (_gesture != TransformGesture.None && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateGesture(e.GetPosition(_ink));
        }
        else if (Settings.Tool == DrawTool.Select && e.LeftButton == MouseButtonState.Released)
        {
            HoverSelect(e.GetPosition(_ink));
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_current is not null)
        {
            ReleaseMouseCapture();
            End();
        }
        else if (_movingMarker is not null)
        {
            ReleaseMouseCapture();
            EndMarkerMove(e.GetPosition(_ink));
        }
        else if (_selectDragging)
        {
            ReleaseMouseCapture();
            EndSelect(e.GetPosition(_ink));
        }
        else if (_gesture != TransformGesture.None)
        {
            UpdateGesture(e.GetPosition(_ink));
            EndGesture();
        }
    }

    /// <summary>Правая кнопка — радиальное меню у указателя (в любом режиме панели; так панель никогда не обязательна).</summary>
    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsInChrome(d))
        {
            return;
        }

        e.Handled = true;
        RadialRequested?.Invoke();
    }

    private void Begin(Point p)
    {
        _from = p;
        var stroke = Settings.Color.ToFrozenBrush();
        _current = Settings.Tool switch
        {
            DrawTool.Pen or DrawTool.SmartPen => new Polyline { Points = [p] },
            DrawTool.Line => new Line { X1 = p.X, Y1 = p.Y, X2 = p.X, Y2 = p.Y },
            DrawTool.Arrow => new Path(),
            DrawTool.Rectangle => new Rectangle(),
            DrawTool.RoundedRectangle => new Rectangle(),
            DrawTool.Polygon => new Polygon(),
            _ => new Ellipse(),
        };
        _stabilizer.Reset();
        _recognizeCountdown = 0;
        _current.Stroke = stroke;
        _current.StrokeThickness = Settings.Thickness;
        _current.StrokeLineJoin = PenLineJoin.Round;
        _current.StrokeStartLineCap = _current.StrokeEndLineCap = PenLineCap.Round;
        _current.StrokeDashCap = PenLineCap.Round;
        _current.StrokeDashArray = Settings.Dash switch
        {
            DrawDash.Dashed => [3, 2],
            DrawDash.Dotted => [0.01, 2],
            _ => null,
        };
        _current.IsHitTestVisible = false;
        _current.Effect = NeonGlow(Settings.Color, Settings.Thickness);
        _ink.Children.Add(_current);
        Extend(p);
    }

    private void Extend(Point p)
    {
        _lastPointer = p;
        // Phase 4I: Shift и Alt во время жеста — линия и стрелка с шагом 15°, многоугольник с поворотом шагом 15°,
        // прямоугольники и эллипсы — равные стороны (Shift) и от центра (Alt). Исходная точка не меняется.
        if (ShiftDown && _current is Line or Path or Polygon)
        {
            p = DrawingGeometry.SnapDirection(_from, p);
        }

        switch (_current)
        {
            case Polyline pl:
                pl.Points.Add(p);
                if (Settings.Tool == DrawTool.SmartPen)
                {
                    UpdateCandidate(pl);
                }

                break;
            case Polygon poly:
                // Правильный многоугольник: от центра (точка нажатия) к вершине — радиус и поворот; фигура видна во время построения.
                poly.Points = RegularPolygon(_from, p, Settings.PolygonSides);
                break;
            case Line l:
                l.X2 = p.X;
                l.Y2 = p.Y;
                break;
            case Path arrow:
                arrow.Data = ArrowGeometry(_from, p, Settings.Thickness);
                RememberArrow(arrow, _from, p, Settings.Thickness);
                break;
            case Rectangle or Ellipse:
                var r = DrawingGeometry.Constrain(_from, p, ShiftDown, AltDown);
                Canvas.SetLeft(_current, r.X);
                Canvas.SetTop(_current, r.Y);
                _current.Width = r.Width;
                _current.Height = r.Height;
                if (_current is Rectangle rect && Settings.Tool == DrawTool.RoundedRectangle)
                {
                    rect.RadiusX = rect.RadiusY = EffectiveRadius(Settings.CornerRadius, r.Width, r.Height);
                }

                break;
        }
    }

    private void End()
    {
        if (_current is null)
        {
            return;
        }

        var done = _current;
        _current = null;
        var angle = _stabilizer.Current is { Kind: RecognizedKind.Angle } a ? a : null;
        ClearCandidate(done as Polyline);
        HideAngleLabel(); // подпись угла — только пока кнопка нажата (постоянная — см. ниже)
        if (done is Polygon { Points.Count: 0 } || (done is Polygon poly && (poly.Points[0] - _from).Length < 2))
        {
            _ink.Children.Remove(done); // щелчок без протягивания — многоугольник нулевого размера не создаётся
            return;
        }

        Push(done);
        if (Settings.Tool == DrawTool.SmartPen && done is Polyline pl)
        {
            TrySmartReplace(pl);
            if (Settings.AngleLabels && angle is { Vertices: [var av, var vv, var bv] } && _shapes.LastOrDefault()?.Element is Polyline { Points.Count: 3 })
            {
                // Постоянная подпись угла (включается на панели): отдельный текст рядом с вершиной, его можно двигать.
                var label = AngleLabelBlock((int)Math.Round(angle.AngleDegrees) + "°");
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var at = DrawingGeometry.AngleLabelPlace(av, vv, bv, new Point(-1000, -1000), label.DesiredSize, new Rect(0, 0, Math.Max(1, _ink.ActualWidth), Math.Max(1, _ink.ActualHeight)));
                Canvas.SetLeft(label, at.X);
                Canvas.SetTop(label, at.Y);
                _ink.Children.Add(label);
                var shape = new DrawnShape(label, _shapes[^1].FadeAt);
                _shapes.Add(shape);
                _undo.Peek().Add(shape); // одна запись истории: замена штриха углом и подпись
            }
        }
    }

    // ── умный карандаш ──────────────────────────────────────────────────────

    /// <summary>Сколько штрихов умный карандаш превратил в фигуры (для проверок).</summary>
    public int SmartReplacements { get; private set; }

    /// <summary>
    /// Завершённый штрих умного карандаша: при уверенном распознавании прямой, эллипса, прямоугольника или многоугольника штрих заменяется
    /// редактируемой фигурой с теми же цветом, толщиной, прозрачностью и типом линии. Замена — отдельный шаг истории:
    /// отмена возвращает исходный штрих. При низкой уверенности остаётся штрих.
    /// </summary>
    private void TrySmartReplace(Polyline stroke)
    {
        if (ShapeRecognizer.Recognize(stroke.Points.ToList()) is not { } r)
        {
            return;
        }

        var shape = CreateRecognized(r);

        shape.Stroke = stroke.Stroke;
        shape.StrokeThickness = stroke.StrokeThickness;
        shape.StrokeLineJoin = stroke.StrokeLineJoin;
        shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round;
        shape.StrokeDashCap = stroke.StrokeDashCap;
        shape.StrokeDashArray = stroke.StrokeDashArray;
        shape.Opacity = stroke.Opacity;
        shape.IsHitTestVisible = false;
        var old = _shapes.Last(s => ReferenceEquals(s.Element, stroke));
        var replacement = new DrawnShape(shape, old.FadeAt);
        Remove(old);
        Add(replacement);
        _undo.Push([old, replacement]);
        _redo.Clear();
        SmartReplacements++;
    }

    /// <summary>Тест и самопроверка: штрих умного карандаша по точкам (как движение мыши).</summary>
    public void SmartStroke(IReadOnlyList<Point> points)
    {
        var tool = Settings.Tool;
        SetSettings(Settings with { Tool = DrawTool.SmartPen });
        Begin(points[0]);
        foreach (var p in points.Skip(1))
        {
            Extend(p);
        }

        End();
        SetSettings(Settings with { Tool = tool });
    }

    // ── нумерованные отметки ─────────────────────────────────────────────────

    private DrawnShape? _movingMarker;
    private Point _markerGrab;
    private int _markerStart = 1;
    private int _markerSeries;

    /// <summary>Отметки текущей серии на экране (номера по порядку).</summary>
    public IReadOnlyList<int> MarkerNumbers => _shapes.Select(s => s.Element).OfType<MarkerElement>().Where(m => m.Series == _markerSeries).Select(m => m.Number).OrderBy(n => n).ToList();

    /// <summary>
    /// Следующий номер: на единицу больше наибольшего среди видимых отметок текущей серии, иначе — начальный. Поэтому после
    /// отмены нумерация продолжается согласованно (отменённый номер выдаётся снова).
    /// </summary>
    public int NextMarkerNumber => MarkerNumbers.Count == 0 ? _markerStart : MarkerNumbers.Max() + 1;

    /// <summary>Начальный номер и сброс нумерации: новая серия начинается с <paramref name="start"/>.</summary>
    public void ResetMarkers(int start)
    {
        _markerStart = Math.Clamp(start, 0, 999);
        _markerSeries++;
    }

    public void SetMarkerStart(int start)
    {
        if (MarkerNumbers.Count == 0)
        {
            _markerStart = Math.Clamp(start, 0, 999);
        }
    }

    public double MarkerSize { get; set; } = 36;

    private void BeginMarker(Point p)
    {
        var hit = _shapes.LastOrDefault(s => s.Element is MarkerElement m && (p - m.Center).Length <= m.Size / 2);
        if (hit is not null)
        {
            _movingMarker = hit;
            _markerGrab = p;
            CaptureMouse();
            return;
        }

        PlaceMarker(p);
    }

    /// <summary>Поставить отметку со следующим номером (тесты и самопроверка вызывают напрямую).</summary>
    public void PlaceMarker(Point center)
    {
        var m = new MarkerElement(NextMarkerNumber, _markerSeries, MarkerSize, Settings.Color, center);
        _ink.Children.Add(m);
        Push(m);
    }

    private void MoveMarkerPreview(Point p)
    {
        if (_movingMarker?.Element is MarkerElement m)
        {
            m.RenderTransform = new TranslateTransform(p.X - _markerGrab.X, p.Y - _markerGrab.Y);
        }
    }

    /// <summary>Перемещение отметки — шаг истории: прежняя отметка заменяется такой же на новом месте.</summary>
    private void EndMarkerMove(Point p)
    {
        var old = _movingMarker;
        _movingMarker = null;
        if (old?.Element is not MarkerElement m)
        {
            return;
        }

        m.RenderTransform = null;
        var delta = p - _markerGrab;
        if (delta.Length < 2)
        {
            return;
        }

        MoveMarker(old, m.Center + delta);
    }

    /// <summary>Переместить отметку (шаг истории). Возвращает новую фигуру.</summary>
    public void MoveMarker(int number, Point center)
    {
        if (_shapes.LastOrDefault(s => s.Element is MarkerElement m && m.Number == number && m.Series == _markerSeries) is { } shape)
        {
            MoveMarker(shape, center);
        }
    }

    private void MoveMarker(DrawnShape old, Point center)
    {
        var m = (MarkerElement)old.Element;
        var moved = new DrawnShape(new MarkerElement(m.Number, m.Series, m.Size, m.ColorValue, center), old.FadeAt);
        Remove(old);
        Add(moved);
        _undo.Push([old, moved]);
        _redo.Clear();
    }

    private void Push(FrameworkElement element)
    {
        var shape = new DrawnShape(element, Settings.FadeSec > 0 ? DateTime.UtcNow.AddSeconds(Settings.FadeSec) : null);
        _shapes.Add(shape);
        _undo.Push([shape]);
        _redo.Clear();
        if (shape.FadeAt is not null)
        {
            _fade.Start();
        }
    }

    public static Geometry ArrowGeometry(Point from, Point to, double thickness)
    {
        var v = to - from;
        double len = v.Length;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(from, false, false);
            c.LineTo(to, true, true);
            if (len > 1)
            {
                v /= len;
                double head = Math.Max(10, thickness * 3.5);
                var n = new Vector(-v.Y, v.X);
                c.BeginFigure(to - (v * head) + (n * head * 0.55), false, false);
                c.LineTo(to, true, true);
                c.LineTo(to - (v * head) - (n * head * 0.55), true, true);
            }
        }

        g.Freeze();
        return g;
    }

    // ── текст ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Начать ввод текста в точке (DIP): многострочное поле в пунктирной рамке с ручкой для перемещения. Ctrl+Enter или щелчок
    /// вне поля — подтвердить; Esc — отменить (рисование не закрывается).
    /// </summary>
    public void BeginText(Point at)
    {
        CommitText();
        _editorBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            MinWidth = 60,
            Background = new SolidColorBrush(Color.FromArgb(0x20, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Foreground = Settings.Color.ToFrozenBrush(),
            CaretBrush = Settings.Color.ToFrozenBrush(),
            FontSize = Settings.TextSize,
            FontFamily = new FontFamily("Segoe UI"),
            Padding = new Thickness(2),
        };
        System.Windows.Automation.AutomationProperties.SetName(_editorBox, Loc.T("Draw.Tool.Text"));
        _editorBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                CommitText();
            }
        };
        var grip = new Thumb
        {
            Width = 16,
            Height = 16,
            Cursor = Cursors.SizeAll,
            ToolTip = Loc.T("Draw.MoveText"),
            Template = GripTemplate("move"),
        };
        grip.DragDelta += (_, e) => MoveEditor(e.HorizontalChange, e.VerticalChange);
        var panel = new DockPanel();
        DockPanel.SetDock(grip, Dock.Left);
        grip.VerticalAlignment = VerticalAlignment.Top;
        grip.Margin = new Thickness(0, 2, 4, 0);
        panel.Children.Add(grip);
        panel.Children.Add(_editorBox);
        _editor = new Border
        {
            Child = panel,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xC0, 0x0A, 0x84, 0xFF)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(3),
            Cursor = Cursors.IBeam,
        };
        Canvas.SetLeft(_editor, at.X - 22);
        Canvas.SetTop(_editor, at.Y - 6);
        _chrome.Children.Add(_editor);
        if (!TestNoActivate)
        {
            Activate();
        }

        _editorBox.Focus();
        Keyboard.Focus(_editorBox);
    }

    /// <summary>Сдвиг поля ввода до подтверждения (ручка; тесты).</summary>
    public void MoveEditor(double dx, double dy)
    {
        if (_editor is not null)
        {
            Canvas.SetLeft(_editor, Canvas.GetLeft(_editor) + dx);
            Canvas.SetTop(_editor, Canvas.GetTop(_editor) + dy);
        }
    }

    /// <summary>Подтвердить текст: пустой не создаёт объект; непустой — объект на экране с отменой и повтором.</summary>
    public void CommitText()
    {
        if (_editor is null || _editorBox is null)
        {
            return;
        }

        string text = _editorBox.Text.TrimEnd();
        double x = Canvas.GetLeft(_editor) + 22 + 2, y = Canvas.GetTop(_editor) + 6;
        var color = _editorBox.Foreground;
        double size = _editorBox.FontSize;
        RemoveEditor();
        if (text.Length == 0)
        {
            return;
        }

        var block = new TextBlock
        {
            Text = text,
            Foreground = color,
            FontSize = size,
            FontFamily = new FontFamily("Segoe UI"),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        _ink.Children.Add(block);
        Push(block);
    }

    /// <summary>Отменить ввод текста (Esc): объект не создаётся.</summary>
    public void CancelText() => RemoveEditor();

    private void RemoveEditor()
    {
        if (_editor is not null)
        {
            _chrome.Children.Remove(_editor);
        }

        _editor = null;
        _editorBox = null;
    }

    // ── история ──────────────────────────────────────────────────────────────

    public void Undo()
    {
        CommitText();
        if (_undo.Count == 0)
        {
            return;
        }

        var batch = _undo.Pop();
        // Присутствие каждой фигуры пакета переключается: добавленная убирается, убранная (очистка, замена) возвращается.
        Toggle(batch);
        _redo.Push(batch);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var batch = _redo.Pop();
        Toggle(batch);
        _undo.Push(batch);
    }

    public void Clear()
    {
        CancelText();
        if (_shapes.Count == 0)
        {
            return;
        }

        var all = _shapes.ToList();
        foreach (var s in all)
        {
            Remove(s);
        }

        _undo.Push(all);
        _redo.Clear();
    }

    private void Toggle(List<DrawnShape> batch)
    {
        var present = batch.Where(_shapes.Contains).ToList();
        var absent = batch.Where(s => !_shapes.Contains(s)).ToList();
        foreach (var s in present)
        {
            Remove(s);
        }

        foreach (var s in absent)
        {
            Add(s);
        }
    }

    private void Add(DrawnShape s)
    {
        if (!_shapes.Contains(s))
        {
            _shapes.Add(s);
            s.Element.Opacity = 1;
            _ink.Children.Add(s.Element);
        }
    }

    private void Remove(DrawnShape s)
    {
        _shapes.Remove(s);
        _ink.Children.Remove(s.Element);
    }

    /// <summary>Исчезновение по сроку: объект удаляется и выпадает из истории (его нельзя вернуть отменой).</summary>
    internal void FadeTick(DateTime? now = null)
    {
        var t = now ?? DateTime.UtcNow;
        foreach (var s in _shapes.Where(s => s.FadeAt is { } at && t >= at).ToList())
        {
            Remove(s);
            foreach (var batch in _undo.Concat(_redo))
            {
                batch.Remove(s);
            }
        }

        if (!_shapes.Any(s => s.FadeAt is not null))
        {
            _fade.Stop();
        }
    }

    // ── снимок ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Снимок монитора с рисунками без служебных элементов: текст подтверждается, панель, меню, подсказки и уведомления скрываются
    /// на время захвата (после отрисовки кадра без них), затем возвращаются.
    /// </summary>
    public async Task<BitmapSource> CaptureAsync()
    {
        CommitText();
        var hidden = new List<(UIElement E, Visibility V)>();
        foreach (UIElement e in _chrome.Children)
        {
            hidden.Add((e, e.Visibility));
            e.Visibility = Visibility.Hidden;
        }

        try
        {
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(90); // кадр без служебных элементов должен дойти до экрана (композиция DWM)
            return ScreenCapture.Capture(Bounds);
        }
        finally
        {
            foreach (var (e, v) in hidden)
            {
                e.Visibility = v;
            }
        }
    }

    private Border Separator() => _toolbar.Orientation == Orientation.Horizontal
        ? new Border { Width = 1, Margin = new Thickness(5, 7, 5, 7), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) }
        : new Border { Height = 1, Margin = new Thickness(7, 5, 7, 5), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) };

    private bool IsInChrome(DependencyObject d)
    {
        for (var x = d; x is not null; x = VisualTreeHelper.GetParent(x) ?? LogicalTreeHelper.GetParent(x))
        {
            if (ReferenceEquals(x, _toolbarHost) || ReferenceEquals(x, _radial) || ReferenceEquals(x, _editor) || x is Popup)
            {
                return true;
            }
        }

        return false;
    }

    // ── радиальное меню ──────────────────────────────────────────────────────

    /// <summary>Точка указателя в координатах окна (DIP).</summary>
    public Point CursorPoint()
    {
        var (x, y) = Screens.Cursor();
        return new Point((x - Bounds.Left) / Scale, (y - Bounds.Top) / Scale);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (!IsEditingText && OnModifierChanged(e))
        {
            e.Handled = true; // Shift/Alt меняют текущий жест (Alt не открывает системное меню)
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (CancelGesture() || CancelDrag())
            {
                // Esc во время изменения размера, поворота или перемещения — только отмена жеста
            }
            else if (FlyoutOpen)
            {
                CloseFlyout(); // Esc закрывает сначала всплывающую группу панели, затем выбор, затем — решает контроллер
            }
            else if (_selected is not null && !RadialOpen)
            {
                ClearSelection();
            }
            else
            {
                EscapeRequested?.Invoke();
            }
        }
        else if (IsEditingText)
        {
            return; // клавиши — в поле ввода текста
        }
        else if (e.Key == Key.Delete && _selected is not null)
        {
            e.Handled = true;
            DeleteSelected();
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Undo();
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Redo();
        }
    }
}

/// <summary>Нумерованная отметка: круг цвета рисования с номером. Центр и размер — DIP окна рисования.</summary>
public sealed class MarkerElement : Grid
{
    public MarkerElement(int number, int series, double size, Settings.ThemeColor color, Point center)
    {
        Number = number;
        Series = series;
        Size = size;
        ColorValue = color;
        Center = center;
        Width = Height = size;
        IsHitTestVisible = false;
        Canvas.SetLeft(this, center.X - (size / 2));
        Canvas.SetTop(this, center.Y - (size / 2));
        Children.Add(new Ellipse { Fill = color.ToFrozenBrush(), Stroke = Brushes.White, StrokeThickness = Math.Max(1.5, size * 0.06) });
        Children.Add(new TextBlock
        {
            Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = size * (number >= 100 ? 0.36 : number >= 10 ? 0.45 : 0.55),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }

    public int Number { get; }

    public int Series { get; }

    public double Size { get; }

    public Settings.ThemeColor ColorValue { get; }

    public Point Center { get; }
}
