using System.Windows.Media;

namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Шрифт подписей на кейкапах и индикаторе раскладки: «Стандартный» — встроенный Inter 4.1, или встроенный Spectral (settings v7,
/// <c>fonts.keycap</c>). Оба — ресурсы сборки, в системе не устанавливаются. Цепочка «Spectral → Inter → Segoe UI → Segoe UI Symbol»:
/// символа, которого нет в шрифте (стрелки, ⌘, греческий), WPF берёт из следующего по списку. Значки клавиш — векторная графика
/// и от шрифта не зависят.
/// </summary>
public static class VisualFonts
{
    public static readonly Uri FontsBaseUri = new("pack://application:,,,/KeyOverlay.Visual;component/Fonts/");

    private static FontFamily? s_keycap;
    private static string? s_family;

    /// <summary>Растёт при каждой смене шрифта: кейкапы перестраивают подписи.</summary>
    public static int Generation { get; private set; }

    /// <summary>Выбранный шрифт подписей (null — «Стандартный», встроенный Inter).</summary>
    public static string? KeycapFamilyName => s_family;

    public static FontFamily Keycap
    {
        get
        {
            if (s_keycap is null)
            {
                EnsurePackScheme();
                s_keycap = new FontFamily(FontsBaseUri, Chain(s_family, "Segoe UI, Segoe UI Symbol"));
            }

            return s_keycap;
        }
    }

    /// <summary>Сменить шрифт подписей (null — встроенный). Повторный вызов с тем же значением ничего не делает.</summary>
    public static void SetKeycapFamily(string? family)
    {
        family = Settings.FontSettings.NormalizeName(family);
        if (family == s_family && s_keycap is not null)
        {
            return;
        }

        s_family = family;
        s_keycap = null;
        Generation++;
    }

    /// <summary>
    /// Цепочка семейств: null — «Стандартный» (встроенный Inter); «Spectral» — встроенный Spectral перед Inter; затем системные
    /// запасные семейства для отсутствующих символов.
    /// </summary>
    public static string Chain(string? family, string systemFallback) =>
        (Settings.FontSettings.NormalizeName(family) is null ? "" : "./#Spectral, ") + "./#Inter, " + systemFallback;

    /// <summary>Регистрирует схему pack:// и вне WPF-приложения (тесты, превью до старта Application).</summary>
    public static void EnsurePackScheme() => _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
}
