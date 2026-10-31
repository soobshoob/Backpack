using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyOverlay.Visual.Brand;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Layout;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Mouse;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Profiles;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Styles;
using KeyOverlay.Visual.Tray;
using MouseButtonState = KeyOverlay.Visual.Mouse.MouseButtonState;

namespace KeyOverlay.Lab.App;

/// <summary>
/// Снимки для справки (--render-help-images=папка): настоящие окна KeyOverlay на примерных данных во временной папке,
/// отрисованные в память (RenderTargetBitmap) из собственных элементов WPF. Экран, рабочий стол и чужие окна не снимаются;
/// окна выводятся за пределами видимой области и не активируются. Захват ввода в этом режиме не запускается.
/// </summary>
internal static class HelpImages
{
    private const double Scale = 1.25;

    private static readonly (string Code, UiLanguage Language, string Main, string Lesson, string Stream)[] Languages =
    [
        ("uk", UiLanguage.Ukrainian, "Основний", "Запис уроку", "Стрим"),
        ("en", UiLanguage.English, "Main", "Lesson recording", "Stream"),
        ("ru", UiLanguage.Russian, "Основной", "Запись урока", "Стрим"),
    ];

    public static int Run(string outDir)
    {
        string work = Path.Combine(Path.GetTempPath(), "keyoverlay-help-images-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Снимки — непрозрачный материал (подложка DWM в растр не попадает) и вариант для каждой темы окна.
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque, Glow: true));
            RenderCommon(Path.Combine(outDir, "common"), dark: false);
            RenderCommon(Path.Combine(outDir, "common"), dark: true);
            foreach (var lang in Languages)
            {
                RenderLanguage(Path.Combine(outDir, lang.Code), Path.Combine(work, lang.Code), lang);
            }

            return 0;
        }
        finally
        {
            UiTheme.StopListening();
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }

    private static void RenderLanguage(string dir, string work, (string Code, UiLanguage Language, string Main, string Lesson, string Stream) lang)
    {
        Directory.CreateDirectory(dir);
        Loc.Set(lang.Language);
        UiTheme.Set(UiThemeMode.Light);

        var store = SettingsStore.Load(Path.Combine(work, "settings.json"));
        var prefs = AppPreferencesStore.Load(Path.Combine(work, "app.json"));
        prefs.Update(p => p with { Language = lang.Language, Theme = UiThemeMode.Light, Material = new AppearanceMaterial(WindowMaterial.Opaque) });
        var styles = new StyleStore(Path.Combine(work, "styles"));
        var session = new ProfileSession(new ProfileStore(Path.Combine(work, "profiles"), styles), store, prefs, () => SystemParameters.WorkArea, lang.Main);
        // Горячие клавиши в снимке не регистрируются в системе (без глобального перехвата): подменённый вызов Win32.
        using var hotkeys = new HotkeyManager(new NoRegistration(), createWindow: false);
        hotkeys.Apply(prefs.Current.HotkeysOrDefault);
        var about = AppInfo.FromAssembly(typeof(HelpImages).Assembly) with { Channel = "Preview", ReleaseDate = null };
        var d = LabSettings.Default;
        session.Store.Create(lang.Lesson, d with
        {
            Appearance = d.Appearance with { Size = 40 },
            MouseCursor = d.MouseCursor with { Enabled = true },
        });
        session.Store.Create(lang.Stream, d with { Keys = d.Keys with { Filter = KeyFilter.CombosOnly } });
        store.Update(s => s with { Appearance = s.Appearance with { Size = 34 } }); // есть несохранённые изменения

        // Phase 4G: страницы плагинов — настоящие поставляемые пакеты (папка Plugins рядом с программой или
        // BACKPACK_BUNDLED_PLUGINS), только импорт: код плагинов не запускается, доверие не подтверждается.
        var commands = new KeyOverlay.Visual.Commands.CommandRegistry();
        var pluginStore = new KeyOverlay.Visual.Plugins.PluginStore(Path.Combine(work, "plugins"))
        {
            ProfileLoad = id => store.Current.PluginsOrEmpty.Get(id),
            ProfileSave = (id, values) => store.Update(x => x with { Plugins = x.PluginsOrEmpty.With(id, values) }),
        };
        using var plugins = new KeyOverlay.Visual.Plugins.PluginManager(pluginStore, commands, Dispatcher.CurrentDispatcher, "Backpack.exe", () => Loc.Code, safeMode: false);
        // Phase 4H: как при запуске — проверенные пакеты индекса поставки, с отметкой «От создателя».
        foreach (var e in KeyOverlay.Visual.Plugins.BundledIndex.Load().Verified)
        {
            plugins.Import(e.Path, e.Sha256);
        }

        var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => Loc.F("LayoutStatus.Unchanged", 3), () => { }, () => { },
                DisplayVisible: () => true, SetDisplayVisible: _ => { }, Commands: commands, Plugins: plugins),
            SettingsSection.Appearance, session, prefs, styles, hotkeys, about, new KeyOverlay.Visual.Updates.UpdateChecker(null))
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000,
            Width = 1280,
            Height = 820,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        w.Show();
        try
        {
            Pump(900);
            foreach (var (mode, suffix) in new[] { (UiThemeMode.Light, ""), (UiThemeMode.Dark, ".dark") })
            {
                prefs.Update(p => p with { Theme = mode });
                UiTheme.Set(mode);
                Pump(200);
                w.Show(SettingsSection.Appearance);
                Pump(500);
                Save(RenderWindow(w, w.Landmarks()), Path.Combine(dir, $"settings-overview{suffix}.png"));
                Shot(w, SettingsSection.Keys, 1300, Path.Combine(dir, $"settings-keys{suffix}.png"));
                Shot(w, SettingsSection.Mouse, 800, Path.Combine(dir, $"settings-mouse{suffix}.png"));
                Shot(w, SettingsSection.Layout, 300, Path.Combine(dir, $"settings-layout{suffix}.png"));
                Shot(w, SettingsSection.Position, 300, Path.Combine(dir, $"settings-position{suffix}.png"));
                Shot(w, SettingsSection.Profiles, 300, Path.Combine(dir, $"settings-profiles{suffix}.png"));
                Shot(w, SettingsSection.Home, 300, Path.Combine(dir, $"settings-home{suffix}.png"));
                Shot(w, SettingsSection.Appearance, 600, Path.Combine(dir, $"settings-styles{suffix}.png"));
                Shot(w, SettingsSection.Hotkeys, 300, Path.Combine(dir, $"settings-hotkeys{suffix}.png"));

                Shot(w, SettingsSection.Interface, 300, Path.Combine(dir, $"settings-interface{suffix}.png"));
                Shot(w, SettingsSection.Privacy, 900, Path.Combine(dir, $"settings-privacy{suffix}.png"));
                Shot(w, SettingsSection.Application, 300, Path.Combine(dir, $"settings-application{suffix}.png"));
                Shot(w, SettingsSection.About, 300, Path.Combine(dir, $"settings-about{suffix}.png"));
                Shot(w, SettingsSection.Plugins, 400, Path.Combine(dir, $"settings-plugins{suffix}.png"));
                foreach (var (id, file) in new[] { ("com.example.focus", "settings-plugin-focus"), ("com.example.cursor-zoom", "settings-plugin-cursor-zoom"), ("com.example.drawing", "settings-drawing") })
                {
                    if (pluginStore.Get(id) is not null)
                    {
                        w.ShowPlugin(id);
                        Pump(900);
                        Save(RenderWindow(w, []), Path.Combine(dir, file + suffix + ".png"));
                    }
                }
            }

            prefs.Update(p => p with { Theme = UiThemeMode.Light });
            UiTheme.Set(UiThemeMode.Light);

            var menu = TrayMenu.Build(store, session, new TrayCommands(() => { }, () => { }, () => true, _ => { }, () => { }, () => { }));
            Save(RenderDetached(menu), Path.Combine(dir, "tray-menu.png"));

        }
        finally
        {
            w.Close();
            session.Dispose();
        }
    }

    private static void Shot(SettingsWindow w, SettingsSection section, int pumpMs, string path)
    {
        w.Show(section);
        Pump(pumpMs);
        Save(RenderWindow(w, []), path);
    }

    /// <summary>
    /// Нейтральные примеры без текста интерфейса: кейкапы, мышь у курсора, индикатор, панель и меню рисования. Фон — сцена в
    /// цветах темы окна (<paramref name="dark"/> — файлы <c>*.dark.png</c>); цвета стилей кейкапов и мыши не меняются.
    /// </summary>
    private static void RenderCommon(string dir, bool dark)
    {
        Directory.CreateDirectory(dir);
        UiTheme.Set(dark ? UiThemeMode.Dark : UiThemeMode.Light);
        string sfx = dark ? ".dark" : "";
        var a = AppearanceSettings.Default;

        Save(Stage(Row(a, TrailMode.None, Typing("H", "E", "L"))), Path.Combine(dir, $"trail-none{sfx}.png"));
        Save(Stage(Row(a, TrailMode.FadeInPlace, Typing("H", "E", "L"))), Path.Combine(dir, $"trail-fade{sfx}.png"));
        Save(Stage(Row(a, TrailMode.None, [[Mod(1, 1, "Ctrl"), Mod(2, 2, "Shift"), Key(3, 3, "K", 2)]])), Path.Combine(dir, $"combo-chord{sfx}.png"));
        Save(Stage(Row(a, TrailMode.None, [[Key(4, 4, "A")]])), Path.Combine(dir, $"combo-letter{sfx}.png"));
        foreach (var (set, name) in new[] { (LabelSet.Windows, "windows"), (LabelSet.MacOS, "macos"), (LabelSet.Linux, "linux") })
        {
            var look = a with { LabelSet = set };
            Save(Stage(Row(look, TrailMode.None, [[Mod(1, 1, "Ctrl"), Mod(2, 2, "Alt"), Mod(3, 3, "Win", KeySide.Right), Key(4, 4, "Enter"), Key(5, 5, "Backspace")]])),
                Path.Combine(dir, $"labels-{name}{sfx}.png"));
        }

        var mouse = new MouseCursorVisual();
        var s = LabSettings.Default;
        mouse.Apply(s.MouseCursor with { Enabled = true }, s.Appearance.MouseLook, new MouseButtonState(MouseButtons.Left, MouseButtons.None));
        var canvas = new Canvas { Width = 220, Height = 160 };
        var pointer = SettingsWindow.PreviewPointer();
        canvas.Children.Add(mouse);
        canvas.Children.Add(pointer);
        Canvas.SetLeft(pointer, 80);
        Canvas.SetTop(pointer, 50);
        Canvas.SetLeft(mouse, 80 - mouse.Origin.X);
        Canvas.SetTop(mouse, 50 - mouse.Origin.Y);
        Save(Stage(canvas), Path.Combine(dir, $"mouse-cursor{sfx}.png"));

        var indicator = new LayoutIndicatorVisual();
        indicator.Set(new LayoutLabel("UK", null), IndicatorLook.Resolve(s.Appearance, s.LayoutIndicator));
        Save(Stage(indicator), Path.Combine(dir, $"layout-indicator{sfx}.png"));

        // Рендеры кейкапов: по одному встроенному стилю каждого рендера, нажата последняя клавиша.
        var renderers = new StackPanel();
        foreach (var id in new[] { "keyoverlay.lowprofile.classic", "keyoverlay.laptop.silver", "keyoverlay.pbt.ocean", "keyoverlay.minimal.light", "keyoverlay.volumetric.lavender" })
        {
            var look = StyleOps.ApplyKeycap(a with { Size = 28 }, BuiltInStyles.Find(id)!.Keycap!);
            var row = Row(look, TrailMode.None, [[Mod(1, 1, "Ctrl"), Mod(2, 2, "Shift"), Key(3, 3, "K")]]);
            row.Margin = new Thickness(0, 6, 0, 6);
            row.HorizontalAlignment = HorizontalAlignment.Left;
            renderers.Children.Add(row);
        }

        Save(Stage(renderers), Path.Combine(dir, $"keycap-renderers{sfx}.png"));

        // Семейства мыши: левая кнопка, прокрутка вверх, боковая кнопка.
        var mice = new StackPanel();
        foreach (var id in new[] { "keyoverlay.mouse.outline-light", "keyoverlay.mouse.solid-light", "keyoverlay.mouse.retro-bluegrey", "keyoverlay.mouse.sketch-ink" })
        {
            var m = BuiltInStyles.Find(id)!.Mouse!;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            foreach (var st in new[] { new MouseIconState(MouseButtons.Left, MouseButtons.None, 0, 0), new MouseIconState(MouseButtons.None, MouseButtons.None, 1, 0), new MouseIconState(MouseButtons.X1, MouseButtons.None, 0, 0) })
            {
                line.Children.Add(new Border { Background = m.IconBackground.ToFrozenBrush(), CornerRadius = new CornerRadius(14), Padding = new Thickness(7), Margin = new Thickness(5), Child = MouseIcon.Create(46, m, st) });
            }

            mice.Children.Add(line);
        }

        Save(Stage(mice), Path.Combine(dir, $"mouse-styles{sfx}.png"));

        // Панель рисования и радиальное меню — те же элементы, что в режиме рисования (окно не показывается).
        Save(Stage(DrawingWindow.PreviewToolbar(DrawSettings.Default, DrawingProfile.Default)), Path.Combine(dir, $"draw-toolbar{sfx}.png"));
        var radial = new RadialMenu();
        Save(Stage(radial), Path.Combine(dir, $"radial-menu{sfx}.png"));

        // Phase 4E: вложенное кольцо, компактная панель, предварительное распознавание, дополнительная подпись раскладки.
        var nested = new RadialMenu();
        nested.Activate("cat.shapes");
        Save(Stage(nested), Path.Combine(dir, $"radial-menu-nested{sfx}.png"));
        Save(Stage(DrawingWindow.PreviewToolbar(DrawSettings.Default with { Tool = DrawTool.Polygon }, DrawingProfile.Default with { Compact = true })), Path.Combine(dir, $"draw-toolbar-compact{sfx}.png"));
        Save(Stage(DrawingWindow.PreviewSmartPen()), Path.Combine(dir, $"smart-pen-preview{sfx}.png"));
        Save(Stage(Row(a with { Size = 40 }, TrailMode.None, [[Key(1, 1, "Q") with { Secondary = "Й" }, Key(2, 2, "A") with { Secondary = "Ф" }, Key(3, 3, "[") with { Secondary = "Х" }, Key(4, 4, "1")]])),
            Path.Combine(dir, $"secondary-labels{sfx}.png"));
    }

    private sealed class NoRegistration : IHotkeyApi
    {
        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            return true;
        }

        public bool Unregister(nint hwnd, int id) => true;
    }

    private static KeycapItem Mod(long id, int key, string name, KeySide side = KeySide.None) => new(id, key, name, true, true, side);

    private static KeycapItem Key(long id, int key, string name, int count = 1) => new(id, key, name, false, true, PressCount: count);

    /// <summary>Быстрая печать: каждое нажатие — новая клавиша, предыдущая отпущена.</summary>
    private static List<KeycapItem[]> Typing(params string[] letters)
    {
        var steps = new List<KeycapItem[]>();
        for (int i = 0; i < letters.Length; i++)
        {
            steps.Add([new KeycapItem(10 + i, 100 + i, letters[i], false, true)]);
            steps.Add([new KeycapItem(10 + i, 100 + i, letters[i], false, false)]);
        }

        steps.RemoveAt(steps.Count - 1); // последняя буква ещё нажата
        return steps;
    }

    /// <summary>Ряд кейкапов тем же презентером, что и оверлей; анимации доводятся по ручным часам до кадра «после последнего шага».</summary>
    private static FrameworkElement Row(AppearanceSettings look, TrailMode trail, IReadOnlyList<KeycapItem[]> steps)
    {
        var clock = new ManualClock();
        var presenter = new OverlayPresenter(clock, useRenderingLoop: false);
        presenter.Apply(look, new RowOptions(trail, MouseLabelMode.Graphic, HorizontalAlignment.Center));
        foreach (var step in steps)
        {
            presenter.Update(step);
            for (int i = 0; i < 3; i++)
            {
                clock.Now += TimeSpan.FromMilliseconds(20);
                presenter.Tick();
            }
        }

        // Кадр вскоре после последнего нажатия: появление закончено, предыдущие буквы (в режиме следа) ещё гаснут.
        for (int i = 0; i < 2; i++)
        {
            clock.Now += TimeSpan.FromMilliseconds(20);
            presenter.Tick();
        }

        return presenter.Root;
    }

    /// <summary>Подложка (не рабочий стол): сцена в цветах текущей темы окна, как у превью в приложении.</summary>
    private static BitmapSource Stage(FrameworkElement content)
    {
        var host = new Border { Padding = new Thickness(28), Child = content, Background = UiTheme.Brush("Ko.StageBrush") };
        host.Resources.MergedDictionaries.Add(UiTheme.Resources);
        return RenderDetached(host);
    }

    private static BitmapSource RenderDetached(FrameworkElement e)
    {
        e.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        e.Arrange(new Rect(e.DesiredSize));
        e.UpdateLayout();
        return Render(e, e.ActualWidth, e.ActualHeight, null, []);
    }

    private static BitmapSource RenderWindow(Window w, IEnumerable<(int Number, FrameworkElement Element)> marks)
    {
        var root = (FrameworkElement)w.Content;
        root.UpdateLayout();
        HideLocalPaths(root);
        root.UpdateLayout();
        return Render(root, root.ActualWidth, root.ActualHeight, w.Background, marks, root.Margin);
    }

    /// <summary>
    /// Снимки публикуются: локальные пути (временная папка снимков, профиль пользователя с его именем) заменяются
    /// обезличенными примерами до отрисовки.
    /// </summary>
    private static void HideLocalPaths(DependencyObject root)
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string Clean(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[A-Za-z]:\\[^\s""]*?keyoverlay-help-images-[0-9a-f]{8}\\(uk|en|ru|common)", @"%APPDATA%\Backpack");
            return profile.Length > 3 ? s.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase) : s;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            switch (child)
            {
                case TextBlock tb when Clean(tb.Text) is var t && t != tb.Text:
                    tb.Text = t;
                    break;
                case TextBox box when Clean(box.Text) is var t && t != box.Text:
                    box.Text = t;
                    break;
            }

            HideLocalPaths(child);
        }
    }

    private static BitmapSource Render(FrameworkElement root, double contentWidth, double contentHeight, Brush? background,
        IEnumerable<(int Number, FrameworkElement Element)> marks, Thickness margin = default)
    {
        double width = contentWidth + margin.Left + margin.Right, height = contentHeight + margin.Top + margin.Bottom;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            if (background is not null)
            {
                dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            }

            dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null,
                new Rect(margin.Left, margin.Top, contentWidth, contentHeight));
            foreach (var (n, e) in marks)
            {
                if (!e.IsVisible)
                {
                    continue;
                }

                var p = e.TranslatePoint(new Point(0, 0), root) + new Vector(margin.Left, margin.Top);
                Badge(dc, n, new Point(Math.Clamp(p.X - 15, 14, width - 14), Math.Clamp(p.Y + 2, 14, height - 14)));
            }
        }

        var bmp = new RenderTargetBitmap((int)Math.Ceiling(width * Scale), (int)Math.Ceiling(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Номер-выноска: синий круг с белой цифрой и светлой каймой (виден на светлом и тёмном фоне).</summary>
    private static void Badge(DrawingContext dc, int number, Point center)
    {
        var fill = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xEB));
        dc.DrawEllipse(fill, new Pen(Brushes.White, 2.5), center, 13, 13);
        var text = new FormattedText(number.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 15, Brushes.White, 1.0);
        dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
    }

    private static void Save(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(path);
        enc.Save(f);
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            frame.Continue = false;
        };
        t.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class ManualClock : IAnimationClock
    {
        public TimeSpan Now { get; set; }
    }
}
