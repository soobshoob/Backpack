using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Backpack.Sdk;

namespace Backpack.Visual.Plugins;

/// <summary>Проверенный запрос инструмента плагина: увеличение, затемнение, холст рисования или снимок (ровно одно).</summary>
public sealed record PluginToolRequest(string ToolId, MagnifierRequest? Magnifier, DimRequest? Dim, DrawRequest? Draw = null, SnapshotRequest? Snapshot = null);

/// <summary>Проверенная панель плагина (SDK 1.1): значения ограничены, кнопки — только из допустимых ID и иконок.</summary>
public sealed record PluginPanelSpec(string Title, string Text, double? Progress, double FontSize, double Opacity, string? Accent, string State, IReadOnlyList<PanelButton> Buttons, bool Movable)
{
    public static PluginPanelSpec Simple(string title, string text, double? progress) => new(title, text, progress, 30, 0.9, null, "normal", [], false);
}

/// <summary>
/// Движок инструментов, которые рисует хост по запросам плагинов (SDK 1.1, возможности overlay.magnifier и overlay.dim).
/// Каждый инструмент принадлежит плагину (<paramref name="pluginId"/>); отключение плагина закрывает все его инструменты.
/// Плагин не получает ни изображения экрана, ни координат: только событие закрытия с причиной.
/// </summary>
public interface IPluginToolHost
{
    /// <summary>Открыть или обновить инструмент. Повторный запрос с тем же ID меняет параметры открытого инструмента.</summary>
    void Open(string pluginId, PluginToolRequest request);

    void Close(string pluginId, string toolId);

    /// <summary>Показать управление инструментом без наведения (клавиатурный вызов редактирования).</summary>
    void Edit(string pluginId, string toolId);

    /// <summary>Закрыть всё, что открыл плагин (отключение, удаление, сбой). Событие <see cref="Closed"/> при этом не возникает.</summary>
    void CloseAll(string pluginId);

    bool IsOpen(string pluginId, string toolId);

    /// <summary>Действие на открытом инструменте (SDK 1.2: холст рисования); у чужого или закрытого инструмента ничего не делает.</summary>
    void Action(string pluginId, string toolId, string action);

    /// <summary>Инструмент закрыт не плагином: (плагин, инструмент, причина: escape, cancelled, system, host).</summary>
    event Action<string, string, string>? Closed;

    /// <summary>Пользователь изменил параметры инструмента на нём самом: (плагин, инструмент, значения в форме запроса).</summary>
    event Action<string, string, JsonObject>? ToolState;
}

/// <summary>
/// Холст рисования, который хост открывает по запросу плагина (SDK 1.2, overlay.draw). Одновременно открыт один холст;
/// запрос другого плагина, пока холст занят, отклоняется (событие закрытия с причиной busy).
/// </summary>
public interface IPluginDrawHost
{
    bool IsOpen(string pluginId, string toolId);

    /// <summary>Открыть или обновить параметры; false — холст занят другим плагином.</summary>
    bool Open(string pluginId, string toolId, DrawRequest request);

    void Close(string pluginId, string toolId);

    void Action(string pluginId, string toolId, string action);

    /// <summary>Холст закрыт не плагином (Esc, выход по снимку, система): (плагин, инструмент, причина).</summary>
    event Action<string, string, string>? Closed;

    /// <summary>Пользователь изменил параметры на панели или в радиальном меню: (плагин, инструмент, значения).</summary>
    event Action<string, string, JsonObject>? StateChanged;
}

/// <summary>Индикатор, который хост рисует по запросу плагина (SDK 1.2, overlay.indicator); null — скрыть индикатор плагина.</summary>
public interface IPluginIndicatorHost
{
    void Show(string pluginId, IndicatorRequest? request);
}

/// <summary>Разбор сообщений инструментов и панели на границе SDK: неизвестное — отказ, числа — в допустимых пределах.</summary>
public static partial class PluginToolParser
{
    public const int MaxToolsPerPlugin = 4;

    public static bool IsValidToolId(string? id) => id is not null && ToolIdRegex().IsMatch(id);

    /// <summary>Запрос tool.open; null — неверный ID или вид. <paramref name="kind"/> — magnifier или dim.</summary>
    public static PluginToolRequest? Parse(JsonObject msg, out string? kind)
    {
        kind = (string?)msg["kind"];
        string? id = (string?)msg["id"];
        if (!IsValidToolId(id))
        {
            return null;
        }

        if (kind == "magnifier")
        {
            var mode = (string?)msg["mode"] switch { "follow" => MagnifierMode.Follow, "pinned" => MagnifierMode.Pinned, "region" => MagnifierMode.Region, _ => (MagnifierMode?)null };
            if (mode is null)
            {
                return null;
            }

            var r = new MagnifierRequest(mode.Value,
                Num(msg, "zoom", 1.5, 1, 4), Num(msg, "width", 220, 60, 1600), Num(msg, "height", 220, 60, 1200),
                Bool(msg, "round", false), Num(msg, "radius", 16, 0, 64), Num(msg, "borderWidth", 2, 0, 12),
                Color(msg, "borderColor", "#31C99A"), Bool(msg, "shadow", true), Bool(msg, "glow", false), Bool(msg, "showSource", true),
                (int)Num(msg, "smoothingMs", 50, 0, 500), Bool(msg, "hideMouseIcon", false));
            return new PluginToolRequest(id!, r, null);
        }

        if (kind == "dim")
        {
            string monitors = (string?)msg["monitors"] is "primary" or "all" ? (string)msg["monitors"]! : "selection";
            var r = new DimRequest(Num(msg, "opacity", 0.7, 0, 0.95), Num(msg, "width", 360, 60, 1600), Num(msg, "height", 220, 60, 1200),
                Num(msg, "radius", 16, 0, 64), Num(msg, "borderWidth", 2, 0, 12), Color(msg, "borderColor", "#D4D4D4"), Bool(msg, "glow", false),
                Num(msg, "softness", 0, 0, 60), (int)Num(msg, "appearMs", 150, 0, 1000), monitors);
            return new PluginToolRequest(id!, null, r);
        }

        if (kind == "draw")
        {
            static string One(JsonObject o, string key, string fallback, params string[] allowed) =>
                (string?)o[key] is { } s && allowed.Contains(s) ? s : fallback;
            var r = new DrawRequest(
                One(msg, "tool", "smartpen", DrawToolNames),
                Color(msg, "color", "#00FF9C"),
                Num(msg, "thickness", 4, 1, 40),
                One(msg, "dash", "solid", "solid", "dashed", "dotted"),
                (int)Num(msg, "fadeSec", 0, 0, 120),
                Num(msg, "textSize", 28, 10, 96),
                Num(msg, "cornerRadius", 16, 0, 120),
                (int)Num(msg, "polygonSides", 6, 3, 12),
                One(msg, "toolbar", "docked", "docked", "autohide", "radial"),
                One(msg, "autoHideEdge", "top", "top", "bottom", "left", "right"),
                Bool(msg, "compact", false),
                msg["toolbarX"] is JsonValue ? Num(msg, "toolbarX", 0.5, 0, 1) : null,
                msg["toolbarY"] is JsonValue ? Num(msg, "toolbarY", 0, 0, 1) : null,
                One(msg, "shotAction", "save", "save", "copy", "both"),
                One(msg, "afterShot", "keep", "keep", "clear", "exit"),
                (int)Num(msg, "markerStart", 1, 0, 999),
                Num(msg, "markerSize", 36, 20, 96),
                Bool(msg, "angleLabels", false));
            return new PluginToolRequest(id!, null, null, r);
        }

        if (kind == "snapshot")
        {
            return new PluginToolRequest(id!, null, null, null, ParseSnapshot(msg));
        }

        return null;
    }

    /// <summary>Параметры снимка (SDK 1.3): строки — только из допустимых значений, числа и цвета — в пределах.</summary>
    public static SnapshotRequest ParseSnapshot(JsonObject msg)
    {
        static string One(JsonObject o, string key, string fallback, params string[] allowed) =>
            (string?)o[key] is { } s && allowed.Contains(s) ? s : fallback;
        return new SnapshotRequest(
            One(msg, "place", "last", "last", "corner", "capture"),
            One(msg, "corner", "topRight", "topRight", "topLeft", "bottomRight", "bottomLeft"),
            Num(msg, "startSize", 420, 80, 2400),
            Bool(msg, "selectionDim", true),
            One(msg, "aspect", "keep", "keep", "fit", "stretch"),
            Color(msg, "background", "#111513"),
            Num(msg, "minSize", 120, 48, 600),
            Num(msg, "maxSize", 0.9, 0.2, 1),
            Num(msg, "edgeMargin", 24, 0, 200),
            Bool(msg, "topmost", true),
            Num(msg, "opacity", 1, 0.2, 1),
            Bool(msg, "doubleClick", true),
            Bool(msg, "spaceHover", true),
            Bool(msg, "autoCollapse", true),
            Num(msg, "expandMargin", 48, 0, 400),
            Bool(msg, "upscale", true),
            Bool(msg, "clickThrough", false),
            Num(msg, "radius", 16, 0, 64),
            Num(msg, "borderWidth", 2, 0, 12),
            Color(msg, "borderColor", "#D4D4D4"),
            Num(msg, "borderOpacity", 1, 0, 1),
            Bool(msg, "glow", false),
            Bool(msg, "shadow", true),
            Color(msg, "shadowColor", "#000000"),
            Num(msg, "shadowBlur", 16, 0, 60),
            Num(msg, "shadowOpacity", 0.55, 0, 1),
            One(msg, "expandMonitor", "snapshot", "snapshot", "pointer", "primary"),
            Bool(msg, "keepDip", true),
            One(msg, "keep", "session", "session", "unlocked"));
    }

    /// <summary>Действие снимка (SDK 1.3): одно из <see cref="SnapshotActions.All"/>; null — неизвестное.</summary>
    public static string? ParseSnapshotAction(string? action) => action is not null && SnapshotActions.All.Contains(action) ? action : null;

    /// <summary>Имена инструментов рисования в протоколе (SDK 1.2).</summary>
    public static readonly string[] DrawToolNames = ["select", "smartpen", "pen", "line", "arrow", "rectangle", "roundrect", "ellipse", "polygon", "text", "marker"];

    /// <summary>Действие холста рисования: одно из <see cref="DrawActions"/> или выбор инструмента; null — неизвестное.</summary>
    public static string? ParseDrawAction(string? action) =>
        action is DrawActions.Undo or DrawActions.Redo or DrawActions.Clear or DrawActions.Screenshot or DrawActions.Radial or DrawActions.MarkersReset
            ? action
            : action is { } a && a.StartsWith("tool:", StringComparison.Ordinal) && DrawToolNames.Contains(a[5..]) ? a : null;

    /// <summary>Показ индикатора (SDK 1.2): пустой код — отказ, строки — из допустимых значений.</summary>
    public static IndicatorRequest? ParseIndicator(JsonObject msg)
    {
        string code = Cut((string?)msg["code"], 12).Trim();
        if (code.Length == 0)
        {
            return null;
        }

        string? detail = (string?)msg["detail"] is { Length: > 0 } d ? Cut(d, 80) : null;
        string anim = (string?)msg["animation"] is "fade" or "none" ? (string)msg["animation"]! : "inherit";
        string position = (string?)msg["position"] == "screen" ? "screen" : "overlay";
        string anchor = (string?)msg["anchor"] is { } a && Enum.TryParse<Settings.ScreenAnchor>(a, ignoreCase: true, out _) ? a : "center";
        string style = (string?)msg["style"] == "custom" ? "custom" : "keycaps";
        return new IndicatorRequest(code, detail, (int)Num(msg, "durationMs", 1200, 200, 10000), anim, position, anchor,
            Num(msg, "offsetX", 0, -2000, 2000), Num(msg, "offsetY", 0, -2000, 2000), style);
    }

    /// <summary>Панель (обычная SDK 1.0 или с оформлением и кнопками SDK 1.1).</summary>
    public static PluginPanelSpec ParsePanel(JsonObject msg, Func<string, bool> iconExists)
    {
        double? progress = msg["progress"] is JsonValue pv && pv.TryGetValue(out double d) && double.IsFinite(d) ? Math.Clamp(d, 0, 1) : null;
        var buttons = new List<PanelButton>();
        if (msg["buttons"] is JsonArray arr)
        {
            foreach (var b in arr.Take(6).OfType<JsonObject>())
            {
                string? bid = (string?)b["id"];
                if (IsValidToolId(bid))
                {
                    string icon = (string?)b["icon"] is { } i && iconExists(i) ? i : "circle";
                    buttons.Add(new PanelButton(bid!, icon, Cut((string?)b["title"], 60)));
                }
            }
        }

        string state = (string?)msg["state"] is "warning" or "finished" ? (string)msg["state"]! : "normal";
        string? accent = msg["accent"] is JsonValue av && av.TryGetValue(out string? a) && Settings.ThemeColor.TryParse(a, out _) ? a : null;
        return new PluginPanelSpec(Cut((string?)msg["title"], 80), Cut((string?)msg["text"], 200), progress,
            Num(msg, "fontSize", 30, 12, 160), Num(msg, "opacity", 0.9, 0.2, 1), accent, state, buttons, Bool(msg, "movable", false));
    }

    private static double Num(JsonObject o, string key, double fallback, double min, double max) =>
        o[key] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number
            && double.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) && double.IsFinite(d)
            ? Math.Clamp(d, min, max) : fallback;

    private static bool Bool(JsonObject o, string key, bool fallback) => o[key] is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;

    private static string Color(JsonObject o, string key, string fallback) =>
        o[key] is JsonValue v && v.TryGetValue(out string? s) && Settings.ThemeColor.TryParse(s, out var c) ? c.ToString() : fallback;

    private static string Cut(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];

    [GeneratedRegex("^[a-z][A-Za-z0-9-]{0,31}$")]
    private static partial Regex ToolIdRegex();
}
