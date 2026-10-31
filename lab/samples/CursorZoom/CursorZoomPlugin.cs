using Backpack.Sdk;

namespace CursorZoom;

/// <summary>
/// Увеличение у курсора: три независимые команды, у каждой своё сочетание.
/// <list type="bullet">
/// <item><c>hold</c> — увеличение, пока удерживается сочетание (команда удержания: нажатие открывает, отпускание закрывает);</item>
/// <item><c>follow</c> — включить или выключить увеличение, следующее за указателем;</item>
/// <item><c>pin</c> — закрепить увеличение там, где указатель был в момент нажатия (повторное нажатие убирает).</item>
/// </list>
/// Одновременно на экране не больше одного увеличения: <c>follow</c> и <c>pin</c> заменяют друг друга; <c>hold</c> при уже
/// открытом увеличении ничего не делает (и его отпускание ничего не закрывает). Esc закрывает увеличение (сообщает хост).
/// Изображение рисует Backpack (возможность overlay.magnifier): плагин не получает ни пикселей, ни координат указателя.
/// </summary>
public sealed class CursorZoomPlugin : BackpackPlugin
{
    private const string Tool = "zoom";
    private readonly Lock _lock = new();
    private string? _open; // hold, follow, pin или null

    public override void OnCommand(string commandId)
    {
        lock (_lock)
        {
            switch (commandId)
            {
                case "hold" when _open is null:
                    Open("hold", MagnifierMode.Follow);
                    break;
                case "follow":
                    if (_open == "follow")
                    {
                        Close();
                    }
                    else
                    {
                        Open("follow", MagnifierMode.Follow); // заменяет закреплённое
                    }

                    break;
                case "pin":
                    if (_open == "pin")
                    {
                        Close();
                    }
                    else
                    {
                        Open("pin", MagnifierMode.Pinned);
                    }

                    break;
            }
        }
    }

    public override void OnCommandReleased(string commandId)
    {
        lock (_lock)
        {
            if (commandId == "hold" && _open == "hold")
            {
                Close();
            }
        }
    }

    public override void OnToolClosed(string toolId, string reason)
    {
        lock (_lock)
        {
            _open = null; // Esc, блокировка, сон, выход
        }
    }

    public override void OnSettingsChanged()
    {
        lock (_lock)
        {
            if (_open is { } mode)
            {
                Context.OpenMagnifier(Tool, Request(mode == "pin" ? MagnifierMode.Pinned : MagnifierMode.Follow)); // обновить открытое
            }
        }
    }

    public override void OnStop()
    {
        lock (_lock)
        {
            _open = null; // хост сам закрывает инструменты отключаемого плагина
        }
    }

    private void Open(string mode, MagnifierMode kind)
    {
        if (_open is not null && (_open == "pin") != (kind == MagnifierMode.Pinned))
        {
            Context.CloseTool(Tool); // закреплённое и следующее — разные виды: закрыть прежнее
        }

        _open = mode;
        Context.OpenMagnifier(Tool, Request(kind));
    }

    private void Close()
    {
        _open = null;
        Context.CloseTool(Tool);
    }

    private MagnifierRequest Request(MagnifierMode mode)
    {
        var s = Context.Settings;
        double size = s.Number("size", 220);
        bool round = s.Text("shape", "circle") == "circle";
        return new MagnifierRequest(mode, s.Number("zoom", 1.5), size, size, round, round ? size / 2 : s.Number("radius", 18),
            s.Number("borderWidth", 2), s.Text("borderColor", "#31C99A"), s.Flag("shadow", true), Glow: false, ShowSource: false,
            SmoothingMs: (int)s.Number("smoothing", 50), HideMouseIcon: s.Text("icon", "keep-beside") == "auto-hide");
    }
}
