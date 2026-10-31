using System.Text.Json;
using System.Text.Json.Nodes;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Windows;
using KeyOverlay.Visual.Rendering;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Результат чтения: настройки всегда пригодны к использованию; проблемы — списком сообщений на русском.
/// <paramref name="MigratedFrom"/> — исходная версия формата, если файл был старее текущей.
/// </summary>
public sealed record SettingsReadResult(LabSettings Settings, IReadOnlyList<LocText> Problems, bool FileUnusable, int? MigratedFrom = null);

/// <summary>
/// Формат файла настроек (<c>keyoverlay-lab-settings</c>). Версия 4 (Phase 3B: раздел <c>drawing</c> — панель рисования и снимки)
/// — текущая; версии 1 (Phase 2B), 2 (Phase 2C/2D) и 3 (Phase 2E) читаются с миграцией: новые поля получают значения по умолчанию.
/// Раздел <c>appearance</c> повторяет форму разделов темы (keycap, colors, text, layout, border, group, animation, mouse,
/// languageIndicator), чтобы не было второго формата цветов и единиц. Схема — docs/settings/keyoverlay-lab-settings.schema.json.
/// Чтение мягкое: неверное поле → значение по умолчанию этого поля и сообщение; остальные поля сохраняются.
/// </summary>
public static class SettingsSerializer
{
    public const string Format = "keyoverlay-lab-settings";
    public const int FormatVersion = 9;

    private static readonly (string, KeycapRenderer)[] Renderers =
        [("lowprofile", KeycapRenderer.LowProfile), ("laptop", KeycapRenderer.Laptop), ("pbt", KeycapRenderer.Pbt), ("minimal", KeycapRenderer.Minimal), ("volumetric", KeycapRenderer.Volumetric), ("neon", KeycapRenderer.Neon)];

    /// <summary>Материал подложки (Phase 4H): <c>group.background.material</c> и <c>mouse.indicatorMaterial</c>; «fill» не пишется.</summary>
    private static readonly (string, BackdropMaterial)[] Backdrops =
        [("fill", BackdropMaterial.Fill), ("clear", BackdropMaterial.Clear), ("acrylic", BackdropMaterial.Acrylic), ("mica", BackdropMaterial.Mica)];

    private static string? BackdropName(BackdropMaterial m) => m == BackdropMaterial.Fill ? null : Backdrops.First(x => x.Item2 == m).Item1;

    private static readonly (string, MouseRenderer)[] MouseRenderers =
        [("outline", MouseRenderer.Outline), ("solid", MouseRenderer.Solid), ("retro", MouseRenderer.Retro), ("sketch", MouseRenderer.Sketch), ("neon", MouseRenderer.Neon)];

    public static string RendererName(KeycapRenderer r) => Renderers.First(x => x.Item2 == r).Item1;

    public static string MouseRendererName(MouseRenderer r) => MouseRenderers.First(x => x.Item2 == r).Item1;

    private static readonly string[] AnchorNames =
    [
        "top-left", "top-center", "top-right", "center-left", "center", "center-right", "bottom-left", "bottom-center", "bottom-right",
    ];

    private static readonly (string, ScreenAnchor)[] Anchors = AnchorNames.Select((n, i) => (n, (ScreenAnchor)i)).ToArray();

    public static string AnchorName(ScreenAnchor a) => AnchorNames[(int)a];

    public static string Write(LabSettings s) => ToJson(s).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public static JsonObject ToJson(LabSettings s) => new()
    {
        ["format"] = Format,
        ["formatVersion"] = FormatVersion,
        ["appearance"] = AppearanceToJson(s.Appearance),
        ["keys"] = new JsonObject
        {
            ["filter"] = s.Keys.Filter == KeyFilter.All ? "all" : "combos",
            ["trail"] = s.Keys.Trail == TrailMode.None ? "none" : "fade-in-place",
            ["lingerMs"] = s.Keys.LingerMs,
            ["mouseInRow"] = s.Keys.MouseInRow,
            ["mouseLabels"] = s.Keys.MouseLabels == MouseLabelMode.Graphic ? "graphic" : "text",
            ["chords"] = s.Keys.Chords == ChordMode.AnyHeld ? "any-held" : "modifiers",
            ["anyHeldMs"] = s.Keys.AnyHeldMs,
        },
        ["placement"] = new JsonObject
        {
            ["anchor"] = AnchorName(s.Placement.Anchor),
            ["edgeX"] = s.Placement.EdgeX,
            ["edgeY"] = s.Placement.EdgeY,
            ["offsetX"] = s.Placement.OffsetX,
            ["offsetY"] = s.Placement.OffsetY,
        },
        ["mouseCursor"] = new JsonObject
        {
            ["enabled"] = s.MouseCursor.Enabled,
            ["showIcon"] = s.MouseCursor.ShowIcon,
            ["showCircle"] = s.MouseCursor.ShowCircle,
            ["iconOffsetX"] = s.MouseCursor.OffsetX,
            ["iconOffsetY"] = s.MouseCursor.OffsetY,
            ["showHighlight"] = s.MouseCursor.ShowHighlight,
            ["highlightColor"] = s.MouseCursor.Highlight.ToString(),
            ["highlightSize"] = s.MouseCursor.HighlightSize,
            ["sideButtons"] = s.Appearance.MouseLook.SideButtons switch { SideButtonsMode.Hidden => "hidden", SideButtonsMode.One => "one", _ => "two" },
            ["sideButtonsSide"] = s.Appearance.MouseLook.SideButtonsSide == SideButtonsSide.Left ? "left" : "right",
        },
        ["drawing"] = DrawingToJson(s.DrawingOrDefault),
        ["displays"] = DisplaysToJson(s.DisplaysOrDefault),
        ["fonts"] = WithoutNulls(new JsonObject { ["ui"] = s.FontsOrDefault.Ui, ["keycap"] = s.FontsOrDefault.Keycap }),
        ["labels"] = WithoutNulls(new JsonObject
        {
            ["secondary"] = SecondaryModes.First(x => x.Item2 == s.LabelsOrDefault.Mode).Item1,
            ["layout"] = s.LabelsOrDefault.Layout,
        }),
        ["plugins"] = new JsonObject(s.PluginsOrEmpty.Map.Select(x => KeyValuePair.Create(x.Key, JsonNode.Parse(x.Value)))),
        ["layoutIndicator"] = new JsonObject
        {
            ["enabled"] = s.LayoutIndicator.Enabled,
            ["durationMs"] = s.LayoutIndicator.DurationMs,
            ["showOnWindowSwitch"] = s.LayoutIndicator.ShowOnWindowSwitch,
            ["style"] = s.LayoutIndicator.CustomStyle ? "custom" : "inherit",
            ["animation"] = s.LayoutIndicator.Animation switch { IndicatorAnimation.Fade => "fade", IndicatorAnimation.None => "none", _ => "inherit" },
            ["position"] = s.LayoutIndicator.Position == IndicatorPosition.AboveOverlay ? "above-overlay" : "screen",
            ["anchor"] = AnchorName(s.LayoutIndicator.Anchor),
            ["offsetX"] = s.LayoutIndicator.OffsetX,
            ["offsetY"] = s.LayoutIndicator.OffsetY,
        },
    };

    private static readonly (string, DrawToolbarMode)[] ToolbarModes = [("docked", DrawToolbarMode.Docked), ("auto-hide", DrawToolbarMode.AutoHide), ("radial-only", DrawToolbarMode.RadialOnly)];
    private static readonly (string, ScreenEdge)[] Edges = [("top", ScreenEdge.Top), ("bottom", ScreenEdge.Bottom), ("left", ScreenEdge.Left), ("right", ScreenEdge.Right)];
    private static readonly (string, ScreenshotAction)[] ShotActions = [("save", ScreenshotAction.Save), ("copy", ScreenshotAction.Copy), ("save-and-copy", ScreenshotAction.SaveAndCopy)];
    private static readonly (string, AfterScreenshot)[] AfterShots = [("keep", AfterScreenshot.KeepDrawings), ("clear", AfterScreenshot.ClearDrawings), ("exit", AfterScreenshot.ExitDrawing)];

    private static readonly (string, LabelCorner)[] Corners =
        [("top-left", LabelCorner.TopLeft), ("top-right", LabelCorner.TopRight), ("bottom-left", LabelCorner.BottomLeft), ("bottom-right", LabelCorner.BottomRight)];

    private static readonly (string, SecondaryLabelMode)[] SecondaryModes =
        [("off", SecondaryLabelMode.Off), ("active-layout", SecondaryLabelMode.ActiveLayout), ("fixed-layout", SecondaryLabelMode.FixedLayout)];

    private static readonly (string, MonitorMode)[] MonitorModes =
        [("primary", MonitorMode.Primary), ("selected", MonitorMode.Selected), ("multiple", MonitorMode.Multiple), ("under-cursor", MonitorMode.UnderCursor)];

    private static JsonObject TargetToJson(MonitorTarget t) => new()
    {
        ["mode"] = MonitorModes.First(x => x.Item2 == t.Mode).Item1,
        ["monitors"] = new JsonArray(t.Monitors.Select(m => (JsonNode)new JsonObject { ["id"] = m.Id, ["name"] = m.Name }).ToArray()),
    };

    private static JsonObject DisplaysToJson(DisplaySettings d) => new()
    {
        ["keycaps"] = TargetToJson(d.Keycaps),
        ["indicator"] = d.IndicatorInherits ? new JsonObject { ["mode"] = "inherit" } : TargetToJson(d.Indicator),
    };

    /// <summary>
    /// Выбор мониторов из файла. Неверный режим — «основной»; записи без id или с слишком длинными строками пропускаются
    /// (не более 16 мониторов). Отсутствие монитора на этом компьютере здесь не проверяется — это решается при показе.
    /// </summary>
    private static MonitorTarget ReadTarget(Reader r, JsonObject? o, string path, List<LocText> problems)
    {
        var mode = r.Enum(o, "mode", path + ".mode", MonitorMode.Primary, MonitorModes);
        var list = new List<MonitorRef>();
        if (o?["monitors"] is JsonArray arr)
        {
            foreach (var n in arr.Take(16))
            {
                if (n is JsonObject m && m["id"] is JsonValue idv && idv.TryGetValue(out string? id) && id.Length is > 0 and <= 512)
                {
                    string name = m["name"] is JsonValue nv && nv.TryGetValue(out string? nm) && nm.Length <= 256 ? nm : id;
                    list.Add(new MonitorRef(id, name));
                }
                else
                {
                    problems.Add(new LocText("Settings.Problem.Value", path + ".monitors"));
                }
            }
        }

        return mode is MonitorMode.Selected or MonitorMode.Multiple && list.Count == 0 ? MonitorTarget.Primary : new MonitorTarget(mode, list);
    }

    private static JsonObject DrawingToJson(DrawingProfile d)
    {
        var o = new JsonObject
        {
            ["toolbarMode"] = ToolbarModes.First(x => x.Item2 == d.ToolbarMode).Item1,
            ["autoHideEdge"] = Edges.First(x => x.Item2 == d.AutoHideEdge).Item1,
            ["screenshotAction"] = ShotActions.First(x => x.Item2 == d.ShotAction).Item1,
            ["afterScreenshot"] = AfterShots.First(x => x.Item2 == d.AfterShot).Item1,
            ["compact"] = d.Compact,
        };
        if (d.ToolbarX is { } x && d.ToolbarY is { } y)
        {
            o["toolbarX"] = x;
            o["toolbarY"] = y;
        }

        return o;
    }

    /// <summary>Разделы внешнего вида в форме схемы темы (проверяется тестом по самой схеме).</summary>
    public static JsonObject AppearanceToJson(AppearanceSettings a)
    {
        var m = a.MouseLook;
        var li = a.IndicatorLook;
        return new JsonObject
        {
            ["keycap"] = WithoutNulls(new JsonObject
            {
                ["style"] = RendererName(a.Renderer),
                ["size"] = a.Size,
                ["volEnterShape"] = a.VolEnterShape,
                ["styleId"] = a.KeycapStyleId,
                ["twoLabels"] = TwoLabelsToJson(a.TwoLabelsOrDefault),
            }),
            ["colors"] = new JsonObject { ["primary"] = a.Primary.ToString(), ["secondary"] = a.Secondary.ToString(), ["useGradient"] = a.UseGradient },
            ["text"] = new JsonObject
            {
                ["color"] = a.TextColor.ToString(),
                ["variant"] = a.LabelMode == LabelMode.Symbol ? "icon" : "text-short",
                ["labelSet"] = LabelSetName(a.LabelSet),
            },
            ["layout"] = new JsonObject
            {
                ["showIcon"] = a.LabelMode != LabelMode.Text,
                ["showPressCount"] = a.ShowPressCount,
                ["sideMarkers"] = a.SideMarkers,
            },
            ["border"] = new JsonObject
            {
                ["enabled"] = a.BorderEnabled,
                ["width"] = a.BorderWidth,
                ["color"] = a.BorderColor.ToString(),
                ["radius"] = a.Radius,
                ["glow"] = a.Glow,
            },
            ["group"] = new JsonObject
            {
                ["background"] = GroupBackgroundToJson(a),
            },
            ["animation"] = new JsonObject
            {
                ["type"] = a.Animation == AnimationKind.Fade ? "fade" : "none",
                ["durationMs"] = a.DurationMs,
                ["press"] = a.PressAnimation,
            },
            ["mouse"] = MouseToJson(m),
            ["languageIndicator"] = IndicatorToJson(li),
        };
    }

    /// <summary>Внешний вид мыши (без боковых кнопок — это настройка оборудования, раздел <c>mouseCursor</c>).</summary>
    public static JsonObject MouseToJson(MouseVisual m) => WithoutNulls(new JsonObject
    {
        ["renderer"] = MouseRendererName(m.Renderer),
        ["clickColor"] = m.ClickColor.ToString(),
        ["clickSize"] = m.ClickSize,
        ["indicatorSize"] = m.IconSize,
        ["indicatorBackground"] = m.IconBackground.ToString(),
        ["indicatorMaterial"] = BackdropName(m.Plate),
        ["indicatorForeground"] = m.IconForeground.ToString(),
        ["indicatorAccent"] = m.IconAccent.ToString(),
        ["body"] = m.BodyColor?.ToString(),
        ["wheelArrow"] = m.WheelArrowColor?.ToString(),
        ["wheelPulseMs"] = m.WheelPulseMs,
        ["outlineWidth"] = m.OutlineWidth,
        ["solidOutline"] = m.SolidOutline?.ToString(),
        ["glow"] = m.Glow > 0 ? m.Glow : null,
        ["styleId"] = m.StyleId,
    });

    public static JsonObject IndicatorToJson(IndicatorVisual li) => WithoutNulls(new JsonObject
    {
        ["background"] = li.Background.ToString(),
        ["foreground"] = li.Foreground.ToString(),
        ["border"] = li.Border.ToString(),
        ["borderWidth"] = li.BorderWidth,
        ["radius"] = li.Radius,
        ["textSize"] = li.TextSize,
        ["styleId"] = li.StyleId,
    });

    /// <summary>Только визуальные параметры кейкапа — то, что сохраняет стиль кейкапа (форма разделов темы, без размера).</summary>
    public static JsonObject KeycapLookToJson(AppearanceSettings a) => new()
    {
        ["keycap"] = new JsonObject { ["style"] = RendererName(a.Renderer), ["volEnterShape"] = a.VolEnterShape, ["twoLabels"] = TwoLabelsToJson(a.TwoLabelsOrDefault) },
        ["colors"] = new JsonObject { ["primary"] = a.Primary.ToString(), ["secondary"] = a.Secondary.ToString(), ["useGradient"] = a.UseGradient },
        ["text"] = new JsonObject { ["color"] = a.TextColor.ToString() },
        ["border"] = new JsonObject { ["enabled"] = a.BorderEnabled, ["width"] = a.BorderWidth, ["color"] = a.BorderColor.ToString(), ["radius"] = a.Radius, ["glow"] = a.Glow },
        ["group"] = new JsonObject { ["background"] = GroupBackgroundToJson(a) },
    };

    private static JsonObject GroupBackgroundToJson(AppearanceSettings a) => WithoutNulls(new JsonObject
    {
        ["enabled"] = a.BackgroundEnabled,
        ["color"] = a.BackgroundColor.ToString(),
        ["material"] = BackdropName(a.Backdrop),
    });

    /// <summary>Геометрия двух подписей (settings v9): <c>appearance.keycap.twoLabels</c>, тот же объект в стилях кейкапов.</summary>
    public static JsonObject TwoLabelsToJson(TwoLabelLayout t) => new()
    {
        ["mainSize"] = t.MainScale,
        ["secondarySize"] = t.SecondaryScale,
        ["corner"] = Corners.First(x => x.Item2 == t.Corner).Item1,
        ["offsetX"] = t.OffsetX,
        ["offsetY"] = t.OffsetY,
    };

    internal static TwoLabelLayout ReadTwoLabels(Reader r, JsonObject? o, string path, TwoLabelLayout basis) => new TwoLabelLayout(
        r.Num(o, "mainSize", path + ".mainSize", basis.MainScale, TwoLabelLayout.MainRange),
        r.Num(o, "secondarySize", path + ".secondarySize", basis.SecondaryScale, TwoLabelLayout.SecondaryRange),
        r.Enum(o, "corner", path + ".corner", basis.Corner, Corners),
        r.Num(o, "offsetX", path + ".offsetX", basis.OffsetX, TwoLabelLayout.OffsetRange),
        r.Num(o, "offsetY", path + ".offsetY", basis.OffsetY, TwoLabelLayout.OffsetRange)).Sanitized();

    /// <summary>Значения по умолчанию хранятся как «не задано» (null): записи с одинаковыми параметрами равны.</summary>
    private static TwoLabelLayout? DefaultAsNull(TwoLabelLayout t) => t == TwoLabelLayout.Default ? null : t;

    private static JsonObject WithoutNulls(JsonObject o)
    {
        foreach (var key in o.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList())
        {
            o.Remove(key);
        }

        return o;
    }

    public static string LabelSetName(LabelSet s) => s switch { LabelSet.MacOS => "macos", LabelSet.Linux => "linux", _ => "windows" };

    public static SettingsReadResult Read(string json)
    {
        var problems = new List<LocText>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException ex)
        {
            return Unusable(new LocText("Settings.Problem.Corrupt", (ex.LineNumber ?? 0) + 1));
        }

        if (root is not JsonObject obj)
        {
            return Unusable(new LocText("Settings.Problem.NotObject"));
        }

        if (Str(obj, "format") != Format)
        {
            return Unusable(new LocText("Settings.Problem.NoFormat"));
        }

        if (obj["formatVersion"] is not JsonValue v || !v.TryGetValue(out int version))
        {
            return Unusable(new LocText("Settings.Problem.NoVersion"));
        }

        if (version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9))
        {
            return Unusable(version > FormatVersion
                ? new LocText("Settings.Problem.Newer", version)
                : new LocText("Settings.Problem.Unsupported", version));
        }

        var r = new Reader(problems);
        var d = LabSettings.Default;
        if (version == 1)
        {
            Unknown(obj, problems, "", "format", "formatVersion", "appearance", "placement", "behavior", "layoutIndicator");
        }
        else
        {
            Unknown(obj, problems, "", "format", "formatVersion", "appearance", "keys", "placement", "mouseCursor", "layoutIndicator", "drawing", "displays", "fonts", "labels", "plugins");
        }

        var appearance = ReadAppearance(r, problems, r.Obj(obj, "appearance"), version);

        KeysSettings keys;
        PlacementSettings placement;
        LayoutIndicatorSettings indicator;
        MouseCursorSettings cursor = d.MouseCursor;
        var dk = d.Keys;
        var dp = d.Placement;
        var dl = d.LayoutIndicator;

        if (version == 1)
        {
            // Миграция Phase 2B → 2C: прежние отступы были расстоянием от края по осям, где якорь у края, — это EdgeX/EdgeY;
            // смещения не было. Положение на экране не меняется.
            var pl = r.Obj(obj, "placement");
            placement = new PlacementSettings(
                r.Enum(pl, "anchor", "placement.anchor", dp.Anchor, Anchors),
                r.Num(pl, "marginX", "placement.marginX", dp.EdgeX, Limits.Edge),
                r.Num(pl, "marginY", "placement.marginY", dp.EdgeY, Limits.Edge),
                0, 0);
            var bh = r.Obj(obj, "behavior");
            keys = dk with { LingerMs = r.Int(bh, "lingerMs", "behavior.lingerMs", dk.LingerMs, Limits.LingerMs) };
            var li = r.Obj(obj, "layoutIndicator");
            bool center = r.Enum(li, "position", "layoutIndicator.position", false, ("above-overlay", false), ("screen-center", true));
            indicator = dl with
            {
                Enabled = r.Bool(li, "enabled", "layoutIndicator.enabled", dl.Enabled),
                DurationMs = r.Int(li, "durationMs", "layoutIndicator.durationMs", dl.DurationMs, Limits.IndicatorDurationMs),
                ShowOnWindowSwitch = r.Bool(li, "showOnWindowSwitch", "layoutIndicator.showOnWindowSwitch", dl.ShowOnWindowSwitch),
                Position = center ? IndicatorPosition.Screen : IndicatorPosition.AboveOverlay,
                Anchor = ScreenAnchor.Center,
            };
        }
        else
        {
            var k = r.Obj(obj, "keys");
            keys = new KeysSettings(
                r.Enum(k, "filter", "keys.filter", dk.Filter, ("all", KeyFilter.All), ("combos", KeyFilter.CombosOnly)),
                r.Enum(k, "trail", "keys.trail", dk.Trail, ("none", TrailMode.None), ("fade-in-place", TrailMode.FadeInPlace)),
                r.Int(k, "lingerMs", "keys.lingerMs", dk.LingerMs, Limits.LingerMs),
                r.Bool(k, "mouseInRow", "keys.mouseInRow", dk.MouseInRow),
                r.Enum(k, "mouseLabels", "keys.mouseLabels", dk.MouseLabels, ("graphic", MouseLabelMode.Graphic), ("text", MouseLabelMode.Text)),
                version >= 3 ? r.Enum(k, "chords", "keys.chords", dk.Chords, ("modifiers", ChordMode.Modifiers), ("any-held", ChordMode.AnyHeld)) : dk.Chords,
                version >= 4 ? r.Int(k, "anyHeldMs", "keys.anyHeldMs", dk.AnyHeldMs, Limits.AnyHeldMs) : dk.AnyHeldMs);

            var pl = r.Obj(obj, "placement");
            placement = new PlacementSettings(
                r.Enum(pl, "anchor", "placement.anchor", dp.Anchor, Anchors),
                r.Num(pl, "edgeX", "placement.edgeX", dp.EdgeX, Limits.Edge),
                r.Num(pl, "edgeY", "placement.edgeY", dp.EdgeY, Limits.Edge),
                r.Num(pl, "offsetX", "placement.offsetX", dp.OffsetX, Limits.Offset),
                r.Num(pl, "offsetY", "placement.offsetY", dp.OffsetY, Limits.Offset));

            var mc = r.Obj(obj, "mouseCursor");
            var dm = d.MouseCursor;
            cursor = new MouseCursorSettings(
                r.Bool(mc, "enabled", "mouseCursor.enabled", dm.Enabled),
                r.Bool(mc, "showIcon", "mouseCursor.showIcon", dm.ShowIcon),
                r.Bool(mc, "showCircle", "mouseCursor.showCircle", dm.ShowCircle),
                r.Num(mc, "iconOffsetX", "mouseCursor.iconOffsetX", dm.OffsetX, Limits.CursorOffset),
                r.Num(mc, "iconOffsetY", "mouseCursor.iconOffsetY", dm.OffsetY, Limits.CursorOffset),
                version >= 6 && r.Bool(mc, "showHighlight", "mouseCursor.showHighlight", dm.ShowHighlight),
                version >= 6 && r.Color(mc, "highlightColor", "mouseCursor.highlightColor", MouseCursorSettings.DefaultHighlight) is var hc && hc != MouseCursorSettings.DefaultHighlight ? hc : null,
                version >= 6 ? r.Num(mc, "highlightSize", "mouseCursor.highlightSize", dm.HighlightSize, Limits.HighlightSize) : dm.HighlightSize);
            if (version >= 3)
            {
                var look = appearance.MouseLook;
                appearance = appearance with
                {
                    Mouse = look with
                    {
                        SideButtons = r.Enum(mc, "sideButtons", "mouseCursor.sideButtons", look.SideButtons, ("hidden", SideButtonsMode.Hidden), ("one", SideButtonsMode.One), ("two", SideButtonsMode.Two)),
                        SideButtonsSide = r.Enum(mc, "sideButtonsSide", "mouseCursor.sideButtonsSide", look.SideButtonsSide, ("left", SideButtonsSide.Left), ("right", SideButtonsSide.Right)),
                    },
                };
            }

            var li = r.Obj(obj, "layoutIndicator");
            indicator = new LayoutIndicatorSettings(
                r.Bool(li, "enabled", "layoutIndicator.enabled", dl.Enabled),
                r.Int(li, "durationMs", "layoutIndicator.durationMs", dl.DurationMs, Limits.IndicatorDurationMs),
                r.Bool(li, "showOnWindowSwitch", "layoutIndicator.showOnWindowSwitch", dl.ShowOnWindowSwitch),
                r.Enum(li, "style", "layoutIndicator.style", dl.CustomStyle, ("inherit", false), ("custom", true)),
                r.Enum(li, "animation", "layoutIndicator.animation", dl.Animation, ("inherit", IndicatorAnimation.Inherit), ("fade", IndicatorAnimation.Fade), ("none", IndicatorAnimation.None)),
                r.Enum(li, "position", "layoutIndicator.position", dl.Position, ("above-overlay", IndicatorPosition.AboveOverlay), ("screen", IndicatorPosition.Screen)),
                r.Enum(li, "anchor", "layoutIndicator.anchor", dl.Anchor, Anchors),
                r.Num(li, "offsetX", "layoutIndicator.offsetX", dl.OffsetX, Limits.Offset),
                r.Num(li, "offsetY", "layoutIndicator.offsetY", dl.OffsetY, Limits.Offset));
        }

        var drawing = DrawingProfile.Default;
        if (version >= 4)
        {
            var dr = r.Obj(obj, "drawing");
            var dd = DrawingProfile.Default;
            double? tx = r.OptNum(dr, "toolbarX", "drawing.toolbarX", (0, 1)), ty = r.OptNum(dr, "toolbarY", "drawing.toolbarY", (0, 1));
            drawing = new DrawingProfile(
                r.Enum(dr, "toolbarMode", "drawing.toolbarMode", dd.ToolbarMode, ToolbarModes),
                r.Enum(dr, "autoHideEdge", "drawing.autoHideEdge", dd.AutoHideEdge, Edges),
                tx is not null && ty is not null ? tx : null,
                tx is not null && ty is not null ? ty : null,
                r.Enum(dr, "screenshotAction", "drawing.screenshotAction", dd.ShotAction, ShotActions),
                r.Enum(dr, "afterScreenshot", "drawing.afterScreenshot", dd.AfterShot, AfterShots),
                version >= 6 && r.Bool(dr, "compact", "drawing.compact", dd.Compact));
        }

        var displays = DisplaySettings.Default;
        if (version >= 5 && r.Obj(obj, "displays") is { } ds)
        {
            var keycaps = ReadTarget(r, ds["keycaps"] as JsonObject, "displays.keycaps", problems);
            var ind = ds["indicator"] as JsonObject;
            bool inherit = ind is null || (ind["mode"] is JsonValue im && im.TryGetValue(out string? imode) && imode == "inherit");
            displays = new DisplaySettings(keycaps, inherit, inherit ? MonitorTarget.Primary : ReadTarget(r, ind, "displays.indicator", problems));
        }

        if (version < 6 && cursor.Enabled && cursor.ShowCircle)
        {
            // До v6 «круг щелчка» был виден постоянно. Теперь он появляется при нажатии, а постоянный круг — «подсветка курсора»:
            // включается с прежними цветом и размером, поэтому вид у курсора не меняется молча.
            cursor = cursor with { ShowHighlight = true, HighlightColor = appearance.MouseLook.ClickColor, HighlightSize = Math.Clamp(appearance.MouseLook.ClickSize, Limits.HighlightSize.Min, Limits.HighlightSize.Max) };
        }

        var fonts = FontSettings.Default;
        var labels = SecondaryLabelSettings.Default;
        if (version >= 6)
        {
            var fo = r.Obj(obj, "fonts");
            // v7: «Стандартный» (поля нет) или "Spectral". v6 допускал любое установленное семейство — оно переводится на «Стандартный».
            fonts = version >= 7
                ? new FontSettings(r.Enum(fo, "ui", "fonts.ui", (string?)null, (FontSettings.Spectral, FontSettings.Spectral)), r.Enum(fo, "keycap", "fonts.keycap", (string?)null, (FontSettings.Spectral, FontSettings.Spectral)))
                : new FontSettings(r.OptString(fo, "ui", "fonts.ui", FontSettings.MaxNameLength), r.OptString(fo, "keycap", "fonts.keycap", FontSettings.MaxNameLength)).Sanitized();
            var lo = r.Obj(obj, "labels");
            labels = new SecondaryLabelSettings(
                r.Enum(lo, "secondary", "labels.secondary", SecondaryLabelMode.Off, SecondaryModes),
                r.OptString(lo, "layout", "labels.layout", 8)).Sanitized();
            if (version == 8)
            {
                // v8 хранил угол, размер (доля основной подписи) и сдвиг (от угла тесной области основной подписи) в labels.
                // v9: геометрия — в виде кейкапа и отсчитывается от безопасной точки у угла лицевой поверхности. Угол переносится,
                // размер пересчитывается в долю стандартной подписи, прежний сдвиг сбрасывается: в новой системе координат
                // старые крайние значения (например −10 %) вытолкнули бы подпись к краю.
                var t = TwoLabelLayout.Default;
                double oldSize = r.Num(lo, "size", "labels.size", 0.5, (0.3, 0.8));
                appearance = appearance with
                {
                    TwoLabels = (t with { Corner = r.Enum(lo, "corner", "labels.corner", t.Corner, Corners), SecondaryScale = Math.Round(oldSize * 0.86, 2) }).Sanitized(),
                };
            }
        }

        var plugins = PluginProfileValues.Empty;
        if (version >= 8 && r.Obj(obj, "plugins") is { } po)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var (id, value) in po)
            {
                if (value is JsonObject vo && vo.ToJsonString() is { Length: <= PluginProfileValues.MaxBytes } pjson && Plugins.PluginManifest.IsValidId(id))
                {
                    list.Add(KeyValuePair.Create(id, pjson));
                }
                else
                {
                    problems.Add(new LocText("Settings.Problem.NotSection", "plugins." + id));
                }
            }

            plugins = new PluginProfileValues(list);
        }

        return new SettingsReadResult(new LabSettings(appearance, placement, keys, indicator, cursor, drawing, displays, fonts, labels, plugins), problems, false, version < FormatVersion ? version : null);

        static SettingsReadResult Unusable(LocText message) => new(LabSettings.Default, [message], true);
    }

    private static AppearanceSettings ReadAppearance(Reader r, List<LocText> problems, JsonObject? ap, int version)
    {
        var keycap = r.Obj(ap, "keycap", "appearance.");
        var colors = r.Obj(ap, "colors", "appearance.");
        var text = r.Obj(ap, "text", "appearance.");
        var layout = r.Obj(ap, "layout", "appearance.");
        var border = r.Obj(ap, "border", "appearance.");
        var group = r.Obj(ap, "group", "appearance.");
        var bg = r.Obj(group, "background", "appearance.group.");
        var anim = r.Obj(ap, "animation", "appearance.");
        var mouse = r.Obj(ap, "mouse", "appearance.");
        var lind = r.Obj(ap, "languageIndicator", "appearance.");
        if (ap is not null)
        {
            Unknown(ap, problems, "appearance.", "keycap", "colors", "text", "layout", "border", "group", "animation", "mouse", "languageIndicator");
        }

        var da = AppearanceSettings.Default;
        // До версии 3 стиль кейкапа всегда был lowprofile, а идентификаторов стилей не было. Миграция сохраняет параметры
        // как есть и не привязывает старые настройки к пресету (иначе возникла бы ложная отметка «изменён»).
        var renderer = r.Enum(keycap, "style", "appearance.keycap.style", da.Renderer, Renderers);
        string? keycapStyleId = version >= 3 ? r.Id(keycap, "styleId", "appearance.keycap.styleId") : null;
        string variant = r.Enum(text, "variant", "appearance.text.variant", "text-short", ("text", "text"), ("text-short", "text-short"), ("icon", "icon"));
        bool showIcon = r.Bool(layout, "showIcon", "appearance.layout.showIcon", true);
        var mode = variant == "icon" ? LabelMode.Symbol : showIcon ? LabelMode.Mixed : LabelMode.Text;

        return new AppearanceSettings(
            Size: r.Num(keycap, "size", "appearance.keycap.size", da.Size, Limits.Size),
            Radius: r.Num(border, "radius", "appearance.border.radius", da.Radius, Limits.Radius),
            Primary: r.Color(colors, "primary", "appearance.colors.primary", da.Primary),
            Secondary: r.Color(colors, "secondary", "appearance.colors.secondary", da.Secondary),
            TextColor: r.Color(text, "color", "appearance.text.color", da.TextColor),
            BorderEnabled: r.Bool(border, "enabled", "appearance.border.enabled", da.BorderEnabled),
            BorderWidth: r.Num(border, "width", "appearance.border.width", da.BorderWidth, Limits.BorderWidth),
            BorderColor: r.Color(border, "color", "appearance.border.color", da.BorderColor),
            BackgroundEnabled: r.Bool(bg, "enabled", "appearance.group.background.enabled", da.BackgroundEnabled),
            BackgroundColor: r.Color(bg, "color", "appearance.group.background.color", da.BackgroundColor),
            Animation: r.Enum(anim, "type", "appearance.animation.type", da.Animation, ("fade", AnimationKind.Fade), ("none", AnimationKind.None)),
            DurationMs: r.Int(anim, "durationMs", "appearance.animation.durationMs", da.DurationMs, Limits.DurationMs),
            LabelSet: r.Enum(text, "labelSet", "appearance.text.labelSet", LabelSet.Windows, ("windows", LabelSet.Windows), ("macos", LabelSet.MacOS), ("linux", LabelSet.Linux)),
            LabelMode: mode,
            SideMarkers: r.Bool(layout, "sideMarkers", "appearance.layout.sideMarkers", da.SideMarkers),
            ShowPressCount: r.Bool(layout, "showPressCount", "appearance.layout.showPressCount", da.ShowPressCount),
            PressAnimation: r.Bool(anim, "press", "appearance.animation.press", da.PressAnimation),
            Mouse: ReadMouse(r, mouse, version, "appearance.mouse"),
            Indicator: ReadIndicator(r, lind, version, "appearance.languageIndicator"),
            Renderer: renderer,
            UseGradient: r.Bool(colors, "useGradient", "appearance.colors.useGradient", da.UseGradient),
            VolEnterShape: r.Bool(keycap, "volEnterShape", "appearance.keycap.volEnterShape", da.VolEnterShape),
            KeycapStyleId: keycapStyleId,
            TwoLabels: version >= 9 ? DefaultAsNull(ReadTwoLabels(r, r.Obj(keycap, "twoLabels", "appearance.keycap."), "appearance.keycap.twoLabels", TwoLabelLayout.Default)) : null,
            Backdrop: r.Enum(bg, "material", "appearance.group.background.material", BackdropMaterial.Fill, Backdrops),
            Glow: r.Num(border, "glow", "appearance.border.glow", 0, Limits.Glow));
    }

    /// <summary>Мышь в форме раздела <c>appearance.mouse</c> (также файлы стилей мыши).</summary>
    internal static MouseVisual ReadMouse(Reader r, JsonObject? mouse, int version, string path)
    {
        var dm = MouseVisual.Default;
        return new MouseVisual(
            r.Color(mouse, "clickColor", path + ".clickColor", dm.ClickColor),
            r.Num(mouse, "clickSize", path + ".clickSize", dm.ClickSize, Limits.ClickSize),
            r.Num(mouse, "indicatorSize", path + ".indicatorSize", dm.IconSize, Limits.IconSize),
            r.Color(mouse, "indicatorBackground", path + ".indicatorBackground", dm.IconBackground),
            r.Color(mouse, "indicatorForeground", path + ".indicatorForeground", dm.IconForeground),
            r.Color(mouse, "indicatorAccent", path + ".indicatorAccent", dm.IconAccent),
            Renderer: r.Enum(mouse, "renderer", path + ".renderer", MouseRenderer.Outline, MouseRenderers),
            BodyColor: r.OptColor(mouse, "body", path + ".body"),
            WheelArrowColor: r.OptColor(mouse, "wheelArrow", path + ".wheelArrow"),
            WheelPulseMs: r.Int(mouse, "wheelPulseMs", path + ".wheelPulseMs", dm.WheelPulseMs, Limits.WheelPulseMs),
            StyleId: version >= 3 ? r.Id(mouse, "styleId", path + ".styleId") : null,
            OutlineWidth: r.Num(mouse, "outlineWidth", path + ".outlineWidth", dm.OutlineWidth, Limits.MouseOutline),
            SolidOutline: r.OptColor(mouse, "solidOutline", path + ".solidOutline"),
            Plate: r.Enum(mouse, "indicatorMaterial", path + ".indicatorMaterial", BackdropMaterial.Fill, Backdrops),
            Glow: r.Num(mouse, "glow", path + ".glow", 0, Limits.Glow));
    }

    internal static IndicatorVisual ReadIndicator(Reader r, JsonObject? lind, int version, string path)
    {
        var di = IndicatorVisual.Default;
        return new IndicatorVisual(
            r.Color(lind, "background", path + ".background", di.Background),
            r.Color(lind, "foreground", path + ".foreground", di.Foreground),
            r.Color(lind, "border", path + ".border", di.Border),
            r.Num(lind, "borderWidth", path + ".borderWidth", di.BorderWidth, Limits.IndicatorBorderWidth),
            r.Num(lind, "radius", path + ".radius", di.Radius, Limits.Radius),
            r.Num(lind, "textSize", path + ".textSize", di.TextSize, Limits.IndicatorTextSize),
            version >= 3 ? r.Id(lind, "styleId", path + ".styleId") : null);
    }

    /// <summary>Визуальные параметры кейкапа из разделов темы (файлы стилей): остальные поля берутся из <paramref name="basis"/>.</summary>
    internal static AppearanceSettings ReadKeycapLook(Reader r, JsonObject? ap, AppearanceSettings basis, string path)
    {
        var keycap = r.Obj(ap, "keycap", path + ".");
        var colors = r.Obj(ap, "colors", path + ".");
        var text = r.Obj(ap, "text", path + ".");
        var border = r.Obj(ap, "border", path + ".");
        var group = r.Obj(ap, "group", path + ".");
        var bg = r.Obj(group, "background", path + ".group.");
        return basis with
        {
            Renderer = r.Enum(keycap, "style", path + ".keycap.style", basis.Renderer, Renderers),
            VolEnterShape = r.Bool(keycap, "volEnterShape", path + ".keycap.volEnterShape", basis.VolEnterShape),
            // Стиль без twoLabels (созданный до v9) не меняет текущую геометрию подписей.
            TwoLabels = keycap?["twoLabels"] is JsonObject tl ? DefaultAsNull(ReadTwoLabels(r, tl, path + ".keycap.twoLabels", TwoLabelLayout.Default)) : basis.TwoLabels,
            Primary = r.Color(colors, "primary", path + ".colors.primary", basis.Primary),
            Secondary = r.Color(colors, "secondary", path + ".colors.secondary", basis.Secondary),
            UseGradient = r.Bool(colors, "useGradient", path + ".colors.useGradient", basis.UseGradient),
            TextColor = r.Color(text, "color", path + ".text.color", basis.TextColor),
            BorderEnabled = r.Bool(border, "enabled", path + ".border.enabled", basis.BorderEnabled),
            BorderWidth = r.Num(border, "width", path + ".border.width", basis.BorderWidth, Limits.BorderWidth),
            BorderColor = r.Color(border, "color", path + ".border.color", basis.BorderColor),
            Radius = r.Num(border, "radius", path + ".border.radius", basis.Radius, Limits.Radius),
            BackgroundEnabled = r.Bool(bg, "enabled", path + ".group.background.enabled", basis.BackgroundEnabled),
            BackgroundColor = r.Color(bg, "color", path + ".group.background.color", basis.BackgroundColor),
            // Стиль без material (до Phase 4H) — заливка, как и раньше.
            Backdrop = r.Enum(bg, "material", path + ".group.background.material", BackdropMaterial.Fill, Backdrops),
            Glow = r.Num(border, "glow", path + ".border.glow", 0, Limits.Glow),
        };
    }

    private static string? Str(JsonObject? o, string name) =>
        o?[name] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static void Unknown(JsonObject o, List<LocText> problems, string prefix, params string[] known)
    {
        foreach (var (key, _) in o)
        {
            if (!known.Contains(key))
            {
                problems.Add(new LocText("Settings.Problem.UnknownField", prefix + key));
            }
        }
    }

    internal sealed class Reader(List<LocText> problems)
    {
        /// <summary>Необязательное число в диапазоне: нет поля — null; неверное значение — сообщение и null.</summary>
        public double? OptNum(JsonObject? o, string name, string path, (double Min, double Max) range) =>
            Has(o, name) ? Num(o, name, path, double.NaN, range) is var v && !double.IsNaN(v) ? v : null : null;

        /// <summary>Необязательный цвет: нет поля — null; неверное значение — сообщение и null.</summary>
        public ThemeColor? OptColor(JsonObject? o, string name, string path)
        {
            if (!Has(o, name))
            {
                return null;
            }

            string? s = o![name] is JsonValue v && v.TryGetValue(out string? t) ? t : null;
            if (ThemeColor.TryParse(s, out var c))
            {
                return c;
            }

            problems.Add(new LocText("Settings.Problem.Color", path, s ?? o[name]?.ToJsonString(), "—"));
            return null;
        }

        /// <summary>Необязательная строка не длиннее <paramref name="max"/>: нет поля — null; неверное значение — сообщение и null.</summary>
        public string? OptString(JsonObject? o, string name, string path, int max)
        {
            if (!Has(o, name))
            {
                return null;
            }

            if (o![name] is JsonValue v && v.TryGetValue(out string? t) && t.Length <= max)
            {
                return t;
            }

            problems.Add(new LocText("Settings.Problem.Value", path));
            return null;
        }

        /// <summary>Идентификатор стиля (та же форма, что у профилей); неверный — сообщение и «без стиля».</summary>
        public string? Id(JsonObject? o, string name, string path)
        {
            if (!Has(o, name))
            {
                return null;
            }

            string? s = o![name] is JsonValue v && v.TryGetValue(out string? t) ? t : null;
            if (Profiles.ProfileSerializer.IsValidId(s))
            {
                return s;
            }

            problems.Add(new LocText("Settings.Problem.StyleId", path, s ?? o[name]?.ToJsonString()));
            return null;
        }

        public JsonObject? Obj(JsonObject? parent, string name, string prefix = "")
        {
            if (parent is null || !parent.ContainsKey(name))
            {
                return null;
            }

            if (parent[name] is JsonObject o)
            {
                return o;
            }

            problems.Add(new LocText("Settings.Problem.NotSection", prefix + name));
            return null;
        }

        public double Num(JsonObject? o, string name, string path, double fallback, (double Min, double Max) range)
        {
            if (!Has(o, name))
            {
                return fallback;
            }

            if (o![name] is JsonValue v && v.TryGetValue(out double x) && double.IsFinite(x))
            {
                if (x >= range.Min && x <= range.Max)
                {
                    return x;
                }

                problems.Add(new LocText("Settings.Problem.Range", path, x, range.Min, range.Max, fallback));
                return fallback;
            }

            problems.Add(new LocText("Settings.Problem.Number", path, fallback));
            return fallback;
        }

        public int Int(JsonObject? o, string name, string path, int fallback, (int Min, int Max) range)
        {
            if (!Has(o, name))
            {
                return fallback;
            }

            if (o![name] is JsonValue v && v.TryGetValue(out double x) && double.IsFinite(x) && x == Math.Floor(x))
            {
                if (x >= range.Min && x <= range.Max)
                {
                    return (int)x;
                }

                problems.Add(new LocText("Settings.Problem.Range", path, x, range.Min, range.Max, fallback));
                return fallback;
            }

            problems.Add(new LocText("Settings.Problem.Integer", path, fallback));
            return fallback;
        }

        public bool Bool(JsonObject? o, string name, string path, bool fallback)
        {
            if (!Has(o, name))
            {
                return fallback;
            }

            if (o![name] is JsonValue v && v.TryGetValue(out bool b))
            {
                return b;
            }

            problems.Add(new LocText("Settings.Problem.Bool", path, fallback ? "true" : "false"));
            return fallback;
        }

        public ThemeColor Color(JsonObject? o, string name, string path, ThemeColor fallback)
        {
            if (!Has(o, name))
            {
                return fallback;
            }

            string? s = o![name] is JsonValue v && v.TryGetValue(out string? t) ? t : null;
            if (ThemeColor.TryParse(s, out var c))
            {
                return c;
            }

            problems.Add(new LocText("Settings.Problem.Color", path, s ?? o[name]?.ToJsonString(), fallback.ToString()));
            return fallback;
        }

        public T Enum<T>(JsonObject? o, string name, string path, T fallback, params (string Name, T Value)[] values)
        {
            if (!Has(o, name))
            {
                return fallback;
            }

            string? s = o![name] is JsonValue v && v.TryGetValue(out string? t) ? t : null;
            foreach (var (n, value) in values)
            {
                if (n == s)
                {
                    return value;
                }
            }

            problems.Add(new LocText("Settings.Problem.Enum", path, s ?? o[name]?.ToJsonString(), string.Join(", ", values.Select(x => x.Name))));
            return fallback;
        }

        private static bool Has(JsonObject? o, string name) => o is not null && o.ContainsKey(name);
    }
}
