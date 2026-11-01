using System.Diagnostics;
using Backpack.Core.Input;
using Backpack.Core.Model;
using static Backpack.Core.Tests.Harness;

namespace Backpack.Core.Tests;

public class InputModelTests
{
    [Fact]
    public void DownUp_WithAutorepeat_ProducesSingleDownAndSingleUpTransition()
    {
        var h = new Harness();
        h.Down(A);
        h.Down(A); // автоповтор
        h.Down(A);
        h.Up(A);
        h.Drain();

        Assert.Equal(new[] { (A, KeyState.Down), (A, KeyState.Up) }, h.Transitions.Select(t => (t.KeySlot, t.State)));
        Assert.All(h.Transitions, t => Assert.NotEqual(0, t.At)); // время захвата события переходит в переход (приватность по контексту нажатия)
        Assert.Equal(2, h.Capture.Repeats);
        Assert.Equal(4, h.Model.Counters.EventsApplied);
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
    }

    [Fact]
    public void AutorepeatIsFlaggedAtSourceAndDoesNotRaisePressCount()
    {
        var h = new Harness();
        var display = new DisplayModel(TimeSpan.FromSeconds(2));
        h.Down(A);
        h.Down(A);
        h.Down(A);
        h.Drain();
        foreach (var t in h.Transitions)
        {
            display.OnTransition(t, TimeSpan.Zero);
        }

        var entry = Assert.Single(display.Entries);
        Assert.Equal(1, entry.PressCount);
        Assert.Equal(KeyState.Down, entry.State);
    }

    [Fact]
    public void LeftAndRightModifiers_AreDistinctPhysicalKeys_WithDistinctProbeVk()
    {
        var h = new Harness();
        h.Down(LShift, vk: 0x10); // Raw Input отдаёт общий VK_SHIFT
        h.Down(RShift, vk: 0x10);
        h.Up(LShift, vk: 0x10);
        h.Drain();

        Assert.Equal(KeyState.Up, h.Model.AggregateOf(LShift));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(RShift));
        Assert.Equal("LShift", KeyInfo.Label(LShift));
        Assert.Equal("RShift", KeyInfo.Label(RShift));
        Assert.Equal((ushort)0xA0, KeyInfo.ProbeVk(LShift, 0x10));
        Assert.Equal((ushort)0xA1, KeyInfo.ProbeVk(RShift, 0x10));
        Assert.Equal("RCtrl", KeyInfo.Label(RCtrl));
        Assert.Equal(ModifierKind.Ctrl, KeyInfo.Modifier(RCtrl));
    }

    [Fact]
    public void NumpadEnterAndEnter_AreDifferentPhysicalKeys_ButShareVk()
    {
        Assert.NotEqual(Enter, NumEnter);
        Assert.Equal("Enter", KeyInfo.Label(Enter));
        Assert.Equal("Num Enter", KeyInfo.Label(NumEnter));
        Assert.Equal(KeyInfo.ProbeVk(Enter, 0x0D), KeyInfo.ProbeVk(NumEnter, 0x0D));
    }

    [Fact]
    public void SyntheticLayoutCtrl_IsSeparateFromRealLeftCtrl()
    {
        int fake = new PhysicalKey(ScanPrefix.Synthetic, 0x1D).Slot;
        Assert.NotEqual(LCtrl, fake);
        Assert.Equal("LCtrl*", KeyInfo.Label(fake));
        Assert.Equal(ModifierKind.Ctrl, KeyInfo.Modifier(fake));
    }

    [Fact]
    public void LostRelease_AfterSessionReset_BecomesUnknown_NotUp_AndIsResolvedByNextEvent()
    {
        var h = new Harness();
        h.Down(A);
        h.Drain();
        // Up произошёл на защищённом рабочем столе и источником не наблюдался.
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);

        Assert.Equal(KeyState.Unknown, h.Model.AggregateOf(A));
        Assert.Equal(StateReason.SessionReset, h.Model.ReasonOf(0, A));

        h.Down(A); // источник видит это как повтор: его таблица всё ещё «нажата»
        h.Drain();
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));
        Assert.Equal(1, h.Model.Counters.UnknownResolvedByEvent);
    }

    [Fact]
    public void UnknownKey_DoesNotFormChord()
    {
        var h = new Harness();
        var display = new DisplayModel(TimeSpan.FromSeconds(2));
        h.Down(LCtrl);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);
        h.Down(C);
        h.Drain();
        foreach (var t in h.Transitions)
        {
            display.OnTransition(t, TimeSpan.Zero);
        }

        // LCtrl в Unknown не удерживается, поэтому C — новая группа, а не «Ctrl + C».
        var entry = Assert.Single(display.Entries);
        Assert.Equal(C, entry.KeySlot);
    }

    [Fact]
    public void DeviceRemoval_ReleasesOnlyThatDevice()
    {
        var h = new Harness();
        h.Down(A, device: 1);
        h.Down(A, device: 2);
        h.Down(B, device: 1);
        h.Drain();
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));

        h.Capture.RemoveDevice(1, Stopwatch.GetTimestamp());
        h.Drain();

        Assert.Equal(KeyState.Up, h.Model.StateOf(1, A));
        Assert.Equal(KeyState.Up, h.Model.StateOf(1, B));
        Assert.Equal(StateReason.DeviceRemoved, h.Model.ReasonOf(1, A));
        Assert.Equal(KeyState.Down, h.Model.StateOf(2, A));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A)); // устройство 2 всё ещё держит A
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(B));
        Assert.Equal(2, h.Model.Counters.DeviceRemovedReleases);

        h.Capture.RemoveDevice(2, Stopwatch.GetTimestamp());
        h.Drain();
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
    }

    [Fact]
    public void StaleRepeatHeuristic_MarksLastPressedKeyUnknown_OnlyWithoutActivity()
    {
        var model = new SourceModel(SourceKind.RawInput);
        var output = new List<KeyTransition>();
        long t0 = 10 * Stopwatch.Frequency;
        long window = (long)(1.5 * Stopwatch.Frequency);
        model.ApplyEvent(new InputEvent(1, t0, InputKind.Down, (ushort)A, 0, 0x41, InputFlags.None), t0, output);

        model.CheckStaleRepeat(t0 + Stopwatch.Frequency, window, output);
        Assert.Equal(KeyState.Down, model.AggregateOf(A));

        // повтор продлевает активность
        model.ApplyEvent(new InputEvent(2, t0, InputKind.Down, (ushort)A, 0, 0x41, InputFlags.Repeat), t0 + Stopwatch.Frequency, output);
        model.CheckStaleRepeat(t0 + 2 * Stopwatch.Frequency, window, output);
        Assert.Equal(KeyState.Down, model.AggregateOf(A));

        model.CheckStaleRepeat(t0 + 3 * Stopwatch.Frequency, window, output);
        Assert.Equal(KeyState.Unknown, model.AggregateOf(A));
        Assert.Equal(1, model.Counters.StaleNoRepeat);
    }

    [Fact]
    public void StaleRepeatHeuristic_DoesNotTouchEarlierHeldModifier()
    {
        var model = new SourceModel(SourceKind.RawInput);
        var output = new List<KeyTransition>();
        long t0 = 10 * Stopwatch.Frequency;
        model.ApplyEvent(new InputEvent(1, t0, InputKind.Down, (ushort)LCtrl, 0, 0xA2, InputFlags.None), t0, output);
        model.ApplyEvent(new InputEvent(2, t0, InputKind.Down, (ushort)C, 0, 0x43, InputFlags.None), t0, output);
        model.ApplyEvent(new InputEvent(3, t0, InputKind.Up, (ushort)C, 0, 0x43, InputFlags.None), t0, output);

        // Последняя нажатая клавиша (C) отпущена; удерживаемый Ctrl не повторяется — это норма, не потеря.
        model.CheckStaleRepeat(t0 + 10 * Stopwatch.Frequency, Stopwatch.Frequency, output);
        Assert.Equal(KeyState.Down, model.AggregateOf(LCtrl));
    }
}
