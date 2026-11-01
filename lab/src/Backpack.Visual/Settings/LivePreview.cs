using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Localization;
using Backpack.Visual.Rendering;

namespace Backpack.Visual.Settings;

/// <summary>
/// Живые превью плагинов (увеличение, линза, фокус, таймер) и «Приватности»: искусственная сцена (окно-«доска» со строками,
/// движущийся указатель) в цветах темы окна и настоящие параметры инструмента. Ввод и экран не используются. Один таймер
/// ~30 кадров/с только пока превью открыто и анимации разрешены; иначе — статичный кадр.
/// <para>
/// Phase 4G: область превью — сам виртуальный монитор. Сцена получает пропорции контейнера (вертикальное превью — вертикальный
/// монитор), её логический размер — размер области, делённый на масштаб <see cref="VirtualSize"/> (элементы не мельче базовой
/// сцены <see cref="W"/>×<see cref="H"/> и не крупнее вдвое), поэтому внутри нет второго уменьшенного «экрана» с пустыми полями.
/// При изменении размера сцена строится заново; позиции считаются от её границ (<see cref="SW"/>, <see cref="SH"/>).
/// </para>
/// </summary>
public sealed class LivePreview : Viewbox, IDisposable
{
    /// <summary>Базовый размер сцены (DIP) — масштаб 1 и пропорции по умолчанию.</summary>
    public const double W = 420, H = 300;

    private readonly Canvas _scene = new() { Width = W, Height = H, ClipToBounds = true };
    private readonly DispatcherTimer _timer;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Action<LivePreview> _build;
    private readonly Func<LivePreview, Action<double>> _frameFactory;
    private readonly List<IDisposable> _owned = new();
    private readonly bool _animate;
    private Action<double> _frame;
    private bool _disposed;

    private LivePreview(Action<LivePreview> build, Func<LivePreview, Action<double>> frame, bool animate)
    {
        Stretch = Stretch.Uniform;
        Margin = new Thickness(0); // границы превью — границы виртуального монитора (затемнение доходит до краёв рамки)
        Child = _scene;
        _build = build;
        _frameFactory = frame;
        _animate = animate;
        build(this);
        _frame = frame(this);
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => _frame(_clock.Elapsed.TotalSeconds);
        _frame(animate ? 0 : 2.2);
        if (animate)
        {
            _timer.Start();
        }


        Unloaded += (_, _) => _timer.Stop();
        Loaded += (_, _) =>
        {
            if (animate && !_disposed)
            {
                _timer.Start();
            }
        };
    }

    public bool IsRunning => _timer.IsEnabled;

    public Canvas Scene => _scene;

    /// <summary>Логическая ширина виртуального монитора сцены.</summary>
    public double SW { get; private set; } = W;

    /// <summary>Логическая высота виртуального монитора сцены.</summary>
    public double SH { get; private set; } = H;

    /// <summary>
    /// Логический размер виртуального монитора для области <paramref name="area"/>: те же пропорции, масштаб — как у базовой
    /// сцены, вписанной в область, но не меньше 0,75 и не больше 2 (элементы остаются крупными и не становятся огромными).
    /// </summary>
    public static Size VirtualSize(Size area)
    {
        if (!(area.Width > 1) || !(area.Height > 1) || double.IsInfinity(area.Width) || double.IsInfinity(area.Height))
        {
            return new Size(W, H);
        }

        double k = Math.Clamp(Math.Min(area.Width / W, area.Height / H), 0.75, 2);
        return new Size(area.Width / k, area.Height / k);
    }

    /// <summary>Ресурс сцены, освобождаемый при перестройке и закрытии превью.</summary>
    internal T Own<T>(T item) where T : IDisposable
    {
        _owned.Add(item);
        return item;
    }

    /// <summary>Перестроить сцену под область превью (вызывается при изменении размера; тесты вызывают напрямую).</summary>
    internal void Resize(Size area)
    {
        var size = VirtualSize(area);
        if (_disposed || (Math.Abs(size.Width - SW) < 1 && Math.Abs(size.Height - SH) < 1))
        {
            return;
        }

        ReleaseOwned();
        SW = size.Width;
        SH = size.Height;
        _scene.Children.Clear();
        _scene.Width = SW;
        _scene.Height = SH;
        _build(this);
        _frame = _frameFactory(this);
        _frame(_animate ? _clock.Elapsed.TotalSeconds : 2.2);
    }

    /// <summary>Область, которую даёт контейнер, — границы виртуального монитора: сцена перестраивается до измерения.</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        Resize(constraint);
        return base.MeasureOverride(constraint);
    }

    private void ReleaseOwned()
    {
        foreach (var d in _owned)
        {
            d.Dispose();
        }

        _owned.Clear();
    }

    /// <summary>Перерисовать кадр с текущими настройками (после изменения на странице).</summary>
    public void Refresh() => _frame(_clock.Elapsed.TotalSeconds);

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        ReleaseOwned();
    }

    // ── сцена ────────────────────────────────────────────────────────────────

    /// <summary>Окно-«доска» в цветах темы: заголовок с точками, крупный заголовок и строки (как иллюстрации сайта).</summary>
    private static Border Board(double w, double h, string title)
    {
        var c = new Canvas { Width = w, Height = h };
        var head = new Border { Width = w, Height = 26, CornerRadius = new CornerRadius(10, 10, 0, 0) };
        head.SetResourceReference(Border.BackgroundProperty, "Ko.HoverBrush");
        c.Children.Add(head);
        for (int i = 0; i < 3; i++)
        {
            var dot = new Ellipse { Width = 7, Height = 7 };
            dot.SetResourceReference(Shape.FillProperty, "Ko.StageLineBrush");
            Canvas.SetLeft(dot, 12 + (i * 12));
            Canvas.SetTop(dot, 9.5);
            c.Children.Add(dot);
        }

        var t = new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Ko.TextBrush");
        Canvas.SetLeft(t, 16);
        Canvas.SetTop(t, 38);
        c.Children.Add(t);
        int rows = Math.Clamp((int)((h - 80) / 38), 2, 30);
        for (int i = 0; i < rows; i++)
        {
            var row = new Border { Width = w - 32, Height = 30, CornerRadius = new CornerRadius(6) };
            row.SetResourceReference(Border.BackgroundProperty, i == 1 ? "Ko.SelectedBrush" : "Ko.SegmentBackBrush");
            Canvas.SetLeft(row, 16);
            Canvas.SetTop(row, 70 + (i * 38));
            c.Children.Add(row);
            var n = new TextBlock { Text = (i + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture), FontSize = 11, FontWeight = FontWeights.SemiBold };
            n.SetResourceReference(TextBlock.ForegroundProperty, "Ko.LogoBrush");
            Canvas.SetLeft(n, 26);
            Canvas.SetTop(n, 78 + (i * 38));
            c.Children.Add(n);
            var bar = new Border { Width = (w - 120) * (0.55 + (0.12 * ((i * 7) % 4))), Height = 7, CornerRadius = new CornerRadius(3.5) };
            bar.SetResourceReference(Border.BackgroundProperty, "Ko.StageLineBrush");
            Canvas.SetLeft(bar, 56);
            Canvas.SetTop(bar, 82 + (i * 38));
            c.Children.Add(bar);
        }

        var board = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = c };
        board.SetResourceReference(Border.BackgroundProperty, "Ko.StagePanelBrush");
        board.SetResourceReference(Border.BorderBrushProperty, "Ko.BorderBrush");
        return board;
    }

    private static System.Windows.Shapes.Path Pointer(double scale = 1)
    {
        var p = SettingsWindow.PreviewPointer();
        p.RenderTransform = new ScaleTransform(scale, scale);
        p.IsHitTestVisible = false;
        return p;
    }

    private static Brush ColorBrush(string hex, Color fallback) =>
        new SolidColorBrush(ThemeColor.TryParse(hex, out var c) ? Color.FromArgb(c.A, c.R, c.G, c.B) : fallback);

    private static double Ease(double x) => 0.5 - (0.5 * Math.Cos(Math.PI * Math.Clamp(x, 0, 1)));

    // ── «Фокус» ──────────────────────────────────────────────────────────────

    /// <summary>
    /// «Фокус»: в режиме «Линза» — отдельное окно-лупа с увеличенной областью и пунктирная рамка области-источника, указатель
    /// водит источником; в режиме «Затемнение» — всё вокруг области затемнено. Оформление — из настроек «Фокуса».
    /// </summary>
    public static LivePreview Focus(Func<FocusSettings> settings, bool animate)
    {
        Border board = null!;
        VisualBrush brush = null!;
        Border lens = null!, header = null!;
        Rectangle source = null!;
        System.Windows.Shapes.Path dim = null!, hole = null!, pointer = null!;
        TextBlock caption = null!;
        return new LivePreview(v =>
        {
            board = Board(v.SW - 40, v.SH - 60, Loc.T("Preview.BoardTitle"));
            Canvas.SetLeft(board, 20);
            Canvas.SetTop(board, 20);
            v._scene.Children.Add(board);
            source = new Rectangle { StrokeThickness = 1.2, StrokeDashArray = [3, 3], RadiusX = 4, RadiusY = 4 };
            source.SetResourceReference(Shape.StrokeProperty, "Ko.LogoBrush");
            v._scene.Children.Add(source);
            dim = new System.Windows.Shapes.Path { Fill = Brushes.Black, IsHitTestVisible = false };
            v._scene.Children.Add(dim);
            hole = new System.Windows.Shapes.Path { IsHitTestVisible = false };
            v._scene.Children.Add(hole);
            brush = new VisualBrush(board) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
            lens = new Border { Background = brush };
            v._scene.Children.Add(lens);
            header = new Border { Height = 18, CornerRadius = new CornerRadius(6, 6, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
            var label = new TextBlock { Text = Loc.T("Focus.Mode.Lens"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
            header.Child = label;
            v._scene.Children.Add(header);
            pointer = Pointer(0.9);
            v._scene.Children.Add(pointer);
            caption = new TextBlock { FontSize = 11 };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "Ko.SecondaryTextBrush");
            Canvas.SetLeft(caption, 20);
            Canvas.SetTop(caption, v.SH - 30);
            v._scene.Children.Add(caption);
        }, v => t =>
        {
            var f = settings();
            double phase = (t % 8) / 8;
            // Вертикальный монитор: линза — под строками, источник ходит в верхней части; горизонтальный — линза справа.
            bool tall = v.SH > v.SW * 1.15;
            double zoom = Math.Clamp(f.Zoom, 1, 3);
            double ow = tall ? Math.Min(v.SW - 60, 260) : 180, oh = tall ? ow * 0.62 : 112, sw = ow / zoom, sh = oh / zoom;
            // Источник ходит по строкам доски; указатель — у правого нижнего угла источника.
            double sx = 70 + (Math.Max(40, v.SW - 270) * Ease(phase < 0.5 ? phase * 2 : 2 - (phase * 2)));
            double sy = 96 + ((tall ? Math.Max(40, v.SH - oh - 230) : Math.Max(40, v.SH - 240)) * Ease((Math.Sin(t * 0.9) + 1) / 2));
            var border = ColorBrush(f.BorderColor, Color.FromRgb(0xD4, 0xD4, 0xD4));
            bool lensMode = f.Mode == FocusMode.Lens;
            Canvas.SetLeft(pointer, sx + sw - 2);
            Canvas.SetTop(pointer, sy + sh - 2);
            caption.Text = lensMode ? Loc.F("Preview.FocusLens", zoom.ToString("0.0#", Loc.Culture)) : Loc.F("Preview.FocusDim", Math.Round(f.DimOpacity * 100));
            Effect? Shadow() => f.Glow
                ? new DropShadowEffect { Color = ((SolidColorBrush)border).Color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.7 }
                : f.Shadow ? new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Direction = 270, Opacity = 0.45 } : null;
            if (lensMode)
            {
                dim.Visibility = hole.Visibility = Visibility.Collapsed;
                source.Visibility = lens.Visibility = header.Visibility = Visibility.Visible;
                source.Width = sw;
                source.Height = sh;
                Canvas.SetLeft(source, sx);
                Canvas.SetTop(source, sy);
                brush.Viewbox = new Rect(sx - 20, sy - 20, sw, sh);
                lens.Width = ow;
                lens.Height = oh;
                lens.CornerRadius = new CornerRadius(Math.Min(f.Radius, oh / 2));
                lens.BorderThickness = new Thickness(f.BorderWidth);
                lens.BorderBrush = border;
                lens.Effect = Shadow();
                double lx = tall ? (v.SW - ow) / 2 : v.SW - ow - 26, ly = tall ? v.SH - oh - 54 : 46;
                Canvas.SetLeft(lens, lx);
                Canvas.SetTop(lens, ly);
                header.Width = Math.Min(96, ow);
                header.Background = border;
                Canvas.SetLeft(header, lx + 10);
                Canvas.SetTop(header, ly - 17);
            }
            else
            {
                source.Visibility = lens.Visibility = header.Visibility = Visibility.Collapsed;
                dim.Visibility = hole.Visibility = Visibility.Visible;
                double hw = Math.Min(190, v.SW - 80), hh = 86, r = Math.Min(f.Radius, hh / 2);
                var rect = new Rect(Math.Min(sx - 30, v.SW - hw - 20), sy - 10, hw, hh);
                dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, v.SW, v.SH)), new RectangleGeometry(rect, r, r));
                dim.Opacity = Math.Clamp(f.DimOpacity, 0, 0.95);
                dim.Effect = f.EdgeSoftness > 0 ? new BlurEffect { Radius = Math.Min(f.EdgeSoftness, 24) } : null;
                hole.Data = new RectangleGeometry(rect, r, r);
                hole.Stroke = border;
                hole.StrokeThickness = f.BorderWidth;
                hole.Effect = Shadow();
            }
        }, animate);
    }

    // ── увеличение у курсора ─────────────────────────────────────────────────

    /// <summary>Увеличение у курсора: круг или скруглённый прямоугольник следует за указателем со сглаживанием из настроек.</summary>
    public static LivePreview CursorZoom(Func<CursorZoomSettings> settings, bool animate)
    {
        Border board = null!, lens = null!;
        VisualBrush brush = null!;
        System.Windows.Shapes.Path pointer = null!;
        Point follow = new(W / 2, H / 2);
        double last = 0;
        return new LivePreview(v =>
        {
            board = Board(v.SW - 40, v.SH - 40, Loc.T("Preview.BoardTitle"));
            Canvas.SetLeft(board, 20);
            Canvas.SetTop(board, 20);
            v._scene.Children.Add(board);
            follow = new Point(v.SW / 2, v.SH / 2);
            brush = new VisualBrush(board) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
            lens = new Border { Background = brush };
            v._scene.Children.Add(lens);
            pointer = Pointer(0.9);
            v._scene.Children.Add(pointer);
        }, v => t =>
        {
            var z = settings();
            var target = new Point((v.SW / 2) + (((v.SW / 2) - 90) * Math.Sin(t * 0.8)), (v.SH / 2) + (((v.SH / 2) - 95) * Math.Sin(t * 1.3)));
            double dt = Math.Clamp(t - last, 0, 0.2);
            last = t;
            double k = z.SmoothingMs <= 0 ? 1 : 1 - Math.Exp(-dt * 1000 / z.SmoothingMs);
            follow = new Point(follow.X + ((target.X - follow.X) * k), follow.Y + ((target.Y - follow.Y) * k));
            double d = Math.Clamp(z.Diameter * 0.55, 60, 170), zoom = z.Zoom;
            lens.Width = lens.Height = d;
            lens.CornerRadius = new CornerRadius(z.Shape == CursorZoomShape.Circle ? d / 2 : Math.Min(18, d / 4));
            lens.BorderThickness = new Thickness(z.BorderWidth);
            lens.BorderBrush = ColorBrush(z.BorderColor, Color.FromRgb(0x31, 0xC9, 0x9A));
            lens.Effect = z.Shadow ? new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.45 } : null;
            Canvas.SetLeft(lens, follow.X - (d / 2));
            Canvas.SetTop(lens, follow.Y - (d / 2));
            double s = d / zoom;
            brush.Viewbox = new Rect(target.X - 20 - (s / 2), target.Y - 20 - (s / 2), s, s);
            Canvas.SetLeft(pointer, target.X);
            Canvas.SetTop(pointer, target.Y);
        }, animate);
    }

    // ── панель плагина ───────────────────────────────────────────────────────

    /// <summary>
    /// Панель плагина (таймер): карточка у правого верхнего угла «экрана» с крупными цифрами, полосой прогресса и акцентом.
    /// Демонстрация сжата: последние 12 секунд отсчёта (предупреждение тёплым цветом) и состояние «закончено» с рамкой акцента.
    /// </summary>
    public static LivePreview Panel(Func<(double TotalSeconds, double FontSize, double Opacity, string Accent, bool Progress)> settings, bool animate)
    {
        Border card = null!, bar = null!, track = null!;
        TextBlock time = null!, title = null!;
        return new LivePreview(v =>
        {
            var board = Board(v.SW - 40, v.SH - 40, Loc.T("Preview.BoardTitle"));
            Canvas.SetLeft(board, 20);
            Canvas.SetTop(board, 20);
            v._scene.Children.Add(board);
            title = new TextBlock { Text = Loc.T("Preview.Timer"), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0xC7, 0xCF, 0xCB)) };
            time = new TextBlock { FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            bar = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left };
            track = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 5, 0, 0), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), Child = bar };
            var stack = new StackPanel();
            stack.Children.Add(title);
            stack.Children.Add(time);
            stack.Children.Add(track);
            card = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.2), Padding = new Thickness(10, 6, 10, 8), Child = stack };
            v._scene.Children.Add(card);
        }, v => t =>
        {
            var (total, font, opacity, accentHex, progress) = settings();
            var accent = ThemeColor.TryParse(accentHex, out var ac) ? Color.FromRgb(ac.R, ac.G, ac.B) : Color.FromRgb(0x31, 0xC9, 0x9A);
            var warm = Color.FromRgb(0xF5, 0xB4, 0x3C);
            double cycle = t % 16;
            double left = Math.Max(0, Math.Min(total, 12) - cycle);
            bool finished = left <= 0;
            bool warn = !finished && left <= 6;
            var shown = TimeSpan.FromSeconds(Math.Ceiling(left));
            time.Text = shown.ToString(shown.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
            time.FontSize = Math.Clamp(font * 0.55, 12, 48);
            time.Foreground = new SolidColorBrush(finished ? accent : warn ? warm : Color.FromRgb(0xF4, 0xF6, 0xF5));
            card.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0.2, 1) * 255), 0x11, 0x15, 0x13));
            card.BorderBrush = new SolidColorBrush(finished ? accent : Color.FromArgb(0x55, accent.R, accent.G, accent.B));
            card.Effect = finished ? new DropShadowEffect { Color = accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.55 } : null;
            track.Visibility = progress ? Visibility.Visible : Visibility.Collapsed;
            bar.Background = new SolidColorBrush(warn ? warm : accent);
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = Math.Max(96, card.DesiredSize.Width);
            card.Width = w;
            bar.Width = Math.Max(0, (w - 22) * (1 - (left / Math.Max(1, total))));
            Canvas.SetLeft(card, v.SW - 34 - w);
            Canvas.SetTop(card, 34);
        }, animate);
    }

    // ── приватность ──────────────────────────────────────────────────────────

    /// <summary>
    /// Приватность (имитация): набор в обычном поле показывается кейкапами текущего стиля; в защищённом поле — только точки и
    /// значок замка, кейкапов нет; после возврата в обычное поле показываются только новые нажатия.
    /// </summary>
    public static LivePreview Privacy(Func<LabSettings> settings, bool animate)
    {
        TextBlock normal = null!, secret = null!, caption = null!;
        Border normalBox = null!, secretBox = null!, lockBadge = null!;
        OverlayPresenter keys = null!;
        var clock = new StopwatchClock();
        string phrase = Loc.T("Preview.SafePhrase"), back = Loc.T("Preview.ReturnPhrase");
        return new LivePreview(v =>
        {
            Border Field(string label, double y, out TextBlock value)
            {
                var l = new TextBlock { Text = label, FontSize = 11 };
                l.SetResourceReference(TextBlock.ForegroundProperty, "Ko.SecondaryTextBrush");
                Canvas.SetLeft(l, 24);
                Canvas.SetTop(l, y - 16);
                v._scene.Children.Add(l);
                value = new TextBlock { FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
                value.SetResourceReference(TextBlock.ForegroundProperty, "Ko.TextBrush");
                var box = new Border { Width = v.SW - 48, Height = 38, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Padding = new Thickness(12, 0, 12, 0), Child = value };
                box.SetResourceReference(Border.BackgroundProperty, "Ko.StagePanelBrush");
                box.SetResourceReference(Border.BorderBrushProperty, "Ko.ControlBorderBrush");
                Canvas.SetLeft(box, 24);
                Canvas.SetTop(box, y);
                v._scene.Children.Add(box);
                return box;
            }

            normalBox = Field(Loc.T("Preview.NormalField"), 36, out normal);
            secretBox = Field(Loc.T("Preview.ProtectedField"), 108, out secret);
            var lockIcon = Icons.Lucide.Icon("lock", 16, "Ko.LogoBrush");
            lockBadge = new Border { Child = lockIcon, Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Visibility = Visibility.Collapsed };
            lockBadge.SetResourceReference(Border.BackgroundProperty, "Ko.SelectedBrush");
            Canvas.SetLeft(lockBadge, v.SW - 60);
            Canvas.SetTop(lockBadge, 114);
            v._scene.Children.Add(lockBadge);
            keys = v.Own(new OverlayPresenter(clock));
            var row = new Viewbox { Width = v.SW - 48, Height = 70, Child = keys.Root, StretchDirection = StretchDirection.DownOnly };
            Canvas.SetLeft(row, 24);
            Canvas.SetTop(row, 170);
            v._scene.Children.Add(row);
            caption = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Width = v.SW - 48 };
            caption.SetResourceReference(TextBlock.ForegroundProperty, "Ko.SecondaryTextBrush");
            Canvas.SetLeft(caption, 24);
            Canvas.SetTop(caption, v.SH - 34);
            v._scene.Children.Add(caption);
        }, v =>
        {
            int lastKey = -1;
            return t =>
            {
                var s = settings();
                keys.Apply(s.Appearance with { Size = 30 }, new RowOptions(TrailMode.None, MouseLabelMode.Graphic, HorizontalAlignment.Center));
                double c = t % 12;
                int phase = c < 4.5 ? 0 : c < 8 ? 1 : 2;
                normalBox.SetResourceReference(Border.BorderBrushProperty, phase != 1 ? "Ko.FocusBrush" : "Ko.ControlBorderBrush");
                secretBox.SetResourceReference(Border.BorderBrushProperty, phase == 1 ? "Ko.FocusBrush" : "Ko.ControlBorderBrush");
                lockBadge.Visibility = phase == 1 ? Visibility.Visible : Visibility.Collapsed;
                int typed = phase == 0 ? (int)Math.Clamp((c - 0.5) / 0.35, 0, phrase.Length) : phrase.Length;
                int dots = phase == 0 ? 0 : (int)Math.Clamp((phase == 1 ? c - 4.8 : 3.0) / 0.3, 0, 9);
                int returned = phase == 2 ? (int)Math.Clamp((c - 8.4) / 0.35, 0, back.Length) : 0;
                normal.Text = phase == 2 ? back[..returned] : phrase[..typed];
                secret.Text = new string('•', dots);
                caption.Text = phase == 1 ? Loc.T("Preview.PrivacyProtected") : Loc.T("Preview.PrivacyNormal");

                // Кейкапы: только нажатия в обычном поле (последние три); в защищённом — пусто, без «догоняющих» нажатий.
                string source = phase == 0 ? phrase[..typed] : phase == 2 ? back[..returned] : "";
                int key = phase == 1 ? -2 : source.Length;
                if (key == lastKey)
                {
                    return;
                }

                lastKey = key;
                var items = source.Where(char.IsLetter).TakeLast(3).Select((ch, i) => new KeycapItem(1000 + i + (phase * 100), char.ToUpperInvariant(ch), char.ToUpperInvariant(ch).ToString(), false, i == 2)).ToList();
                keys.Update(items);
            };
        }, animate);
    }
}
