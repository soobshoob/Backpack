using KeyOverlay.Lab.Core.Input;

namespace KeyOverlay.Lab.Core.Diagnostics;

public enum EdgeClass { Modifier, Other, Mouse, Synthetic, Injected }

/// <summary>
/// Сравнение событий двух источников в режиме compare. Коды клавиш живут только в памяти, в окне сопоставления,
/// наружу выходят лишь счётчики по классам. Это наблюдение: расхождение само по себе не доказывает отказ источника.
/// </summary>
public sealed class EdgeComparator
{
    private readonly TimeSpan _window;
    private readonly int _maxPending;
    private readonly List<Pending>[] _pending = [new(), new()];
    private readonly long[,] _matched = new long[2, 5];
    private readonly long[,] _only = new long[2, 5];

    public EdgeComparator(TimeSpan window, int maxPending)
    {
        _window = window;
        _maxPending = maxPending;
    }

    public long PendingOverflow { get; private set; }

    public static EdgeClass Classify(int slot, InputFlags flags)
    {
        if ((flags & InputFlags.Injected) != 0)
        {
            return EdgeClass.Injected;
        }

        if (Slots.IsMouse(slot))
        {
            return EdgeClass.Mouse;
        }

        if (PhysicalKey.FromSlot(slot).Prefix == ScanPrefix.Synthetic)
        {
            return EdgeClass.Synthetic;
        }

        return KeyInfo.Modifier(slot) != ModifierKind.None ? EdgeClass.Modifier : EdgeClass.Other;
    }

    public void Observe(SourceKind source, in InputEvent e, TimeSpan now)
    {
        if (e.Kind is InputKind.DeviceRemoved or InputKind.Wheel || (e.Flags & InputFlags.Repeat) != 0)
        {
            return; // сравниваются только переходы; автоповтор у источников устроен по-разному
        }

        Flush(now);
        int self = (int)source, other = 1 - self;
        ushort slot = e.Slot;
        var cls = Classify(slot, e.Flags);
        bool down = e.Kind == InputKind.Down;

        var list = _pending[other];
        int match = list.FindIndex(p => p.Slot == slot && p.Down == down);
        if (match >= 0)
        {
            list.RemoveAt(match);
            _matched[self, (int)cls]++;
            return;
        }

        var mine = _pending[self];
        if (mine.Count >= _maxPending)
        {
            mine.RemoveAt(0);
            PendingOverflow++;
        }

        mine.Add(new Pending(slot, down, cls, now));
    }

    public void Flush(TimeSpan now)
    {
        for (int s = 0; s < 2; s++)
        {
            var list = _pending[s];
            int expired = 0;
            while (expired < list.Count && now - list[expired].At > _window)
            {
                _only[s, (int)list[expired].Class]++;
                expired++;
            }

            list.RemoveRange(0, expired);
        }
    }

    public ComparatorStats ToStats()
    {
        var classes = new List<ComparatorClassStats>();
        foreach (EdgeClass c in Enum.GetValues<EdgeClass>())
        {
            classes.Add(new ComparatorClassStats(
                c.ToString(),
                _matched[0, (int)c] + _matched[1, (int)c],
                _only[(int)SourceKind.RawInput, (int)c],
                _only[(int)SourceKind.LowLevelHook, (int)c]));
        }

        return new ComparatorStats(_window.TotalMilliseconds, PendingOverflow, classes);
    }

    private readonly record struct Pending(ushort Slot, bool Down, EdgeClass Class, TimeSpan At);
}
