using Backpack.Core.Input;

namespace Backpack.Core.Transport;

/// <summary>
/// Ограниченное кольцо «один производитель — один потребитель» без блокировок.
/// Производитель никогда не ждёт: при заполнении запись отклоняется.
/// </summary>
public sealed class EventRing
{
    private readonly InputEvent[] _items;
    private readonly int _mask;
    private long _head; // читает потребитель
    private long _tail; // пишет производитель

    public EventRing(int capacityPowerOfTwo)
    {
        if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityPowerOfTwo), "Ёмкость должна быть степенью двойки ≥ 2.");
        }

        _items = new InputEvent[capacityPowerOfTwo];
        _mask = capacityPowerOfTwo - 1;
    }

    public int Capacity => _items.Length;

    /// <summary>Приблизительная глубина (для диагностики).</summary>
    public int Count => (int)Math.Max(0, Volatile.Read(ref _tail) - Volatile.Read(ref _head));

    /// <summary>Только поток производителя.</summary>
    public bool TryWrite(in InputEvent item)
    {
        long tail = _tail;
        if (tail - Volatile.Read(ref _head) >= _items.Length)
        {
            return false;
        }

        _items[tail & _mask] = item;
        Volatile.Write(ref _tail, tail + 1);
        return true;
    }

    /// <summary>Только поток потребителя.</summary>
    public bool TryRead(out InputEvent item)
    {
        long head = _head;
        if (head >= Volatile.Read(ref _tail))
        {
            item = default;
            return false;
        }

        item = _items[head & _mask];
        Volatile.Write(ref _head, head + 1);
        return true;
    }
}
