using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KeyOverlay.Visual.Brand;

/// <summary>
/// Текстовый фирменный знак «Backpack.» (Phase 4H): название шрифтом заголовков и изумрудная точка сразу после последней буквы,
/// как в логотипе сайта. Точка — часть знака: стоит на базовой линии (InlineUIContainer, BaselineAlignment.Baseline), диаметр и
/// отступ — доли размера шрифта. Цвет — кисть логотипа темы (<c>Ko.LogoBrush</c>: в Emerald — изумрудный), свечение — эффект
/// акцента темы (<c>Ko.AccentGlowEffect</c>; в стилях без свечения он пуст). Доступное имя — «Backpack» без точки.
/// Используется только там, где показан именно фирменный знак (логотип над меню, приветствие, «О программе»), а не в тексте,
/// именах файлов и идентификаторах.
/// </summary>
public static class Wordmark
{
    /// <summary>Диаметр точки и отступ от последней буквы в долях размера шрифта.</summary>
    public const double DotSize = 0.2, DotGap = 0.05;

    public static TextBlock Create(double fontSize, FontWeight weight)
    {
        var text = new TextBlock { FontSize = fontSize, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center, Tag = "wordmark" };
        text.SetResourceReference(TextBlock.FontFamilyProperty, "Ko.HeadingFont");
        text.Inlines.Add(new Run("Backpack"));
        double d = Math.Max(3, Math.Round(fontSize * DotSize, 1));
        var dot = new Ellipse { Width = d, Height = d, Margin = new Thickness(Math.Round(fontSize * DotGap, 1), 0, 0, 0), IsHitTestVisible = false, Tag = "brand-dot" };
        dot.SetResourceReference(Shape.FillProperty, "Ko.LogoBrush");
        dot.SetResourceReference(UIElement.EffectProperty, "Ko.AccentGlowEffect");
        text.Inlines.Add(new InlineUIContainer(dot) { BaselineAlignment = BaselineAlignment.Baseline });
        System.Windows.Automation.AutomationProperties.SetName(text, "Backpack");
        return text;
    }

    /// <summary>Точка знака внутри созданного <see cref="Create"/> блока (для проверок).</summary>
    public static Ellipse? Dot(TextBlock wordmark) =>
        wordmark.Inlines.OfType<InlineUIContainer>().Select(c => c.Child).OfType<Ellipse>().FirstOrDefault();
}
