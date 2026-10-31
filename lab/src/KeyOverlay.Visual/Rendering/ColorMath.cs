using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Осветление и затемнение в OKLCH (Phase 2A CO-04): меняется только светлота L, хрома и тон сохраняются; выход из гаммы sRGB
/// обрезается по каналам. Формулы OKLab — Björn Ottosson (публичные, без внешних библиотек). Альфа не меняется.
/// </summary>
public static class ColorMath
{
    public static ThemeColor Lighten(ThemeColor c, double amount) => ShiftL(c, amount);

    public static ThemeColor Darken(ThemeColor c, double amount) => ShiftL(c, -amount);

    /// <summary>Смешение двух цветов в sRGB (для производных оттенков: доля <paramref name="t"/> второго цвета).</summary>
    public static ThemeColor Mix(ThemeColor a, ThemeColor b, double t)
    {
        byte M(byte x, byte y) => (byte)Math.Round(x + ((y - x) * Math.Clamp(t, 0, 1)));
        return new ThemeColor(M(a.R, b.R), M(a.G, b.G), M(a.B, b.B), M(a.A, b.A));
    }

    public static ThemeColor WithAlpha(ThemeColor c, double alpha) => c with { A = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255) };

    /// <summary>Относительная яркость (WCAG), 0…1.</summary>
    public static double Luminance(ThemeColor c)
    {
        static double Lin(byte v)
        {
            double x = v / 255.0;
            return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Lin(c.R)) + (0.7152 * Lin(c.G)) + (0.0722 * Lin(c.B));
    }

    private static ThemeColor ShiftL(ThemeColor c, double delta)
    {
        var (l, a, b) = ToOkLab(c);
        l = Math.Clamp(l + delta, 0, 1);
        return FromOkLab(l, a, b, c.A);
    }

    public static (double L, double A, double B) ToOkLab(ThemeColor c)
    {
        double r = ToLinear(c.R), g = ToLinear(c.G), bl = ToLinear(c.B);
        double l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * bl));
        double m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * bl));
        double s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * bl));
        return (
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    public static ThemeColor FromOkLab(double lightness, double a, double b, byte alpha = 0xFF)
    {
        double l = lightness + (0.3963377774 * a) + (0.2158037573 * b);
        double m = lightness - (0.1055613458 * a) - (0.0638541728 * b);
        double s = lightness - (0.0894841775 * a) - (1.2914855480 * b);
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        double r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        double g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        double bl = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);
        return new ThemeColor(FromLinear(r), FromLinear(g), FromLinear(bl), alpha);
    }

    private static double ToLinear(byte v)
    {
        double x = v / 255.0;
        return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
    }

    private static byte FromLinear(double x)
    {
        x = Math.Clamp(x, 0, 1);
        double v = x <= 0.0031308 ? x * 12.92 : (1.055 * Math.Pow(x, 1 / 2.4)) - 0.055;
        return (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
    }
}
