using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Presentation;

/// <summary>
/// Окно на весь монитор, которое принимает мышь только пока его режим активен (выбор области, рисование). Почти прозрачный фон
/// (альфа 1/255) нужен, чтобы окно получало щелчки; содержимое рабочего стола под ним видно и продолжает обновляться.
/// Не в панели задач и не в Alt+Tab. <see cref="SetInteractive"/>(false) возвращает click-through и no-activate; закрытие режима
/// закрывает окно целиком — невидимого перехвата мыши не остаётся.
/// </summary>
public abstract class InteractiveOverlayWindow : Window
{
    private readonly PixelRect _bounds;

    /// <summary>Только для тестов: окна режимов не активируются (не забирают фокус у пользователя во время прогона тестов).</summary>
    internal static bool TestNoActivate { get; set; }

    protected InteractiveOverlayWindow(PixelRect bounds, double scale)
    {
        _bounds = bounds;
        Scale = scale;
        ShowActivated = !TestNoActivate;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        Left = bounds.Left / scale;
        Top = bounds.Top / scale;
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;
        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            Screens.SetExStyle(Hwnd, Screens.ExStyle(Hwnd) | Screens.WsExToolWindow);
            Screens.Place(Hwnd, _bounds, activate: Interactive && !TestNoActivate);
        };
        Closed += (_, _) => Hwnd = 0;
    }

    public nint Hwnd { get; private set; }

    public double Scale { get; }

    public PixelRect Bounds => _bounds;

    public bool Interactive { get; private set; } = true;

    /// <summary>Принимать мышь или пропускать её (click-through + no-activate).</summary>
    public void SetInteractive(bool on)
    {
        Interactive = on;
        IsHitTestVisible = on;
        if (Hwnd == 0)
        {
            return;
        }

        long ex = Screens.ExStyle(Hwnd);
        const long passive = Screens.WsExTransparent | Screens.WsExNoActivate;
        Screens.SetExStyle(Hwnd, on ? ex & ~passive : ex | passive);
        if (on && !TestNoActivate)
        {
            Activate();
        }
    }

    /// <summary>Есть ли у окна сейчас стили click-through (для проверок).</summary>
    public bool IsClickThrough => Hwnd != 0 && (Screens.ExStyle(Hwnd) & Screens.WsExTransparent) != 0;

    /// <summary>Точка окна (DIP) → физические пиксели экрана.</summary>
    public (int X, int Y) ToScreen(Point p) => (_bounds.Left + (int)Math.Round(p.X * Scale), _bounds.Top + (int)Math.Round(p.Y * Scale));

    protected static TextBlock Hint(string text) => new()
    {
        Text = text,
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x1E, 0x1E, 0x22)),
        Padding = new Thickness(14, 8, 14, 8),
        FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 24, 0, 0),
        IsHitTestVisible = false,
    };
}

/// <summary>
/// Выбор области линзы: протянуть прямоугольник мышью; щелчок без протягивания — область по умолчанию вокруг точки; Esc или
/// правая кнопка — отмена. Слегка затемняет экран, пока идёт выбор. Результат — физические пиксели.
/// </summary>
public sealed class LensSelectionWindow : InteractiveOverlayWindow
{
    private readonly Canvas _canvas = new();
    private readonly Rectangle _rect = new() { Stroke = Brushes.White, StrokeThickness = 2, StrokeDashArray = [4, 2], Fill = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), Visibility = Visibility.Collapsed };
    private readonly Size _defaultSize;
    private Point? _start;

    public LensSelectionWindow(PixelRect bounds, double scale, Size defaultSizeDip) : base(bounds, scale)
    {
        Title = "Backpack — lens selection";
        _defaultSize = defaultSizeDip;
        Background = new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0));
        Cursor = Cursors.Cross;
        var grid = new Grid();
        _canvas.Children.Add(_rect);
        grid.Children.Add(_canvas);
        grid.Children.Add(Hint(Loc.T("Lens.SelectHint")));
        Content = grid;
        MouseLeftButtonDown += (_, e) =>
        {
            _start = e.GetPosition(_canvas);
            CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (_start is { } s)
            {
                var p = e.GetPosition(_canvas);
                var r = new Rect(s, p);
                Canvas.SetLeft(_rect, r.X);
                Canvas.SetTop(_rect, r.Y);
                _rect.Width = r.Width;
                _rect.Height = r.Height;
                _rect.Visibility = Visibility.Visible;
            }
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (_start is not { } s)
            {
                return;
            }

            ReleaseMouseCapture();
            _start = null;
            Complete(s, e.GetPosition(_canvas));
        };
        MouseRightButtonUp += (_, _) => Cancelled?.Invoke();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancelled?.Invoke();
            }
        };
    }

    public event Action<PixelRect>? Selected;

    public event Action? Cancelled;

    /// <summary>Завершение выбора (также для тестов): маленькое протягивание (меньше 12 DIP) считается щелчком.</summary>
    public void Complete(Point from, Point to)
    {
        var r = new Rect(from, to);
        if (r.Width < 12 || r.Height < 12)
        {
            r = new Rect(to.X - (_defaultSize.Width / 2), to.Y - (_defaultSize.Height / 2), _defaultSize.Width, _defaultSize.Height);
        }

        var (x1, y1) = ToScreen(r.TopLeft);
        var (x2, y2) = ToScreen(r.BottomRight);
        Selected?.Invoke(PixelRect.FromPoints(x1, y1, x2, y2).ClampInto(Bounds));
    }
}

