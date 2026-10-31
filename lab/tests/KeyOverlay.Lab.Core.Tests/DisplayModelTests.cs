using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;
using static KeyOverlay.Lab.Core.Tests.Harness;

namespace KeyOverlay.Lab.Core.Tests;

public class DisplayModelTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static void Press(DisplayModel d, int slot, double t) => d.OnTransition(new KeyTransition(slot, KeyState.Down), S(t));

    private static void Release(DisplayModel d, int slot, double t) => d.OnTransition(new KeyTransition(slot, KeyState.Up), S(t));

    [Fact]
    public void LongHold_StaysVisible_AndEachKeyLingersFromItsOwnRelease()
    {
        var d = new DisplayModel(S(2));
        Press(d, LCtrl, 0);
        Assert.False(d.Tick(S(10)));                // удержание 10 с — ничего не исчезает
        Press(d, C, 10);
        Release(d, C, 10.1);
        Release(d, LCtrl, 10.2);

        d.Tick(S(12.15));
        Assert.Equal(new[] { LCtrl }, d.Entries.Select(e => e.KeySlot)); // C ушла (10.1 + 2)
        // Первая клавиша группы держится от собственного отпускания (в отличие от дефекта Keyviz с индексом 0).
        d.Tick(S(12.2));
        Assert.Empty(d.Entries);
    }

    [Fact]
    public void NextDeadline_PointsToEarliestExpiry()
    {
        var d = new DisplayModel(S(2));
        Press(d, LCtrl, 0);
        Press(d, C, 0.1);
        Release(d, C, 0.5);
        Assert.Equal(S(2.5), d.NextDeadline);
        Release(d, LCtrl, 0.3);
        Assert.Equal(S(2.3), d.NextDeadline);
    }

    [Fact]
    public void Chord_ModifiersFirstInCanonicalOrder()
    {
        var d = new DisplayModel(S(2));
        Press(d, LShift, 0);
        Press(d, LCtrl, 0.1);
        Press(d, C, 0.2);
        Assert.Equal(new[] { "LCtrl", "LShift", "C" }, d.Entries.Select(e => e.Label));
    }

    [Fact]
    public void OverlappingLetters_DoNotMergeIntoChord()
    {
        var d = new DisplayModel(S(2));
        Press(d, A, 0);
        Press(d, B, 0.05); // A ещё не отпущена
        Assert.Equal(new[] { B }, d.Entries.Select(e => e.KeySlot));
    }

    [Fact]
    public void RepeatedPressWithinChord_IncrementsCount()
    {
        var d = new DisplayModel(S(2));
        Press(d, LCtrl, 0);
        Press(d, C, 0.1);
        Release(d, C, 0.2);
        Press(d, C, 0.3);
        var c = d.Entries.Single(e => e.KeySlot == C);
        Assert.Equal(2, c.PressCount);
        Assert.Equal(KeyState.Down, c.State);
    }

    [Fact]
    public void NewKeyAfterReleasedGroup_ReplacesGroup()
    {
        var d = new DisplayModel(S(2));
        Press(d, LCtrl, 0);
        Press(d, C, 0.1);
        Release(d, C, 0.2);
        Release(d, LCtrl, 0.3);
        Press(d, B, 0.5);
        Assert.Equal(new[] { B }, d.Entries.Select(e => e.KeySlot));
    }

    [Fact]
    public void EntryId_IsStableWhileEntryLives_AndNewAfterExpiry()
    {
        var d = new DisplayModel(S(2));
        Press(d, A, 0);
        long first = d.Entries.Single().Id;
        Release(d, A, 0.1);
        Press(d, A, 0.5);                            // повтор в пределах выдержки — та же запись
        Assert.Equal(first, d.Entries.Single().Id);
        Assert.Equal(2, d.Entries.Single().PressCount);
        Release(d, A, 0.6);
        d.Tick(S(2.6));
        Assert.Empty(d.Entries);
        Press(d, A, 3);                              // после удаления — новое появление, новый Id
        Assert.NotEqual(first, d.Entries.Single().Id);
        Assert.Equal(1, d.Entries.Single().PressCount);
    }

    [Fact]
    public void EntryIds_AreUniqueAcrossReplacement()
    {
        var d = new DisplayModel(S(2));
        Press(d, A, 0);
        long a = d.Entries.Single().Id;
        Press(d, B, 0.05);                           // замена группы
        long b = d.Entries.Single().Id;
        Press(d, A, 0.1);                            // A снова (B ещё нажата, не модификатор) — снова замена
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, d.Entries.Single().Id);
        Assert.NotEqual(b, d.Entries.Single().Id);
    }

    [Fact]
    public void SetLinger_AppliesToAlreadyReleasedKeys_WithoutRecreatingEntries()
    {
        var d = new DisplayModel(S(5));
        Press(d, LCtrl, 0);
        Press(d, C, 0.1);
        Release(d, C, 0.2);
        long ctrlId = d.Entries.Single(e => e.KeySlot == LCtrl).Id;
        Assert.Equal(S(5.2), d.NextDeadline);

        d.SetLinger(S(1));
        Assert.Equal(S(1.2), d.NextDeadline);        // отсчёт — от собственного отпускания C
        d.Tick(S(1.25));
        Assert.Equal(new[] { LCtrl }, d.Entries.Select(e => e.KeySlot)); // удержанная клавиша не тронута
        Assert.Equal(ctrlId, d.Entries.Single().Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.SetLinger(S(-1)));
    }

    [Fact]
    public void CapText_KeepsSidesAndNumpadDistinguishable()
    {
        Assert.Equal(new KeyCapText("Ctrl", "L"), KeyInfo.CapText(LCtrl));
        Assert.Equal(new KeyCapText("Ctrl", "R"), KeyInfo.CapText(RCtrl));
        Assert.Equal(new KeyCapText("Enter", null), KeyInfo.CapText(Enter));
        Assert.Equal(new KeyCapText("Enter", "Num"), KeyInfo.CapText(NumEnter));
        Assert.Equal(new KeyCapText("A", null), KeyInfo.CapText(A));
        Assert.Equal(new KeyCapText("X1", "Mouse"), KeyInfo.CapText(Slots.Mouse(MouseButton.X1)));
        Assert.StartsWith("SC ", KeyInfo.CapText(0x7E).Main); // неизвестная клавиша — подпись, а не исключение
    }

    [Fact]
    public void Semantic_GivesLayoutIndependentKeyNames_WithSideAndNumpad()
    {
        Assert.Equal(new KeySemantic("Ctrl", 'L', false), KeyInfo.Semantic(LCtrl));
        Assert.Equal(new KeySemantic("Ctrl", 'R', false), KeyInfo.Semantic(RCtrl));
        Assert.Equal(new KeySemantic("Enter", ' ', false), KeyInfo.Semantic(Enter));
        Assert.Equal(new KeySemantic("Enter", ' ', true), KeyInfo.Semantic(NumEnter));
        Assert.Equal(new KeySemantic("A", ' ', false), KeyInfo.Semantic(A));
        Assert.Equal(new KeySemantic("Space", ' ', false), KeyInfo.Semantic(0x39));
        Assert.Equal(new KeySemantic("ArrowUp", ' ', false), KeyInfo.Semantic(new PhysicalKey(ScanPrefix.E0, 0x48).Slot));
        Assert.Equal(new KeySemantic("Delete", ' ', false), KeyInfo.Semantic(new PhysicalKey(ScanPrefix.E0, 0x53).Slot));
        Assert.Equal(new KeySemantic("Win", 'L', false), KeyInfo.Semantic(new PhysicalKey(ScanPrefix.E0, 0x5B).Slot));
        Assert.Equal(new KeySemantic("7", ' ', true), KeyInfo.Semantic(0x47));
        Assert.Equal(new KeySemantic("MouseX1", ' ', false), KeyInfo.Semantic(Slots.Mouse(MouseButton.X1)));
        Assert.StartsWith("SC ", KeyInfo.Semantic(0x7E).Key);
    }

    [Fact]
    public void UnknownKey_ExpiresLikeRelease()
    {
        var d = new DisplayModel(S(2));
        Press(d, A, 0);
        d.OnTransition(new KeyTransition(A, KeyState.Unknown), S(1));
        Assert.Equal(KeyState.Unknown, d.Entries.Single().State);
        d.Tick(S(3));
        Assert.Empty(d.Entries);
    }
}
