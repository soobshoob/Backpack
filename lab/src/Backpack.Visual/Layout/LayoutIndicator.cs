using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Layout;

/// <summary>Разрешённое оформление индикатора: собственное или унаследованное от кейкапов.</summary>
public sealed record IndicatorLook(ThemeColor Background, ThemeColor Foreground, ThemeColor Border, double BorderWidth, double Radius, double TextSize, AnimationKind Animation, int FadeMs)
{
    /// <summary>
    /// Наследование: фон — основной цвет кейкапа, текст — цвет текста, рамка — рамка кейкапа, скругление — скругление кейкапа,
    /// размер — 1.125·S (36 при S = 32). Анимация — своя или кейкапов.
    /// </summary>
    public static IndicatorLook Resolve(AppearanceSettings a, LayoutIndicatorSettings s)
    {
        var anim = s.Animation switch
        {
            IndicatorAnimation.Fade => AnimationKind.Fade,
            IndicatorAnimation.None => AnimationKind.None,
            _ => a.Animation,
        };
        if (s.CustomStyle)
        {
            var c = a.IndicatorLook;
            return new IndicatorLook(c.Background, c.Foreground, c.Border, c.BorderWidth, c.Radius, c.TextSize, anim, a.DurationMs);
        }

        return new IndicatorLook(a.Primary, a.TextColor, a.BorderColor, a.BorderEnabled ? a.BorderWidth : 0, a.Radius, Math.Round(a.Size * 1.125, 2), anim, a.DurationMs);
    }
}

/// <summary>Содержимое индикатора (код языка и вторая строка); используется и окном, и превью в настройках.</summary>
public sealed class LayoutIndicatorVisual : Border
{
    private readonly TextBlock _code = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _detail = new() { HorizontalAlignment = HorizontalAlignment.Center };

    public LayoutIndicatorVisual()
    {
        var panel = new StackPanel();
        panel.Children.Add(_code);
        panel.Children.Add(_detail);
        Child = panel;
        IsHitTestVisible = false;
    }

    public string CodeText => _code.Text;

    public string? DetailText => _detail.Visibility == Visibility.Visible ? _detail.Text : null;

    public double CodeSize => _code.FontSize;

    /// <summary>Применить подпись и оформление; возвращает размер содержимого (DIP).</summary>
    public Size Set(LayoutLabel label, IndicatorLook look)
    {
        double size = look.TextSize;
        Background = look.Background.ToFrozenBrush();
        BorderBrush = look.BorderWidth > 0 ? look.Border.ToFrozenBrush() : null;
        BorderThickness = new Thickness(look.BorderWidth);
        Padding = new Thickness(size * 0.5, size * 0.18, size * 0.5, size * 0.22);
        foreach (var t in new[] { _code, _detail })
        {
            t.FontFamily = VisualFonts.Keycap;
            t.Foreground = look.Foreground.ToFrozenBrush();
        }

        _code.FontSize = size;
        _code.FontWeight = FontWeights.SemiBold;
        _code.Text = label.Code;
        _detail.FontSize = Math.Max(9, size * 0.38);
        _detail.Text = label.Detail ?? "";
        _detail.Visibility = label.Detail is null ? Visibility.Collapsed : Visibility.Visible;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = DesiredSize;
        // Скругление — доля половины высоты: 1 — «пилюля», 0 — прямые углы.
        CornerRadius = new CornerRadius(look.Radius * Math.Min(desired.Height, desired.Width) / 2);
        return desired;
    }
}

/// <summary>Окно индикатора: no-activate, click-through, создаётся один раз и скрывается, а не пересоздаётся.</summary>
public sealed class LayoutIndicatorWindow : NoActivateWindow
{
    public LayoutIndicatorWindow()
    {
        Title = "Backpack — индикатор раскладки";
        SizeToContent = SizeToContent.Manual;
        Content = Visual;
    }

    public LayoutIndicatorVisual Visual { get; } = new() { Margin = new Thickness(4) };

    public FrameworkElement Box => Visual;

    public string CodeText => Visual.CodeText;

    public string? DetailText => Visual.DetailText;
}

/// <summary>
/// Наблюдение за раскладкой активного внешнего окна: опрос <see cref="ILayoutProbe"/> раз в 100 мс в UI-потоке,
/// только пока индикатор включён. Показ — только по событию детектора; срок показа отсчитывается от момента показа
/// и не продлевается ни повторными опросами, ни изменением оформления. Потоки захвата ввода не затрагиваются.
/// </summary>
public sealed class LayoutIndicatorController : IDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private const double ScreenEdge = 48;

    private readonly ILayoutProbe _probe;
    private readonly Func<MonitorDesc, Rect?> _overlayGroup;
    private readonly Func<IReadOnlyList<MonitorDesc>> _targets;
    private readonly IAnimationClock _clock;
    private readonly LayoutChangeDetector _detector = new((uint)Environment.ProcessId);
    private readonly DispatcherTimer _poll;
    private readonly DispatcherTimer _hold;
    private readonly Dictionary<string, (LayoutIndicatorWindow Window, MonitorDesc Monitor)> _windows = new();
    private LayoutIndicatorSettings _settings = LayoutIndicatorSettings.Default;
    private AppearanceSettings _appearance = AppearanceSettings.Default;
    private TimeSpan? _shownAt;
    private bool _hiding;
    private bool _visible;
    private int _fadeGeneration;
    private bool _disposed;

    /// <param name="overlayGroup">Прямоугольник группы кейкапов на экране (DIP) — для положения «над оверлеем».</param>
    /// <param name="workArea">Рабочая область (DIP) единственного «монитора» (тесты и простое размещение).</param>
    public LayoutIndicatorController(ILayoutProbe probe, Func<Rect> overlayGroup, Func<Rect> workArea, IAnimationClock? clock = null)
        : this(probe, _ => overlayGroup(), () => [Pseudo(workArea())], clock)
    {
    }

    /// <param name="overlayGroup">Группа кейкапов на этом мониторе в DIP его рабочей области (null — кейкапов на нём нет).</param>
    /// <param name="targets">Мониторы, на которых показывается индикатор (выбор пользователя; один источник раскладки на все окна).</param>
    public LayoutIndicatorController(ILayoutProbe probe, Func<MonitorDesc, Rect?> overlayGroup, Func<IReadOnlyList<MonitorDesc>> targets, IAnimationClock? clock = null)
    {
        _probe = probe;
        _overlayGroup = overlayGroup;
        _targets = targets;
        _clock = clock ?? new StopwatchClock();
        _poll = new DispatcherTimer(DispatcherPriority.Normal) { Interval = PollInterval };
        _poll.Tick += (_, _) => Poll();
        _hold = new DispatcherTimer(DispatcherPriority.Normal);
        _hold.Tick += (_, _) => CheckHide();
    }

    /// <summary>«Монитор» из прямоугольника DIP (без Win32-дескриптора): окна размещаются через Left/Top.</summary>
    private static MonitorDesc Pseudo(Rect wa)
    {
        var r = new Presentation.PixelRect((int)wa.Left, (int)wa.Top, (int)wa.Width, (int)wa.Height);
        return new MonitorDesc("work-area", "work-area", r, r, 1.0, true);
    }

    public LayoutChangeDetector Detector => _detector;

    public bool IsPolling => _poll.IsEnabled;

    public LayoutLabel? LastShown { get; private set; }

    public long Shown { get; private set; }

    public bool IsVisible => _visible && !_hiding;

    /// <summary>Только для тестов: окно не выводится на экран, логика показа и срока та же.</summary>
    internal bool ShowOnScreen { get; set; } = true;

    /// <summary>Момент, когда индикатор начнёт исчезать (по часам контроллера), если он показан.</summary>
    public TimeSpan? HideAt => _shownAt is { } t && !_hiding ? t + TimeSpan.FromMilliseconds(_settings.DurationMs) : null;

    public bool? WindowStylesApplied => Window?.Hwnd is > 0 ? Window.StylesApplied : null;

    public nint WindowHandle => Window?.Hwnd ?? 0;

    /// <summary>Первое окно индикатора (для проверок).</summary>
    internal LayoutIndicatorWindow? Window => _windows.Values.Select(x => x.Window).FirstOrDefault();

    /// <summary>Окна индикатора по мониторам (одна модель раскладки на все окна).</summary>
    public IReadOnlyList<(LayoutIndicatorWindow Window, MonitorDesc Monitor)> Windows => _windows.Values.ToList();

    /// <summary>
    /// Phase 4H: показ решает плагин индикатора раскладки (SDK 1.2). Детектор и наблюдение остаются здесь (ядро: ребазлайн при
    /// запуске, разблокировке и выходе из сна), но смена раскладки не показывается сама, а сообщается событием
    /// <see cref="LayoutChanged"/> с причиной (фильтр «при смене окна» применяет плагин). Показ — <see cref="Show(Backpack.Sdk.IndicatorRequest)"/>.
    /// </summary>
    public bool PluginMode { get; private set; }

    /// <summary>Смена раскладки активного окна (режим плагина): подпись и причина.</summary>
    public event Action<LayoutLabel, LayoutChangeReason>? LayoutChanged;

    /// <summary>Режим плагина: наблюдать за раскладкой, пока <paramref name="watch"/> (есть плагин с input.layout); оформление — <paramref name="appearance"/>.</summary>
    public void UsePlugins(bool watch, AppearanceSettings appearance)
    {
        if (_disposed)
        {
            return;
        }

        PluginMode = true;
        _appearance = appearance;
        _detector.ShowOnWindowSwitch = true; // причина передаётся плагину, фильтрует он
        if (watch && !_poll.IsEnabled)
        {
            _detector.Rebaseline(); // начало наблюдения — не событие смены раскладки
            _poll.Start();
        }
        else if (!watch && _poll.IsEnabled)
        {
            _poll.Stop();
        }

        if (IsVisible && LastShown is { } label)
        {
            LayoutAll(label);
        }
    }

    /// <summary>
    /// Показ по запросу плагина: подпись, срок (от момента показа), анимация, положение и стиль (кейкапов или индикатора из
    /// профиля). Мониторы — выбор пользователя в Backpack.
    /// </summary>
    public void Show(Backpack.Sdk.IndicatorRequest r)
    {
        _settings = new LayoutIndicatorSettings(true, r.DurationMs, true, r.Style == "custom",
            r.Animation switch { "fade" => IndicatorAnimation.Fade, "none" => IndicatorAnimation.None, _ => IndicatorAnimation.Inherit },
            r.Position == "screen" ? IndicatorPosition.Screen : IndicatorPosition.AboveOverlay,
            Enum.TryParse<ScreenAnchor>(r.Anchor, ignoreCase: true, out var anchor) ? anchor : ScreenAnchor.Center, r.OffsetX, r.OffsetY);
        ShowLabel(new LayoutLabel(r.Code, r.Detail));
    }

    /// <summary>Скрыть индикатор сразу (плагин отключён или попросил скрыть).</summary>
    public void Hide() => HideNow();

    public void Apply(LayoutIndicatorSettings settings, AppearanceSettings appearance)
    {
        if (_disposed)
        {
            return;
        }

        bool wasPolling = _poll.IsEnabled;
        _settings = settings;
        _appearance = appearance;
        _detector.ShowOnWindowSwitch = settings.ShowOnWindowSwitch;
        if (settings.Enabled && !wasPolling)
        {
            _detector.Rebaseline(); // включение — не событие смены раскладки
            _poll.Start();
        }
        else if (!settings.Enabled)
        {
            _poll.Stop();
            HideNow();
        }

        // Уже показанный индикатор получает новое оформление и положение сразу; срок показа не продлевается.
        if (IsVisible && LastShown is { } label)
        {
            LayoutAll(label);
            ScheduleHold();
        }
    }

    /// <summary>Мониторы изменились (подключение, разрешение, выбор): видимый индикатор переносится без перезапуска срока.</summary>
    public void Relayout()
    {
        if (IsVisible && LastShown is { } label)
        {
            LayoutAll(label);
        }
        else
        {
            foreach (var key in _windows.Keys.ToList())
            {
                if (!_targets().Any(m => m.Id == key))
                {
                    _windows[key].Window.Close();
                    _windows.Remove(key);
                }
            }
        }
    }

    /// <summary>Запуск, разблокировка сессии, выход из сна: текущая раскладка запоминается без показа.</summary>
    public void Rebaseline() => _detector.Rebaseline();

    /// <summary>
    /// Отображение скрыто командой «показать/скрыть»: индикатор не показывается, наблюдение продолжается (база раскладки
    /// остаётся актуальной), поэтому после возврата не всплывает устаревшая смена.
    /// </summary>
    public bool Suppressed
    {
        get => _suppressed;
        set
        {
            _suppressed = value;
            if (value)
            {
                HideNow();
            }
        }
    }

    private bool _suppressed;

    /// <summary>Показ подписи (по событию детектора, для примера в настройках и --demo). Срок показа начинается заново.</summary>
    public void ShowLabel(LayoutLabel label)
    {
        if (_disposed || _suppressed)
        {
            return;
        }

        var fresh = LayoutAll(label);
        foreach (var (w, _) in _windows.Values)
        {
            if (!_visible || fresh.Contains(w))
            {
                w.Box.Opacity = 0;
                if (ShowOnScreen)
                {
                    w.Show();
                }
            }
        }

        _visible = true;
        LastShown = label;
        Shown++;
        _shownAt = _clock.Now;
        _hiding = false;
        Fade(1, null);
        ScheduleHold();
    }

    /// <summary>Проверка срока показа (таймером и тестами с подменёнными часами).</summary>
    internal void CheckHide()
    {
        if (HideAt is { } at && _clock.Now >= at)
        {
            _hold.Stop();
            _hiding = true;
            Fade(0, () =>
            {
                foreach (var (w, _) in _windows.Values)
                {
                    w.Hide();
                }

                _visible = false;
                _hiding = false;
                _shownAt = null;
            });
        }
        else
        {
            ScheduleHold();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _poll.Stop();
        _hold.Stop();
        foreach (var (w, _) in _windows.Values)
        {
            w.Close();
        }

        _windows.Clear();
    }

    private void Poll()
    {
        if (_disposed || (!PluginMode && !_settings.Enabled))
        {
            return;
        }

        if (_detector.Feed(_probe.Sample()) is { } change)
        {
            var label = LayoutNames.Describe(change.Hkl, _probe.InstalledLayouts());
            if (PluginMode)
            {
                LayoutChanged?.Invoke(label, change.Reason);
            }
            else
            {
                ShowLabel(label);
            }
        }
    }

    /// <summary>Окно на каждом целевом мониторе (лишние закрываются). Возвращает только что созданные окна.</summary>
    private List<LayoutIndicatorWindow> LayoutAll(LayoutLabel label)
    {
        var targets = _targets();
        var created = new List<LayoutIndicatorWindow>();
        foreach (var key in _windows.Keys.ToList())
        {
            if (!targets.Any(m => m.Id == key))
            {
                _windows[key].Window.Close();
                _windows.Remove(key);
            }
        }

        foreach (var m in targets)
        {
            if (!_windows.TryGetValue(m.Id, out var entry))
            {
                entry = (new LayoutIndicatorWindow(), m);
                created.Add(entry.Window);
            }

            _windows[m.Id] = (entry.Window, m);
            Layout(entry.Window, label, m);
        }

        return created;
    }

    private void Layout(LayoutIndicatorWindow w, LayoutLabel label, MonitorDesc m)
    {
        var look = IndicatorLook.Resolve(_appearance, _settings);
        var content = w.Visual.Set(label, look);
        var size = new Size(content.Width + 8, content.Height + 8); // + поле окна
        var pos = Position(size, m);
        w.Width = size.Width;
        w.Height = size.Height;
        if (m.Handle == 0)
        {
            w.Left = m.Work.Left + pos.X;
            w.Top = m.Work.Top + pos.Y;
        }
        else
        {
            PhysicalPlacement.Place(w, m.ToPixels(new Rect(pos, size)));
        }
    }

    /// <summary>Положение на первом целевом мониторе (DIP от левого верхнего угла его рабочей области).</summary>
    internal Point Position(Size size) => _targets() is { Count: > 0 } t ? Position(size, t[0]) : new Point(0, 0);

    /// <summary>Положение в DIP рабочей области монитора <paramref name="m"/>.</summary>
    internal Point Position(Size size, MonitorDesc m)
    {
        var wa = m.LocalWorkDip;
        if (_settings.Position == IndicatorPosition.Screen)
        {
            return OverlayPlacement.Place(wa, size, _settings.Anchor, ScreenEdge, ScreenEdge, _settings.OffsetX, _settings.OffsetY);
        }

        // Над группой кейкапов этого монитора (если группа у верхнего края — под ней), по центру группы; плюс смещение.
        // Кейкапов на мониторе нет — по нижнему центру, как группа по умолчанию.
        var g = _overlayGroup(m) is { } local
            ? (m.Handle == 0 ? local with { X = local.X - m.Work.Left, Y = local.Y - m.Work.Top } : local)
            : new Rect((wa.Width - size.Width) / 2, wa.Height - 100 - size.Height, size.Width, size.Height);
        double x = g.Left + ((g.Width - size.Width) / 2) + _settings.OffsetX;
        double y = g.Top - size.Height - 8;
        if (y < wa.Top)
        {
            y = g.Bottom + 8;
        }

        y += _settings.OffsetY;
        return new Point(Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - size.Width)), Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - size.Height)));
    }

    private void ScheduleHold()
    {
        _hold.Stop();
        if (HideAt is { } at)
        {
            var wait = at - _clock.Now;
            _hold.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
            _hold.Start();
        }
    }

    private void HideNow()
    {
        _hold.Stop();
        _fadeGeneration++;
        _shownAt = null;
        _hiding = false;
        _visible = false;
        foreach (var (w, _) in _windows.Values)
        {
            w.Box.BeginAnimation(UIElement.OpacityProperty, null);
            w.Box.Opacity = 0;
            w.Hide();
        }
    }

    private void Fade(double to, Action? completed)
    {
        int generation = ++_fadeGeneration;
        var look = IndicatorLook.Resolve(_appearance, _settings);
        var boxes = _windows.Values.Select(x => x.Window.Box).ToList();
        if (look.Animation == AnimationKind.None || boxes.Count == 0)
        {
            foreach (var b in boxes)
            {
                b.BeginAnimation(UIElement.OpacityProperty, null);
                b.Opacity = to;
            }

            completed?.Invoke();
            return;
        }

        int pending = boxes.Count;
        foreach (var b in boxes)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(look.FadeMs))
            {
                EasingFunction = new BezierEase(CubicBezier.EaseOutQuint),
            };
            anim.Completed += (_, _) =>
            {
                // Устаревшее завершение (за это время начался новый показ) ничего не скрывает.
                if (--pending == 0 && generation == _fadeGeneration && !_disposed)
                {
                    completed?.Invoke();
                }
            };
            b.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private sealed class BezierEase : EasingFunctionBase
    {
        private readonly CubicBezier curve;

        public BezierEase(CubicBezier curve)
        {
            this.curve = curve;
            EasingMode = EasingMode.EaseIn; // EaseInCore = сама кривая, без инверсии EaseOut
        }

        protected override double EaseInCore(double normalizedTime) => curve.Evaluate(normalizedTime);

        protected override Freezable CreateInstanceCore() => new BezierEase(curve);
    }
}
