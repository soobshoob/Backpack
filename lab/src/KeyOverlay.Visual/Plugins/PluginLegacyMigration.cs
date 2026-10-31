using System.Text.Json.Nodes;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Plugins;

/// <summary>
/// Перенос прежних встроенных инструментов в поставляемые плагины (Phase 4G: лупа, линза, фокус; Phase 4H: рисование и
/// индикатор раскладки). Прежние разделы предпочтений и профиля собираются в форму <c>legacy</c> (по разделу на инструмент),
/// правила переноса — в manifest плагина (<see cref="PluginMigration"/>). Плагин не включается и доверие не подтверждается.
/// </summary>
public static class PluginLegacyMigration
{
    /// <summary>Прежние разделы: значения по умолчанию заполнены; ключи рисования и индикатора — уже в форме запроса SDK 1.2.</summary>
    public static JsonObject Legacy(AppPreferences p, LabSettings s)
    {
        var draw = PluginDrawHost.FromSettings(p.DrawOrDefault);
        var d = s.DrawingOrDefault;
        var m = p.MarkersOrDefault;
        draw["toolbar"] = d.ToolbarMode switch { DrawToolbarMode.AutoHide => "autohide", DrawToolbarMode.RadialOnly => "radial", _ => "docked" };
        draw["autoHideEdge"] = d.AutoHideEdge.ToString().ToLowerInvariant();
        draw["compact"] = d.Compact;
        draw["toolbarX"] = d.ToolbarX ?? -1;
        draw["toolbarY"] = d.ToolbarY ?? -1;
        draw["shotAction"] = d.ShotAction switch { ScreenshotAction.Copy => "copy", ScreenshotAction.SaveAndCopy => "both", _ => "save" };
        draw["afterShot"] = d.AfterShot switch { AfterScreenshot.ClearDrawings => "clear", AfterScreenshot.ExitDrawing => "exit", _ => "keep" };
        draw["markerStart"] = m.StartNumber;
        draw["markerSize"] = m.Size;

        var li = s.LayoutIndicator;
        string anchor = li.Anchor.ToString();
        var indicator = new JsonObject
        {
            ["enabled"] = li.Enabled,
            ["durationMs"] = li.DurationMs,
            ["showOnWindowSwitch"] = li.ShowOnWindowSwitch,
            ["style"] = li.CustomStyle ? "custom" : "keycaps",
            ["animation"] = li.Animation switch { IndicatorAnimation.Fade => "fade", IndicatorAnimation.None => "none", _ => "inherit" },
            ["position"] = li.Position == IndicatorPosition.Screen ? "screen" : "overlay",
            ["anchor"] = char.ToLowerInvariant(anchor[0]) + anchor[1..],
            ["offsetX"] = li.OffsetX,
            ["offsetY"] = li.OffsetY,
        };

        return new JsonObject
        {
            ["focus"] = PreferenceSections.ToNode(p.FocusOrDefault),
            ["cursorZoom"] = PreferenceSections.ToNode(p.CursorZoomOrDefault),
            ["tools"] = PreferenceSections.ToNode(p.ToolsOrDefault),
            ["drawing"] = draw,
            ["layoutIndicator"] = indicator,
        };
    }

    /// <summary>Перенести настройки, назначения и «избранное» для плагина <paramref name="m"/>. false — переносить нечего.</summary>
    public static bool Apply(PluginManager pm, AppPreferencesStore prefs, SettingsStore store, PluginManifest m)
    {
        var p = prefs.Current;
        var binds = p.BindingsOrEmpty;
        string? HotkeyOf(string old) => binds[old] is { } v ? v : old == "tool.focus" ? Hotkeys.HotkeySettings.Default.Lens?.ToString() : null;
        var existing = store.Current.PluginsOrEmpty.Get(m.Id);
        var plan = PluginMigration.Plan(m, Legacy(p, store.Current), HotkeyOf, existing, id => binds[id] is not null);
        if (plan.IsEmpty)
        {
            return false;
        }

        if (plan.Settings is { } values)
        {
            var merged = pm.Store.LoadSettings(m);
            foreach (var (k, v) in values)
            {
                merged[k] = v?.DeepClone();
            }

            pm.SettingsChanged(m.Id, merged);
        }

        if (plan.Bindings.Count > 0 || plan.Removed.Count > 0)
        {
            prefs.Update(x =>
            {
                var b = x.BindingsOrEmpty;
                foreach (var old in plan.Removed)
                {
                    b = b.With(old, null);
                }

                foreach (var (id, hk) in plan.Bindings)
                {
                    b = b.With(id, hk);
                }

                return x with { Bindings = b };
            });
        }

        if (plan.Favorite)
        {
            pm.Store.SetFavorite(m.Id, true);
        }

        return true;
    }
}
