using Backpack.Sdk;

namespace Lens;

/// <summary>
/// Линза: пользователь выделяет область-источник, Backpack показывает её живое увеличение в отдельной перемещаемой линзе.
/// <list type="bullet">
/// <item><c>start</c> — открыть линзу (сначала выделение области); повторное нажатие закрывает;</item>
/// <item><c>edit</c> — показать ручки линзы и источника без наведения (клавиатурный вызов).</item>
/// </list>
/// Ручки появляются при наведении на линзу; Esc закрывает. Изображение рисует Backpack (overlay.magnifier, вид region):
/// плагин не получает ни пикселей, ни координат.
/// </summary>
public sealed class LensPlugin : BackpackPlugin
{
    private const string Tool = "lens";
    private readonly Lock _lock = new();
    private bool _open;

    public override void OnCommand(string commandId)
    {
        lock (_lock)
        {
            switch (commandId)
            {
                case "start" when _open:
                    _open = false;
                    Context.CloseTool(Tool);
                    break;
                case "start":
                    _open = true;
                    Context.OpenMagnifier(Tool, Request());
                    break;
                case "edit" when _open:
                    Context.EditTool(Tool);
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
                Context.OpenMagnifier(Tool, Request());
            }
        }
    }

    private MagnifierRequest Request()
    {
        var s = Context.Settings;
        return new MagnifierRequest(MagnifierMode.Region, s.Number("zoom", 1.5), s.Number("width", 360), s.Number("height", 220), Round: false,
            s.Number("radius", 16), s.Number("borderWidth", 2), s.Text("borderColor", ToolDefaults.BorderColor), s.Flag("shadow", true), s.Flag("glow", false),
            s.Flag("showSource", true));
    }
}
