using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backpack.Visual.Plugins;

/// <summary>Запись индекса поставки: файл пакета, ID и версия плагина, SHA-256 пакета.</summary>
public sealed record BundledEntry(string Path, string Id, string Version, string Sha256);

/// <summary>
/// Индекс поставляемых плагинов (Phase 4H): <c>Plugins\bundled.json</c> рядом с программой, его пишет
/// tools/package-release.ps1. Пакет считается поставленным вместе с программой, только если файл лежит в той же папке, его
/// SHA-256 совпадает с индексом, а ID и версия в manifest — с записью. Отметка «От создателя» связана с этой проверкой, а не
/// с именем автора в manifest. Это проверка происхождения файла в папке программы, а не криптографическая подпись издателя:
/// тот, кто может менять папку программы, может изменить и индекс.
/// </summary>
public sealed class BundledIndex
{
    public const string FileName = "bundled.json";
    public const string Format = "backpack-bundled";

    private BundledIndex(IReadOnlyList<BundledEntry> verified, IReadOnlyList<string> problems)
    {
        Verified = verified;
        Problems = problems;
    }

    public static BundledIndex Empty { get; } = new([], []);

    /// <summary>Пакеты, прошедшие проверку.</summary>
    public IReadOnlyList<BundledEntry> Verified { get; }

    /// <summary>Отклонённые записи (для диагностики): нет файла, другой хеш, несовпадение manifest.</summary>
    public IReadOnlyList<string> Problems { get; }

    public bool Contains(string id) => Verified.Any(e => e.Id == id);

    /// <summary>Прочитать и проверить индекс в папке <paramref name="dir"/> (по умолчанию — как <see cref="PluginMigration.BundledPackages"/>).</summary>
    public static BundledIndex Load(string? dir = null)
    {
        dir ??= PluginMigration.BundledDirectory();
        string path = System.IO.Path.Combine(dir, FileName);
        var verified = new List<BundledEntry>();
        var problems = new List<string>();
        try
        {
            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o || (string?)o["format"] != Format || o["packages"] is not JsonArray list)
            {
                return Empty;
            }

            foreach (var n in list.OfType<JsonObject>().Take(32))
            {
                string? file = (string?)n["file"], id = (string?)n["id"], version = (string?)n["version"], sha = (string?)n["sha256"];
                if (file is null || id is null || version is null || sha is null || file != System.IO.Path.GetFileName(file) || !file.EndsWith(PluginStore.PackageExtension, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{file ?? "?"}: incomplete entry");
                    continue;
                }

                string pkg = System.IO.Path.Combine(dir, file);
                if (!File.Exists(pkg))
                {
                    problems.Add($"{file}: missing");
                    continue;
                }

                string actual = Sha256Of(pkg);
                if (!string.Equals(actual, sha, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{file}: SHA-256 differs");
                    continue;
                }

                if (PluginStore.Inspect(pkg).Manifest is not { } m || m.Id != id || m.Version != version)
                {
                    problems.Add($"{file}: manifest differs");
                    continue;
                }

                verified.Add(new BundledEntry(pkg, id, version, actual));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            problems.Add("index: unreadable");
        }

        return new BundledIndex(verified, problems);
    }

    public static string Sha256Of(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    /// <summary>Записать индекс для пакетов папки (используется сборкой поставки и тестами).</summary>
    public static void Write(string dir)
    {
        var packages = new JsonArray();
        foreach (var pkg in Directory.GetFiles(dir, "*" + PluginStore.PackageExtension).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (PluginStore.Inspect(pkg).Manifest is { } m)
            {
                packages.Add(new JsonObject { ["file"] = System.IO.Path.GetFileName(pkg), ["id"] = m.Id, ["version"] = m.Version, ["sha256"] = Sha256Of(pkg) });
            }
        }

        File.WriteAllText(System.IO.Path.Combine(dir, FileName), new JsonObject { ["format"] = Format, ["formatVersion"] = 1, ["packages"] = packages }
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
