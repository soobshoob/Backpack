using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Profiles;

public sealed record ProfileInfo(string Id, string Name);

/// <summary>Полностью проверенный профиль. Создаётся только при отсутствии ошибок.</summary>
/// <param name="Styles">
/// Пользовательские стили, вложенные в экспортированный файл (только при экспорте и импорте; в папке профилей не хранятся —
/// там стили лежат в каталоге). Оформление профиля от них не зависит: параметры записаны в <see cref="Settings"/> полностью.
/// </param>
public sealed record Profile(string Id, string Name, LabSettings Settings, IReadOnlyList<Styles.StyleEntry>? Styles = null);

/// <summary>Результат проверки: профиль целиком или список ошибок (тогда ничего не применяется).</summary>
public sealed record ProfileReadResult(Profile? Profile, IReadOnlyList<LocText> Errors, int? MigratedSettingsFrom = null, int FileVersion = ProfileSerializer.FormatVersion)
{
    public bool Ok => Profile is not null && Errors.Count == 0;
}

/// <summary>
/// Файл профиля <c>backpack-profile</c>, версия 1 (схема docs/settings/backpack-profile.schema.json):
/// <c>{format, formatVersion, id, name, settings}</c>, где <c>settings</c> — объект файла настроек (формат 1 или 2,
/// с той же миграцией). Профиль хранит оформление, поведение ряда, мышь, индикатор раскладки и размещение —
/// но не язык и тему интерфейса, не поведение трея и не источник захвата. История ввода не хранится; код и XAML не загружаются.
/// Проверка строгая: любая ошибка или неизвестное поле — отказ целиком, без частичного применения.
/// </summary>
public static partial class ProfileSerializer
{
    public const string Format = "backpack-profile";

    /// <summary>
    /// Версия 3 (Phase 4F): та же структура, что у версии 2 (настройки и необязательный массив <c>styles</c>), но файл записан
    /// моделью автосохранения — профиль совпадает с применёнными настройками. Версии 1 и 2 читаются; профиль версии ниже 3
    /// при первом запуске переносится без потери (см. <see cref="ProfileSession"/>).
    /// </summary>
    public const int FormatVersion = 3;
    public const int MaxEmbeddedStyles = 16;
    public const int MaxFileBytes = 256 * 1024;

    public static bool IsValidId(string? id) => id is not null && IdRegex().IsMatch(id);

    public static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 64;

    public static string Write(Profile p)
    {
        var o = new JsonObject
        {
            ["format"] = Format,
            ["formatVersion"] = FormatVersion,
            ["id"] = p.Id,
            ["name"] = p.Name,
            ["settings"] = SettingsSerializer.ToJson(p.Settings),
        };
        if (p.Styles is { Count: > 0 } styles)
        {
            o["styles"] = new JsonArray(styles.Select(st => (JsonNode)Styles.StyleSerializer.ToJson(st)).ToArray());
        }

        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static ProfileReadResult Read(string json)
    {
        if (json.Length > MaxFileBytes)
        {
            return Fail(new LocText("Profile.Error.TooLarge"));
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException ex)
        {
            return Fail(new LocText("Profile.Error.Syntax", (ex.LineNumber ?? 0) + 1));
        }

        if (root is not JsonObject o)
        {
            return Fail(new LocText("Profile.Error.NotProfile"));
        }

        if ((o["format"] as JsonValue)?.TryGetValue(out string? format) != true || format != Format)
        {
            return Fail(new LocText("Profile.Error.NotProfile"));
        }

        if (o["formatVersion"] is not JsonValue v || !v.TryGetValue(out int version))
        {
            return Fail(new LocText("Profile.Error.NoVersion"));
        }

        if (version is not (1 or 2 or FormatVersion))
        {
            return Fail(version > FormatVersion ? new LocText("Profile.Error.Newer", version) : new LocText("Profile.Error.Unsupported", version));
        }

        var errors = new List<LocText>();
        foreach (var (key, _) in o)
        {
            if (key is not ("format" or "formatVersion" or "id" or "name" or "settings") && !(version >= 2 && key == "styles"))
            {
                errors.Add(new LocText("Settings.Problem.UnknownField", key));
            }
        }

        var styles = new List<Styles.StyleEntry>();
        if (o["styles"] is { } stylesNode)
        {
            if (stylesNode is not JsonArray arr || arr.Count > MaxEmbeddedStyles)
            {
                errors.Add(new LocText("Profile.Error.Styles"));
            }
            else
            {
                foreach (var item in arr)
                {
                    var sr = item is JsonObject so ? Styles.StyleSerializer.Read(so) : new Styles.StyleReadResult(null, [new LocText("Style.Error.NotStyle")]);
                    if (sr.Ok)
                    {
                        styles.Add(sr.Style!);
                    }
                    else
                    {
                        errors.AddRange(sr.Errors);
                    }
                }
            }
        }

        string? id = (o["id"] as JsonValue)?.TryGetValue(out string? i) == true ? i : null;
        string? name = (o["name"] as JsonValue)?.TryGetValue(out string? n) == true ? n?.Trim() : null;
        if (!IsValidId(id))
        {
            errors.Add(new LocText("Profile.Error.Id"));
        }

        if (!IsValidName(name))
        {
            errors.Add(new LocText("Profile.Error.Name"));
        }

        if (o["settings"] is not JsonObject settings)
        {
            errors.Add(new LocText("Profile.Error.NoSettings"));
            return new ProfileReadResult(null, errors);
        }

        var read = SettingsSerializer.Read(settings.ToJsonString());
        errors.AddRange(read.Problems);
        if (read.FileUnusable || errors.Count > 0)
        {
            return new ProfileReadResult(null, errors);
        }

        return new ProfileReadResult(new Profile(id!, name!, read.Settings, styles.Count > 0 ? styles : null), [], read.MigratedFrom, version);
    }

    public static string NewId() => "p-" + Guid.NewGuid().ToString("N")[..10];

    private static ProfileReadResult Fail(LocText error) => new(null, [error]);

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,62}[a-z0-9]$")]
    private static partial Regex IdRegex();
}

/// <summary>Папка профилей: один файл <c>&lt;id&gt;.json</c> на профиль, безопасная запись, строгая проверка при чтении.</summary>
public sealed class ProfileStore
{
    private readonly string _dir;

    public ProfileStore(string directory, Styles.StyleStore? styles = null)
    {
        _dir = directory;
        Styles = styles;
    }

    public string Directory => _dir;

    /// <summary>Каталог пользовательских стилей для вложения при экспорте и переноса при импорте (может отсутствовать).</summary>
    public Styles.StyleStore? Styles { get; }

    public static string DirectoryNextTo(string settingsPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "profiles");

    /// <summary>Профили, которые читаются без ошибок, по имени. Повреждённые файлы пропускаются (и не удаляются).</summary>
    public IReadOnlyList<ProfileInfo> List()
    {
        if (!System.IO.Directory.Exists(_dir))
        {
            return [];
        }

        var list = new List<ProfileInfo>();
        foreach (var f in System.IO.Directory.GetFiles(_dir, "*.json"))
        {
            var r = TryRead(f);
            if (r.Ok && Path.GetFileNameWithoutExtension(f) == r.Profile!.Id)
            {
                list.Add(new ProfileInfo(r.Profile.Id, r.Profile.Name));
            }
        }

        return list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public ProfileReadResult Load(string id) =>
        ProfileSerializer.IsValidId(id) && File.Exists(PathOf(id)) ? TryRead(PathOf(id)) : new ProfileReadResult(null, [new LocText("Profile.Error.Missing")]);

    public void Save(Profile p) => AtomicFile.Write(PathOf(p.Id), ProfileSerializer.Write(p with { Styles = null }));

    public Profile Create(string name, LabSettings settings)
    {
        var p = new Profile(ProfileSerializer.NewId(), UniqueName(name.Trim()), settings);
        Save(p);
        return p;
    }

    public Profile? Rename(string id, string name)
    {
        var r = Load(id);
        if (!r.Ok)
        {
            return null;
        }

        var p = r.Profile! with { Name = UniqueName(name.Trim(), except: id) };
        Save(p);
        return p;
    }

    /// <summary>
    /// Профиль неизменяем: файл помечен «только чтение» (так поставляются эталонные профили). Автосохранение в него не пишет,
    /// а создаёт пользовательскую рабочую копию.
    /// </summary>
    public bool IsReadOnly(string id)
    {
        try
        {
            return ProfileSerializer.IsValidId(id) && File.Exists(PathOf(id)) && File.GetAttributes(PathOf(id)).HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Delete(string id)
    {
        if (ProfileSerializer.IsValidId(id) && File.Exists(PathOf(id)))
        {
            File.Delete(PathOf(id));
        }
    }

    /// <summary>Импорт: файл проверяется целиком; при успехе профиль сохраняется под новым идентификатором (без перезаписи).</summary>
    public ProfileReadResult Import(string path, Rect workArea)
    {
        ProfileReadResult r;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > ProfileSerializer.MaxFileBytes)
            {
                return new ProfileReadResult(null, [new LocText("Profile.Error.TooLarge")]);
            }

            r = ProfileSerializer.Read(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProfileReadResult(null, [new LocText("Profile.Error.ReadFailed", ex.GetType().Name)]);
        }

        if (!r.Ok)
        {
            return r;
        }

        var safe = PlacementGuard.EnsureVisible(r.Profile!.Settings, workArea);
        safe = ImportStyles(safe, r.Profile.Styles);
        var p = new Profile(ProfileSerializer.NewId(), UniqueName(r.Profile.Name), safe);
        Save(p);
        return new ProfileReadResult(p, [], r.MigratedSettingsFrom);
    }

    /// <summary>
    /// Вложенные стили переносятся в каталог (конфликты — см. <see cref="Styles.StyleStore.Import(Styles.StyleReadResult)"/>),
    /// ссылки настроек переписываются на итоговые идентификаторы. Без каталога ссылки остаются как есть: оформление всё равно
    /// полностью в настройках профиля.
    /// </summary>
    private LabSettings ImportStyles(LabSettings s, IReadOnlyList<Styles.StyleEntry>? embedded)
    {
        if (Styles is null || embedded is null)
        {
            return s;
        }

        var map = new Dictionary<string, string>();
        foreach (var style in embedded)
        {
            var result = Styles.Import(new Styles.StyleReadResult(style, []));
            if (result.Ok)
            {
                map[style.Id] = result.Style!.Id;
            }
        }

        string? Remap(string? id) => id is not null && map.TryGetValue(id, out var to) ? to : id;
        var a = s.Appearance;
        return s with
        {
            Appearance = a with
            {
                KeycapStyleId = Remap(a.KeycapStyleId),
                Indicator = a.IndicatorLook with { StyleId = Remap(a.IndicatorLook.StyleId) },
                Mouse = a.MouseLook with { StyleId = Remap(a.MouseLook.StyleId) },
            },
        };
    }

    /// <summary>Экспорт: вместе с профилем — пользовательские стили, на которые ссылаются его настройки и которые есть в каталоге.</summary>
    public void Export(Profile p, string path) => AtomicFile.Write(path, ProfileSerializer.Write(p with { Styles = ReferencedStyles(p.Settings) }));

    public IReadOnlyList<Styles.StyleEntry> ReferencedStyles(LabSettings s)
    {
        if (Styles is null)
        {
            return [];
        }

        var ids = new[] { s.Appearance.KeycapStyleId, s.Appearance.IndicatorLook.StyleId, s.Appearance.MouseLook.StyleId };
        return ids.Where(id => id is not null && !Backpack.Visual.Styles.StyleIds.IsBuiltIn(id))
            .Distinct()
            .Select(id => Styles.Load(id!))
            .Where(r => r.Ok)
            .Select(r => r.Style!)
            .ToList();
    }

    public string UniqueName(string name, string? except = null)
    {
        var names = List().Where(p => p.Id != except).Select(p => p.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(name))
        {
            return name;
        }

        for (int i = 2; ; i++)
        {
            string candidate = $"{name} ({i})";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private string PathOf(string id) => Path.Combine(_dir, id + ".json");

    private static ProfileReadResult TryRead(string file)
    {
        try
        {
            return ProfileSerializer.Read(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ProfileReadResult(null, [new LocText("Profile.Error.ReadFailed", ex.GetType().Name)]);
        }
    }
}

/// <summary>
/// Перенос профиля на другой компьютер: смещения, которые выводят группу за рабочую область этого экрана,
/// уменьшаются до её размеров. Группа и так не выходит за край (ограничение при размещении), это защищает от
/// бессмысленно больших значений и «прилипания» к краю.
/// </summary>
public static class PlacementGuard
{
    public static LabSettings EnsureVisible(LabSettings s, Rect workArea)
    {
        double maxX = Math.Max(0, workArea.Width / 2), maxY = Math.Max(0, workArea.Height / 2);
        var p = s.Placement;
        var fixedP = p with
        {
            OffsetX = Math.Clamp(p.OffsetX, -maxX, maxX),
            OffsetY = Math.Clamp(p.OffsetY, -maxY, maxY),
            EdgeX = Math.Min(p.EdgeX, Math.Max(0, workArea.Width / 3)),
            EdgeY = Math.Min(p.EdgeY, Math.Max(0, workArea.Height / 3)),
        };
        var li = s.LayoutIndicator;
        var fixedLi = li with { OffsetX = Math.Clamp(li.OffsetX, -maxX, maxX), OffsetY = Math.Clamp(li.OffsetY, -maxY, maxY) };
        return s with { Placement = fixedP, LayoutIndicator = fixedLi };
    }
}

/// <summary>
/// Активный профиль и автосохранение (Phase 4F). Рабочие настройки (<see cref="SettingsStore"/>) и активный профиль — одно и то же:
/// каждое принятое изменение применяется сразу и записывается в файл профиля. Частые изменения (ползунок) объединяются — запись
/// не чаще раза в <see cref="SaveDelay"/>; при переключении профиля, экспорте и выходе ожидающая запись выполняется немедленно
/// (<see cref="Flush"/>). Ошибка записи не выдаётся за успех: <see cref="SaveError"/>, событие <see cref="SaveFailed"/> и
/// повтор (<see cref="Flush"/>). Неизменяемый профиль (<see cref="ProfileStore.IsReadOnly"/>) не перезаписывается — при первом
/// изменении создаётся пользовательская рабочая копия, и она становится активной.
/// <para>
/// Перенос прежней модели (профили формата 1–2, «Сохранить / Не сохранять»): если применённые настройки отличались от
/// сохранённого профиля, сохранённая версия остаётся отдельным профилем (<see cref="LegacyBackupName"/>), а активный профиль
/// получает применённые настройки. Ничего не теряется.
/// </para>
/// </summary>
public sealed class ProfileSession : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ProfileStore _profiles;
    private readonly SettingsStore _settings;
    private readonly AppPreferencesStore _prefs;
    private readonly Func<Rect> _workArea;
    private readonly Func<string, string> _copyName;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private Profile? _active;
    private bool _pending;
    private bool _switching;
    private bool _disposed;

    /// <param name="copyName">Имя рабочей копии неизменяемого профиля и сохранённой версии при переносе (по имени исходного).</param>
    public ProfileSession(ProfileStore profiles, SettingsStore settings, AppPreferencesStore prefs, Func<Rect> workArea, string defaultName,
        Func<string, string>? copyName = null)
    {
        _profiles = profiles;
        _settings = settings;
        _prefs = prefs;
        _workArea = workArea;
        _copyName = copyName ?? (n => n + " (2)");

        ProfileReadResult? loaded = null;
        if (prefs.Current.ActiveProfileId is { } id && profiles.Load(id) is { Ok: true } r)
        {
            loaded = r;
        }
        else if (profiles.List() is { Count: > 0 } list && profiles.Load(list[0].Id) is { Ok: true } first)
        {
            loaded = first;
            prefs.Update(p => p with { ActiveProfileId = first.Profile!.Id });
        }

        if (loaded is null)
        {
            // Первый запуск: текущие рабочие настройки становятся первым профилем.
            _active = profiles.Create(defaultName, settings.Current);
            prefs.Update(p => p with { ActiveProfileId = _active.Id });
        }
        else
        {
            _active = loaded.Profile!;
            if (_active.Settings != settings.Current)
            {
                if (loaded.FileVersion < ProfileSerializer.FormatVersion && !_profiles.IsReadOnly(_active.Id))
                {
                    // Прежняя модель: сохранённая версия профиля остаётся отдельным профилем.
                    var backup = _profiles.Create(_copyName(_active.Name), _active.Settings);
                    LegacyBackupName = backup.Name;
                }

                Accept(settings.Current); // применённые настройки — актуальное состояние профиля
                Flush();
            }
        }

        _settings.Changed += OnSettingsChanged;
    }

    public event Action? Changed;

    /// <summary>Запись профиля не удалась (сообщение — <see cref="SaveError"/>; изменения остаются применёнными и ждут повтора).</summary>
    public event Action? SaveFailed;

    public ProfileStore Store => _profiles;

    public Profile? Active => _active;

    public string ActiveName => _active?.Name ?? "";

    /// <summary>Есть изменения, ещё не записанные в файл профиля (ожидание объединения или ошибка записи).</summary>
    public bool HasPendingWrite => _pending;

    /// <summary>Последняя ошибка записи (null — записано или нечего записывать).</summary>
    public LocText? SaveError { get; private set; }

    /// <summary>Имя профиля с сохранённой версией, созданного при переносе со старой модели (для сообщения при запуске).</summary>
    public string? LegacyBackupName { get; }

    /// <summary>Имя рабочей копии, созданной вместо записи в неизменяемый профиль (последняя; для уведомления).</summary>
    public string? WorkingCopyName { get; private set; }

    public IReadOnlyList<ProfileInfo> List() => _profiles.List();

    private void OnSettingsChanged(LabSettings s)
    {
        if (_switching || _disposed)
        {
            return;
        }

        Accept(s);
        Schedule();
        Changed?.Invoke();
    }

    /// <summary>Новое состояние активного профиля в памяти; неизменяемый профиль заменяется рабочей копией.</summary>
    private void Accept(LabSettings s)
    {
        if (_active is null)
        {
            return;
        }

        if (_profiles.IsReadOnly(_active.Id))
        {
            var copy = new Profile(ProfileSerializer.NewId(), _profiles.UniqueName(_copyName(_active.Name)), s);
            _active = copy;
            WorkingCopyName = copy.Name;
            _prefs.Update(p => p with { ActiveProfileId = copy.Id });
        }
        else
        {
            _active = _active with { Settings = s };
        }

        _pending = true;
    }

    private void Schedule()
    {
        if (System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread) is not { } dispatcher)
        {
            Flush(); // без диспетчера (тесты, служебные запуски) — сразу
            return;
        }

        _timer ??= new System.Windows.Threading.DispatcherTimer(SaveDelay, System.Windows.Threading.DispatcherPriority.Background, (_, _) => Flush(), dispatcher) { IsEnabled = false };
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Записать ожидающие изменения сейчас. false — запись не удалась (<see cref="SaveError"/>); можно повторить.</summary>
    public bool Flush()
    {
        _timer?.Stop();
        if (!_pending || _active is null)
        {
            return SaveError is null;
        }

        try
        {
            _profiles.Save(_active);
            _pending = false;
            bool hadError = SaveError is not null;
            SaveError = null;
            if (hadError)
            {
                Changed?.Invoke();
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = new LocText("Profiles.Error.SaveFailed", _active.Name, ex.GetType().Name);
            SaveFailed?.Invoke();
            Changed?.Invoke();
            return false;
        }
    }

    /// <summary>Отписка и запись ожидающих изменений (выход из приложения).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Flush();
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _timer?.Stop();
    }

    /// <summary>Новый профиль со значениями по умолчанию (рабочие настройки при этом не меняются).</summary>
    public Profile CreateDefault(string name) => _profiles.Create(name, Styles.BrandStyles.Starter);

    /// <summary>Копия профиля; копия текущего — с актуальными (в том числе ещё не записанными) настройками.</summary>
    public Profile? Duplicate(string id, Func<string, string> copyName)
    {
        if (_active?.Id == id)
        {
            return _profiles.Create(copyName(_active.Name), _settings.Current);
        }

        var r = _profiles.Load(id);
        return r.Ok ? _profiles.Create(copyName(r.Profile!.Name), r.Profile.Settings) : null;
    }

    public void Rename(string id, string name)
    {
        if (_active?.Id == id && !Flush())
        {
            return;
        }

        var p = _profiles.Rename(id, name);
        if (p is not null && _active?.Id == id)
        {
            _active = _active with { Name = p.Name };
        }

        Changed?.Invoke();
    }

    /// <summary>Удаление. Активный профиль удалить нельзя — сначала переключитесь на другой.</summary>
    public bool Delete(string id)
    {
        if (_active?.Id == id)
        {
            return false;
        }

        _profiles.Delete(id);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Переключение. Ожидающие изменения текущего профиля сначала записываются; если запись не удалась, переключения нет
    /// (ошибка — в результате, изменения не теряются). Новый профиль проверяется целиком; при ошибке рабочие настройки не меняются.
    /// </summary>
    public (bool Switched, IReadOnlyList<LocText> Errors) SwitchTo(string id)
    {
        if (!Flush())
        {
            return (false, [SaveError!]);
        }

        var r = _profiles.Load(id);
        if (!r.Ok)
        {
            return (false, r.Errors);
        }

        // Если положение пришлось подправить под этот экран, профиль на диске не меняется до следующего изменения.
        _active = r.Profile!;
        var visible = PlacementGuard.EnsureVisible(_active.Settings, _workArea());
        _prefs.Update(p => p with { ActiveProfileId = _active.Id });
        _switching = true;
        try
        {
            _settings.Update(_ => visible);
        }
        finally
        {
            _switching = false;
        }

        _active = _active with { Settings = _settings.Current };
        Changed?.Invoke();
        return (true, []);
    }

    /// <summary>Экспорт текущего профиля: актуальное состояние всех настроек (ожидающие изменения учитываются).</summary>
    public void ExportCurrent(string path)
    {
        Flush();
        var current = (_active ?? new Profile(ProfileSerializer.NewId(), "Backpack", _settings.Current)) with { Settings = _settings.Current };
        _profiles.Export(current, path);
    }

    public ProfileReadResult Import(string path) => _profiles.Import(path, _workArea());
}
