using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Presentation;

/// <summary>
/// Область «Фокуса» во внутренней модели (физические пиксели): <see cref="Source"/> — прямоугольник рабочего стола,
/// <see cref="Output"/> — окно, в котором показано увеличение (в «Затемнении» вывод не используется: показывается сам
/// источник). Размеры и положения независимы; перемещение вывода не меняет источник, и наоборот. Закрепление фиксирует
/// координаты (а не кадр): содержимое продолжает обновляться. Чистые функции — проверяются тестами.
/// </summary>
public sealed record FocusRegion(PixelRect Source, PixelRect Output)
{
    /// <summary>Из выделения: вывод — выделенный прямоугольник, источник — его центр, уменьшенный в <paramref name="zoom"/> раз.</summary>
    public static FocusRegion FromSelection(PixelRect selected, double zoom) =>
        new(MagnifierSurface.SourceAround(selected.Left + (selected.Width / 2), selected.Top + (selected.Height / 2), selected, Math.Max(1, zoom)), selected);

    public FocusRegion MoveOutput(int dx, int dy, PixelRect bounds) => this with { Output = (Output with { Left = Output.Left + dx, Top = Output.Top + dy }).ClampInto(bounds) };

    public FocusRegion MoveSource(int dx, int dy, PixelRect bounds) => this with { Source = (Source with { Left = Source.Left + dx, Top = Source.Top + dy }).ClampInto(bounds) };

    /// <summary>Новый размер вывода (тянут край): источник пересчитывается по текущему увеличению вокруг своего центра.</summary>
    public FocusRegion ResizeOutput(PixelRect output, double zoom, PixelRect bounds, int min)
    {
        var o = new PixelRect(output.Left, output.Top, Math.Max(min, output.Width), Math.Max(min, output.Height)).ClampInto(bounds);
        return new FocusRegion(Recenter(Source, o, zoom, bounds), o);
    }

    /// <summary>Новое увеличение: размер источника = вывод / увеличение, центр источника сохраняется.</summary>
    public FocusRegion WithZoom(double zoom, PixelRect bounds) => this with { Source = Recenter(Source, Output, zoom, bounds) };

    /// <summary>
    /// Источник прямо под линзой (область источника скрыта): центр источника = центр вывода, размер = вывод / увеличение.
    /// Собственные окна исключены из захвата, поэтому линза не увеличивает саму себя.
    /// </summary>
    public static FocusRegion UnderOutput(PixelRect output, double zoom) =>
        new(MagnifierSurface.SourceAround(output.Left + (output.Width / 2), output.Top + (output.Height / 2), output, Math.Max(1, zoom)), output);

    /// <summary>Перемещение линзы вместе с источником под ней (источник скрыт).</summary>
    public FocusRegion MoveOutputFollowing(int dx, int dy, PixelRect bounds, double zoom) =>
        UnderOutput((Output with { Left = Output.Left + dx, Top = Output.Top + dy }).ClampInto(bounds), zoom);

    /// <summary>Перенос на монитор с другим масштабом: размер вывода в DIP сохраняется (физический — × <paramref name="ratio"/>) вокруг центра.</summary>
    public FocusRegion RescaleOutput(double ratio, PixelRect bounds, double zoom, bool follow)
    {
        int w = Math.Max(1, (int)Math.Round(Output.Width * ratio)), h = Math.Max(1, (int)Math.Round(Output.Height * ratio));
        var o = new PixelRect(Output.Left + ((Output.Width - w) / 2), Output.Top + ((Output.Height - h) / 2), w, h).ClampInto(bounds);
        return follow ? UnderOutput(o, zoom) : new FocusRegion(Recenter(Source, o, zoom, VirtualBounds(bounds, Source)), o);
    }

    private static PixelRect VirtualBounds(PixelRect a, PixelRect b)
    {
        int l = Math.Min(a.Left, b.Left), t = Math.Min(a.Top, b.Top);
        return new PixelRect(l, t, Math.Max(a.Right, b.Right) - l, Math.Max(a.Bottom, b.Bottom) - t);
    }

    private static PixelRect Recenter(PixelRect source, PixelRect output, double zoom, PixelRect bounds) =>
        MagnifierSurface.SourceAround(source.Left + (source.Width / 2), source.Top + (source.Height / 2), output, Math.Max(1, zoom)).ClampInto(bounds);
}

/// <summary>Состояние «Фокуса».</summary>
public enum FocusState { Off, Selecting, Active, Editing }

/// <summary>
/// Затемнение: чёрный слой с прозрачностью <see cref="FocusSettings.DimOpacity"/> на мониторе, кроме «окна» с рамкой.
/// Это не увеличение, не размытие и не снимок: под окном виден живой экран. Click-through, без активации.
/// </summary>
public sealed class DimWindow : NoActivateWindow
{
    private readonly Path _shade = new() { Fill = Brushes.Black, IsHitTestVisible = false };
    private readonly Path _ring = new() { IsHitTestVisible = false };
    private readonly Canvas _canvas = new();

    public DimWindow(MonitorDesc monitor)
    {
        Monitor = monitor;
        _canvas.Children.Add(_shade);
        _canvas.Children.Add(_ring);
        Content = _canvas;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;
        Opacity = 0;
    }

    public MonitorDesc Monitor { get; }

    /// <summary>«Окно» в DIP этого монитора (пусто — монитор затемнён целиком; выделение на другом мониторе).</summary>
    public Rect Hole { get; private set; }

    public double ShadeOpacity => _shade.Opacity;

    public void Show(PixelRect hole, FocusSettings s, bool animate)
    {
        var m = Monitor;
        PhysicalPlacement.Place(this, m.Bounds);
        double w = m.Bounds.Width / m.Scale, h = m.Bounds.Height / m.Scale;
        var local = new Rect((hole.Left - m.Bounds.Left) / m.Scale, (hole.Top - m.Bounds.Top) / m.Scale, hole.Width / m.Scale, hole.Height / m.Scale);
        var screen = new Rect(0, 0, w, h);
        Hole = local.IntersectsWith(screen) ? local : Rect.Empty;
        var outer = new RectangleGeometry(screen);
        Geometry shade = Hole.IsEmpty ? outer : new CombinedGeometry(GeometryCombineMode.Exclude, outer, new RectangleGeometry(Hole, s.Radius, s.Radius));
        _shade.Data = shade;
        _shade.Opacity = s.DimOpacity;
        _shade.Effect = s.EdgeSoftness > 0 ? new BlurEffect { Radius = s.EdgeSoftness } : null;
        var color = ThemeColor.TryParse(s.BorderColor, out var c) ? c.ToWpf() : Colors.LightGray;
        _ring.Data = Hole.IsEmpty ? null : new RectangleGeometry(Hole, s.Radius, s.Radius);
        _ring.Stroke = new SolidColorBrush(color);
        _ring.StrokeThickness = s.BorderWidth;
        _ring.Visibility = s.BorderWidth > 0 && !Hole.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        _ring.Effect = s.Glow ? new DropShadowEffect { Color = color, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.9 } : null;
        if (!IsVisible)
        {
            base.Show();
        }

        if (animate && s.AppearMs > 0 && Opacity < 1)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(s.AppearMs)));
        }
        else
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        }
    }
}

/// <summary>
/// Явный режим редактирования области (команда «Фокус: изменить область»): окно поверх монитора принимает мышь. Перетаскивание
/// внутри пунктирной рамки источника — перемещение источника («Линза», когда источник показан), внутри рамки линзы — её
/// перемещение, за край — изменение размера; Ctrl не нужен (Ctrl + перетаскивание внутри линзы по-прежнему двигает источник).
/// Esc, Enter или двойной щелчок — выход в режим просмотра (окно закрывается, мышь снова проходит насквозь). В режиме просмотра
/// то же делают ручки (<see cref="FocusGrip"/>), не перекрывая экран.
/// </summary>
public sealed class FocusEditWindow : InteractiveOverlayWindow
{
    private const double Grip = 14;
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0)) };
    private readonly Rectangle _box = new() { Stroke = Brushes.White, StrokeThickness = 2, StrokeDashArray = [4, 3], Fill = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)) };
    private readonly Rectangle _src = new() { Stroke = new SolidColorBrush(Color.FromRgb(0x31, 0xC9, 0x9A)), StrokeThickness = 1.5, StrokeDashArray = [2, 2] };
    private readonly TextBlock _hint = new() { Foreground = Brushes.White, FontSize = 14, Padding = new Thickness(10, 6, 10, 6), Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x11, 0x15, 0x13)) };
    private Point _down;
    private string? _drag;
    private FocusRegion _start = new(default, default);

    private readonly bool _showSource;

    public FocusEditWindow(MonitorDesc monitor, FocusRegion region, bool showSource, string hint) : base(monitor.Bounds, monitor.Scale)
    {
        Monitor = monitor;
        Region = region;
        _showSource = showSource;
        _hint.Text = hint;
        _canvas.Children.Add(_box);
        if (showSource)
        {
            _canvas.Children.Add(_src);
        }

        _canvas.Children.Add(_hint);
        Canvas.SetLeft(_hint, 24);
        Canvas.SetTop(_hint, 24);
        Content = _canvas;
        Layout();
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += (_, _) =>
        {
            _drag = null;
            ReleaseMouseCapture();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Enter)
            {
                Done?.Invoke();
            }
        };
    }

    public MonitorDesc Monitor { get; }

    public FocusRegion Region { get; private set; }

    public event Action<FocusRegion>? Changed;

    public event Action? Done;

    private Rect ToLocal(PixelRect r) => new((r.Left - Bounds.Left) / Scale, (r.Top - Bounds.Top) / Scale, r.Width / Scale, r.Height / Scale);

    private void Layout()
    {
        var o = ToLocal(Region.Output);
        Canvas.SetLeft(_box, o.X);
        Canvas.SetTop(_box, o.Y);
        _box.Width = o.Width;
        _box.Height = o.Height;
        var s = ToLocal(Region.Source);
        Canvas.SetLeft(_src, s.X);
        Canvas.SetTop(_src, s.Y);
        _src.Width = s.Width;
        _src.Height = s.Height;
    }

    /// <summary>
    /// Что тянуть по точке: край линзы (e/s/se) — размер; внутри показанного источника — источник; внутри линзы — перемещение
    /// (с Ctrl — источник, как раньше).
    /// </summary>
    internal static string? HitTest(Rect output, Point p, bool ctrl, Rect? source = null)
    {
        bool right = Math.Abs(p.X - output.Right) <= Grip && p.Y >= output.Top - Grip && p.Y <= output.Bottom + Grip;
        bool bottom = Math.Abs(p.Y - output.Bottom) <= Grip && p.X >= output.Left - Grip && p.X <= output.Right + Grip;
        if (right || bottom)
        {
            return right && bottom ? "se" : right ? "e" : "s";
        }

        if (source is { } src && src.Contains(p) && !output.Contains(p))
        {
            return "source";
        }

        var inflated = output;
        inflated.Inflate(Grip, Grip);
        return inflated.Contains(p) ? (ctrl ? "source" : "move") : null;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Done?.Invoke();
            return;
        }

        _down = e.GetPosition(_canvas);
        _drag = HitTest(ToLocal(Region.Output), _down, Keyboard.Modifiers.HasFlag(ModifierKeys.Control), _showSource ? ToLocal(Region.Source) : null);
        _start = Region;
        if (_drag is not null)
        {
            CaptureMouse();
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_drag is null)
        {
            return;
        }

        var p = e.GetPosition(_canvas);
        int dx = (int)Math.Round((p.X - _down.X) * Scale), dy = (int)Math.Round((p.Y - _down.Y) * Scale);
        double zoom = _start.Source.Width > 0 ? (double)_start.Output.Width / _start.Source.Width : 1;
        Region = _drag switch
        {
            "move" => _start.MoveOutput(dx, dy, Bounds),
            "source" => _start.MoveSource(dx, dy, Bounds),
            _ => _start.ResizeOutput(_start.Output with
            {
                Width = _start.Output.Width + (_drag.Contains('e') ? dx : 0),
                Height = _start.Output.Height + (_drag.Contains('s') ? dy : 0),
            }, zoom, Bounds, (int)(48 * Scale)),
        };
        Layout();
        Changed?.Invoke(Region);
    }
}

/// <summary>
/// Инструмент «Фокус»: один механизм выделения, перемещения и изменения размера области; два режима — «Линза» (живое
/// увеличение 1.0–3.0×, источник и вывод раздельно) и «Затемнение» (экран вне выделения затемнён, внутри — без изменений).
/// После выделения — режим просмотра: линза, рамки и затемнение пропускают клики к приложению под ними, а небольшие ручки
/// (<see cref="FocusGrip"/>) принимают мышь: ручка над линзой — перемещение, угол — размер, ручка над пунктирной рамкой — перемещение
/// источника (Phase 4F). Когда источник показан, линза и источник перемещаются независимо; когда скрыт — линза увеличивает то, что
/// прямо под ней, и источник следует за ней. Ручки есть и у области затемнения. Явный режим редактирования (<see cref="ToggleEdit"/>)
/// остаётся; Esc выходит из редактирования, повторный Esc — из инструмента; при выходе закрываются все окна, ручки тоже.
/// </summary>
public sealed class FocusController : IDisposable
{
    private readonly Func<FocusSettings> _settings;
    private readonly Func<IReadOnlyList<MonitorDesc>> _monitors;
    private readonly Func<IEnumerable<nint>> _ownWindows;
    private readonly List<DimWindow> _dim = new();
    private LensSelectionWindow? _selection;
    private MagnifierSurface? _surface;
    private readonly Dictionary<FocusGripKind, FocusGrip> _grips = new();
    private FocusRegion? _dragStart;
    private HoverReveal? _reveal;

    /// <summary>Анимации разрешены (появление управления; иначе — сразу).</summary>
    public Func<bool> Animate { get; set; } = () => SystemParameters.ClientAreaAnimation;

    /// <summary>Управление (ручки) сейчас показано (наведение, перетаскивание или клавиатурный вызов).</summary>
    public bool ControlsVisible => _reveal?.Visible == true;

    /// <summary>Показать управление без наведения (клавиатурная команда редактирования).</summary>
    public void RevealControls() => _reveal?.Reveal();

    /// <summary>Цель наведения: линза или область и (если показан) источник.</summary>
    private PixelRect? RevealTarget()
    {
        if (Region is not { } r)
        {
            return null;
        }

        var a = r.Output;
        if (Mode == FocusMode.Lens && _settings().ShowSource)
        {
            var b = r.Source;
            int l = Math.Min(a.Left, b.Left), t = Math.Min(a.Top, b.Top);
            return new PixelRect(l, t, Math.Max(a.Right, b.Right) - l, Math.Max(a.Bottom, b.Bottom) - t);
        }

        return a;
    }
    private FocusEditWindow? _edit;
    private MonitorDesc? _monitor;
    private bool _disposed;

    public FocusController(Func<FocusSettings> settings, Func<IReadOnlyList<MonitorDesc>> monitors, Func<IEnumerable<nint>>? ownWindows = null)
    {
        _settings = settings;
        _monitors = monitors;
        _ownWindows = ownWindows ?? (() => []);
    }

    public FocusState State { get; private set; } = FocusState.Off;

    public FocusMode Mode { get; private set; } = FocusMode.Lens;

    public FocusRegion? Region { get; private set; }

    public MagnifierSurface? Surface => _surface;

    public IReadOnlyList<DimWindow> DimWindows => _dim;

    public FocusEditWindow? EditWindow => _edit;

    public LensSelectionWindow? Selection => _selection;

    /// <summary>Есть окно, перекрывающее монитор и принимающее мышь (выделение или редактирование). В режиме просмотра — false: мышь принимают только ручки.</summary>
    public bool CapturesMouse => _selection is not null || _edit is not null;

    /// <summary>Ручки режима просмотра (пусто вне режима просмотра).</summary>
    public IReadOnlyCollection<FocusGrip> Grips => _grips.Values;

    public FocusGrip? GripOf(FocusGripKind kind) => _grips.GetValueOrDefault(kind);

    /// <summary>Источник следует за линзой: режим «Линза» и область источника скрыта.</summary>
    private bool Follows => Mode == FocusMode.Lens && !_settings().ShowSource;

    public event Action<FocusState>? StateChanged;

    /// <summary>Начать: выделение области на мониторе под указателем.</summary>
    public void Start(FocusMode mode)
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        Mode = mode;
        var (x, y) = Screens.Cursor();
        _monitor = MonitorResolver.UnderCursor(_monitors(), (x, y));
        if (_monitor is null)
        {
            return;
        }

        var s = _settings();
        _selection = new LensSelectionWindow(_monitor.Bounds, _monitor.Scale, new Size(s.AreaWidth, s.AreaHeight));
        _selection.Selected += r => Activate(r);
        _selection.Cancelled += Stop;
        _selection.Show();
        if (!InteractiveOverlayWindow.TestNoActivate)
        {
            _selection.Activate();
        }

        SetState(FocusState.Selecting);
    }

    /// <summary>Показать область без интерактивного выделения (тесты, демонстрация, команда «повторить последнюю»).</summary>
    public void Activate(PixelRect selected)
    {
        CloseSelection();
        var s = _settings();
        // Монитор — тот, где центр выбранной области (а не где был указатель при запуске): масштаб рамок и ручек — его.
        _monitor = MonitorResolver.UnderCursor(_monitors(), (selected.Left + (selected.Width / 2), selected.Top + (selected.Height / 2))) ?? _monitor;
        Region = FocusRegion.FromSelection(selected, Mode == FocusMode.Lens ? s.Zoom : 1);
        SetState(FocusState.Active);
        Show(animate: true);
    }

    /// <summary>Новые настройки (увеличение, оформление) применяются к открытой области на месте.</summary>
    public void ApplySettings()
    {
        if (Region is not { } r || _monitor is null || State is FocusState.Off or FocusState.Selecting)
        {
            return;
        }

        if (Mode == FocusMode.Lens)
        {
            // Источник скрыт — он снова прямо под линзой (прежняя закреплённая область не остаётся источником незаметно).
            Region = Follows ? FocusRegion.UnderOutput(r.Output, _settings().Zoom) : r.WithZoom(_settings().Zoom, VirtualBounds());
        }

        Show(animate: false);
    }

    /// <summary>Режим редактирования (перемещение, размер) ↔ режим просмотра.</summary>
    public void ToggleEdit()
    {
        if (State == FocusState.Editing)
        {
            EndEdit();
        }
        else if (State == FocusState.Active && Region is { } r && _monitor is not null)
        {
            ShowSource(null, null); // в редактировании источник показывает окно редактирования
            CloseGrips();
            bool showSource = Mode == FocusMode.Lens && _settings().ShowSource;
            _edit = new FocusEditWindow(_monitor, r, showSource, Localization.Loc.T(showSource ? "Focus.EditHint" : "Focus.EditHintFollow"));
            _edit.Changed += region =>
            {
                Region = Mode == FocusMode.Dim ? region with { Source = region.Output }
                    : Follows ? FocusRegion.UnderOutput(region.Output, _settings().Zoom)
                    : region;
                Show(animate: false);
            };
            _edit.Done += EndEdit;
            _edit.Show();
            if (!InteractiveOverlayWindow.TestNoActivate)
            {
                _edit.Activate();
            }

            SetState(FocusState.Editing);
        }
    }

    /// <summary>Esc: из редактирования — в просмотр; из выделения или просмотра — выключить инструмент.</summary>
    public void Escape()
    {
        if (State == FocusState.Editing)
        {
            EndEdit();
        }
        else if (State != FocusState.Off)
        {
            Stop();
        }
    }

    private void EndEdit()
    {
        var e = _edit;
        _edit = null;
        e?.Close();
        if (State == FocusState.Editing)
        {
            SetState(FocusState.Active);
            Show(animate: false);
        }
    }

    public void Stop()
    {
        CloseSelection();
        EndEdit();
        CloseGrips();
        _surface?.Dispose();
        _surface = null;
        foreach (var d in _dim)
        {
            d.Close();
        }

        _dim.Clear();
        ShowSource(null, null);
        Region = null;
        SetState(FocusState.Off);
    }

    private SourceOutline? _source;

    /// <summary>Окно с пунктирной рамкой области-источника (режим просмотра «Линзы»). null — убрать.</summary>
    public SourceOutline? SourceOutlineWindow => _source;

    private void ShowSource(PixelRect? source, string? color)
    {
        if (source is not { } rect || _monitor is null)
        {
            _source?.Close();
            _source = null;
            return;
        }

        _source ??= new SourceOutline();
        // Масштаб — того монитора, где источник (он может быть на другом мониторе, чем линза, с другим DPI).
        var at = MonitorResolver.UnderCursor(_monitors(), (rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2))) ?? _monitor;
        _source.Place(rect, at.Scale, color ?? "#31C99A");
        if (!_source.IsVisible)
        {
            _source.Show();
        }
    }

    private void CloseSelection()
    {
        var s = _selection;
        _selection = null;
        s?.Close();
    }

    private void Show(bool animate)
    {
        if (Region is not { } r || _monitor is null)
        {
            return;
        }

        var s = _settings();
        string? label = s.ShowSource ? Localization.Loc.F("Focus.LensLabel", s.Zoom.ToString("0.0#", Localization.Loc.Culture)) : null;
        var style = new MagnifierFrameStyle(s.Radius, s.BorderWidth, s.BorderColor, s.Shadow, s.Glow, false, label);
        if (Mode == FocusMode.Lens)
        {
            _surface ??= new MagnifierSurface(MagnifierShape.RoundedRect, style, () => _ownWindows().Concat(_dim.Select(d => d.Hwnd))
                .Concat(_source is { Hwnd: not 0 } o ? [o.Hwnd] : []).Concat(_grips.Values.Select(g => g.Hwnd).Where(h => h != 0)));
            _surface.SetStyle(MagnifierShape.RoundedRect, style);
            _surface.Open(r.Source, r.Output, _monitor.Scale);
            ShowSource(s.ShowSource && State != FocusState.Editing ? r.Source : null, s.BorderColor);
            UpdateGrips();
            return;
        }

        // Затемнение: мониторы по политике (по умолчанию — только монитор выделения; не все экраны без выбора).
        var all = _monitors();
        var targets = s.DimMonitors switch
        {
            MonitorMode.Multiple => all,
            MonitorMode.Primary => all.Where(m => m.IsPrimary || m.Id == _monitor.Id).ToList(),
            _ => (IReadOnlyList<MonitorDesc>)[_monitor],
        };
        foreach (var gone in _dim.Where(d => !targets.Any(t => t.Id == d.Monitor.Id)).ToList())
        {
            gone.Close();
            _dim.Remove(gone);
        }

        foreach (var m in targets)
        {
            var w = _dim.FirstOrDefault(d => d.Monitor.Id == m.Id);
            if (w is null)
            {
                w = new DimWindow(m);
                _dim.Add(w);
            }

            w.Show(r.Source, s, animate);
        }

        UpdateGrips();
    }

    // ── ручки режима просмотра ──────────────────────────────────────────────

    /// <summary>Объединение мониторов (физические пиксели): по нему ограничивается перемещение между мониторами.</summary>
    private PixelRect VirtualBounds()
    {
        var all = _monitors();
        if (all.Count == 0)
        {
            return _monitor?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
        }

        int l = all.Min(m => m.Bounds.Left), t = all.Min(m => m.Bounds.Top);
        return new PixelRect(l, t, all.Max(m => m.Bounds.Right) - l, all.Max(m => m.Bounds.Bottom) - t);
    }

    /// <summary>Ручки: перемещение и размер линзы (области), перемещение показанного источника. Только в режиме просмотра.</summary>
    private void UpdateGrips()
    {
        if (State != FocusState.Active || Region is not { } r || _monitor is null || _disposed)
        {
            CloseGrips();
            return;
        }

        bool created = false;
        Grip(FocusGripKind.Move, Mode == FocusMode.Lens ? "Focus.Grip.Lens" : "Focus.Grip.Area", r.Output, _monitor, ref created);
        Grip(FocusGripKind.Resize, "Focus.Grip.Resize", r.Output, _monitor, ref created);
        if (Mode == FocusMode.Lens && _settings().ShowSource)
        {
            var sourceMonitor = MonitorResolver.UnderCursor(_monitors(), (r.Source.Left + (r.Source.Width / 2), r.Source.Top + (r.Source.Height / 2))) ?? _monitor;
            Grip(FocusGripKind.Source, "Focus.Grip.Source", r.Source, sourceMonitor, ref created);
            if (_grips.TryGetValue(FocusGripKind.Source, out var sg) && _grips.TryGetValue(FocusGripKind.Move, out var mg) && Overlaps(sg.Rect, mg.Rect))
            {
                sg.Place(r.Source, sourceMonitor, below: true); // ручки не накладываются, когда источник внутри линзы
            }
        }
        else if (_grips.Remove(FocusGripKind.Source, out var old))
        {
            old.Close();
        }

        if (created)
        {
            _surface?.UpdateExclusions(); // новые окна ручек не попадают в увеличенное изображение
        }

        // Ручки видны только при наведении (единый механизм хоста HoverReveal); новые окна сразу получают текущую видимость.
        _reveal ??= new HoverReveal(RevealTarget, () => _grips.Values, () => Animate());
        _reveal.Tick();
    }

    private void Grip(FocusGripKind kind, string labelKey, PixelRect target, MonitorDesc monitor, ref bool created)
    {
        if (!_grips.TryGetValue(kind, out var g))
        {
            g = new FocusGrip(kind, Localization.Loc.T(labelKey));
            g.DragStarted += _ =>
            {
                _dragStart = Region;
                _dragOffset = (0, 0);
                if (_reveal is not null)
                {
                    _reveal.Hold = true; // во время перетаскивания управление не исчезает
                }
            };
            g.Dragged += OnGripDragged;
            g.DragEnded += _ =>
            {
                if (_reveal is not null)
                {
                    _reveal.Hold = false;
                }

                OnGripReleased();
            };
            _grips[kind] = g;
            created = true;
        }

        g.Place(target, monitor);
    }

    private static bool Overlaps(PixelRect a, PixelRect b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private void CloseGrips()
    {
        _reveal?.Dispose();
        _reveal = null;
        foreach (var g in _grips.Values)
        {
            g.Close();
        }

        _grips.Clear();
        _dragStart = null;
    }

    /// <summary>Перетаскивание ручки: смещение от начала (физические пиксели) применяется к области на начало перетаскивания.</summary>
    internal void OnGripDragged(FocusGrip grip, int dx, int dy) => DragBy(grip.Kind, dx, dy);

    /// <summary>То же без окна ручки (тесты и самопроверка): начать, сдвинуть, отпустить.</summary>
    public void DragBy(FocusGripKind kind, int dx, int dy)
    {
        if (State != FocusState.Active || _monitor is null || (_dragStart ?? Region) is not { } start)
        {
            return;
        }

        _dragStart ??= start;
        if (_reveal is not null)
        {
            _reveal.Hold = true; // как у перетаскивания ручки: управление не исчезает до отпускания
        }

        int ddx = dx, ddy = dy;
        dx -= _dragOffset.X;
        dy -= _dragOffset.Y;
        var s = _settings();
        double zoom = Mode == FocusMode.Lens ? Math.Max(1, s.Zoom) : 1;
        var all = VirtualBounds();
        Region = kind switch
        {
            FocusGripKind.Source => start.MoveSource(dx, dy, all),
            FocusGripKind.Resize => ResizeBy(start, dx, dy, zoom),
            _ when Mode == FocusMode.Dim => start.MoveOutput(dx, dy, all) is var moved ? moved with { Source = moved.Output } : start,
            _ when Follows => start.MoveOutputFollowing(dx, dy, all, zoom),
            _ => start.MoveOutput(dx, dy, all),
        };
        if (kind == FocusGripKind.Move && SwitchMonitorIfNeeded(zoom))
        {
            _dragStart = Region;          // дальнейшее смещение — от нового положения на новом мониторе
            _dragOffset = (ddx, ddy);
        }

        Show(animate: false);
    }

    private FocusRegion ResizeBy(FocusRegion start, int dx, int dy, double zoom)
    {
        var bounds = _monitor!.Bounds;
        int min = (int)(48 * _monitor.Scale);
        var output = start.Output with { Width = Math.Max(min, start.Output.Width + dx), Height = Math.Max(min, start.Output.Height + dy) };
        var resized = start.ResizeOutput(output, zoom, bounds, min);
        return Mode == FocusMode.Dim ? resized with { Source = resized.Output } : Follows ? FocusRegion.UnderOutput(resized.Output, zoom) : resized;
    }

    /// <summary>Центр линзы перешёл на другой монитор: он становится монитором «Фокуса»; размер в DIP сохраняется при другом DPI.</summary>
    private bool SwitchMonitorIfNeeded(double zoom)
    {
        if (Region is not { } r || _monitor is null)
        {
            return false;
        }

        var c = (r.Output.Left + (r.Output.Width / 2), r.Output.Top + (r.Output.Height / 2));
        if (MonitorResolver.UnderCursor(_monitors(), c) is not { } m || m.Id == _monitor.Id)
        {
            return false;
        }

        double ratio = m.Scale / _monitor.Scale;
        _monitor = m;
        var next = r.RescaleOutput(ratio, VirtualBounds(), zoom, Follows || Mode == FocusMode.Dim);
        Region = Mode == FocusMode.Dim ? next with { Source = next.Output } : next;
        return true;
    }

    private (int X, int Y) _dragOffset;

    /// <summary>Отпустили ручку: область целиком помещается на мониторе, где оказался центр линзы (у края — сдвиг внутрь).</summary>
    private void OnGripReleased()
    {
        _dragStart = null;
        _dragOffset = (0, 0);
        if (Region is not { } r || _monitor is null)
        {
            return;
        }

        var output = r.Output.ClampInto(_monitor.Bounds);
        double zoom = Mode == FocusMode.Lens ? Math.Max(1, _settings().Zoom) : 1;
        Region = Mode == FocusMode.Dim ? new FocusRegion(output, output) : Follows ? FocusRegion.UnderOutput(output, zoom) : r with { Output = output };
        Show(animate: false);
    }

    /// <summary>Конец перетаскивания без окна ручки (тесты и самопроверка).</summary>
    public void EndDrag()
    {
        if (_reveal is not null)
        {
            _reveal.Hold = false;
        }

        OnGripReleased();
    }

    private void SetState(FocusState s)
    {
        if (State != s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        StateChanged = null;
    }
}

/// <summary>
/// Пунктирная рамка области-источника «Линзы» в режиме просмотра: видно, какая часть экрана увеличивается (как на сайте).
/// Окно без активации и прозрачное для мыши; в само увеличенное изображение не попадает (исключено из источника лупы).
/// </summary>
public sealed class SourceOutline : NoActivateWindow
{
    private readonly Rectangle _box = new() { StrokeThickness = 1.5, StrokeDashArray = [3, 2.5], RadiusX = 4, RadiusY = 4, IsHitTestVisible = false };

    public SourceOutline()
    {
        Title = "Backpack — focus source";
        Content = _box;
    }

    public void Place(PixelRect source, double scale, string color)
    {
        var c = (Color)ColorConverter.ConvertFromString(color);
        _box.Stroke = new SolidColorBrush(c);
        _box.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8 };
        int m = (int)Math.Round(3 * scale);
        PhysicalPlacement.Place(this, new PixelRect(source.Left - m, source.Top - m, source.Width + (2 * m), source.Height + (2 * m)));
        Width = (source.Width / scale) + 6;
        Height = (source.Height / scale) + 6;
    }
}
