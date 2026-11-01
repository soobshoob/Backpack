using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Localization;

namespace Backpack.Visual.Settings;

/// <summary>
/// Общие предпочтения приложения на этом компьютере — не часть визуального профиля: язык интерфейса, тема окон, поведение
/// крестика главного окна, подсказка о трее, активный профиль, глобальные горячие клавиши, что скрывает команда показа,
/// линза, рисование и обновления. Загрузка и импорт профиля их не меняют.
/// </summary>
public sealed record AppPreferences(
    UiLanguage Language,
    UiThemeMode Theme,
    bool CloseToTray,
    bool TrayHintShown,
    string? ActiveProfileId,
    HotkeySettings? Hotkeys = null,
    DisplayParts HideParts = DisplayParts.All,
    UpdatePreferences? Updates = null,
    LensSettings? Lens = null,
    DrawSettings? Draw = null,
    string? ScreenshotFolder = null,
    bool HomeAnimation = true,
    PrivacySettings? Privacy = null,
    string ColorTheme = UiPalettes.DefaultId,
    ToolToggles? Tools = null,
    FocusSettings? Focus = null,
    CursorZoomSettings? CursorZoom = null,
    MarkerSettings? Markers = null,
    CommandBindings? Bindings = null,
    bool ReducedMotion = false,
    AppearanceMaterial? Material = null,
    bool PluginsAsList = false)
{
    /// <summary>Материал окон, интенсивность и свечение акцентов (app.json v4).</summary>
    public AppearanceMaterial MaterialOrDefault => Material ?? AppearanceMaterial.Default;

    public PrivacySettings PrivacyOrDefault => Privacy ?? PrivacySettings.Default;

    public ToolToggles ToolsOrDefault => Tools ?? ToolToggles.Default;

    public FocusSettings FocusOrDefault => Focus ?? FocusSettings.Default;

    public CursorZoomSettings CursorZoomOrDefault => CursorZoom ?? CursorZoomSettings.Default;

    public MarkerSettings MarkersOrDefault => Markers ?? MarkerSettings.Default;

    /// <summary>Назначенные сочетания команд реестра (id команды → текст сочетания; пустая строка — без сочетания).</summary>
    public CommandBindings BindingsOrEmpty => Bindings ?? CommandBindings.Empty;

    public static AppPreferences Default { get; } = new(UiLanguage.System, UiThemeMode.System, true, false, null, HotkeySettings.Default, DisplayParts.All,
        UpdatePreferences.Default, LensSettings.Default, DrawSettings.Default);

    public HotkeySettings HotkeysOrDefault => Hotkeys ?? HotkeySettings.Default;

    public UpdatePreferences UpdatesOrDefault => Updates ?? UpdatePreferences.Default;

    public LensSettings LensOrDefault => Lens ?? LensSettings.Default;

    public DrawSettings DrawOrDefault => Draw ?? DrawSettings.Default;

    /// <summary>Папка снимков: заданная или «Изображения\Backpack».</summary>
    public string ScreenshotFolderOrDefault => string.IsNullOrWhiteSpace(ScreenshotFolder)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Backpack")
        : ScreenshotFolder;
}

/// <summary>Запись файла целиком через временный файл и замену (одна операция MoveFileEx на томе).</summary>
public static class AtomicFile
{
    public static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var w = new StreamWriter(fs))
        {
            w.Write(text);
        }

        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>
/// Файл <c>app.json</c> (<c>backpack-app</c>, версия 4; версии 1–3 читаются — новые поля получают значения по умолчанию,
/// тема окон до v3 становится Backpack Emerald с сообщением о том, как вернуть прежнее оформление, а прежние пять сочетаний
/// переносятся в команды (<see cref="LegacyMap"/>)). Повреждённый файл не мешает запуску. Неверное отдельное поле заменяется
/// значением по умолчанию.
/// </summary>
public sealed class AppPreferencesStore
{
    public const string Format = "backpack-app";

    private readonly string _path;

    private AppPreferencesStore(string path, AppPreferences current, bool unreadable)
    {
        _path = path;
        Current = current;
        Unreadable = unreadable;
    }

    public AppPreferences Current { get; private set; }

    public bool Unreadable { get; }

    /// <summary>Версия формата прочитанного файла (0 — файла не было). Меньше 3 — первое знакомство с Backpack.</summary>
    public int LoadedVersion { get; private init; }

    /// <summary>Файл v1–v3 содержал прежние сочетания: они перенесены в команды (страница «Команды и сочетания» сообщает об этом).</summary>
    public bool LegacyHotkeysMigrated { get; private init; }

    public string FilePath => _path;

    public event Action<AppPreferences>? Changed;

    public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;

    public static string PathNextTo(string settingsPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "app.json");

    public static AppPreferencesStore Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppPreferencesStore(path, AppPreferences.Default, false);
            }

            string text = File.ReadAllText(path);
            var p = Parse(text);
            int version = 0;
            try
            {
                version = JsonNode.Parse(text) is JsonObject root && root["formatVersion"] is JsonValue fv && fv.TryGetValue(out int n) ? n : 0;
            }
            catch (JsonException)
            {
            }

            bool migrated = version is > 0 and <= 3 && JsonNode.Parse(text) is JsonObject o && o["hotkeys"] is JsonObject;
            return new AppPreferencesStore(path, p ?? AppPreferences.Default, p is null) { LoadedVersion = version, LegacyHotkeysMigrated = migrated && p is not null };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AppPreferencesStore(path, AppPreferences.Default, true);
        }
    }

    public static AppPreferences? Parse(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject o || (string?)o["format"] != Format || (int?)o["formatVersion"] is not (1 or 2 or 3 or 4 or 5) || (int?)o["formatVersion"] is not int version)
            {
                return null;
            }

            var d = AppPreferences.Default;
            var bindings = o["bindings"] is JsonObject bo ? new CommandBindings(PreferenceSections.ReadBindings(bo)) : null;
            if (version <= 3 && o["hotkeys"] is JsonObject legacy)
            {
                bindings = MigrateLegacyHotkeys(legacy, bindings ?? CommandBindings.Empty);
            }

            return new AppPreferences(
                Enum.TryParse<UiLanguage>((string?)o["language"], true, out var l) ? l : d.Language,
                Enum.TryParse<UiThemeMode>((string?)o["theme"], true, out var t) ? t : d.Theme,
                o["closeToTray"] is JsonValue c && c.TryGetValue(out bool close) ? close : d.CloseToTray,
                o["trayHintShown"] is JsonValue h && h.TryGetValue(out bool hint) ? hint : d.TrayHintShown,
                o["activeProfile"] is JsonValue a && a.TryGetValue(out string? id) && Profiles.ProfileSerializer.IsValidId(id) ? id : null,
                ReadHotkeys(o["hotkeys"] as JsonObject),
                ReadParts(o["hideParts"] as JsonArray),
                ReadUpdates(o["updates"] as JsonObject),
                ReadLens(o["lens"] as JsonObject),
                ReadDraw(o["draw"] as JsonObject, version),
                o["screenshotFolder"] is JsonValue sf && sf.TryGetValue(out string? folder) && IsSafeFolder(folder) ? folder : null,
                o["homeAnimation"] is JsonValue ha && ha.TryGetValue(out bool anim) ? anim : true,
                o["privacy"] is JsonObject po ? ReadPrivacy(po) : null,
                // До версии 3 темы окон не было: управляемая миграция на Backpack Emerald (прежнее — «Классическая»).
                o["colorTheme"] is JsonValue ct && ct.TryGetValue(out string? theme) && UiPalettes.Exists(theme) ? theme : UiPalettes.DefaultId,
                PreferenceSections.ReadOptional(o["tools"], ToolToggles.Default),
                PreferenceSections.ReadOptional(o["focus"], FocusSettings.Default, f => f.Sanitized()),
                PreferenceSections.ReadOptional(o["cursorZoom"], CursorZoomSettings.Default, z => z.Sanitized()),
                PreferenceSections.ReadOptional(o["markers"], MarkerSettings.Default, m => m.Sanitized()),
                bindings,
                o["reducedMotion"] is JsonValue rm && rm.TryGetValue(out bool reduced) && reduced,
                PreferenceSections.ReadOptional(o["material"], AppearanceMaterial.Default, m => m.Sanitized()),
                o["pluginsView"] is JsonValue pv && pv.TryGetValue(out string? view) && view == "list"); // вид страницы «Плагины» (Phase 4F)
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// app.json v1–v3 хранил пять прежних действий отдельно (<c>hotkeys</c>). В v4 это команды реестра:
    /// показ → <c>display.toggle</c>, «Живая линза» → <c>tool.focus</c> (запуск выбранного режима «Фокуса»), рисование →
    /// <c>tool.drawing</c>, радиальное меню → <c>tool.radial</c>, снимок → <c>tool.drawing.screenshot</c>. Переносится только
    /// отличное от прежнего значения по умолчанию (в том числе снятое — пустая строка); назначение пользователя для новой ID
    /// не перезаписывается. Сочетания по умолчанию у новых ID те же, поэтому ничего не теряется.
    /// </summary>
    public static readonly (string Legacy, string Command, Hotkey? Default)[] LegacyMap =
    [
        ("toggleDisplay", "display.toggle", HotkeySettings.Default.ToggleDisplay), ("lens", "tool.focus", HotkeySettings.Default.Lens),
        ("draw", "tool.drawing", HotkeySettings.Default.Draw), ("radial", "tool.radial", HotkeySettings.Default.Radial),
        ("screenshot", "tool.drawing.screenshot", HotkeySettings.Default.Screenshot),
    ];

    private static CommandBindings MigrateLegacyHotkeys(JsonObject legacy, CommandBindings bindings)
    {
        foreach (var (name, command, def) in LegacyMap)
        {
            if (bindings[command] is not null || legacy[name] is not JsonValue v || !v.TryGetValue(out string? text))
            {
                continue;
            }

            if (text.Length == 0)
            {
                bindings = bindings.With(command, "");
            }
            else if (Hotkey.TryParse(text, out var h) && h != def)
            {
                bindings = bindings.With(command, h.ToString());
            }
        }

        return bindings;
    }

    public static string Write(AppPreferences p)
    {
        var lens = p.LensOrDefault;
        var draw = p.DrawOrDefault;
        var up = p.UpdatesOrDefault;
        return new JsonObject
        {
            ["format"] = Format,
            ["formatVersion"] = 5,
            ["language"] = p.Language.ToString().ToLowerInvariant(),
            ["theme"] = p.Theme.ToString().ToLowerInvariant(),
            ["closeToTray"] = p.CloseToTray,
            ["trayHintShown"] = p.TrayHintShown,
            ["activeProfile"] = p.ActiveProfileId,
            ["hideParts"] = new JsonArray(Enum.GetValues<DisplayParts>().Where(x => x is not (DisplayParts.None or DisplayParts.All) && p.HideParts.HasFlag(x))
                .Select(x => (JsonNode)x.ToString().ToLowerInvariant()).ToArray()),
            ["updates"] = new JsonObject { ["channel"] = up.Channel.ToString().ToLowerInvariant(), ["autoCheck"] = up.AutoCheck },
            ["lens"] = new JsonObject
            {
                ["zoom"] = lens.Zoom,
                ["defaultWidth"] = lens.DefaultWidth,
                ["defaultHeight"] = lens.DefaultHeight,
                ["radius"] = lens.Radius,
                ["borderColor"] = lens.BorderColor.ToString(),
                ["borderWidth"] = lens.BorderWidth,
                ["shadow"] = lens.Shadow,
                ["durationSec"] = lens.DurationSec,
            },
            ["draw"] = new JsonObject
            {
                ["tool"] = draw.Tool.ToString().ToLowerInvariant(),
                ["color"] = draw.Color.ToString(),
                ["thickness"] = draw.Thickness,
                ["dash"] = draw.Dash.ToString().ToLowerInvariant(),
                ["fadeSec"] = draw.FadeSec,
                ["textSize"] = draw.TextSize,
                ["cornerRadius"] = draw.CornerRadius,
                ["polygonSides"] = draw.PolygonSides,
            },
            ["screenshotFolder"] = p.ScreenshotFolder,
            ["homeAnimation"] = p.HomeAnimation,
            ["privacy"] = p.Privacy is { } pr ? new JsonObject
            {
                ["enabled"] = pr.Enabled,
                ["showBadge"] = pr.ShowBadge,
                ["blockedApps"] = new JsonArray(pr.Apps.Select(a => (JsonNode)a).ToArray()),
            } : null,
            ["colorTheme"] = p.ColorTheme,
            ["tools"] = p.Tools is null ? null : PreferenceSections.ToNode(p.Tools),
            ["focus"] = p.Focus is null ? null : PreferenceSections.ToNode(p.Focus),
            ["cursorZoom"] = p.CursorZoom is null ? null : PreferenceSections.ToNode(p.CursorZoom),
            ["markers"] = p.Markers is null ? null : PreferenceSections.ToNode(p.Markers),
            ["bindings"] = p.Bindings is { } b ? new JsonObject(b.Map.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))) : null,
            ["reducedMotion"] = p.ReducedMotion,
            ["pluginsView"] = p.PluginsAsList ? "list" : "cards",
            ["material"] = p.Material is null ? null : PreferenceSections.ToNode(p.Material),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static PrivacySettings ReadPrivacy(JsonObject? o)
    {
        var d = PrivacySettings.Default;
        if (o is null)
        {
            return d;
        }

        var apps = new List<string>();
        if (o["blockedApps"] is JsonArray a)
        {
            foreach (var n in a.Take(64))
            {
                if (n is JsonValue v && v.TryGetValue(out string? name) && PrivacySettings.NormalizeApp(name) is { } app && !apps.Contains(app))
                {
                    apps.Add(app);
                }
            }
        }

        return new PrivacySettings(
            o["enabled"] is JsonValue e && e.TryGetValue(out bool enabled) ? enabled : d.Enabled,
            o["showBadge"] is JsonValue b && b.TryGetValue(out bool badge) ? badge : d.ShowBadge,
            apps);
    }

    /// <summary>Пустая строка — действие без сочетания; неверная запись — значение по умолчанию для этого действия.</summary>
    private static HotkeySettings ReadHotkeys(JsonObject? o)
    {
        var d = HotkeySettings.Default;
        if (o is null)
        {
            return d;
        }

        Hotkey? One(string name, Hotkey? fallback)
        {
            if (o[name] is not JsonValue v || !v.TryGetValue(out string? text))
            {
                return fallback;
            }

            return text.Length == 0 ? null : Hotkey.TryParse(text, out var h) ? h : fallback;
        }

        return new HotkeySettings(One("toggleDisplay", d.ToggleDisplay), One("lens", d.Lens), One("draw", d.Draw), One("radial", d.Radial), One("screenshot", d.Screenshot));
    }

    private static DisplayParts ReadParts(JsonArray? a)
    {
        if (a is null)
        {
            return DisplayParts.All;
        }

        var parts = DisplayParts.None;
        foreach (var n in a)
        {
            if (n is JsonValue v && v.TryGetValue(out string? s) && Enum.TryParse<DisplayParts>(s, true, out var part) && part is not DisplayParts.All)
            {
                parts |= part;
            }
        }

        return parts;
    }

    private static UpdatePreferences ReadUpdates(JsonObject? o)
    {
        var d = UpdatePreferences.Default;
        return o is null ? d : new UpdatePreferences(
            Enum.TryParse<UpdateChannel>((string?)(o["channel"] as JsonValue), true, out var ch) ? ch : d.Channel,
            o["autoCheck"] is JsonValue a && a.TryGetValue(out bool auto) ? auto : d.AutoCheck);
    }

    private static LensSettings ReadLens(JsonObject? o)
    {
        var d = LensSettings.Default;
        if (o is null)
        {
            return d;
        }

        double N(string name, double fallback, (double Min, double Max) range) =>
            o[name] is JsonValue v && v.TryGetValue(out double x) && double.IsFinite(x) && x >= range.Min && x <= range.Max ? x : fallback;

        return new LensSettings(
            N("zoom", d.Zoom, LensSettings.ZoomRange),
            N("defaultWidth", d.DefaultWidth, LensSettings.SizeRange),
            N("defaultHeight", d.DefaultHeight, LensSettings.SizeRange),
            N("radius", d.Radius, LensSettings.RadiusRange),
            ThemeColor.TryParse((string?)(o["borderColor"] as JsonValue), out var c) ? c : d.BorderColor,
            N("borderWidth", d.BorderWidth, LensSettings.BorderRange),
            o["shadow"] is JsonValue sh && sh.TryGetValue(out bool shadow) ? shadow : d.Shadow,
            (int)N("durationSec", d.DurationSec, (LensSettings.DurationRange.Min, LensSettings.DurationRange.Max)));
    }

    /// <summary>Прежний цвет рисования по умолчанию (до app.json v5).</summary>
    private static readonly ThemeColor OldDefaultDrawColor = new(0xFF, 0x3B, 0x30);

    /// <summary>
    /// v5: цвет по умолчанию — неоновый изумрудный. В файлах до v5 значение, равное прежнему умолчанию (красный), неотличимо от
    /// «не выбирали» и переводится на новое умолчание; любой другой сохранённый цвет остаётся.
    /// </summary>
    private static DrawSettings ReadDraw(JsonObject? o, int version)
    {
        var r = ReadDraw(o);
        return version < 5 && r.Color == OldDefaultDrawColor ? r with { Color = DrawSettings.Default.Color } : r;
    }

    private static DrawSettings ReadDraw(JsonObject? o)
    {
        var d = DrawSettings.Default;
        if (o is null)
        {
            return d;
        }

        return new DrawSettings(
            Enum.TryParse<DrawTool>((string?)(o["tool"] as JsonValue), true, out var t) ? t : d.Tool,
            ThemeColor.TryParse((string?)(o["color"] as JsonValue), out var c) ? c : d.Color,
            o["thickness"] is JsonValue th && th.TryGetValue(out double x) && x >= DrawSettings.ThicknessRange.Min && x <= DrawSettings.ThicknessRange.Max ? x : d.Thickness,
            Enum.TryParse<DrawDash>((string?)(o["dash"] as JsonValue), true, out var dash) ? dash : d.Dash,
            o["fadeSec"] is JsonValue f && f.TryGetValue(out int fade) && fade >= DrawSettings.FadeRange.Min && fade <= DrawSettings.FadeRange.Max ? fade : d.FadeSec,
            o["textSize"] is JsonValue ts && ts.TryGetValue(out double size) && size >= DrawSettings.TextSizeRange.Min && size <= DrawSettings.TextSizeRange.Max ? size : d.TextSize,
            o["cornerRadius"] is JsonValue cr && cr.TryGetValue(out double radius) && radius >= DrawSettings.CornerRange.Min && radius <= DrawSettings.CornerRange.Max ? radius : d.CornerRadius,
            o["polygonSides"] is JsonValue ps && ps.TryGetValue(out int sides) && sides >= DrawSettings.SidesRange.Min && sides <= DrawSettings.SidesRange.Max ? sides : d.PolygonSides);
    }

    /// <summary>Папка снимков: абсолютный локальный путь без недопустимых символов (иначе — папка по умолчанию).</summary>
    public static bool IsSafeFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 240 || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        try
        {
            return Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Update(Func<AppPreferences, AppPreferences> change)
    {
        var next = change(Current);
        if (next == Current)
        {
            return;
        }

        Current = next;
        Save();
        Changed?.Invoke(next);
    }

    private bool _backedUp;

    private void Save()
    {
        try
        {
            if (Unreadable && !_backedUp && File.Exists(_path))
            {
                // Повреждённый или чужой (например, прежнего формата) файл не пропадает молча: копия перед первой записью.
                File.Copy(_path, Path.Combine(Path.GetDirectoryName(_path)!, "app.unreadable.bak.json"), overwrite: true);
            }

            _backedUp = true;
            AtomicFile.Write(_path, Write(Current));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Предпочтения — не критичные данные: при ошибке записи приложение продолжает работать с ними в памяти.
        }
    }
}
