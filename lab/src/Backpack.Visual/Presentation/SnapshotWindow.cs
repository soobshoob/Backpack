using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Backpack.Sdk;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Presentation;

/// <summary>
/// Оформление снимка (DIP; доли 0…1). Значения по умолчанию — как у «Фокуса» и «Линзы»: скругление 16, рамка 2 цвета
/// <see cref="Backpack.Sdk.ToolDefaults.BorderColor"/> (#31C99A), без
/// свечения; тень — как у рамки линзы (<see cref="MagnifierFrame"/>): чёрная, размытие 16, смещение 2, непрозрачность 55 %.
/// </summary>
public sealed record SnapshotStyle(double Radius, double BorderWidth, string BorderColor, double BorderOpacity, bool Glow, bool Shadow, string ShadowColor,
    double ShadowBlur, double ShadowOpacity, string Background, double Opacity)
{
    public static SnapshotStyle Default { get; } = From(new SnapshotRequest());

    public static SnapshotStyle From(SnapshotRequest r) => new(r.Radius, r.BorderWidth, r.BorderColor, r.BorderOpacity, r.Glow, r.Shadow, r.ShadowColor,
        r.ShadowBlur, r.ShadowOpacity, r.Background, r.Opacity);

    internal static Color ParseColor(string hex, Color fallback) => ThemeColor.TryParse(hex, out var c) ? c.ToWpf() : fallback;
}

/// <summary>
/// Тень или свечение под снимком: отдельное окно без активации, прозрачное для мыши, на поле вокруг карточки. Карточка с
/// изображением лежит поверх этого окна, поэтому тень не затемняет снимок. Окно — с запасом размера (<see cref="LayeredWindowSize"/>):
/// изменение размера снимка меняет только содержимое.
/// </summary>
public sealed class SnapshotFrame : NoActivateWindow
{
    private const double ShadowRoom = 72;
    private readonly Border _card = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private LayeredWindowSize _window;
    private SnapshotStyle? _style;

    public SnapshotFrame()
    {
        Title = "Backpack — snapshot shadow";
        Content = _card;
    }

    /// <summary>Тень или свечение видны (иначе окно скрыто).</summary>
    public bool HasEffect => _style is { } s && (s.Glow || (s.Shadow && s.ShadowOpacity > 0));

    public void Set(SnapshotStyle s)
    {
        if (s == _style)
        {
            return;
        }

        _style = s;
        _card.CornerRadius = new CornerRadius(s.Radius);
        _card.Background = new SolidColorBrush(SnapshotStyle.ParseColor(s.Background, Color.FromRgb(0x11, 0x15, 0x13)));
        var border = SnapshotStyle.ParseColor(s.BorderColor, Colors.LightGray);
        // Свечение — как у «Фокуса» (цвет рамки, размытие 22, 90 %); иначе тень рамки линзы с настраиваемым цветом и размытием.
        _card.Effect = s.Glow
            ? new DropShadowEffect { Color = border, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.9 }
            : s.Shadow && s.ShadowOpacity > 0
                ? new DropShadowEffect { Color = SnapshotStyle.ParseColor(s.ShadowColor, Colors.Black), BlurRadius = s.ShadowBlur, ShadowDepth = 2, Opacity = s.ShadowOpacity }
                : null;
    }

    /// <summary>Поле под тень вокруг карточки (физические пиксели).</summary>
    public static int MarginPixels(double scale) => (int)Math.Round(ShadowRoom * scale);

    /// <summary>Разместить тень под карточкой <paramref name="card"/> (физические пиксели).</summary>
    public void Place(PixelRect card, double scale, double opacity, bool topmost)
    {
        int m = MarginPixels(scale);
        _card.Margin = new Thickness(m / scale);
        _card.Width = card.Width / scale;
        _card.Height = card.Height / scale;
        Opacity = opacity;
        var outer = new PixelRect(card.Left - m, card.Top - m, card.Width + (2 * m), card.Height + (2 * m));
        PhysicalPlacement.Place(this, _window.Fit(this, outer, scale), topmost);
    }
}

/// <summary>
/// Окно снимка: изображение в карточке со скруглением и рамкой (рамка поверх края изображения, внутри карточки). Без активации —
/// клавиатурный фокус остаётся у программы пользователя. Принимает мышь (если не включён пропуск щелчков): перетаскивание
/// перемещает снимок, двойной щелчок разворачивает. Окно ровно по размеру карточки — щелчки вне снимка к нему не попадают.
/// Изображение масштабируется с высоким качеством; в размере 1:1 пиксели снимка совпадают с пикселями экрана.
/// </summary>
public sealed class SnapshotWindow : NoActivateWindow
{
    /// <summary>Смещение указателя (физические пиксели), после которого нажатие считается перетаскиванием, а не щелчком.</summary>
    public const int DragThreshold = 4;

    private readonly Grid _root = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Border _clip = new() { ClipToBounds = true };
    private readonly Image _image = new() { SnapsToDevicePixels = true };
    private readonly Border _ring = new() { IsHitTestVisible = false, Background = null };
    private (int X, int Y) _down;
    private bool _pressed;
    private bool _clickThrough;

    public SnapshotWindow()
    {
        Title = "Backpack — snapshot";
        IsHitTestVisible = true;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _clip.Child = _image;
        _root.Children.Add(_clip);
        _root.Children.Add(_ring);
        Content = _root;
        Cursor = Cursors.SizeAll;
        System.Windows.Automation.AutomationProperties.SetName(this, "Backpack snapshot");
        SourceInitialized += (_, _) => ApplyClickThrough();
        MouseLeftButtonDown += OnDown;
        MouseMove += (_, _) =>
        {
            if (!_pressed)
            {
                return;
            }

            var (x, y) = Screens.Cursor();
            int dx = x - _down.X, dy = y - _down.Y;
            if (!IsDragging && (Math.Abs(dx) >= DragThreshold || Math.Abs(dy) >= DragThreshold))
            {
                IsDragging = true;
                DragStarted?.Invoke();
            }

            if (IsDragging)
            {
                Dragged?.Invoke(dx, dy);
            }
        };
        MouseLeftButtonUp += (_, _) => EndPress();
        LostMouseCapture += (_, _) => EndPress();
    }

    public BitmapSource? Image => _image.Source as BitmapSource;

    /// <summary>Идёт перетаскивание снимка.</summary>
    public bool IsDragging { get; private set; }

    public event Action? DragStarted;

    /// <summary>Смещение указателя от нажатия (физические пиксели).</summary>
    public event Action<int, int>? Dragged;

    public event Action? DragEnded;

    public event Action? DoubleClicked;

    /// <summary>Щелчки проходят к программе под снимком (окно прозрачно для мыши).</summary>
    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (_clickThrough != value)
            {
                _clickThrough = value;
                ApplyClickThrough();
            }
        }
    }

    private void ApplyClickThrough()
    {
        if (Hwnd == 0)
        {
            return;
        }

        long ex = Screens.ExStyle(Hwnd);
        Screens.SetExStyle(Hwnd, _clickThrough ? ex | Screens.WsExTransparent : ex & ~Screens.WsExTransparent);
    }

    public void SetImage(BitmapSource? image) => _image.Source = image;

    /// <summary>Оформление и вписывание изображения (Keep и Fit — целиком, Stretch — растянуто).</summary>
    public void SetStyle(SnapshotStyle s, SnapshotAspect aspect)
    {
        var border = SnapshotStyle.ParseColor(s.BorderColor, Colors.LightGray);
        _ring.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(s.BorderOpacity, 0, 1) * 255), border.R, border.G, border.B));
        _ring.BorderThickness = new Thickness(s.BorderWidth);
        _ring.CornerRadius = new CornerRadius(s.Radius);
        _ring.Visibility = s.BorderWidth > 0 && s.BorderOpacity > 0 ? Visibility.Visible : Visibility.Collapsed;
        _clip.CornerRadius = new CornerRadius(s.Radius);
        _clip.Background = new SolidColorBrush(SnapshotStyle.ParseColor(s.Background, Color.FromRgb(0x11, 0x15, 0x13)));
        _image.Stretch = aspect == SnapshotAspect.Stretch ? Stretch.Fill : Stretch.Uniform;
        _radius = s.Radius;
        UpdateClip();
    }

    private double _radius;

    private void UpdateClip()
    {
        double w = _root.Width, h = _root.Height;
        _clip.Clip = double.IsNaN(w) || double.IsNaN(h) ? null : new RectangleGeometry(new Rect(0, 0, w, h), _radius, _radius);
    }

    /// <summary>Разместить карточку (физические пиксели) на мониторе масштаба <paramref name="scale"/>.</summary>
    public void Place(PixelRect card, double scale, double opacity, bool topmost)
    {
        double w = card.Width / scale, h = card.Height / scale;
        if (Math.Abs(_root.Width - w) > 0.001 || Math.Abs(_root.Height - h) > 0.001 || double.IsNaN(_root.Width))
        {
            _root.Width = w;
            _root.Height = h;
            UpdateClip();
        }

        Width = w;
        Height = h;
        Opacity = opacity;
        PhysicalPlacement.Place(this, card, topmost);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            EndPress();
            DoubleClicked?.Invoke();
            e.Handled = true;
            return;
        }

        _down = Screens.Cursor();
        _pressed = CaptureMouse();
        e.Handled = true;
    }

    private void EndPress()
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (IsDragging)
        {
            IsDragging = false;
            DragEnded?.Invoke();
        }
    }

    /// <summary>Освободить изображение (снимок удалён) — без ссылок на буфер.</summary>
    public void ReleaseImage() => _image.Source = null;
}
