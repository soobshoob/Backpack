using System.Windows;
using System.Windows.Interop;
using Backpack.Core.Input;
using Backpack.Core.Model;
using Backpack.Core.Privacy;
using Backpack.Visual.Icons;
using Backpack.Visual.Localization;
using Backpack.Win32;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using System.Windows.Controls;
using System.Windows.Media;
using Backpack.Visual.Windows;
using VisualKeySide = Backpack.Visual.Rendering.KeySide;

namespace Backpack.App;

/// <summary>
/// Оверлей на одном мониторе: прозрачная полоса во всю ширину его рабочей области, no-activate, click-through
/// (layered + WS_EX_TRANSPARENT), без панели задач и Alt+Tab. Кейкапы — <see cref="OverlayPresenter"/>: элементы не
/// пересоздаются при каждом изменении модели. Фильтр «все / только сочетания» и показ мыши в ряду — <see cref="RowFilter"/>,
/// только на уровне представления. Окон может быть несколько (по одному на выбранный монитор): все читают одну модель
/// отображения, источник ввода один. Широковещательные уведомления (сессия, питание, дисплей) принимает только первое окно.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const int WmWtsSessionChange = 0x02B1;
    private const int WmPowerBroadcast = 0x0218;
    private const int WmDisplayChange = 0x007E;
    private const int WmSettingChange = 0x001A;
    private const int SpiSetWorkArea = 0x002F;

    private readonly LabController _controller;
    private readonly RowFilter _filter = new();
    private DemoPlayer? _demo;
    private bool _sessionRegistered;
    private readonly bool _systemNotifications;
    private readonly Border _lock;
    private readonly TextBlock _lockText = new() { Foreground = Brushes.White, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private LabSettings _settings = LabSettings.Default;
    private Rect _band;

    /// <param name="monitor">Монитор окна (null — основной на момент создания).</param>
    /// <param name="systemNotifications">Принимать уведомления сессии, питания и дисплея (только одно окно).</param>
    public OverlayWindow(LabController controller, MonitorDesc? monitor = null, bool systemNotifications = true)
    {
        InitializeComponent();
        _controller = controller;
        _systemNotifications = systemNotifications;
        Monitor = monitor ?? DisplayMonitors.Enumerate().FirstOrDefault(m => m.IsPrimary) ?? FallbackMonitor();
        Presenter = new OverlayPresenter(new StopwatchClock());
        Host.Children.Add(Presenter.Root);

        // Ненавязчивый статус приватности: замок и причина, без количества скрытых нажатий и без текста поля.
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Lucide.Icon("lock", 16, Brushes.White));
        row.Children.Add(_lockText);
        _lock = new Border
        {
            Child = row,
            Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x11, 0x15, 0x13)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x31, 0xC9, 0x9A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 6, 14, 6),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        Host.Children.Add(_lock);

        controller.DisplayChanged += Refresh;
        controller.PrivacyCleared += OnPrivacyCleared;
        controller.PrivacyChanged += UpdateLock;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        Reposition();
    }

    /// <summary>Монитор, на котором показано это окно.</summary>
    public MonitorDesc Monitor { get; private set; }

    /// <summary>Показывать значок замка, когда приватность скрыла ввод.</summary>
    public bool ShowPrivacyBadge { get; set; } = true;

    /// <summary>Значок замка сейчас виден (для проверок).</summary>
    public bool PrivacyBadgeVisible => _lock.Visibility == Visibility.Visible;

    public string PrivacyBadgeText => _lockText.Text;

    private static MonitorDesc FallbackMonitor()
    {
        var wa = SystemParameters.WorkArea;
        var r = new Backpack.Visual.Presentation.PixelRect(0, 0, (int)wa.Width, (int)wa.Height);
        return new MonitorDesc("fallback", "fallback", r, r, 1.0, true);
    }

    /// <summary>
    /// Перенести окно на другой монитор. Кейкапы, удержания и идущие анимации не трогаются: меняется только положение окна,
    /// поэтому при следовании за курсором нет дублей, старой копии и повторного появления.
    /// </summary>
    public void PlaceOn(MonitorDesc monitor)
    {
        Monitor = monitor;
        Reposition();
    }

    /// <summary>Причина закрытия показа простыми словами (или null, если показ открыт).</summary>
    public static string? PrivacyStatusText(PrivacyGate g) => g.Open ? null : g.Reason switch
    {
        PrivacyReason.PasswordField => Loc.T("Privacy.Status.Password"),
        PrivacyReason.BlockedApp => Loc.T("Privacy.Status.BlockedApp"),
        PrivacyReason.Checking => Loc.T("Privacy.Status.Checking"),
        _ => Loc.T("Privacy.Status.NoData"),
    };

    private void OnPrivacyCleared()
    {
        _demo?.Dispose();
        _demo = null;
        Presenter.Clear(); // без анимации исчезновения: ни один кадр со скрываемым вводом
        UpdateLock();
    }

    private void UpdateLock()
    {
        var g = _controller.Privacy;
        bool show = ShowPrivacyBadge && !g.Open && _controller.HiddenInputSinceOpen && g.State != PrivacyState.Checking;
        _lockText.Text = PrivacyStatusText(g) ?? "";
        _lock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Presenter.Root.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    public OverlayPresenter Presenter { get; }

    public RowFilter Filter => _filter;

    public nint Hwnd { get; private set; }

    public bool StylesApplied { get; private set; }

    /// <summary>
    /// Прямоугольник группы кейкапов на экране (DIP) — для индикатора «над кейкапами». Пока группа не видна —
    /// расчётное место для группы шириной в три клавиши.
    /// </summary>
    public Rect GroupLocalRect
    {
        get
        {
            var root = Presenter.Root;
            if (root.Visibility == Visibility.Visible && root.ActualWidth > 0)
            {
                var p = root.TranslatePoint(new Point(0, 0), Host);
                return new Rect(_band.Left + p.X, _band.Top + p.Y, root.ActualWidth, root.ActualHeight);
            }

            var m = KeycapMetrics.From(_settings.Appearance);
            double width = (3 * m.ModifierMinWidth) + (2 * m.KeyGap) + (_settings.Appearance.BackgroundEnabled ? 2 * m.GroupPadding : 0);
            return OverlayPlacement.GroupRect(Monitor.LocalWorkDip, _settings.Placement, _settings.Appearance, width);
        }
    }

    public void ApplySettings(LabSettings s)
    {
        _settings = s;
        var align = OverlayPlacement.Horizontal(s.Placement.Anchor);
        Presenter.Apply(s.Appearance, new RowOptions(s.Keys.Trail, s.Keys.MouseLabels, align));
        Presenter.Root.HorizontalAlignment = HorizontalAlignment.Left;
        Presenter.Root.Margin = new Thickness(KeycapMetrics.From(s.Appearance).OuterMargin);
        Host.Placement = s.Placement;
        _filter.Configure(s.Keys.Filter, s.Keys.MouseInRow, ModelItems(), s.Keys.Chords);
        _controller.Display.Rule = s.Keys.Chords == ChordMode.AnyHeld ? ChordRule.AnyHeld : ChordRule.Modifiers;
        _controller.Display.AnyHeldThreshold = TimeSpan.FromMilliseconds(s.Keys.AnyHeldMs);
        Reposition();
        Refresh();
    }

    public void ShowOverlay()
    {
        Show();
        if (Hwnd != 0)
        {
            OverlayWindowTools.ShowNoActivate(Hwnd);
        }
    }

    /// <summary>Положение относительно рабочей области своего монитора (DIP этого монитора → физические пиксели).</summary>
    public void Reposition()
    {
        _band = OverlayPlacement.Band(Monitor.LocalWorkDip, _settings.Placement, _settings.Appearance);
        Width = _band.Width;
        Height = _band.Height;
        PhysicalPlacement.Place(this, Monitor.ToPixels(_band));
    }

    /// <summary>Пример на оверлее: демонстрационный сценарий вместо реального ввода на <paramref name="duration"/>.</summary>
    public void ShowExample(TimeSpan duration, DemoScenario? scenario = null)
    {
        _demo?.Dispose();
        _demo = new DemoPlayer(Presenter, new StopwatchClock(), duration, scenario,
            items => RowFilter.Visible(items, _settings.Keys.Filter, _settings.Keys.MouseInRow, _settings.Keys.Chords));
        _demo.Finished += () =>
        {
            _demo?.Dispose();
            _demo = null;
            Refresh();
        };
    }

    public bool ExampleRunning => _demo is not null;

    /// <summary>Записи модели отображения в виде элементов визуального слоя (до фильтра).</summary>
    public List<KeycapItem> ModelItems()
    {
        var items = new List<KeycapItem>();
        foreach (var e in _controller.Display.Entries)
        {
            var sem = KeyInfo.Semantic(e.KeySlot);
            items.Add(new KeycapItem(
                e.Id,
                e.KeySlot,
                sem.Key,
                e.Modifier != ModifierKind.None,
                IsPressed: e.State == KeyState.Down,
                Side: sem.Side switch { 'L' => VisualKeySide.Left, 'R' => VisualKeySide.Right, _ => VisualKeySide.None },
                Numpad: sem.Numpad,
                IsUnknown: e.State == KeyState.Unknown,
                PressCount: e.PressCount));
        }

        return items;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        Hwnd = new WindowInteropHelper(this).Handle;
        OverlayWindowTools.ApplyNoActivateClickThrough(Hwnd);
        StylesApplied = OverlayWindowTools.HasClickThroughStyles(Hwnd);
        _sessionRegistered = _systemNotifications && SystemNotifications.RegisterSession(Hwnd);
        HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _controller.DisplayChanged -= Refresh;
        _controller.PrivacyCleared -= OnPrivacyCleared;
        _controller.PrivacyChanged -= UpdateLock;
        _demo?.Dispose();
        _demo = null;
        Presenter.Dispose();
        if (_sessionRegistered)
        {
            SystemNotifications.UnregisterSession(Hwnd);
        }

        Hwnd = 0; // окно уничтожено — дальнейшие проверки состояния не выполняются
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            handled = true;
            return MaNoActivate; // вторая линия защиты от активации
        }

        if (!_systemNotifications)
        {
            return 0;
        }

        switch (msg)
        {
            case WmSettingChange when (int)wParam == SpiSetWorkArea:
                _controller.OnDisplayChange(); // изменилась рабочая область (панель задач)
                break;
            case WmWtsSessionChange:
                _controller.OnSessionChange((int)wParam);
                break;
            case WmPowerBroadcast:
                _controller.OnPower((int)wParam);
                break;
            case WmDisplayChange:
                _controller.OnDisplayChange();
                break;
        }

        return 0;
    }

    private void Refresh()
    {
        var items = _filter.Apply(ModelItems());
        if (_demo is not null)
        {
            return; // пока идёт пример, реальная модель не показывается (и не теряется: следующий Refresh её покажет)
        }

        if (!_controller.Privacy.Open)
        {
            items = items.Where(i => Slots.IsMouse(i.MatchKey)).ToList(); // вторая линия: клавиш в модели уже нет
        }

        Presenter.Update(items);
        _controller.NoteRefresh(Presenter.Visuals.Count);
    }

    /// <summary>Видимая группа кейкапов в физических пикселях экрана (null — группа не видна).</summary>
    public (int Left, int Top, int Right, int Bottom)? GroupScreenPixels()
    {
        if (Presenter.Root.Visibility != Visibility.Visible || Presenter.Root.ActualWidth <= 0)
        {
            return null;
        }

        var r = Monitor.ToPixels(GroupLocalRect);
        return (r.Left, r.Top, r.Left + r.Width, r.Top + r.Height);
    }
}
