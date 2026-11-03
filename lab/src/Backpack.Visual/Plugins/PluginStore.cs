using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Backpack.Sdk;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Plugins;

/// <summary>Установленный плагин: manifest, папка, состояние на этом компьютере (не часть профилей).</summary>
public sealed record InstalledPlugin(PluginManifest Manifest, string Directory, bool Enabled, bool Trusted, bool Favorite, int RecentCrashes, string? DisabledReason, string? SymbolOverride = null)
{
    public string Id => Manifest.Id;

    /// <summary>Значок плагина для меню, карточки и шапки: выбор пользователя, иначе значок автора (manifest «symbol»), иначе «puzzle».</summary>
    public string Symbol => PluginSymbols.Resolve(Manifest.Symbol, SymbolOverride);
}

public enum ImportOutcome { Installed, Updated, Rejected }

public sealed record ImportResult(ImportOutcome Outcome, PluginManifest? Manifest, IReadOnlyList<string> Errors, string? PreviousVersion = null);

/// <summary>
/// Плагины на этом компьютере: <c>plugins\&lt;id&gt;\</c> (содержимое пакета) и <c>plugins\plugins.json</c> (включён,
/// доверие, избранное, сбои). Настройки плагина — <c>plugins\&lt;id&gt;.settings.json</c> (сохраняются при обновлении).
/// Импорт только проверяет и распаковывает: код не запускается, доверие не выдаётся.
/// </summary>
public sealed class PluginStore
{
    public const string PackageExtension = ".bppkg";
    public const long MaxPackageBytes = 64L * 1024 * 1024;
    public const long MaxEntryBytes = 32L * 1024 * 1024;
    public const int MaxEntries = 500;
    public const double MaxRatio = 100;
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(10);
    public const int CrashLimit = 3;

    private readonly string _root;
    private readonly Dictionary<string, State> _state = new(StringComparer.Ordinal);

    public PluginStore(string root)
    {
        _root = root;
        LoadState();
    }

    public static string DirectoryNextTo(string settingsPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "plugins");

    public string Root => _root;

    public event Action? Changed;

    private sealed class State
    {
        public bool Enabled { get; set; }

        public bool Trusted { get; set; }

        public bool Favorite { get; set; }

        public List<DateTime> Crashes { get; set; } = [];

        public string? DisabledReason { get; set; }

        /// <summary>SHA-256 поставляемого пакета, из которого установлена эта версия (Phase 4H, отметка «От создателя»); null — импорт пользователем.</summary>
        public string? BundledSha { get; set; }

        public string? BundledVersion { get; set; }

        /// <summary>Значок плагина, выбранный пользователем (Phase 4I; имя Lucide из каталога). Хранится по ID и переживает обновление пакета.</summary>
        public string? Symbol { get; set; }
    }

    /// <summary>Поставляемые плагины, которые пользователь удалил: при запуске они не устанавливаются снова (можно вернуть вручную).</summary>
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);

    public IReadOnlySet<string> DismissedBundled => _dismissed;

    /// <summary>
    /// Установленная версия плагина поставлена вместе с программой: пакет совпал по SHA-256 с индексом поставки
    /// (<see cref="BundledIndex"/>) и с тех пор не заменён другим пакетом. Это проверка происхождения файла, не подпись издателя.
    /// </summary>
    public bool IsBundled(InstalledPlugin p) =>
        _state.GetValueOrDefault(p.Id) is { BundledSha: not null } st && st.BundledVersion == p.Manifest.Version;

    /// <summary>Вернуть удалённый поставляемый плагин в список устанавливаемых при запуске.</summary>
    public void Undismiss(string id)
    {
        if (_dismissed.Remove(id))
        {
            SaveState();
        }
    }

    /// <summary>Установленные плагины с читаемым manifest (повреждённая папка пропускается).</summary>
    public IReadOnlyList<InstalledPlugin> List()
    {
        var list = new List<InstalledPlugin>();
        if (!Directory.Exists(_root))
        {
            return list;
        }

        foreach (var dir in Directory.GetDirectories(_root))
        {
            if (dir.EndsWith(".staging", StringComparison.Ordinal) || dir.EndsWith(".old", StringComparison.Ordinal))
            {
                continue;
            }

            string mf = Path.Combine(dir, PluginManifest.FileName);
            if (!File.Exists(mf))
            {
                continue;
            }

            var (m, _) = PluginManifest.Parse(File.ReadAllText(mf));
            if (m is null || !string.Equals(Path.GetFileName(dir), m.Id, StringComparison.Ordinal))
            {
                continue;
            }

            var st = _state.GetValueOrDefault(m.Id) ?? new State();
            list.Add(new InstalledPlugin(m, dir, st.Enabled && st.Trusted, st.Trusted, st.Favorite, RecentCrashes(st), st.DisabledReason, st.Symbol));
        }

        return list.OrderBy(p => p.Manifest.Name.For("en"), StringComparer.OrdinalIgnoreCase).ToList();
    }

    public InstalledPlugin? Get(string id) => List().FirstOrDefault(p => p.Id == id);

    /// <summary>Проверить пакет без установки: структура архива, размеры, пути, manifest, совместимость SDK.</summary>
    public static (PluginManifest? Manifest, List<string> Errors) Inspect(string packagePath)
    {
        var errors = new List<string>();
        try
        {
            var info = new FileInfo(packagePath);
            if (!info.Exists || info.Length == 0 || info.Length > MaxPackageBytes)
            {
                return (null, ["package: size"]);
            }

            using var zip = ZipFile.OpenRead(packagePath);
            return Inspect(zip);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return (null, ["package: not a valid archive"]);
        }
    }

    private static (PluginManifest? Manifest, List<string> Errors) Inspect(ZipArchive zip)
    {
        var errors = new List<string>();
        if (zip.Entries.Count == 0 || zip.Entries.Count > MaxEntries)
        {
            return (null, ["package: entries"]);
        }

        long total = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries)
        {
            string name = e.FullName;
            if (name.EndsWith('/'))
            {
                continue; // папка
            }

            if (!PackagePath.IsSafe(name))
            {
                errors.Add($"package: unsafe path {Short(name)}");
                continue;
            }

            if (!names.Add(name.Replace('\\', '/')))
            {
                errors.Add($"package: duplicate {Short(name)}");
            }

            // Атрибуты Unix: символические ссылки в пакете не допускаются.
            if (((e.ExternalAttributes >> 16) & 0xF000) == 0xA000)
            {
                errors.Add($"package: link {Short(name)}");
            }

            total += e.Length;
            if (e.Length > MaxEntryBytes || total > MaxPackageBytes || (e.CompressedLength > 0 && (double)e.Length / e.CompressedLength > MaxRatio))
            {
                errors.Add($"package: size {Short(name)}");
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var mf = zip.GetEntry(PluginManifest.FileName);
        if (mf is null || mf.Length > 256 * 1024)
        {
            return (null, ["package: manifest.json"]);
        }

        string json;
        using (var r = new StreamReader(mf.Open()))
        {
            json = r.ReadToEnd();
        }

        var (m, merr) = PluginManifest.Parse(json);
        if (m is null)
        {
            return (null, merr);
        }

        if (!SdkInfo.IsCompatible(m.Sdk))
        {
            errors.Add($"package: SDK {m.Sdk} is not compatible with {SdkInfo.Version}");
        }

        if (!names.Contains(m.Entry.Replace('\\', '/')))
        {
            errors.Add("package: entry assembly missing");
        }

        foreach (var res in m.Resources.Where(r => !names.Contains(r.Replace('\\', '/'))))
        {
            errors.Add($"package: resource missing {Short(res)}");
        }

        return errors.Count > 0 ? (null, errors) : (m, errors);
    }

    private static string Short(string s) => s.Length <= 60 ? s : s[..60];

    /// <summary>
    /// Импорт: проверка, затем распаковка во временную папку и замена одной операцией. Тот же ID: обновление только на
    /// более новую версию (настройки, доверие и избранное сохраняются; после обновления плагин выключен до подтверждения).
    /// Встроенные ID (<c>backpack.*</c>) заняты. Код плагина не запускается.
    /// </summary>
    /// <param name="bundledSha">
    /// Пакет из поставки программы с проверенным SHA-256 (<see cref="BundledIndex"/>): версия отмечается «От создателя», а при
    /// обновлении сохраняется прежнее состояние включения (поставка программы — то же доверие, что и обновление самой программы).
    /// Пакет, выбранный пользователем, — null: отметка снимается, обновлённый плагин выключен до явного включения.
    /// </param>
    public ImportResult Import(string packagePath, string? bundledSha = null)
    {
        var (m, errors) = Inspect(packagePath);
        if (m is null)
        {
            return new ImportResult(ImportOutcome.Rejected, null, errors);
        }

        if (m.Id.StartsWith("backpack.", StringComparison.Ordinal))
        {
            return new ImportResult(ImportOutcome.Rejected, m, ["package: reserved id"]);
        }

        var existing = Get(m.Id);
        // Поставляемый пакет той же версии заменяет файлы, установленные без отметки (например, импортированные вручную в 4G):
        // после этого происхождение установленной версии проверено.
        bool sameBundled = bundledSha is not null && existing is not null && PluginManifest.CompareVersions(m.Version, existing.Manifest.Version) == 0 && !IsBundled(existing);
        if (existing is not null && !sameBundled && PluginManifest.CompareVersions(m.Version, existing.Manifest.Version) <= 0)
        {
            return new ImportResult(ImportOutcome.Rejected, m, [$"package: version {m.Version} is not newer than installed {existing.Manifest.Version}"], existing.Manifest.Version);
        }

        string target = Path.Combine(_root, m.Id);
        string staging = target + ".staging";
        string old = target + ".old";
        try
        {
            Directory.CreateDirectory(_root);
            DeleteIfExists(staging);
            DeleteIfExists(old);
            using (var zip = ZipFile.OpenRead(packagePath))
            {
                foreach (var e in zip.Entries.Where(e => !e.FullName.EndsWith('/')))
                {
                    string dst = Path.GetFullPath(Path.Combine(staging, e.FullName));
                    if (!dst.StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("path");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    e.ExtractToFile(dst);
                }
            }

            if (Directory.Exists(target))
            {
                Directory.Move(target, old);
            }

            Directory.Move(staging, target);
            DeleteIfExists(old);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            DeleteIfExists(staging);
            if (!Directory.Exists(target) && Directory.Exists(old))
            {
                Directory.Move(old, target); // вернуть прежнюю версию
            }

            return new ImportResult(ImportOutcome.Rejected, m, ["package: could not be installed"]);
        }

        var st = _state.GetValueOrDefault(m.Id) ?? new State();
        // Новая или обновлённая пользователем версия не запускается без явного включения. Поставляемая с программой —
        // сохраняет осознанный выбор пользователя (включённый плагин остаётся включённым, новый — выключен).
        bool keep = bundledSha is not null && existing is not null && st.Enabled && st.Trusted;
        st.Enabled = keep;
        st.Crashes.Clear();
        st.DisabledReason = null;
        st.BundledSha = bundledSha;
        st.BundledVersion = bundledSha is null ? null : m.Version;
        _state[m.Id] = st;
        _dismissed.Remove(m.Id);
        SaveState();
        Changed?.Invoke();
        return new ImportResult(existing is null ? ImportOutcome.Installed : ImportOutcome.Updated, m, [], existing?.Manifest.Version);
    }

    /// <summary>Подтверждение доверия (перед первым включением, в диалоге с предупреждением).</summary>
    public void Trust(string id)
    {
        Mutate(id, s => s.Trusted = true);
    }

    /// <summary>Включить (только доверенный) или отключить. Отключение сбрасывает причину автоматического отключения.</summary>
    public bool SetEnabled(string id, bool enabled)
    {
        var st = _state.GetValueOrDefault(id);
        if (enabled && st?.Trusted != true)
        {
            return false;
        }

        Mutate(id, s =>
        {
            s.Enabled = enabled;
            s.DisabledReason = null;
            if (enabled)
            {
                s.Crashes.Clear();
            }
        });
        return true;
    }

    /// <summary>Звёздочка: присутствие в боковом меню (не включение и не удаление).</summary>
    public void SetFavorite(string id, bool favorite) => Mutate(id, s => s.Favorite = favorite);

    /// <summary>
    /// Значок плагина в меню, карточке и шапке страницы (Phase 4I): имя из каталога <see cref="PluginSymbols"/> или null — вернуть
    /// значок автора. Хранится по стабильному ID в plugins.json (не в пакете и не по порядку карточек), обновление пакета его не трогает.
    /// </summary>
    public void SetSymbol(string id, string? symbol) => Mutate(id, s => s.Symbol = PluginSymbols.IsChoosable(symbol) ? symbol : null);

    /// <summary>Учесть сбой; при <see cref="CrashLimit"/> сбоях за <see cref="CrashWindow"/> плагин отключается. Возвращает true, если отключён.</summary>
    public bool RecordCrash(string id, DateTime nowUtc)
    {
        bool disabled = false;
        Mutate(id, s =>
        {
            s.Crashes.RemoveAll(t => nowUtc - t > CrashWindow);
            s.Crashes.Add(nowUtc);
            if (s.Crashes.Count >= CrashLimit)
            {
                s.Enabled = false;
                s.DisabledReason = "crashes";
                disabled = true;
            }
        });
        return disabled;
    }

    /// <summary>Удалить плагин: папку, состояние и настройки.</summary>
    public bool Remove(string id)
    {
        if (!PluginManifest.IsValidId(id))
        {
            return false;
        }

        try
        {
            DeleteIfExists(Path.Combine(_root, id));
            string settings = SettingsPath(id);
            if (File.Exists(settings))
            {
                File.Delete(settings);
            }

            if (File.Exists(PanelPath(id)))
            {
                File.Delete(PanelPath(id));
            }

            foreach (var tool in Directory.Exists(_root) ? Directory.GetFiles(_root, id + ".tool.*.json") : [])
            {
                File.Delete(tool);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (_state.GetValueOrDefault(id)?.BundledSha is not null)
        {
            _dismissed.Add(id); // осознанное удаление поставляемого плагина: не возвращать при следующем запуске
        }

        _state.Remove(id);
        SaveState();
        Changed?.Invoke();
        return true;
    }

    public string SettingsPath(string id) => Path.Combine(_root, id + ".settings.json");

    private string PanelPath(string id) => Path.Combine(_root, id + ".panel.json");

    /// <summary>Место окна инструмента хоста (SDK 1.3: снимок) — рядом с пакетами, как место панели; только геометрия, без изображения.</summary>
    private string ToolPath(string id, string toolId) => Path.Combine(_root, id + ".tool." + toolId + ".json");

    /// <summary>Сохранённое место и размер окна инструмента (физические пиксели); null — нет, повреждено или неверный ID.</summary>
    public Presentation.PixelRect? LoadToolBounds(string id, string toolId) =>
        PluginManifest.IsValidId(id) && PluginToolParser.IsValidToolId(toolId) ? LoadBounds(ToolPath(id, toolId)) : null;

    /// <summary>Сохранить место окна инструмента; null — удалить сохранённое.</summary>
    public void SaveToolBounds(string id, string toolId, Presentation.PixelRect? r)
    {
        if (!PluginManifest.IsValidId(id) || !PluginToolParser.IsValidToolId(toolId))
        {
            return;
        }

        if (r is { } rect)
        {
            SaveBounds(ToolPath(id, toolId), rect);
            return;
        }

        try
        {
            File.Delete(ToolPath(id, toolId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Сохранённое место и размер панели плагина (физические пиксели); null — не сохранялось или повреждено.</summary>
    public Presentation.PixelRect? LoadPanelBounds(string id) => LoadBounds(PanelPath(id));

    private static Presentation.PixelRect? LoadBounds(string path)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o
                && (int?)o["left"] is { } l && (int?)o["top"] is { } t && (int?)o["width"] is { } w && (int?)o["height"] is { } h && w is > 40 and < 20000 && h is > 20 and < 20000)
            {
                return new Presentation.PixelRect(l, t, w, h);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
        }

        return null;
    }

    public void SavePanelBounds(string id, Presentation.PixelRect r) => SaveBounds(PanelPath(id), r);

    private static void SaveBounds(string path, Presentation.PixelRect r)
    {
        try
        {
            AtomicFile.Write(path, new JsonObject { ["left"] = r.Left, ["top"] = r.Top, ["width"] = r.Width, ["height"] = r.Height }.ToJsonString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Хранилище значений в активном профиле (settings v8): чтение (null — в профиле нет) и запись. Без него (тесты, режим
    /// хоста плагина) — файл <c>&lt;id&gt;.settings.json</c> рядом с пакетами. Прежний файл читается, пока в профиле значений нет
    /// (перенос без потерь), и после первой записи в профиль больше не используется.
    /// </summary>
    public Func<string, JsonObject?>? ProfileLoad { get; set; }

    public Action<string, JsonObject>? ProfileSave { get; set; }

    /// <summary>
    /// Отметка в сохранённых значениях: их записал Backpack, который знает <see cref="PluginSetting.PreviousDefaults"/>. Без неё
    /// значение, равное прежнему значению по умолчанию, — это не выбор пользователя (прежние версии сохраняли все поля страницы,
    /// включая нетронутые), и оно заменяется новым значением по умолчанию. С отметкой любое сохранённое значение — выбор
    /// пользователя и не меняется (в том числе специально выбранный прежний цвет).
    /// </summary>
    public const string ExplicitMarker = "$explicit";

    /// <summary>Значения настроек плагина (по схеме manifest; неизвестные ключи отбрасываются, отсутствующие — значение по умолчанию).</summary>
    public JsonObject LoadSettings(PluginManifest m)
    {
        JsonObject saved = ProfileLoad?.Invoke(m.Id) ?? new();
        try
        {
            if (saved.Count == 0 && File.Exists(SettingsPath(m.Id)) && JsonNode.Parse(File.ReadAllText(SettingsPath(m.Id))) is JsonObject o)
            {
                saved = o;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        bool explicitValues = saved[ExplicitMarker] is JsonValue mark && mark.TryGetValue(out bool isExplicit) && isExplicit;
        var result = new JsonObject();
        foreach (var f in m.Settings)
        {
            var value = Sanitize(f, saved[f.Key]);
            if (value is not null && !explicitValues && IsPreviousDefault(f, value))
            {
                value = null; // прежнее значение по умолчанию, сохранённое прежней версией, — не выбор пользователя
            }

            result[f.Key] = value ?? f.Default?.DeepClone();
        }

        return result;
    }

    /// <summary>Значение совпадает с одним из прежних значений по умолчанию (после той же проверки и нормализации, например цвета).</summary>
    public static bool IsPreviousDefault(PluginSetting f, JsonNode value) =>
        f.PreviousDefaults is { Count: > 0 } previous
        && previous.Any(p => Sanitize(f, p) is { } clean && clean.ToJsonString() == value.ToJsonString());

    public void SaveSettings(PluginManifest m, JsonObject values)
    {
        var clean = new JsonObject();
        foreach (var f in m.Settings)
        {
            clean[f.Key] = Sanitize(f, values[f.Key]) ?? f.Default?.DeepClone();
        }

        clean[ExplicitMarker] = true; // записано этой версией: значения — выбор пользователя (см. LoadSettings)

        if (ProfileSave is { } save)
        {
            save(m.Id, clean);
            return;
        }

        AtomicFile.Write(SettingsPath(m.Id), clean.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Значение настройки по схеме manifest (null — неверное или отсутствует: берётся значение по умолчанию).</summary>
    public static JsonNode? Sanitize(PluginSetting f, JsonNode? v) => f.Type switch
    {
        PluginSettingType.Color => v is JsonValue col && col.TryGetValue(out string? hex) && Settings.ThemeColor.TryParse(hex, out var tc) ? JsonValue.Create(tc.ToString()) : null,
        PluginSettingType.Toggle => v is JsonValue b && b.TryGetValue(out bool x) ? JsonValue.Create(x) : null,
        PluginSettingType.Number => v is JsonValue n && n.GetValueKind() == JsonValueKind.Number && double.TryParse(n.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) && double.IsFinite(d) ? JsonValue.Create(Math.Clamp(d, f.Min, f.Max)) : null,
        PluginSettingType.Text => v is JsonValue t && t.TryGetValue(out string? s) && s.Length <= 200 ? JsonValue.Create(s) : null,
        _ => v is JsonValue c && c.TryGetValue(out string? ch) && f.Choices.Contains(ch) ? JsonValue.Create(ch) : null,
    };

    private void Mutate(string id, Action<State> change)
    {
        var st = _state.GetValueOrDefault(id) ?? new State();
        change(st);
        _state[id] = st;
        SaveState();
        Changed?.Invoke();
    }

    private static int RecentCrashes(State s) => s.Crashes.Count(t => DateTime.UtcNow - t <= CrashWindow);

    private string StatePath => Path.Combine(_root, "plugins.json");

    private void LoadState()
    {
        try
        {
            if (File.Exists(StatePath) && JsonNode.Parse(File.ReadAllText(StatePath)) is JsonObject o && o["plugins"] is JsonObject list)
            {
                foreach (var (id, node) in list)
                {
                    if (PluginManifest.IsValidId(id) && node is JsonObject s)
                    {
                        _state[id] = new State
                        {
                            Enabled = (bool?)s["enabled"] ?? false,
                            Trusted = (bool?)s["trusted"] ?? false,
                            Favorite = (bool?)s["favorite"] ?? false,
                            DisabledReason = (string?)s["disabledReason"],
                            Crashes = s["crashes"] is JsonArray a ? a.Select(x => DateTime.TryParse((string?)x, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t : (DateTime?)null).OfType<DateTime>().ToList() : [],
                            BundledSha = (string?)s["bundledSha"],
                            BundledVersion = (string?)s["bundledVersion"],
                            Symbol = PluginSymbols.IsChoosable((string?)s["symbol"]) ? (string?)s["symbol"] : null,
                        };
                    }
                }

                if (o["dismissedBundled"] is JsonArray dismissed)
                {
                    foreach (var id in dismissed.Select(x => (string?)x).Where(PluginManifest.IsValidId))
                    {
                        _dismissed.Add(id!);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Повреждённое состояние: все плагины выключены (безопасно), установленные файлы на месте.
        }
    }

    private void SaveState()
    {
        var list = new JsonObject();
        foreach (var (id, s) in _state.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            list[id] = new JsonObject
            {
                ["enabled"] = s.Enabled,
                ["trusted"] = s.Trusted,
                ["favorite"] = s.Favorite,
                ["disabledReason"] = s.DisabledReason,
                ["crashes"] = new JsonArray(s.Crashes.Select(t => (JsonNode)t.ToString("O")).ToArray()),
                ["bundledSha"] = s.BundledSha,
                ["bundledVersion"] = s.BundledVersion,
                ["symbol"] = s.Symbol,
            };
        }

        try
        {
            var root = new JsonObject
            {
                ["format"] = "backpack-plugins", ["formatVersion"] = 1, ["plugins"] = list,
                ["dismissedBundled"] = new JsonArray(_dismissed.Order(StringComparer.Ordinal).Select(x => (JsonNode)x).ToArray()),
            };
            AtomicFile.Write(StatePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteIfExists(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
