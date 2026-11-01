using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Presentation;

public enum LensStatus { Closed, Live, SourceUnavailable, Failed }

/// <summary>
/// Живая линза: окно-хозяин (Win32, layered, click-through, no-activate, topmost, со скруглённой областью) с дочерним элементом
/// Magnifier, который система заполняет живым изображением центральной части выбранной области с увеличением
/// <see cref="LensSettings.Zoom"/>. Обновление — таймер ~60 Гц (<c>MagSetWindowSource</c> + перерисовка), только пока линза открыта.
/// Окна линзы и рамки (и окно рисования, если оно есть) исключены из источника (<c>MagSetWindowFilterList</c>) — нет
/// рекурсивного изображения. Если источник недоступен (блокировка, защищённый рабочий стол, ошибка API), элемент скрывается и
/// показывается надпись: устаревший кадр не выдаётся за живой. Клик по линзе проходит к окну под ней (линза только показывает);
/// совпадение клика с увеличенным элементом не обещается, преобразования и инжекции ввода нет. Без uiAccess и повышения прав.
/// </summary>
public sealed class LiveLens : IDisposable
{
    private const string HostClass = "Backpack.LensHost";
    private const uint WsPopup = 0x80000000, WsChild = 0x40000000, WsVisible = 0x10000000, WsClipChildren = 0x02000000;
    private static bool s_classRegistered;
    private static int s_initialized;

    private readonly LensSettings _settings;
    private readonly PixelRect _target;
    private readonly double _scale;
    private readonly Func<IEnumerable<nint>> _exclude;
    private readonly DispatcherTimer _refresh;
    private LensFrameWindow? _frame;
    private nint _host;
    private nint _magnifier;
    private bool _magInit;
    private bool _disposed;

    public LiveLens(LensSettings settings, PixelRect target, double scale, Func<IEnumerable<nint>> exclude)
    {
        _settings = settings;
        _target = target;
        _scale = scale;
        _exclude = exclude;
        _refresh = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _refresh.Tick += (_, _) => Refresh();
    }

    public LensStatus Status { get; private set; } = LensStatus.Closed;

    public PixelRect Target => _target;

    /// <summary>Источник (физические пиксели): центр выбранной области, уменьшенный в <see cref="LensSettings.Zoom"/> раз.</summary>
    public PixelRect Source => SourceFor(_target, _settings.Zoom);

    public nint HostHandle => _host;

    public nint MagnifierHandle => _magnifier;

    public nint FrameHandle => _frame?.Hwnd ?? 0;

    public long Frames { get; private set; }

    public long SourceFailures { get; private set; }

    /// <summary>Число открытых экземпляров MagInitialize в процессе (проверка освобождения).</summary>
    public static int Initialized => Volatile.Read(ref s_initialized);

    public static PixelRect SourceFor(PixelRect target, double zoom)
    {
        int w = Math.Max(1, (int)Math.Round(target.Width / zoom));
        int h = Math.Max(1, (int)Math.Round(target.Height / zoom));
        return new PixelRect(target.Left + ((target.Width - w) / 2), target.Top + ((target.Height - h) / 2), w, h);
    }

    public unsafe bool Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        Interlocked.Increment(ref s_initialized);
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
                    Background = MagnifierApi.GetStockObject(4), // BLACK_BRUSH: под скрытым элементом — чёрный фон, не мусор
                    ClassName = (nint)name,
                };
                s_classRegistered = MagnifierApi.RegisterClassExW(wc) != 0 || System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == 1410; // CLASS_ALREADY_EXISTS
            }
        }

        uint ex = (uint)(Screens.WsExLayered | Screens.WsExTransparent | Screens.WsExTopmost | Screens.WsExToolWindow | Screens.WsExNoActivate);
        _host = MagnifierApi.CreateWindowExW(ex, HostClass, "Backpack lens", WsPopup | WsClipChildren, _target.Left, _target.Top, _target.Width, _target.Height, 0, 0, instance, 0);
        if (_host == 0)
        {
            Status = LensStatus.Failed;
            Close();
            return false;
        }

        MagnifierApi.SetLayeredWindowAttributes(_host, 0, 255, 0x2 /* LWA_ALPHA */);
        int r = (int)Math.Round(_settings.Radius * _scale * 2);
        MagnifierApi.SetWindowRgn(_host, MagnifierApi.CreateRoundRectRgn(0, 0, _target.Width + 1, _target.Height + 1, r, r), true);
        _magnifier = MagnifierApi.CreateWindowExW(0, MagnifierApi.WindowClass, "Backpack magnifier", WsChild | WsVisible, 0, 0, _target.Width, _target.Height, _host, 0, instance, 0);
        if (_magnifier == 0)
        {
            Status = LensStatus.Failed;
            Close();
            return false;
        }

        float z = (float)_settings.Zoom;
        float* m = stackalloc float[9] { z, 0, 0, 0, z, 0, 0, 0, 1 };
        MagnifierApi.MagSetWindowTransform(_magnifier, m);

        _frame = new LensFrameWindow(_settings, _scale);
        _frame.Show();
        if (_frame.Hwnd != 0)
        {
            var margin = _frame.MarginPixels;
            Screens.Place(_frame.Hwnd, new PixelRect(_target.Left - margin, _target.Top - margin, _target.Width + (2 * margin), _target.Height + (2 * margin)));
        }

        UpdateExclusions();
        Screens.Place(_host, _target);
        if (_frame.Hwnd != 0)
        {
            var margin = _frame.MarginPixels;
            Screens.Place(_frame.Hwnd, new PixelRect(_target.Left - margin, _target.Top - margin, _target.Width + (2 * margin), _target.Height + (2 * margin)));
        }

        Refresh();
        _refresh.Start();
        return Status == LensStatus.Live;
    }

    /// <summary>Повторить список исключений (например, появилось окно рисования).</summary>
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

        var src = Source;
        bool ok = MagnifierApi.MagSetWindowSource(_magnifier, new Screens.R { Left = src.Left, Top = src.Top, Right = src.Right, Bottom = src.Bottom });
        if (ok)
        {
            if (Status != LensStatus.Live)
            {
                MagnifierApi.ShowWindow(_magnifier, 5 /* SW_SHOW */);
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
                // Не показывать последний кадр как живой.
                MagnifierApi.ShowWindow(_magnifier, 0 /* SW_HIDE */);
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
        if (_magInit)
        {
            _magInit = false;
            MagnifierApi.MagUninitialize();
            Interlocked.Decrement(ref s_initialized);
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
    }
}

/// <summary>Рамка и тень линзы: прозрачное окно click-through поверх хозяина, середина пустая (видна линза).</summary>
public sealed class LensFrameWindow : NoActivateWindow
{
    private const double ShadowMargin = 18;
    private readonly TextBlock _unavailable;
    private readonly double _scale;

    public LensFrameWindow(LensSettings s, double scale)
    {
        _scale = scale;
        Title = "Backpack — lens frame";
        Left = -32000;
        Top = -32000;
        SizeToContent = SizeToContent.Manual;
        var grid = new Grid();
        var frame = new Border
        {
            Margin = new Thickness(ShadowMargin),
            CornerRadius = new CornerRadius(s.Radius),
            BorderThickness = new Thickness(s.BorderWidth),
            BorderBrush = s.BorderColor.ToFrozenBrush(),
        };
        if (s.Shadow)
        {
            frame.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = 0.45, Color = Colors.Black };
        }

        _unavailable = new TextBlock
        {
            Text = Loc.T("Lens.Unavailable"),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x20, 0x20, 0x24)),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            FontSize = 14,
        };
        grid.Children.Add(frame);
        grid.Children.Add(_unavailable);
        Content = grid;
    }

    /// <summary>Поле под тень вокруг линзы, физические пиксели.</summary>
    public int MarginPixels => (int)Math.Round(ShadowMargin * _scale);

    public bool UnavailableShown => _unavailable.Visibility == Visibility.Visible;

    public void SetUnavailable(bool on) => _unavailable.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
}
