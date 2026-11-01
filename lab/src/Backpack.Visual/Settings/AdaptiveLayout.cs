using System.Windows;
using System.Windows.Controls;

namespace Backpack.Visual.Settings;

/// <summary>
/// Строка настройки (Phase 4F): подпись с пояснением и элемент управления. Если рядом с элементом (в его естественной ширине)
/// остаётся не меньше <see cref="LabelMinWidth"/> для подписи — элемент справа; иначе элемент переносится под подпись и получает
/// всю ширину строки (сегменты, кнопки и ползунок при этом сами переносятся или сужаются). Ничего не обрезается и не выходит
/// за границы карточки; переключение происходит в обычном проходе раскладки, состояние элементов не теряется.
/// Дочерние элементы: [0] — подпись, [1] — элемент управления.
/// </summary>
public sealed class AdaptiveRow : Panel
{
    public const double Gap = 16, StackGap = 8;

    /// <summary>Наименьшая ширина подписи, при которой элемент ещё стоит справа.</summary>
    public double LabelMinWidth { get; set; } = 200;

    /// <summary>Элемент перенесён под подпись (последняя раскладка).</summary>
    public bool IsStacked { get; private set; }

    private UIElement? Label => InternalChildren.Count > 0 ? InternalChildren[0] : null;

    private UIElement? Control => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    private double _controlWidth;

    protected override Size MeasureOverride(Size available)
    {
        var label = Label;
        var control = Control;
        if (label is null)
        {
            return default;
        }

        if (control is null || control.Visibility == Visibility.Collapsed)
        {
            label.Measure(new Size(available.Width, double.PositiveInfinity));
            IsStacked = false;
            return label.DesiredSize;
        }

        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double natural = control.DesiredSize.Width;
        if (double.IsPositiveInfinity(available.Width) || natural + Gap + LabelMinWidth <= available.Width)
        {
            IsStacked = false;
            _controlWidth = natural;
            double labelWidth = double.IsPositiveInfinity(available.Width) ? double.PositiveInfinity : available.Width - natural - Gap;
            label.Measure(new Size(labelWidth, double.PositiveInfinity));
            double w = double.IsPositiveInfinity(available.Width) ? label.DesiredSize.Width + Gap + natural : available.Width;
            return new Size(w, Math.Max(label.DesiredSize.Height, control.DesiredSize.Height));
        }

        IsStacked = true;
        label.Measure(new Size(available.Width, double.PositiveInfinity));
        control.Measure(new Size(available.Width, double.PositiveInfinity));
        _controlWidth = Math.Min(available.Width, control.DesiredSize.Width);
        return new Size(available.Width, label.DesiredSize.Height + StackGap + control.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var label = Label;
        var control = Control;
        if (label is null)
        {
            return final;
        }

        if (control is null || control.Visibility == Visibility.Collapsed)
        {
            label.Arrange(new Rect(0, 0, final.Width, label.DesiredSize.Height));
            return final;
        }

        if (!IsStacked)
        {
            double h = final.Height;
            double cw = Math.Min(_controlWidth, final.Width);
            label.Arrange(new Rect(0, (h - label.DesiredSize.Height) / 2, Math.Max(0, final.Width - cw - Gap), label.DesiredSize.Height));
            control.Arrange(new Rect(final.Width - cw, (h - control.DesiredSize.Height) / 2, cw, control.DesiredSize.Height));
        }
        else
        {
            label.Arrange(new Rect(0, 0, final.Width, label.DesiredSize.Height));
            control.Arrange(new Rect(0, label.DesiredSize.Height + StackGap, Math.Min(final.Width, _controlWidth), control.DesiredSize.Height));
        }

        return final;
    }
}

/// <summary>
/// Сетка карточек одинаковой ширины: столбцов столько, сколько помещается при ширине не меньше <see cref="MinItemWidth"/>
/// (не больше <see cref="MaxColumns"/>); высота строки — по самой высокой карточке. Используется для карточек «Главной» и плагинов.
/// </summary>
public sealed class AdaptiveGrid : Panel
{
    public double MinItemWidth { get; set; } = 280;

    public double Gap { get; set; } = 12;

    public int MaxColumns { get; set; } = 4;

    /// <summary>Столбцов в последней раскладке.</summary>
    public int Columns { get; private set; } = 1;

    private readonly List<double> _rowHeights = new();

    private int ColumnsFor(double width) =>
        double.IsPositiveInfinity(width) ? MaxColumns : Math.Clamp((int)Math.Floor((width + Gap) / (MinItemWidth + Gap)), 1, MaxColumns);

    protected override Size MeasureOverride(Size available)
    {
        var items = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
        // Колонок столько, сколько помещается по ширине, даже если карточек меньше: одна карточка не растягивается на всю строку.
        Columns = ColumnsFor(available.Width);
        double width = double.IsPositiveInfinity(available.Width) ? MinItemWidth : (available.Width - ((Columns - 1) * Gap)) / Columns;
        _rowHeights.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Measure(new Size(width, double.PositiveInfinity));
            if (i % Columns == 0)
            {
                _rowHeights.Add(0);
            }

            _rowHeights[^1] = Math.Max(_rowHeights[^1], items[i].DesiredSize.Height);
        }

        double total = _rowHeights.Sum() + (Math.Max(0, _rowHeights.Count - 1) * Gap);
        return new Size(double.IsPositiveInfinity(available.Width) ? (Columns * width) + ((Columns - 1) * Gap) : available.Width, total);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var items = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
        double width = (final.Width - ((Columns - 1) * Gap)) / Columns;
        double y = 0;
        for (int i = 0; i < items.Count; i++)
        {
            int row = i / Columns, col = i % Columns;
            if (col == 0 && row > 0)
            {
                y += _rowHeights[row - 1] + Gap;
            }

            items[i].Arrange(new Rect(col * (width + Gap), y, width, row < _rowHeights.Count ? _rowHeights[row] : items[i].DesiredSize.Height));
        }

        return final;
    }
}

/// <summary>
/// Содержимое страницы (Phase 4F): карточки настроек и превью — в одном проходе раскладки, внутри общей прокрутки.
/// <list type="bullet">
/// <item>Широкий режим (ширина ≥ <see cref="WideThreshold"/>): вводные карточки (<see cref="PreviewStart"/>, например каталог стилей —
///       он сам состоит из миниатюр) идут во всю ширину; дальше превью — колонка справа. Его верх совпадает с верхом первой карточки
///       блока, низ — с нижней границей последней карточки логического блока. Блок — наименьшее число первых карточек, высота которых не
///       меньше естественной высоты сцены (ширина превью × <see cref="PreviewAspect"/>), но не больше <see cref="PreviewBlockMax"/>
///       карточек: на длинной странице превью не растягивается на всю страницу пустым контейнером.</item>
/// <item>Узкий режим: превью над карточками своего блока (после вводных) на всю ширину содержимого, высота — ширина × <see cref="PreviewAspect"/>, не больше
///       <see cref="MaxStackedPreviewHeight"/> (доля видимой высоты окна, задаёт окно).</item>
/// </list>
/// Сцена внутри превью масштабируется отдельно (Viewbox Uniform) — без искажения пропорций. Смена размера окна, языка или шрифта
/// перестраивает раскладку автоматически; переход между режимами не пересоздаёт элементы страницы.
/// </summary>
public sealed class PageLayout : Panel
{
    public static readonly DependencyProperty IsPreviewProperty = DependencyProperty.RegisterAttached(
        "IsPreview", typeof(bool), typeof(PageLayout), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static void SetIsPreview(UIElement e, bool v) => e.SetValue(IsPreviewProperty, v);

    public static bool GetIsPreview(UIElement e) => (bool)e.GetValue(IsPreviewProperty);

    /// <summary>
    /// Ширина, с которой превью встаёт колонкой справа внутри страницы. Главное окно держит превью справа само (колонка на всю высоту
    /// окна), поэтому внутри страницы оставляет только вертикальную компоновку (<see cref="double.PositiveInfinity"/>).
    /// </summary>
    public double WideThreshold { get; set; } = 860;

    public double Gap { get; set; } = 24;

    public double PreviewAspect { get; set; } = 0.62;

    public int PreviewBlockMax { get; set; } = 4;

    /// <summary>Число вводных карточек перед блоком превью (во всю ширину).</summary>
    public int PreviewStart { get; set; }

    /// <summary>Верх первой карточки блока (с ним совпадает верх превью в широком режиме).</summary>
    public double BlockTop { get; private set; }

    public double MaxStackedPreviewHeight { get; set; } = 260;

    /// <summary>Наибольшая ширина колонки карточек без превью (очень широкое окно — строки не растягиваются бесконечно).</summary>
    public double MaxCardsWidth { get; set; } = 1100;

    public bool IsWide { get; private set; }

    /// <summary>Прямоугольник превью в координатах панели (последняя раскладка; пусто — превью нет).</summary>
    public Rect PreviewRect { get; private set; } = Rect.Empty;

    /// <summary>Карточек в логическом блоке превью (широкий режим).</summary>
    public int BlockCards { get; private set; }

    /// <summary>Нижняя граница последней карточки блока (без её внешнего отступа) — с ней совпадает низ превью.</summary>
    public double BlockBottom { get; private set; }

    private UIElement? Preview => InternalChildren.Cast<UIElement>().FirstOrDefault(c => GetIsPreview(c) && c.Visibility != Visibility.Collapsed);

    private List<UIElement> Cards => InternalChildren.Cast<UIElement>().Where(c => !GetIsPreview(c) && c.Visibility != Visibility.Collapsed).ToList();

    private double _cardsWidth, _previewWidth, _previewHeight;

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsPositiveInfinity(available.Width) ? WideThreshold : available.Width;
        var preview = Preview;
        var cards = Cards;
        IsWide = preview is not null && w >= WideThreshold;
        if (preview is null)
        {
            _cardsWidth = Math.Min(w, MaxCardsWidth);
            double h = MeasureCards(cards, _cardsWidth);
            PreviewRect = Rect.Empty;
            return new Size(w, h);
        }

        int start = Math.Clamp(PreviewStart, 0, cards.Count);
        double lead = MeasureCards(cards.Take(start).ToList(), w);
        var rest = cards.Skip(start).ToList();
        BlockTop = lead;
        if (IsWide)
        {
            _previewWidth = Math.Clamp(w * 0.38, 320, 520);
            _cardsWidth = w - _previewWidth - Gap;
            double total = MeasureCards(rest, _cardsWidth);
            var (count, bottom) = Block(rest, _previewWidth * PreviewAspect);
            BlockCards = count;
            BlockBottom = lead + bottom;
            _previewHeight = bottom;
            preview.Measure(new Size(_previewWidth, _previewHeight));
            return new Size(w, lead + Math.Max(total, _previewHeight));
        }

        _cardsWidth = w;
        _previewWidth = w;
        _previewHeight = Math.Max(120, Math.Min(w * PreviewAspect, MaxStackedPreviewHeight));
        preview.Measure(new Size(_previewWidth, _previewHeight));
        BlockCards = 0;
        double cardsHeight = MeasureCards(rest, w);
        return new Size(w, lead + _previewHeight + Gap + cardsHeight);
    }

    private static double MeasureCards(List<UIElement> cards, double width)
    {
        double h = 0;
        foreach (var c in cards)
        {
            c.Measure(new Size(width, double.PositiveInfinity));
            h += c.DesiredSize.Height;
        }

        return h;
    }

    /// <summary>Логический блок: первые карточки до высоты не меньше <paramref name="target"/> (не больше <see cref="PreviewBlockMax"/>).</summary>
    private (int Count, double Bottom) Block(List<UIElement> cards, double target)
    {
        double y = 0, bottom = 0;
        int n = 0;
        foreach (var c in cards)
        {
            double margin = c is FrameworkElement fe ? fe.Margin.Bottom : 0;
            bottom = y + c.DesiredSize.Height - margin;
            y += c.DesiredSize.Height;
            n++;
            if (bottom >= target || n >= PreviewBlockMax)
            {
                break;
            }
        }

        return (n, n == 0 ? target : Math.Max(bottom, 120));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var preview = Preview;
        var cards = Cards;
        int start = preview is null ? 0 : Math.Clamp(PreviewStart, 0, cards.Count);
        double y = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (i == start && preview is not null)
            {
                if (IsWide)
                {
                    PreviewRect = new Rect(final.Width - _previewWidth, y, _previewWidth, _previewHeight);
                }
                else
                {
                    PreviewRect = new Rect(0, y, final.Width, _previewHeight);
                    y += _previewHeight + Gap;
                }

                preview.Arrange(PreviewRect);
            }

            double width = preview is null ? Math.Min(final.Width, _cardsWidth) : i < start || !IsWide ? final.Width : _cardsWidth;
            cards[i].Arrange(new Rect(0, y, width, cards[i].DesiredSize.Height));
            y += cards[i].DesiredSize.Height;
        }

        if (preview is not null && start >= cards.Count)
        {
            PreviewRect = IsWide ? new Rect(final.Width - _previewWidth, y, _previewWidth, _previewHeight) : new Rect(0, y, final.Width, _previewHeight);
            preview.Arrange(PreviewRect);
        }

        return final;
    }
}
