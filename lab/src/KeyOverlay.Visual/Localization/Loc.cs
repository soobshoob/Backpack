using System.Globalization;
using System.Resources;

namespace KeyOverlay.Visual.Localization;

/// <summary>
/// Язык интерфейса. «Системный» — язык Windows, если он поддерживается, иначе английский. Phase 4I: 18 языков сайта и русский
/// (значения добавлены в конец — номера прежних не меняются; в app.json хранится имя).
/// </summary>
public enum UiLanguage
{
    System, Ukrainian, English, Russian,
    German, French, Spanish, Italian, Portuguese, Dutch, Polish, Czech, Hungarian, Romanian, Swedish, Danish, Norwegian, Finnish, Lithuanian, Greek,
}

/// <summary>
/// Строки интерфейса из ресурсов <c>Localization/Strings*.resx</c> (en — по умолчанию, uk, ru). Переводимые фразы хранятся целиком
/// с местами подстановки {0}, а не собираются из кусков. Язык меняется без перезапуска: окна подписываются на <see cref="Changed"/>.
/// Язык интерфейса не влияет на раскладку клавиатуры, обозначения клавиш и содержимое профилей.
/// Состояние — на поток (у WPF-окон стенда один UI-поток; тесты в своих STA-потоках не мешают друг другу).
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Strings = new("KeyOverlay.Visual.Localization.Strings", typeof(Loc).Assembly);

    [ThreadStatic] private static CultureInfo? t_culture;
    [ThreadStatic] private static UiLanguage t_language;
    [ThreadStatic] private static Action? t_changed;

    public static CultureInfo Culture => t_culture ??= Resolve(UiLanguage.System);

    public static UiLanguage Language => t_language;

    /// <summary>Код языка интерфейса: «uk», «en» или «ru» (папка справки, имена изображений).</summary>
    public static string Code => Culture.TwoLetterISOLanguageName;

    public static event Action Changed
    {
        add => t_changed += value;
        remove => t_changed -= value;
    }

    public static int SubscriberCount => t_changed?.GetInvocationList().Length ?? 0;

    public static void Set(UiLanguage language)
    {
        var c = Resolve(language);
        t_language = language;
        bool changed = t_culture is null || c.Name != t_culture.Name;
        t_culture = c;
        if (changed)
        {
            t_changed?.Invoke();
        }
    }

    /// <summary>
    /// Поддерживаемые языки: язык, код (ресурсы, справка), культура (числа, даты) и самоназвание для выбора языка. Порядок — как
    /// на сайте, русский в конце (на сайте его нет, в приложении он сохраняется).
    /// </summary>
    public static IReadOnlyList<(UiLanguage Language, string Code, string Culture, string NativeName)> Supported { get; } =
    [
        (UiLanguage.English, "en", "en-US", "English"),
        (UiLanguage.Ukrainian, "uk", "uk-UA", "Українська"),
        (UiLanguage.German, "de", "de-DE", "Deutsch"),
        (UiLanguage.French, "fr", "fr-FR", "Français"),
        (UiLanguage.Spanish, "es", "es-ES", "Español"),
        (UiLanguage.Italian, "it", "it-IT", "Italiano"),
        (UiLanguage.Portuguese, "pt", "pt-PT", "Português"),
        (UiLanguage.Dutch, "nl", "nl-NL", "Nederlands"),
        (UiLanguage.Polish, "pl", "pl-PL", "Polski"),
        (UiLanguage.Czech, "cs", "cs-CZ", "Čeština"),
        (UiLanguage.Hungarian, "hu", "hu-HU", "Magyar"),
        (UiLanguage.Romanian, "ro", "ro-RO", "Română"),
        (UiLanguage.Swedish, "sv", "sv-SE", "Svenska"),
        (UiLanguage.Danish, "da", "da-DK", "Dansk"),
        (UiLanguage.Norwegian, "nb", "nb-NO", "Norsk bokmål"),
        (UiLanguage.Finnish, "fi", "fi-FI", "Suomi"),
        (UiLanguage.Lithuanian, "lt", "lt-LT", "Lietuvių"),
        (UiLanguage.Greek, "el", "el-GR", "Ελληνικά"),
        (UiLanguage.Russian, "ru", "ru-RU", "Русский"),
    ];

    public static CultureInfo Resolve(UiLanguage language, CultureInfo? system = null)
    {
        if (language != UiLanguage.System && Supported.FirstOrDefault(s => s.Language == language) is { Culture: { } culture })
        {
            return CultureInfo.GetCultureInfo(culture);
        }

        // Системный: по двухбуквенному коду Windows (норвежский «no»/«nn» — букмол), иначе английский.
        string two = (system ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        two = two is "no" or "nn" ? "nb" : two;
        return CultureInfo.GetCultureInfo(Supported.FirstOrDefault(s => s.Code == two).Culture ?? "en-US");
    }

    /// <summary>Строка по ключу; отсутствующий ключ виден как [ключ] (тест полноты ловит такие случаи).</summary>
    public static string T(string key) => Strings.GetString(key, Culture) ?? $"[{key}]";

    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);

    /// <summary>Строка для конкретного языка (проверки полноты перевода).</summary>
    public static string? TFor(string key, CultureInfo culture) => Strings.GetString(key, culture);

    public static ResourceSet? SetFor(CultureInfo culture) => Strings.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
}

/// <summary>Сообщение с ключом и аргументами: переводится в момент показа, поэтому смена языка не оставляет старый текст.</summary>
public sealed record LocText(string Key, params object?[] Args)
{
    public override string ToString() => Loc.F(Key, Args);
}
