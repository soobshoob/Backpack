using System.Diagnostics;
using KeyOverlay.Lab.Core.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;
using KeyOverlay.Lab.Core.Transport;
using static KeyOverlay.Lab.Core.Tests.Harness;

namespace KeyOverlay.Lab.Core.Tests;

/// <summary>
/// Phase 2E: Win-сочетания по всей цепочке «событие → состояние → группа», правило объединения «любые одновременные клавиши»,
/// импульсы колеса. События искусственные: это проверка обработки, а не доказательство того, что Windows присылает
/// настоящее Win+Space приложению.
/// </summary>
public class Phase2ETests
{
    private static readonly int LWin = new PhysicalKey(ScanPrefix.E0, 0x5B).Slot;
    private static readonly int RWin = new PhysicalKey(ScanPrefix.E0, 0x5C).Slot;
    private const int Space = 0x39;
    private const int Esc = 0x01;

    private sealed class Pipeline
    {
        private readonly Harness _h = new(ringCapacity: 64);
        private TimeSpan _t;

        public Pipeline(TimeSpan linger) => Display = new DisplayModel(linger);

        public DisplayModel Display { get; }
        public Harness H => _h;

        public void Down(int slot, InputFlags flags = InputFlags.None) => Step(() => _h.Down(slot, flags: flags));
        public void Up(int slot) => Step(() => _h.Up(slot));

        public void Step(Action capture)
        {
            capture();
            _t += TimeSpan.FromMilliseconds(30);
            _h.Transitions.Clear();
            _h.Drain();
            Apply();
        }

        public void Apply()
        {
            foreach (var t in _h.Transitions)
            {
                Display.OnTransition(t, _t);
            }

            _h.Transitions.Clear();
        }

        public string[] Labels => Display.Entries.Select(e => e.Label).ToArray();

        public TimeSpan Now => _t;

        public void Advance(TimeSpan d) => _t += d;
    }

    [Theory]
    [InlineData(0)] // Win↓ Space↓ Space↑ Win↑
    [InlineData(1)] // Win↓ Space↓ Win↑ Space↑
    [InlineData(2)] // Win↓ (автоповтор) Space↓ (автоповтор) Space↑ Win↑
    public void WinSpace_AllReleaseOrders_FormOneGroup(int order)
    {
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(LWin);
        if (order == 2)
        {
            p.Down(LWin);
            p.Down(LWin);
        }

        p.Down(Space);
        Assert.Equal(new[] { "LWin", "Space" }, p.Labels);
        if (order == 2)
        {
            p.Down(Space);
        }

        if (order == 1)
        {
            p.Up(LWin);
            p.Up(Space);
        }
        else
        {
            p.Up(Space);
            p.Up(LWin);
        }

        Assert.Equal(new[] { "LWin", "Space" }, p.Labels);
        Assert.All(p.Display.Entries, e => Assert.Equal(KeyState.Up, e.State));
        Assert.Equal(1, p.H.Model.Counters.WinChords);
        Assert.Equal(1, p.H.Model.Counters.ModifierChords);
        Assert.Equal(1, p.Display.Entries.Single(e => e.Label == "Space").PressCount);
    }

    [Fact]
    public void WinSpace_RepeatedSpaceWhileWinHeld_CountsPresses_NoDuplicates()
    {
        // Переключение раскладки по кругу: Win удерживается, Space нажимается трижды.
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(RWin);
        for (int i = 0; i < 3; i++)
        {
            p.Down(Space);
            p.Up(Space);
        }

        p.Up(RWin);
        Assert.Equal(new[] { "RWin", "Space" }, p.Labels);
        Assert.Equal(3, p.Display.Entries.Single(e => e.Label == "Space").PressCount);
        Assert.Equal(3, p.H.Model.Counters.WinChords);
    }

    [Fact]
    public void ContextReset_WhileWinHeld_TurnsWinUnknown_AndNextKeyDoesNotJoin()
    {
        // Смена рабочего стола / блокировка во время удержания: Win → Unknown. Unknown не удержание: следующая клавиша
        // не формирует сочетание. Так выглядит потеря сочетания после сброса контекста — это не ошибка группировки.
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(LWin);
        var output = new List<KeyTransition>();
        p.H.Model.MarkAllUnknown(StateReason.DesktopSwitch, output);
        p.H.Transitions.AddRange(output);
        p.Apply();
        Assert.Equal(KeyState.Unknown, p.Display.Entries.Single().State);

        p.Down(Space);
        Assert.Equal(new[] { "Space" }, p.Labels);
        Assert.Equal(0, p.H.Model.Counters.WinChords);

        // Следующее событие Win снова делает его достоверным: сочетание формируется.
        p.Up(Space);
        p.Down(LWin);
        p.Down(Space);
        Assert.Equal(new[] { "LWin", "Space" }, p.Labels);
    }

    [Fact]
    public void StaleRepeatHeuristic_WinHeldWithoutRepeat_BecomesUnknown()
    {
        // Гипотеза из отчёта 2E: если Win удерживается дольше окна автоповтора без повторов (клавиатура или драйвер не повторяют
        // модификатор), эвристика устаревшего Down переводит его в Unknown, и Win+Space не образует группу.
        // Тест фиксирует это поведение как известное; счётчик StaleNoRepeat в экспорте показывает, случается ли оно на деле.
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(LWin);
        var output = new List<KeyTransition>();
        p.H.Model.CheckStaleRepeat(Stopwatch.GetTimestamp() + (5 * Stopwatch.Frequency), Stopwatch.Frequency, output);
        p.H.Transitions.AddRange(output);
        p.Apply();
        Assert.Equal(1, p.H.Model.Counters.StaleNoRepeat);
        p.Down(Space);
        Assert.Equal(new[] { "Space" }, p.Labels);
    }

    // ── правило объединения ──────────────────────────────────────────────────

    [Fact]
    public void AnyHeld_KeysHeldTogether_JoinAfterThreshold()
    {
        // Нажатия перекрылись быстрее порога (30 мс), но обе клавиши остаются нажатыми — через порог это одна группа.
        var p = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld } };
        p.Down(A);
        p.Down(B);
        Assert.Equal(new[] { "B" }, p.Labels);
        Assert.Equal(1, p.Display.PendingCount);
        Assert.True(p.Display.Tick(p.Now + TimeSpan.FromMilliseconds(200)));
        Assert.Equal(new[] { "A", "B" }, p.Labels);
        p.Up(A);
        p.Up(B);
        p.Down(C); // ничего не удерживается — новая группа
        Assert.Equal(new[] { "C" }, p.Labels);
    }

    [Fact]
    public void AnyHeld_FastTypingOverlap_DoesNotJoin()
    {
        // Быстрая печать: A ещё нажата, когда нажимают B, но отпускается раньше порога — группы нет ни сразу, ни потом.
        var p = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld } };
        p.Down(A);
        p.Down(B);
        p.Up(A);
        Assert.Equal(0, p.Display.PendingCount);
        p.Display.Tick(p.Now + TimeSpan.FromMilliseconds(300));
        Assert.Equal(new[] { "B" }, p.Labels);
        p.Down(C);
        p.Up(B);
        p.Up(C);
        p.Display.Tick(p.Now + TimeSpan.FromMilliseconds(300));
        Assert.Equal(new[] { "C" }, p.Labels);
    }

    [Fact]
    public void AnyHeld_SettledHold_JoinsImmediately_AndZeroThresholdKeepsOldBehavior()
    {
        var p = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld } };
        p.Down(A);
        p.Advance(TimeSpan.FromMilliseconds(400)); // A удерживается дольше порога
        p.Down(B);
        Assert.Equal(new[] { "A", "B" }, p.Labels);

        var q = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld, AnyHeldThreshold = TimeSpan.Zero } };
        q.Down(A);
        q.Down(B);
        Assert.Equal(new[] { "A", "B" }, q.Labels);
    }

    [Fact]
    public void Modifiers_CalmTyping_NoRegression()
    {
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(A);
        p.Down(B);
        Assert.Equal(new[] { "B" }, p.Labels);
        p.Up(A);
        p.Up(B);
        p.Down(LCtrl);
        p.Down(C);
        Assert.Equal(new[] { "LCtrl", "C" }, p.Labels);
    }

    [Fact]
    public void AnyHeld_Repeat_DoesNotDuplicate_AndUnknownIsNotHeld()
    {
        var p = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld } };
        p.Down(A);
        p.Down(A);
        p.Down(A);
        Assert.Single(p.Display.Entries);
        Assert.Equal(1, p.Display.Entries[0].PressCount);

        var output = new List<KeyTransition>();
        p.H.Model.MarkAllUnknown(StateReason.SessionReset, output);
        p.H.Transitions.AddRange(output);
        p.Apply();
        p.Down(B); // A — Unknown: не удержание, B начинает новую группу
        Assert.Equal(new[] { "B" }, p.Labels);
    }

    [Fact]
    public void AnyHeld_ReleasedInLinger_IsNotHeld()
    {
        // Одновременность — пересечение Down, а не выдержка: A отпущена и ещё видна, B — новая группа.
        var p = new Pipeline(TimeSpan.FromSeconds(5)) { Display = { Rule = ChordRule.AnyHeld } };
        p.Down(A);
        p.Up(A);
        p.Down(B);
        Assert.Equal(new[] { "B" }, p.Labels);
    }

    [Fact]
    public void SwitchingRule_DoesNotReturnOldEntries_NorLeaveStuckKeys()
    {
        var p = new Pipeline(TimeSpan.FromSeconds(5));
        p.Down(A);
        p.Down(B); // Modifiers: только B
        Assert.Equal(new[] { "B" }, p.Labels);
        p.Display.Rule = ChordRule.AnyHeld;
        Assert.Equal(new[] { "B" }, p.Labels); // A не возвращается
        p.Up(A);
        p.Up(B);
        p.Display.Rule = ChordRule.Modifiers;
        Assert.All(p.Display.Entries, e => Assert.NotEqual(KeyState.Down, e.State));
        Assert.Equal(0, p.H.Model.CountInState(KeyState.Down));
    }

    // ── колесо ───────────────────────────────────────────────────────────────

    [Fact]
    public void Wheel_SignAxisAndSmallDelta_ReachModel_WithoutHeldState()
    {
        var h = new Harness(ringCapacity: 16);
        var pulses = new List<WheelPulse>();
        h.Model.WheelObserved = pulses.Add;
        long t = Stopwatch.GetTimestamp();
        h.Capture.ObserveWheel(WheelAxis.Vertical, 120, 0, InputFlags.None, t);
        h.Capture.ObserveWheel(WheelAxis.Vertical, -15, 0, InputFlags.None, t);   // высокоточное колесо
        h.Capture.ObserveWheel(WheelAxis.Horizontal, 30, 0, InputFlags.None, t);
        h.Capture.ObserveWheel(WheelAxis.Horizontal, 0, 0, InputFlags.None, t);   // нулевое не публикуется
        h.Drain();

        Assert.Equal(new[] { (WheelAxis.Vertical, 120), (WheelAxis.Vertical, -15), (WheelAxis.Horizontal, 30) }, pulses.Select(p => (p.Axis, p.Delta)));
        Assert.Empty(h.Transitions);
        Assert.False(h.Model.HasNonUpKeys());
        Assert.Equal(3, h.Capture.WheelEvents);
        Assert.Equal(3, h.Model.Counters.WheelPulses);
        Assert.Equal(1, h.Capture.Ignored);
    }

    [Fact]
    public void Wheel_AndMiddleButton_AreIndependent()
    {
        var h = new Harness(ringCapacity: 16);
        var pulses = new List<WheelPulse>();
        h.Model.WheelObserved = pulses.Add;
        int middle = Slots.Mouse(MouseButton.Middle);
        h.Down(middle);
        h.Capture.ObserveWheel(WheelAxis.Vertical, -120, 0, InputFlags.None, Stopwatch.GetTimestamp());
        h.Up(middle);
        h.Capture.ObserveWheel(WheelAxis.Vertical, 120, 0, InputFlags.None, Stopwatch.GetTimestamp());
        h.Drain();

        Assert.Equal(new[] { (middle, KeyState.Down), (middle, KeyState.Up) }, h.Transitions.Select(t => (t.KeySlot, t.State)));
        Assert.Equal(new[] { -120, 120 }, pulses.Select(p => p.Delta));
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(middle));
    }

    [Fact]
    public void Wheel_FastSeries_OverflowResync_DoesNotRestoreWheelOrStickKeys()
    {
        var h = new Harness(ringCapacity: 4);
        var pulses = new List<WheelPulse>();
        h.Model.WheelObserved = pulses.Add;
        h.Down(A);
        for (int i = 0; i < 20; i++)
        {
            h.Capture.ObserveWheel(WheelAxis.Vertical, 120, 0, InputFlags.None, Stopwatch.GetTimestamp());
        }

        h.Up(A);
        Assert.True(h.Capture.DropEpoch > 0);
        h.Drain(); // находит потерю, снимок применяется при следующем опросе
        h.Drain();
        Assert.Equal(KeyState.Up, h.Model.AggregateOf(A));   // Up, потерянный в переполнении, восстановлен снимком
        Assert.False(h.Model.HasNonUpKeys());
        Assert.True(pulses.Count < 20);                      // потерянные импульсы не воспроизводятся
        Assert.All(pulses, p => Assert.Equal(120, p.Delta));
    }

    [Fact]
    public void Wheel_DoesNotTouchComparator()
    {
        var cmp = new EdgeComparator(TimeSpan.FromMilliseconds(200), 16);
        cmp.Observe(SourceKind.RawInput, new InputEvent(1, 0, InputKind.Wheel, 0, 0, 0, InputFlags.None, 120), TimeSpan.Zero);
        Assert.All(cmp.ToStats().Classes, c => Assert.Equal(0, c.RawOnly + c.Matched));
    }

    [Fact]
    public void ChordCounters_CountOnlyReliableModifierChords()
    {
        var h = new Harness(ringCapacity: 32);
        h.Down(A);
        h.Up(A);
        h.Down(LCtrl);
        h.Down(C);
        h.Up(C);
        h.Up(LCtrl);
        h.Down(Esc);
        h.Drain();
        Assert.Equal(1, h.Model.Counters.ModifierChords);
        Assert.Equal(0, h.Model.Counters.WinChords);
    }
}
