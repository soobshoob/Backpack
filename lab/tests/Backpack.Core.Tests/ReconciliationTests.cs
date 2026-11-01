using System.Diagnostics;
using Backpack.Core.Model;
using static Backpack.Core.Tests.Harness;

namespace Backpack.Core.Tests;

public class ReconciliationTests
{
    private static readonly long Later = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
    private static readonly long Quiet = Stopwatch.Frequency / 10;

    [Fact]
    public void UntrustedContext_ChangesNothing()
    {
        var h = new Harness();
        h.Down(A);
        h.Drain();
        var probe = new FakeProbe { Context = new ProbeContext(false, InputDesktopIsOurs: null, ForegroundIntegrityNotHigher: true) };
        var c = new ReconcileCounters();

        var v = Reconciliation.Run(h.Model, probe, Later, Quiet, c, h.Transitions);

        Assert.Equal(ReconcileVerdict.Untrusted, v);
        Assert.Empty(probe.Queried);
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));
    }

    [Fact]
    public void ZerosWithoutPositiveControl_AreInconclusive()
    {
        var h = new Harness();
        h.Down(A);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);
        var probe = new FakeProbe(); // всё возвращает 0
        var c = new ReconcileCounters();

        var v = Reconciliation.Run(h.Model, probe, Later, Quiet, c, h.Transitions);
        v = Reconciliation.Run(h.Model, probe, Later, Quiet, c, h.Transitions); // повторные нули тоже ничего не доказывают

        Assert.Equal(ReconcileVerdict.NoPositiveControl, v);
        Assert.Equal(2, c.NoPositiveControl);
        Assert.Equal(KeyState.Unknown, h.Model.AggregateOf(A));
    }

    [Fact]
    public void DownResult_IsNotAttributed_WhenSeveralSlotsShareVk()
    {
        var h = new Harness();
        h.Down(A, device: 1);
        h.Down(A, device: 2);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);
        var probe = new FakeProbe();
        probe.DownVks.Add(VkFor(A)); // агрегированно «A нажата» — но на какой клавиатуре?
        var c = new ReconcileCounters();

        Reconciliation.Run(h.Model, probe, Later, Quiet, c, h.Transitions);

        Assert.Equal(KeyState.Unknown, h.Model.StateOf(1, A));
        Assert.Equal(KeyState.Unknown, h.Model.StateOf(2, A));
        Assert.Equal(2, c.AmbiguousVk);
    }

    [Fact]
    public void DownResult_IsNotAttributed_BetweenEnterAndNumpadEnter()
    {
        var h = new Harness();
        h.Down(Enter);
        h.Down(NumEnter);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);
        var probe = new FakeProbe();
        probe.DownVks.Add(0x0D);

        Reconciliation.Run(h.Model, probe, Later, Quiet, new ReconcileCounters(), h.Transitions);

        Assert.Equal(KeyState.Unknown, h.Model.AggregateOf(Enter));
        Assert.Equal(KeyState.Unknown, h.Model.AggregateOf(NumEnter));
    }

    [Fact]
    public void UniqueUnknown_ConfirmedDown_AndZeroWithPositiveControl_Released()
    {
        var h = new Harness();
        h.Down(A);
        h.Down(B);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions);
        var probe = new FakeProbe();
        probe.DownVks.Add(VkFor(B)); // B действительно держится → положительный контроль

        var v = Reconciliation.Run(h.Model, probe, Later, Quiet, new ReconcileCounters(), h.Transitions);

        Assert.Equal(ReconcileVerdict.Applied, v);
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.Equal(StateReason.ReconciledUp, h.Model.ReasonOf(0, A));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(B));
        Assert.Equal(StateReason.ReconciledDown, h.Model.ReasonOf(0, B));
    }

    [Fact]
    public void KeysWithRecentEvents_AreNotReconciled()
    {
        var h = new Harness();
        h.Down(A);
        h.Drain();
        var probe = new FakeProbe();
        probe.DownVks.Add(0x7B);

        long now = Stopwatch.GetTimestamp(); // событие только что пришло
        var v = Reconciliation.Run(h.Model, probe, now, Stopwatch.Frequency, new ReconcileCounters(), h.Transitions);

        Assert.Equal(ReconcileVerdict.NoCandidates, v);
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));
    }
}
