using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Layout;
using Backpack.Visual.Localization;
using Backpack.Visual.Mouse;
using MouseButtonState = Backpack.Visual.Mouse.MouseButtonState;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Help;

/// <summary>Увеличенный просмотр изображения справки: «по размеру окна» или 1:1 с прокруткой, масштаб колесом с Ctrl. Одно окно.</summary>
public sealed class HelpImageWindow : Window
{
    [ThreadStatic] private static HelpImageWindow? t_window;

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Button _fit = new() { Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button _actual = new() { Padding = new Thickness(10, 3, 10, 3) };
    private readonly TextBlock _caption = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4, 12, 10) };
    private bool _fitMode = true;

    private HelpImageWindow()
    {
        UiTheme.Attach(this);
        Width = 1100;
        Height = 800;
        MinWidth = 400;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _image.LayoutTransform = _scale;
        _scroll.Content = _image;
        _caption.Style = UiTheme.Resources.StyleOf("Ko.Hint");
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 10, 12, 6) };
        bar.Children.Add(_fit);
        bar.Children.Add(_actual);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_caption, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(_caption);
        root.Children.Add(_scroll);
        Content = root;
        _fit.Click += (_, _) => SetFit(true);
        _actual.Click += (_, _) => SetFit(false);
        PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                SetFit(false);
                double f = Math.Clamp(_scale.ScaleX * (e.Delta > 0 ? 1.15 : 1 / 1.15), 0.25, 4);
                _scale.ScaleX = _scale.ScaleY = f;
                e.Handled = true;
            }
        };
        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.Escape, ModifierKeys.None));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => Close()));
        Loc.Changed += ApplyTexts;
        Closed += (_, _) =>
        {
            Loc.Changed -= ApplyTexts;
            t_window = null;
        };
        ApplyTexts();
    }

    public static HelpImageWindow? Current => t_window;

    public static HelpImageWindow? Show(Window? owner, string target, string caption)
    {
        var source = HelpLibrary.LoadImage(target);
        if (source is null)
        {
            return null;
        }

        var w = t_window ??= new HelpImageWindow();
        if (owner is not null && owner != w && w.Owner is null && owner.IsLoaded)
        {
            w.Owner = owner;
        }

        w._image.Source = source;
        w._caption.Text = caption;
        w._caption.Visibility = caption.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        w.Title = caption.Length > 0 ? caption : Loc.T("Help.ImageTitle");
        w.SetFit(true);
        if (HelpService.ShowOnScreen)
        {
            w.Show();
            w.Activate();
        }

        return w;
    }

    private void SetFit(bool fit)
    {
        _fitMode = fit;
        _scale.ScaleX = _scale.ScaleY = 1;
        if (fit)
        {
            _image.Stretch = Stretch.Uniform;
            _image.StretchDirection = StretchDirection.DownOnly;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else
        {
            _image.Stretch = Stretch.None;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        _fit.IsEnabled = !_fitMode;
    }

    private void ApplyTexts()
    {
        _fit.Content = Loc.T("Help.ImageFit");
        _actual.Content = Loc.T("Help.ImageActual");
    }
}

/// <summary>
/// Безопасные примеры из справки: показываются в собственном окне на искусственных данных, без реального ввода, без изменения
/// настроек и без оверлея поверх других окон. Одно окно; новый пример заменяет предыдущий; таймеры останавливаются при закрытии.
/// </summary>
public sealed class HelpDemoWindow : Window
{
    public static readonly string[] Demos = ["trail-none", "trail-fade", "combos", "labels", "mouse-cursor", "layout-indicator", "position"];

    [ThreadStatic] private static HelpDemoWindow? t_window;

    private readonly IAnimationClock _clock = new StopwatchClock();
    private readonly ContentControl _stage = new();
    private readonly TextBlock _heading = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _note = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly List<IDisposable> _running = [];
    private readonly List<DispatcherTimer> _timers = [];
    private OverlayPresenter? _presenter;

    private HelpDemoWindow()
    {
        UiTheme.Attach(this);
        Width = 760;
        Height = 480;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _heading.Style = UiTheme.Resources.StyleOf("Ko.H2");
        _text.Style = UiTheme.Resources.StyleOf("Ko.Hint");
        _note.Style = UiTheme.Resources.StyleOf("Ko.Hint");
        var top = new StackPanel { Margin = new Thickness(20, 16, 20, 10) };
        top.Children.Add(_heading);
        top.Children.Add(_text);
        var bottom = new StackPanel { Margin = new Thickness(20, 0, 20, 16) };
        bottom.Children.Add(_note);
        var stageFrame = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(20, 0, 20, 8), Child = _stage, ClipToBounds = true };
        stageFrame.Background = new LinearGradientBrush(Color.FromRgb(0x3A, 0x3F, 0x4A), Color.FromRgb(0xD9, 0xDC, 0xE2), 0);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(stageFrame);
        Content = root;
        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.Escape, ModifierKeys.None));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => Close()));
        Loc.Changed += Restart;
        Closed += (_, _) =>
        {
            Loc.Changed -= Restart;
            StopAll();
            t_window = null;
        };
    }

    public static HelpDemoWindow? Current => t_window;

    public string Demo { get; private set; } = "";

    public bool IsRunning => _timers.Any(t => t.IsEnabled) || _running.Count > 0;

    public static HelpDemoWindow? Show(Window? owner, string demo)
    {
        if (!Demos.Contains(demo))
        {
            return null;
        }

        var w = t_window ??= new HelpDemoWindow();
        if (owner is not null && owner != w && w.Owner is null && owner.IsLoaded)
        {
            w.Owner = owner;
        }

        w.Start(demo);
        if (HelpService.ShowOnScreen)
        {
            w.Show();
            w.Activate();
        }

        return w;
    }

    public static void CloseIfOpen() => t_window?.Close();

    private void Restart() => Start(Demo);

    private void Start(string demo)
    {
        StopAll();
        Demo = demo;
        string key = demo switch
        {
            "trail-none" => "TrailNone",
            "trail-fade" => "TrailFade",
            "combos" => "Combos",
            "labels" => "Labels",
            "mouse-cursor" => "MouseCursor",
            "layout-indicator" => "Layout",
            _ => "Position",
        };
        Title = Loc.T("Demo.WindowTitle");
        _heading.Text = Loc.T($"Demo.{key}.Title");
        _text.Text = Loc.T($"Demo.{key}.Text");
        _note.Text = Loc.T("Demo.Note");
        var s = LabSettings.Default;
        switch (demo)
        {
            case "trail-none":
                PlayRow(s, DemoScenario.Typing, TrailMode.None, KeyFilter.All);
                break;
            case "trail-fade":
                PlayRow(s, DemoScenario.Typing, TrailMode.FadeInPlace, KeyFilter.All);
                break;
            case "combos":
                PlayRow(s, DemoScenario.Chords, TrailMode.None, KeyFilter.CombosOnly);
                break;
            case "labels":
                PlayRow(s, DemoScenario.Chords, TrailMode.None, KeyFilter.All);
                break;
            case "mouse-cursor":
                PlayMouse(s);
                break;
            case "layout-indicator":
                PlayIndicator(s);
                break;
            default:
                PlayPosition();
                break;
        }
    }

    private void PlayRow(LabSettings s, DemoScenario scenario, TrailMode trail, KeyFilter filter)
    {
        _presenter = new OverlayPresenter(_clock);
        _presenter.Apply(s.Appearance, new RowOptions(trail, s.Keys.MouseLabels, HorizontalAlignment.Center));
        _stage.Content = new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(16), Child = _presenter.Root };
        _running.Add(new DemoPlayer(_presenter, _clock, scenario: scenario, filter: items => RowFilter.Visible(items, filter, true)));
    }

    private void PlayMouse(LabSettings s)
    {
        var visual = new MouseCursorVisual();
        var pointer = SettingsWindow.PreviewPointer();
        var canvas = new Canvas();
        canvas.Children.Add(visual);
        canvas.Children.Add(pointer);
        _stage.Content = canvas;
        MouseButtons[] steps = [MouseButtons.None, MouseButtons.Left, MouseButtons.None, MouseButtons.Right, MouseButtons.None, MouseButtons.Middle];
        int step = 0;
        var shown = s.MouseCursor with { Enabled = true };
        double angle = 0;
        void Place()
        {
            double cx = (canvas.ActualWidth / 2) + (Math.Cos(angle) * 90), cy = (canvas.ActualHeight / 2) + (Math.Sin(angle) * 40);
            Canvas.SetLeft(pointer, cx);
            Canvas.SetTop(pointer, cy);
            var o = visual.Origin;
            Canvas.SetLeft(visual, cx - o.X);
            Canvas.SetTop(visual, cy - o.Y);
        }

        visual.Apply(shown, s.Appearance.MouseLook, new MouseButtonState(MouseButtons.None, MouseButtons.None));
        canvas.SizeChanged += (_, _) => Place();
        AddTimer(700, () => visual.Apply(shown, s.Appearance.MouseLook, new MouseButtonState(steps[++step % steps.Length], MouseButtons.None)));
        AddTimer(40, () =>
        {
            angle += 0.03;
            Place();
        });
    }

    private void PlayIndicator(LabSettings s)
    {
        var visual = new LayoutIndicatorVisual { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var look = IndicatorLook.Resolve(s.Appearance, s.LayoutIndicator);
        string[] codes = ["EN", "UK", "RU"];
        int i = 0;
        visual.Set(new LayoutLabel(codes[0], null), look);
        _stage.Content = new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(16), Child = visual };
        AddTimer(1400, () =>
        {
            i++;
            visual.Visibility = i % 2 == 1 ? Visibility.Hidden : Visibility.Visible;
            visual.Set(new LayoutLabel(codes[(i / 2) % codes.Length], null), look);
        });
    }

    /// <summary>Схема экрана: прямоугольник ряда обходит якоря с отступами от краёв.</summary>
    private void PlayPosition()
    {
        var canvas = new Canvas { Width = 320, Height = 180 };
        var screen = new Rectangle { Width = 320, Height = 180, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0x10, 0x14)), Stroke = Brushes.White, StrokeThickness = 1.5 };
        var row = new Rectangle { Width = 70, Height = 18, RadiusX = 4, RadiusY = 4, Fill = Brushes.White };
        canvas.Children.Add(screen);
        canvas.Children.Add(row);
        _stage.Content = new Viewbox { Margin = new Thickness(16), Child = canvas };
        var anchors = Enum.GetValues<ScreenAnchor>();
        int i = Array.IndexOf(anchors, ScreenAnchor.BottomCenter) - 1;
        void Next()
        {
            i = (i + 1) % anchors.Length;
            var a = anchors[i];
            double margin = 14;
            double x = a.ToString().EndsWith("Left", StringComparison.Ordinal) ? margin : a.ToString().EndsWith("Right", StringComparison.Ordinal) ? 320 - 70 - margin : (320 - 70) / 2.0;
            double y = a.ToString().StartsWith("Top", StringComparison.Ordinal) ? margin : a.ToString().StartsWith("Bottom", StringComparison.Ordinal) ? 180 - 18 - margin : (180 - 18) / 2.0;
            Canvas.SetLeft(row, x);
            Canvas.SetTop(row, y);
            row.ToolTip = Loc.T("Anchor." + a);
        }

        Next();
        AddTimer(1100, Next);
    }

    private void AddTimer(int ms, Action tick)
    {
        var t = new DispatcherTimer(TimeSpan.FromMilliseconds(ms), DispatcherPriority.Normal, (_, _) => tick(), Dispatcher);
        _timers.Add(t);
        t.Start();
    }

    private void StopAll()
    {
        foreach (var t in _timers)
        {
            t.Stop();
        }

        _timers.Clear();
        foreach (var r in _running)
        {
            r.Dispose();
        }

        _running.Clear();
        if (_presenter is not null)
        {
            SettingsWindow.DetachFromParent(_presenter.Root);
            _presenter.Dispose();
            _presenter = null;
        }

        _stage.Content = null;
    }
}
