using System.Text.Json.Nodes;
using KeyOverlay.Visual.Plugins;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Демонстрации на странице плагина (SDK 1.1, блок <c>preview</c> манифеста). Сцены рисует хост — те же, что у прежних
/// встроенных инструментов: <c>magnifier.follow</c> (увеличение у указателя), <c>magnifier.region</c> (линза и источник),
/// <c>dim</c> (затемнение вокруг области), <c>panel</c> (панель плагина). Параметры сцены берутся из текущих значений настроек
/// через привязку <c>bind</c> (параметр сцены → ключ настройки); код плагина для демонстрации не запускается.
/// </summary>
public static class PluginPreviews
{
    public static LivePreview? Create(PluginPreview p, Func<JsonObject> values, bool animate)
    {
        double N(JsonObject v, string param, double fallback) =>
            p.Bind.TryGetValue(param, out var key) && v[key] is JsonValue x && x.TryGetValue(out double d) && double.IsFinite(d) ? d : fallback;
        bool B(JsonObject v, string param, bool fallback) =>
            p.Bind.TryGetValue(param, out var key) && v[key] is JsonValue x && x.TryGetValue(out bool b) ? b : fallback;
        string S(JsonObject v, string param, string fallback) =>
            p.Bind.TryGetValue(param, out var key) && v[key] is JsonValue x && x.TryGetValue(out string? s) && s is not null ? s : fallback;

        return p.Scene switch
        {
            "magnifier.follow" => LivePreview.CursorZoom(() =>
            {
                var v = values();
                return new CursorZoomSettings(true, CursorZoomMode.Always, N(v, "zoom", 1.5), N(v, "size", 220),
                    S(v, "shape", "circle") == "circle" ? CursorZoomShape.Circle : CursorZoomShape.RoundedRect, 50,
                    N(v, "borderWidth", 2), S(v, "borderColor", "#31C99A"), B(v, "shadow", true));
            }, animate),
            "magnifier.region" => LivePreview.Focus(() =>
            {
                var v = values();
                return new FocusSettings(FocusMode.Lens, Zoom: N(v, "zoom", 1.5), Radius: N(v, "radius", 16), BorderWidth: N(v, "borderWidth", 2),
                    BorderColor: S(v, "borderColor", "#D4D4D4"), Shadow: B(v, "shadow", true), Glow: B(v, "glow", false), ShowSource: B(v, "showSource", true),
                    Preset: FocusPreset.Custom);
            }, animate),
            "dim" => LivePreview.Focus(() =>
            {
                var v = values();
                return new FocusSettings(FocusMode.Dim, DimOpacity: N(v, "opacity", 70) / 100, Radius: N(v, "radius", 16), BorderWidth: N(v, "borderWidth", 2),
                    BorderColor: S(v, "borderColor", "#D4D4D4"), Glow: B(v, "glow", false), EdgeSoftness: N(v, "softness", 0), Preset: FocusPreset.Custom);
            }, animate),
            "panel" => LivePreview.Panel(() =>
            {
                var v = values();
                double total = (N(v, "minutes", 5) * 60) + N(v, "seconds", 0);
                return (Math.Max(1, total), N(v, "fontSize", 40), N(v, "opacity", 90) / 100, S(v, "accent", "#31C99A"), B(v, "progress", true));
            }, animate),
            _ => null,
        };
    }
}
