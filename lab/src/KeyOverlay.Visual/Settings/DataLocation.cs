using System.IO;

namespace KeyOverlay.Visual.Settings;

/// <summary>Где лежат данные Backpack и как они туда попали.</summary>
/// <param name="Directory">Папка данных: settings.json, app.json, profiles\, styles\.</param>
/// <param name="Portable">Данные рядом с программой (portable-режим).</param>
/// <param name="MigratedFrom">Папка KeyOverlay, из которой при первом запуске перенесены данные (оригинал не изменяется).</param>
/// <param name="PortableUnavailable">Метка portable есть, но папка рядом с программой недоступна для записи.</param>
public sealed record DataLocation(string Directory, bool Portable, string? MigratedFrom, bool PortableUnavailable)
{
    public string SettingsPath => Path.Combine(Directory, "settings.json");
}

/// <summary>
/// Выбор папки данных. Запись в папку программы (например, Program Files) не требуется:
/// <list type="number">
/// <item><c>--settings=путь</c> — папка этого файла;</item>
/// <item>рядом с программой есть файл <see cref="PortableMarker"/> (или прежний <c>KeyOverlay.portable</c>) и папка <c>Data</c>
///       рядом доступна для записи — portable;</item>
/// <item>иначе — <c>%APPDATA%\Backpack</c>. Если там ещё нет настроек, а в прежней папке (<c>%APPDATA%\KeyOverlay</c>, затем
///       <c>%APPDATA%\KeyOverlay.Lab</c>) они есть, выполняется перенос (см. <see cref="MigrateFolder"/>).</item>
/// </list>
/// </summary>
public static class DataLocations
{
    public const string PortableMarker = "Backpack.portable";
    public const string LegacyPortableMarker = "KeyOverlay.portable";
    public const string ProductFolder = "Backpack";
    public const string PreviousFolder = "KeyOverlay";
    public const string LegacyFolder = "KeyOverlay.Lab";

    /// <summary>Файл в новой папке: откуда и когда перенесены данные (для «О программе» и повторного запуска).</summary>
    public const string MigrationNote = "migrated-from.txt";

    public static DataLocation Resolve(string? explicitSettingsPath, string programDirectory, string appDataRoot)
    {
        if (explicitSettingsPath is not null)
        {
            return new DataLocation(Path.GetDirectoryName(Path.GetFullPath(explicitSettingsPath))!, false, null, false);
        }

        bool portableUnavailable = false;
        if (File.Exists(Path.Combine(programDirectory, PortableMarker)) || File.Exists(Path.Combine(programDirectory, LegacyPortableMarker)))
        {
            string data = Path.Combine(programDirectory, "Data");
            if (IsWritable(data))
            {
                return new DataLocation(data, true, null, false);
            }

            portableUnavailable = true;
        }

        string roaming = Path.Combine(appDataRoot, ProductFolder);
        string? migrated = null;
        if (!File.Exists(Path.Combine(roaming, "settings.json")))
        {
            foreach (var old in new[] { PreviousFolder, LegacyFolder })
            {
                string source = Path.Combine(appDataRoot, old);
                if (File.Exists(Path.Combine(source, "settings.json")) && MigrateFolder(source, roaming))
                {
                    migrated = source;
                    break;
                }
            }
        }

        return new DataLocation(roaming, false, migrated, portableUnavailable);
    }

    /// <summary>
    /// Перенос KeyOverlay → Backpack: settings.json, app.json, profiles\*.json, styles\*.json копируются во временную папку
    /// <c>Backpack.migrating</c> рядом с целевой; только после полного успеха она переименовывается в целевую (одна операция
    /// на томе). Исходная папка не изменяется и остаётся резервной копией. Незавершённая попытка (осталась временная папка)
    /// при следующем запуске удаляется и повторяется — перенос повторяем. Если целевая папка уже существует (например,
    /// без settings.json), файлы дописываются в неё без перезаписи существующих. Ошибка — false, прежние данные на месте.
    /// </summary>
    public static bool MigrateFolder(string source, string target)
    {
        string staging = target + ".migrating";
        try
        {
            if (System.IO.Directory.Exists(staging))
            {
                System.IO.Directory.Delete(staging, recursive: true);
            }

            System.IO.Directory.CreateDirectory(staging);
            foreach (var name in new[] { "settings.json", "app.json" })
            {
                string src = Path.Combine(source, name);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(staging, name));
                }
            }

            foreach (var sub in new[] { "profiles", "styles" })
            {
                string dir = Path.Combine(source, sub);
                if (!System.IO.Directory.Exists(dir))
                {
                    continue;
                }

                string to = Path.Combine(staging, sub);
                System.IO.Directory.CreateDirectory(to);
                foreach (var f in System.IO.Directory.GetFiles(dir, "*.json"))
                {
                    File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
                }
            }

            File.WriteAllText(Path.Combine(staging, MigrationNote), $"{source}\n{DateTime.UtcNow:O}\n");

            if (!System.IO.Directory.Exists(target))
            {
                System.IO.Directory.Move(staging, target);
                return true;
            }

            // Целевая папка уже есть (без настроек): дописать отсутствующие файлы, существующие не трогать.
            foreach (var f in System.IO.Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
            {
                string dst = Path.Combine(target, Path.GetRelativePath(staging, f));
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                if (!File.Exists(dst))
                {
                    File.Copy(f, dst);
                }
            }

            System.IO.Directory.Delete(staging, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не удалось — продолжаем с чистой папкой; прежние файлы на месте, временная папка удалится при следующей попытке.
            return false;
        }
    }

    /// <summary>Пробная запись во временный файл (создание папки и файла, затем удаление).</summary>
    public static bool IsWritable(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N")[..8]);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
