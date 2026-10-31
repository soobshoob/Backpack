using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Шапка страницы (Phase 4I, общая для всех страниц, включая страницы плагинов): «Назад» слева, крупное название и действия
/// раздела у правого края области содержимого. Дочерние элементы: [0] — «Назад», [1] — название (TextBlock), [2] — действия.
/// <list type="bullet">
/// <item>«Назад» и действия центрируются по <b>первой строке</b> названия — не по всей высоте шапки и не по окну, поэтому
///       перенос длинного названия их не сдвигает;</item>
/// <item>действия стоят у правого края (тот же отступ, что у карточек), ниже кнопок окна (<see cref="CaptionBottom"/>);</item>
/// <item>название не заходит под кнопки окна: справа оставляется <see cref="CaptionReserve"/>, а если действия шире — их место;</item>
/// <item>при RTL WPF зеркалирует раскладку панели сама.</item>
/// </list>
/// Ручные отступы на отдельных страницах не нужны.
/// </summary>
public sealed class PageHeader : Panel
{
    public const double Gap = 12;

    /// <summary>Сколько справа (от правого края шапки) занимают кнопки окна по горизонтали.</summary>
    public double CaptionReserve { get; set; }

    /// <summary>Нижний край кнопок окна в координатах шапки (действия не поднимаются выше него).</summary>
    public double CaptionBottom { get; set; }

    private UIElement? Back => InternalChildren.Count > 0 ? InternalChildren[0] : null;

    private TextBlock? Title => InternalChildren.Count > 1 ? InternalChildren[1] as TextBlock : null;

    private UIElement? Actions => InternalChildren.Count > 2 ? InternalChildren[2] : null;

    private static bool Shown(UIElement? e) => e is not null && e.Visibility != Visibility.Collapsed;

    /// <summary>Высота первой строки названия (по метрикам шрифта, с учётом LineHeight).</summary>
    public double FirstLineHeight
    {
        get
        {
            if (Title is not { } t)
            {
                return 0;
            }

            return !double.IsNaN(t.LineHeight) && t.LineHeight > 0 ? t.LineHeight : t.FontFamily.LineSpacing * t.FontSize;
        }
    }

    private (double Back, double Actions) Widths(double available)
    {
        double back = Shown(Back) ? Back!.DesiredSize.Width + Gap : 0;
        double actions = Shown(Actions) ? Actions!.DesiredSize.Width + Gap : 0;
        return (back, Math.Max(actions, CaptionReserve));
    }

    protected override Size MeasureOverride(Size available)
    {
        Back?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Actions?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (back, actions) = Widths(available.Width);
        double titleWidth = double.IsPositiveInfinity(available.Width) ? double.PositiveInfinity : Math.Max(40, available.Width - back - actions);
        Title?.Measure(new Size(titleWidth, double.PositiveInfinity));
        double line = FirstLineHeight;
        double center = Center(line);
        double h = center - (line / 2) + (Title?.DesiredSize.Height ?? 0);
        h = Math.Max(h, center + Half(Back));
        h = Math.Max(h, center + Half(Actions));
        double w = double.IsPositiveInfinity(available.Width) ? back + (Title?.DesiredSize.Width ?? 0) + actions : available.Width;
        return new Size(w, h);
    }

    private static double Half(UIElement? e) => Shown(e) ? e!.DesiredSize.Height / 2 : 0;

    /// <summary>Центр первой строки; если действия иначе зашли бы на кнопки окна — ниже, а название сдвигается вместе.</summary>
    private double Center(double line)
    {
        double center = line / 2;
        double minCenter = CaptionBottom + Half(Actions);
        return Shown(Actions) && center < minCenter ? minCenter : center;
    }

    protected override Size ArrangeOverride(Size final)
    {
        var (back, actions) = Widths(final.Width);
        double line = FirstLineHeight;
        double center = Center(line);
        double titleTop = center - (line / 2);
        if (Shown(Back))
        {
            var b = Back!.DesiredSize;
            Back.Arrange(new Rect(0, center - (b.Height / 2), b.Width, b.Height));
        }

        if (Title is { } t)
        {
            t.Arrange(new Rect(back, titleTop, Math.Max(0, final.Width - back - actions), t.DesiredSize.Height));
        }

        if (Shown(Actions))
        {
            var a = Actions!.DesiredSize;
            Actions.Arrange(new Rect(Math.Max(0, final.Width - a.Width), center - (a.Height / 2), a.Width, a.Height));
        }

        return final;
    }

    /// <summary>Центр первой строки названия по вертикали в координатах шапки (для проверок).</summary>
    public double FirstLineCenter => Center(FirstLineHeight);

    internal static Rect BoundsIn(UIElement e, System.Windows.Media.Visual ancestor) => e.TransformToAncestor(ancestor).TransformBounds(new Rect(e.RenderSize));
}
