using System.Reflection;
using Backpack.Visual.Updates;

namespace Backpack.Visual.Brand;

/// <summary>Сторонний компонент в составе программы и его лицензия.</summary>
public sealed record ThirdPartyNotice(string Name, string Version, string License, string Use);

/// <summary>
/// Сведения о программе для страницы «О программе». Версия, канал, дата выпуска и репозиторий обновлений берутся из метаданных
/// сборки (задаются при сборке: <c>Directory.Build.props</c> или параметры скрипта упаковки), а не из текущей даты запуска.
/// Пустая дата выпуска — сборка разработчика: дата не показывается как дата выпуска.
/// </summary>
public sealed record AppInfo(
    string Name,
    string Version,
    string Channel,
    string? ReleaseDate,
    string Author,
    string AuthorUrl,
    string? License,
    string? UpdateRepository,
    IReadOnlyList<ThirdPartyNotice> ThirdParty,
    string? LicenseUrl = null)
{
    public const string AuthorName = "soobshoob";
    public const string AuthorLink = "https://github.com/soobshoob";

    public SemVersion? SemVer => SemVersion.TryParse(Version, out var v) ? v : null;

    /// <summary>Сведения из метаданных указанной сборки (обычно — исполняемого файла).</summary>
    public static AppInfo FromAssembly(Assembly assembly)
    {
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            version = version[..plus];
        }

        string? Meta(string key) => assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value is { Length: > 0 } v ? v : null;
        return new AppInfo(
            "Backpack",
            version,
            Meta("BackpackChannel") ?? "Development",
            Meta("BackpackReleaseDate"),
            AuthorName,
            AuthorLink,
            Meta("BackpackLicense"),
            Meta("BackpackUpdateRepository"),
            ThirdPartyNotices,
            Meta("BackpackLicenseUrl"));
    }

    /// <summary>Сторонние компоненты, которые входят в пакет (сверено с пакетом Phase 3A).</summary>
    public static IReadOnlyList<ThirdPartyNotice> ThirdPartyNotices { get; } =
    [
        new("Inter", "4.1", "SIL Open Font License 1.1", "font (embedded)"),
        new("Spectral", "Google Fonts 8b0a1d0", "SIL Open Font License 1.1", "font (embedded, optional)"),
        new(".NET runtime, WPF, Windows Forms (NotifyIcon)", "10", "MIT", "runtime (self-contained package)"),
    ];
}
