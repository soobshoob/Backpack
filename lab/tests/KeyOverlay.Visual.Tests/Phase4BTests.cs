using System.IO;
using System.Windows.Controls;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4B: перенос данных KeyOverlay → Backpack, меню, демонстрация на «Главной».</summary>
public class Phase4BTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bp-mig-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase4BTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private string Old(string name = "KeyOverlay")
    {
        string old = Path.Combine(_root, "AppData", name);
        Directory.CreateDirectory(Path.Combine(old, "profiles"));
        Directory.CreateDirectory(Path.Combine(old, "styles"));
        File.WriteAllText(Path.Combine(old, "settings.json"), "{\"s\":1}");
        File.WriteAllText(Path.Combine(old, "app.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(old, "profiles", "p-1.json"), "{}");
        File.WriteAllText(Path.Combine(old, "styles", "s-1.json"), "{}");
        return old;
    }

    [Fact]
    public void KeyOverlay_data_moves_to_Backpack_and_the_source_is_kept()
    {
        string old = Old();
        var loc = DataLocations.Resolve(null, Path.Combine(_root, "Program"), Path.Combine(_root, "AppData"));
        Assert.Equal(Path.Combine(_root, "AppData", "Backpack"), loc.Directory);
        Assert.Equal(old, loc.MigratedFrom);
        foreach (var f in new[] { "settings.json", "app.json", Path.Combine("profiles", "p-1.json"), Path.Combine("styles", "s-1.json"), DataLocations.MigrationNote })
        {
            Assert.True(File.Exists(Path.Combine(loc.Directory, f)), f);
        }

        Assert.True(File.Exists(Path.Combine(old, "settings.json"))); // исходные данные остаются (резервная копия)
        Assert.False(Directory.Exists(loc.Directory + ".migrating"));

        // Повторный запуск: перенос не повторяется и не перезаписывает новые данные.
        File.WriteAllText(Path.Combine(loc.Directory, "settings.json"), "{\"new\":1}");
        var again = DataLocations.Resolve(null, Path.Combine(_root, "Program"), Path.Combine(_root, "AppData"));
        Assert.Null(again.MigratedFrom);
        Assert.Equal("{\"new\":1}", File.ReadAllText(Path.Combine(again.Directory, "settings.json")));
    }

    [Fact]
    public void Interrupted_migration_is_retried_from_scratch()
    {
        Old();
        string staging = Path.Combine(_root, "AppData", "Backpack.migrating");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "half.json"), "x");
        var loc = DataLocations.Resolve(null, Path.Combine(_root, "Program"), Path.Combine(_root, "AppData"));
        Assert.NotNull(loc.MigratedFrom);
        Assert.False(File.Exists(Path.Combine(loc.Directory, "half.json")));
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public void Older_lab_folder_is_used_when_there_is_no_KeyOverlay_folder_and_legacy_portable_marker_works()
    {
        string lab = Old("KeyOverlay.Lab");
        var loc = DataLocations.Resolve(null, Path.Combine(_root, "Program"), Path.Combine(_root, "AppData"));
        Assert.Equal(lab, loc.MigratedFrom);

        string program = Path.Combine(_root, "Portable");
        Directory.CreateDirectory(program);
        File.WriteAllText(Path.Combine(program, DataLocations.LegacyPortableMarker), "");
        Assert.True(DataLocations.Resolve(null, program, Path.Combine(_root, "AppData")).Portable);
    }

    [Fact]
    public void Navigation_is_flat_without_duplicates_and_every_item_has_an_icon()
    {
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(_root, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(_root, "app.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Home, prefs: prefs);
            var names = w.NavItems.Select(n => System.Windows.Automation.AutomationProperties.GetName(n)).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal(
                [SettingsSection.Home, SettingsSection.Keys, SettingsSection.Appearance, SettingsSection.Position, SettingsSection.Layout,
                 SettingsSection.Mouse, SettingsSection.Application, SettingsSection.Interface, SettingsSection.Privacy,
                 SettingsSection.Hotkeys, SettingsSection.About], // Phase 4F: «Справка» — только внизу меню
                w.NavItems.Select(n => (SettingsSection)n.Tag).ToList());
            Assert.All(w.NavItems, n => Assert.IsType<Viewbox>(((StackPanel)n.Content).Children[0])); // иконка Lucide
            Assert.DoesNotContain(names, n => n!.Contains("diagnost", StringComparison.OrdinalIgnoreCase));

            // Phase 4E: подстраница — собственный пункт раскрытой группы (выделен он сам, группа раскрыта); страница инструмента
            // начинается с «Инструмент включён».
            w.Show(SettingsSection.Hotkeys);
            Assert.True(w.NavItems.Single(n => (SettingsSection)n.Tag == SettingsSection.Hotkeys).IsChecked);
            Assert.False(w.NavItems.Single(n => (SettingsSection)n.Tag == SettingsSection.Application).IsChecked);
            Assert.True(w.IsExpanded(SettingsSection.Application));
            Assert.Single(w.NavItems, n => n.IsChecked == true);
            w.Show(SettingsSection.Lens);
            Assert.Equal(Localization.Loc.T("Tool.Enabled.Title"), ((TextBlock)((StackPanel)((Border)w.Cards.Children[0]).Child).Children[0]).Text);
            w.ReleaseResources();
            w.Close();
        });
    }

    [Fact]
    public void Home_demo_covers_all_phases_and_stops()
    {
        Sta.Run(() =>
        {
            var demo = new HomeDemo(LabSettings.Default, animate: false);
            Assert.False(demo.IsPlaying); // «меньше движения» / выключенные анимации — не запускается сама
            var phases = new HashSet<int>();
            for (int i = 0; i < HomeDemo.CycleSteps; i++)
            {
                demo.Step();
                phases.Add(demo.Phase);
            }

            Assert.Equal([0, 1, 2, 3], phases.OrderBy(x => x));
            demo.SetPlaying(true);
            Assert.True(demo.IsPlaying);
            demo.Dispose();
            Assert.False(demo.IsPlaying);
        });
    }
}
