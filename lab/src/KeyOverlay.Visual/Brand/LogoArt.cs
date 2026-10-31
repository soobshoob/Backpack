using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KeyOverlay.Visual.Brand;

/// <summary>
/// Знак Backpack — тот же рюкзак, что на сайте (Phase 4I): геометрия значка <c>Backpack</c> из lucide-react 0.468.0 (ISC), которым
/// сайт рисует логотип в шапке и подвале (<c>Website/src/App.tsx</c>, класс <c>.brand</c>; толщина линии 1,5 и цвет — в
/// <c>Website/src/style.css</c>, <c>.brand&gt;svg</c>). Контуры перенесены без изменений из
/// <c>Website/node_modules/lucide-react/dist/esm/icons/backpack.js</c>; прежний собственный рисунок (ручка, клапан, карман с пряжкой)
/// не используется. Только знак, без подложки и без букв. Цвет в окнах — акцент темы (<see cref="Themed"/>, кисть
/// <c>Ko.LogoBrush</c>), у файлов значка — <see cref="IconColor"/>.
/// Один набор контуров (<see cref="Spec"/>) даёт и WPF-рисунок, и SVG (<see cref="ToSvg"/>). Малые размеры (<see cref="Spec.Small"/>)
/// — те же контуры с более толстой линией (настройка растеризации, не другой рисунок).
/// </summary>
public static class LogoArt
{
    /// <summary>
    /// Цвет файлов значка программы (ICO, SVG): изумрудный между акцентом (#087F5B) и свечением (#31C99A) сайта. Свечение сайта на
    /// светлой панели задач Windows даёт контраст около 2:1 и теряется; этот оттенок читается на светлой и на тёмной панели.
    /// </summary>
    public const string IconColor = "#1FA67C";

    /// <summary>Источник знака (для «О программе», отчётов и проверок).</summary>
    public const string Source = "lucide-react 0.468.0 · icons/backpack.js (Website/src/App.tsx → .brand)";

    /// <summary>Параметры в сетке 24×24 (как у Lucide): контуры (язык путей SVG/WPF), толщина линии.</summary>
    public sealed record Spec(double Canvas, double BackgroundRadius, double Stroke, IReadOnlyList<string> Paths)
    {
        /// <summary>Контуры знака сайта — без изменений (lucide-react 0.468.0, Backpack).</summary>
        public static readonly string[] SitePaths =
        [
            "M4 10a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z",
            "M8 10h8",
            "M8 18h8",
            "M8 22v-6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v6",
            "M9 6V4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2",
        ];

        /// <summary>Как на сайте: толщина линии 1,5 (CSS <c>.brand&gt;svg { stroke-width: 1.5 }</c>).</summary>
        public static Spec Full { get; } = new(24, 0, 1.5, SitePaths);

        /// <summary>Малые размеры: те же контуры, линия 2 (иначе на 16–24 px контур тоньше пикселя).</summary>
        public static Spec Small { get; } = new(24, 0, 2, SitePaths);
    }

    /// <summary>Доля стороны квадрата, которую занимает контур в значке (ICO, окно); остаток — поле для сглаживания.</summary>
    public const double IconFill = 0.94;

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Рисунок логотипа в квадрате <paramref name="size"/>: контур цветом <paramref name="color"/> (без подложки).</summary>
    public static DrawingGroup Drawing(double size, bool simplified = false, Color? color = null, bool iconFrame = false)
    {
        var spec = simplified || size <= 24 ? Spec.Small : Spec.Full;
        double k = size / spec.Canvas;
        double stroke = iconFrame ? IconStroke(size, spec) : spec.Stroke;
        var pen = new Pen(new SolidColorBrush(color ?? Hex(IconColor)), stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var geometries = spec.Paths.Select(Geometry.Parse).ToList();
        var g = new DrawingGroup();
        using (var dc = g.Open())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            if (iconFrame)
            {
                // Значок (ICO, окно, панель задач): контур с учётом толщины линии — по центру квадрата и на IconFill его стороны.
                // Без этого рюкзак занимал ~59 % ширины и ~78 % высоты и выглядел мелким рядом с другими значками панели задач.
                var bounds = Rect.Empty;
                foreach (var geo in geometries)
                {
                    bounds.Union(geo.GetRenderBounds(pen));
                }

                double fit = spec.Canvas * IconFill / Math.Max(bounds.Width, bounds.Height);
                dc.PushTransform(new TranslateTransform(spec.Canvas / 2, spec.Canvas / 2));
                dc.PushTransform(new ScaleTransform(fit, fit));
                dc.PushTransform(new TranslateTransform(-(bounds.Left + (bounds.Width / 2)), -(bounds.Top + (bounds.Height / 2))));
            }

            foreach (var geo in geometries)
            {
                dc.DrawGeometry(null, pen, geo);
            }

            if (iconFrame)
            {
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }

            dc.Pop();
        }

        g.Freeze();
        return g;
    }

    /// <summary>Растр логотипа (квадрат <paramref name="pixels"/>), прозрачный вне контура.</summary>
    public static BitmapSource Render(int pixels, bool simplified = false, Color? color = null, bool iconFrame = false)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawDrawing(Drawing(pixels, simplified, color, iconFrame));
        }

        var bmp = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// Толщина линии значка (в единицах сетки 24) по размеру кадра: на 16–48 px линия сайта (1,5) — около 1–3 px и выглядит
    /// бледной рядом с заливными значками панели задач; утолщается, с 72 px — как на сайте. Контуры при этом те же.
    /// </summary>
    public static double IconStroke(double size, Spec spec) => size <= 20 ? 2.2 : size <= 32 ? 2 : size <= 48 ? 1.85 : size <= 64 ? 1.7 : spec.Stroke;

    /// <summary>Размеры кадров значка окна и ICO: панель задач берёт 24/32/36/48 px по масштабу, Alt+Tab — до 256.</summary>
    public static readonly int[] IconSizes = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 96, 128, 256];

    /// <summary>
    /// Значок окна (панель задач, Alt+Tab, заголовок) заданного цвета — растровый многоразмерный ICO.
    /// <para>
    /// Phase 4H: прежде окну давался векторный <see cref="DrawingImage"/>. Его размер — границы линий (около 24×30 DIP), а не
    /// квадрат, и WPF кладёт такой рисунок в значок 48×48 по центру 1:1: на панели задач при 150 % рюкзак занимал 24×30 из 48
    /// пикселей. Теперь у WPF есть кадр нужного размера, где контур занимает <see cref="IconFill"/> стороны.
    /// </para>
    /// </summary>
    public static BitmapFrame WindowIcon(Color? color = null)
    {
        var decoder = new IconBitmapDecoder(new MemoryStream(Ico(IconSizes, color: color)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    /// <summary>Изображение значка заданного цвета (то же, что <see cref="WindowIcon"/>).</summary>
    public static ImageSource Image(double size, Color? color = null) => WindowIcon(color);

    /// <summary>Логотип для окон: вектор с кистью темы <c>Ko.LogoBrush</c> (меняется вместе с темой без перестройки).</summary>
    public static FrameworkElement Themed(double size)
    {
        var spec = size <= 24 ? Spec.Small : Spec.Full;
        var canvas = new Canvas { Width = spec.Canvas, Height = spec.Canvas };
        foreach (var d in spec.Paths)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(d),
                StrokeThickness = spec.Stroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };
            p.SetResourceReference(Shape.StrokeProperty, "Ko.LogoBrush");
            canvas.Children.Add(p);
        }

        var box = new Viewbox { Width = size, Height = size, Child = canvas, Stretch = Stretch.Uniform };
        System.Windows.Automation.AutomationProperties.SetName(box, "Backpack");
        return box;
    }

    /// <summary>Редактируемый исходник SVG из тех же контуров (цвет — <paramref name="color"/>).</summary>
    public static string ToSvg(bool simplified = false, string color = IconColor)
    {
        var s = simplified ? Spec.Small : Spec.Full;
        var inv = CultureInfo.InvariantCulture;
        string F(double v) => Math.Round(v, 3).ToString(inv);
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(s.Canvas)}\" height=\"{F(s.Canvas)}\" viewBox=\"0 0 {F(s.Canvas)} {F(s.Canvas)}\">");
        sb.AppendLine("  <title>Backpack</title>");
        sb.AppendLine("  <!-- Backpack logo: backpack outline only, no background. Generated by LogoArt.ToSvg; edit Spec there to keep the app icon in sync. -->");
        sb.AppendLine($"  <g fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(s.Stroke)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        foreach (var d in s.Paths)
        {
            sb.AppendLine($"    <path d=\"{d}\"/>");
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Файл ICO с несколькими размерами (PNG внутри). До 24 px включительно — упрощённая версия, начиная с 32 px — полная (при
    /// <paramref name="simplifiedOnly"/> — везде упрощённая, для трея). Цвет — <paramref name="color"/> (по умолчанию изумрудный).
    /// </summary>
    public static byte[] Ico(IReadOnlyList<int> sizes, bool simplifiedOnly = false, Color? color = null)
    {
        var images = sizes.Select(px => (px, Png(Render(px, simplifiedOnly || px <= 24, color, iconFrame: true)))).ToList();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)images.Count);
        int offset = 6 + (16 * images.Count);
        foreach (var (px, data) in images)
        {
            w.Write((byte)(px >= 256 ? 0 : px));
            w.Write((byte)(px >= 256 ? 0 : px));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(data.Length);
            w.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in images)
        {
            w.Write(data);
        }

        w.Flush();
        return ms.ToArray();
    }

    public static byte[] Png(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
