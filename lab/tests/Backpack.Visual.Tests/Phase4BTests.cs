using System.IO;
using System.Windows.Controls;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>Phase 4B: меню, демонстрация на «Главной».</summary>
public class Phase4BTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bp-mig-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase4BTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

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
