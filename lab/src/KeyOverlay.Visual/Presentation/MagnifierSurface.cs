using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Presentation;

/// <summary>Форма окна вывода увеличения.</summary>
public enum MagnifierShape { RoundedRect, Ellipse }

/// <summary>Оформление рамки окна вывода (DIP монитора окна; цвет — #RRGGBB / #AARRGGBB).</summary>
public sealed record MagnifierFrameStyle(double Radius, double BorderWidth, string BorderColor, bool Shadow, bool Glow, bool Crosshair, string? Label = null)
{
    public static MagnifierFrameStyle Default { get; } = new(16, 2, "#D4D4D4", true, false, false);
}

/// <summary>
/// Общий движок живого увеличения (Phase 4C). Две независимые величины:
/// <list type="bullet">
/// <item><see cref="SourceRect"/> — прямоугольник рабочего стола (физические пиксели), который показывается;</item>
/// <item><see cref="OutputRect"/> — окно вывода (физические пиксели), где показывается увеличение.</item>
/// </list>
/// Увеличение = размер вывода / размер источника (по ширине). Перемещение окна вывода не меняет источник и наоборот.
/// Окно-хозяин — Win32, layered, click-through (WS_EX_TRANSPARENT), no-activate, topmost; клики проходят к окну под ним
/// без преобразования координат. Собственные окна исключены из источника (<c>MagSetWindowFilterList</c>) — без
/// рекурсии. Курсор Magnification API в изображение не добавляет (нет «второго курсора»). Обновление — таймер ~60 Гц
/// только пока поверхность открыта; при недоступном источнике изображение скрывается (устаревший кадр не выдаётся за живой).
/// </summary>
public sealed class MagnifierSurface : IDisposable
{
    private const string HostClass = "Backpack.MagnifierHost";
    private const uint WsPopup = 0x80000000, WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000;
    private static bool s_classRegistered;
    private static int s_open;

    private readonly Func<IEnumerable<nint>> _exclude;
    private readonly DispatcherTimer _refresh;
    private MagnifierFrame? _frame;
    private MagnifierShape _shape;
    private MagnifierFrameStyle _style;
    private double _scale = 1;
    private nint _host;
    private nint _magnifier;
    private bool _magInit;
    private bool _disposed;
    private double _appliedZoom;

    public MagnifierSurface(MagnifierShape shape, MagnifierFrameStyle style, Func<IEnumerable<nint>> exclude)
    {
        _shape = shape;
        _style = style;
        _exclude = exclude;
        _refresh = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _refresh.Tick += (_, _) => Tick?.Invoke();
        _refresh.Tick += (_, _) => Refresh();
    }

    /// <summary>Каждый кадр перед обновлением изображения (следование за курсором, анимации).</summary>
    public event Action? Tick;

    public LensStatus Status { get; private set; } = LensStatus.Closed;

    public PixelRect SourceRect { get; private set; }

    public PixelRect OutputRect { get; private set; }

    public double Zoom => SourceRect.Width > 0 ? (double)OutputRect.Width / SourceRect.Width : 1;

    public long Frames { get; private set; }

    public long SourceFailures { get; private set; }

    public nint HostHandle => _host;

    public nint FrameHandle => _frame?.Hwnd ?? 0;

    public bool IsOpen => _host != 0;

    /// <summary>Открытые поверхности в процессе (проверка освобождения).</summary>
    public static int OpenCount => Volatile.Read(ref s_open);

    /// <summary>Источник размера вывода <paramref name="output"/> с увеличением <paramref name="zoom"/>, центрированный в (cx, cy).</summary>
    public static PixelRect SourceAround(int cx, int cy, PixelRect output, double zoom)
    {
        int w = Math.Max(1, (int)Math.Round(output.Width / zoom)), h = Math.Max(1, (int)Math.Round(output.Height / zoom));
        return new PixelRect(cx - (w / 2), cy - (h / 2), w, h);
    }

    public unsafe bool Open(PixelRect source, PixelRect output, double scale)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_host != 0)
        {
            SetGeometry(source, output, scale);
            return Status == LensStatus.Live;
        }

        try
        {
            if (!MagnifierApi.MagInitialize())
            {
                Status = LensStatus.Failed;
                return false;
            }
        }
        catch (DllNotFoundException)
        {
            Status = LensStatus.Failed;
            return false;
        }

        _magInit = true;
        Interlocked.Increment(ref s_open);
        nint instance = MagnifierApi.GetModuleHandleW(null);
        if (!s_classRegistered)
        {
            fixed (char* name = HostClass)
            {
                var wc = new MagnifierApi.WndClassEx
                {
                    Size = (uint)sizeof(MagnifierApi.WndClassEx),
                    WndProc = MagnifierApi.GetProcAddress(MagnifierApi.GetModuleHandleW("user32.dll"), "DefWindowProcW"),
                    Instance = instance,
                    Background = MagnifierApi.GetStockObject(4),
                    ClassName = (nint)name,
                };
                s_classRegistered = MagnifierApi.RegisterClassExW(wc) != 0 || System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == 1410;
            }
        }

        uint ex = (uint)(Screens.WsExLayered | Screens.WsExTransparent | Screens.WsExTopmost | Screens.WsExToolWindow | Screens.WsExNoActivate);
        _host = MagnifierApi.CreateWindowExW(ex, HostClass, "Backpack magnifier", WsPopup | WsClipChildren, output.Left, output.Top, output.Width, output.Height, 0, 0, instance, 0);
        if (_host == 0)
        {
            Status = LensStatus.Failed;
            Close();
            return false;
        }

        MagnifierApi.SetLayeredWindowAttributes(_host, 0, 255, 0x2);
        _magnifier = MagnifierApi.CreateWindowExW(0, MagnifierApi.WindowClass, "Backpack magnifier view", WsChild | WsVisible, 0, 0, output.Width, output.Height, _host, 0, instance, 0);
        if (_magnifier == 0)
        {
            Status = LensStatus.Failed;
            Close();
            return false;
        }

        _frame = new MagnifierFrame(_shape, _style, scale);
        _frame.Show();
        SetGeometry(source, output, scale);
        UpdateExclusions();
        Refresh();
        _refresh.Start();
        return Status == LensStatus.Live;
    }

    /// <summary>Новый источник и/или окно вывода (перемещение, изменение размера, следование). Окна не пересоздаются.</summary>
    public unsafe void SetGeometry(PixelRect source, PixelRect output, double scale)
    {
        bool outputChanged = output != OutputRect || Math.Abs(scale - _scale) > 0.001;
        SourceRect = source;
        OutputRect = output;
        _scale = scale;
        if (_host == 0)
        {
            return;
        }

        if (outputChanged)
        {
            Screens.Place(_host, output);
            MagnifierApi.MoveWindow(_magnifier, 0, 0, output.Width, output.Height, true);
            ApplyRegion();
            _frame?.Place(output, scale);
        }

        double zoom = Zoom;
        if (Math.Abs(zoom - _appliedZoom) > 0.0001)
        {
            _appliedZoom = zoom;
            float z = (float)zoom;
            float* m = stackalloc float[9] { z, 0, 0, 0, z, 0, 0, 0, 1 };
            MagnifierApi.MagSetWindowTransform(_magnifier, m);
        }
    }

    public void SetStyle(MagnifierShape shape, MagnifierFrameStyle style)
    {
        _shape = shape;
        _style = style;
        if (_host != 0)
        {
            ApplyRegion();
            _frame?.SetStyle(shape, style);
            _frame?.Place(OutputRect, _scale);
        }
    }

    private void ApplyRegion()
    {
        var o = OutputRect;
        nint rgn = _shape == MagnifierShape.Ellipse
            ? MagnifierApi.CreateRoundRectRgn(0, 0, o.Width + 1, o.Height + 1, o.Width, o.Height)
            : MagnifierApi.CreateRoundRectRgn(0, 0, o.Width + 1, o.Height + 1, (int)Math.Round(_style.Radius * _scale * 2), (int)Math.Round(_style.Radius * _scale * 2));
        MagnifierApi.SetWindowRgn(_host, rgn, true); // система владеет областью после успешного вызова
    }

    /// <summary>Повторить список исключений (появилось другое окно Backpack).</summary>
    public unsafe void UpdateExclusions()
    {
        if (_magnifier == 0)
        {
            return;
        }

        var list = new List<nint> { _host };
        if (_frame?.Hwnd is > 0 and var f)
        {
            list.Add(f);
        }

        list.AddRange(_exclude().Where(h => h != 0));
        var arr = list.Distinct().ToArray();
        fixed (nint* p = arr)
        {
            MagnifierApi.MagSetWindowFilterList(_magnifier, MagnifierApi.FilterModeExclude, arr.Length, p);
        }
    }

    private void Refresh()
    {
        if (_magnifier == 0 || _disposed)
        {
            return;
        }

        var src = SourceRect;
        bool ok = !src.IsEmpty && MagnifierApi.MagSetWindowSource(_magnifier, new Screens.R { Left = src.Left, Top = src.Top, Right = src.Right, Bottom = src.Bottom });
        if (ok)
        {
            if (Status != LensStatus.Live)
            {
                MagnifierApi.ShowWindow(_magnifier, 5);
                _frame?.SetUnavailable(false);
            }

            Status = LensStatus.Live;
            Frames++;
            MagnifierApi.InvalidateRect(_magnifier, 0, true);
        }
        else
        {
            SourceFailures++;
            if (Status != LensStatus.SourceUnavailable)
            {
                MagnifierApi.ShowWindow(_magnifier, 0);
                _frame?.SetUnavailable(true);
            }

            Status = LensStatus.SourceUnavailable;
        }
    }

    public void Close()
    {
        _refresh.Stop();
        if (_magnifier != 0)
        {
            MagnifierApi.DestroyWindow(_magnifier);
            _magnifier = 0;
        }

        if (_host != 0)
        {
            MagnifierApi.DestroyWindow(_host);
            _host = 0;
        }

        _frame?.Close();
        _frame = null;
        _appliedZoom = 0;
        if (_magInit)
        {
            _magInit = false;
            MagnifierApi.MagUninitialize();
            Interlocked.Decrement(ref s_open);
        }

        if (Status != LensStatus.Failed)
        {
            Status = LensStatus.Closed;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _disposed = true;
        Tick = null;
    }
}

/// <summary>
/// Рамка, тень, свечение и (по желанию) отметка реальной точки указателя поверх окна вывода. Click-through, без активации;
/// середина прозрачна (видно увеличение). Отметка в центре показывает, какая точка увеличенного изображения находится под
/// настоящим указателем, — увеличение не вводит в заблуждение о месте реального клика.
/// </summary>
public sealed class MagnifierFrame : NoActivateWindow
{
    private const double FrameMargin = 24;
    private readonly Grid _root = new();
    private readonly Border _shadow = new() { Background = Brushes.Transparent };
    private readonly Path _ring;
    private readonly Path _cross = new() { StrokeThickness = 1.5, Stroke = Brushes.White, IsHitTestVisible = false };
    private readonly TextBlock _unavailable = new() { Foreground = Brushes.White, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, Text = "—" };
    private readonly Border _label = new() { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 3), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly TextBlock _labelText = new() { Foreground = Brushes.White, FontSize = 11.5, FontWeight = FontWeights.SemiBold };
    private MagnifierShape _shape;
    private MagnifierFrameStyle _style;

    public MagnifierFrame(MagnifierShape shape, MagnifierFrameStyle style, double scale)
    {
        _shape = shape;
        _style = style;
        _ring = new Path { IsHitTestVisible = false };
        _root.Margin = new Thickness(FrameMargin);
        _root.Children.Add(_ring);
        _root.Children.Add(_cross);
        _root.Children.Add(_unavailable);
        _label.Child = _labelText;
        _label.Margin = new Thickness(10, -20, 0, 0); // ярлык окна лупы над рамкой (как на сайте), не поверх изображения
        _root.Children.Add(_label);
        Content = _root;
        SetStyle(shape, style);
    }

    public void SetUnavailable(bool on) => _unavailable.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    public void SetStyle(MagnifierShape shape, MagnifierFrameStyle style)
    {
        _shape = shape;
        _style = style;
        var color = (Color)ColorConverter.ConvertFromString(style.BorderColor);
        _ring.Stroke = new SolidColorBrush(color);
        _ring.StrokeThickness = Math.Max(0.01, style.BorderWidth);
        _ring.Visibility = style.BorderWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
        _ring.Effect = style.Glow
            ? new DropShadowEffect { Color = color, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.85 }
            : style.Shadow ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 2, Opacity = 0.55 } : null;
        _cross.Visibility = style.Crosshair ? Visibility.Visible : Visibility.Collapsed;
        _labelText.Text = style.Label ?? "";
        _label.Background = new SolidColorBrush(Color.FromArgb(0xE6, color.R, color.G, color.B));
        _labelText.Foreground = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B) > 150 ? Brushes.Black : Brushes.White;
        _label.Visibility = style.Label is null ? Visibility.Collapsed : Visibility.Visible;
        _cross.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 0, Opacity = 0.9 };
    }

    /// <summary>Разместить рамку вокруг окна вывода (физические пиксели) с полем под тень.</summary>
    public void Place(PixelRect output, double scale)
    {
        double w = output.Width / scale, h = output.Height / scale;
        _ring.Data = _shape == MagnifierShape.Ellipse
            ? new EllipseGeometry(new Rect(0, 0, w, h))
            : new RectangleGeometry(new Rect(0, 0, w, h), _style.Radius, _style.Radius);
        double c = 7;
        _cross.Data = Geometry.Parse(FormattableString.Invariant($"M{(w / 2) - c},{h / 2} H{(w / 2) + c} M{w / 2},{(h / 2) - c} V{(h / 2) + c}"));
        int m = (int)Math.Round(FrameMargin * scale);
        PhysicalPlacement.Place(this, new PixelRect(output.Left - m, output.Top - m, output.Width + (2 * m), output.Height + (2 * m)));
        Width = w + (2 * FrameMargin);
        Height = h + (2 * FrameMargin);
    }
}
