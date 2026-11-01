using Backpack.Core.Diagnostics;

namespace Backpack.Core.Tests;

/// <summary>Phase 4I, п. 4: этап остановки конвейера отображения и восстановление окна без пожизненного лимита.</summary>
public class DisplayWatchTests
{
    private static readonly DisplayWindowFacts Good = new(IntendedVisible: true, Visible: true, Topmost: true, Covered: false, PrivacyOpen: true, SourceSilent: false);

    private static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void A_shown_press_that_reaches_the_window_is_ok()
    {
        var w = new DisplayWatch();
        w.KeyEvent(S(1));
        w.Transition(shown: true, pending: false, down: true, S(1));
        w.ViewApplied(S(1.01));
        w.Refreshed(1, S(1.02));
        w.Evaluate(S(2.5), Good);
        Assert.Equal(DisplayStage.Ok, w.Stage);
    }

    [Theory]
    [InlineData(false, false, true, true, false, DisplayStage.NotApplied)]
    [InlineData(true, false, true, true, false, DisplayStage.NotRefreshed)]
    [InlineData(true, true, false, true, false, DisplayStage.WindowHidden)]
    [InlineData(true, true, true, false, false, DisplayStage.WindowNotTopmost)]
    [InlineData(true, true, true, true, true, DisplayStage.WindowCovered)]
    public void The_stage_where_the_press_stopped_is_named(bool applied, bool refreshed, bool visible, bool topmost, bool covered, DisplayStage expected)
    {
        var w = new DisplayWatch();
        w.KeyEvent(S(1));
        w.Transition(true, false, true, S(1));
        if (applied)
        {
            w.ViewApplied(S(1.01));
        }

        if (refreshed)
        {
            w.Refreshed(1, S(1.02));
        }

        w.Evaluate(S(3), Good with { Visible = visible, Topmost = topmost, Covered = covered });
        Assert.Equal(expected, w.Stage);
        Assert.True(DisplayWatch.IsFault(expected));
        Assert.Equal(1, w.Entered[expected]);
        Assert.DoesNotContain("K", w.Line(S(3)).Split(' ').Where(p => p.StartsWith("key=", StringComparison.Ordinal))); // без клавиш
    }

    [Fact]
    public void Privacy_and_intentional_hiding_are_not_faults_and_old_presses_become_idle()
    {
        var w = new DisplayWatch();
        w.KeyEvent(S(1));
        w.Transition(shown: false, pending: false, down: true, S(1));
        w.Evaluate(S(2), Good with { PrivacyOpen = false });
        Assert.Equal(DisplayStage.PrivacyClosed, w.Stage);
        Assert.False(DisplayWatch.IsFault(w.Stage));
        w.Evaluate(S(2), Good with { IntendedVisible = false });
        Assert.Equal(DisplayStage.IntentionallyHidden, w.Stage);
        w.Evaluate(S(100), Good);
        Assert.Equal(DisplayStage.Idle, w.Stage);
        Assert.Equal(1, w.Hidden);
    }

    [Fact]
    public void Silent_source_is_reported_and_a_press_in_flight_keeps_the_previous_stage()
    {
        var w = new DisplayWatch();
        w.Evaluate(S(1), Good with { SourceSilent = true });
        Assert.Equal(DisplayStage.NoSourceEvents, w.Stage);
        w.KeyEvent(S(5));
        w.Transition(true, false, true, S(5));
        w.Evaluate(S(5.2), Good);
        Assert.Equal(DisplayStage.NoSourceEvents, w.Stage); // меньше Grace — нажатие ещё в пути
    }

    [Fact]
    public void Window_recovery_is_limited_per_window_of_time_not_for_the_whole_process_lifetime()
    {
        var p = new RecoveryPolicy(TimeSpan.FromSeconds(5), 5, TimeSpan.FromMinutes(5));
        var code = FindingCode.OverlayTopmostLost;
        var t = TimeSpan.Zero;
        for (int i = 0; i < 5; i++)
        {
            Assert.True(p.TryBegin(code, FindingLevel.Confirmed, true, false, t));
            t += TimeSpan.FromSeconds(10);
        }

        Assert.False(p.TryBegin(code, FindingLevel.Confirmed, true, false, t)); // шестая за 5 минут — отказ
        // Через час (Phase 4H: отказ навсегда — кейкапы пропадали до выключения и включения показа) окно снова восстанавливается.
        Assert.True(p.TryBegin(code, FindingLevel.Confirmed, true, false, TimeSpan.FromHours(1)));
        Assert.Equal(6, p.Attempts);
    }
}
