using Backpack.Sdk;

namespace Focus;

/// <summary>
/// Фокус: затемнение экрана вокруг выбранной области.
/// <list type="bullet">
/// <item><c>start</c> — выделить область и затемнить всё вокруг; повторное нажатие закрывает;</item>
/// <item><c>edit</c> — показать ручки области без наведения (клавиатурный вызов).</item>
/// </list>
/// Ручки появляются при наведении на область; затемнение пропускает щелчки к приложениям; Esc закрывает. Затемнение рисует
/// Backpack (overlay.dim): плагин не получает ни изображения, ни координат.
/// </summary>
public sealed class FocusPlugin : BackpackPlugin
{
    private const string Tool = "focus";
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
                    Context.OpenDim(Tool, Request());
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
                Context.OpenDim(Tool, Request());
            }
        }
    }

    private DimRequest Request()
    {
        var s = Context.Settings;
        string monitors = s.Text("monitors", "selected") switch { "primary" => "primary", "multiple" => "all", _ => "selection" };
        return new DimRequest(s.Number("opacity", 70) / 100, s.Number("width", 360), s.Number("height", 220), s.Number("radius", 16),
            s.Number("borderWidth", 2), s.Text("borderColor", ToolDefaults.BorderColor), s.Flag("glow", false), s.Number("softness", 0), (int)s.Number("appearMs", 150), monitors);
    }
}
