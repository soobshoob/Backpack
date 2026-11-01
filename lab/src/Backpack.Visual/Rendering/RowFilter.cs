using Backpack.Visual.Settings;

namespace Backpack.Visual.Rendering;

/// <summary>
/// Фильтр <b>представления</b>: решает, какие записи модели отображения показать. Модель ввода и модель отображения
/// не меняются — отпускания по-прежнему принимаются, удерживаемые клавиши по-прежнему формируют сочетания.
/// <list type="bullet">
/// <item><b>Все клавиши</b> — всё, кроме кнопок мыши, если показ мыши в ряду выключен.</item>
/// <item><b>Только сочетания</b> — при правиле «с модификаторами» группа показывается, только если в ней есть модификатор
///       (Ctrl, Shift, Alt, Win) и хотя бы одна другая клавиша (в том числе кнопка мыши, если она показывается в ряду).
///       Одиночный модификатор и обычная печать не показываются. Shift + буква — тоже сочетание. При правиле «любые одновременные
///       клавиши» показывается любая группа из двух и более клавиш (A+B тоже).</item>
/// <item>При смене режима отпущенные записи, которые до смены не были видны, остаются скрытыми до своего исчезновения:
///       переключение не «возвращает» старые нажатия. Удерживаемые клавиши показываются сразу.</item>
/// </list>
/// </summary>
public sealed class RowFilter
{
    private readonly HashSet<long> _suppressed = new();
    private KeyFilter _mode;
    private ChordMode _chords;
    private bool _mouseInRow;
    private bool _configured;

    public KeyFilter Mode => _mode;

    public bool MouseInRow => _mouseInRow;

    public IReadOnlyCollection<long> Suppressed => _suppressed;

    /// <summary>Новый режим. <paramref name="current"/> — записи модели на момент смены.</summary>
    public void Configure(KeyFilter mode, bool mouseInRow, IReadOnlyList<KeycapItem> current, ChordMode chords = ChordMode.Modifiers)
    {
        if (_configured && mode == _mode && mouseInRow == _mouseInRow && chords == _chords)
        {
            return;
        }

        var visibleBefore = _configured ? Visible(current, _mode, _mouseInRow, _chords).Select(i => i.Id).ToHashSet() : null;
        _mode = mode;
        _mouseInRow = mouseInRow;
        _chords = chords;
        _configured = true;
        if (visibleBefore is null)
        {
            return;
        }

        foreach (var item in current)
        {
            if (!visibleBefore.Contains(item.Id) && !item.IsPressed)
            {
                _suppressed.Add(item.Id);
            }
        }
    }

    public IReadOnlyList<KeycapItem> Apply(IReadOnlyList<KeycapItem> items)
    {
        // Подавление действует, пока запись существует в модели; повторное нажатие той же записи снимает его.
        var present = new HashSet<long>();
        foreach (var i in items)
        {
            present.Add(i.Id);
            if (i.IsPressed)
            {
                _suppressed.Remove(i.Id);
            }
        }

        _suppressed.RemoveWhere(id => !present.Contains(id));
        return Visible(items, _mode, _mouseInRow, _chords).Where(i => !_suppressed.Contains(i.Id)).ToList();
    }

    public static IReadOnlyList<KeycapItem> Visible(IReadOnlyList<KeycapItem> items, KeyFilter mode, bool mouseInRow, ChordMode chords = ChordMode.Modifiers)
    {
        var shown = mouseInRow ? items : items.Where(i => !i.IsMouse).ToList();
        if (mode == KeyFilter.All)
        {
            return shown;
        }

        if (chords == ChordMode.AnyHeld)
        {
            return shown.Count >= 2 ? shown : [];
        }

        bool hasModifier = shown.Any(i => i.IsModifier);
        bool hasOther = shown.Any(i => !i.IsModifier);
        return hasModifier && hasOther ? shown : [];
    }
}
