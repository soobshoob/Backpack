using System.IO;
using KeyOverlay.Visual.Help;
using KeyOverlay.Visual.Layout;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Окна и контроллеры не оставляют подписок, таймеров и анимаций после закрытия.</summary>
public class WindowLifetimeTests
{
    private sealed class FakeProbe : ILayoutProbe
    {
        public LayoutSample Next { get; set; }

        public LayoutSample Sample() => Next;

        public IReadOnlyList<nint> InstalledLayouts() => [];
    }

    private static SettingsStore TempStore() => SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));

    private static SettingsWindowActions NoActions => new(() => { }, () => { }, () => "");

    [Fact]
    public void SettingsWindow_ReleasesStoreSubscription_TimersAndAnimations_InEverySection()
    {
        Sta.Run(() =>
        {
            var store = TempStore();
            int baseline = OverlayPresenter.ActiveRenderingSubscriptions;
            foreach (var section in Enum.GetValues<SettingsSection>())
            {
                var w = new SettingsWindow(store, NoActions, section);
                Assert.Equal(1, store.SubscriberCount);
                // Phase 4E, без профилей, предпочтений и плагинов: Главная, Клавиши (Стиль кейкапов, Положение и мониторы), Мышь,
                // Раскладка, Справка, О программе. «Фокус», «Рисование», «Увеличение у курсора», «Плагины», «Профили» и
                // «Настройки приложения» требуют своих хранилищ.
                Assert.Equal(7, w.NavItems.Count); // Phase 4F: без пункта «Справка»
                Assert.NotEmpty(w.Cards.Children);
                store.Update(s => s with { Appearance = s.Appearance with { Size = 30 + (int)section } }); // живое превью
                w.ReleaseResources();
                w.ReleaseResources();                // повторно — без эффекта
                Assert.Equal(0, store.SubscriberCount);
                Assert.False(w.DemoRunning);
                Assert.Equal(baseline, OverlayPresenter.ActiveRenderingSubscriptions);
                w.Close();
            }
        });
    }

    [Fact]
    public void SettingsWindow_PreviewUsesSameRenderer_AndFollowsSettings()
    {
        Sta.Run(() =>
        {
            var store = TempStore();
            var w = new SettingsWindow(store, NoActions, SettingsSection.Keys);
            Assert.True(w.DemoRunning);
            store.Update(s => s with { Keys = s.Keys with { Trail = TrailMode.FadeInPlace }, Appearance = s.Appearance with { LabelSet = LabelSet.MacOS } });
            Assert.Equal(TrailMode.FadeInPlace, w.Preview.Options.Trail);
            Assert.Equal(LabelSet.MacOS, w.Preview.Settings.LabelSet);

            w.Show(SettingsSection.Mouse);
            Assert.NotNull(w.MousePreview);
            w.Show(SettingsSection.Layout);
            Assert.NotNull(w.IndicatorPreview);
            store.Update(s => s with { LayoutIndicator = s.LayoutIndicator with { CustomStyle = true }, Appearance = s.Appearance with { Indicator = IndicatorVisual.Default with { TextSize = 50 } } });
            Assert.Equal(50, w.IndicatorPreview!.CodeSize);
            w.ReleaseResources();
            w.Close();
        });
    }

    [Fact]
    public void SettingsWindow_F1_FollowsCurrentSection()
    {
        Sta.Run(() =>
        {
            var store = TempStore();
            var w = new SettingsWindow(store, NoActions);
            Assert.Equal("11-styles.md", HelpService.ResolveTopic(w)); // «Внешний вид» — каталог стилей (Phase 2E)
            w.Show(SettingsSection.Keys);
            Assert.Equal("03-keys-and-labels.md", HelpService.ResolveTopic(w));
            w.Show(SettingsSection.Layout);
            Assert.Equal("05-keyboard-layout.md", HelpService.ResolveTopic(w));
            w.Show(SettingsSection.Position);
            Assert.Equal("06-position.md", HelpService.ResolveTopic(w));
            w.Show(SettingsSection.Mouse);
            Assert.Equal(SettingsWindow.HelpFile(SettingsSection.Mouse), HelpService.ResolveTopic(w));
            w.ReleaseResources();
            w.Close();
        });
    }

    [Fact]
    public void SettingsWindow_ResetSection_RestoresOnlyThatSection()
    {
        Sta.Run(() =>
        {
            var store = TempStore();
            var w = new SettingsWindow(store, NoActions, SettingsSection.Position);
            store.Update(s => s with { Placement = new PlacementSettings(ScreenAnchor.Center, 5, 5, 300, -100), Keys = s.Keys with { LingerMs = 999 } });
            var reset = typeof(SettingsWindow).GetMethod("ResetSection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            reset.Invoke(w, null);
            Assert.Equal(PlacementSettings.Default, store.Current.Placement);
            Assert.Equal(999, store.Current.Keys.LingerMs);
            w.ReleaseResources();
            w.Close();
        });
    }

    [Fact]
    public void HelpService_RepeatedF1_ReusesSingleWindow()
    {
        Sta.Run(() =>
        {
            HelpService.ShowOnScreen = false;
            try
            {
                int created = HelpService.WindowsCreated;
                var first = HelpService.Show("05-keyboard-layout.md");
                var second = HelpService.Show("04-mouse.md");
                var third = HelpService.Show();
                Assert.Same(first, second);
                Assert.Same(first, third);
                Assert.Equal(created + 1, HelpService.WindowsCreated);
                Assert.Equal("README.md", first.CurrentTopic);
                Assert.Equal(2, first.BackDepth);   // переходы попали в историю
            }
            finally
            {
                HelpService.ShowOnScreen = true;
            }
        });
    }

    /// <summary>Снимки содержимого окон настроек и справки (KEYOVERLAY_SNAPSHOTS=папка); окна на экран не выводятся.</summary>
    [Fact]
    public void WindowContent_Snapshots()
    {
        string? dir = Environment.GetEnvironmentVariable("KEYOVERLAY_SNAPSHOTS");
        Sta.Run(() =>
        {
            var store = TempStore();
            store.Update(s => s with { Appearance = s.Appearance with { Animation = AnimationKind.None } }); // снимок без незавершённого появления
            foreach (var section in Enum.GetValues<SettingsSection>())
            {
                var w = new SettingsWindow(store, NoActions with { LayoutStatus = () => Localization.Loc.F("LayoutStatus.Baseline", 0) }, section);
                if (section is SettingsSection.Appearance or SettingsSection.Keys)
                {
                    w.Preview.Update([new KeycapItem(1, 1, "Ctrl", true, true, KeySide.Left), new KeycapItem(2, 2, "K", false, false, PressCount: 2)]);
                }

                Render(w, dir, "settings-" + section);
                w.ReleaseResources();
                w.Close();
            }

            var help = new HelpWindow(HelpLibrary.FromResources());
            help.Navigate("03-keys-and-labels.md");
            Render(help, dir, "help-window");
            help.Close();
        });

        static void Render(System.Windows.Window w, string? dir, string name)
        {
            var content = (System.Windows.FrameworkElement)w.Content;
            content.Measure(new System.Windows.Size(w.Width, w.Height));
            content.Arrange(new System.Windows.Rect(0, 0, w.Width, w.Height));
            content.UpdateLayout();
            Assert.True(content.ActualWidth > 0);
            if (dir is null)
            {
                return;
            }

            Directory.CreateDirectory(dir);
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.Width, (int)w.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var bg = new System.Windows.Media.DrawingVisual();
            using (var dc = bg.RenderOpen())
            {
                dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(0, 0, w.Width, w.Height));
            }

            bmp.Render(bg);
            bmp.Render(content);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var f = File.Create(Path.Combine(dir, name + ".png"));
            enc.Save(f);
        }
    }

    [Fact]
    public void LayoutIndicator_PollsOnlyWhenEnabled_AndStopsOnDispose()
    {
        Sta.Run(() =>
        {
            var probe = new FakeProbe();
            var c = new LayoutIndicatorController(probe, () => new System.Windows.Rect(800, 900, 300, 100), () => new System.Windows.Rect(0, 0, 1920, 1040)) { ShowOnScreen = false };
            c.Apply(LayoutIndicatorSettings.Default with { Enabled = false }, AppearanceSettings.Default);
            Assert.False(c.IsPolling);
            c.Apply(LayoutIndicatorSettings.Default, AppearanceSettings.Default);
            Assert.True(c.IsPolling);
            c.Apply(LayoutIndicatorSettings.Default with { Enabled = false }, AppearanceSettings.Default);
            Assert.False(c.IsPolling);
            c.Apply(LayoutIndicatorSettings.Default, AppearanceSettings.Default);
            c.Dispose();
            Assert.False(c.IsPolling);
            c.Apply(LayoutIndicatorSettings.Default, AppearanceSettings.Default); // после Dispose не оживает
            Assert.False(c.IsPolling);
        });
    }
}
