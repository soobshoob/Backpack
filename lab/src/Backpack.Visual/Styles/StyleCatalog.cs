using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Backpack.Visual.Localization;
using Backpack.Visual.Profiles;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Styles;

/// <summary>Что оформляет стиль.</summary>
public enum StyleKind { Keycap, Indicator, Mouse }

/// <summary>
/// Стиль — сохранённый набор только визуальных параметров одного элемента. Понятия разведены так:
/// <list type="bullet">
/// <item><b>рендер</b> — геометрия и способ отрисовки в коде (<see cref="KeycapRenderer"/>, <see cref="MouseRenderer"/>);</item>
/// <item><b>визуальный пресет</b> — встроенный неизменяемый стиль (<c>backpack.*</c>): рендер + параметры и палитра;</item>
/// <item><b>пользовательский стиль</b> — то же, сохранённое пользователем в папке стилей (<c>u-…</c>);</item>
/// <item><b>профиль</b> — вся настройка: внешний вид (полная копия параметров), поведение и размещение.</item>
/// </list>
/// Стиль кейкапа не содержит размер, подписи и анимацию; стиль мыши не содержит боковые кнопки (это оборудование).
/// </summary>
public sealed record StyleEntry(
    string Id,
    string Name,
    StyleKind Kind,
    bool BuiltIn,
    string? BasedOn,
    AppearanceSettings? Keycap = null,
    IndicatorVisual? Indicator = null,
    MouseVisual? Mouse = null)
{
    public KeycapRenderer? KeycapRenderer => Keycap?.Renderer;
}

public static class StyleIds
{
    public const string BuiltInPrefix = "backpack.";

    public static bool IsValid(string? id) => ProfileSerializer.IsValidId(id);

    public static bool IsBuiltIn(string? id) => id is not null && id.StartsWith(BuiltInPrefix, StringComparison.Ordinal);

    public static string NewId() => "u-" + Guid.NewGuid().ToString("N")[..10];
}

/// <summary>Встроенные пресеты. Палитры — собственные значения Backpack (не копия схем Keyviz или Icons8).</summary>
public static class BuiltInStyles
{
    private static readonly ThemeColor White = new(0xFF, 0xFF, 0xFF);

    public static IReadOnlyList<StyleEntry> All { get; } = Build();

    public static StyleEntry? Find(string? id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Название встроенного стиля на языке интерфейса (идентификатор не переводится).</summary>
    public static string LocalizedName(StyleEntry e) => e.BuiltIn ? Names.TryGetValue(e.Id, out var f) ? f() : e.Name : e.Name;

    private static readonly Dictionary<string, Func<string>> Names = new()
    {
        [BrandStyles.KeycapId] = () => BrandStyles.Name,
        [BrandStyles.MouseId] = () => BrandStyles.Name,
        ["backpack.lowprofile.classic"] = () => Loc.T("Style.LowProfileClassic"),
        ["backpack.lowprofile.graphite"] = () => Loc.T("Style.LowProfileGraphite"),
        ["backpack.laptop.silver"] = () => Loc.T("Style.LaptopSilver"),
        ["backpack.laptop.midnight"] = () => Loc.T("Style.LaptopMidnight"),
        ["backpack.pbt.cream"] = () => Loc.T("Style.PbtCream"),
        ["backpack.pbt.ocean"] = () => Loc.T("Style.PbtOcean"),
        ["backpack.minimal.light"] = () => Loc.T("Style.MinimalLight"),
        ["backpack.minimal.ink"] = () => Loc.T("Style.MinimalInk"),
        ["backpack.volumetric.lavender"] = () => Loc.T("Style.VolLavender"),
        ["backpack.volumetric.mint"] = () => Loc.T("Style.VolMint"),
        ["backpack.volumetric.rose"] = () => Loc.T("Style.VolRose"),
        ["backpack.indicator.dark-pill"] = () => Loc.T("Style.IndicatorDark"),
        ["backpack.indicator.light-pill"] = () => Loc.T("Style.IndicatorLight"),
        ["backpack.indicator.accent"] = () => Loc.T("Style.IndicatorAccent"),
        ["backpack.indicator.outline"] = () => Loc.T("Style.IndicatorOutline"),
        ["backpack.mouse.outline-light"] = () => Loc.T("Style.MouseOutlineLight"),
        ["backpack.mouse.outline-dark"] = () => Loc.T("Style.MouseOutlineDark"),
        ["backpack.mouse.solid-light"] = () => Loc.T("Style.MouseSolidLight"),
        ["backpack.mouse.solid-dark"] = () => Loc.T("Style.MouseSolidDark"),
        ["backpack.mouse.retro-bluegrey"] = () => Loc.T("Style.MouseRetroBlueGrey"),
        ["backpack.mouse.retro-beige"] = () => Loc.T("Style.MouseRetroBeige"),
        ["backpack.mouse.retro-graphite"] = () => Loc.T("Style.MouseRetroGraphite"),
        ["backpack.mouse.retro-sage"] = () => Loc.T("Style.MouseRetroSage"),
        ["backpack.mouse.sketch-ink"] = () => Loc.T("Style.MouseSketchInk"),
        ["backpack.mouse.sketch-chalk"] = () => Loc.T("Style.MouseSketchChalk"),
    };

    private static ThemeColor C(string hex) => ThemeColor.Parse(hex);

    private static List<StyleEntry> Build()
    {
        var d = AppearanceSettings.Default with { KeycapStyleId = null };
        StyleEntry K(string id, AppearanceSettings a) => new(id, id, StyleKind.Keycap, true, null, Keycap: a);
        StyleEntry I(string id, IndicatorVisual v) => new(id, id, StyleKind.Indicator, true, null, Indicator: v with { StyleId = null });
        StyleEntry M(string id, MouseVisual v) => new(id, id, StyleKind.Mouse, true, null, Mouse: v with { StyleId = null });
        var dm = MouseVisual.Default with { StyleId = null };

        return
        [
            K(BrandStyles.KeycapId, BrandStyles.Keycap),
            K("backpack.lowprofile.classic", d),
            K("backpack.lowprofile.graphite", d with
            {
                Primary = C("#2B2D31"), Secondary = C("#111214"), TextColor = C("#F2F3F5"), BorderColor = C("#0B0C0E"), BackgroundColor = C("#00000066"),
            }),
            K("backpack.laptop.silver", d with
            {
                Renderer = KeycapRenderer.Laptop, Primary = C("#E9EBEF"), Secondary = C("#C3C8D0"), TextColor = C("#1F2328"),
                BorderWidth = 1, BorderColor = C("#9AA1AB"), BackgroundColor = C("#FFFFFF80"), Radius = 0.35,
            }),
            K("backpack.laptop.midnight", d with
            {
                Renderer = KeycapRenderer.Laptop, Primary = C("#2A2E37"), Secondary = C("#1B1E24"), TextColor = C("#E8EAED"),
                BorderWidth = 1, BorderColor = C("#0E1013"), BackgroundColor = C("#00000059"), Radius = 0.35,
            }),
            K("backpack.pbt.cream", d with
            {
                Renderer = KeycapRenderer.Pbt, Primary = C("#F4EBDD"), Secondary = C("#CDBFA8"), TextColor = C("#3B3227"),
                BorderWidth = 1.5, BorderColor = C("#6B5E4C"), BackgroundColor = C("#FFFFFF73"),
            }),
            K("backpack.pbt.ocean", d with
            {
                Renderer = KeycapRenderer.Pbt, Primary = C("#DCEBF5"), Secondary = C("#3E6E8E"), TextColor = C("#12324A"),
                BorderWidth = 1.5, BorderColor = C("#1D3B52"), BackgroundColor = C("#FFFFFF73"),
            }),
            K("backpack.minimal.light", d with
            {
                Renderer = KeycapRenderer.Minimal, TextColor = White, BorderEnabled = false, BackgroundColor = C("#0000008C"),
            }),
            K("backpack.minimal.ink", d with
            {
                Renderer = KeycapRenderer.Minimal, TextColor = C("#111111"), BorderEnabled = false, BackgroundColor = C("#FFFFFFB3"),
            }),
            K("backpack.volumetric.lavender", d with
            {
                Renderer = KeycapRenderer.Volumetric, Primary = C("#E8E9FB"), Secondary = C("#A9ACDB"), TextColor = C("#1A1B3A"),
                BorderColor = C("#1E2147"), BorderWidth = 2, BackgroundEnabled = false,
            }),
            K("backpack.volumetric.mint", d with
            {
                Renderer = KeycapRenderer.Volumetric, Primary = C("#C7EBD4"), Secondary = C("#5FAE7E"), TextColor = C("#12301F"),
                BorderColor = C("#173826"), BorderWidth = 2, BackgroundEnabled = false,
            }),
            K("backpack.volumetric.rose", d with
            {
                Renderer = KeycapRenderer.Volumetric, Primary = C("#F6D3E6"), Secondary = C("#D9629B"), TextColor = C("#3A1028"),
                BorderColor = C("#3A1028"), BorderWidth = 2, BackgroundEnabled = false,
            }),

            I("backpack.indicator.dark-pill", IndicatorVisual.Default),
            I("backpack.indicator.light-pill", new IndicatorVisual(C("#FFFFFFEB"), C("#111111"), C("#00000026"), 1, 0.5, 36)),
            I("backpack.indicator.accent", new IndicatorVisual(C("#2563EBF0"), White, C("#FFFFFF40"), 1, 0.35, 36)),
            I("backpack.indicator.outline", new IndicatorVisual(C("#0000004D"), White, White, 2, 1, 36)),

            M(BrandStyles.MouseId, BrandStyles.Mouse),
            M("backpack.mouse.outline-light", dm),
            M("backpack.mouse.outline-dark", dm with { IconForeground = C("#111111"), IconBackground = C("#FFFFFFCC"), IconAccent = C("#2563EB") }),
            M("backpack.mouse.solid-light", dm with { Renderer = MouseRenderer.Solid, IconForeground = C("#F5F5F5"), IconBackground = C("#00000099"), IconAccent = C("#F59E0B") }),
            M("backpack.mouse.solid-dark", dm with { Renderer = MouseRenderer.Solid, IconForeground = C("#1C1C1C"), IconBackground = C("#FFFFFFCC"), IconAccent = C("#EF4444") }),
            M("backpack.mouse.retro-bluegrey", dm with { Renderer = MouseRenderer.Retro, BodyColor = C("#B8C4D6"), IconForeground = C("#5E6B80"), IconBackground = C("#FFFFFF59"), IconAccent = C("#7DD3FC") }),
            M("backpack.mouse.retro-beige", dm with { Renderer = MouseRenderer.Retro, BodyColor = C("#E3D5BC"), IconForeground = C("#7A6A52"), IconBackground = C("#FFFFFF59"), IconAccent = C("#E07A5F") }),
            M("backpack.mouse.retro-graphite", dm with { Renderer = MouseRenderer.Retro, BodyColor = C("#6B7280"), IconForeground = C("#2F343B"), IconBackground = C("#FFFFFF4D"), IconAccent = C("#A3E635") }),
            M("backpack.mouse.retro-sage", dm with { Renderer = MouseRenderer.Retro, BodyColor = C("#A7C4BC"), IconForeground = C("#4F6F68"), IconBackground = C("#FFFFFF59"), IconAccent = C("#F4A261") }),
            M("backpack.mouse.sketch-ink", dm with { Renderer = MouseRenderer.Sketch, IconForeground = C("#111111"), BodyColor = C("#FFFFFFE6"), IconBackground = C("#FFFFFF99"), IconAccent = C("#FACC15") }),
            M("backpack.mouse.sketch-chalk", dm with { Renderer = MouseRenderer.Sketch, IconForeground = C("#F8FAFC"), IconBackground = C("#00000099"), IconAccent = C("#FB7185") }),
        ];
    }
}

/// <summary>
/// Применение и сравнение стилей. Применение меняет только поля, которые хранит стиль; идентификатор выбранного стиля
/// записывается в настройки как ссылка (параметры всё равно записаны полностью, поэтому профиль не зависит от файла стиля).
/// </summary>
public static class StyleOps
{
    public static LabSettings Apply(LabSettings s, StyleEntry e) => e.Kind switch
    {
        StyleKind.Keycap => s with { Appearance = ApplyKeycap(s.Appearance, e.Keycap!) with { KeycapStyleId = e.Id } },
        StyleKind.Indicator => s with
        {
            Appearance = s.Appearance with { Indicator = e.Indicator! with { StyleId = e.Id } },
            LayoutIndicator = s.LayoutIndicator with { CustomStyle = true },
        },
        _ => s with
        {
            Appearance = s.Appearance with
            {
                Mouse = e.Mouse! with
                {
                    StyleId = e.Id,
                    ClickColor = s.Appearance.MouseLook.ClickColor,
                    ClickSize = s.Appearance.MouseLook.ClickSize,
                    IconSize = s.Appearance.MouseLook.IconSize,
                    SideButtons = s.Appearance.MouseLook.SideButtons,
                    SideButtonsSide = s.Appearance.MouseLook.SideButtonsSide,
                },
            },
        },
    };

    /// <summary>Поля стиля кейкапа: рендер, палитра, рамка, скругление, подложка, градиент, форма Enter, геометрия двух подписей. Размер, набор подписей и анимация — из профиля.</summary>
    public static AppearanceSettings ApplyKeycap(AppearanceSettings current, AppearanceSettings look) => current with
    {
        Renderer = look.Renderer,
        Primary = look.Primary,
        Secondary = look.Secondary,
        TextColor = look.TextColor,
        BorderEnabled = look.BorderEnabled,
        BorderWidth = look.BorderWidth,
        BorderColor = look.BorderColor,
        Radius = look.Radius,
        BackgroundEnabled = look.BackgroundEnabled,
        BackgroundColor = look.BackgroundColor,
        Backdrop = look.Backdrop, // Phase 4H: материал подложки — часть стиля кейкапа
        UseGradient = look.UseGradient,
        VolEnterShape = look.VolEnterShape,
        TwoLabels = look.TwoLabels, // Phase 4H: геометрия двух подписей — часть стиля кейкапа
        Glow = look.Glow, // Phase 4I: свечение — часть стиля кейкапа
    };

    /// <summary>Текущие параметры совпадают со стилем (иначе — «изменён»: можно сохранить или восстановить).</summary>
    public static bool Matches(LabSettings s, StyleEntry e) => e.Kind switch
    {
        StyleKind.Keycap => KeycapKey(s.Appearance) == KeycapKey(e.Keycap!),
        StyleKind.Indicator => s.Appearance.IndicatorLook with { StyleId = null } == e.Indicator! with { StyleId = null },
        _ => MouseKey(s.Appearance.MouseLook) == MouseKey(e.Mouse!),
    };

    /// <summary>Снимок текущих параметров как новый пользовательский стиль.</summary>
    public static StyleEntry Capture(LabSettings s, StyleKind kind, string id, string name, string? basedOn) => kind switch
    {
        StyleKind.Keycap => new StyleEntry(id, name, kind, false, basedOn, Keycap: ApplyKeycap(AppearanceSettings.Default, s.Appearance) with { KeycapStyleId = null }),
        StyleKind.Indicator => new StyleEntry(id, name, kind, false, basedOn, Indicator: s.Appearance.IndicatorLook with { StyleId = null }),
        _ => new StyleEntry(id, name, kind, false, basedOn, Mouse: Normalize(s.Appearance.MouseLook)),
    };

    public static string? SelectedId(LabSettings s, StyleKind kind) => kind switch
    {
        StyleKind.Keycap => s.Appearance.KeycapStyleId,
        StyleKind.Indicator => s.Appearance.IndicatorLook.StyleId,
        _ => s.Appearance.MouseLook.StyleId,
    };

    /// <summary>Мышь без полей, которые стиль не хранит (размеры у курсора, круг, боковые кнопки).</summary>
    public static MouseVisual Normalize(MouseVisual m)
    {
        var d = MouseVisual.Default;
        return m with
        {
            StyleId = null,
            ClickColor = d.ClickColor,
            ClickSize = d.ClickSize,
            IconSize = d.IconSize,
            SideButtons = d.SideButtons,
            SideButtonsSide = d.SideButtonsSide,
        };
    }

    private static string KeycapKey(AppearanceSettings a) => SettingsSerializer.KeycapLookToJson(a).ToJsonString();

    private static MouseVisual MouseKey(MouseVisual m) => Normalize(m);
}

public sealed record StyleReadResult(StyleEntry? Style, IReadOnlyList<LocText> Errors)
{
    public bool Ok => Style is not null && Errors.Count == 0;
}

/// <summary>
/// Формат файла стиля <c>backpack-style</c>, версия 1. Только данные: никакого XAML, путей к сборкам, скриптов и
/// выражений; неизвестные поля и неверные значения отклоняют файл целиком (без частичного применения). Более новая версия
/// формата отклоняется с понятным сообщением. Идентификаторы <c>backpack.*</c> зарезервированы для встроенных.
/// <code>
/// { "format": "backpack-style", "formatVersion": 1, "id": "u-…", "name": "…", "kind": "keycap|indicator|mouse",
///   "basedOn": "backpack.…" (необязательно), "look": { разделы темы для этого вида } }
/// </code>
/// </summary>
public static class StyleSerializer
{
    public const string Format = "backpack-style";
    public const int FormatVersion = 1;
    public const int MaxFileBytes = 64 * 1024;

    private static readonly string[] KindNames = ["keycap", "indicator", "mouse"];

    public static JsonObject ToJson(StyleEntry e)
    {
        var o = new JsonObject
        {
            ["format"] = Format,
            ["formatVersion"] = FormatVersion,
            ["id"] = e.Id,
            ["name"] = e.Name,
            ["kind"] = KindNames[(int)e.Kind],
        };
        if (e.BasedOn is not null)
        {
            o["basedOn"] = e.BasedOn;
        }

        o["look"] = e.Kind switch
        {
            StyleKind.Keycap => SettingsSerializer.KeycapLookToJson(e.Keycap!),
            StyleKind.Indicator => SettingsSerializer.IndicatorToJson(e.Indicator! with { StyleId = null }),
            _ => MouseLook(e.Mouse!),
        };
        return o;
    }

    public static string Write(StyleEntry e) => ToJson(e).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public static StyleReadResult Read(string json) => Read(json, allowBuiltInId: false);

    internal static StyleReadResult Read(string json, bool allowBuiltInId)
    {
        if (json.Length > MaxFileBytes)
        {
            return Fail(new LocText("Style.Error.TooLarge"));
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 12 });
        }
        catch (JsonException ex)
        {
            return Fail(new LocText("Profile.Error.Syntax", (ex.LineNumber ?? 0) + 1));
        }

        return root is JsonObject o ? Read(o, allowBuiltInId) : Fail(new LocText("Style.Error.NotStyle"));
    }

    public static StyleReadResult Read(JsonObject o, bool allowBuiltInId = false)
    {
        // Объект, собранный в памяти, хранит числа как типизированные значения (int и т. п.); чтение рассчитано на разобранный
        // текст. Нормализация через текст делает результат одинаковым для файла и для объекта.
        if (JsonNode.Parse(o.ToJsonString(), documentOptions: new JsonDocumentOptions { MaxDepth = 12 }) is JsonObject normalized)
        {
            o = normalized;
        }

        if ((o["format"] as JsonValue)?.TryGetValue(out string? format) != true || format != Format)
        {
            return Fail(new LocText("Style.Error.NotStyle"));
        }

        if (o["formatVersion"] is not JsonValue v || !v.TryGetValue(out int version))
        {
            return Fail(new LocText("Profile.Error.NoVersion"));
        }

        if (version != FormatVersion)
        {
            return Fail(version > FormatVersion ? new LocText("Style.Error.Newer", version) : new LocText("Profile.Error.Unsupported", version));
        }

        var errors = new List<LocText>();
        foreach (var (key, _) in o)
        {
            if (key is not ("format" or "formatVersion" or "id" or "name" or "kind" or "basedOn" or "look"))
            {
                errors.Add(new LocText("Settings.Problem.UnknownField", key));
            }
        }

        string? id = Str(o, "id");
        string? name = Str(o, "name")?.Trim();
        string? kindName = Str(o, "kind");
        string? basedOn = o.ContainsKey("basedOn") ? Str(o, "basedOn") : null;
        if (!StyleIds.IsValid(id) || (!allowBuiltInId && StyleIds.IsBuiltIn(id)))
        {
            errors.Add(new LocText("Style.Error.Id"));
        }

        if (!ProfileSerializer.IsValidName(name))
        {
            errors.Add(new LocText("Profile.Error.Name"));
        }

        int kindIndex = Array.IndexOf(KindNames, kindName);
        if (kindIndex < 0)
        {
            errors.Add(new LocText("Style.Error.Kind"));
        }

        if (o.ContainsKey("basedOn") && !StyleIds.IsValid(basedOn))
        {
            errors.Add(new LocText("Style.Error.Id"));
        }

        if (o["look"] is not JsonObject look)
        {
            errors.Add(new LocText("Style.Error.NoLook"));
            return new StyleReadResult(null, errors);
        }

        if (errors.Count > 0)
        {
            return new StyleReadResult(null, errors);
        }

        var kind = (StyleKind)kindIndex;
        var problems = new List<LocText>();
        var r = new SettingsSerializer.Reader(problems);
        StyleEntry entry;
        switch (kind)
        {
            case StyleKind.Keycap:
                Allowed(look, problems, "look.", "keycap", "colors", "text", "border", "group");
                AllowedIn(look, "keycap", problems, "style", "volEnterShape", "twoLabels");
                AllowedIn(look, "colors", problems, "primary", "secondary", "useGradient");
                AllowedIn(look, "text", problems, "color");
                AllowedIn(look, "border", problems, "enabled", "width", "color", "radius", "glow");
                AllowedIn(look, "group", problems, "background");
                var keycap = SettingsSerializer.ReadKeycapLook(r, look, AppearanceSettings.Default with { KeycapStyleId = null }, "look");
                entry = new StyleEntry(id!, name!, kind, false, basedOn, Keycap: StyleOps.ApplyKeycap(AppearanceSettings.Default with { KeycapStyleId = null }, keycap));
                break;
            case StyleKind.Indicator:
                Allowed(look, problems, "look.", "background", "foreground", "border", "borderWidth", "radius", "textSize");
                entry = new StyleEntry(id!, name!, kind, false, basedOn, Indicator: SettingsSerializer.ReadIndicator(r, look, 2, "look"));
                break;
            default:
                Allowed(look, problems, "look.", "renderer", "indicatorBackground", "indicatorForeground", "indicatorAccent", "body", "wheelArrow", "wheelPulseMs", "outlineWidth", "solidOutline", "indicatorMaterial", "glow");
                entry = new StyleEntry(id!, name!, kind, false, basedOn, Mouse: StyleOps.Normalize(SettingsSerializer.ReadMouse(r, look, 2, "look")));
                break;
        }

        return problems.Count > 0 ? new StyleReadResult(null, problems) : new StyleReadResult(entry, []);
    }

    /// <summary>Мышь в файле стиля: без размеров и круга у курсора (их задаёт профиль).</summary>
    private static JsonObject MouseLook(MouseVisual m)
    {
        var o = SettingsSerializer.MouseToJson(StyleOps.Normalize(m));
        o.Remove("clickColor");
        o.Remove("clickSize");
        o.Remove("indicatorSize");
        return o;
    }

    private static void Allowed(JsonObject o, List<LocText> problems, string prefix, params string[] keys)
    {
        foreach (var (key, _) in o)
        {
            if (!keys.Contains(key))
            {
                problems.Add(new LocText("Settings.Problem.UnknownField", prefix + key));
            }
        }
    }

    private static void AllowedIn(JsonObject look, string section, List<LocText> problems, params string[] keys)
    {
        if (look[section] is JsonObject s)
        {
            Allowed(s, problems, "look." + section + ".", keys);
        }
    }

    private static string? Str(JsonObject o, string name) => (o[name] as JsonValue)?.TryGetValue(out string? s) == true ? s : null;

    private static StyleReadResult Fail(LocText e) => new(null, [e]);
}

/// <summary>Итог импорта: стиль в каталоге и что произошло с идентификатором.</summary>
public enum ImportOutcome { Added, AlreadyPresent, Renamed }

public sealed record StyleImportResult(StyleEntry? Style, ImportOutcome Outcome, IReadOnlyList<LocText> Errors)
{
    public bool Ok => Style is not null && Errors.Count == 0;
}

/// <summary>
/// Папка пользовательских стилей: один файл <c>&lt;id&gt;.json</c>, атомарная запись, строгая проверка при чтении.
/// Повреждённые файлы пропускаются и не удаляются. Встроенные стили неизменяемы: их нельзя сохранить, переименовать
/// или удалить; редактирование встроенного сохраняется как пользовательская копия.
/// </summary>
public sealed class StyleStore
{
    private readonly string _dir;

    public StyleStore(string directory) => _dir = directory;

    public string Directory => _dir;

    public static string DirectoryNextTo(string settingsPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "styles");

    /// <summary>Число файлов, пропущенных при последнем чтении (повреждённые, чужие, более новой версии).</summary>
    public int SkippedFiles { get; private set; }

    public IReadOnlyList<StyleEntry> List(StyleKind kind) => ListAll().Where(s => s.Kind == kind).ToList();

    public IReadOnlyList<StyleEntry> ListAll()
    {
        SkippedFiles = 0;
        if (!System.IO.Directory.Exists(_dir))
        {
            return [];
        }

        var list = new List<StyleEntry>();
        foreach (var f in System.IO.Directory.GetFiles(_dir, "*.json"))
        {
            var r = TryRead(f);
            if (r.Ok && Path.GetFileNameWithoutExtension(f) == r.Style!.Id)
            {
                list.Add(r.Style);
            }
            else
            {
                SkippedFiles++;
            }
        }

        return list.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Встроенные и пользовательские стили одного вида: сначала встроенные.</summary>
    public IReadOnlyList<StyleEntry> Catalog(StyleKind kind) => BuiltInStyles.All.Where(s => s.Kind == kind).Concat(List(kind)).ToList();

    public StyleEntry? Find(string? id) => id is null ? null : BuiltInStyles.Find(id) ?? (Load(id) is { Ok: true } r ? r.Style : null);

    public StyleReadResult Load(string id) =>
        StyleIds.IsValid(id) && !StyleIds.IsBuiltIn(id) && File.Exists(PathOf(id)) ? TryRead(PathOf(id)) : new StyleReadResult(null, [new LocText("Profile.Error.Missing")]);

    public void Save(StyleEntry e)
    {
        if (e.BuiltIn || StyleIds.IsBuiltIn(e.Id))
        {
            throw new InvalidOperationException("Встроенный стиль неизменяем.");
        }

        AtomicFile.Write(PathOf(e.Id), StyleSerializer.Write(e));
    }

    public StyleEntry SaveAsNew(StyleEntry e, string name)
    {
        var copy = e with { Id = StyleIds.NewId(), Name = UniqueName(name.Trim(), e.Kind), BuiltIn = false };
        Save(copy);
        return copy;
    }

    public StyleEntry Duplicate(StyleEntry e, Func<string, string> copyName) =>
        SaveAsNew(e with { BasedOn = e.BuiltIn ? e.Id : e.BasedOn }, copyName(e.BuiltIn ? BuiltInStyles.LocalizedName(e) : e.Name));

    public StyleEntry? Rename(string id, string name)
    {
        var r = Load(id);
        if (!r.Ok)
        {
            return null;
        }

        var e = r.Style! with { Name = UniqueName(name.Trim(), r.Style.Kind, except: id) };
        Save(e);
        return e;
    }

    public bool Delete(string id)
    {
        if (!StyleIds.IsValid(id) || StyleIds.IsBuiltIn(id) || !File.Exists(PathOf(id)))
        {
            return false;
        }

        File.Delete(PathOf(id));
        return true;
    }

    public void Export(StyleEntry e, string path) => AtomicFile.Write(path, StyleSerializer.Write(e with { BuiltIn = false }));

    public StyleImportResult Import(string path)
    {
        try
        {
            if (new FileInfo(path).Length > StyleSerializer.MaxFileBytes)
            {
                return new StyleImportResult(null, ImportOutcome.Added, [new LocText("Style.Error.TooLarge")]);
            }

            return Import(StyleSerializer.Read(File.ReadAllText(path), allowBuiltInId: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new StyleImportResult(null, ImportOutcome.Added, [new LocText("Profile.Error.ReadFailed", ex.GetType().Name)]);
        }
    }

    /// <summary>
    /// Конфликт идентификаторов: тот же id и те же параметры — стиль уже есть (дубль не создаётся); тот же id и другие
    /// параметры, или id встроенного стиля — сохраняется под новым id с пометкой в имени. Существующий файл не перезаписывается.
    /// </summary>
    public StyleImportResult Import(StyleReadResult read)
    {
        if (!read.Ok)
        {
            return new StyleImportResult(null, ImportOutcome.Added, read.Errors);
        }

        var incoming = read.Style!;
        var existing = Find(incoming.Id);
        if (existing is not null)
        {
            if (SameLook(existing, incoming))
            {
                return new StyleImportResult(existing, ImportOutcome.AlreadyPresent, []);
            }

            var renamed = incoming with { Id = StyleIds.NewId(), Name = UniqueName(Loc.F("Style.ImportedName", incoming.Name), incoming.Kind), BuiltIn = false };
            Save(renamed);
            return new StyleImportResult(renamed, ImportOutcome.Renamed, []);
        }

        var added = incoming with { Name = UniqueName(incoming.Name, incoming.Kind), BuiltIn = false };
        Save(added);
        return new StyleImportResult(added, ImportOutcome.Added, []);
    }

    public static bool SameLook(StyleEntry a, StyleEntry b) =>
        a.Kind == b.Kind && StyleSerializer.ToJson(a with { Id = "x-x", Name = "x", BasedOn = null })["look"]!.ToJsonString()
            == StyleSerializer.ToJson(b with { Id = "x-x", Name = "x", BasedOn = null })["look"]!.ToJsonString();

    public string UniqueName(string name, StyleKind kind, string? except = null)
    {
        var names = List(kind).Where(s => s.Id != except).Select(s => s.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
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

    private static StyleReadResult TryRead(string file)
    {
        try
        {
            return StyleSerializer.Read(File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new StyleReadResult(null, [new LocText("Profile.Error.ReadFailed", ex.GetType().Name)]);
        }
    }
}
