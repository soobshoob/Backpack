using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Transport;

namespace KeyOverlay.Lab.Core.Model;

public enum KeyState : byte { Up = 0, Down = 1, Unknown = 2 }

/// <summary>Почему слот находится в текущем состоянии (для диагностики; наружу — только агрегаты).</summary>
public enum StateReason : byte
{
    Initial,
    Event,
    Snapshot,
    SessionReset,
    DesktopSwitch,
    PowerResume,
    StaleNoRepeat,
    ReconciledUp,
    ReconciledDown,
    DeviceRemoved,
    ManualReset,
}

/// <summary>Изменение агрегированного по устройствам состояния физической клавиши.</summary>
/// <summary>
/// Изменение состояния клавиши или кнопки. <see cref="At"/> — время захвата исходного события (Stopwatch); 0 — время неизвестно
/// (снимок, сверка, сброс): такой переход относится к текущему контексту фокуса.
/// </summary>
public readonly record struct KeyTransition(int KeySlot, KeyState State, long At = 0);

/// <summary>Импульс колеса (только в памяти): ось и знаковое смещение. Не состояние и не переход клавиши.</summary>
public readonly record struct WheelPulse(WheelAxis Axis, int Delta, long CaptureTimestamp);

/// <summary>
/// Состояние одного источника на стороне потребителя (UI-поток). У каждого источника — своя модель.
///
/// Протокол согласования с <see cref="CaptureState"/>:
/// <list type="bullet">
/// <item>у каждого слота есть <c>asOf</c> — номер последовательности источника, на момент которого известно состояние;</item>
/// <item>событие с номером ≤ <see cref="LastAppliedSeq"/> уже учтено (снимком) и повторно не применяется;</item>
/// <item>снимок меняет слот только если источник наблюдал по нему что-то <b>новее</b> <c>asOf</c>
/// (поэтому поздний старый снимок не отменяет сверку или сброс, и старый Down не возвращается);</item>
/// <item>сброс и сверка ставят <c>asOf = LastAppliedSeq</c>: всё, что источник наблюдал до этого, считается учтённым.</item>
/// </list>
/// </summary>
public sealed class SourceModel
{
    private readonly KeyState[] _state = new KeyState[Slots.Total];
    private readonly long[] _asOf = new long[Slots.Total];
    private readonly ushort[] _vk = new ushort[Slots.Total];
    private readonly long[] _lastActivity = new long[Slots.Total]; // Stopwatch timestamp последнего Down/Up/повтора
    private readonly StateReason[] _reason = new StateReason[Slots.Total];
    private readonly KeyState[] _aggregate = new KeyState[Slots.PerDevice];
    private int _lastDownIndex = -1;

    public SourceModel(SourceKind kind) => Kind = kind;

    public SourceKind Kind { get; }

    public long LastAppliedSeq { get; private set; }

    public ModelCounters Counters { get; } = new();

    /// <summary>
    /// Получатель импульсов колеса (задаёт потребитель основного источника). Импульсы не хранятся в модели:
    /// без получателя они только считаются.
    /// </summary>
    public Action<WheelPulse>? WheelObserved { get; set; }

    public KeyState StateOf(int device, int slot) => _state[Slots.Index(device, slot)];

    public StateReason ReasonOf(int device, int slot) => _reason[Slots.Index(device, slot)];

    public KeyState AggregateOf(int slot) => _aggregate[slot];

    public int CountInState(KeyState state)
    {
        int n = 0;
        foreach (var s in _state)
        {
            if (s == state)
            {
                n++;
            }
        }

        return n;
    }

    public bool HasNonUpKeys()
    {
        foreach (var s in _state)
        {
            if (s != KeyState.Up)
            {
                return true;
            }
        }

        return false;
    }

    // ── события ───────────────────────────────────────────────────────────────

    public void ApplyEvent(in InputEvent e, long now, List<KeyTransition> output)
    {
        if (e.Seq <= LastAppliedSeq)
        {
            Counters.DuplicatesSkipped++;
            return;
        }

        _eventAt = e.CaptureTimestamp;
        try
        {
            ApplyEventCore(e, now, output);
        }
        finally
        {
            _eventAt = 0;
        }
    }

    /// <summary>Время захвата события, которое сейчас применяется (переходы получают его в <see cref="KeyTransition.At"/>).</summary>
    private long _eventAt;

    private void ApplyEventCore(in InputEvent e, long now, List<KeyTransition> output)
    {
        LastAppliedSeq = e.Seq;
        Counters.EventsApplied++;

        if (e.Kind == InputKind.Wheel)
        {
            // Колесо не меняет состояние клавиш и кнопок: средняя кнопка и прокрутка — разные события.
            Counters.WheelPulses++;
            WheelObserved?.Invoke(new WheelPulse((WheelAxis)e.Slot, e.Delta, e.CaptureTimestamp));
            return;
        }

        if (e.Kind == InputKind.DeviceRemoved)
        {
            int start = Slots.Index(e.Device, 0);
            for (int i = start; i < start + Slots.PerDevice; i++)
            {
                if (_state[i] != KeyState.Up)
                {
                    Counters.DeviceRemovedReleases++;
                    Set(i, KeyState.Up, e.Seq, StateReason.DeviceRemoved, output);
                }
            }

            return;
        }

        int index = Slots.Index(e.Device, e.Slot);
        _vk[index] = e.Vk;
        _lastActivity[index] = now;

        if (e.Kind == InputKind.Down)
        {
            _lastDownIndex = index;
            if (_state[index] == KeyState.Unknown)
            {
                Counters.UnknownResolvedByEvent++;
            }
            else if (_state[index] == KeyState.Down && (e.Flags & InputFlags.Repeat) == 0)
            {
                // Источник считал клавишу отпущенной, а модель — нажатой (например, после сверки «Down»).
                Counters.DownWhileDown++;
            }

            Set(index, KeyState.Down, e.Seq, StateReason.Event, output);
        }
        else
        {
            if (_state[index] == KeyState.Unknown)
            {
                Counters.UnknownResolvedByEvent++;
            }

            Set(index, KeyState.Up, e.Seq, StateReason.Event, output);
        }
    }

    // ── ресинхронизация ───────────────────────────────────────────────────────

    public void ApplySnapshot(SourceSnapshot snapshot, List<KeyTransition> output)
    {
        Counters.SnapshotsApplied++;
        for (int i = 0; i < Slots.Total; i++)
        {
            bool observedDown = snapshot.Down[i];
            if (_state[i] == KeyState.Up && !observedDown)
            {
                continue;
            }

            if (snapshot.LastSeq[i] <= _asOf[i])
            {
                // Источник не наблюдал по слоту ничего новее того, что уже учтено моделью
                // (в том числе после сброса или сверки) — снимок не меняет состояние.
                continue;
            }

            var target = observedDown ? KeyState.Down : KeyState.Up;
            _vk[i] = snapshot.LastVk[i];
            if (_state[i] != target)
            {
                Counters.SnapshotCorrections++;
            }

            Set(i, target, snapshot.Seq, StateReason.Snapshot, output);
        }

        if (snapshot.Seq > LastAppliedSeq)
        {
            LastAppliedSeq = snapshot.Seq;
        }
    }

    // ── потеря контекста ──────────────────────────────────────────────────────

    /// <summary>
    /// Перевести все нажатые слоты в Unknown (блокировка/разблокировка, смена рабочего стола, пробуждение).
    /// Не утверждает отпускание: клавиша могла остаться нажатой.
    /// </summary>
    public void MarkAllUnknown(StateReason reason, List<KeyTransition> output)
    {
        for (int i = 0; i < Slots.Total; i++)
        {
            if (_state[i] == KeyState.Down)
            {
                Counters.MarkedUnknown++;
                Set(i, KeyState.Unknown, LastAppliedSeq, reason, output);
            }
        }
    }

    /// <summary>
    /// Эвристика (не гарантия Win32): автоповтор есть только у последней нажатой клавиши. Если она «нажата»,
    /// но ни повторов, ни других событий по ней нет дольше <paramref name="repeatWindowTicks"/>, — вероятно,
    /// потерян Up. Клавиша переводится в Unknown, а не в Up.
    /// </summary>
    public void CheckStaleRepeat(long now, long repeatWindowTicks, List<KeyTransition> output)
    {
        int i = _lastDownIndex;
        if (i < 0 || _state[i] != KeyState.Down || Slots.IsMouse(i % Slots.PerDevice))
        {
            return;
        }

        if (now - _lastActivity[i] > repeatWindowTicks)
        {
            Counters.StaleNoRepeat++;
            Set(i, KeyState.Unknown, LastAppliedSeq, StateReason.StaleNoRepeat, output);
        }
    }

    public void ResetAll(List<KeyTransition> output)
    {
        for (int i = 0; i < Slots.Total; i++)
        {
            if (_state[i] != KeyState.Up)
            {
                Set(i, KeyState.Up, LastAppliedSeq, StateReason.ManualReset, output);
            }
        }
    }

    // ── сверка ────────────────────────────────────────────────────────────────

    internal IEnumerable<ReconcileCandidate> NonUpSlots()
    {
        for (int i = 0; i < Slots.Total; i++)
        {
            if (_state[i] != KeyState.Up)
            {
                int slot = i % Slots.PerDevice;
                yield return new ReconcileCandidate(i, _state[i], KeyInfo.ProbeVk(slot, _vk[i]), _lastActivity[i]);
            }
        }
    }

    internal void ApplyReconcile(int index, KeyState target, List<KeyTransition> output)
    {
        if (_state[index] == target)
        {
            return;
        }

        if (target == KeyState.Up)
        {
            Counters.ReconciledUp++;
        }
        else
        {
            Counters.ReconciledDown++;
        }

        Set(index, target, LastAppliedSeq, target == KeyState.Up ? StateReason.ReconciledUp : StateReason.ReconciledDown, output);
    }

    private void Set(int index, KeyState state, long asOf, StateReason reason, List<KeyTransition> output)
    {
        _state[index] = state;
        _asOf[index] = asOf;
        _reason[index] = reason;

        int slot = index % Slots.PerDevice;
        var aggregate = KeyState.Up;
        for (int d = 0; d < Slots.MaxDevices; d++)
        {
            var s = _state[Slots.Index(d, slot)];
            if (s == KeyState.Down)
            {
                aggregate = KeyState.Down;
                break;
            }

            if (s == KeyState.Unknown)
            {
                aggregate = KeyState.Unknown;
            }
        }

        if (_aggregate[slot] != aggregate)
        {
            if (aggregate == KeyState.Down && reason == StateReason.Event)
            {
                CountChord(slot);
            }

            _aggregate[slot] = aggregate;
            output.Add(new KeyTransition(slot, aggregate, _eventAt));
        }
    }

    private static readonly int[] ModifierSlots = Enumerable.Range(0, Slots.KeySlots).Where(s => KeyInfo.Modifier(s) != ModifierKind.None).ToArray();

    /// <summary>
    /// Агрегированная диагностика получения сочетаний (без клавиш и порядка): новая не-модификаторная клавиша или кнопка
    /// стала Down по событию, пока модификатор достоверно Down. Отдельно — когда среди удерживаемых есть Win.
    /// В режиме compare расхождение между источниками отделяет «событие не получено» от «получено, но не показано».
    /// </summary>
    private void CountChord(int slot)
    {
        if (KeyInfo.Modifier(slot) != ModifierKind.None)
        {
            return;
        }

        bool any = false, win = false;
        foreach (int m in ModifierSlots)
        {
            if (_aggregate[m] == KeyState.Down)
            {
                any = true;
                win |= KeyInfo.Modifier(m) == ModifierKind.Win;
            }
        }

        if (any)
        {
            Counters.ModifierChords++;
        }

        if (win)
        {
            Counters.WinChords++;
        }
    }
}

internal readonly record struct ReconcileCandidate(int Index, KeyState State, ushort ProbeVk, long LastActivity);

public sealed class ModelCounters
{
    public long EventsApplied { get; set; }
    public long DuplicatesSkipped { get; set; }
    public long DownWhileDown { get; set; }
    public long SnapshotsApplied { get; set; }
    public long SnapshotCorrections { get; set; }
    public long MarkedUnknown { get; set; }
    public long UnknownResolvedByEvent { get; set; }
    public long StaleNoRepeat { get; set; }
    public long ReconciledUp { get; set; }
    public long ReconciledDown { get; set; }
    public long DeviceRemovedReleases { get; set; }
    public long WheelPulses { get; set; }
    public long ModifierChords { get; set; }
    public long WinChords { get; set; }
}
