using System.Globalization;
using System.Windows.Media;

namespace Backpack.Visual.Settings;

/// <summary>
/// Цвет в записи темы Backpack: <c>#RRGGBB</c> или <c>#RRGGBBAA</c> — альфа <b>последней</b>, как в CSS и схеме темы.
/// WPF пишет альфу первой (<c>#AARRGGBB</c>); преобразование только здесь, чтобы в настройках был один формат.
/// </summary>
public readonly record struct ThemeColor(byte R, byte G, byte B, byte A = 0xFF)
{
    public static ThemeColor Parse(string text) =>
        TryParse(text, out var c) ? c : throw new FormatException($"Ожидается цвет #RRGGBB или #RRGGBBAA, получено «{text}»");

    public static bool TryParse(string? text, out ThemeColor color)
    {
        color = default;
        if (text is null || text.Length is not (7 or 9) || text[0] != '#')
        {
            return false;
        }

        for (int i = 1; i < text.Length; i++)
        {
            if (!char.IsAsciiHexDigit(text[i]))
            {
                return false;
            }
        }

        byte P(int i) => byte.Parse(text.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        color = new ThemeColor(P(1), P(3), P(5), text.Length == 9 ? P(7) : (byte)0xFF);
        return true;
    }

    public Color ToWpf() => Color.FromArgb(A, R, G, B);

    public static ThemeColor FromWpf(Color c) => new(c.R, c.G, c.B, c.A);

    public SolidColorBrush ToFrozenBrush()
    {
        var b = new SolidColorBrush(ToWpf());
        b.Freeze();
        return b;
    }

    /// <summary>Запись темы: без альфы, если цвет непрозрачный.</summary>
    public override string ToString() => A == 0xFF ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
