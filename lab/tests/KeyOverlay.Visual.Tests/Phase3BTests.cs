using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Profiles;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 3B: панель рисования (режимы, края, перенос), текст, снимки, радиальное меню без панели.</summary>
public class Phase3BTests
{
    private static DrawingWindow Window(DrawingProfile p, double w = 1100, double h = 620)
    {
        var win = new DrawingWindow(new PixelRect(-32000, -32000, (int)w, (int)h), 1.0, DrawSettings.Default, p);
        var root = (FrameworkElement)win.Content;
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));
        win.ApplyProfile(p);
        root.UpdateLayout();
        return win;
    }

    [Theory]
    [InlineData(ScreenEdge.Top)]
    [InlineData(ScreenEdge.Bottom)]
    [InlineData(ScreenEdge.Left)]
    [InlineData(ScreenEdge.Right)]
    public void AutoHideToolbar_SitsAtItsEdge_AndFitsTheMonitor(ScreenEdge edge)
    {
        Sta.Run(() =>
        {
            foreach (var (w, h) in new[] { (1100.0, 620.0), (1920.0, 1080.0), (800.0, 600.0) })
            {
                var win = Window(DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = edge }, w, h);
                win.RevealToolbarForPreview();
                ((FrameworkElement)win.Content).UpdateLayout();
                var bar = win.ToolbarHost;
                double x = Canvas.GetLeft(bar), y = Canvas.GetTop(bar);
                var size = new Size(bar.ActualWidth, bar.ActualHeight);
                Assert.True(x >= 0 && y >= 0 && x + size.Width <= w + 0.5 && y + size.Height <= h + 0.5, $"{edge} {w}x{h}: {x},{y} {size}");
                switch (edge)
                {
                    case ScreenEdge.Top: Assert.True(y < 20, $"{edge}: y {y}"); break;
                    case ScreenEdge.Bottom: Assert.True(h - (y + size.Height) < 20, $"{edge}: bottom gap {h - (y + size.Height)}"); break;
                    case ScreenEdge.Left: Assert.True(x < 20, $"{edge}: x {x}"); break;
                    default: Assert.True(w - (x + size.Width) < 20, $"{edge}: right gap {w - (x + size.Width)}"); break;
                }

                win.Close();
            }
        });
    }

    [Fact]
    public void AutoHide_RevealsOnlyAfterDwellAtEdge_HidesAfterDelay_HoldKeepsIt()
    {
        var area = new Size(1000, 600);
        var bar = new Rect(300, 8, 400, 44);
        var a = new AutoHideLogic(ScreenEdge.Top);
        var t = TimeSpan.Zero;
        TimeSpan Ms(double ms) => t += TimeSpan.FromMilliseconds(ms);
        Assert.False(a.Update(new Point(500, 2), area, bar, Ms(0)));
        Assert.False(a.Update(new Point(500, 300), area, bar, Ms(60)));   // быстрый проход мимо края — не открывается
        Assert.False(a.Update(new Point(500, 2), area, bar, Ms(40)));
        Assert.False(a.Update(new Point(500, 3), area, bar, Ms(80)));
        Assert.True(a.Update(new Point(500, 3), area, bar, Ms(80)));      // задержка у края ≥ 140 мс
        Assert.True(a.Update(new Point(520, 70), area, bar, Ms(500)));    // рядом с панелью — не прячется
        Assert.True(a.Update(new Point(500, 400), area, bar, Ms(100)));
        Assert.True(a.Update(new Point(500, 400), area, bar, Ms(800), hold: true)); // меню или ввод текста удерживают панель
        Assert.True(a.Update(new Point(500, 400), area, bar, Ms(300)));
        Assert.True(a.Update(new Point(500, 400), area, bar, Ms(450)));   // после удержания отсчёт начат заново
        Assert.False(a.Update(new Point(500, 400), area, bar, Ms(300)));  // далеко дольше 700 мс — спряталась
        foreach (var edge in new[] { ScreenEdge.Bottom, ScreenEdge.Left, ScreenEdge.Right })
        {
            var e = new AutoHideLogic(edge);
            var at = edge switch { ScreenEdge.Bottom => new Point(500, 598), ScreenEdge.Left => new Point(1, 300), _ => new Point(999, 300) };
            Assert.False(e.Update(at, area, bar, TimeSpan.Zero), edge.ToString());
            Assert.True(e.Update(at, area, bar, TimeSpan.FromMilliseconds(200)), edge.ToString());
        }
    }

    [Fact]
    public void DockedToolbar_PositionFromProfile_StaysOnTheMonitor()
    {
        Sta.Run(() =>
        {
            foreach (var (fx, fy) in new[] { (0.25, 0.9), (1.0, 1.0), (0.0, 0.0) })
            {
                var win = Window(DrawingProfile.Default with { ToolbarX = fx, ToolbarY = fy });
                var bar = win.ToolbarHost;
                double x = Canvas.GetLeft(bar), y = Canvas.GetTop(bar);
                Assert.True(x >= 0 && y >= 0 && x + bar.ActualWidth <= 1100.5 && y + bar.ActualHeight <= 620.5, $"{fx},{fy}: {x},{y}");
                if (fy > 0.5)
                {
                    Assert.True(y > 300, $"{fx},{fy}: y {y}");
                }

                win.Close();
            }
        });
    }

    [Fact]
    public void TextTool_TypeMoveCommit_UndoRedo_CancelAndEmpty()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawingProfile.Default);
            w.SetSettings(w.Settings with { Tool = DrawTool.Text, TextSize = 40, Color = DrawingWindow.Palette[2] });
            w.BeginText(new Point(100, 100));
            Assert.True(w.IsEditingText);
            Assert.True(w.TextEditor!.AcceptsReturn); // многострочный ввод
            w.TypeForPreview("Hello" + Environment.NewLine + "world");
            w.MoveEditor(50, 20);                     // перемещение до подтверждения
            w.CommitText();
            Assert.False(w.IsEditingText);
            Assert.Equal(1, w.TextCount);
            var block = Descendants<TextBlock>(w.Content as DependencyObject).Single(b => b.Text.StartsWith("Hello", StringComparison.Ordinal));
            Assert.Contains("world", block.Text);
            Assert.Equal(40, block.FontSize);
            Assert.Equal(DrawingWindow.Palette[2].ToWpf(), ((SolidColorBrush)block.Foreground).Color);
            Assert.True(Canvas.GetLeft(block) > 120 && Canvas.GetTop(block) > 110, $"{Canvas.GetLeft(block)},{Canvas.GetTop(block)}");
            w.Undo();
            Assert.Equal(0, w.TextCount);
            w.Redo();
            Assert.Equal(1, w.TextCount);

            w.BeginText(new Point(300, 300));
            w.TypeForPreview("discard me");
            w.CancelText();                            // Esc — отменяет только текст
            Assert.Equal(1, w.TextCount);
            w.BeginText(new Point(300, 300));
            w.CommitText();                            // пустой текст — объекта нет
            Assert.Equal(1, w.TextCount);
            w.BeginText(new Point(10, 10));
            w.TypeForPreview("by tool switch");
            w.SetSettings(w.Settings with { Tool = DrawTool.Pen }); // смена инструмента подтверждает текст
            Assert.False(w.IsEditingText);
            Assert.Equal(2, w.TextCount);
            w.Close();
        });
    }

    private static PresentationController Controller(DrawingProfile profile, Func<BitmapSource, ScreenshotResult> deliver) =>
        new(() => LensSettings.Default, () => DrawSettings.Default, () => (new PixelRect(-32000, -32000, 900, 600), 1.0), profile: () => profile)
        {
            CaptureOverride = () => Task.FromResult<BitmapSource>(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8)),
            DeliverOverride = deliver,
        };

    private static T Run<T>(Task<T> task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.Result;
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    [Theory]
    [InlineData(AfterScreenshot.KeepDrawings)]
    [InlineData(AfterScreenshot.ClearDrawings)]
    [InlineData(AfterScreenshot.ExitDrawing)]
    public void Screenshot_AfterActions_AndFailureKeepsDrawings(AfterScreenshot after)
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                var ok = new ScreenshotResult(true, Path.Combine(Path.GetTempPath(), "Backpack_2026-10-29_10-00-00.png"), false, null);
                using var c = Controller(DrawingProfile.Default with { AfterShot = after }, _ => ok);
                Assert.Null(Run(c.TakeScreenshotAsync()));   // вне режима рисования снимок не делается
                c.Execute(PresentationCommand.Draw);
                c.Drawing!.Draw(new Point(10, 10), new Point(200, 100));
                Assert.True(Run(c.TakeScreenshotAsync())!.Ok);
                switch (after)
                {
                    case AfterScreenshot.KeepDrawings:
                        Assert.Equal(1, c.Drawing!.ShapeCount);
                        Assert.Contains("Backpack_2026-10-29_10-00-00.png", c.Drawing.ToastText);
                        break;
                    case AfterScreenshot.ClearDrawings:
                        Assert.Equal(0, c.Drawing!.ShapeCount);
                        Assert.True(c.State.Drawing);
                        break;
                    default:
                        Assert.Null(c.Drawing);
                        Assert.False(c.State.Drawing);
                        Assert.False(c.AnyWindowCapturesMouse);
                        Assert.Empty(c.OpenWindows);
                        break;
                }

                // Ошибка сохранения: рисунки остаются при любом варианте.
                using var failing = Controller(DrawingProfile.Default with { AfterShot = after }, _ => new ScreenshotResult(false, null, false, "IOException"));
                failing.Execute(PresentationCommand.Draw);
                failing.Drawing!.Draw(new Point(10, 10), new Point(200, 100));
                Assert.False(Run(failing.TakeScreenshotAsync())!.Ok);
                Assert.NotNull(failing.Drawing);
                Assert.Equal(1, failing.Drawing!.ShapeCount);
                Assert.True(failing.State.Drawing);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Fact]
    public void Screenshot_HeldKey_DoesNotCreateDuplicates()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                int delivered = 0;
                using var c = Controller(DrawingProfile.Default, _ =>
                {
                    delivered++;
                    return new ScreenshotResult(true, null, true, null);
                });
                c.Execute(PresentationCommand.Draw);
                Assert.NotNull(Run(c.TakeScreenshotAsync()));
                for (int i = 0; i < 5; i++)
                {
                    Assert.Null(Run(c.TakeScreenshotAsync()));  // автоповтор удерживаемой клавиши
                }

                Assert.Equal(1, delivered);
                Assert.Equal(1, c.ScreenshotsTaken);
                Assert.Equal(5, c.ScreenshotsIgnored);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Fact]
    public void ScreenshotSaver_WritesPng_UniqueNames_AndReportsFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "ko-3b-" + Guid.NewGuid().ToString("N")[..8]);
        string dir = Path.Combine(root, "shots with space");
        try
        {
            var img = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 3 * 4], 16);
            img.Freeze();
            var now = new DateTime(2026, 10, 29, 10, 5, 7);
            bool copied = false;
            var a = ScreenshotSaver.Deliver(img, ScreenshotAction.SaveAndCopy, dir, now, _ => copied = true);
            var b = ScreenshotSaver.Deliver(img, ScreenshotAction.Save, dir, now);
            Assert.True(a.Ok && b.Ok && copied && a.Copied);
            Assert.Equal("Backpack_2026-10-29_10-05-07.png", Path.GetFileName(a.FilePath));
            Assert.Equal("Backpack_2026-10-29_10-05-07_2.png", Path.GetFileName(b.FilePath));
            using (var fs = File.OpenRead(a.FilePath!))
            {
                var decoder = new PngBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(4, decoder.Frames[0].PixelWidth);
                Assert.Equal(3, decoder.Frames[0].PixelHeight);
            }

            var copyOnly = ScreenshotSaver.Deliver(img, ScreenshotAction.Copy, dir, now, _ => { });
            Assert.True(copyOnly.Ok && copyOnly.FilePath is null && copyOnly.Copied);
            string blocked = Path.Combine(dir, "blocked");
            File.WriteAllText(blocked, "");
            var fail = ScreenshotSaver.Deliver(img, ScreenshotAction.Save, blocked, now); // «папка» — это файл
            Assert.False(fail.Ok);
            Assert.NotNull(fail.Error);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RadialOnly_EverythingReachable_RightClickOpensMenu_EscAndExitClose()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                Assert.Equal(Enum.GetValues<RadialItem>().Length, RadialMenu.Order.Distinct().Count());

                int shots = 0;
                using var c = Controller(DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly }, _ =>
                {
                    shots++;
                    return new ScreenshotResult(true, null, true, null);
                });
                c.Execute(PresentationCommand.Draw);
                var d = c.Drawing!;
                Assert.False(d.ToolbarShown);                       // панели нет
                d.RaiseEvent(new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent });
                Assert.True(c.State.Radial && d.RadialOpen);        // правая кнопка — меню
                d.Radial!.Choose(RadialItem.Text);
                Assert.Equal(DrawTool.Text, d.Settings.Tool);
                Assert.False(c.State.Radial);
                d.BeginText(new Point(50, 50));
                d.TypeForPreview("note");
                c.Execute(PresentationCommand.Radial);              // открытие меню подтверждает текст
                Assert.Equal(1, d.TextCount);
                d.Radial.Choose(RadialItem.Undo);
                Assert.Equal(0, d.TextCount);
                c.Execute(PresentationCommand.Radial);
                d.Radial.Choose(RadialItem.Redo);
                Assert.Equal(1, d.TextCount);
                c.Execute(PresentationCommand.Radial);
                d.Radial.Choose(RadialItem.Screenshot);
                Pump(150);
                Assert.Equal(1, shots);
                Assert.False(c.State.Radial);
                Assert.Equal(1, d.TextCount);                       // KeepDrawings по умолчанию

                // Esc при вводе текста отменяет только текст, режим остаётся.
                d.BeginText(new Point(80, 80));
                c.Escape();
                Assert.False(d.IsEditingText);
                Assert.True(c.State.Drawing);
                Pump(250);
                c.Execute(PresentationCommand.Radial);
                d.Radial.Choose(RadialItem.Exit);
                Assert.Null(c.Drawing);
                Assert.False(c.AnyWindowCapturesMouse);
                Assert.Empty(c.OpenWindows);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    private sealed class FakeApi : IHotkeyApi
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

    [Fact]
    public void ScreenshotHotkey_F2_RegisteredOnlyWhileDrawing()
    {
        var api = new FakeApi();
        using var m = new HotkeyManager(api, createWindow: false);
        m.Apply(HotkeySettings.Default);
        Assert.Equal(HotkeyStatus.WaitingForMode, m.StatusOf(HotkeyAction.Screenshot));
        Assert.DoesNotContain(api.Registered.Values, r => r.Vk == 0x71);            // вне рисования F2 остаётся другим программам
        m.SetDrawingActive(true);
        Assert.Equal(HotkeyStatus.Registered, m.StatusOf(HotkeyAction.Screenshot));
        Assert.Contains(api.Registered.Values, r => r.Vk == 0x71 && (r.Mods & 0x4000) != 0); // MOD_NOREPEAT
        m.SetDrawingActive(false);
        Assert.DoesNotContain(api.Registered.Values, r => r.Vk == 0x71);
        Assert.Equal(HotkeyStatus.WaitingForMode, m.StatusOf(HotkeyAction.Screenshot));
        Assert.False(HotkeyRules.IsAcceptable(HotkeySettings.DefaultScreenshot, HotkeyAction.ToggleDisplay)); // одиночная F2 — только для режима
        Assert.True(HotkeyRules.IsAcceptable(HotkeySettings.DefaultScreenshot, HotkeyAction.Screenshot));
        Assert.False(HotkeyRules.IsAcceptable(new Hotkey(HotkeyModifiers.None, 'A'), HotkeyAction.Screenshot));
    }

    [Fact]
    public void SettingsFormat3_MigratesToFour_DrawingProfileAndThresholdRoundTrip()
    {
        var v3 = SettingsSerializer.ToJson(LabSettings.Default);
        v3["formatVersion"] = 3;
        v3.Remove("drawing");
        ((JsonObject)v3["keys"]!).Remove("anyHeldMs");
        var r = SettingsSerializer.Read(v3.ToJsonString());
        Assert.Equal(3, r.MigratedFrom);
        Assert.Empty(r.Problems);
        Assert.Equal(DrawingProfile.Default, r.Settings.DrawingOrDefault);
        Assert.Equal(150, r.Settings.Keys.AnyHeldMs);

        var full = LabSettings.Default with
        {
            Keys = LabSettings.Default.Keys with { Chords = ChordMode.AnyHeld, AnyHeldMs = 220 },
            Drawing = new DrawingProfile(DrawToolbarMode.AutoHide, ScreenEdge.Right, 0.3, 0.7, ScreenshotAction.SaveAndCopy, AfterScreenshot.ExitDrawing),
        };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(full));
        Assert.Empty(back.Problems);
        Assert.Equal(full, back.Settings);
        var profile = ProfileSerializer.Read(ProfileSerializer.Write(new Profile("p-draw000001", "D", full)));
        Assert.True(profile.Ok);
        Assert.Equal(full.DrawingOrDefault, profile.Profile!.Settings.DrawingOrDefault);
        Assert.Equal(220, profile.Profile.Settings.Keys.AnyHeldMs);
    }

    [Fact]
    public void Navigation_HasLensAndDrawing_NoDuplicates_HotkeysPageHasOnlyHotkeys_HelpHasDiagnostics()
    {
        Sta.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "ko-3b-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
                var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
                var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => "", () => { }, () => { }), SettingsSection.Home, prefs: prefs);
                var sections = w.NavItems.Select(n => (SettingsSection)n.Tag).ToList();
                Assert.Equal(sections.Count, sections.Distinct().Count());
                Assert.DoesNotContain(SettingsSection.Lens, sections); // Phase 4G: «Фокус» и линза — плагины
                Assert.DoesNotContain(SettingsSection.Drawing, sections); // Phase 4H: рисование — плагин (страница плагина)
                w.Show(SettingsSection.Hotkeys);
                Assert.Empty(Descendants<NumberSlider>(w.Cards));   // параметры инструментов — на их страницах
                w.Show(SettingsSection.Help);
                var texts = Descendants<TextBlock>(w.Cards).Select(t => t.Text).ToList();
                Assert.Contains(Loc.T("HelpSection.Diag.Title"), texts);
                Assert.Contains(Loc.T("HelpSection.Diagnostics"), texts);
                foreach (var section in sections)
                {
                    w.Show(section);
                    foreach (var bar in Descendants<ButtonBar>(w.Cards))
                    {
                        Assert.All(bar.Children.OfType<FrameworkElement>(), b => Assert.Equal(new Thickness(0, 0, ButtonBar.Gap, ButtonBar.Gap), b.Margin));
                    }
                }

                w.Close();
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject? root)
        where T : DependencyObject
    {
        if (root is null)
        {
            yield break;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(child))
            {
                yield return d;
            }
        }
    }
}
