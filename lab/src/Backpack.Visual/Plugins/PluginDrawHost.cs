using System.Text.Json.Nodes;
using Backpack.Sdk;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Plugins;

/// <summary>
/// Холст рисования хоста для плагинов (SDK 1.2, overlay.draw): прежний механизм временного рисования
/// (<see cref="PresentationController"/> и <see cref="DrawingWindow"/> — панель и её режимы, радиальное меню, фигуры, умный
/// карандаш, текст, номера, цвета, отмена и повтор, снимки) теперь открывается только по запросу плагина и берёт параметры
/// из его <see cref="DrawRequest"/>. Изменения на самой панели (инструмент, цвет, положение панели…) уходят плагину
/// событием <see cref="StateChanged"/>; рисунки и изображение экрана плагину не передаются. Один холст на всех плагинов.
/// </summary>
public sealed class PluginDrawHost : IPluginDrawHost
{
    private PresentationController? _p;
    private (string Plugin, string Tool, DrawRequest Request)? _owner;
    private bool _closing;

    public event Action<string, string, string>? Closed;

    public event Action<string, string, JsonObject>? StateChanged;

    /// <summary>Владелец открытого холста (для проверок и сочетаний whileTool).</summary>
    public (string Plugin, string Tool)? Owner => _owner is { } o ? (o.Plugin, o.Tool) : null;

    public PresentationController? Controller => _p;

    /// <summary>Параметры рисования текущего запроса (для <see cref="PresentationController"/>).</summary>
    public DrawSettings Settings => ToSettings(_owner?.Request ?? new DrawRequest());

    /// <summary>Поведение панели и снимка текущего запроса.</summary>
    public DrawingProfile Profile => ToProfile(_owner?.Request ?? new DrawRequest());

    /// <summary>Подключить механизм рисования (создаётся приложением с функциями <see cref="Settings"/> и <see cref="Profile"/>).</summary>
    public void Attach(PresentationController p)
    {
        _p = p;
        p.StateChanged += OnState;
        p.SaveDraw = s => Notify(FromSettings(s));
    }

    /// <summary>Положение или вид панели изменены пользователем (перетаскивание, компактный вид).</summary>
    public void SaveProfile(DrawingProfile profile) => Notify(new JsonObject
    {
        ["toolbarX"] = profile.ToolbarX, ["toolbarY"] = profile.ToolbarY, ["compact"] = profile.Compact,
    });

    public bool IsOpen(string pluginId, string toolId) => _owner is { } o && o.Plugin == pluginId && o.Tool == toolId && _p?.State.Drawing == true;

    public bool Open(string pluginId, string toolId, DrawRequest request)
    {
        if (_p is null || (_owner is { } o && (o.Plugin != pluginId || o.Tool != toolId) && _p.State.Drawing))
        {
            return false;
        }

        _owner = (pluginId, toolId, request);
        if (!_p.State.Drawing)
        {
            _p.Execute(PresentationCommand.Draw);
        }
        else if (_p.Drawing is { } d)
        {
            // Обновление параметров открытого холста (например, изменены настройки плагина): рисунки остаются.
            d.SetSettings(ToSettings(request));
            _p.ApplyDrawingProfile(ToProfile(request));
        }

        if (_p.Drawing is { } w)
        {
            w.MarkerSize = request.MarkerSize;
            w.SetMarkerStart(request.MarkerStart);
        }

        return _p.State.Drawing;
    }

    public void Close(string pluginId, string toolId)
    {
        if (_owner is not { } o || o.Plugin != pluginId || o.Tool != toolId)
        {
            return;
        }

        _closing = true;
        try
        {
            if (_p?.State.Drawing == true)
            {
                _p.Execute(PresentationCommand.Draw);
            }
        }
        finally
        {
            _closing = false;
            _owner = null;
        }
    }

    public void Action(string pluginId, string toolId, string action)
    {
        if (!IsOpen(pluginId, toolId) || _p?.Drawing is not { } d)
        {
            return;
        }

        switch (action)
        {
            case DrawActions.Undo:
                d.Undo();
                break;
            case DrawActions.Redo:
                d.Redo();
                break;
            case DrawActions.Clear:
                d.Clear();
                break;
            case DrawActions.Screenshot:
                _ = _p.TakeScreenshotAsync();
                break;
            case DrawActions.Radial:
                _p.Execute(PresentationCommand.Radial);
                break;
            case DrawActions.MarkersReset:
                d.ResetMarkers(_owner!.Value.Request.MarkerStart);
                break;
            default:
                if (action.StartsWith("tool:", StringComparison.Ordinal) && ToolOf(action[5..]) is { } tool)
                {
                    d.SetSettings(d.Settings with { Tool = tool });
                }

                break;
        }
    }

    private void OnState(PresentationState s)
    {
        if (!s.Drawing && _owner is { } o && !_closing)
        {
            // Холст закрыт не плагином: Esc, «выйти после снимка», система.
            _owner = null;
            Closed?.Invoke(o.Plugin, o.Tool, "escape");
        }
    }

    private void Notify(JsonObject values)
    {
        if (_owner is { } o)
        {
            StateChanged?.Invoke(o.Plugin, o.Tool, values);
        }
    }

    // ── преобразование запроса SDK ↔ параметры механизма рисования ───────────

    private static readonly (string Name, DrawTool Tool)[] Tools =
    [
        ("select", DrawTool.Select), ("smartpen", DrawTool.SmartPen), ("pen", DrawTool.Pen), ("line", DrawTool.Line), ("arrow", DrawTool.Arrow),
        ("rectangle", DrawTool.Rectangle), ("roundrect", DrawTool.RoundedRectangle), ("ellipse", DrawTool.Ellipse), ("polygon", DrawTool.Polygon),
        ("text", DrawTool.Text), ("marker", DrawTool.Marker),
    ];

    public static DrawTool? ToolOf(string name) => Tools.FirstOrDefault(x => x.Name == name) is { Name: not null } t ? t.Tool : null;

    public static string NameOf(DrawTool tool) => Tools.First(x => x.Tool == tool).Name;

    public static DrawSettings ToSettings(DrawRequest r) => new DrawSettings(
        ToolOf(r.Tool) ?? DrawTool.SmartPen,
        ThemeColor.TryParse(r.Color, out var c) ? c : DrawSettings.Default.Color,
        r.Thickness,
        r.Dash switch { "dashed" => DrawDash.Dashed, "dotted" => DrawDash.Dotted, _ => DrawDash.Solid },
        r.FadeSec,
        r.TextSize,
        r.CornerRadius,
        r.PolygonSides,
        r.AngleLabels).Sanitized();

    public static DrawingProfile ToProfile(DrawRequest r) => new(
        r.Toolbar switch { "autohide" => DrawToolbarMode.AutoHide, "radial" => DrawToolbarMode.RadialOnly, _ => DrawToolbarMode.Docked },
        r.AutoHideEdge switch { "bottom" => ScreenEdge.Bottom, "left" => ScreenEdge.Left, "right" => ScreenEdge.Right, _ => ScreenEdge.Top },
        r.ToolbarX,
        r.ToolbarY,
        r.ShotAction switch { "copy" => ScreenshotAction.Copy, "both" => ScreenshotAction.SaveAndCopy, _ => ScreenshotAction.Save },
        r.AfterShot switch { "clear" => AfterScreenshot.ClearDrawings, "exit" => AfterScreenshot.ExitDrawing, _ => AfterScreenshot.KeepDrawings },
        r.Compact);

    /// <summary>Значения панели → ключи запроса (camelCase), которые плагин может сохранить в своих настройках.</summary>
    public static JsonObject FromSettings(DrawSettings s) => new()
    {
        ["tool"] = NameOf(s.Tool), ["color"] = s.Color.ToString(), ["thickness"] = s.Thickness,
        ["dash"] = s.Dash switch { DrawDash.Dashed => "dashed", DrawDash.Dotted => "dotted", _ => "solid" },
        ["fadeSec"] = s.FadeSec, ["textSize"] = s.TextSize, ["cornerRadius"] = s.CornerRadius, ["polygonSides"] = s.PolygonSides,
        ["angleLabels"] = s.AngleLabels,
    };
}
