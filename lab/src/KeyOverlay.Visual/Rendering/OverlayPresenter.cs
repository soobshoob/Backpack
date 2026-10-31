using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Rendering;

public enum KeycapPhase { Entering, Visible, Exiting }

/// <summary>Визуальная запись презентера: элемент WPF живёт, пока запись не удалена, и не пересоздаётся при обновлениях.</summary>
public sealed class KeycapVisual
{
    internal KeycapVisual(KeycapItem item, KeycapControl control)
    {
        Item = item;
        Control = control;
    }

    public long Id => Item.Id;
    public KeycapItem Item { get; internal set; }
    /// <summary>Элемент кейкапа. Меняется только при смене рендера (идентичность записи, фаза и ход нажатия сохраняются).</summary>
    public KeycapControl Control { get; internal set; }
    public KeycapPhase Phase { get; internal set; }
    public double Opacity { get; internal set; }

    /// <summary>Растёт при каждой смене фазы. Завершение исчезновения принимается только для своего поколения.</summary>
    public int Generation { get; internal set; }

    internal TimeSpan PhaseStart;
    internal double PhaseFrom;
    internal TimeSpan PressStart;
    internal double PressFrom;
    internal double PressTo;
}

/// <summary>Поведение ряда (не внешний вид кейкапа).</summary>
public sealed record RowOptions(TrailMode Trail, MouseLabelMode MouseLabels, HorizontalAlignment ContentAlignment)
{
    public static RowOptions Default { get; } = new(TrailMode.None, MouseLabelMode.Graphic, HorizontalAlignment.Center);
}

/// <summary>
/// Одна группа кейкапов (история — этап 3). Сводит желаемый список <see cref="KeycapItem"/> к живым элементам по Id:
/// <list type="bullet">
/// <item>новый Id → новый элемент; тот же Id → обновление без пересоздания;</item>
/// <item>новый Id той же клавиши, пока её кейкап исчезает → тот же элемент «оживает» с текущей прозрачности;</item>
/// <item>завершение исчезновения принимается только для текущего поколения фазы (устаревшее не удаляет);</item>
/// </list>
/// След предыдущих клавиш (<see cref="TrailMode"/>):
/// <list type="bullet">
/// <item><b>None</b> (рекомендуемый): записи, вытесненные новыми (замена группы), удаляются сразу, без исчезновения на месте;
///       заменяющий кейкап появляется сразу на том же месте; ширина группы не уменьшается, пока группа видна
///       (кейкап остаётся на месте при разной ширине подписей). Плавно исчезает только вся группа, когда новых нажатий нет.</item>
/// <item><b>FadeInPlace</b>: прежнее поведение 2B — вытесненные кейкапы исчезают на своём месте рядом с новыми.</item>
/// </list>
/// Анимация считается по внедряемым часам в <see cref="Tick"/>; в приложении тик идёт от CompositionTarget.Rendering
/// только пока есть активные анимации. Режим None завершает переходы немедленно.
/// </summary>
public sealed class OverlayPresenter : IDisposable
{
    public const int MaxExiting = 32;
    public static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(100);

    private static int s_renderingSubscriptions;

    private readonly IAnimationClock _clock;
    private readonly bool _useRenderingLoop;
    private readonly Border _group = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly Grid _stickyHost = new();
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    private readonly List<KeycapVisual> _visuals = new();
    private AppearanceSettings _s = AppearanceSettings.Default;
    private RowOptions _o = RowOptions.Default;
    private double _stickyWidth;
    private bool _subscribed;
    private bool _disposed;

    public OverlayPresenter(IAnimationClock clock, bool useRenderingLoop = true)
    {
        _clock = clock;
        _useRenderingLoop = useRenderingLoop;
        _stickyHost.Children.Add(_row);
        _group.Child = _stickyHost;
        Apply(_s, _o);
    }

    /// <summary>Корневой элемент группы: размещается окном оверлея или превью.</summary>
    public FrameworkElement Root => _group;

    /// <summary>Ряд кейкапов внутри группы (для измерения положения в тестах).</summary>
    public Panel Row => _row;

    public IReadOnlyList<KeycapVisual> Visuals => _visuals;

    public AppearanceSettings Settings => _s;

    public RowOptions Options => _o;

    /// <summary>Текущая устойчивая минимальная ширина ряда (режим без следа), DIP.</summary>
    public double StickyWidth => _stickyWidth;

    /// <summary>Число активных подписок на CompositionTarget.Rendering по всем презентерам (проверка утечек).</summary>
    public static int ActiveRenderingSubscriptions => Volatile.Read(ref s_renderingSubscriptions);

    public bool IsAnimating => _visuals.Any(v => v.Phase != KeycapPhase.Visible || v.Control.PressOffset != v.PressTo);

    private TimeSpan Duration => TimeSpan.FromMilliseconds(_s.DurationMs);

    private bool Animated => _s.Animation == AnimationKind.Fade;

    private bool Calm => _o.Trail == TrailMode.None;

    public void Apply(AppearanceSettings s) => Apply(s, _o);

    /// <summary>Новые параметры во время анимации: элементы не пересоздаются, текущая фаза продолжается с текущего значения.</summary>
    public void Apply(AppearanceSettings s, RowOptions o)
    {
        var now = _clock.Now;
        Advance(now);
        double oldTravel = KeycapMetrics.From(_s).PressTravel;
        bool rendererChanged = s.Renderer != _s.Renderer;
        _s = s;
        _o = o;
        var m = KeycapMetrics.From(s);
        if (rendererChanged)
        {
            ReplaceControls();
        }

        // Материал подложки (Phase 4H) меняет только заливку и контур подложки — геометрия группы та же.
        var (fill, outline, outlineWidth) = s.BackgroundEnabled ? BackdropMaterials.Paint(s.Backdrop, s.BackgroundColor) : (null, null, 0);
        _group.Background = fill;
        _group.BorderBrush = outline;
        _group.BorderThickness = new Thickness(outlineWidth);
        _group.Padding = new Thickness(s.BackgroundEnabled ? Math.Max(0, m.GroupPadding - outlineWidth) : 0); // контур внутри прежних размеров
        _group.CornerRadius = new CornerRadius(m.GroupCornerRadius);
        _row.HorizontalAlignment = o.ContentAlignment;
        _stickyWidth = 0; // размеры изменились — устойчивая ширина набирается заново

        foreach (var v in _visuals)
        {
            // Перебазирование: фаза продолжается от текущей прозрачности с новой длительностью — без скачка.
            v.PhaseFrom = v.Opacity;
            v.PhaseStart = now;
            double scale = oldTravel > 0 ? m.PressTravel / oldTravel : 0;
            v.PressFrom = v.Control.PressOffset * scale;
            v.PressTo = v.Item.IsPressed ? m.PressTravel : 0;
            v.PressStart = now;
            v.Control.PressOffset = s.PressAnimation ? v.PressFrom : v.PressTo;
        }

        if (!Animated)
        {
            FinishTransitionsNow();
        }

        RefreshContent();
        SyncChildren();
        Advance(now);
        EnsureLoop();
    }

    public void Update(IReadOnlyList<KeycapItem> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Layout.SecondaryLabels.Settings.Mode != KeyOverlay.Visual.Settings.SecondaryLabelMode.Off && items.Any(i => i.Secondary is null && !i.IsModifier))
        {
            // Дополнительная подпись раскладки — для записей, уже прошедших фильтр приватности (оверлей) или искусственных (превью).
            items = items.Select(i => i.Secondary is null && !i.IsModifier ? i with { Secondary = Layout.SecondaryLabels.For(i.Key, i.Numpad) } : i).ToList();
        }

        var now = _clock.Now;
        Advance(now);

        bool groupVisible = _visuals.Any(v => v.Opacity > 0);
        var desired = new HashSet<long>(items.Select(i => i.Id));
        var byId = _visuals.ToDictionary(v => v.Id);
        var ordered = new List<KeycapVisual>(items.Count);
        var created = new List<KeycapVisual>();

        foreach (var item in items)
        {
            if (byId.TryGetValue(item.Id, out var v))
            {
                if (v.Phase == KeycapPhase.Exiting)
                {
                    Revive(v, now); // тот же Id вернулся (например, после ресинхронизации)
                }

                SetItem(v, item, now);
            }
            else if (_visuals.FirstOrDefault(x => x.Phase == KeycapPhase.Exiting && x.Item.MatchKey == item.MatchKey && !desired.Contains(x.Id) && !ordered.Contains(x)) is { } ghost)
            {
                Revive(ghost, now);
                SetItem(ghost, item, now);
                v = ghost;
            }
            else
            {
                v = Create(item, now);
                _visuals.Add(v);
                created.Add(v);
            }

            ordered.Add(v);
        }

        bool replaced = false;
        foreach (var v in _visuals.ToArray())
        {
            if (ordered.Contains(v))
            {
                continue;
            }

            if (Calm && items.Count > 0)
            {
                // Без следа: вытесненный или ещё исчезающий кейкап не занимает место рядом с новыми.
                replaced |= v.Phase != KeycapPhase.Exiting;
                RemoveNow(v);
            }
            else if (v.Phase != KeycapPhase.Exiting)
            {
                BeginExit(v, now);
            }
        }

        if (Calm && replaced && groupVisible && Animated)
        {
            // Замена на том же месте: новый кейкап сразу виден, без мигания пустого места.
            foreach (var v in created)
            {
                v.Generation++;
                v.Phase = KeycapPhase.Visible;
                v.Opacity = v.PhaseFrom = 1;
                v.Control.Opacity = 1;
            }
        }

        // Живые элементы — в порядке модели; исчезающие (режим со следом) остаются на своих местах.
        var liveSlots = new List<int>();
        for (int i = 0; i < _visuals.Count; i++)
        {
            if (ordered.Contains(_visuals[i]))
            {
                liveSlots.Add(i);
            }
        }

        for (int k = 0; k < liveSlots.Count; k++)
        {
            _visuals[liveSlots[k]] = ordered[k];
        }

        TrimExiting();
        if (!Animated)
        {
            FinishTransitionsNow();
        }

        RefreshContent();
        SyncChildren();
        Advance(now);
        EnsureLoop();
    }

    /// <summary>Продвинуть анимации к текущему времени часов. Возвращает true, если анимации ещё идут.</summary>
    public bool Tick()
    {
        if (_disposed)
        {
            return false;
        }

        Advance(_clock.Now);
        return IsAnimating;
    }

    /// <summary>
    /// Завершение исчезновения. Принимается только если элемент всё ещё исчезает и поколение совпадает:
    /// устаревшее завершение (элемент уже ожил) ничего не удаляет.
    /// </summary>
    internal bool TryCompleteExit(KeycapVisual v, int generation)
    {
        if (v.Phase != KeycapPhase.Exiting || v.Generation != generation)
        {
            return false;
        }

        return RemoveNow(v);
    }

    public void Clear()
    {
        _visuals.Clear();
        _row.Children.Clear();
        SyncChildren();
        Unsubscribe();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }

    private bool RemoveNow(KeycapVisual v)
    {
        if (!_visuals.Remove(v))
        {
            return false;
        }

        v.Generation++;
        _row.Children.Remove(v.Control);
        RefreshContent();
        SyncChildren();
        return true;
    }

    /// <summary>
    /// Смена рендера: каждый живой кейкап получает новый элемент на том же месте; Id, фаза, поколение и прозрачность
    /// сохраняются, поэтому идущее исчезновение или оживление продолжается, а устаревшее завершение по-прежнему отклоняется.
    /// </summary>
    private void ReplaceControls()
    {
        foreach (var v in _visuals)
        {
            var old = v.Control;
            var next = KeycapFactory.Create(_s.Renderer);
            next.Opacity = old.Opacity;
            int at = _row.Children.IndexOf(old);
            if (at >= 0)
            {
                _row.Children.RemoveAt(at);
                _row.Children.Insert(at, next);
            }

            v.Control = next;
        }
    }

    private KeycapVisual Create(KeycapItem item, TimeSpan now)
    {
        var control = KeycapFactory.Create(_s.Renderer);
        var v = new KeycapVisual(item, control)
        {
            Phase = Animated ? KeycapPhase.Entering : KeycapPhase.Visible,
            Opacity = Animated ? 0 : 1,
            PhaseFrom = Animated ? 0 : 1,
            PhaseStart = now,
        };
        double travel = KeycapMetrics.From(_s).PressTravel;
        // Новый кейкап нажатой клавиши сразу рисуется нажатым (появление идёт прозрачностью).
        v.PressFrom = v.PressTo = item.IsPressed ? travel : 0;
        v.PressStart = now;
        control.Apply(_s, item, showBadge: false, _o.MouseLabels == MouseLabelMode.Graphic);
        control.PressOffset = v.PressFrom;
        control.Opacity = v.Opacity;
        return v;
    }

    private void SetItem(KeycapVisual v, KeycapItem item, TimeSpan now)
    {
        double target = item.IsPressed ? KeycapMetrics.From(_s).PressTravel : 0;
        if (target != v.PressTo)
        {
            v.PressFrom = v.Control.PressOffset;
            v.PressTo = target;
            v.PressStart = now;
        }

        v.Item = item;
    }

    private void Revive(KeycapVisual v, TimeSpan now)
    {
        v.Generation++;
        v.Phase = Animated ? KeycapPhase.Entering : KeycapPhase.Visible;
        v.PhaseFrom = v.Opacity;
        v.PhaseStart = now;
    }

    private void BeginExit(KeycapVisual v, TimeSpan now)
    {
        v.Generation++;
        v.Phase = KeycapPhase.Exiting;
        v.PhaseFrom = v.Opacity;
        v.PhaseStart = now;
        // Отпускание анимируется и во время исчезновения.
        if (v.PressTo != 0)
        {
            v.PressFrom = v.Control.PressOffset;
            v.PressTo = 0;
            v.PressStart = now;
        }
    }

    private void FinishTransitionsNow()
    {
        foreach (var v in _visuals.ToArray())
        {
            if (v.Phase == KeycapPhase.Exiting)
            {
                TryCompleteExit(v, v.Generation);
            }
            else
            {
                v.Generation++;
                v.Phase = KeycapPhase.Visible;
                v.Opacity = v.PhaseFrom = 1;
                v.Control.Opacity = 1;
            }
        }
    }

    private void TrimExiting()
    {
        var exiting = _visuals.Where(v => v.Phase == KeycapPhase.Exiting).ToList();
        for (int i = 0; i < exiting.Count - MaxExiting; i++)
        {
            TryCompleteExit(exiting[i], exiting[i].Generation);
        }
    }

    private void Advance(TimeSpan now)
    {
        var duration = Duration;
        foreach (var v in _visuals.ToArray())
        {
            // Нажатие и отпускание: 100 мс, easeInOutExpo, от текущего положения (прерывается без скачка); можно выключить.
            double pp = !_s.PressAnimation ? 1 : Math.Clamp((now - v.PressStart).TotalMilliseconds / PressDuration.TotalMilliseconds, 0, 1);
            v.Control.PressOffset = pp >= 1 ? v.PressTo : v.PressFrom + ((v.PressTo - v.PressFrom) * CubicBezier.EaseInOutExpo.Evaluate(pp));

            if (v.Phase == KeycapPhase.Visible)
            {
                continue;
            }

            double target = v.Phase == KeycapPhase.Entering ? 1 : 0;
            // Длительность пропорциональна оставшемуся пути: ожившее с 0.6 доходит до 1 за 0.4·D.
            double span = Math.Abs(target - v.PhaseFrom) * duration.TotalMilliseconds;
            double p = span <= 0 ? 1 : Math.Clamp((now - v.PhaseStart).TotalMilliseconds / span, 0, 1);
            v.Opacity = v.PhaseFrom + ((target - v.PhaseFrom) * CubicBezier.EaseOutQuint.Evaluate(p));
            v.Control.Opacity = v.Opacity;

            if (p >= 1)
            {
                if (v.Phase == KeycapPhase.Exiting)
                {
                    TryCompleteExit(v, v.Generation);
                }
                else
                {
                    v.Phase = KeycapPhase.Visible;
                    v.Generation++;
                }
            }
        }

        UpdateGroupOpacity();
    }

    private void RefreshContent()
    {
        var last = _visuals.LastOrDefault(v => v.Phase != KeycapPhase.Exiting);
        bool graphic = _o.MouseLabels == MouseLabelMode.Graphic;
        foreach (var v in _visuals)
        {
            v.Control.Apply(_s, v.Item, showBadge: _s.ShowPressCount && ReferenceEquals(v, last) && v.Item.PressCount > 1, graphic);
        }
    }

    private void SyncChildren()
    {
        var m = KeycapMetrics.From(_s);
        for (int i = 0; i < _visuals.Count; i++)
        {
            var c = _visuals[i].Control;
            int at = _row.Children.IndexOf(c);
            if (at != i)
            {
                if (at >= 0)
                {
                    _row.Children.RemoveAt(at); // перемещение того же элемента, без пересоздания
                }

                _row.Children.Insert(i, c);
            }

            c.Margin = new Thickness(i == 0 ? 0 : m.KeyGap, 0, 0, 0);
        }

        while (_row.Children.Count > _visuals.Count)
        {
            _row.Children.RemoveAt(_row.Children.Count - 1);
        }

        if (_visuals.Count == 0)
        {
            _stickyWidth = 0;
        }
        else if (Calm)
        {
            // Устойчивая ширина: пока группа видна, ряд не становится уже — без горизонтального дрожания.
            _row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _stickyWidth = Math.Max(_stickyWidth, _row.DesiredSize.Width);
        }
        else
        {
            _stickyWidth = 0;
        }

        _stickyHost.MinWidth = _stickyWidth;
        _group.Visibility = _visuals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateGroupOpacity();
    }

    /// <summary>Подложка группы появляется и уходит вместе с кейкапами (не пропадает мгновенно, ср. Phase 2A AN-05).</summary>
    private void UpdateGroupOpacity() => _group.Opacity = _visuals.Count == 0 ? 0 : _visuals.Max(v => v.Opacity);

    private void EnsureLoop()
    {
        if (_useRenderingLoop && !_disposed && IsAnimating && !_subscribed)
        {
            CompositionTarget.Rendering += OnRendering;
            _subscribed = true;
            Interlocked.Increment(ref s_renderingSubscriptions);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!Tick())
        {
            Unsubscribe();
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            CompositionTarget.Rendering -= OnRendering;
            _subscribed = false;
            Interlocked.Decrement(ref s_renderingSubscriptions);
        }
    }
}
