using System.IO;
using System.Windows.Threading;
using Backpack.Visual.Localization;

namespace Backpack.Visual.Settings;

/// <summary>
/// Текущие настройки в памяти (UI-поток) + файл. Изменения применяются сразу через <see cref="Changed"/>,
/// запись на диск — не чаще раза в <see cref="SaveDelay"/> и при <see cref="Flush"/>.
///
/// Запись: временный файл рядом → <see cref="File.Move(string, string, bool)"/> с заменой (одна операция MoveFileEx на томе).
/// Если исходный файл был повреждён или создан более новой версией, перед первой записью он копируется в
/// <c>settings.unreadable.bak.json</c>, а файл старого формата — в <c>settings.v1.bak.json</c>, чтобы ничего не терялось молча.
/// </summary>
public sealed class SettingsStore
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private DispatcherTimer? _saveTimer;
    private bool _dirty;
    private string? _backupName;

    private SettingsStore(string path, SettingsReadResult read)
    {
        _path = path;
        Current = read.Settings;
        LoadProblems = read.Problems;
        MigratedFrom = read.MigratedFrom;
        _backupName = !File.Exists(path) ? null
            : read.FileUnusable ? "settings.unreadable.bak.json"
            : read.MigratedFrom is { } v ? $"settings.v{v}.bak.json"
            : null;
    }

    /// <summary>Путь по умолчанию без portable-режима (полное правило выбора — <see cref="DataLocations"/>).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DataLocations.ProductFolder, "settings.json");

    /// <summary>Никогда не бросает исключение: при любой проблеме — значения по умолчанию и сообщение.</summary>
    public static SettingsStore Load(string path)
    {
        SettingsReadResult read;
        try
        {
            read = File.Exists(path)
                ? SettingsSerializer.Read(File.ReadAllText(path))
                : new SettingsReadResult(Styles.BrandStyles.Starter, [], false); // чистая установка: фирменные кейкапы и мышь (Phase 4I)
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            read = new SettingsReadResult(Styles.BrandStyles.Starter, [new LocText("Settings.Problem.ReadFailed", ex.GetType().Name)], true);
        }

        return new SettingsStore(path, read);
    }

    public string FilePath => _path;

    public LabSettings Current { get; private set; }

    public IReadOnlyList<LocText> LoadProblems { get; }

    /// <summary>Версия формата файла, если он был обновлён при чтении (исходный файл копируется перед первой записью).</summary>
    public int? MigratedFrom { get; }

    public LocText? LastSaveError { get; private set; }

    public event Action<LabSettings>? Changed;

    /// <summary>Попытка записи завершена (успешно или с ошибкой — см. <see cref="LastSaveError"/>).</summary>
    public event Action? Saved;

    /// <summary>Для проверок утечек: число подписчиков <see cref="Changed"/>.</summary>
    public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;

    public void Update(Func<LabSettings, LabSettings> change)
    {
        var next = change(Current);
        if (next == Current)
        {
            return;
        }

        Current = next;
        _dirty = true;
        Changed?.Invoke(next);
        ScheduleSave();
    }

    public void Flush()
    {
        _saveTimer?.Stop();
        if (_dirty)
        {
            Save();
        }
    }

    private void ScheduleSave()
    {
        if (Dispatcher.FromThread(Thread.CurrentThread) is null)
        {
            return; // без диспетчера (тесты) — только Flush
        }

        _saveTimer ??= new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => Flush(), Dispatcher.CurrentDispatcher) { IsEnabled = false };
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (_backupName is { } backup)
            {
                File.Copy(_path, Path.Combine(Path.GetDirectoryName(_path)!, backup), overwrite: true);
                _backupName = null;
            }

            string tmp = _path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var w = new StreamWriter(fs))
            {
                w.Write(SettingsSerializer.Write(Current));
            }

            File.Move(tmp, _path, overwrite: true);
            _dirty = false;
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = new LocText("Settings.Problem.SaveFailed", ex.GetType().Name);
        }

        Saved?.Invoke();
    }
}
