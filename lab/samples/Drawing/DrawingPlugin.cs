using System.Text.Json.Nodes;
using Backpack.Sdk;

namespace Drawing;

/// <summary>
/// Временное рисование поверх экрана. Холст, панель инструментов и радиальное меню рисует Backpack (overlay.draw): плагин
/// хранит параметры, открывает и закрывает холст и передаёт действия. Рисунки, указатель и снимки экрана плагину не передаются.
/// <list type="bullet">
/// <item><c>toggle</c> — открыть или закрыть холст; <c>radial</c> — радиальное меню (открывает холст при необходимости);</item>
/// <item>пока холст открыт: <c>screenshot</c> (F2), <c>undo</c>, <c>redo</c>, <c>clear</c>, <c>markersReset</c> и выбор инструмента
///       <c>tool-*</c> — их сочетания действуют только в это время (whileTool), вне рисования клавиши остаются другим программам.</item>
/// </list>
/// Последние инструмент, цвет, толщина, штрих и место панели запоминаются в настройках плагина (UpdateSettings).
/// </summary>
public sealed class DrawingPlugin : BackpackPlugin
{
    private const string Canvas = "canvas";
    private static readonly string[] Tools = ["select", "smartpen", "pen", "line", "arrow", "rectangle", "roundrect", "ellipse", "polygon", "text", "marker"];
    private readonly Lock _lock = new();
    private bool _open;

    public override void OnCommand(string commandId)
    {
        lock (_lock)
        {
            switch (commandId)
            {
                case "toggle" when _open:
                    _open = false;
                    Context.CloseTool(Canvas);
                    break;
                case "toggle":
                    Open();
                    break;
                case "radial":
                    if (!_open)
                    {
                        Open();
                    }

                    Context.DrawAction(Canvas, DrawActions.Radial);
                    break;
                case "screenshot" or "undo" or "redo" or "clear" when _open:
                    Context.DrawAction(Canvas, commandId);
                    break;
                case "markersReset" when _open:
                    Context.DrawAction(Canvas, DrawActions.MarkersReset);
                    break;
                default:
                    if (_open && commandId.StartsWith("tool-", StringComparison.Ordinal) && Tools.Contains(commandId[5..]))
                    {
                        Context.DrawAction(Canvas, DrawActions.Tool(commandId[5..]));
                    }

                    break;
            }
        }
    }

    public override void OnToolClosed(string toolId, string reason)
    {
        lock (_lock)
        {
            _open = false;
        }
    }

    public override void OnSettingsChanged()
    {
        lock (_lock)
        {
            if (_open)
            {
                Context.OpenDrawing(Canvas, Request()); // параметры применяются к открытому холсту, рисунки остаются
            }
        }
    }

    /// <summary>Пользователь сменил инструмент, цвет, толщину или переместил панель — запомнить как значения по умолчанию.</summary>
    public override void OnToolState(string toolId, IReadOnlyDictionary<string, JsonNode?> values)
    {
        var keep = new Dictionary<string, JsonNode?>();
        foreach (var (k, v) in values)
        {
            // Неперетащенная панель (null) хранится как -1: так значение остаётся числом схемы.
            keep[k] = k is "toolbarX" or "toolbarY" && v is null ? JsonValue.Create(-1.0) : v?.DeepClone();
        }

        Context.UpdateSettings(keep);
    }

    public override void OnStop()
    {
        lock (_lock)
        {
            if (_open)
            {
                Context.CloseTool(Canvas);
                _open = false;
            }
        }
    }

    private void Open()
    {
        _open = true;
        Context.OpenDrawing(Canvas, Request());
    }

    private DrawRequest Request()
    {
        var s = Context.Settings;
        static double? Pos(double v) => v < 0 ? null : v;
        return new DrawRequest(
            s.Text("tool", "smartpen"), s.Text("color", "#00FF9C"), s.Number("thickness", 4), s.Text("dash", "solid"), (int)s.Number("fadeSec", 0),
            s.Number("textSize", 28), s.Number("cornerRadius", 16), (int)s.Number("polygonSides", 6),
            s.Text("toolbar", "docked"), s.Text("autoHideEdge", "top"), s.Flag("compact", false), Pos(s.Number("toolbarX", -1)), Pos(s.Number("toolbarY", -1)),
            s.Text("shotAction", "save"), s.Text("afterShot", "keep"), (int)s.Number("markerStart", 1), s.Number("markerSize", 36),
            s.Flag("angleLabels", false));
    }
}
