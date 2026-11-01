using Backpack.Core.Input;
using Backpack.Core.Model;
using Backpack.Core.Transport;
using static Backpack.Core.Tests.Harness;

namespace Backpack.Core.Tests;

public class TransportTests
{
    [Fact]
    public void Ring_RejectsNonPowerOfTwo_AndKeepsFifoOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventRing(3));
        var ring = new EventRing(4);
        for (int i = 1; i <= 4; i++)
        {
            Assert.True(ring.TryWrite(new InputEvent(i, 0, InputKind.Down, 0, 0, 0, InputFlags.None)));
        }

        Assert.False(ring.TryWrite(new InputEvent(5, 0, InputKind.Down, 0, 0, 0, InputFlags.None)));
        for (int i = 1; i <= 4; i++)
        {
            Assert.True(ring.TryRead(out var e));
            Assert.Equal(i, e.Seq);
        }

        Assert.False(ring.TryRead(out _));
    }

    [Fact]
    public void Overflow_IncrementsEpoch_AndSignalsEvenWhenDropped()
    {
        var h = new Harness(ringCapacity: 2);
        h.Down(A);
        h.Down(B);
        int before = h.Signals;
        h.Up(A); // не помещается
        Assert.Equal(1, h.Capture.DropEpoch);
        // Сигнал «один ожидающий» уже взведён первым событием; после вычитки он должен приходить снова.
        h.Drain();
        h.Up(B);
        Assert.True(h.Signals > before);
    }

    [Fact]
    public void Overflow_SnapshotIsConsistent_CoveredEventsNotReapplied_LaterEventsNotLost()
    {
        var h = new Harness(ringCapacity: 4);
        h.Down(A); // 1
        h.Down(B); // 2
        h.Down(C); // 3
        h.Down(D); // 4
        h.Up(A);   // 5 — отброшено: кольцо полно

        // Потребитель успевает взять одно событие, видит рост эпохи, запрашивает снимок (S = 5).
        h.Reader.Drain(1, h.Transitions);
        Assert.Equal(1, h.SnapshotRequests);
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));

        h.Down(E); // 6 — после снимка, должно дойти
        h.Drain();

        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));   // потерянный Up восстановлен снимком
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(B));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(C));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(D));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(E)); // событие после снимка не потеряно
        Assert.Equal(3, h.Model.Counters.DuplicatesSkipped); // B, C, D (2..4 ≤ S) повторно не применялись
        Assert.Equal(6, h.Model.LastAppliedSeq);
        Assert.False(h.Reader.ResyncPending);

        // Каждый переход по клавише встречается ровно один раз.
        Assert.Equal(1, h.Transitions.Count(t => t == new KeyTransition(B, KeyState.Down)));
    }

    [Fact]
    public void LostLastEvent_WithoutFurtherInput_IsStillDetectedOnNextPoll()
    {
        var h = new Harness(ringCapacity: 2, synchronousSnapshot: false);
        h.Down(A);
        h.Down(B);
        h.Up(A); // последнее событие, потеряно; больше ввода не будет

        h.Drain(); // применяет A↓, B↓; видит эпоху → запрос снимка (асинхронно, «в поток захвата»)
        Assert.Equal(1, h.SnapshotRequests);
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(A));

        h.Capture.CreateAndPublishSnapshot(); // поток захвата обработал запрос
        h.Drain();                             // резервный опрос UI без новых событий

        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(B));
        Assert.Equal(1, h.Model.Counters.SnapshotCorrections);
    }

    [Fact]
    public void LostSnapshotRequest_IsRetriedByPollWithoutNewInput()
    {
        var h = new Harness(ringCapacity: 2, synchronousSnapshot: false);
        h.Down(A);
        h.Down(B);
        h.Up(A); // потеряно
        h.Drain();
        Assert.Equal(1, h.SnapshotRequests); // запрос «не дошёл»: снимок так и не построен

        Thread.Sleep(1100);
        h.Drain(); // ввода больше нет — повторный запрос делает резервный опрос
        Assert.Equal(2, h.SnapshotRequests);
        Assert.Equal(1, h.Reader.SnapshotRerequests);

        h.Capture.CreateAndPublishSnapshot();
        h.Drain();
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.False(h.Reader.ResyncPending);
    }

    [Fact]
    public void RepeatedOverflowAfterSnapshot_TriggersAnotherResync()
    {
        var h = new Harness(ringCapacity: 2, synchronousSnapshot: false);
        h.Down(A); h.Down(B); h.Up(A); // потеря №1
        h.Drain();
        h.Capture.CreateAndPublishSnapshot();
        h.Up(B); h.Down(C); h.Down(D); // потеря №2 после снимка
        h.Drain();
        Assert.Equal(2, h.SnapshotRequests);
        h.Capture.CreateAndPublishSnapshot();
        h.Drain();
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(B));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(C));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(D));
    }

    [Fact]
    public void StaleDown_DoesNotReturnAfterSessionReset_OnNextOverflow()
    {
        var h = new Harness(ringCapacity: 2);
        h.Down(A);
        h.Drain();
        h.Model.MarkAllUnknown(StateReason.SessionReset, h.Transitions); // Up потерян на secure desktop

        // Таблица источника всё ещё «A нажата». Новое переполнение по другим клавишам:
        h.Down(B); h.Down(C); h.Down(D);
        h.Drain(); // снимок применяется в следующем вызове
        h.Drain();

        Assert.Equal(KeyState.Unknown, h.Model.AggregateOf(A)); // старый Down из снимка не вернулся
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(D));
    }

    [Fact]
    public void StaleDown_DoesNotReturnAfterTrustedRelease_OnNextOverflow()
    {
        var h = new Harness(ringCapacity: 2);
        var probe = new FakeProbe();
        h.Down(A);
        h.Down(B);
        h.Drain();

        // A: Up потерян. B физически удерживается — положительный контроль.
        probe.DownVks.Add(VkFor(B));
        var counters = new ReconcileCounters();
        long later = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency * 10;
        var verdict = Reconciliation.Run(h.Model, probe, later, System.Diagnostics.Stopwatch.Frequency / 10, counters, h.Transitions);
        Assert.Equal(ReconcileVerdict.Applied, verdict);
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(B));

        h.Down(C); h.Down(D); h.Down(E); // переполнение
        h.Drain();
        h.Drain();
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
    }

    [Fact]
    public void LateOldSnapshot_DoesNotOverrideNewerReconcileOrEvents()
    {
        var h = new Harness();
        h.Down(A);
        h.Down(B);
        h.Drain();
        var old = h.Capture.CreateAndPublishSnapshot(); // S = 2: A↓, B↓
        h.Capture.TakePublishedSnapshot();               // «затерялся»

        h.Up(B); // 3
        h.Drain();
        var probe = new FakeProbe();
        probe.DownVks.Add(VkFor(C));
        h.Down(C); // 4 — положительный контроль
        h.Drain();
        long later = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency * 10;
        Reconciliation.Run(h.Model, probe, later, 0, new ReconcileCounters(), h.Transitions);
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));

        h.Model.ApplySnapshot(old, h.Transitions); // поздний старый снимок
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(B));
        Assert.Equal(KeyState.Down, h.Model.AggregateOf(C));
        Assert.Equal(4, h.Model.LastAppliedSeq);
    }
}
