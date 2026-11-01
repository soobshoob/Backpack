using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Json.Schema;
using Backpack.Visual.Commands;
using Backpack.Visual.Plugins;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4I, п. 2: значок плагина. Автор задаёт значок в manifest («symbol», имя из каталога Lucide), пользователь может выбрать
/// другой и вернуть значок автора; выбор хранится по ID и переживает обновление пакета; меню, карточка и шапка показывают один
/// и тот же значок; звёздочка — только «в избранном».
/// </summary>
public class Phase4IPluginSymbolTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bp-sym-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase4IPluginSymbolTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private static JsonObject Manifest(string id, string name, string? symbol, string version = "1.0.0") => new()
    {
        ["format"] = "backpack-plugin",
        ["formatVersion"] = 1,
        ["id"] = id,
        ["name"] = new JsonObject { ["en"] = name },
        ["author"] = "Test author",
        ["version"] = version,
        ["sdk"] = "1.0",
        ["entry"] = "lib/P.dll",
        ["type"] = "P.Plugin",
        ["license"] = "Test",
        ["symbol"] = symbol,
    };

    private string Package(string file, JsonObject manifest)
    {
        string path = Path.Combine(_dir, file);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
        {
            w.Write(manifest.ToJsonString());
        }

        using (var w = new StreamWriter(zip.CreateEntry("lib/P.dll").Open()))
        {
            w.Write("never loaded");
        }

        return path;
    }

    [Theory]
    [InlineData("<Path Data=\"M0 0\"/>")]
    [InlineData("https://example.com/icon.png")]
    [InlineData("../icon")]
    [InlineData("Pen-Tool")]
    [InlineData("pen tool")]
    public void Manifest_symbol_is_only_an_icon_name_never_markup_paths_or_urls(string bad)
    {
        var (m, errors) = PluginManifest.Parse(Manifest("com.example.a", "A", bad).ToJsonString());
        Assert.Null(m);
        Assert.Contains("manifest: symbol", errors);
    }

    [Fact]
    public void Resolve_prefers_user_then_author_then_default_and_never_star()
    {
        Assert.Equal("timer", PluginSymbols.Resolve("timer", null));
        Assert.Equal("rocket", PluginSymbols.Resolve("timer", "rocket"));
        Assert.Equal(PluginSymbols.Fallback, PluginSymbols.Resolve(null, null));
        Assert.Equal(PluginSymbols.Fallback, PluginSymbols.Resolve("not-in-catalog", null)); // имя новее этой версии Backpack
        Assert.False(PluginSymbols.IsChoosable("star"));                                    // звёздочка — «в избранном»
        Assert.Equal("timer", PluginSymbols.Resolve("timer", "star"));
        Assert.All(PluginSymbols.Catalog, c => Assert.True(Backpack.Visual.Icons.Lucide.Has(c.Name), c.Name));
        Assert.Equal(PluginSymbols.Catalog.Count, PluginSymbols.Catalog.Select(c => c.Name).Distinct().Count());
        Assert.Contains("timer", PluginSymbols.Search("countdown"));
        Assert.Contains("pen-tool", PluginSymbols.Search("PEN"));
        Assert.Empty(PluginSymbols.Search("zzzz"));
    }

    [Fact]
    public void User_choice_is_stored_by_id_survives_update_and_reload_and_can_be_reset()
    {
        string root = Path.Combine(_dir, "plugins");
        var store = new PluginStore(root);
        Assert.Equal(ImportOutcome.Installed, store.Import(Package("a1.bppkg", Manifest("com.example.a", "A", "timer"))).Outcome);
        Assert.Equal(ImportOutcome.Installed, store.Import(Package("b1.bppkg", Manifest("com.example.b", "B", null))).Outcome);
        Assert.Equal("timer", store.Get("com.example.a")!.Symbol);
        Assert.Equal(PluginSymbols.Fallback, store.Get("com.example.b")!.Symbol);

        store.SetSymbol("com.example.a", "rocket");
        store.SetSymbol("com.example.b", "<bad>");                       // не из каталога — не сохраняется
        Assert.Equal("rocket", store.Get("com.example.a")!.Symbol);
        Assert.Null(store.Get("com.example.b")!.SymbolOverride);

        // Обновление пакета (новая версия, новый значок автора) не трогает выбор пользователя; перезапуск — тоже.
        Assert.Equal(ImportOutcome.Updated, store.Import(Package("a2.bppkg", Manifest("com.example.a", "A", "hourglass", "1.1.0"))).Outcome);
        var reloaded = new PluginStore(root);
        Assert.Equal("rocket", reloaded.Get("com.example.a")!.Symbol);
        Assert.Equal("hourglass", reloaded.Get("com.example.a")!.Manifest.Symbol);
        Assert.Contains("\"symbol\": \"rocket\"", File.ReadAllText(Path.Combine(root, "plugins.json")));

        reloaded.SetSymbol("com.example.a", null);
        Assert.Equal("hourglass", new PluginStore(root).Get("com.example.a")!.Symbol);
    }

    [Fact]
    public void Bundled_plugins_have_distinct_meaningful_icons_and_valid_manifests()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Repo.PathOf("docs", "plugins", "backpack-plugin-manifest.schema.json")));
        var expected = new Dictionary<string, string>
        {
            ["Drawing"] = "pen-tool", ["LayoutIndicator"] = "globe", ["Lens"] = "scan-search", ["Focus"] = "focus",
            ["CursorZoom"] = "zoom-in", ["PresentationTimer"] = "timer",
        };
        foreach (var (folder, symbol) in expected)
        {
            string text = File.ReadAllText(Repo.PathOf("lab", "samples", folder, "manifest.json"));
            var (m, errors) = PluginManifest.Parse(text);
            Assert.True(m is not null, folder + ": " + string.Join("; ", errors));
            Assert.Equal(symbol, m!.Symbol);
            Assert.True(PluginSymbols.IsChoosable(m.Symbol), folder);
            var r = schema.Evaluate(System.Text.Json.JsonDocument.Parse(text).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.True(r.IsValid, folder + ": " + string.Join("; ", (r.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => d.InstanceLocation + " " + e.Value))));
        }

        Assert.Equal(expected.Count, expected.Values.Distinct().Count());
    }

    [Fact]
    public void Menu_card_and_page_header_show_the_same_chosen_icon()
    {
        Sta.Run(() =>
        {
            Localization.Loc.Set(Localization.UiLanguage.English);
            var store = new PluginStore(Path.Combine(_dir, "plugins"));
            store.Import(Package("a.bppkg", Manifest("com.example.a", "Alpha", "timer")));
            store.Import(Package("b.bppkg", Manifest("com.example.b", "Beta", "pen-tool")));
            store.SetFavorite("com.example.b", true);
            store.SetSymbol("com.example.b", "rocket");
            var pm = new PluginManager(store, new CommandRegistry(), System.Windows.Threading.Dispatcher.CurrentDispatcher, "Backpack.exe", () => "en", safeMode: false);
            var w = new SettingsWindow(SettingsStore.Load(Path.Combine(_dir, "settings.json")), new SettingsWindowActions(() => { }, () => { }, () => "", Plugins: pm),
                SettingsSection.Plugins, prefs: AppPreferencesStore.Load(Path.Combine(_dir, "app.json")))
            {
                Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, Width = 1240, Height = 900,
            };
            w.Show();
            try
            {
                w.UpdateLayout();
                var glyphs = Descendants<FrameworkElement>(w).Where(e => e is Viewbox && e.Uid.Length > 0).Select(e => e.Uid).ToList();
                Assert.Contains("timer", glyphs);   // обычный пункт в группе «Плагины»
                Assert.Contains("rocket", glyphs);  // избранный — выбранный пользователем значок, не звёздочка и не коробка
                Assert.Equal("symbol", (string)((FrameworkElement)SettingsWindow.PluginIcon(store.Get("com.example.b")!, 44)).Tag);

                w.ShowPlugin("com.example.b");
                w.UpdateLayout();
                Assert.Equal("rocket", w.PageSymbol);
                Assert.Contains(Descendants<FrameworkElement>(w.Header!), e => (string?)e.Tag == "page-symbol");
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject =>
        Phase4ISecondaryLabelTests.Descendants<T>(root);
}
