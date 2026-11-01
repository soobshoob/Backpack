using Backpack.Visual.Layout;

namespace Backpack.Visual.Tests;

public class LayoutTests
{
    private const uint Own = 100, Other = 200;
    private static readonly nint En = 0x04090409, Ru = 0x04190419, RuTypewriter = unchecked((nint)0xF0080419);

    private static LayoutSample S(nint hwnd, nint hkl, uint pid = Other) => new(hwnd, pid, hkl);

    private static LayoutChange? Twice(LayoutChangeDetector d, LayoutSample s)
    {
        var first = d.Feed(s);
        Assert.Null(first);                       // одиночный опрос никогда не показывает
        return d.Feed(s);
    }

    [Fact]
    public void FirstStableValue_IsBaseline_NotAnEvent()
    {
        var d = new LayoutChangeDetector(Own);
        Assert.Null(Twice(d, S(1, En)));
        Assert.Equal(En, d.Baseline);
    }

    [Fact]
    public void ChangeInSameWindow_ShownOnce_NotOnEveryPoll()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        var change = Twice(d, S(1, Ru));
        Assert.Equal(new LayoutChange(Ru, LayoutChangeReason.InWindow), change);
        for (int i = 0; i < 20; i++)
        {
            Assert.Null(d.Feed(S(1, Ru)));       // дальнейшие опросы выдержку не продлевают
        }

        Assert.Equal(1, d.Changes);
    }

    [Fact]
    public void TransientValue_ForOnePoll_IsIgnored()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        Assert.Null(d.Feed(S(2, Ru)));           // переходное окно (Alt+Tab) на один опрос
        Assert.Null(d.Feed(S(1, En)));
        Assert.Null(d.Feed(S(1, En)));
        Assert.Equal(0, d.Changes);
    }

    [Fact]
    public void WindowSwitch_ToDifferentLanguage_RespectsSetting()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        Assert.Equal(new LayoutChange(Ru, LayoutChangeReason.WindowSwitch), Twice(d, S(2, Ru)));

        d.ShowOnWindowSwitch = false;
        Assert.Null(Twice(d, S(1, En)));         // база обновлена молча
        Assert.Equal(En, d.Baseline);
        Assert.Equal(new LayoutChange(Ru, LayoutChangeReason.InWindow), Twice(d, S(1, Ru)));
    }

    [Fact]
    public void WindowSwitch_SameLanguage_IsNotAnEvent()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        Assert.Null(Twice(d, S(2, En)));
    }

    [Fact]
    public void OwnProcessWindows_AreSkipped()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        Assert.Null(Twice(d, S(9, Ru, Own)));    // окно настроек Backpack с другой раскладкой
        Assert.Equal(En, d.Baseline);
        Assert.Equal(2, d.OwnWindowSkips);
        Assert.Null(Twice(d, S(1, En)));         // возврат во внешнее окно — не событие
    }

    [Fact]
    public void Undetermined_NoWindowOrNoHkl_ChangesNothing()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        Assert.Null(d.Feed(S(0, 0)));
        Assert.Null(d.Feed(S(1, 0)));
        Assert.Equal(2, d.Undetermined);
        Assert.Equal(En, d.Baseline);
    }

    [Fact]
    public void Rebaseline_AfterUnlockOrResume_DoesNotFlash()
    {
        var d = new LayoutChangeDetector(Own);
        Twice(d, S(1, En));
        d.Rebaseline();
        Assert.Null(Twice(d, S(1, Ru)));
        Assert.Equal(Ru, d.Baseline);
    }

    [Fact]
    public void Counters_DistinguishFocusThread_OwnerFallback_AndHklDisagreement()
    {
        var d = new LayoutChangeDetector(Own);
        d.Feed(new LayoutSample(1, Other, Ru, HklSource.FocusThread, ThreadsDiffer: true, OwnerHkl: En));
        d.Feed(new LayoutSample(1, Other, Ru, HklSource.FocusThread, ThreadsDiffer: true, OwnerHkl: En));
        d.Feed(new LayoutSample(2, Other, En, HklSource.OwnerThread, false, En));
        var c = d.Counters;
        Assert.Equal(2, c.FocusThread);
        Assert.Equal(1, c.OwnerThreadFallback);
        Assert.Equal(2, c.ThreadsDiffer);
        Assert.Equal(2, c.OwnerHklDiffers);   // способ 2B (поток-владелец) дал бы здесь другой язык
        Assert.Equal(1, c.Baselines);
    }

    [Fact]
    public void Outcomes_ExplainWhyNothingWasShown()
    {
        var d = new LayoutChangeDetector(Own);
        d.Feed(S(0, 0));
        Assert.Equal(LayoutOutcome.NoForeground, d.LastOutcome);
        d.Feed(S(1, En, Own));
        Assert.Equal(LayoutOutcome.OwnWindow, d.LastOutcome);
        d.Feed(S(1, 0));
        Assert.Equal(LayoutOutcome.Unavailable, d.LastOutcome);
        d.Feed(S(1, En));
        Assert.Equal(LayoutOutcome.Unconfirmed, d.LastOutcome);
        d.Feed(S(1, En));
        Assert.Equal(LayoutOutcome.Baseline, d.LastOutcome);
        d.Feed(S(1, En));
        Assert.Equal(LayoutOutcome.Unchanged, d.LastOutcome);
        Assert.NotNull(Twice(d, S(1, Ru)));
        Assert.Equal(LayoutOutcome.ShownInWindow, d.LastDecision);
        Assert.NotNull(Twice(d, S(2, En)));
        Assert.Equal(LayoutOutcome.ShownWindowSwitch, d.LastDecision);
        d.ShowOnWindowSwitch = false;
        Assert.Null(Twice(d, S(3, Ru)));
        Assert.Equal(LayoutOutcome.SuppressedWindowSwitch, d.LastDecision);
        Assert.Equal(1, d.Counters.SuppressedWindowSwitch);
        Assert.Equal(1, d.Counters.ShownInWindow);
        Assert.Equal(1, d.Counters.ShownWindowSwitch);
    }

    [Fact]
    public void LanguageCodes_FromLangId()
    {
        Assert.Equal("EN", LayoutNames.LanguageCode(0x0409));
        Assert.Equal("RU", LayoutNames.LanguageCode(0x0419));
        Assert.Equal("DE", LayoutNames.LanguageCode(0x0407));
        Assert.Null(LayoutNames.LanguageCode(0x2000)); // transient LANGID — язык не выводится
        Assert.Null(LayoutNames.LanguageCode(0));
        Assert.Equal(0x0419, LayoutNames.LangId(RuTypewriter));
        Assert.Equal(0xF008, LayoutNames.DeviceWord(RuTypewriter));
    }

    [Fact]
    public void Compose_ShowsDetailOnlyForSeveralLayoutsOfOneLanguage()
    {
        Assert.Equal(new LayoutLabel("RU", null), LayoutNames.Compose("RU", 1, 1, "Русская"));
        Assert.Equal(new LayoutLabel("RU", "Русская (машинопись)"), LayoutNames.Compose("RU", 2, 2, "Русская (машинопись)"));
        Assert.Equal(new LayoutLabel("RU", "2"), LayoutNames.Compose("RU", 2, 2, null));
        Assert.Equal(new LayoutLabel("?", null), LayoutNames.Compose(null, 1, 1, null));
        Assert.Equal(new LayoutLabel("⌨", "Custom"), LayoutNames.Compose(null, 1, 1, "Custom"));
    }

    [Fact]
    public void Describe_DistinguishesTwoLayoutsOfOneLanguage()
    {
        var installed = new[] { En, Ru, RuTypewriter };
        var a = LayoutNames.Describe(Ru, installed);
        var b = LayoutNames.Describe(RuTypewriter, installed);
        Assert.Equal("RU", a.Code);
        Assert.Equal("RU", b.Code);
        Assert.NotNull(a.Detail);
        Assert.NotNull(b.Detail);
        Assert.NotEqual(a.Detail, b.Detail);
        Assert.Null(LayoutNames.Describe(En, installed).Detail);
    }
}
