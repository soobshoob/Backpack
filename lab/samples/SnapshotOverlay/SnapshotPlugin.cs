using System.Text.Json.Nodes;
using Backpack.Sdk;

namespace SnapshotOverlay;

/// <summary>
/// Snapshot Overlay: статичный снимок выбранной области экрана в плавающем окне поверх остальных (например, чертёж рядом с окном
/// CAD на одном мониторе).
/// <list type="bullet">
/// <item><c>capture</c> — выделить область и показать снимок; при открытом снимке — заменить его новым;</item>
/// <item><c>toggleVisible</c> — скрыть / показать (снимок остаётся в памяти);</item>
/// <item><c>expand</c> — временно развернуть почти на весь экран / свернуть точно на прежнее место;</item>
/// <item><c>clear</c> — удалить снимок; <c>edit</c> — показать ручки и кнопки без наведения; <c>reset</c> — настройки и место по умолчанию.</item>
/// </list>
/// Выделение, захват, окно, ручки, двойной щелчок, Space над снимком и Esc обрабатывает Backpack (overlay.snapshot): плагин не
/// получает ни изображения, ни координат и ничего не записывает на диск.
/// </summary>
public sealed class SnapshotPlugin : BackpackPlugin
{
    /// <summary>ID инструмента. Один снимок; несколько независимых снимков — несколько ID (хост держит до четырёх на плагин).</summary>
    public const string Tool = "snapshot";

    private readonly Lock _lock = new();
    private bool _open;

    /// <summary>Значения настроек по умолчанию — те же, что в manifest.json (проверяется тестом); нужны команде «Сбросить».</summary>
    public static IReadOnlyDictionary<string, JsonNode?> Defaults { get; } = new Dictionary<string, JsonNode?>
    {
        ["place"] = "last", ["corner"] = "topRight", ["startSize"] = 420.0, ["selectionDim"] = true,
        ["aspect"] = "keep", ["background"] = "#111513", ["minSize"] = 120.0, ["maxSize"] = 90.0, ["edgeMargin"] = 24.0, ["topmost"] = true,
        ["doubleClick"] = true, ["spaceHover"] = true, ["autoCollapse"] = true, ["expandMargin"] = 48.0, ["upscale"] = true, ["clickThrough"] = false,
        ["radius"] = 16.0, ["borderWidth"] = 2.0, ["borderColor"] = "#D4D4D4", ["borderOpacity"] = 100.0, ["glow"] = false, ["opacity"] = 100.0,
        ["shadow"] = true, ["shadowColor"] = "#000000", ["shadowBlur"] = 16.0, ["shadowOpacity"] = 55.0,
        ["expandMonitor"] = "snapshot", ["keepDip"] = true, ["keep"] = "session",
    };

    /// <summary>Открыт ли снимок (для проверок).</summary>
    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _open;
            }
        }
    }

    public override void OnCommand(string commandId)
    {
        lock (_lock)
        {
            switch (commandId)
            {
                case "capture" when _open:
                    Context.SnapshotAction(Tool, SnapshotActions.Capture);
                    break;
                case "capture":
                    _open = true;
                    Context.OpenSnapshot(Tool, Request());
                    break;
                case "toggleVisible" when _open:
                    Context.SnapshotAction(Tool, SnapshotActions.ToggleVisible);
                    break;
                case "expand" when _open:
                    Context.SnapshotAction(Tool, SnapshotActions.ToggleExpand);
                    break;
                case "clear" when _open:
                    _open = false;
                    Context.CloseTool(Tool);
                    break;
                case "edit" when _open:
                    Context.EditTool(Tool);
                    break;
                case "reset":
                    Context.UpdateSettings(Defaults);
                    if (_open)
                    {
                        Context.OpenSnapshot(Tool, Request());
                    }

                    Context.SnapshotAction(Tool, SnapshotActions.ResetPlace);
                    break;
            }
        }
    }

    public override void OnToolClosed(string toolId, string reason)
    {
        lock (_lock)
        {
            if (toolId == Tool)
            {
                _open = false;
            }
        }
    }

    public override void OnSettingsChanged()
    {
        lock (_lock)
        {
            if (_open)
            {
                Context.OpenSnapshot(Tool, Request()); // тот же ID: только новые параметры, без нового захвата
            }
        }
    }

    /// <summary>Настройки страницы плагина → запрос снимка (проценты — в доли).</summary>
    public SnapshotRequest Request()
    {
        var s = Context.Settings;
        return new SnapshotRequest(
            Place: s.Text("place", "last"),
            Corner: s.Text("corner", "topRight"),
            StartSize: s.Number("startSize", 420),
            SelectionDim: s.Flag("selectionDim", true),
            Aspect: s.Text("aspect", "keep"),
            Background: s.Text("background", "#111513"),
            MinSize: s.Number("minSize", 120),
            MaxSize: s.Number("maxSize", 90) / 100,
            EdgeMargin: s.Number("edgeMargin", 24),
            Topmost: s.Flag("topmost", true),
            Opacity: s.Number("opacity", 100) / 100,
            DoubleClick: s.Flag("doubleClick", true),
            SpaceHover: s.Flag("spaceHover", true),
            AutoCollapse: s.Flag("autoCollapse", true),
            ExpandMargin: s.Number("expandMargin", 48),
            Upscale: s.Flag("upscale", true),
            ClickThrough: s.Flag("clickThrough", false),
            Radius: s.Number("radius", 16),
            BorderWidth: s.Number("borderWidth", 2),
            BorderColor: s.Text("borderColor", "#D4D4D4"),
            BorderOpacity: s.Number("borderOpacity", 100) / 100,
            Glow: s.Flag("glow", false),
            Shadow: s.Flag("shadow", true),
            ShadowColor: s.Text("shadowColor", "#000000"),
            ShadowBlur: s.Number("shadowBlur", 16),
            ShadowOpacity: s.Number("shadowOpacity", 55) / 100,
            ExpandMonitor: s.Text("expandMonitor", "snapshot"),
            KeepDip: s.Flag("keepDip", true),
            Keep: s.Text("keep", "session"));
    }
}
