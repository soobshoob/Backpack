using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Backpack.Sdk;

namespace Backpack.Visual.Plugins;

/// <summary>Текст на трёх языках интерфейса (en обязателен как запасной).</summary>
public sealed record LocalizedText(IReadOnlyDictionary<string, string> Values)
{
    /// <summary>
    /// Языки текстов manifest (Phase 4I): языки интерфейса Backpack — 18 языков сайта и русский. Порядок ключей в manifest —
    /// сначала en, uk, ru: Backpack до 0.4.0-preview.6 читает только первые восемь и только эти три.
    /// </summary>
    public static IReadOnlySet<string> Languages { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "en", "uk", "ru", "de", "fr", "es", "it", "pt", "nl", "pl", "cs", "hu", "ro", "sv", "da", "nb", "fi", "lt", "el",
    };

    public string For(string locale) => Values.TryGetValue(locale, out var v) ? v : Values.TryGetValue("en", out var en) ? en : Values.Values.FirstOrDefault() ?? "";
}

/// <summary>
/// Команда плагина. <see cref="Hold"/> (SDK 1.1): действие на время удержания назначенного сочетания — плагин получает нажатие
/// (OnCommand) и отпускание (OnCommandReleased); отпускание определяет хост.
/// SDK 1.2: <see cref="WhileTool"/> — сочетание действует только пока открыт инструмент этого плагина (вне его клавиша остаётся
/// другим программам); <see cref="Hotkey"/> — сочетание по умолчанию (занятое или зарезервированное показывается как конфликт и
/// не регистрируется); <see cref="Icon"/> — значок Lucide для меню и радиального меню.
/// </summary>
public sealed record PluginCommand(string Id, LocalizedText Title, bool Hold = false, bool WhileTool = false, string? Hotkey = null, string? Icon = null);

public enum PluginSettingType { Toggle, Number, Text, Choice, Color }

/// <summary>
/// Поле страницы настроек плагина (рисует хост). <see cref="Group"/> — заголовок карточки (поля одной группы стоят вместе);
/// <see cref="Hint"/> — короткое пояснение; <see cref="ShowWhen"/> — поле видно, только когда другая настройка имеет одно из
/// значений (скрытое значение сохраняется); <see cref="ChoiceTitles"/> — подписи вариантов выбора. <see cref="Hidden"/> (SDK 1.2) —
/// значение хранится и проверяется, но на странице не показывается (например, место перетащенной панели, которое плагин
/// записывает сам через UpdateSettings).
/// </summary>
public sealed record PluginSetting(string Key, PluginSettingType Type, LocalizedText Title, JsonNode? Default, double Min, double Max, IReadOnlyList<string> Choices,
    LocalizedText? Group = null, LocalizedText? Hint = null, double Step = 1, string? Unit = null, (string Key, IReadOnlyList<string> Values)? ShowWhen = null,
    IReadOnlyList<LocalizedText>? ChoiceTitles = null, bool Hidden = false);

/// <summary>
/// Демонстрация на странице плагина (SDK 1.1): сцена, которую рисует хост, и привязка параметров сцены к ключам настроек.
/// Сцены: magnifier.follow, magnifier.region, dim, panel; SDK 1.2 — drawing (панель рисования на виртуальном мониторе) и indicator.
/// </summary>
public sealed record PluginPreview(string Scene, IReadOnlyDictionary<string, string> Bind);

/// <summary>
/// Перенос из прежних встроенных инструментов (декларативно, без проверок ID в хосте): <see cref="Section"/> — раздел старых
/// настроек компьютера (например, focus, cursorZoom), <see cref="Settings"/> — ключ настройки плагина → поле раздела,
/// <see cref="Commands"/> — прежний ID команды → локальная команда плагина (переносится назначенное сочетание),
/// <see cref="Tool"/> — прежний переключатель инструмента: если он был включён, плагин добавляется в меню.
/// </summary>
public sealed record PluginLegacy(string? Section, IReadOnlyDictionary<string, string> Settings, IReadOnlyDictionary<string, string> Commands, string? Tool);

/// <summary>
/// manifest.json пакета расширения (<c>backpack-plugin</c>, версия формата 1): ID, автор, название, версии плагина и SDK,
/// точка входа (сборка и тип), возможности, зависимости, ресурсы, локализации, лицензия, команды, схема настроек.
/// Чтение строгое: неизвестная возможность, неверный ID, путь вне пакета — ошибка (пакет не импортируется).
/// </summary>
public sealed record PluginManifest(
    string Id,
    LocalizedText Name,
    string Author,
    string Version,
    string Sdk,
    string Entry,
    string Type,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Resources,
    IReadOnlyList<string> Locales,
    string License,
    IReadOnlyList<PluginCommand> Commands,
    IReadOnlyList<PluginSetting> Settings,
    LocalizedText? Description = null,
    string? Icon = null,
    PluginPreview? Preview = null,
    PluginLegacy? Legacy = null,
    string? Symbol = null)
{
    public const string Format = "backpack-plugin";
    public const string FileName = "manifest.json";

    private static readonly Regex IdPattern = new("^[a-z0-9]+(\\.[a-z0-9-]+){1,5}$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new("^\\d{1,5}\\.\\d{1,5}\\.\\d{1,5}(-[0-9A-Za-z.]{1,32})?$", RegexOptions.CultureInvariant);
    private static readonly Regex LocalId = new("^[a-z][A-Za-z0-9-]{0,31}$", RegexOptions.CultureInvariant);

    public bool Has(string capability) => Capabilities.Contains(capability);

    public static bool IsValidId(string? id) => id is { Length: <= 96 } && IdPattern.IsMatch(id);

    /// <summary>Сравнение версий плагина (SemVer без сборочных метаданных; предварительная версия меньше выпуска).</summary>
    public static int CompareVersions(string a, string b)
    {
        static (int[] Nums, string? Pre) P(string v)
        {
            var (core, pre) = v.Split('-', 2) is [var c, var p] ? (c, p) : (v, (string?)null);
            return (core.Split('.').Select(int.Parse).ToArray(), pre);
        }

        var (x, xp) = P(a);
        var (y, yp) = P(b);
        for (int i = 0; i < 3; i++)
        {
            if (x[i] != y[i])
            {
                return x[i].CompareTo(y[i]);
            }
        }

        return xp == yp ? 0 : xp is null ? 1 : yp is null ? -1 : string.CompareOrdinal(xp, yp);
    }

    public static (PluginManifest? Manifest, List<string> Errors) Parse(string json)
    {
        var errors = new List<string>();
        JsonObject o;
        try
        {
            if (JsonNode.Parse(json, documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 12 }) is not JsonObject obj)
            {
                return (null, ["manifest: not an object"]);
            }

            o = obj;
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, ["manifest: invalid JSON"]);
        }

        string S(string name, int max)
        {
            var v = o[name] is JsonValue jv && jv.TryGetValue(out string? s) ? s : null;
            if (string.IsNullOrWhiteSpace(v) || v.Length > max)
            {
                errors.Add($"manifest: {name}");
                return "";
            }

            return v;
        }

        if ((string?)o["format"] != Format || o["formatVersion"] is not JsonValue fv || !fv.TryGetValue(out int ver) || ver != 1)
        {
            errors.Add("manifest: format");
        }

        string id = S("id", 96);
        if (id.Length > 0 && !IsValidId(id))
        {
            errors.Add("manifest: id");
        }

        var name = Text(o["name"], "name", errors);
        string author = S("author", 120);
        string version = S("version", 48);
        if (version.Length > 0 && !VersionPattern.IsMatch(version))
        {
            errors.Add("manifest: version");
        }

        string sdk = S("sdk", 16);
        if (sdk.Length > 0 && !SdkInfo.TryParse(sdk, out _))
        {
            errors.Add("manifest: sdk");
        }

        string entry = S("entry", 200);
        if (entry.Length > 0 && (!PackagePath.IsSafe(entry) || !entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("manifest: entry");
        }

        string type = S("type", 200);
        string license = S("license", 200);
        var caps = Strings(o["capabilities"], 16, 32);
        foreach (var c in caps.Where(c => !Backpack.Sdk.Capabilities.Known.Contains(c)))
        {
            errors.Add($"manifest: capability {c}");
        }

        var deps = Strings(o["dependencies"], 16, 96);
        var resources = Strings(o["resources"], 64, 200);
        foreach (var r in resources.Where(r => !PackagePath.IsSafe(r)))
        {
            errors.Add($"manifest: resource {r}");
        }

        var locales = Strings(o["locales"], LocalizedText.Languages.Count, 8);
        var commands = new List<PluginCommand>();
        if (o["commands"] is JsonArray ca)
        {
            foreach (var n in ca.Take(32))
            {
                if (n is JsonObject c && (string?)c["id"] is { } cid && LocalId.IsMatch(cid))
                {
                    static bool Flag(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out bool b) && b;
                    string? hotkey = (string?)c["hotkey"] is { } hk && Hotkeys.Hotkey.TryParse(hk, out var parsed) ? parsed.ToString() : null;
                    string? cmdIcon = (string?)c["icon"] is { Length: > 0 and <= 40 } ic ? ic : null;
                    commands.Add(new PluginCommand(cid, Text(c["title"], "commands.title", errors), Flag(c, "hold"), Flag(c, "whileTool"), hotkey, cmdIcon));
                }
                else
                {
                    errors.Add("manifest: commands");
                }
            }
        }

        var settings = new List<PluginSetting>();
        if (o["settings"] is JsonArray sa)
        {
            foreach (var n in sa.Take(32))
            {
                if (n is JsonObject f && (string?)f["key"] is { } key && LocalId.IsMatch(key) && Enum.TryParse<PluginSettingType>((string?)f["type"], true, out var st))
                {
                    double min = (double?)f["min"] ?? 0, max = (double?)f["max"] ?? 100;
                    (string, IReadOnlyList<string>)? when = null;
                    if (f["showWhen"] is JsonObject sw && sw.Count == 1 && sw.First() is var (wk, wv) && LocalId.IsMatch(wk))
                    {
                        var values = wv is JsonArray wa ? Strings(wa, 16, 64) : wv is JsonValue one && one.ToJsonString().Trim('"') is { Length: > 0 } single ? [single] : [];
                        when = values.Count > 0 ? (wk, values) : null;
                        if (when is null)
                        {
                            errors.Add("manifest: settings.showWhen");
                        }
                    }

                    var choiceTitles = f["choiceTitles"] is JsonArray ct ? ct.Take(16).Select(x => Text(x, "settings.choiceTitles", errors)).ToList() : null;
                    settings.Add(new PluginSetting(key, st, Text(f["title"], "settings.title", errors), f["default"]?.DeepClone(), min, max, Strings(f["choices"], 16, 64),
                        f["group"] is null ? null : Text(f["group"], "settings.group", errors), f["hint"] is null ? null : Text(f["hint"], "settings.hint", errors),
                        (double?)f["step"] is { } step && step > 0 ? step : 1, (string?)f["unit"] is { Length: <= 8 } unit ? unit : null, when, choiceTitles,
                        f["hidden"] is JsonValue hv2 && hv2.TryGetValue(out bool hidden) && hidden));
                }
                else
                {
                    errors.Add("manifest: settings");
                }
            }
        }

        if (commands.Count > 0 && !caps.Contains(Backpack.Sdk.Capabilities.Commands))
        {
            errors.Add("manifest: commands without capability");
        }

        if (settings.Count > 0 && !caps.Contains(Backpack.Sdk.Capabilities.Settings))
        {
            errors.Add("manifest: settings without capability");
        }

        var description = o["description"] is null ? null : Text(o["description"], "description", errors);
        // Необязательная иконка карточки (Phase 4F): PNG внутри пакета. Это данные — хост только показывает картинку, код не запускается.
        string? icon = o["icon"] is null ? null : S("icon", 200);
        if (icon is not null && (!PackagePath.IsSafe(icon) || !icon.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("manifest: icon");
        }

        // Значок плагина в меню (Phase 4I): имя значка Lucide из каталога Backpack — данные, а не разметка. XAML, пути к файлам и
        // адреса не принимаются; имя, которого нет в каталоге этой версии Backpack, не ошибка (показывается «puzzle»).
        string? symbol = null;
        if (o["symbol"] is not null)
        {
            symbol = S("symbol", 40);
            if (symbol.Length > 0 && !PluginSymbols.IsWellFormed(symbol))
            {
                errors.Add("manifest: symbol");
            }

            symbol = symbol.Length > 0 ? symbol : null;
        }

        PluginPreview? preview = null;
        if (o["preview"] is JsonObject pv)
        {
            string? scene = (string?)pv["scene"];
            if (scene is not ("magnifier.follow" or "magnifier.region" or "dim" or "panel" or "drawing" or "indicator"))
            {
                errors.Add("manifest: preview.scene");
            }
            else
            {
                preview = new PluginPreview(scene, Map(pv["bind"], errors, "preview.bind"));
            }
        }

        PluginLegacy? legacy = null;
        if (o["legacy"] is JsonObject lg)
        {
            legacy = new PluginLegacy((string?)lg["section"] is { Length: > 0 and <= 32 } sec ? sec : null, Map(lg["settings"], errors, "legacy.settings"),
                Map(lg["commands"], errors, "legacy.commands"), (string?)lg["tool"] is { Length: > 0 and <= 32 } tool ? tool : null);
            foreach (var target in legacy.Commands.Values.Where(v => commands.All(c => c.Id != v.Split('|')[0])))
            {
                errors.Add($"manifest: legacy command {target}");
            }
        }

        return errors.Count > 0
            ? (null, errors)
            : (new PluginManifest(id, name, author, version, sdk, entry, type, caps, deps, resources, locales, license, commands, settings, description, string.IsNullOrEmpty(icon) ? null : icon, preview, legacy, symbol), errors);
    }

    /// <summary>Объект «строка → строка» (до 32 пар, ключи и значения — короткие идентификаторы).</summary>
    private static IReadOnlyDictionary<string, string> Map(JsonNode? n, List<string> errors, string what)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (n is null)
        {
            return d;
        }

        if (n is not JsonObject o || o.Count > 32)
        {
            errors.Add("manifest: " + what);
            return d;
        }

        foreach (var (k, v) in o)
        {
            if (k.Length is > 0 and <= 64 && v is JsonValue jv && jv.TryGetValue(out string? s) && s.Length is > 0 and <= 64)
            {
                d[k] = s;
            }
            else
            {
                errors.Add("manifest: " + what);
            }
        }

        return d;
    }

    private static LocalizedText Text(JsonNode? n, string what, List<string> errors)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (n is JsonValue v && v.TryGetValue(out string? single) && single.Length is > 0 and <= 200)
        {
            d["en"] = single;
        }
        else if (n is JsonObject o)
        {
            foreach (var (k, val) in o.Take(LocalizedText.Languages.Count))
            {
                if (LocalizedText.Languages.Contains(k) && val is JsonValue jv && jv.TryGetValue(out string? s) && s.Length is > 0 and <= 400)
                {
                    d[k] = s;
                }
            }
        }

        if (d.Count == 0)
        {
            errors.Add($"manifest: {what}");
        }

        return new LocalizedText(d);
    }

    private static List<string> Strings(JsonNode? n, int maxCount, int maxLength) =>
        n is JsonArray a ? a.Take(maxCount).Select(x => x is JsonValue v && v.TryGetValue(out string? s) && s.Length is > 0 && s.Length <= maxLength ? s : null).OfType<string>().ToList() : [];
}

/// <summary>Безопасные пути внутри пакета: относительные, без «..», без корня и диска, без запрещённых символов.</summary>
public static class PackagePath
{
    public static bool IsSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 200 || path.StartsWith('/') || path.StartsWith('\\') || path.Contains(':'))
        {
            return false;
        }

        var parts = path.Replace('\\', '/').Split('/');
        return parts.All(p => p.Length > 0 && p != "." && p != ".." && p.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !p.EndsWith('.') && !p.EndsWith(' '));
    }
}
