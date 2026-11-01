using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Backpack.Sdk;
using Backpack.Visual.Commands;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Plugins;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4H, п. 5: рисование и индикатор раскладки — плагины (SDK 1.2); поставка «От создателя» по индексу с SHA-256;
/// установка поставляемых без запуска кода; перенос прежних настроек и сочетаний. Код плагинов здесь не запускается.
/// </summary>
public class Phase4HPluginsTests : IDisposable
{
    private static readonly string Samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bp-4h-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase4HPluginsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private static PluginManifest Sample(string name)
    {
        var (m, errors) = PluginManifest.Parse(File.ReadAllText(Path.Combine(Samples, name, "manifest.json")));
        Assert.Empty(errors);
        return m!;
    }

    /// <summary>Пакет из manifest образца (вместо сборки — заглушка DLL: импорт код не загружает).</summary>
    private string Package(string sample, string? version = null, string? dir = null)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Samples, sample, "manifest.json")))!.AsObject();
        if (version is not null)
        {
            json["version"] = version;
        }

        dir ??= Path.Combine(_dir, "bundle");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, sample + "-" + (string)json["version"]! + PluginStore.PackageExtension);
        File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
        {
            w.Write(json.ToJsonString());
        }

        using (var w = new StreamWriter(zip.CreateEntry((string)json["entry"]!).Open()))
        {
            w.Write("not an assembly — import never loads it");
        }

        return path;
    }

    [Fact]
    public void Drawing_and_layout_indicator_manifests_use_sdk_1_2_capabilities_and_carry_legacy_rules()
    {
        var draw = Sample("Drawing");
        Assert.Equal("1.2", draw.Sdk);
        Assert.True(draw.Has(Capabilities.OverlayDraw));
        Assert.True(SdkInfo.IsCompatible(draw.Sdk));
        var shot = draw.Commands.Single(c => c.Id == "screenshot");
        Assert.True(shot.WhileTool);
        Assert.Equal("F2", shot.Hotkey);
        Assert.Equal("Ctrl+Alt+Shift+D", draw.Commands.Single(c => c.Id == "toggle").Hotkey);
        Assert.All(draw.Commands, c => Assert.True(Icons.Lucide.Has(c.Icon!), c.Id));
        Assert.Equal(11, draw.Commands.Count(c => c.Id.StartsWith("tool-", StringComparison.Ordinal)));
        Assert.Contains(draw.Settings, f => f.Key == "toolbarX" && f.Hidden);
        Assert.Equal("drawing", draw.Preview!.Scene);
        Assert.Equal("drawing", draw.Legacy!.Section);

        var li = Sample("LayoutIndicator");
        Assert.True(li.Has(Capabilities.InputLayout) && li.Has(Capabilities.OverlayIndicator));
        Assert.False(li.Has(Capabilities.InputVisible)); // нажатия клавиш индикатору не нужны
        Assert.Equal("toggle", li.Legacy!.Commands["layout.indicator"]);
        Assert.Equal("indicator", li.Preview!.Scene);
    }

    [Fact]
    public void Bundled_index_accepts_only_packages_whose_sha256_and_manifest_match()
    {
        string dir = Path.Combine(_dir, "bundle");
        string a = Package("Drawing", dir: dir), b = Package("LayoutIndicator", dir: dir);
        BundledIndex.Write(dir);
        var index = BundledIndex.Load(dir);
        Assert.Equal(2, index.Verified.Count);
        Assert.Empty(index.Problems);

        // Подменённый файл (другой хеш) — не «От создателя».
        File.AppendAllText(b, "x");
        var tampered = BundledIndex.Load(dir);
        Assert.Single(tampered.Verified);
        Assert.Contains(tampered.Problems, p => p.Contains("SHA-256", StringComparison.Ordinal));
        Assert.True(tampered.Contains("com.example.drawing"));
        Assert.False(tampered.Contains("com.example.layout-indicator"));
        Assert.Same(BundledIndex.Empty.Verified, BundledIndex.Load(Path.Combine(_dir, "nothing")).Verified);
    }

    [Fact]
    public void Bundled_plugins_install_disabled_untrusted_marked_and_removed_ones_stay_removed()
    {
        string dir = Path.Combine(_dir, "bundle");
        Package("Drawing", dir: dir);
        Package("LayoutIndicator", dir: dir);
        BundledIndex.Write(dir);
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        var added = PluginMigration.SyncBundled(store, BundledIndex.Load(dir));
        Assert.Equal(2, added.Count);
        Assert.All(store.List(), p =>
        {
            Assert.False(p.Enabled);
            Assert.False(p.Trusted);
            Assert.True(store.IsBundled(p));
        });
        Assert.Empty(PluginMigration.SyncBundled(store, BundledIndex.Load(dir))); // повторный запуск ничего не меняет

        // Пользователь удалил поставляемый плагин — при следующем запуске он не возвращается сам.
        Assert.True(store.Remove("com.example.drawing"));
        Assert.Contains("com.example.drawing", new PluginStore(store.Root).DismissedBundled);
        Assert.Empty(PluginMigration.SyncBundled(new PluginStore(store.Root), BundledIndex.Load(dir)));

        // Тот же ID из пакета пользователя — без отметки, хотя автор в manifest — «Backpack».
        var mine = Package("LayoutIndicator", version: "1.1.0", dir: Path.Combine(_dir, "mine"));
        var fresh = new PluginStore(store.Root);
        Assert.Equal(ImportOutcome.Updated, fresh.Import(mine).Outcome);
        Assert.False(fresh.IsBundled(fresh.Get("com.example.layout-indicator")!));
    }

    [Fact]
    public void Update_from_the_delivery_keeps_an_enabled_plugin_enabled_but_a_user_update_does_not()
    {
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        string v1 = Package("Drawing", dir: Path.Combine(_dir, "b1"));
        store.Import(v1, BundledIndex.Sha256Of(v1));
        store.Trust("com.example.drawing");
        store.SetEnabled("com.example.drawing", true);

        string v2 = Package("Drawing", version: "1.0.1", dir: Path.Combine(_dir, "b2"));
        store.Import(v2, BundledIndex.Sha256Of(v2));
        Assert.True(store.Get("com.example.drawing")!.Enabled); // осознанный выбор сохранён

        string v3 = Package("Drawing", version: "1.0.2", dir: Path.Combine(_dir, "b3"));
        store.Import(v3);
        Assert.False(store.Get("com.example.drawing")!.Enabled); // пакет пользователя — до явного включения
    }

    [Fact]
    public void Legacy_drawing_and_indicator_settings_and_hotkeys_move_to_the_plugins()
    {
        var prefs = AppPreferences.Default with
        {
            Draw = DrawSettings.Default with { Tool = DrawTool.Pen, Thickness = 9, Dash = DrawDash.Dotted },
            Markers = new MarkerSettings(5, 48),
            Bindings = CommandBindings.Empty.With("tool.drawing", "").With("tool.radial", "Ctrl+Alt+R"),
        };
        var settings = LabSettings.Default with
        {
            Drawing = DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = ScreenEdge.Left, ShotAction = ScreenshotAction.SaveAndCopy },
            LayoutIndicator = LayoutIndicatorSettings.Default with { DurationMs = 2500, CustomStyle = true, Position = IndicatorPosition.Screen, Anchor = ScreenAnchor.TopRight },
        };
        var legacy = PluginLegacyMigration.Legacy(prefs, settings);
        var binds = prefs.BindingsOrEmpty;

        var draw = PluginMigration.Plan(Sample("Drawing"), legacy, id => binds[id], null, _ => false);
        Assert.Equal("pen", (string?)draw.Settings!["tool"]);
        Assert.Equal(9, (double)draw.Settings["thickness"]!);
        Assert.Equal("dotted", (string?)draw.Settings["dash"]);
        Assert.Equal("autohide", (string?)draw.Settings["toolbar"]);
        Assert.Equal("left", (string?)draw.Settings["autoHideEdge"]);
        Assert.Equal("both", (string?)draw.Settings["shotAction"]);
        Assert.Equal(5, (double)draw.Settings["markerStart"]!);
        // Снятое пользователем сочетание переносится как «без сочетания» — сочетание плагина по умолчанию не включается.
        Assert.Equal("", draw.Bindings["plugin.com.example.drawing.toggle"]);
        Assert.Equal("Ctrl+Alt+R", draw.Bindings["plugin.com.example.drawing.radial"]);
        Assert.Contains("tool.drawing.screenshot", draw.Removed);
        Assert.True(draw.Favorite); // инструмент был включён — плагин в меню (но выключен)

        var li = PluginMigration.Plan(Sample("LayoutIndicator"), legacy, id => binds[id], null, _ => false);
        Assert.Equal(2500, (double)li.Settings!["durationMs"]!);
        Assert.Equal("custom", (string?)li.Settings["style"]);
        Assert.Equal("screen", (string?)li.Settings["position"]);
        Assert.Equal("topRight", (string?)li.Settings["anchor"]);
    }

    [Fact]
    public void Sdk_1_2_messages_are_checked_at_the_boundary()
    {
        var open = Protocol.ToJson("canvas", new DrawRequest(Tool: "polygon", Thickness: 400, Toolbar: "radial", ToolbarX: 0.4));
        var r = PluginToolParser.Parse(open, out string? kind);
        Assert.Equal("draw", kind);
        Assert.Equal("polygon", r!.Draw!.Tool);
        Assert.Equal(40, r.Draw.Thickness);           // ограничено
        Assert.Equal(0.4, r.Draw.ToolbarX);
        open["tool"] = "flamethrower";
        Assert.Equal("smartpen", PluginToolParser.Parse(open, out _)!.Draw!.Tool);

        Assert.Equal("tool:arrow", PluginToolParser.ParseDrawAction("tool:arrow"));
        Assert.Null(PluginToolParser.ParseDrawAction("tool:nope"));
        Assert.Null(PluginToolParser.ParseDrawAction("format-disk"));

        var ind = PluginToolParser.ParseIndicator(Protocol.ToJson(new IndicatorRequest("UK", "Українська", 999999, "fade", "screen", "bottomLeft", Style: "custom")))!;
        Assert.Equal(10000, ind.DurationMs);
        Assert.Equal("bottomLeft", ind.Anchor);
        Assert.Null(PluginToolParser.ParseIndicator(new JsonObject { ["t"] = "indicator", ["code"] = "  " }));
    }

    [Fact]
    public void Draw_request_maps_to_the_host_drawing_and_back()
    {
        var req = new DrawRequest(Tool: "roundrect", Color: "#FF3B30", Thickness: 7, Dash: "dashed", Toolbar: "autohide", AutoHideEdge: "right", AfterShot: "exit");
        var s = PluginDrawHost.ToSettings(req);
        Assert.Equal(DrawTool.RoundedRectangle, s.Tool);
        Assert.Equal(DrawDash.Dashed, s.Dash);
        var p = PluginDrawHost.ToProfile(req);
        Assert.Equal(DrawToolbarMode.AutoHide, p.ToolbarMode);
        Assert.Equal(ScreenEdge.Right, p.AutoHideEdge);
        Assert.Equal(AfterScreenshot.ExitDrawing, p.AfterShot);
        var back = PluginDrawHost.FromSettings(s);
        Assert.Equal("roundrect", (string?)back["tool"]);
        Assert.Equal("#FF3B30", (string?)back["color"]);
        Assert.All(Enum.GetValues<DrawTool>(), t => Assert.Equal(t, PluginDrawHost.ToolOf(PluginDrawHost.NameOf(t))));
    }

    [Fact]
    public void While_tool_hotkeys_register_only_while_the_plugin_has_a_tool_open()
    {
        Sta.Run(() =>
        {
            var reg = new CommandRegistry();
            reg.Register(new CommandDef("plugin.com.example.drawing.screenshot", "Shot", "Commands.Category.Plugins", CommandScope.Tool, CommandKind.Action, () => { },
                DefaultHotkey: new Hotkey(HotkeyModifiers.None, 0x71), Tool: CommandTool.Plugin, Source: "com.example.drawing"));
            using var manager = new HotkeyManager(new AlwaysFree(), createWindow: false);
            manager.Apply(HotkeySettings.None);
            manager.ApplyCommands(reg.Bindings(CommandBindings.Empty));
            Assert.Equal(HotkeyStatus.WaitingForMode, manager.StatusOf("plugin.com.example.drawing.screenshot")); // F2 остаётся другим программам
            manager.SetPluginToolActive("com.example.drawing", true);
            Assert.Equal(HotkeyStatus.Registered, manager.StatusOf("plugin.com.example.drawing.screenshot"));
            manager.SetPluginToolActive("com.example.other", true); // чужой инструмент не включает сочетание
            manager.SetPluginToolActive("com.example.drawing", false);
            Assert.Equal(HotkeyStatus.WaitingForMode, manager.StatusOf("plugin.com.example.drawing.screenshot"));
        });
    }

    private sealed class AlwaysFree : IHotkeyApi
    {
        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            return true;
        }

        public bool Unregister(nint hwnd, int id) => true;
    }
}
