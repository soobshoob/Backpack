using System.IO;

namespace Backpack.Visual.Settings;

/// <summary>Где лежат данные Backpack.</summary>
/// <param name="Directory">Папка данных: settings.json, app.json, profiles\, styles\.</param>
/// <param name="Portable">Данные рядом с программой (portable-режим).</param>
/// <param name="PortableUnavailable">Метка portable есть, но папка рядом с программой недоступна для записи.</param>
public sealed record DataLocation(string Directory, bool Portable, bool PortableUnavailable)
{
    public string SettingsPath => Path.Combine(Directory, "settings.json");
}

/// <summary>
/// Выбор папки данных. Запись в папку программы (например, Program Files) не требуется:
/// <list type="number">
/// <item><c>--settings=путь</c> — папка этого файла;</item>
/// <item>рядом с программой есть файл <see cref="PortableMarker"/> и папка <c>Data</c> рядом доступна для записи — portable;</item>
/// <item>иначе — <c>%APPDATA%\Backpack</c>.</item>
/// </list>
/// </summary>
public static class DataLocations
{
    public const string PortableMarker = "Backpack.portable";
    public const string ProductFolder = "Backpack";

    public static DataLocation Resolve(string? explicitSettingsPath, string programDirectory, string appDataRoot)
    {
        if (explicitSettingsPath is not null)
        {
            return new DataLocation(Path.GetDirectoryName(Path.GetFullPath(explicitSettingsPath))!, false, false);
        }

        bool portableUnavailable = false;
        if (File.Exists(Path.Combine(programDirectory, PortableMarker)))
        {
            string data = Path.Combine(programDirectory, "Data");
            if (IsWritable(data))
            {
                return new DataLocation(data, true, false);
            }

            portableUnavailable = true;
        }

        return new DataLocation(Path.Combine(appDataRoot, ProductFolder), false, portableUnavailable);
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
