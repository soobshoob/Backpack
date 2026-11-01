using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Mouse;

/// <summary>
/// Состояние кнопок: нажатые (достоверно) и неизвестные (после блокировки и т. п. — не изображаются нажатыми);
/// направление последнего импульса прокрутки по осям (−1, 0, +1), пока импульс показывается.
/// </summary>
public readonly record struct MouseButtonState(MouseButtons Pressed, MouseButtons Unknown, int WheelV = 0, int WheelH = 0);

/// <summary>
/// Круг под указателем и значок мыши со смещением. Начало координат указателя — <see cref="Origin"/> внутри элемента.
/// Используется окном у курсора и превью в настройках.
/// </summary>
public sealed class MouseCursorVisual : Canvas
{
    private MouseCursorSettings _settings = MouseCursorSettings.Default;
    private MouseVisual _look = MouseVisual.Default;
    private MouseButtonState _state;

    public MouseCursorVisual()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>Точка острия указателя внутри элемента (DIP).</summary>
    public Point Origin { get; private set; }

    public MouseButtonState State => _state;

    public bool IconShown { get; private set; }

    public bool CircleShown { get; private set; }

    public bool HighlightShown { get; private set; }

    public void Apply(MouseCursorSettings s, MouseVisual look, MouseButtonState state)
    {
        if (s == _settings && look == _look && state == _state && Children.Count > 0)
        {
            return;
        }

        _settings = s;
        _look = look;
        _state = state;
        Children.Clear();

        // Размер окна не зависит от нажатия (круг щелчка появляется только при нажатии, но место под него есть всегда) —
        // иначе окно меняло бы размер и «прыгало» при каждом щелчке.
        double r = Math.Max(s.ShowCircle ? look.ClickSize / 2 : 0, s.ShowHighlight ? s.HighlightSize / 2 : 0);
        double icon = look.IconSize;
        double padding = icon * 0.18;
        double iconWidth = icon * MouseIcon.GridWidth / MouseIcon.GridHeight;
        var iconRect = new Rect(s.OffsetX, s.OffsetY, iconWidth + (2 * padding), icon + (2 * padding));
        var bounds = new Rect(-r, -r, 2 * r, 2 * r);
        if (s.ShowIcon)
        {
            bounds = r > 0 ? Rect.Union(bounds, iconRect) : iconRect;
            bounds.Union(new Point(0, 0));
        }

        Origin = new Point(-bounds.Left + 1, -bounds.Top + 1);
        Width = bounds.Width + 2;
        Height = bounds.Height + 2;

        CircleShown = s.ShowCircle && state.Pressed != MouseButtons.None;
        IconShown = s.ShowIcon;
        HighlightShown = s.ShowHighlight;

        if (s.ShowHighlight)
        {
            // Подсветка курсора: постоянный мягкий ореол цвета подсветки, к краю прозрачный.
            double h = s.HighlightSize / 2;
            var c = s.Highlight;
            var halo = new RadialGradientBrush(Color.FromArgb(c.A, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)) { GradientOrigin = new Point(0.5, 0.5) };
            halo.GradientStops.Insert(1, new GradientStop(Color.FromArgb(c.A, c.R, c.G, c.B), 0.55));
            halo.Freeze();
            var glow = new Ellipse { Width = 2 * h, Height = 2 * h, Fill = halo };
            SetLeft(glow, Origin.X - h);
            SetTop(glow, Origin.Y - h);
            Children.Add(glow);
        }

        if (CircleShown)
        {
            // Круг щелчка — только пока кнопка нажата.
            double cr = look.ClickSize / 2;
            var circle = new Ellipse { Width = 2 * cr, Height = 2 * cr, Fill = look.ClickColor.ToFrozenBrush() };
            SetLeft(circle, Origin.X - cr);
            SetTop(circle, Origin.Y - cr);
            Children.Add(circle);
        }

        if (s.ShowIcon)
        {
            // Материал подложки значка (Phase 4H): заливка или прозрачная подложка с контуром; размеры те же.
            var (fill, outline, width) = BackdropMaterials.Paint(look.Plate, look.IconBackground);
            var pill = new Border
            {
                Width = iconRect.Width,
                Height = iconRect.Height,
                Background = fill,
                BorderBrush = outline,
                BorderThickness = new Thickness(width),
                CornerRadius = new CornerRadius(iconRect.Width * 0.45),
                Padding = new Thickness(Math.Max(0, padding - width)),
                Child = MouseIcon.Create(icon, look, new MouseIconState(state.Pressed, state.Unknown & ~state.Pressed, state.WheelV, state.WheelH)),
            };
            SetLeft(pill, Origin.X + iconRect.Left);
            SetTop(pill, Origin.Y + iconRect.Top);
            Children.Add(pill);
        }
    }
}

/// <summary>Окно у курсора: no-activate, click-through, без панели задач; не мешает кликам и перетаскиванию.</summary>
public sealed class MouseCursorWindow : NoActivateWindow
{
    public MouseCursorWindow()
    {
        Title = "Backpack — мышь у курсора";
        SizeToContent = SizeToContent.WidthAndHeight;
        Left = -32000; // до первого позиционирования — за пределами экрана, без вспышки в случайном месте
        Top = -32000;
        Content = Visual;
    }

    public MouseCursorVisual Visual { get; } = new();
}

/// <summary>
/// Сопровождение указателя без глобального хука движения: пока режим включён, таймер ~60 Гц читает <c>GetCursorPos</c>
/// (физические пиксели, процесс Per-Monitor V2), DPI монитора под указателем (<c>MonitorFromPoint</c> + <c>GetDpiForMonitor</c>)
/// и состояние кнопок из модели источника; окно двигается <c>SetWindowPos</c> без активации. Координаты могут быть
/// отрицательными (мониторы слева/сверху от основного). Положение и действия не сохраняются.
/// </summary>
public sealed partial class MouseCursorController : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(15);

    private readonly Func<MouseButtonState> _buttons;
    private readonly DispatcherTimer _timer;
    private MouseCursorWindow? _window;
    private MouseCursorSettings _settings = MouseCursorSettings.Default;
    private MouseVisual _look = MouseVisual.Default;
    private (int X, int Y, double Scale)? _last;
    private bool _disposed;
    private (int V, int H) _wheel;
    private long _wheelUntil;

    public MouseCursorController(Func<MouseButtonState> buttons)
    {
        _buttons = buttons;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = Interval };
        _timer.Tick += (_, _) => Follow();
    }

    public bool IsRunning => _timer.IsEnabled;

    public bool WindowVisible => _window?.IsVisible == true;

    public nint WindowHandle => _window?.Hwnd ?? 0;

    public bool? WindowStylesApplied => _window?.Hwnd is > 0 ? _window.StylesApplied : null;

    public long Moves { get; private set; }

    public long WheelPulses { get; private set; }

    /// <summary>
    /// Отображение скрыто командой «показать/скрыть»: окно у курсора прячется и таймер останавливается; состояние кнопок
    /// по-прежнему ведёт модель источника, поэтому после возврата показывается текущее состояние.
    /// </summary>
    public bool Suppressed
    {
        get => _suppressed;
        set
        {
            if (_suppressed == value)
            {
                return;
            }

            _suppressed = value;
            _wheel = (0, 0);
            Apply(_settings, _look);
        }
    }

    private bool _suppressed;
    private bool _zoomHidden;
    private int _avoidPx;

    /// <summary>
    /// Временное взаимодействие с увеличением у курсора: скрыть значок или сдвинуть его вправо на <paramref name="avoidPx"/>
    /// физических пикселей (за край области увеличения). Сохранённые настройки не меняются; <c>(false, 0)</c> возвращает
    /// прежнее состояние. Не связано с командой «скрыть отображение» (<see cref="Suppressed"/>).
    /// </summary>
    public void SetZoomInteraction(bool hide, int avoidPx)
    {
        if (_zoomHidden == hide && _avoidPx == avoidPx)
        {
            return;
        }

        _zoomHidden = hide;
        _avoidPx = Math.Max(0, avoidPx);
        _last = null;
        Apply(_settings, _look);
    }

    public bool ZoomHidden => _zoomHidden;

    public int AvoidPixels => _avoidPx;

    /// <summary>Направление показываемого импульса прокрутки (для тестов).</summary>
    public (int V, int H) WheelShown => _wheel;

    /// <summary>
    /// Импульс прокрутки: показывается стрелкой на значке <see cref="MouseVisual.WheelPulseMs"/> мс от последнего импульса.
    /// Мелкие смещения высокоточных колёс тоже дают импульс (важен знак). Смена направления показывается сразу. Колесо не
    /// становится удерживаемым состоянием.
    /// </summary>
    public void Pulse(bool horizontal, int delta)
    {
        if (_disposed || delta == 0)
        {
            return;
        }

        WheelPulses++;
        int dir = Math.Sign(delta);
        _wheel = horizontal ? (0, dir) : (dir, 0);
        _wheelUntil = Environment.TickCount64 + _look.WheelPulseMs;
        if (_window is not null && _timer.IsEnabled)
        {
            _window.Visual.Apply(_settings, _look, Current());
        }
    }

    private MouseButtonState Current()
    {
        if (_wheel != (0, 0) && Environment.TickCount64 >= _wheelUntil)
        {
            _wheel = (0, 0);
        }

        return _buttons() with { WheelV = _wheel.V, WheelH = _wheel.H };
    }

    /// <summary>Только для тестов: окно не выводится на экран.</summary>
    internal bool ShowOnScreen { get; set; } = true;

    internal MouseCursorWindow? Window => _window;

    public void Apply(MouseCursorSettings settings, MouseVisual look)
    {
        if (_disposed)
        {
            return;
        }

        _settings = settings;
        _look = look;
        bool active = settings.Enabled && (settings.ShowIcon || settings.ShowCircle || settings.ShowHighlight) && !_suppressed && !_zoomHidden;
        if (active)
        {
            _window ??= new MouseCursorWindow();
            _window.Visual.Apply(settings, look, Current());
            _last = null;
            if (!_window.IsVisible && ShowOnScreen)
            {
                _window.Show();
            }

            _timer.Start();
            Follow();
        }
        else
        {
            Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _window?.Close();
        _window = null;
    }

    private void Stop()
    {
        _timer.Stop();
        _window?.Hide();
        _last = null;
    }

    private void Follow()
    {
        if (_window is null || _disposed || _window.Hwnd == 0 || !GetCursorPos(out var p))
        {
            return;
        }

        _window.Visual.Apply(_settings, _look, Current());
        double scale = DpiScaleAt(p.X, p.Y);
        var o = _window.Visual.Origin;
        int x = p.X - (int)Math.Round(o.X * scale) + _avoidPx;
        int y = p.Y - (int)Math.Round(o.Y * scale);
        if (_last is { } l && l.X == x && l.Y == y && l.Scale == scale)
        {
            return;
        }

        _last = (x, y, scale);
        Moves++;
        SetWindowPos(_window.Hwnd, -1 /* HWND_TOPMOST */, x, y, 0, 0, SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
    }

    /// <summary>Масштаб монитора под точкой (1.0 = 96 DPI). При ошибке — 1.0.</summary>
    public static double DpiScaleAt(int x, int y)
    {
        nint monitor = MonitorFromPoint(new PointInt { X = x, Y = y }, 2 /* MONITOR_DEFAULTTONEAREST */);
        return monitor != 0 && GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96.0 : 1.0;
    }

    private const uint SwpNoSize = 0x0001, SwpNoActivate = 0x0010, SwpNoOwnerZOrder = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInt
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out PointInt point);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(PointInt pt, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
}
