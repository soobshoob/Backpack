namespace KeyOverlay.Visual.Layout;

public enum LayoutChangeReason { InWindow, WindowSwitch }

public readonly record struct LayoutChange(nint Hkl, LayoutChangeReason Reason);

/// <summary>Итог последнего опроса — почему индикатор показан или не показан (для диагностики, без данных об окнах).</summary>
public enum LayoutOutcome
{
    NotStarted,
    /// <summary>Нет окна переднего плана.</summary>
    NoForeground,
    /// <summary>Окно есть, но раскладку получить не удалось.</summary>
    Unavailable,
    /// <summary>Окно KeyOverlay — не учитывается.</summary>
    OwnWindow,
    /// <summary>Значение ещё не подтверждено вторым опросом.</summary>
    Unconfirmed,
    /// <summary>Значение запомнено как исходное (запуск, разблокировка, включение) — без показа.</summary>
    Baseline,
    /// <summary>Раскладка не изменилась.</summary>
    Unchanged,
    ShownInWindow,
    ShownWindowSwitch,
    /// <summary>Другая раскладка в другом окне, но показ при переключении окна выключен.</summary>
    SuppressedWindowSwitch,
}

/// <summary>Агрегированные счётчики наблюдения за раскладкой (без окон, заголовков, клавиш и текста).</summary>
public sealed class LayoutCounters
{
    public long Samples { get; internal set; }
    public long NoForeground { get; internal set; }
    public long Unavailable { get; internal set; }
    public long OwnWindow { get; internal set; }
    public long FocusThread { get; internal set; }
    public long ActiveThread { get; internal set; }
    public long OwnerThreadFallback { get; internal set; }
    public long ThreadsDiffer { get; internal set; }
    public long OwnerHklDiffers { get; internal set; }
    public long Baselines { get; internal set; }
    public long ShownInWindow { get; internal set; }
    public long ShownWindowSwitch { get; internal set; }
    public long SuppressedWindowSwitch { get; internal set; }
}

/// <summary>
/// Решает, когда показывать индикатор. Чистая логика без Win32 (тестируется).
/// <list type="bullet">
/// <item>значение принимается, только если одна и та же пара (окно, HKL) получена два опроса подряд — без вспышек
///       на переходных состояниях (Alt+Tab, переключатель задач);</item>
/// <item>первое принятое значение — база, не событие; так же после <see cref="Rebaseline"/> (запуск, разблокировка, сон);</item>
/// <item>окна собственного процесса пропускаются: раскладка окна настроек — не раскладка внешнего приложения;</item>
/// <item>нет окна / нет HKL — «неопределённо»: ничего не показывается, база не меняется;</item>
/// <item>одно изменение — одно событие: повторные опросы с тем же значением ничего не дают.</item>
/// </list>
/// Нажатия клавиш (Alt+Shift, Win+Space) не используются вовсе.
/// </summary>
public sealed class LayoutChangeDetector
{
    private readonly uint _ownProcessId;
    private nint _candidateHwnd, _candidateHkl;
    private int _candidateCount;
    private nint? _baseline;
    private nint _baselineHwnd;

    public LayoutChangeDetector(uint ownProcessId) => _ownProcessId = ownProcessId;

    public bool ShowOnWindowSwitch { get; set; } = true;

    public LayoutCounters Counters { get; } = new();

    public LayoutOutcome LastOutcome { get; private set; } = LayoutOutcome.NotStarted;

    /// <summary>Итог последнего подтверждённого значения (не «неподтверждено»), чтобы было видно, чем закончилась последняя смена.</summary>
    public LayoutOutcome LastDecision { get; private set; } = LayoutOutcome.NotStarted;

    public HklSource LastSource { get; private set; }

    public bool LastThreadsDiffer { get; private set; }

    public nint? Baseline => _baseline;

    public long Samples => Counters.Samples;
    public long Undetermined => Counters.NoForeground + Counters.Unavailable;
    public long OwnWindowSkips => Counters.OwnWindow;
    public long Changes => Counters.ShownInWindow + Counters.ShownWindowSwitch;

    public void Rebaseline()
    {
        _baseline = null;
        _candidateCount = 0;
    }

    public LayoutChange? Feed(LayoutSample s)
    {
        var c = Counters;
        c.Samples++;
        if (s.Hwnd == 0)
        {
            c.NoForeground++;
            return Done(LayoutOutcome.NoForeground);
        }

        if (s.ProcessId == _ownProcessId)
        {
            c.OwnWindow++;
            return Done(LayoutOutcome.OwnWindow);
        }

        if (s.Hkl == 0)
        {
            c.Unavailable++;
            return Done(LayoutOutcome.Unavailable);
        }

        switch (s.Source)
        {
            case HklSource.FocusThread: c.FocusThread++; break;
            case HklSource.ActiveThread: c.ActiveThread++; break;
            default: c.OwnerThreadFallback++; break;
        }

        if (s.ThreadsDiffer)
        {
            c.ThreadsDiffer++;
        }

        if (s.OwnerHkl != 0 && s.OwnerHkl != s.Hkl)
        {
            c.OwnerHklDiffers++;
        }

        LastSource = s.Source;
        LastThreadsDiffer = s.ThreadsDiffer;

        if (s.Hwnd == _candidateHwnd && s.Hkl == _candidateHkl)
        {
            _candidateCount++;
        }
        else
        {
            (_candidateHwnd, _candidateHkl, _candidateCount) = (s.Hwnd, s.Hkl, 1);
        }

        if (_candidateCount < 2)
        {
            LastOutcome = LayoutOutcome.Unconfirmed;
            return null;
        }

        if (_candidateCount > 2)
        {
            LastOutcome = LayoutOutcome.Unchanged;
            return null; // уже обработано
        }

        if (_baseline is null)
        {
            (_baseline, _baselineHwnd) = (s.Hkl, s.Hwnd);
            c.Baselines++;
            return Decide(LayoutOutcome.Baseline, null);
        }

        var reason = s.Hwnd == _baselineHwnd ? LayoutChangeReason.InWindow : LayoutChangeReason.WindowSwitch;
        bool changed = s.Hkl != _baseline;
        (_baseline, _baselineHwnd) = (s.Hkl, s.Hwnd);
        if (!changed)
        {
            return Decide(LayoutOutcome.Unchanged, null);
        }

        if (reason == LayoutChangeReason.WindowSwitch && !ShowOnWindowSwitch)
        {
            c.SuppressedWindowSwitch++;
            return Decide(LayoutOutcome.SuppressedWindowSwitch, null);
        }

        if (reason == LayoutChangeReason.InWindow)
        {
            c.ShownInWindow++;
            return Decide(LayoutOutcome.ShownInWindow, new LayoutChange(s.Hkl, reason));
        }

        c.ShownWindowSwitch++;
        return Decide(LayoutOutcome.ShownWindowSwitch, new LayoutChange(s.Hkl, reason));
    }

    private LayoutChange? Done(LayoutOutcome outcome)
    {
        _candidateCount = 0;
        LastOutcome = outcome;
        return null;
    }

    private LayoutChange? Decide(LayoutOutcome outcome, LayoutChange? change)
    {
        LastOutcome = outcome;
        LastDecision = outcome;
        return change;
    }
}
