using Backpack.Core.Input;

namespace Backpack.Core.Transport;

/// <summary>
/// Снимок таблицы источника. Отражает <b>наблюдавшиеся</b> источником события до номера <see cref="Seq"/>
/// включительно, а не гарантированное физическое состояние клавиатуры.
/// </summary>
public sealed class SourceSnapshot
{
    internal SourceSnapshot(long seq, long dropEpoch, bool[] down, long[] lastSeq, ushort[] lastVk)
    {
        Seq = seq;
        DropEpoch = dropEpoch;
        Down = down;
        LastSeq = lastSeq;
        LastVk = lastVk;
    }

    public long Seq { get; }

    /// <summary>Сколько отбрасываний было учтено к моменту снимка.</summary>
    public long DropEpoch { get; }

    internal bool[] Down { get; }
    internal long[] LastSeq { get; }
    internal ushort[] LastVk { get; }
}

/// <summary>
/// Сторона захвата. <b>Всеми</b> методами, кроме чтения счётчиков, пользуется только поток источника
/// (в LL-режиме — тот же поток, где вызывается callback хука; там же строится снимок).
/// Поэтому таблице не нужна синхронизация: снимок строится между событиями, атомарно относительно них.
/// Работа на событие — O(1); построение снимка — O(<see cref="Slots.Total"/>), фиксированный объём.
/// </summary>
public sealed class CaptureState
{
    private readonly bool[] _down = new bool[Slots.Total];
    private readonly long[] _lastSeq = new long[Slots.Total];
    private readonly ushort[] _lastVk = new ushort[Slots.Total];
    private readonly Action? _signal;

    private long _seq;
    private long _dropEpoch;
    private int _signalPending;
    private SourceSnapshot? _published;

    // Счётчики: пишет поток захвата, читает UI (Volatile/Interlocked).
    private long _events;
    private long _repeats;
    private long _ignored;
    private long _mouseMoves;
    private long _wheelNotches;
    private long _devicesRemoved;
    private long _snapshots;

    public CaptureState(SourceKind kind, int ringCapacity, Action? signal)
    {
        Kind = kind;
        Ring = new EventRing(ringCapacity);
        _signal = signal;
    }

    public SourceKind Kind { get; }

    public EventRing Ring { get; }

    public long LastSeq => Volatile.Read(ref _seq);
    public long DropEpoch => Volatile.Read(ref _dropEpoch);
    public long EventsTotal => Volatile.Read(ref _events);
    public long Repeats => Volatile.Read(ref _repeats);
    public long Ignored => Volatile.Read(ref _ignored);
    public long MouseMoves => Volatile.Read(ref _mouseMoves);
    public long WheelNotches => Volatile.Read(ref _wheelNotches);
    public long DevicesRemoved => Volatile.Read(ref _devicesRemoved);
    public long SnapshotsCreated => Volatile.Read(ref _snapshots);

    /// <summary>Время (Stopwatch) последнего события клавиатуры/кнопок; 0 — не было.</summary>
    public long LastEventTimestamp => Volatile.Read(ref _lastEventTimestamp);
    private long _lastEventTimestamp;

    // ── поток захвата ─────────────────────────────────────────────────────────

    public void Observe(InputKind kind, int slot, int device, ushort vk, InputFlags flags, long timestamp)
    {
        if (kind == InputKind.DeviceRemoved || (uint)slot >= Slots.PerDevice || (uint)device >= Slots.MaxDevices)
        {
            Interlocked.Increment(ref _ignored);
            return;
        }

        int index = Slots.Index(device, slot);
        bool wasDown = _down[index];
        if (kind == InputKind.Down && wasDown)
        {
            flags |= InputFlags.Repeat;
            Interlocked.Increment(ref _repeats);
        }

        long seq = _seq + 1;
        _down[index] = kind == InputKind.Down;
        _lastSeq[index] = seq;
        _lastVk[index] = vk;
        Volatile.Write(ref _seq, seq);
        Interlocked.Increment(ref _events);
        Volatile.Write(ref _lastEventTimestamp, timestamp);

        Publish(new InputEvent(seq, timestamp, kind, (ushort)slot, (byte)device, vk, flags));
    }

    public void RemoveDevice(int device, long timestamp)
    {
        if ((uint)device >= Slots.MaxDevices)
        {
            Interlocked.Increment(ref _ignored);
            return;
        }

        long seq = _seq + 1;
        int start = Slots.Index(device, 0);
        for (int i = start; i < start + Slots.PerDevice; i++)
        {
            if (_down[i])
            {
                _down[i] = false;
                _lastSeq[i] = seq;
            }
        }

        Volatile.Write(ref _seq, seq);
        Interlocked.Increment(ref _devicesRemoved);
        Publish(new InputEvent(seq, timestamp, InputKind.DeviceRemoved, 0, (byte)device, 0, InputFlags.None));
    }

    public void CountIgnored() => Interlocked.Increment(ref _ignored);

    public void CountMouseMove() => Interlocked.Increment(ref _mouseMoves);

    /// <summary>Только счётчик щелчков колеса (без события в кольце).</summary>
    public void AddWheel(int notches) => Interlocked.Add(ref _wheelNotches, Math.Abs(notches));

    /// <summary>
    /// Импульс колеса: событие с номером последовательности в кольце, без изменения таблицы удержания — колесо не бывает
    /// «нажатым», и снимок его не восстанавливает. Нулевое смещение не публикуется. Потеря при переполнении кольца
    /// учитывается эпохой, как у клавиш; потерянный импульс не воспроизводится снимком (он не состояние).
    /// </summary>
    public void ObserveWheel(WheelAxis axis, int delta, int device, InputFlags flags, long timestamp)
    {
        if (delta == 0 || (uint)device >= Slots.MaxDevices)
        {
            Interlocked.Increment(ref _ignored);
            return;
        }

        Interlocked.Add(ref _wheelNotches, Math.Max(1, Math.Abs(delta) / 120));
        short clamped = (short)Math.Clamp(delta, short.MinValue, short.MaxValue);
        long seq = _seq + 1;
        Volatile.Write(ref _seq, seq);
        Interlocked.Increment(ref _wheelEvents);
        Publish(new InputEvent(seq, timestamp, InputKind.Wheel, (ushort)axis, (byte)device, 0, flags, clamped));
    }

    public long WheelEvents => Volatile.Read(ref _wheelEvents);

    private long _wheelEvents;

    /// <summary>
    /// Построить снимок и опубликовать его для потребителя. Вызывается в потоке захвата по запросу потребителя,
    /// поэтому все события с номером ≤ <see cref="SourceSnapshot.Seq"/> уже в таблице, а все последующие — в кольце.
    /// </summary>
    public SourceSnapshot CreateAndPublishSnapshot()
    {
        var snapshot = new SourceSnapshot(_seq, Volatile.Read(ref _dropEpoch), (bool[])_down.Clone(), (long[])_lastSeq.Clone(), (ushort[])_lastVk.Clone());
        Interlocked.Increment(ref _snapshots);
        Volatile.Write(ref _published, snapshot);
        RaiseSignal();
        return snapshot;
    }

    private void Publish(in InputEvent e)
    {
        if (!Ring.TryWrite(e))
        {
            // Потеря фиксируется ростом эпохи. Даже если это было последнее событие и новых не будет,
            // потребитель увидит эпоху при следующем опросе (сигнал + резервный таймер).
            Volatile.Write(ref _dropEpoch, _dropEpoch + 1);
        }

        RaiseSignal();
    }

    private void RaiseSignal()
    {
        // Не более одного необработанного сигнала: производитель не наводняет очередь UI.
        if (_signal is not null && Interlocked.Exchange(ref _signalPending, 1) == 0)
        {
            _signal();
        }
    }

    // ── поток потребителя ─────────────────────────────────────────────────────

    /// <summary>Снять флаг сигнала перед вычиткой (потребитель).</summary>
    public void AcknowledgeSignal() => Volatile.Write(ref _signalPending, 0);

    /// <summary>Забрать опубликованный снимок, если он есть (потребитель).</summary>
    public SourceSnapshot? TakePublishedSnapshot() => Interlocked.Exchange(ref _published, null);
}
