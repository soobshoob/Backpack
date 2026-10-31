using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json.Nodes;
using Backpack.Sdk;
using KeyOverlay.Visual.Commands;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Plugins;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4G: перенос инструментов в плагины, контракт SDK 1.1, общая логика наведения, цвета рисования, мышь, мультимедиа.</summary>
public class Phase4GTests
{
    private static readonly string Samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));

    private static PluginManifest Sample(string name)
    {
        var (m, errors) = PluginManifest.Parse(File.ReadAllText(Path.Combine(Samples, name, "manifest.json")));
        Assert.True(m is not null, string.Join("; ", errors));
        return m!;
    }

    private static JsonObject Legacy(string mode = "dim") => new()
    {
        ["focus"] = new JsonObject
        {
            ["mode"] = mode, ["dimOpacity"] = 0.55, ["dimMonitors"] = "primary", ["edgeSoftness"] = 12, ["appearMs"] = 200,
            ["areaWidth"] = 500, ["areaHeight"] = 300, ["radius"] = 8, ["borderWidth"] = 3, ["borderColor"] = "#FF0000", ["glow"] = true,
        },
        ["tools"] = new JsonObject { ["focus"] = true },
    };

    [Theory]
    [InlineData("CursorZoom")]
    [InlineData("Lens")]
    [InlineData("Focus")]
    [InlineData("PresentationTimer")]
    public void Every_sample_manifest_is_valid_and_uses_sdk_1_1(string name)
    {
        var m = Sample(name);
        Assert.StartsWith("com.example.", m.Id);
        Assert.Equal("1.1", m.Sdk);
        Assert.NotEmpty(m.Commands);
        Assert.NotEmpty(m.Settings);
    }

    [Fact]
    public void Migration_plan_moves_settings_bindings_and_favorite_from_legacy_focus()
    {
        var m = Sample("Focus");
        var plan = PluginMigration.Plan(m, Legacy(), id => id == "tool.focus" ? "Ctrl+Alt+F" : null, existing: null, bound: _ => false);
        Assert.False(plan.IsEmpty);
        Assert.Equal(55, (double)plan.Settings!["opacity"]!, 3);            // dimOpacity*100
        Assert.Equal("primary", (string?)plan.Settings["monitors"]);
        Assert.Equal(500, (double)plan.Settings["width"]!);
        Assert.True((bool)plan.Settings["glow"]!);
        Assert.Equal("Ctrl+Alt+F", plan.Bindings["plugin.com.example.focus.start"]);   // общая команда шла в режим dim
        Assert.Contains("tool.focus", plan.Removed);
        Assert.Contains("tool.focus.dim", plan.Removed);
        Assert.True(plan.Favorite);
    }

    [Fact]
    public void Migration_respects_mode_condition_existing_values_and_existing_bindings()
    {
        var m = Sample("Focus");

        // Общая команда «Фокус» в режиме lens не относится к плагину затемнения.
        var lens = PluginMigration.Plan(m, Legacy("lens"), id => id == "tool.focus" ? "Ctrl+Alt+F" : null, null, _ => false);
        Assert.Empty(lens.Bindings);
        Assert.DoesNotContain("tool.focus", lens.Removed);

        // Уже сохранённые значения плагина и назначенное сочетание не перезаписываются.
        var again = PluginMigration.Plan(m, Legacy(), _ => "Ctrl+Alt+F", new JsonObject { ["opacity"] = 10 }, _ => true);
        Assert.Null(again.Settings);
        Assert.Empty(again.Bindings);

        // Значения вне диапазона манифеста не переносятся как есть.
        var bad = Legacy();
        bad["focus"]!["areaWidth"] = 99999;
        var clamped = PluginMigration.Plan(m, bad, _ => null, null, _ => false);
        Assert.True(clamped.Settings?["width"] is null || (double)clamped.Settings["width"]! <= 1600);
    }

    [Fact]
    public void Manifest_without_legacy_block_has_nothing_to_migrate()
    {
        var plan = PluginMigration.Plan(Sample("PresentationTimer"), Legacy(), _ => "Ctrl+1", null, _ => false);
        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Bundled_packages_are_listed_only_from_the_given_folder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bp-bundled-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "b" + PluginStore.PackageExtension), "");
            File.WriteAllText(Path.Combine(dir, "a" + PluginStore.PackageExtension), "");
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "");
            var list = PluginMigration.BundledPackages(dir);
            Assert.Equal(["a", "b"], list.Select(Path.GetFileNameWithoutExtension));
            Assert.Empty(PluginMigration.BundledPackages(Path.Combine(dir, "missing")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Plugin_profile_values_round_trip_through_settings_v8()
    {
        var values = PluginProfileValues.Empty
            .With("com.example.focus", new JsonObject { ["opacity"] = 40, ["glow"] = true })
            .With("com.example.timer", new JsonObject { ["minutes"] = 12 });
        var s = LabSettings.Default with { Plugins = values };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(values, back.Settings.PluginsOrEmpty);
        Assert.Equal(40, (int)back.Settings.PluginsOrEmpty.Get("com.example.focus")!["opacity"]!);
        Assert.Null(values.With("com.example.focus", null).Get("com.example.focus"));
        Assert.Equal(9, SettingsSerializer.FormatVersion);
    }

    [Fact]
    public void Profile_drops_plugin_entries_with_invalid_ids()
    {
        var o = JsonNode.Parse(SettingsSerializer.Write(LabSettings.Default))!.AsObject();
        o["plugins"] = new JsonObject { ["com.example.ok"] = new JsonObject { ["a"] = 1 }, ["../evil"] = new JsonObject { ["a"] = 1 }, ["com.example.num"] = 5 };
        var r = SettingsSerializer.Read(o.ToJsonString());
        Assert.Equal(["com.example.ok"], r.Settings.PluginsOrEmpty.Map.Keys);
    }

    [Fact]
    public void Disabled_command_with_a_saved_hotkey_reports_unavailable_not_not_set()
    {
        Sta.Run(() =>
        {
            var api = new RecordingApi();
            using var m = new HotkeyManager(api, createWindow: false);
            m.Apply(HotkeySettings.Default);
            Assert.True(Hotkey.TryParse("Ctrl+Alt+Shift+3", out var pin));
            Assert.True(Hotkey.TryParse("Ctrl+Alt+Shift+J", out var follow));
            int before = api.Registered.Count;
            m.ApplyCommands([
                ("plugin.com.example.cursor-zoom.pin", pin, CommandScope.Global, CommandTool.None, false),
                ("plugin.com.example.cursor-zoom.follow", follow, CommandScope.Global, CommandTool.None, true),
            ]);
            Assert.Equal(HotkeyStatus.Unavailable, m.StatusOf("plugin.com.example.cursor-zoom.pin"));   // 162201.png: было «Not set»
            Assert.Equal(HotkeyStatus.Registered, m.StatusOf("plugin.com.example.cursor-zoom.follow"));
            Assert.Equal(before + 1, api.Registered.Count);                                            // выключенная не регистрируется
            Assert.False(m.RaiseCommand("plugin.com.example.cursor-zoom.pin"));
        });
    }

    [Fact]
    public void Tool_parser_accepts_known_kinds_and_clamps_numbers()
    {
        // Сообщения приходят текстом из процесса плагина — разбираются так же.
        var mag = PluginToolParser.Parse(Wire("""{"id":"zoom","kind":"magnifier","mode":"follow","zoom":99,"width":5,"borderColor":"nope"}"""), out var kind);
        Assert.Equal("magnifier", kind);
        Assert.Equal(MagnifierMode.Follow, mag!.Magnifier!.Mode);
        Assert.Equal(4, mag.Magnifier.Zoom);
        Assert.Equal(60, mag.Magnifier.Width);
        Assert.Equal("#31C99A", mag.Magnifier.BorderColor);
        Assert.Null(mag.Dim);

        var dim = PluginToolParser.Parse(Wire("""{"id":"focus","kind":"dim","opacity":2,"monitors":"evil"}"""), out _);
        Assert.Equal(0.95, dim!.Dim!.Opacity);
        Assert.Equal("selection", dim.Dim.Monitors);

        Assert.Null(PluginToolParser.Parse(new JsonObject { ["id"] = "zoom", ["kind"] = "screen-capture" }, out _));   // нет такой операции
        Assert.Null(PluginToolParser.Parse(new JsonObject { ["id"] = "zoom", ["kind"] = "magnifier", ["mode"] = "spy" }, out _));
        Assert.Null(PluginToolParser.Parse(new JsonObject { ["id"] = "../x", ["kind"] = "dim" }, out _));
        Assert.False(PluginToolParser.IsValidToolId("A-upper-start"));
        Assert.True(PluginToolParser.IsValidToolId("lens-2"));
    }

    [Fact]
    public void Panel_parser_limits_buttons_text_and_unknown_icons()
    {
        var buttons = new JsonArray();
        for (int i = 0; i < 10; i++)
        {
            buttons.Add(new JsonObject { ["id"] = "b" + i, ["icon"] = i == 0 ? "play" : "unknown-icon", ["title"] = new string('x', 100) });
        }

        var p = PluginToolParser.ParsePanel(Wire(new JsonObject
        {
            ["title"] = "T", ["text"] = new string('9', 500), ["progress"] = 7, ["fontSize"] = 1000, ["opacity"] = 0,
            ["state"] = "explode", ["accent"] = "#00FF9C", ["buttons"] = buttons, ["movable"] = true,
        }.ToJsonString()), icon => icon == "play");
        Assert.Equal(6, p.Buttons.Count);
        Assert.Equal("play", p.Buttons[0].Icon);
        Assert.Equal("circle", p.Buttons[1].Icon);
        Assert.Equal(60, p.Buttons[0].Title.Length);
        Assert.Equal(200, p.Text.Length);
        Assert.Equal(1, p.Progress);
        Assert.Equal(160, p.FontSize);
        Assert.Equal(0.2, p.Opacity);
        Assert.Equal("normal", p.State);
        Assert.Equal("#00FF9C", p.Accent);
        Assert.True(p.Movable);
        Assert.Equal("finished", PluginToolParser.ParsePanel(new JsonObject { ["state"] = "finished" }, _ => false).State);
    }

    [Fact]
    public void Hover_reveal_keeps_controls_while_inside_held_or_recently_left()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var never = DateTime.MinValue;
        Assert.False(HoverReveal.ShouldShow(false, now, never, never, false));                               // по умолчанию скрыто
        Assert.True(HoverReveal.ShouldShow(true, now, never, never, false));                                 // наведение
        Assert.True(HoverReveal.ShouldShow(false, now, now - TimeSpan.FromMilliseconds(200), never, false)); // переход на панель — без мигания
        Assert.False(HoverReveal.ShouldShow(false, now, now - HoverReveal.HideDelay - TimeSpan.FromMilliseconds(1), never, false));
        Assert.True(HoverReveal.ShouldShow(false, now, never, now + TimeSpan.FromSeconds(1), false));        // клавиатурный вызов
        Assert.True(HoverReveal.ShouldShow(false, now, never, never, true));                                 // перетаскивание, меню
    }

    [Fact]
    public void Neon_palette_is_default_and_glows_while_classic_does_not()
    {
        Assert.Equal(new ThemeColor(0x00, 0xFF, 0x9C), DrawSettings.Default.Color);
        Assert.True(DrawingWindow.IsNeon(DrawSettings.Default.Color));
        Assert.True(DrawingWindow.IsNeon(DrawSettings.Default.Color with { A = 0x80 }));   // прозрачность не меняет набор
        Assert.Same(DrawingWindow.NeonPalette, DrawingWindow.PaletteFor(DrawSettings.Default.Color));
        Assert.Same(DrawingWindow.ClassicPalette, DrawingWindow.PaletteFor(DrawingWindow.ClassicPalette[0]));
        Assert.Same(DrawingWindow.NeonPalette, DrawingWindow.PaletteFor(new ThemeColor(0x12, 0x34, 0x56)));   // свой цвет
        Sta.Run(() =>
        {
            Assert.NotNull(DrawingWindow.NeonGlow(DrawSettings.Default.Color, 4));
            Assert.Null(DrawingWindow.NeonGlow(DrawingWindow.ClassicPalette[0], 4));
        });
    }

    [Fact]
    public void Old_red_default_drawing_colour_moves_to_neon_but_chosen_colours_stay()
    {
        static string Prefs(int version, string color)
        {
            var o = JsonNode.Parse(AppPreferencesStore.Write(AppPreferences.Default))!.AsObject();
            o["formatVersion"] = version;
            o["draw"]!["color"] = color;
            return o.ToJsonString();
        }

        Assert.Equal(DrawSettings.Default.Color, AppPreferencesStore.Parse(Prefs(4, "#FF3B30"))!.Draw!.Color);
        Assert.Equal(new ThemeColor(0x0A, 0x84, 0xFF), AppPreferencesStore.Parse(Prefs(4, "#0A84FF"))!.Draw!.Color);
        Assert.Equal(new ThemeColor(0xFF, 0x3B, 0x30), AppPreferencesStore.Parse(Prefs(5, "#FF3B30"))!.Draw!.Color); // выбран после v5
    }

    [Fact]
    public void Mouse_outline_width_and_solid_outline_round_trip_and_clamp()
    {
        var look = MouseVisual.Default with { OutlineWidth = 4.2, SolidOutline = new ThemeColor(0x10, 0x20, 0x30) };
        var s = LabSettings.Default with { Appearance = LabSettings.Default.Appearance with { Mouse = look } };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(look, back.Settings.Appearance.MouseLook);

        var o = JsonNode.Parse(SettingsSerializer.Write(s))!.AsObject();
        o["appearance"]!["mouse"]!["outlineWidth"] = 50;
        var bad = SettingsSerializer.Read(o.ToJsonString());
        Assert.InRange(bad.Settings.Appearance.MouseLook.OutlineWidth, Limits.MouseOutline.Min, Limits.MouseOutline.Max);
    }

    [Theory]
    [InlineData("VolumeMute", "Mute")]
    [InlineData("VolumeUp", "Vol +")]
    [InlineData("MediaNext", "Next")]
    public void Media_keys_show_their_action_in_every_label_set(string key, string text)
    {
        Assert.Contains(key, KeyLabels.MediaKeys);
        foreach (var set in Enum.GetValues<LabelSet>())
        {
            var c = KeyLabels.Resolve(new KeycapItem(1, 1, key, false, false), set, LabelMode.Text, sideMarkers: true);
            Assert.DoesNotContain("SC", c.Text);
            Assert.Contains(text, c.Text);
        }
    }

    [Fact]
    public void Logo_opens_home_and_menu_has_no_separate_home_item_or_visible_scrollbar()
    {
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Keys);
            try
            {
                var home = w.NavItems.Single(n => (SettingsSection)n.Tag == SettingsSection.Home);
                Assert.Equal("home", home.Uid);
                Assert.Equal(Loc.T("Settings.Nav.Home"), System.Windows.Automation.AutomationProperties.GetName(home));
                Assert.Equal(Loc.T("Settings.Nav.Home"), home.ToolTip);
                Assert.True(home.Focusable);
                var content = (StackPanel)home.Content;
                Assert.IsType<Viewbox>(content.Children[0]);                       // знак Backpack, а не значок «домик»
                Assert.Equal("Backpack", System.Windows.Automation.AutomationProperties.GetName(content.Children[1]));
                Assert.NotNull(KeyOverlay.Visual.Brand.Wordmark.Dot((TextBlock)content.Children[1])); // Phase 4H: «Backpack.» с точкой
                Assert.True(((FrameworkElement)content.Children[0]).Width >= 32);  // заметно крупнее значков меню (19)

                // Пункт меню «Главная» исчез: логотип стоит над прокручиваемым списком.
                var scroll = ((Panel)home.Parent).Children.OfType<ScrollViewer>().Single();
                Assert.Equal(ScrollBarVisibility.Hidden, scroll!.VerticalScrollBarVisibility);   // прокрутка есть, полосы нет
                Assert.DoesNotContain(home, ((Panel)scroll.Content).Children.OfType<RadioButton>());

                home.IsChecked = true;                                              // щелчок, Enter или пробел
                Assert.Equal(SettingsSection.Home, w.Section);
                Assert.Single(w.NavItems, n => n.IsChecked == true);
                w.Show(SettingsSection.Mouse);
                Assert.False(home.IsChecked);
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    [Fact]
    public void Variant_specific_rows_are_hidden_for_other_renderers_and_keep_their_values()
    {
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
            store.Update(x => x with { Appearance = x.Appearance with { Renderer = KeycapRenderer.Minimal, UseGradient = false, VolEnterShape = false } });
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Appearance);
            try
            {
                bool Shown(string key)
                {
                    var label = Descendants<TextBlock>(w.Cards).First(t => t.Text == Loc.T(key));
                    for (DependencyObject? d = label; d is not null && d != w.Cards; d = System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
                    {
                        if (d is UIElement { Visibility: not Visibility.Visible })
                        {
                            return false;
                        }
                    }

                    return true;
                }

                Assert.False(Shown("Appearance.Gradient"));   // 163600.png: у минимального стиля градиента нет
                Assert.False(Shown("Appearance.VolEnter"));
                store.Update(x => x with { Appearance = x.Appearance with { Renderer = KeycapRenderer.Volumetric } });
                Assert.True(Shown("Appearance.VolEnter"));
                Assert.False(Shown("Appearance.Gradient"));
                store.Update(x => x with { Appearance = x.Appearance with { Renderer = KeycapRenderer.Pbt } });
                Assert.True(Shown("Appearance.Gradient"));
                Assert.False(Shown("Appearance.VolEnter"));
                Assert.False(store.Current.Appearance.UseGradient);         // скрытие не сбрасывает значения
                Assert.False(store.Current.Appearance.VolEnterShape);
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    [Theory]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(256)]
    public void Icon_outline_fills_the_square_and_is_centred(int px)
    {
        Sta.Run(() =>
        {
            var bmp = new System.Windows.Media.Imaging.FormatConvertedBitmap(KeyOverlay.Visual.Brand.LogoArt.Render(px, px <= 24, iconFrame: true), System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var pixels = new byte[px * px * 4];
            bmp.CopyPixels(pixels, px * 4, 0);
            int top = px, bottom = -1, left = px, right = -1;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    if (pixels[(((y * px) + x) * 4) + 3] > 40)
                    {
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }

            // 163251.png: прежде ~78 % высоты и смещение вниз; теперь контур по центру и почти во всю высоту.
            Assert.True(bottom - top + 1 >= px * 0.9, $"{px}: height {bottom - top + 1}");
            Assert.True(right - left + 1 >= px * 0.68, $"{px}: width {right - left + 1}");
            Assert.InRange(top - (px - 1 - bottom), -1 - (px / 64), 1 + (px / 64));
            Assert.InRange(left - (px - 1 - right), -1 - (px / 64), 1 + (px / 64));
        });
    }

    [Theory]
    [InlineData(400, 700)]   // вертикальное превью — вертикальный монитор
    [InlineData(700, 380)]   // горизонтальное — горизонтальный
    [InlineData(260, 900)]   // очень узкое
    public void Live_preview_scene_takes_the_aspect_of_its_area(double aw, double ah)
    {
        var size = LivePreview.VirtualSize(new Size(aw, ah));
        Assert.Equal(aw / ah, size.Width / size.Height, 3);                 // без полей: пропорции области
        double k = aw / size.Width;
        Assert.InRange(k, 0.75 - 1e-9, 2 + 1e-9);                          // элементы не мельче 0,75 базовой сцены
        Sta.Run(() =>
        {
            var p = LivePreview.CursorZoom(() => CursorZoomSettings.Default, animate: false);
            try
            {
                p.Measure(new Size(aw, ah));
                var inner = LivePreview.VirtualSize(new Size(aw - p.Margin.Left - p.Margin.Right, ah - p.Margin.Top - p.Margin.Bottom));
                Assert.Equal(inner.Width, p.Scene.Width, 1);                     // область без поля превью
                Assert.Equal(inner.Height, p.Scene.Height, 1);
                Assert.All(p.Scene.Children.OfType<FrameworkElement>(), e => Assert.True(Canvas.GetLeft(e) is double.NaN || Canvas.GetLeft(e) + e.Width <= p.SW + 1 || double.IsNaN(e.Width)));
            }
            finally
            {
                p.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(DrawToolbarMode.Docked, ScreenEdge.Top)]
    [InlineData(DrawToolbarMode.AutoHide, ScreenEdge.Bottom)]
    [InlineData(DrawToolbarMode.AutoHide, ScreenEdge.Left)]
    [InlineData(DrawToolbarMode.AutoHide, ScreenEdge.Right)]
    public void Drawing_preview_is_a_virtual_monitor_and_toolbar_fits_its_edge(DrawToolbarMode mode, ScreenEdge edge)
    {
        Sta.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "bp-vm-" + Guid.NewGuid().ToString("N")[..8]);
            var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Drawing, prefs: prefs);
            try
            {
                var p = DrawingProfile.Default with { ToolbarMode = mode, AutoHideEdge = edge };
                foreach (var area in new[] { new Size(400, 760), new Size(720, 420), new Size(400, 260) })
                {
                    var preview = w.DrawingPreview(DrawSettings.Default, p);
                    preview.Measure(area);
                    preview.Arrange(new Rect(area));
                    var logical = w.DrawingPreviewArea;
                    string at = $"{mode}/{edge} {area} → {logical}";
                    Assert.Equal(area.Width / area.Height, logical.Width / logical.Height, 2);      // границы превью = монитор
                    Assert.True(area.Width / logical.Width >= SettingsWindow.DrawingPreviewMinScale - 1e-6, at); // панель не микроскопическая
                    var bar = w.DrawingPreviewBar!;
                    var r = new Rect(new Point(Canvas.GetLeft(bar), Canvas.GetTop(bar)), bar.DesiredSize);
                    Assert.True(r.Left >= -0.5 && r.Top >= -0.5 && r.Right <= logical.Width + 0.5 && r.Bottom <= logical.Height + 0.5, at + " " + r);
                    bool vertical = mode == DrawToolbarMode.AutoHide && edge is ScreenEdge.Left or ScreenEdge.Right;
                    if (vertical)
                    {
                        Assert.Equal(edge == ScreenEdge.Left ? DrawingWindow.EdgeMargin : logical.Width - DrawingWindow.EdgeMargin, edge == ScreenEdge.Left ? r.Left : r.Right, 1);
                    }
                    else if (edge == ScreenEdge.Bottom)
                    {
                        Assert.Equal(logical.Height - DrawingWindow.EdgeMargin, r.Bottom, 1);   // выбранный край действительно определяет место
                    }
                    else
                    {
                        Assert.True(r.Top < logical.Height / 4, at);
                    }
                }
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    [Fact]
    public void Every_sdk_capability_has_a_title_in_three_languages()
    {
        try
        {
            foreach (var lang in new[] { UiLanguage.English, UiLanguage.Ukrainian, UiLanguage.Russian })
            {
                Loc.Set(lang);
                foreach (var cap in Capabilities.Known)
                {
                    string key = "Plugins.Cap." + cap;
                    Assert.False(Loc.T(key) is { } t && (t == key || t.StartsWith('[')), $"{lang}: {key}");
                }
            }
        }
        finally
        {
            Loc.Set(UiLanguage.English);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (c is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(c))
            {
                yield return d;
            }
        }
    }

    private static JsonObject Wire(string json) => JsonNode.Parse(json)!.AsObject();

    private sealed class RecordingApi : IHotkeyApi
    {
        public Dictionary<int, (uint Mods, uint Vk)> Registered { get; } = new();

        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            Registered[id] = (modifiers, vk);
            return true;
        }

        public bool Unregister(nint hwnd, int id) => Registered.Remove(id);
    }
}
