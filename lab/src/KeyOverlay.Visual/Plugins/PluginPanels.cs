using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Backpack.Sdk;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Plugins;

/// <summary>
/// Панель плагина (рисует Backpack): заголовок, крупный текст (масштабируется вместе с панелью, пропорции сохраняются),
/// полоса прогресса, состояние (warning — тёплый акцент, finished — ненавязчивая рамка акцента). Прозрачна для мыши, без активации:
/// щелчки проходят к приложению под ней. Управление — отдельные окна <see cref="PluginPanelHost"/>.
/// </summary>
public sealed class PluginPanelWindow : NoActivateWindow
{
    private readonly TextBlock _title = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _text = new() { FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Viewbox _textBox;
    private readonly Border _bar = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Border _track = new() { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 8, 0, 0) };
    private readonly Border _card;

    public PluginPanelWindow()
    {
        Title = "Backpack — plugin panel";
        _textBox = new Viewbox { Child = _text, StretchDirection = StretchDirection.Both, Stretch = Stretch.Uniform };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(_title);
        Grid.SetRow(_textBox, 1);
        grid.Children.Add(_textBox);
        _track.Child = _bar;
        Grid.SetRow(_track, 2);
        grid.Children.Add(_track);
        _card = new Border
        {
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16, 10, 16, 12),
            Child = grid,
        };
        Content = _card;
        SizeToContent = SizeToContent.Manual;
    }

    public string Text => _text.Text;

    public string State { get; private set; } = "normal";

    /// <summary>Естественный размер для текста размера <paramref name="fontSize"/> (DIP).</summary>
    public Size NaturalSize { get; private set; }

    public void Set(PluginPanelSpec p)
    {
        State = p.State;
        var accent = p.Accent is { } a && ThemeColor.TryParse(a, out var c) ? c.ToWpf() : Color.FromRgb(0x31, 0xC9, 0x9A);
        var warm = Color.FromRgb(0xF5, 0xB4, 0x3C);
        var stateColor = p.State switch { "warning" => warm, "finished" => accent, _ => accent };
        _title.Text = p.Title;
        _title.Foreground = new SolidColorBrush(Color.FromRgb(0xC7, 0xCF, 0xCB));
        _title.Visibility = string.IsNullOrEmpty(p.Title) ? Visibility.Collapsed : Visibility.Visible;
        _text.Text = p.Text;
        _text.FontSize = p.FontSize;
        _text.Foreground = new SolidColorBrush(p.State == "normal" ? Color.FromRgb(0xF4, 0xF6, 0xF5) : stateColor);
        _card.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(p.Opacity * 255), 0x11, 0x15, 0x13));
        _card.BorderBrush = new SolidColorBrush(p.State == "finished" ? stateColor : Color.FromArgb(0x55, accent.R, accent.G, accent.B));
        _card.Effect = p.State == "finished" ? new DropShadowEffect { Color = stateColor, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.55 } : null;
        _bar.Background = new SolidColorBrush(stateColor);
        _track.Visibility = p.Progress is null ? Visibility.Collapsed : Visibility.Visible;
        _bar.Tag = p.Progress ?? 0;
        var probe = new TextBlock { Text = p.Text, FontSize = p.FontSize, FontWeight = FontWeights.SemiBold };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        NaturalSize = new Size(Math.Max(200, probe.DesiredSize.Width + 40), probe.DesiredSize.Height + (p.Title.Length > 0 ? 22 : 0) + (p.Progress is null ? 0 : 12) + 26);
        LayoutBar();
    }

    /// <summary>Размер панели (DIP): текст вписывается целиком, без искажения пропорций.</summary>
    public void SetSize(double width, double height)
    {
        Width = width;
        Height = height;
        LayoutBar();
    }

    private void LayoutBar() => _bar.Width = Math.Max(0, ((double.IsNaN(Width) ? NaturalSize.Width : Width) - 34) * (_bar.Tag is double d ? d : 0));
}

/// <summary>Окно кнопок панели: иконки Lucide из набора хоста, подсказки и имена для экранного диктора. Без активации.</summary>
public sealed class PanelButtonsWindow : NoActivateWindow
{
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };

    public PanelButtonsWindow()
    {
        Title = "Backpack — panel controls";
        IsHitTestVisible = true;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x17, 0x1B, 0x1A)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(4),
            Margin = new Thickness(4),
            Child = _row,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.6 },
        };
        SourceInitialized += (_, _) => Screens.SetExStyle(Hwnd, Screens.ExStyle(Hwnd) & ~Screens.WsExTransparent);
    }

    public event Action<string>? Pressed;

    public IReadOnlyList<Button> Buttons => _row.Children.OfType<Button>().ToList();

    public void Set(IReadOnlyList<PanelButton> buttons)
    {
        if (buttons.Select(b => b.Id + b.Icon + b.Title).SequenceEqual(_row.Children.OfType<Button>().Select(b => (string)b.Tag)))
        {
            return;
        }

        _row.Children.Clear();
        foreach (var b in buttons)
        {
            var id = b.Id;
            var btn = new Button
            {
                Content = Icons.Lucide.Icon(b.Icon, 18, Brushes.White),
                Width = 34,
                Height = 34,
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = b.Title,
                Focusable = false,
                Tag = b.Id + b.Icon + b.Title,
            };
            System.Windows.Automation.AutomationProperties.SetName(btn, b.Title);
            btn.Click += (_, _) => Pressed?.Invoke(id);
            _row.Children.Add(btn);
        }
    }

    public Size Natural()
    {
        ((FrameworkElement)Content).Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return ((FrameworkElement)Content).DesiredSize;
    }
}

/// <summary>
/// Панель плагина с управлением по наведению (единый механизм <see cref="HoverReveal"/>): кнопки плагина над панелью, ручка
/// перемещения и ручка размера — только при наведении на панель или управление, во время перетаскивания и при клавиатурном
/// вызове (<see cref="Reveal"/>). Скрытое управление не перехватывает щелчки; появление не сдвигает панель. Положение и размер
/// (физические пиксели) сохраняются через <see cref="BoundsChanged"/> и восстанавливаются на мониторе, где помещаются.
/// </summary>
public sealed class PluginPanelHost : IDisposable
{
    private readonly PluginPanelWindow _window = new();
    private readonly PanelButtonsWindow _buttons = new();
    private readonly Func<IReadOnlyList<MonitorDesc>> _monitors;
    private FocusGrip? _move;
    private FocusGrip? _resize;
    private HoverReveal? _reveal;
    private PixelRect _bounds;
    private PixelRect _dragStart;
    private bool _userSized;
    private bool _disposed;

    public PluginPanelHost(Func<IReadOnlyList<MonitorDesc>> monitors, PixelRect? saved, Func<bool>? animate = null)
    {
        _monitors = monitors;
        _userSized = saved is not null;
        _bounds = saved ?? default;
        _buttons.Pressed += id => ButtonPressed?.Invoke(id);
        _reveal = new HoverReveal(() => _window.IsVisible ? _bounds : null, Controls, animate);
    }

    public PluginPanelWindow Window => _window;

    public PanelButtonsWindow ButtonsWindow => _buttons;

    public PixelRect Bounds => _bounds;

    public bool ControlsVisible => _reveal?.Visible == true;

    public HoverReveal? RevealState => _reveal;

    public event Action<string>? ButtonPressed;

    /// <summary>Пользователь переместил или изменил размер панели (физические пиксели).</summary>
    public event Action<PixelRect>? BoundsChanged;

    /// <summary>Окна управления панели (кнопки, ручки) — для проверок наведения и пропуска щелчков.</summary>
    public IReadOnlyList<NoActivateWindow> ControlWindows => Controls().ToList();

    private IEnumerable<NoActivateWindow> Controls()
    {
        if (_buttons.IsVisible)
        {
            yield return _buttons;
        }

        if (_move is not null)
        {
            yield return _move;
        }

        if (_resize is not null)
        {
            yield return _resize;
        }
    }

    public void Set(PluginPanelSpec spec, bool movable)
    {
        if (_disposed)
        {
            return;
        }

        _window.Set(spec);
        var m = MonitorOf(_bounds) ?? _monitors().FirstOrDefault(x => x.IsPrimary) ?? _monitors().FirstOrDefault();
        if (m is null)
        {
            return;
        }

        if (_bounds.IsEmpty || !_userSized)
        {
            var size = _window.NaturalSize;
            int w = (int)Math.Round(size.Width * m.Scale), h = (int)Math.Round(size.Height * m.Scale);
            _bounds = _bounds.IsEmpty
                ? new PixelRect(m.Work.Right - w - (int)(24 * m.Scale), m.Work.Top + (int)(24 * m.Scale), w, h)
                : _bounds with { Width = Math.Max(_bounds.Width, w), Height = h };
        }

        _bounds = Fit(_bounds);
        Place();
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _buttons.Set(spec.Buttons);
        if (spec.Buttons.Count > 0 && !_buttons.IsVisible)
        {
            _buttons.Show();
            HoverReveal.SetVisible(_buttons, false, animate: false);
        }

        if (movable && _move is null)
        {
            _move = Grip(FocusGripKind.Move, spec.Title.Length > 0 ? spec.Title : Localization.Loc.T("Focus.Grip.Area"));
            _resize = Grip(FocusGripKind.Resize, Localization.Loc.T("Focus.Grip.Resize"));
        }

        PlaceControls();
        _reveal?.Tick();
    }

    /// <summary>Клавиатурный вызов редактирования: управление видно без наведения.</summary>
    public void Reveal() => _reveal?.Reveal();

    private FocusGrip Grip(FocusGripKind kind, string label)
    {
        var g = new FocusGrip(kind, label);
        g.DragStarted += _ =>
        {
            _dragStart = _bounds;
            if (_reveal is not null)
            {
                _reveal.Hold = true;
            }
        };
        g.Dragged += (grip, dx, dy) =>
        {
            var m = MonitorOf(_dragStart);
            int min = (int)Math.Round(120 * (m?.Scale ?? 1));
            _bounds = grip.Kind == FocusGripKind.Resize
                ? _dragStart with { Width = Math.Max(min, _dragStart.Width + dx), Height = Math.Max(min / 2, _dragStart.Height + dy) }
                : _dragStart with { Left = _dragStart.Left + dx, Top = _dragStart.Top + dy };
            if (grip.Kind == FocusGripKind.Resize)
            {
                _userSized = true;
            }

            Place();
            PlaceControls();
        };
        g.DragEnded += _ =>
        {
            if (_reveal is not null)
            {
                _reveal.Hold = false;
            }

            _bounds = Fit(_bounds);
            _userSized = true;
            Place();
            PlaceControls();
            BoundsChanged?.Invoke(_bounds);
        };
        return g;
    }

    private MonitorDesc? MonitorOf(PixelRect r) => r.IsEmpty ? null : MonitorResolver.UnderCursor(_monitors(), (r.Left + (r.Width / 2), r.Top + (r.Height / 2)));

    /// <summary>Панель целиком на мониторе своего центра (после переноса, смены мониторов, восстановления).</summary>
    private PixelRect Fit(PixelRect r) => MonitorOf(r) is { } m ? r.ClampInto(m.Work.IsEmpty ? m.Bounds : m.Work) : r;

    private void Place()
    {
        var m = MonitorOf(_bounds);
        double scale = m?.Scale ?? 1;
        PhysicalPlacement.Place(_window, _bounds);
        _window.SetSize(_bounds.Width / scale, _bounds.Height / scale);
    }

    /// <summary>Кнопки — над панелью справа (под ней, если сверху нет места), ручки — как у линзы. Всё на мониторе панели.</summary>
    private void PlaceControls()
    {
        var m = MonitorOf(_bounds);
        if (m is null)
        {
            return;
        }

        if (_buttons.IsVisible)
        {
            var size = _buttons.Natural();
            int w = (int)Math.Ceiling(size.Width * m.Scale), h = (int)Math.Ceiling(size.Height * m.Scale);
            int y = _bounds.Top - h - (int)(2 * m.Scale);
            if (y < m.Bounds.Top)
            {
                y = _bounds.Bottom + (int)(2 * m.Scale);
            }

            var r = new PixelRect(_bounds.Right - w, y, w, h).ClampInto(m.Bounds);
            PhysicalPlacement.Place(_buttons, r);
            _buttons.Width = r.Width / m.Scale;
            _buttons.Height = r.Height / m.Scale;
        }

        _move?.Place(_bounds, m);
        _resize?.Place(_bounds, m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reveal?.Dispose();
        _reveal = null;
        _window.Close();
        _buttons.Close();
        _move?.Close();
        _resize?.Close();
        ButtonPressed = null;
        BoundsChanged = null;
    }
}
