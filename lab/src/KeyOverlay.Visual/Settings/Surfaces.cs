using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Рамка со скруглением, которая действительно обрезает содержимое по внутреннему контуру. <c>Border.ClipToBounds</c> в WPF
/// режет только по прямоугольнику, поэтому углы движущегося содержимого (видео, анимации, VisualBrush, свечение) выступали
/// за скруглённую рамку. Здесь при каждой раскладке дочернему элементу задаётся скруглённый <see cref="UIElement.Clip"/>
/// в его собственных координатах: радиус внутреннего контура = радиус рамки − толщина границы. Размер берётся из раскладки,
/// поэтому обрезка верна при любом размере окна и DPI (WPF работает в DIP и сам переводит геометрию в пиксели).
/// </summary>
public class ClipBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (Child is UIElement child)
        {
            var b = BorderThickness;
            var p = Padding;
            double w = Math.Max(0, result.Width - b.Left - b.Right - p.Left - p.Right);
            double h = Math.Max(0, result.Height - b.Top - b.Bottom - p.Top - p.Bottom);
            double r = Math.Max(0, CornerRadius.TopLeft - Math.Max(b.Left, b.Top));
            // Clip задаётся в координатах дочернего элемента: его собственный отступ сдвигает начало координат внутрь.
            var m = child is FrameworkElement fe ? fe.Margin : default;
            var clip = new RectangleGeometry(new Rect(-m.Left, -m.Top, w, h), r, r);
            clip.Freeze();
            child.Clip = clip;
        }

        return result;
    }
}

/// <summary>
/// «Сцена» превью и иллюстраций: фон по теме окна (светлый в светлой теме, тёмный в тёмной) и мягкая сетка «рабочего стола»
/// (кисти <c>Ko.StageBrush</c>, <c>Ko.StageGridBrush</c>). Цвета самих инструментов (стиль кейкапов, мыши, рамка «Фокуса»)
/// сцена не меняет. Смена темы применяется сразу (ссылки на ресурсы).
/// </summary>
public static class Stage
{
    public static Grid Wrap(UIElement content, bool grid = true)
    {
        var g = new Grid();
        g.SetResourceReference(Panel.BackgroundProperty, "Ko.StageBrush");
        if (grid)
        {
            var pattern = new Border { IsHitTestVisible = false };
            pattern.SetResourceReference(Border.BackgroundProperty, "Ko.StageGridBrush");
            g.Children.Add(pattern);
        }

        g.Children.Add(content);
        return g;
    }

    /// <summary>Скруглённая карточка-сцена с настоящей обрезкой (превью и иллюстрации внутри страниц).</summary>
    public static ClipBorder Card(UIElement content, double radius = 12, bool grid = true)
    {
        var b = new ClipBorder { CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Child = Wrap(content, grid) };
        b.SetResourceReference(Border.BorderBrushProperty, "Ko.BorderBrush");
        return b;
    }
}
