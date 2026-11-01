using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Backpack.Visual.Icons;

/// <summary>
/// Линейные иконки Lucide (версия <see cref="Version"/>, геометрия — <c>LucideIcons.g.cs</c>). Сетка 24×24, толщина линии 2,
/// скруглённые концы и соединения — как в оригинале. Цвет — кисть темы (DynamicResource) или явная кисть.
/// Специальные символы клавиш и стили мыши используют собственную предметную графику, а не эти иконки.
/// </summary>
public static partial class Lucide
{
    private static readonly Dictionary<string, Geometry[]> Cache = new();

    /// <summary>
    /// Собственные векторы Backpack в той же сетке 24×24 и толщине линии — для предметов, которых нет в Lucide (префикс <c>bp-</c>).
    /// </summary>
    private static readonly Dictionary<string, string[]> Own = new()
    {
        // Нумерованная отметка: круг с цифрой 1.
        ["bp-marker"] = ["M2,12 A10,10 0 1 0 22,12 A10,10 0 1 0 2,12 Z", "M10,9.2 L12.6,7.5 V16.5", "M10,16.5 H15"],
        // Скруглённый прямоугольник: прямоугольник с крупным скруглением.
        ["bp-roundrect"] = ["M3,12 V10 A5,5 0 0 1 8,5 H16 A5,5 0 0 1 21,10 V14 A5,5 0 0 1 16,19 H8 A5,5 0 0 1 3,14 Z"],
        // rotate-ccw (Lucide, ISC) — сброс таймера и других плагинов; геометрия переписана абсолютными командами.
        ["rotate-ccw"] = ["M3,12 A9,9 0 1 0 12,3 A9.75,9.75 0 0 0 5.26,5.74 L3,8", "M3,3 V8 H8"],
    };

    public static IEnumerable<string> Names => Data.Keys.Concat(Own.Keys);

    public static bool Has(string name) => Data.ContainsKey(name) || Own.ContainsKey(name);

    /// <summary>Геометрии иконки (замороженные, общие для всех элементов).</summary>
    public static Geometry[] Geometries(string name)
    {
        if (!Cache.TryGetValue(name, out var g))
        {
            g = Data.TryGetValue(name, out var parts) || Own.TryGetValue(name, out parts) ? parts.Select(Parse).ToArray() : [];
            Cache[name] = g;
        }

        return g;

        static Geometry Parse(string d)
        {
            var geometry = Geometry.Parse(d);
            geometry.Freeze();
            return geometry;
        }
    }

    /// <summary>Иконка размера <paramref name="size"/> DIP; цвет — ресурс темы <paramref name="brushKey"/>.</summary>
    public static FrameworkElement Icon(string name, double size = 20, string brushKey = "Ko.TextBrush", double strokeWidth = 2)
    {
        var canvas = Canvas(name, strokeWidth, p => p.SetResourceReference(Shape.StrokeProperty, brushKey));
        return Wrap(canvas, size);
    }

    /// <summary>Иконка с явной кистью (окна вне темы интерфейса, оверлей).</summary>
    public static FrameworkElement Icon(string name, double size, Brush brush, double strokeWidth = 2)
    {
        var canvas = Canvas(name, strokeWidth, p => p.Stroke = brush);
        return Wrap(canvas, size);
    }

    private static Canvas Canvas(string name, double strokeWidth, Action<Path> color)
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var g in Geometries(name))
        {
            var p = new Path
            {
                Data = g,
                StrokeThickness = strokeWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = null,
            };
            color(p);
            canvas.Children.Add(p);
        }

        return canvas;
    }

    /// <summary>Перекрасить иконку, созданную <see cref="Icon(string, double, string, double)"/>, другим ресурсом темы (состояние выбора).</summary>
    public static void Recolor(FrameworkElement icon, string brushKey)
    {
        if (icon is Viewbox { Child: Canvas c })
        {
            foreach (var p in c.Children.OfType<Path>())
            {
                p.SetResourceReference(Shape.StrokeProperty, brushKey);
            }
        }
    }

    private static Viewbox Wrap(Canvas canvas, double size) => new()
    {
        Width = size,
        Height = size,
        Child = canvas,
        Stretch = Stretch.Uniform,
        SnapsToDevicePixels = true,
        IsHitTestVisible = false,
        Focusable = false,
    };
}
