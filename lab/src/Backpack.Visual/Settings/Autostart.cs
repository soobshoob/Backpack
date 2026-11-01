using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Backpack.Visual.Settings;

/// <summary>Состояние автозапуска с Windows для этой копии программы.</summary>
public enum AutostartState
{
    /// <summary>Записи автозапуска нет.</summary>
    Off,

    /// <summary>Запись указывает на эту копию и не отключена в Windows.</summary>
    On,

    /// <summary>Запись указывает на другую существующую копию Backpack.</summary>
    OtherCopy,

    /// <summary>Запись указывает на файл, которого больше нет (папку перенесли или удалили).</summary>
    Missing,

    /// <summary>Запись этой копии есть, но автозапуск отключён в Windows («Автозагрузка» диспетчера задач или «Параметры»).</summary>
    DisabledInWindows,
}

/// <summary>Состояние и путь из записи (null — записи нет или её не удалось разобрать).</summary>
public sealed record AutostartStatus(AutostartState State, string? RegisteredPath);

/// <summary>
/// Автозапуск с Windows без прав администратора и без установщика: значение <see cref="ValueName"/> в разделе
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> — <c>"…\Backpack.exe" --autostart</c> (запуск в области уведомлений,
/// без главного окна). Работает и для portable-копии: путь — где лежит исполняемый файл сейчас.
/// <list type="bullet">
/// <item>Состояние читается из реестра каждый раз — переключатель показывает настоящее состояние, а не сохранённую настройку;
///       учитывается и отключение в Windows (<c>…\Explorer\StartupApproved\Run</c>: первый байт с установленным младшим битом).</item>
/// <item>Включение записывает команду этой копии и снимает отключение в Windows; выключение удаляет обе записи.</item>
/// <item><see cref="Repair"/> при обычном запуске: запись указывает на отсутствующий файл (папку перенесли, новую версию
///       распаковали в другое место) или на эту копию с устаревшей командой — запись переводится на эту копию. Запись другой
///       существующей копии не перехватывается.</item>
/// </list>
/// Раздел реестра задаётся конструктором (тесты используют свой раздел, не настоящий автозапуск).
/// </summary>
public sealed class Autostart
{
    public const string ValueName = "Backpack";
    public const string Argument = "--autostart";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _runKey;
    private readonly string _approvedKey;

    public Autostart(string exePath, string runKey = RunKey, string approvedKey = ApprovedKey)
    {
        ExePath = Path.GetFullPath(exePath);
        _runKey = runKey;
        _approvedKey = approvedKey;
    }

    /// <summary>Исполняемый файл этой копии.</summary>
    public string ExePath { get; }

    /// <summary>Команда записи автозапуска для этой копии.</summary>
    public string Command => $"\"{ExePath}\" {Argument}";

    public AutostartStatus Status()
    {
        string? command = ReadCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return new AutostartStatus(AutostartState.Off, null);
        }

        string? path = PathOf(command);
        if (path is null || !File.Exists(path))
        {
            return new AutostartStatus(AutostartState.Missing, path);
        }

        if (!SamePath(path, ExePath))
        {
            return new AutostartStatus(AutostartState.OtherCopy, path);
        }

        return new AutostartStatus(DisabledInWindows() ? AutostartState.DisabledInWindows : AutostartState.On, path);
    }

    /// <summary>Включить для этой копии. Исключения реестра (нет доступа) передаются вызывающему.</summary>
    public void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(_runKey, writable: true))
        {
            run.SetValue(ValueName, Command, RegistryValueKind.String);
        }

        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey, writable: true);
        if (approved?.GetValue(ValueName) is byte[] { Length: > 0 } b && (b[0] & 1) == 1)
        {
            // Пользователь включает автозапуск здесь явно — снять отключение «Автозагрузки» (формат Windows: 12 байт, 0x02 — включено).
            approved.SetValue(ValueName, new byte[12] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }
    }

    /// <summary>Выключить: удалить запись автозапуска (любой копии — переключатель один) и отметку Windows о ней.</summary>
    public void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(_runKey, writable: true))
        {
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Включить или выключить; false и текст ошибки — если реестр недоступен для записи.</summary>
    public bool TrySet(bool on, out string? error)
    {
        error = null;
        try
        {
            if (on)
            {
                Enable();
            }
            else
            {
                Disable();
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            error = ex.GetType().Name;
            return false;
        }
    }

    /// <summary>
    /// Запуск этой копии: запись на отсутствующий файл или устаревшая команда этой копии → команда этой копии. true — запись
    /// обновлена. Ошибки реестра не мешают запуску.
    /// </summary>
    public bool Repair()
    {
        try
        {
            var status = Status();
            bool stale = status.State == AutostartState.Missing
                || (status.State is AutostartState.On or AutostartState.DisabledInWindows && ReadCommand() != Command);
            if (!stale)
            {
                return false;
            }

            using var run = Registry.CurrentUser.CreateSubKey(_runKey, writable: true);
            run.SetValue(ValueName, Command, RegistryValueKind.String); // отключение в Windows (если было) сохраняется
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    private string? ReadCommand()
    {
        using var run = Registry.CurrentUser.OpenSubKey(_runKey);
        return run?.GetValue(ValueName) as string;
    }

    private bool DisabledInWindows()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKey);
        return approved?.GetValue(ValueName) is byte[] { Length: > 0 } b && (b[0] & 1) == 1;
    }

    /// <summary>Путь исполняемого файла из командной строки записи: в кавычках или до «.exe».</summary>
    internal static string? PathOf(string command)
    {
        string c = Environment.ExpandEnvironmentVariables(command.Trim());
        if (c.StartsWith('"'))
        {
            int end = c.IndexOf('"', 1);
            return end > 1 ? c[1..end] : null;
        }

        int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? c[..(exe + 4)] : c.Split(' ')[0];
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
