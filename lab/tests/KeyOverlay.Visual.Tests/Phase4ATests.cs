using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using KeyOverlay.Visual.Icons;
using KeyOverlay.Visual.Layout;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4A: мониторы (вымышленные конфигурации — не проверка физических мониторов), формат settings v5, темы, иконки,
/// миграция app.json. Приватность на уровне логики — в Core (PrivacyGateTests).
/// </summary>
public class Phase4ATests
{
    // Слева — монитор 125 % с отрицательными координатами, основной 100 %, справа — 200 % выше основного.
    private static readonly MonitorDesc Left = new("mon-left", "Left", new PixelRect(-1920, 0, 1920, 1080), new PixelRect(-1920, 0, 1920, 1040), 1.25, false, 1);
    private static readonly MonitorDesc Main = new("mon-main", "Main", new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1392), 1.0, true, 2);
    private static readonly MonitorDesc Right = new("mon-right", "Right", new PixelRect(2560, -400, 3840, 2160), new PixelRect(2560, -400, 3840, 2100), 2.0, false, 3);
    private static readonly MonitorDesc[] Three = [Left, Main, Right];

    private static MonitorTarget Target(MonitorMode mode, params MonitorDesc[] ms) => new(mode, ms.Select(m => new MonitorRef(m.Id, m.Name)).ToList());

    [Fact]
    public void Primary_mode_uses_the_primary_monitor_even_if_it_is_not_first()
    {
        var r = MonitorResolver.Resolve(MonitorTarget.Primary, Three, (0, 0));
        Assert.Equal([Main], r.Monitors);
        Assert.False(r.UsedFallback);
    }

    [Fact]
    public void Selected_and_multiple_modes()
    {
        Assert.Equal([Right], MonitorResolver.Resolve(Target(MonitorMode.Selected, Right, Left), Three, (0, 0)).Monitors);
        Assert.Equal([Left, Right], MonitorResolver.Resolve(Target(MonitorMode.Multiple, Left, Right, Left), Three, (0, 0)).Monitors);
    }

    [Fact]
    public void Missing_saved_monitor_falls_back_to_primary_with_a_reason()
    {
        var gone = new MonitorDesc("mon-gone", "Projector", default, default, 1, false);
        var r = MonitorResolver.Resolve(Target(MonitorMode.Selected, gone), Three, (0, 0));
        Assert.Equal([Main], r.Monitors);
        Assert.Equal("Projector", Assert.Single(r.Missing).Name);

        // Из нескольких выбранных доступен один — показывается он, отсутствующий сообщается.
        var m = MonitorResolver.Resolve(Target(MonitorMode.Multiple, gone, Right), Three, (0, 0));
        Assert.Equal([Right], m.Monitors);
        Assert.Single(m.Missing);
    }

    [Fact]
    public void Under_cursor_is_the_monitor_under_the_pointer_including_negative_coordinates()
    {
        Assert.Equal(Left, MonitorResolver.Resolve(Target(MonitorMode.UnderCursor), Three, (-5, 500)).Monitors[0]);
        Assert.Equal(Main, MonitorResolver.Resolve(Target(MonitorMode.UnderCursor), Three, (0, 0)).Monitors[0]);
        Assert.Equal(Right, MonitorResolver.Resolve(Target(MonitorMode.UnderCursor), Three, (3000, -300)).Monitors[0]);
        // Точка в «дыре» между мониторами (переходный момент) — ближайший, а не пусто.
        Assert.Equal(Right, MonitorResolver.UnderCursor(Three, (2600, -450)));
    }

    [Fact]
    public void No_monitors_gives_an_empty_result_without_throwing()
    {
        var r = MonitorResolver.Resolve(Target(MonitorMode.UnderCursor), [], (0, 0));
        Assert.Empty(r.Monitors);
    }

    [Fact]
    public void Placement_is_relative_to_each_work_area_in_its_own_dpi()
    {
        var p = PlacementSettings.Default; // снизу по центру, отступ 100 DIP
        var a = AppearanceSettings.Default;
        foreach (var m in Three)
        {
            var band = OverlayPlacement.Band(m.LocalWorkDip, p, a);
            var px = m.ToPixels(band);
            Assert.Equal(m.Work.Left, px.Left);
            Assert.InRange(px.Width, m.Work.Width - 1, m.Work.Width + 1);
            // Отступ снизу — 100 DIP этого монитора, т. е. 100 × масштаб физических пикселей.
            Assert.Equal(100 * m.Scale, m.Work.Bottom - px.Bottom, 1.0);
            Assert.True(px.Top >= m.Work.Top && px.Bottom <= m.Work.Bottom);
        }
    }

    [Fact]
    public void Indicator_has_one_window_per_target_monitor_and_follows_the_cursor_monitor_without_duplicates()
    {
        Sta.Run(() =>
        {
            var targets = new List<MonitorDesc> { Main, Right };
            var c = new LayoutIndicatorController(new NullProbe(), _ => null, () => targets, new FakeClock()) { ShowOnScreen = false };
            c.Apply(LayoutIndicatorSettings.Default, AppearanceSettings.Default);
            c.ShowLabel(new LayoutLabel("EN", null));
            Assert.Equal(2, c.Windows.Count);
            var first = c.Windows.Single(w => w.Monitor.Id == Main.Id).Window;

            // «Под курсором»: цель сменилась на левый монитор — окно правого закрыто, новое на левом, показ не перезапущен.
            long shown = c.Shown;
            targets.Remove(Right);
            targets.Add(Left);
            c.Relayout();
            Assert.Equal(2, c.Windows.Count);
            Assert.Same(first, c.Windows.Single(w => w.Monitor.Id == Main.Id).Window);
            Assert.Equal(shown, c.Shown);
            c.Dispose();
            Assert.Empty(c.Windows);
        });
    }

    [Fact]
    public void Indicator_position_on_a_monitor_is_inside_its_local_work_area()
    {
        Sta.Run(() =>
        {
            var c = new LayoutIndicatorController(new NullProbe(), _ => null, () => [Right], new FakeClock()) { ShowOnScreen = false };
            c.Apply(LayoutIndicatorSettings.Default with { Position = IndicatorPosition.Screen, Anchor = ScreenAnchor.BottomRight }, AppearanceSettings.Default);
            var size = new Size(120, 80);
            var pos = c.Position(size, Right);
            var wa = Right.LocalWorkDip;
            Assert.True(pos.X + size.Width <= wa.Right && pos.Y + size.Height <= wa.Bottom && pos.X >= 0 && pos.Y >= 0);
            c.Dispose();
        });
    }

    [Fact]
    public void Settings_v5_round_trip_keeps_monitor_choice()
    {
        var d = new DisplaySettings(Target(MonitorMode.Multiple, Left, Right), false, Target(MonitorMode.UnderCursor));
        var s = LabSettings.Default with { Displays = d };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(d, back.Settings.DisplaysOrDefault);
        Assert.Equal(s, back.Settings);
    }

    [Fact]
    public void Settings_v4_migrate_to_primary_monitor_and_bad_entries_are_reported()
    {
        var v4 = JsonNode.Parse(SettingsSerializer.Write(LabSettings.Default))!.AsObject();
        v4["formatVersion"] = 4;
        v4.Remove("displays");
        var r = SettingsSerializer.Read(v4.ToJsonString());
        Assert.Equal(4, r.MigratedFrom);
        Assert.Equal(DisplaySettings.Default, r.Settings.DisplaysOrDefault);

        var bad = JsonNode.Parse(SettingsSerializer.Write(LabSettings.Default))!.AsObject();
        bad["displays"] = JsonNode.Parse("""{"keycaps":{"mode":"selected","monitors":[{"name":"x"}]},"indicator":{"mode":"inherit"}}""");
        var rb = SettingsSerializer.Read(bad.ToJsonString());
        Assert.NotEmpty(rb.Problems);
        Assert.Equal(MonitorTarget.Primary, rb.Settings.DisplaysOrDefault.Keycaps); // «выбранный» без монитора — основной
    }

    [Fact]
    public void Every_color_theme_has_both_variants_and_readable_contrast()
    {
        foreach (var p in UiPalettes.List)
        {
            foreach (var (name, d) in new[] { ("light", p.Light), ("dark", p.Dark) })
            {
                Assert.True(UiPalettes.Contrast(d["Ko.TextBrush"], d["Ko.WindowBrush"]) >= 7, $"{p.Id}/{name} text");
                Assert.True(UiPalettes.Contrast(d["Ko.TextBrush"], d["Ko.CardBrush"]) >= 7, $"{p.Id}/{name} card text");
                Assert.True(UiPalettes.Contrast(d["Ko.AccentTextBrush"], d["Ko.AccentBrush"]) >= 4.5, $"{p.Id}/{name} accent button");
                Assert.True(UiPalettes.Contrast(d["Ko.AccentTextBrush"], d["Ko.AccentHoverBrush"]) >= 4.5, $"{p.Id}/{name} hover");
                Assert.True(UiPalettes.Contrast(d["Ko.AccentTextBrush"], d["Ko.AccentPressedBrush"]) >= 4.5, $"{p.Id}/{name} pressed");
                Assert.True(UiPalettes.Contrast(d["Ko.TextBrush"], d["Ko.SelectedBrush"]) >= 7, $"{p.Id}/{name} selected");
                Assert.True(UiPalettes.Contrast(d["Ko.ErrorBrush"], d["Ko.CardBrush"]) >= 4.5, $"{p.Id}/{name} error");
                Assert.True(UiPalettes.Contrast(d["Ko.FocusBrush"], d["Ko.WindowBrush"]) >= 3, $"{p.Id}/{name} focus ring");
            }
        }
    }

    [Fact]
    public void Emerald_uses_the_owner_hex_values()
    {
        var e = UiPalettes.Get("emerald").Dark;
        Assert.Equal(0x111513u, e["Ko.WindowBrush"]);
        Assert.Equal(0x222825u, e["Ko.CardBrush"]);
        Assert.Equal(0x087F5Bu, e["Ko.AccentBrush"]);
        Assert.Equal(0x31C99Au, e["Ko.GlowBrush"]);
        Assert.Equal(0xF2F5F3u, e["Ko.TextBrush"]);
        Assert.Equal("emerald", UiPalettes.DefaultId);
    }

    [Fact]
    public void Switching_palette_keeps_light_dark_mode()
    {
        Sta.Run(() =>
        {
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque)); // цвета палитры без прозрачности материала
            UiTheme.Set(UiThemeMode.Dark);
            UiTheme.SetPalette("classic");
            Assert.Equal(Color.FromRgb(0x1F, 0x1F, 0x21), ((SolidColorBrush)UiTheme.Brush("Ko.WindowBrush")).Color);
            UiTheme.SetPalette("emerald");
            Assert.True(UiTheme.IsDark);
            Assert.Equal(Color.FromRgb(0x11, 0x15, 0x13), ((SolidColorBrush)UiTheme.Brush("Ko.WindowBrush")).Color);
        });
    }

    [Fact]
    public void All_lucide_icons_parse_and_required_navigation_icons_exist()
    {
        Sta.Run(() =>
        {
            foreach (var name in Lucide.Names)
            {
                Assert.NotEmpty(Lucide.Geometries(name));
                Assert.All(Lucide.Geometries(name), g => Assert.False(g.Bounds.IsEmpty, name));
            }
        });
        foreach (var n in new[] { "house", "settings-2", "palette", "keyboard", "mouse", "languages", "move", "focus", "pencil", "command", "layers", "circle-question-mark", "stethoscope", "info", "puzzle", "star" })
        {
            Assert.True(Lucide.Has(n), n);
        }
    }

    [Fact]
    public void App_preferences_v2_migrate_to_v3_with_emerald_and_new_sections_round_trip()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bp-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "app.json");
            File.WriteAllText(path, """{"format":"keyoverlay-lab-app","formatVersion":2,"language":"uk","theme":"dark"}""");
            var store = AppPreferencesStore.Load(path);
            Assert.Equal(2, store.LoadedVersion);
            Assert.Equal("emerald", store.Current.ColorTheme);
            Assert.Equal(UiThemeMode.Dark, store.Current.Theme); // светлая/тёмная — отдельная настройка, сохраняется

            var p = store.Current with
            {
                ColorTheme = "classic",
                Privacy = new PrivacySettings(true, false, ["keepass.exe"]),
                Focus = FocusSettings.Default.WithEmerald() with { Mode = FocusMode.Dim },
                CursorZoom = CursorZoomSettings.Default with { Enabled = true, Mode = CursorZoomMode.Pinned },
                Bindings = CommandBindings.Empty.With("tool.focus.toggle", "Ctrl+Alt+Shift+F"),
            };
            var back = AppPreferencesStore.Parse(AppPreferencesStore.Write(p));
            Assert.Equal(p, back);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Privacy_blocked_app_names_are_normalized()
    {
        Assert.Equal("keepass.exe", PrivacySettings.NormalizeApp(@"C:\Program Files\KeePass\KeePass.exe"));
        Assert.Equal("bitwarden.exe", PrivacySettings.NormalizeApp("Bitwarden"));
        Assert.Null(PrivacySettings.NormalizeApp("  "));
        Assert.Null(PrivacySettings.NormalizeApp("a<b"));
    }

    [Fact]
    public void Focus_presets_match_the_concept_values()
    {
        var n = FocusSettings.Default.WithNeutral();
        Assert.Equal((0.7, 16.0, 2.0, "#D4D4D4", 0.0, 150), (n.DimOpacity, n.Radius, n.BorderWidth, n.BorderColor, n.EdgeSoftness, n.AppearMs));
        var z = CursorZoomSettings.Default;
        Assert.Equal((1.5, 220.0, CursorZoomShape.Circle, 2.0, true), (z.Zoom, z.Diameter, z.Shape, z.BorderWidth, z.Shadow));
        Assert.InRange(z.SmoothingMs, 40, 60);
        Assert.Equal(3.0, (FocusSettings.Default with { Zoom = 9 }).Sanitized().Zoom);
    }

    private sealed class NullProbe : ILayoutProbe
    {
        public LayoutSample Sample() => default;

        public IReadOnlyList<nint> InstalledLayouts() => [];
    }
}
