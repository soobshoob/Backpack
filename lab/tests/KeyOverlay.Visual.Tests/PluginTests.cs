using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using KeyOverlay.Visual.Plugins;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4D: manifest, проверка и установка пакетов, доверие, сбои, граница протокола. Код плагинов здесь не запускается.</summary>
public class PluginTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bp-plugins-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private static JsonObject Manifest(string id = "com.example.timer", string version = "1.0.0", string sdk = "1.0") => new()
    {
        ["format"] = "backpack-plugin",
        ["formatVersion"] = 1,
        ["id"] = id,
        ["name"] = new JsonObject { ["en"] = "Timer", ["uk"] = "Таймер" },
        ["author"] = "Test",
        ["version"] = version,
        ["sdk"] = sdk,
        ["entry"] = "lib/Timer.dll",
        ["type"] = "Timer.Plugin",
        ["capabilities"] = new JsonArray("commands", "settings", "overlay.panel"),
        ["license"] = "Test",
        ["commands"] = new JsonArray(new JsonObject { ["id"] = "toggle", ["title"] = "Toggle" }),
        ["settings"] = new JsonArray(new JsonObject { ["key"] = "minutes", ["type"] = "number", ["title"] = "Minutes", ["default"] = 5, ["min"] = 1, ["max"] = 180 }),
    };

    private string Package(string name, Action<ZipArchive> fill)
    {
        string path = Path.Combine(_dir, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            fill(zip);
        }

        return path;
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open());
        w.Write(text);
    }

    private string Valid(JsonObject? manifest = null, string name = "ok.bppkg") => Package(name, z =>
    {
        Add(z, "manifest.json", (manifest ?? Manifest()).ToJsonString());
        Add(z, "lib/Timer.dll", "not really an assembly — import never loads it");
    });

    [Fact]
    public void Valid_package_installs_without_running_code_and_starts_disabled_and_untrusted()
    {
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        var r = store.Import(Valid());
        Assert.Equal(ImportOutcome.Installed, r.Outcome);
        var p = Assert.Single(store.List());
        Assert.False(p.Enabled);
        Assert.False(p.Trusted);
        Assert.False(store.SetEnabled(p.Id, true)); // без подтверждения доверия не включается
        store.Trust(p.Id);
        Assert.True(store.SetEnabled(p.Id, true));
        Assert.True(store.Get(p.Id)!.Enabled);
        store.SetFavorite(p.Id, true);
        Assert.True(new PluginStore(store.Root).Get(p.Id)!.Favorite); // состояние сохраняется
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("lib/../../evil.dll")]
    [InlineData("C:/Windows/evil.dll")]
    [InlineData("/abs/evil.dll")]
    public void Path_traversal_is_rejected(string entry)
    {
        var pkg = Package("bad.bppkg", z =>
        {
            Add(z, "manifest.json", Manifest().ToJsonString());
            Add(z, "lib/Timer.dll", "x");
            Add(z, entry, "x");
        });
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        Assert.Equal(ImportOutcome.Rejected, store.Import(pkg).Outcome);
        Assert.False(File.Exists(Path.Combine(_dir, "evil.dll")));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Corrupt_archive_bomb_duplicates_and_missing_entry_are_rejected()
    {
        string corrupt = Path.Combine(_dir, "corrupt.bppkg");
        File.WriteAllBytes(corrupt, [0x50, 0x4B, 3, 4, 1, 2, 3]);
        Assert.NotEmpty(PluginStore.Inspect(corrupt).Errors);

        var bomb = Package("bomb.bppkg", z =>
        {
            Add(z, "manifest.json", Manifest().ToJsonString());
            Add(z, "lib/Timer.dll", new string('A', 4 * 1024 * 1024)); // сжимается более чем в 100 раз
        });
        Assert.Contains(PluginStore.Inspect(bomb).Errors, e => e.Contains("size"));

        var dup = Package("dup.bppkg", z =>
        {
            Add(z, "manifest.json", Manifest().ToJsonString());
            Add(z, "lib/Timer.dll", "x");
            Add(z, "LIB/timer.dll", "y");
        });
        Assert.Contains(PluginStore.Inspect(dup).Errors, e => e.Contains("duplicate"));

        var noEntry = Package("noentry.bppkg", z => Add(z, "manifest.json", Manifest().ToJsonString()));
        Assert.Contains(PluginStore.Inspect(noEntry).Errors, e => e.Contains("entry"));
    }

    [Fact]
    public void Incompatible_sdk_bad_manifest_and_reserved_id_are_rejected()
    {
        Assert.Contains(PluginStore.Inspect(Valid(Manifest(sdk: "2.0"), "sdk.bppkg")).Errors, e => e.Contains("SDK"));
        var unknownCap = Manifest();
        unknownCap["capabilities"] = new JsonArray("commands", "settings", "keylogger");
        Assert.Null(PluginManifest.Parse(unknownCap.ToJsonString()).Manifest);
        var badId = Manifest(id: "Bad Id");
        Assert.Null(PluginManifest.Parse(badId.ToJsonString()).Manifest);
        var noCap = Manifest();
        noCap["capabilities"] = new JsonArray();
        Assert.Contains(PluginManifest.Parse(noCap.ToJsonString()).Errors, e => e.Contains("without capability"));
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        Assert.Equal(ImportOutcome.Rejected, store.Import(Valid(Manifest(id: "backpack.fake"), "res.bppkg")).Outcome);
    }

    [Fact]
    public void Same_id_updates_only_to_a_newer_version_and_keeps_trust_and_settings()
    {
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        store.Import(Valid(Manifest(version: "1.0.0"), "a.bppkg"));
        var m = store.Get("com.example.timer")!.Manifest;
        store.Trust(m.Id);
        store.SetEnabled(m.Id, true);
        store.SaveSettings(m, new JsonObject { ["minutes"] = 999, ["junk"] = "x" });
        Assert.Equal(180, (double)store.LoadSettings(m)["minutes"]!); // ограничено схемой, лишнее отброшено

        Assert.Equal(ImportOutcome.Rejected, store.Import(Valid(Manifest(version: "1.0.0"), "same.bppkg")).Outcome);
        Assert.Equal(ImportOutcome.Rejected, store.Import(Valid(Manifest(version: "0.9.0"), "old.bppkg")).Outcome);
        var up = store.Import(Valid(Manifest(version: "1.1.0"), "new.bppkg"));
        Assert.Equal(ImportOutcome.Updated, up.Outcome);
        Assert.Equal("1.0.0", up.PreviousVersion);
        var p = store.Get(m.Id)!;
        Assert.True(p.Trusted);
        Assert.False(p.Enabled); // новая версия не запускается без явного включения
        Assert.Equal(180, (double)store.LoadSettings(p.Manifest)["minutes"]!);
        Assert.True(PluginManifest.CompareVersions("1.0.0", "1.0.0-beta") > 0);
    }

    [Fact]
    public void Repeated_crashes_disable_the_plugin_and_remove_cleans_up()
    {
        var store = new PluginStore(Path.Combine(_dir, "plugins"));
        store.Import(Valid());
        store.Trust("com.example.timer");
        store.SetEnabled("com.example.timer", true);
        var t = DateTime.UtcNow;
        Assert.False(store.RecordCrash("com.example.timer", t));
        Assert.False(store.RecordCrash("com.example.timer", t.AddMinutes(1)));
        Assert.True(store.RecordCrash("com.example.timer", t.AddMinutes(2)));
        var p = store.Get("com.example.timer")!;
        Assert.False(p.Enabled);
        Assert.Equal("crashes", p.DisabledReason);
        Assert.True(store.Remove("com.example.timer"));
        Assert.Empty(store.List());
        Assert.False(Directory.Exists(Path.Combine(store.Root, "com.example.timer")));
    }

    [Fact]
    public void Protocol_boundary_checks_capabilities_and_sdk()
    {
        Sta.Run(() =>
        {
            var noPanel = Manifest();
            noPanel["capabilities"] = new JsonArray("commands", "settings");
            var m = PluginManifest.Parse(noPanel.ToJsonString()).Manifest!;
            var p = new PluginProcess(m, System.Windows.Threading.Dispatcher.CurrentDispatcher);
            PluginPanelSpec? shown = null;
            bool crashed = false;
            p.PanelChanged += x => shown = x;
            p.Crashed += _ => crashed = true;
            p.BeginForTest();
            p.Handle("""{"t":"panel","title":"x","text":"y"}"""); // до рукопожатия — нарушение
            p.Handle("""{"t":"ready","sdk":"1.0"}""");
            Assert.Equal(PluginRunState.Running, p.State);
            p.Handle("""{"t":"panel","title":"x","text":"y"}"""); // нет возможности overlay.panel
            Assert.Null(shown);
            Assert.Equal(2, p.Violations);
            p.Handle("not json");
            p.Handle("""{"t":"read-keys"}""");
            p.Handle("""{"t":"screen"}""");
            Assert.True(crashed); // пять нарушений — плагин остановлен
            Assert.Equal(PluginRunState.Failed, p.State);

            var p2 = new PluginProcess(PluginManifest.Parse(Manifest().ToJsonString()).Manifest!, System.Windows.Threading.Dispatcher.CurrentDispatcher);
            bool c2 = false;
            p2.Crashed += _ => c2 = true;
            p2.BeginForTest();
            p2.Handle("""{"t":"ready","sdk":"2.0"}""");
            Assert.True(c2); // несовместимый SDK
        });
    }
}
