using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Backpack.Visual.Commands;
using Backpack.Visual.Plugins;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>Phase 4F, п. 8: карточки плагинов — иконка и замена, длинные тексты, вид «Карточки / Список», модель доверия.</summary>
public class Phase4FPluginCardsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bp-cards-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase4FPluginCardsTests() => Directory.CreateDirectory(_dir);

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

    private static JsonObject Manifest(string id, string name, string? icon = null, string? description = null)
    {
        var m = new JsonObject
        {
            ["format"] = "backpack-plugin",
            ["formatVersion"] = 1,
            ["id"] = id,
            ["name"] = new JsonObject { ["en"] = name },
            ["author"] = "Test author",
            ["version"] = "1.2.3",
            ["sdk"] = "1.0",
            ["entry"] = "lib/P.dll",
            ["type"] = "P.Plugin",
            ["capabilities"] = new JsonArray("commands"),
            ["license"] = "Test",
            ["commands"] = new JsonArray(new JsonObject { ["id"] = "run", ["title"] = "Run" }),
        };
        if (icon is not null)
        {
            m["icon"] = icon;
            m["resources"] = new JsonArray(icon); // файлы пакета вне entry перечисляются в resources
        }

        if (description is not null)
        {
            m["description"] = new JsonObject { ["en"] = description };
        }

        return m;
    }

    private string Package(string file, JsonObject manifest, byte[]? icon = null)
    {
        string path = Path.Combine(_dir, file);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
            {
                w.Write(manifest.ToJsonString());
            }

            using (var w = new StreamWriter(zip.CreateEntry("lib/P.dll").Open()))
            {
                w.Write("not an assembly — never loaded by import or by the card");
            }

            if (icon is not null)
            {
                using var s = zip.CreateEntry((string)manifest["icon"]!).Open();
                s.Write(icon);
            }
        }

        return path;
    }

    private static byte[] Png()
    {
        var bmp = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)0x80, 64).ToArray(), 16);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Manifest_icon_must_be_a_png_inside_the_package()
    {
        Assert.Null(PluginManifest.Parse(Manifest("com.example.a", "A", icon: "../x.png").ToJsonString()).Manifest);
        Assert.Null(PluginManifest.Parse(Manifest("com.example.a", "A", icon: "icon.exe").ToJsonString()).Manifest);
        Assert.Equal("assets/icon.png", PluginManifest.Parse(Manifest("com.example.a", "A", icon: "assets/icon.png").ToJsonString()).Manifest!.Icon);
        Assert.Null(PluginManifest.Parse(Manifest("com.example.a", "A").ToJsonString()).Manifest!.Icon);
    }

    [Fact]
    public void Cards_show_icon_or_fallback_long_texts_wrap_view_is_remembered_and_nothing_runs()
    {
        Sta.Run(() =>
        {
            Localization.Loc.Set(Localization.UiLanguage.English);
            var store = new PluginStore(Path.Combine(_dir, "plugins"));
            string longName = "A plugin with a very long name that must wrap inside its card instead of breaking the layout";
            string longText = string.Join(" ", Enumerable.Repeat("Long description text.", 17));
            Assert.Equal(ImportOutcome.Installed, store.Import(Package("a.bppkg", Manifest("com.example.icon", longName, "assets/icon.png", longText), Png())).Outcome);
            Assert.Equal(ImportOutcome.Installed, store.Import(Package("b.bppkg", Manifest("com.example.noicon", "Zeta"))).Outcome);
            Assert.Equal(ImportOutcome.Installed, store.Import(Package("c.bppkg", Manifest("com.example.badicon", "Beta", "icon.png"), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10])).Outcome);
            var pm = new PluginManager(store, new CommandRegistry(), System.Windows.Threading.Dispatcher.CurrentDispatcher, "Backpack.exe", () => "en", safeMode: false);
            var settings = SettingsStore.Load(Path.Combine(_dir, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(_dir, "app.json"));
            var w = new SettingsWindow(settings, new SettingsWindowActions(() => { }, () => { }, () => "", Plugins: pm), SettingsSection.Plugins, prefs: prefs)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 1240,
                Height = 900,
            };
            w.Show();
            try
            {
                w.UpdateLayout();
                var grid = w.Cards.Children.OfType<AdaptiveGrid>().Single();
                Assert.Equal(3, grid.Children.Count);
                var byName = store.List().ToDictionary(p => p.Id);
                Assert.Equal("icon", (string)((FrameworkElement)SettingsWindow.PluginIcon(byName["com.example.icon"], 44)).Tag);
                Assert.Equal("fallback", (string)((FrameworkElement)SettingsWindow.PluginIcon(byName["com.example.noicon"], 44)).Tag);
                Assert.Equal("fallback", (string)((FrameworkElement)SettingsWindow.PluginIcon(byName["com.example.badicon"], 44)).Tag);

                // Карточки не выходят за сетку, длинное имя и описание переносятся; узкое окно — одна колонка.
                foreach (var card in grid.Children.OfType<FrameworkElement>())
                {
                    var r = card.TransformToAncestor(grid).TransformBounds(new Rect(card.RenderSize));
                    Assert.True(r.Right <= grid.ActualWidth + 0.5, $"{r} vs {grid.ActualWidth}");
                }

                Assert.True(grid.Columns >= 2);
                w.Width = 600;
                w.UpdateLayout();
                Assert.Equal(1, grid.Columns);

                // Просмотр карточек не запускает код и не доверяет плагинам.
                Assert.Empty(pm.Running);
                Assert.All(store.List(), p => Assert.False(p.Trusted || p.Enabled));

                // «Список» запоминается в app.json и переживает перезапуск окна.
                prefs.Update(p => p with { PluginsAsList = true });
                w.Show(SettingsSection.Plugins);
                Assert.Empty(w.Cards.Children.OfType<AdaptiveGrid>());
                var json = AppPreferencesStore.Write(prefs.Current);
                Assert.Contains("\"pluginsView\": \"list\"", json);
                Assert.True(AppPreferencesStore.Parse(json)!.PluginsAsList);
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
                pm.Dispose();
            }
        });
    }
}
