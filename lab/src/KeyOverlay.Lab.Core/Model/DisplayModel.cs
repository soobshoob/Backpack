using KeyOverlay.Lab.Core.Input;

namespace KeyOverlay.Lab.Core.Model;

public sealed class DisplayEntry
{
    internal DisplayEntry(long id, int keySlot, int order)
    {
        Id = id;
        KeySlot = keySlot;
        Label = KeyInfo.Label(keySlot);
        Modifier = KeyInfo.Modifier(keySlot);
        Order = order;
    }

    /// <summary>
    /// Идентичность появления: новая при каждом создании записи, не меняется, пока запись существует
    /// (повторное нажатие, отпускание, Unknown). Запись, удалённая по выдержке и созданная снова, получает новый Id.
    /// </summary>
    public long Id { get; }
    public int KeySlot { get; }
    public string Label { get; }
    public ModifierKind Modifier { get; }
    public KeyState State { get; internal set; } = KeyState.Down;
    public int PressCount { get; internal set; } = 1;
    public TimeSpan? ReleasedAt { get; internal set; }

    /// <summary>Время последнего нажатия (для порога одновременности).</summary>
    public TimeSpan DownAt { get; internal set; }
    internal int Order { get; }
}

/// <summary>
/// Минимальная модель отображения стенда (не дизайн KeyOverlay):
/// <list type="bullet">
/// <item>одна группа на экране;</item>
/// <item>новая клавиша добавляется к группе, если удерживается модификатор (или сама новая — модификатор при удержании другой);
///       иначе группа заменяется — быстрая печать с перекрытием не превращается в «сочетание»;</item>
/// <item>порядок: модификаторы Ctrl, Shift, Alt, Win, затем остальные в порядке нажатия;</item>
/// <item>каждая клавиша видна, пока нажата, и ещё <c>linger</c> после <b>своего</b> отпускания;</item>
/// <item>Unknown не считается удержанием: такая клавиша не формирует сочетание и уходит по выдержке.</item>
/// </list>
/// Время задаётся вызывающим (монотонные часы), без квантования таймером.
/// </summary>
/// <summary>
/// Что объединяется в одну группу. <see cref="Modifiers"/> — новая клавиша присоединяется, только если удерживается модификатор
/// (или сама она модификатор при удержании другой клавиши): спокойная печать не склеивается. <see cref="AnyHeld"/> — присоединяется
/// любая клавиша, нажатая, пока другая достоверно Down (A+B тоже группа), с порогом одновременности
/// (<see cref="DisplayModel.AnyHeldThreshold"/>), отделяющим сочетание от перекрытия нажатий при быстрой печати.
/// </summary>
public enum ChordRule { Modifiers, AnyHeld }

public sealed class DisplayModel
{
    private readonly List<DisplayEntry> _entries = new();
    private int _order;
    private long _nextId = 1;

    public DisplayModel(TimeSpan linger) => Linger = linger;

    public TimeSpan Linger { get; private set; }

    /// <summary>
    /// Правило объединения. Смена действует только на следующие нажатия: существующие записи не меняются и не возвращаются.
    /// Одновременность — пересечение достоверных Down (Unknown и отпущенные в выдержке не считаются удержанием).
    /// </summary>
    public ChordRule Rule { get; set; } = ChordRule.Modifiers;

    /// <summary>
    /// Порог одновременности для <see cref="ChordRule.AnyHeld"/>. Новая клавиша сразу присоединяется к удерживаемой, только если та
    /// удерживается не меньше порога. Если нажатия перекрылись быстрее (типично для печати: 20–80 мс), новая клавиша сначала
    /// заменяет группу, а прежняя удерживаемая откладывается; через порог от более позднего нажатия она возвращается в группу,
    /// только если обе клавиши всё ещё нажаты. При печати первая клавиша к этому моменту отпущена — склейки нет. 0 — без порога.
    /// Это время удержания, а не выдержка после отпускания.
    /// </summary>
    public TimeSpan AnyHeldThreshold { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Отложенные удерживаемые клавиши: вернуть в группу в <c>At</c>, если они и группа всё ещё нажаты.</summary>
    private readonly List<(DisplayEntry Entry, TimeSpan At)> _pending = new();

    /// <summary>Сколько клавиш ждёт решения о присоединении (для тестов).</summary>
    public int PendingCount => _pending.Count;

    public IReadOnlyList<DisplayEntry> Entries => _entries;

    public long Version { get; private set; }

    public TimeSpan? NextDeadline
    {
        get
        {
            TimeSpan? next = null;
            foreach (var e in _entries)
            {
                if (e.State != KeyState.Down && e.ReleasedAt is { } released)
                {
                    var deadline = released + Linger;
                    if (next is null || deadline < next)
                    {
                        next = deadline;
                    }
                }
            }

            foreach (var (_, at) in _pending)
            {
                if (next is null || at < next)
                {
                    next = at;
                }
            }

            return next;
        }
    }

    public bool OnTransition(KeyTransition t, TimeSpan now) => t.State switch
    {
        KeyState.Down => OnDown(t.KeySlot, now),
        _ => OnRelease(t.KeySlot, t.State, now),
    };

    public bool Tick(TimeSpan now)
    {
        bool changed = ResolvePending(now);
        int removed = _entries.RemoveAll(e => e.State != KeyState.Down && e.ReleasedAt is { } r && now - r >= Linger);
        if (removed > 0)
        {
            Version++;
        }

        return removed > 0 || changed;
    }

    /// <summary>Вернуть отложенные клавиши, если к сроку и они, и все клавиши группы всё ещё достоверно нажаты.</summary>
    private bool ResolvePending(TimeSpan now)
    {
        bool changed = false;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var (entry, at) = _pending[i];
            if (now < at)
            {
                continue;
            }

            _pending.RemoveAt(i);
            bool groupHeld = _entries.Count > 0 && _entries.All(e => e.State == KeyState.Down);
            if (Rule == ChordRule.AnyHeld && entry.State == KeyState.Down && groupHeld && !_entries.Any(e => e.KeySlot == entry.KeySlot))
            {
                _entries.Add(entry);
                _entries.Sort(Compare);
                Version++;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Новая выдержка применяется сразу ко всем уже отпущенным записям (отсчёт — от их собственного отпускания).
    /// Записи не пересоздаются; удаление истёкших — обычным <see cref="Tick"/>.
    /// </summary>
    public void SetLinger(TimeSpan linger)
    {
        if (linger < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(linger));
        }

        if (linger != Linger)
        {
            Linger = linger;
            Version++;
        }
    }

    public void Clear()
    {
        _entries.Clear();
        _pending.Clear();
        Version++;
    }

    /// <summary>
    /// Убрать всё, что уже отпущено (в выдержке) или Unknown; удерживаемые сейчас клавиши остаются. Используется при возврате
    /// скрытого отображения: старые нажатия не возвращаются, а то, что держится сейчас, видно сразу.
    /// </summary>
    public bool DropReleased()
    {
        int removed = _entries.RemoveAll(e => e.State != KeyState.Down);
        if (removed > 0)
        {
            Version++;
        }

        return removed > 0;
    }

    private bool OnDown(int keySlot, TimeSpan now)
    {
        var existing = _entries.Find(e => e.KeySlot == keySlot);
        if (existing is not null)
        {
            if (existing.State != KeyState.Down)
            {
                existing.State = KeyState.Down;
                existing.ReleasedAt = null;
                existing.DownAt = now;
                existing.PressCount++;
                Version++;
                return true;
            }

            return false;
        }

        _pending.RemoveAll(p => p.Entry.KeySlot == keySlot);
        var entry = new DisplayEntry(_nextId++, keySlot, _order++) { DownAt = now };
        bool heldModifier = _entries.Any(e => e.State == KeyState.Down && e.Modifier != ModifierKind.None);
        bool heldAny = _entries.Any(e => e.State == KeyState.Down);
        bool join;
        if (Rule == ChordRule.AnyHeld)
        {
            // Модификатор присоединяет сразу (как в обычном правиле); остальные — только после порога удержания.
            bool settled = _entries.Any(e => e.State == KeyState.Down && (now - e.DownAt >= AnyHeldThreshold || e.Modifier != ModifierKind.None));
            join = heldModifier || (heldAny && entry.Modifier != ModifierKind.None) || settled;
            if (!join && heldAny)
            {
                // Быстрое перекрытие: удерживаемые откладываются и вернутся, только если останутся нажатыми вместе с новой.
                foreach (var held in _entries.Where(e => e.State == KeyState.Down))
                {
                    _pending.Add((held, now + AnyHeldThreshold));
                }
            }
        }
        else
        {
            join = heldModifier || (heldAny && entry.Modifier != ModifierKind.None);
        }

        if (!join)
        {
            _entries.Clear();
        }

        _entries.Add(entry);
        _entries.Sort(Compare);
        Version++;
        return true;
    }

    private bool OnRelease(int keySlot, KeyState state, TimeSpan now)
    {
        foreach (var p in _pending.Where(p => p.Entry.KeySlot == keySlot).ToList())
        {
            p.Entry.State = state; // отпущена раньше срока — не вернётся (печать)
            _pending.Remove(p);
        }

        var existing = _entries.Find(e => e.KeySlot == keySlot);
        if (existing is null || existing.State == state)
        {
            return false;
        }

        existing.State = state;
        existing.ReleasedAt = now;
        Version++;
        return true;
    }

    private static int Compare(DisplayEntry a, DisplayEntry b)
    {
        bool am = a.Modifier != ModifierKind.None, bm = b.Modifier != ModifierKind.None;
        if (am != bm)
        {
            return am ? -1 : 1;
        }

        if (am && a.Modifier != b.Modifier)
        {
            return a.Modifier.CompareTo(b.Modifier);
        }

        return a.Order.CompareTo(b.Order);
    }
}
