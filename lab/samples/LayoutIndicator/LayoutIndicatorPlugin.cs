using System.Text.Json.Nodes;
using Backpack.Sdk;

namespace LayoutIndicator;

/// <summary>
/// Индикатор смены раскладки: Backpack сообщает смену раскладки активного окна (input.layout) и рисует индикатор
/// (overlay.indicator) — оформление кейкапов или стиль индикатора из профиля, мониторы по выбору пользователя.
/// Плагин решает, показывать ли: смена окна с другой раскладкой — по настройке; запуск, разблокировка и выход из сна
/// событием не считаются (это обеспечивает Backpack). Нажатия клавиш плагин не получает.
/// <list type="bullet">
/// <item><c>toggle</c> — приостановить или возобновить показ (состояние сохраняется);</item>
/// <item><c>example</c> — показать пример, чтобы проверить оформление и место.</item>
/// </list>
/// </summary>
public sealed class LayoutIndicatorPlugin : BackpackPlugin
{
    public override void OnLayoutChanged(LayoutEvent e)
    {
        var s = Context.Settings;
        if (s.Flag("paused", false) || (e.Reason == "window" && !s.Flag("showOnWindowSwitch", true)))
        {
            return;
        }

        Context.ShowIndicator(Request(e.Code, e.Detail));
    }

    public override void OnCommand(string commandId)
    {
        switch (commandId)
        {
            case "toggle":
                bool paused = !Context.Settings.Flag("paused", false);
                Context.UpdateSettings(new Dictionary<string, JsonNode?> { ["paused"] = paused });
                if (paused)
                {
                    Context.HideIndicator();
                }

                break;
            case "example":
                Context.ShowIndicator(Request(Example(), null));
                break;
        }
    }

    public override void OnSettingsChanged()
    {
        if (Context.Settings.Flag("paused", false))
        {
            Context.HideIndicator();
        }
    }

    // Пример — код языка интерфейса (как короткое имя раскладки): DE, EL, UK…; неизвестный — EN.
    private string Example() => Context.Locale is { Length: 2 } loc && loc.All(char.IsAsciiLetterLower) ? loc.ToUpperInvariant() : "EN";

    private IndicatorRequest Request(string code, string? detail)
    {
        var s = Context.Settings;
        return new IndicatorRequest(code, s.Flag("showName", true) ? detail : null, (int)s.Number("durationMs", 1200), s.Text("animation", "inherit"),
            s.Text("position", "overlay"), s.Text("anchor", "center"), s.Number("offsetX", 0), s.Number("offsetY", 0), s.Text("style", "keycaps"));
    }
}
