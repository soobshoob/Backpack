using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Backpack.Visual.Help;
using Backpack.Visual.Layout;
using Backpack.Visual.Localization;
using Backpack.Visual.Profiles;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Tray;

namespace Backpack.Visual.Tests;

/// <summary>Phase 2D: локализация, тема интерфейса, трей, профили, повторное открытие окон и подписки.</summary>
public partial class Phase2DTests
{
    private static readonly UiLanguage[] Languages = [UiLanguage.Ukrainian, UiLanguage.English, UiLanguage.Russian];

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ko-2d-" + Guid.NewGuid().ToString("N")[..10]);

    private sealed record Env(string Dir, SettingsStore Store, AppPreferencesStore Prefs, ProfileSession Session) : IDisposable
    {
        public void Dispose() => Session.Dispose();
    }

    private static Env NewEnv(Rect? workArea = null)
    {
        string dir = TempDir();
        var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
        var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
        var session = new ProfileSession(new ProfileStore(Path.Combine(dir, "profiles")), store, prefs, () => workArea ?? new Rect(0, 0, 1920, 1040), "Main");
        return new Env(dir, store, prefs, session);
    }

    private static SettingsWindowActions Actions => new(() => { }, () => { }, () => "", () => { }, () => { });

    // ── локализация ──────────────────────────────────────────────────────────

    private static Dictionary<string, string> Resx(string suffix) =>
        XDocument.Load(Repo.PathOf("lab", "src", "Backpack.Visual", "Localization", $"Strings{suffix}.resx"))
            .Root!.Elements("data").ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Element("value")!);

    /// <summary>Каждый ключ, который код запрашивает у Loc (включая составные), есть во всех трёх языках, непустой, с теми же подстановками.</summary>
    [Fact]
    public void Translations_AreComplete_ForEveryKeyUsedInCode()
    {
        var en = Resx("");
        var tables = Loc.Supported.Select(s => (s.Code, Table: s.Code == "en" ? en : Resx("." + s.Code))).ToList();
        foreach (var (code, table) in tables)
        {
            Assert.True(en.Keys.Order().SequenceEqual(table.Keys.Order()), $"{code}: набор ключей отличается от английского");
        }

        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Repo.PathOf("lab", "src"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in LocCall().Matches(File.ReadAllText(file)))
            {
                used.Add(m.Groups["key"].Value);
            }
        }

        // Ключи, собираемые из стабильных идентификаторов (перечисления и имена примеров).
        used.UnionWith(Enum.GetNames<ScreenAnchor>().Select(a => "Anchor." + a));
        used.UnionWith(Enum.GetNames<LayoutOutcome>().Select(o => "Outcome." + o));
        used.UnionWith(Enum.GetNames<Backpack.Visual.Hotkeys.HotkeyAction>().Select(a => "Hotkeys." + a));
        used.UnionWith(Enum.GetNames<Backpack.Visual.Hotkeys.HotkeyStatus>().Select(a => "Hotkeys.Status." + a));
        used.UnionWith(Enum.GetNames<KeycapRenderer>().Select(a => "Renderer." + a));
        used.UnionWith(Enum.GetNames<MouseRenderer>().Select(a => "MouseRenderer." + a));
        used.UnionWith(Enum.GetNames<Backpack.Visual.Updates.UpdateState>().Where(a => a is not ("Available" or "NoSuitablePackage")).Select(a => "Update." + a));
        used.UnionWith(Enum.GetNames<Backpack.Visual.Settings.DrawTool>().Select(t => "Draw.Tool." + t).Append("Draw.NextColor"));
        used.UnionWith(Enum.GetNames<Backpack.Visual.Settings.DrawCategory>().Select(c => "Draw.Category." + c));
        used.UnionWith(new[] { "TrailNone", "TrailFade", "Combos", "Labels", "MouseCursor", "Layout", "Position" }.SelectMany(d => new[] { $"Demo.{d}.Title", $"Demo.{d}.Text" }));
        used.RemoveWhere(k => k.EndsWith('.')); // префиксы составных ключей («Anchor.» + значение)
        Assert.True(used.Count > 300, $"ключей в коде: {used.Count}");

        var missing = used.Where(k => !en.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "нет в ресурсах: " + string.Join(", ", missing));
        foreach (var key in en.Keys)
        {
            foreach (var (lang, table) in tables)
            {
                Assert.False(string.IsNullOrWhiteSpace(table[key]), $"{lang}: пустое значение {key}");
                Assert.True(Placeholders(en[key]).SetEquals(Placeholders(table[key])), $"{lang}: подстановки {key}");
                _ = string.Format(CultureInfo.InvariantCulture, table[key], Enumerable.Repeat<object>(1, 10).ToArray()); // корректный формат
            }
        }
    }

    /// <summary>Без смешения языков: в английских строках нет кириллицы, в украинских — русских букв, в русских — украинских.</summary>
    [Fact]
    public void Translations_DoNotMixLanguages()
    {
        foreach (var (key, v) in Resx(""))
        {
            Assert.False(Regex.IsMatch(v, "[А-Яа-яЁёІіЇїЄєҐґ]"), $"en {key}: {v}");
        }

        foreach (var (key, v) in Resx(".uk"))
        {
            Assert.False(Regex.IsMatch(v, "[ЫыЭэЁёЪъ]"), $"uk {key}: {v}");
        }

        foreach (var (key, v) in Resx(".ru"))
        {
            Assert.False(Regex.IsMatch(v, "[ІіЇїЄєҐґ]"), $"ru {key}: {v}");
        }

        // Языки сайта: без кириллицы; почти всё переведено (совпадают с английским только имена вроде Backpack, PBT, Mica).
        var en = Resx("");
        foreach (var code in Loc.Supported.Select(s => s.Code).Where(c => c is not ("en" or "uk" or "ru")))
        {
            var table = Resx("." + code);
            foreach (var (key, v) in table)
            {
                Assert.False(Regex.IsMatch(v, "[А-Яа-яЁёІіЇїЄєҐґ]"), $"{code} {key}: {v}");
            }

            int same = table.Count(p => p.Value == en[p.Key]);
            Assert.True(same < en.Count * 0.08, $"{code}: {same} строк совпадают с английскими");
        }

        Assert.True(Resx(".el").Count(p => Regex.IsMatch(p.Value, "[α-ωΑ-Ω]")) > en.Count * 0.9, "el: строки не на греческом");
    }

    [Theory]
    [InlineData("uk-UA", "uk")]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "de")]
    [InlineData("pl-PL", "pl")]
    [InlineData("pt-BR", "pt")]
    [InlineData("nn-NO", "nb")]
    [InlineData("ja-JP", "en")] // неподдерживаемый язык системы → английский
    [InlineData("tr-TR", "en")]
    public void SystemLanguage_FallsBackToEnglish(string system, string expected) =>
        Assert.Equal(expected, Loc.Resolve(UiLanguage.System, CultureInfo.GetCultureInfo(system)).TwoLetterISOLanguageName);

    [Fact]
    public void Loc_ReturnsEachLanguage_AndMarksMissingKeys()
    {
        Sta.Run(() =>
        {
            Loc.Set(UiLanguage.Ukrainian);
            Assert.Equal("Профілі", Loc.T("Settings.Nav.Profiles"));
            Loc.Set(UiLanguage.English);
            Assert.Equal("Profiles", Loc.T("Settings.Nav.Profiles"));
            Loc.Set(UiLanguage.Russian);
            Assert.Equal("Профили", Loc.T("Settings.Nav.Profiles"));
            Assert.Equal("[No.Such.Key]", Loc.T("No.Such.Key"));
            Assert.Equal("Профиль: «Стрим»", "Профиль: «" + "Стрим" + "»"); // контроль кодировки исходника
            Assert.Contains("1,5", new LocText("Settings.Problem.Range", "a", 1.5, 0, 1, 0.5).ToString());
            Loc.Set(UiLanguage.English);
            Assert.Contains("1.5", new LocText("Settings.Problem.Range", "a", 1.5, 0, 1, 0.5).ToString());
        });
    }

    [Fact]
    public void NumberInput_AcceptsCommaAndDot_InEveryLanguage()
    {
        Sta.Run(() =>
        {
            foreach (var lang in Languages)
            {
                Loc.Set(lang);
                double value = 0;
                var slider = new NumberSlider(UiTheme.Resources, 0.5, (0, 1), 0.01, "", v => value = v);
                foreach (var text in new[] { "0,25", "0.25" })
                {
                    slider.Box.Text = text;
                    slider.Commit();
                    Assert.Equal(0.25, value, 3);
                }

                Assert.Equal(lang == UiLanguage.English ? "0.25" : "0,25", slider.Box.Text);
            }
        });
    }

    // ── все разделы × языки × темы, падение 2C, повторное открытие ───────────

    /// <summary>
    /// Все переходы между разделами (каждый в каждый) на трёх языках и в двух темах, с профилями и общими настройками.
    /// Нет исключений, все тексты переведены (нет «[ключ]»), заголовок и навигация на текущем языке.
    /// </summary>
    [Fact]
    public void AllSections_AllTransitions_ThreeLanguages_TwoThemes()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            var w = new SettingsWindow(env.Store, Actions, SettingsSection.Appearance, env.Session, env.Prefs);
            // Phase 4F: отдельной страницы «Справка» нет — переход на неё открывает «Настройки приложения» (диагностика там).
            // Phase 4G: «Фокус» и «Увеличение у курсора» — плагины, своих страниц нет.
            // Phase 4H: «Рисование» — плагин, своей страницы нет.
            var sections = Enum.GetValues<SettingsSection>().Where(s => s is not SettingsSection.Plugins and not SettingsSection.Help and not SettingsSection.Lens and not SettingsSection.CursorZoom
                and not SettingsSection.Drawing).ToArray();
            w.Show(SettingsSection.Help);
            Assert.Equal(SettingsSection.Application, w.Section);
            // Phase 4B: подстраницы (стиль кейкапов, положение и мониторы, команды) — без своих пунктов; «Плагины» — без менеджера плагинов.
            Assert.Equal(sections.Count(s => SettingsWindow.NavSection(s) == s), w.NavItems.Count);
            foreach (var lang in Languages)
            {
                foreach (var theme in new[] { UiThemeMode.Light, UiThemeMode.Dark })
                {
                    Loc.Set(lang);
                    UiTheme.Set(theme);
                    foreach (var from in sections)
                    {
                        foreach (var to in sections)
                        {
                            w.Show(from);
                            w.Show(to);
                            Assert.Equal(to, w.Section);
                        }

                        Assert.Equal(Loc.T("Settings.WindowTitle"), w.Title);
                        Assert.Empty(MissingTexts(w.Content as DependencyObject));
                        var nav = SettingsWindow.NavSection(from);
                        Assert.Equal(Loc.T(NavKey(nav)), System.Windows.Automation.AutomationProperties.GetName(w.NavItems.Single(n => (SettingsSection)n.Tag == nav)));
                    }
                }
            }

            w.ReleaseResources();
            w.Close();
        });

        static string NavKey(SettingsSection s) => s switch
        {
            SettingsSection.Help => "Settings.Nav.HelpPage",
            SettingsSection.Lens => "Settings.Nav.Focus",
            SettingsSection.Layout => "Settings.Nav.IndicatorStyle", // Phase 4H: показ — плагин, страница — стиль индикатора
            _ => "Settings.Nav." + s,
        };
    }

    /// <summary>Падение 2C в окне с профилями и предпочтениями: «Внешний вид» → «Клавиши» много раз, после смены языка и темы.</summary>
    [Fact]
    public void KeysSection_AfterAppearance_WithProfiles_AndLanguageChange()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            var w = new SettingsWindow(env.Store, Actions, SettingsSection.Keys, env.Session, env.Prefs);
            for (int i = 0; i < 5; i++)
            {
                w.Show(SettingsSection.Appearance);
                Loc.Set(Languages[i % 3]);
                w.Show(SettingsSection.Keys);
                Assert.True(w.DemoRunning);
                Assert.Same(w.Preview.Root, FindParentViewbox(w.Preview.Root)?.Child);
            }

            w.ReleaseResources();
            w.Close();
        });

        static Viewbox? FindParentViewbox(FrameworkElement e) => e.Parent as Viewbox;
    }

    [Fact]
    public void SettingsWindow_Reopened_LeavesNoSubscriptions()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            Loc.Set(UiLanguage.English);
            UiTheme.Set(UiThemeMode.Light);
            int store0 = env.Store.SubscriberCount, loc0 = Loc.SubscriberCount, theme0 = UiTheme.SubscriberCount, prefs0 = env.Prefs.SubscriberCount;
            int render0 = OverlayPresenter.ActiveRenderingSubscriptions;
            for (int i = 0; i < 4; i++)
            {
                var w = new SettingsWindow(env.Store, Actions, (SettingsSection)(i * 2 % 8), env.Session, env.Prefs);
                Assert.True(Loc.SubscriberCount > loc0);
                Loc.Set(Languages[i % 3]);
                UiTheme.Set(i % 2 == 0 ? UiThemeMode.Dark : UiThemeMode.Light);
                w.Close(); // закрытие само освобождает ресурсы
                Assert.Equal(store0, env.Store.SubscriberCount);
                Assert.Equal(loc0, Loc.SubscriberCount);
                Assert.Equal(theme0, UiTheme.SubscriberCount);
                Assert.Equal(prefs0, env.Prefs.SubscriberCount);
                Assert.Equal(render0, OverlayPresenter.ActiveRenderingSubscriptions);
            }
        });
    }

    /// <summary>Настройки предыдущих версий (формат 1) открываются во всех разделах.</summary>
    [Fact]
    public void FormatOneSettings_OpenInEverySection()
    {
        Sta.Run(() =>
        {
            string dir = TempDir();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, SettingsTests.V1File);
            var store = SettingsStore.Load(path);
            Assert.Equal(1, store.MigratedFrom);
            var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
            using var session = new ProfileSession(new ProfileStore(Path.Combine(dir, "profiles")), store, prefs, () => new Rect(0, 0, 1920, 1040), "Main");
            var w = new SettingsWindow(store, Actions, SettingsSection.Appearance, session, prefs);
            foreach (var s in Enum.GetValues<SettingsSection>())
            {
                w.Show(s);
            }

            Assert.Equal(40, store.Current.Appearance.Size);
            w.Close();
        });
    }

    private static List<string> MissingTexts(DependencyObject? root)
    {
        var found = new List<string>();
        void Walk(object o)
        {
            switch (o)
            {
                case TextBlock t when t.Text.StartsWith('[') && t.Text.EndsWith(']'):
                    found.Add(t.Text);
                    break;
                case ContentControl { Content: string s } when s.StartsWith('[') && s.EndsWith(']'):
                    found.Add(s);
                    break;
            }

            if (o is DependencyObject d)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                {
                    Walk(child);
                }
            }
        }

        if (root is not null)
        {
            Walk(root);
        }

        return found;
    }

    // ── тема интерфейса ───────────────────────────────────────────────────────

    [Fact]
    public void UiTheme_ChangesWindowBrushes_ButNotTheOverlay()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            UiTheme.Set(UiThemeMode.Light);
            var w = new SettingsWindow(env.Store, Actions, SettingsSection.Appearance, env.Session, env.Prefs);
            var appearance = env.Store.Current.Appearance;
            var light = ((SolidColorBrush)UiTheme.Brush("Ko.WindowBrush")).Color;
            var keycapBefore = w.Preview.Settings;
            UiTheme.Set(UiThemeMode.Dark);
            var dark = ((SolidColorBrush)UiTheme.Brush("Ko.WindowBrush")).Color;
            Assert.True(light.R > 200 && dark.R < 60);
            Assert.True(UiTheme.IsDark);
            Assert.Same(keycapBefore, w.Preview.Settings);   // превью кейкапов не перекрашено
            Assert.Equal(appearance, env.Store.Current.Appearance); // настройки оверлея не изменены
            var bg = (SolidColorBrush)w.Background;
            Assert.Equal(dark, bg.Color);                       // открытое окно получило тему без перестройки
            w.Close();
        });
    }

    /// <summary>Контраст основных пар цветов темы (обычный, вторичный, ошибка, акцентная кнопка) — не ниже 4.5:1 в обеих темах.</summary>
    [Theory]
    [InlineData(UiThemeMode.Light)]
    [InlineData(UiThemeMode.Dark)]
    public void UiTheme_TextContrast_IsReadable(UiThemeMode mode)
    {
        Sta.Run(() =>
        {
            UiTheme.Set(mode);
            Color C(string k) => ((SolidColorBrush)UiTheme.Brush(k)).Color;
            foreach (var (fg, bg, min) in new[]
            {
                ("Ko.TextBrush", "Ko.WindowBrush", 7.0), ("Ko.TextBrush", "Ko.CardBrush", 7.0), ("Ko.SecondaryTextBrush", "Ko.CardBrush", 4.5),
                ("Ko.ErrorBrush", "Ko.CardBrush", 4.5), ("Ko.AccentTextBrush", "Ko.AccentBrush", 4.5), ("Ko.LinkBrush", "Ko.WindowBrush", 4.5),
                ("Ko.TextBrush", "Ko.SelectedBrush", 7.0), ("Ko.TextBrush", "Ko.HighlightBrush", 4.5), ("Ko.TextBrush", "Ko.ControlBrush", 7.0),
            })
            {
                double ratio = Contrast(C(fg), C(bg));
                Assert.True(ratio >= min, $"{mode}: {fg} на {bg} = {ratio:F2}");
            }

            // Неактивный текст отличим от фона, но заметно слабее обычного.
            double muted = Contrast(C("Ko.MutedTextBrush"), C("Ko.CardBrush"));
            Assert.True(muted is >= 2.5 and < 7, $"{mode}: muted {muted:F2}");
        });

        static double L(Color c)
        {
            static double Ch(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Ch(c.R)) + (0.7152 * Ch(c.G)) + (0.0722 * Ch(c.B));
        }

        static double Contrast(Color a, Color b)
        {
            double x = L(a), y = L(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
    }

    [Fact]
    public void Dialogs_FollowLanguageAndTheme()
    {
        Sta.Run(() =>
        {
            Loc.Set(UiLanguage.Ukrainian);
            UiTheme.Set(UiThemeMode.Dark);
            var d = KoDialog.Create(Loc.T("Profiles.Delete.Title"), Loc.F("Profiles.Delete.Message", "Стрим"),
                [new("delete", Loc.T("Profiles.Delete"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
            Assert.Equal("Видалити профіль", d.Title);
            Assert.Contains(d.Resources.MergedDictionaries, r => ReferenceEquals(r, UiTheme.Resources));
            Assert.Equal("cancel", d.Result); // закрытие без выбора = отмена
            d.Close();
        });
    }

    // ── трей ─────────────────────────────────────────────────────────────────

    private static TrayCommands Commands(Func<bool> visible, Action<bool> setVisible) => new(() => { }, () => { }, visible, setVisible, () => { }, () => { });

    private static void Click(MenuItem item)
    {
        if (item.IsCheckable)
        {
            item.IsChecked = !item.IsChecked; // как при щелчке пользователя
        }

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    [Fact]
    public void TrayMenu_ReflectsState_AndTogglesSyncWithOpenSettingsWindow()
    {
        Sta.Run(() =>
        {
            Loc.Set(UiLanguage.English);
            using var env = NewEnv();
            bool overlay = true;
            var w = new SettingsWindow(env.Store, Actions, SettingsSection.Keys, env.Session, env.Prefs);
            var menu = TrayMenu.Build(env.Store, env.Session, Commands(() => overlay, v => overlay = v));
            Assert.True(TrayMenu.Find(menu, "tray.overlay")!.IsChecked);
            Assert.False(TrayMenu.Find(menu, "tray.combos")!.IsChecked);
            Assert.Null(TrayMenu.Find(menu, "tray.layout")); // Phase 4H: индикатор раскладки — плагин (его команды в подменю «Плагины»)

            Click(TrayMenu.Find(menu, "tray.combos")!);
            Assert.Equal(KeyFilter.CombosOnly, env.Store.Current.Keys.Filter);
            // Открытое окно настроек перестроило раздел и показывает новое значение.
            Assert.True(SegmentChecked(w, Loc.T("Keys.Filter.Combos")));
            Assert.True(env.Session.Flush());     // изменение из трея сохранено в текущий профиль (автосохранение)
            Assert.Equal(KeyFilter.CombosOnly, env.Session.Store.Load(env.Session.Active!.Id).Profile!.Settings.Keys.Filter);

            Click(TrayMenu.Find(menu, "tray.overlay")!);
            Assert.False(overlay);

            // Меню, построенное заново, отражает изменения; изменение из окна настроек — тоже.
            var again = TrayMenu.Build(env.Store, env.Session, Commands(() => overlay, v => overlay = v));
            Assert.True(TrayMenu.Find(again, "tray.combos")!.IsChecked);
            Assert.False(TrayMenu.Find(again, "tray.overlay")!.IsChecked);
            SegmentClick(w, Loc.T("Keys.Filter.All"));
            Assert.False(TrayMenu.Find(TrayMenu.Build(env.Store, env.Session, Commands(() => overlay, v => overlay = v)), "tray.combos")!.IsChecked);
            w.Close();
        });
    }

    [Fact]
    public void TrayMenu_MouseParts_TurnTheModeOnAndOff()
    {
        var s = LabSettings.Default;
        Assert.False(TrayMenu.MouseIconOn(s));
        s = TrayMenu.SetMouseIcon(s, true);
        Assert.True(s.MouseCursor.Enabled && s.MouseCursor.ShowIcon);
        Assert.True(TrayMenu.MouseCircleOn(s)); // круг по умолчанию включён внутри режима
        s = TrayMenu.SetMouseCircle(s, false);
        Assert.True(s.MouseCursor.Enabled);
        s = TrayMenu.SetMouseIcon(s, false);
        Assert.False(s.MouseCursor.Enabled); // обе части выключены → режим выключен
        s = TrayMenu.SetMouseCircle(s, true);
        Assert.True(s.MouseCursor.Enabled && s.MouseCursor.ShowCircle && !s.MouseCursor.ShowIcon);
    }

    [Fact]
    public void TrayMenu_IsLocalized_AndListsProfiles()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            env.Session.Store.Create("Stream", LabSettings.Default);
            foreach (var lang in Languages)
            {
                Loc.Set(lang);
                var menu = TrayMenu.Build(env.Store, env.Session, Commands(() => true, _ => { }));
                Assert.Equal(Loc.T("Tray.Exit"), TrayMenu.Find(menu, "tray.exit")!.Header);
                var profiles = menu.Items.OfType<MenuItem>().Single(m => m.Name == "tray_profiles");
                Assert.Equal(2, profiles.Items.Count);
                Assert.Single(profiles.Items.OfType<MenuItem>(), m => m.IsChecked);
                Assert.Contains(menu.Resources.MergedDictionaries, r => ReferenceEquals(r, UiTheme.Resources));
            }
        });
    }

    private static IEnumerable<RadioButton> Segments(SettingsWindow w) => Descendants(w.Cards).OfType<RadioButton>();

    private static bool SegmentChecked(SettingsWindow w, string label) => Segments(w).Single(r => (string)r.Content == label).IsChecked == true;

    private static void SegmentClick(SettingsWindow w, string label) => Segments(w).Single(r => (string)r.Content == label).IsChecked = true;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var d in Descendants(child))
            {
                yield return d;
            }
        }
    }

    // ── общие настройки приложения ───────────────────────────────────────────

    [Fact]
    public void AppPreferences_RoundTrip_AndBadFilesFallBack()
    {
        var p = AppPreferences.Default with { Language = UiLanguage.Ukrainian, Theme = UiThemeMode.Dark, CloseToTray = false, TrayHintShown = true, ActiveProfileId = "p-0123456789" };
        Assert.Equal(p, AppPreferencesStore.Parse(AppPreferencesStore.Write(p)));
        Assert.Null(AppPreferencesStore.Parse("{"));
        Assert.Null(AppPreferencesStore.Parse("""{"format":"other","formatVersion":1}"""));
        Assert.Null(AppPreferencesStore.Parse("""{"format":"backpack-app","formatVersion":6}"""));
        var odd = AppPreferencesStore.Parse("""{"format":"backpack-app","formatVersion":1,"language":"klingon","theme":"purple","activeProfile":"../x"}""")!;
        Assert.Equal(UiLanguage.System, odd.Language);
        Assert.Equal(UiThemeMode.System, odd.Theme);
        Assert.Null(odd.ActiveProfileId);

        string dir = TempDir();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "app.json"), "not json");
        var store = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
        Assert.True(store.Unreadable);
        Assert.Equal(AppPreferences.Default, store.Current);
    }

    // ── профили ──────────────────────────────────────────────────────────────

    private static LabSettings Custom => LabSettings.Default with
    {
        Appearance = LabSettings.Default.Appearance with { Size = 44, LabelSet = LabelSet.MacOS, Radius = 0.3 },
        Keys = LabSettings.Default.Keys with { Filter = KeyFilter.CombosOnly, Trail = TrailMode.FadeInPlace, LingerMs = 1500, MouseInRow = false },
        MouseCursor = LabSettings.Default.MouseCursor with { Enabled = true, ShowCircle = false },
        LayoutIndicator = LabSettings.Default.LayoutIndicator with { Enabled = false },
        Placement = new PlacementSettings(ScreenAnchor.TopRight, 40, 30, -10, 15),
    };

    [Fact]
    public void Profile_RoundTrip_KeepsEveryCustomization()
    {
        var p = new Profile("p-roundtrip1", "Stream «Ютуб» ✓", Custom);
        var r = ProfileSerializer.Read(ProfileSerializer.Write(p));
        Assert.True(r.Ok, string.Join("; ", r.Errors));
        Assert.Equal(p, r.Profile);
        string json = ProfileSerializer.Write(p);
        foreach (var key in new[] { "language", "theme", "closeToTray", "trayHintShown", "activeProfile", "source", "ringCapacity" })
        {
            Assert.DoesNotMatch("\"" + key + "\"\\s*:", json); // общие настройки и параметры захвата не входят в профиль
        }
    }

    [Fact]
    public void Profile_WithFormatOneSettings_IsMigrated()
    {
        string json = """{"format":"backpack-profile","formatVersion":1,"id":"p-old0000001","name":"Old","settings":""" + SettingsTests.V1File + "}";
        var r = ProfileSerializer.Read(json);
        Assert.True(r.Ok, string.Join("; ", r.Errors));
        Assert.Equal(1, r.MigratedSettingsFrom);
        Assert.Equal(40, r.Profile!.Settings.Appearance.Size);
        Assert.Equal(30, r.Profile.Settings.Placement.EdgeY);
    }

    [Theory]
    [InlineData("{")]                                                                                     // повреждён
    [InlineData("[]")]
    [InlineData("""{"format":"backpack-settings","formatVersion":2}""")]                           // не профиль
    [InlineData("""{"format":"backpack-profile"}""")]                                              // без версии
    [InlineData("""{"format":"backpack-profile","formatVersion":9,"id":"p-x0000001","name":"A","settings":{}}""")] // новее
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"../evil","name":"A","settings":{"format":"backpack-settings","formatVersion":2}}""")]
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"p-x0000001","name":"","settings":{"format":"backpack-settings","formatVersion":2}}""")]
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"p-x0000001","name":"A"}""")]
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"p-x0000001","name":"A","script":"x","settings":{"format":"backpack-settings","formatVersion":2}}""")]
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"p-x0000001","name":"A","settings":{"format":"backpack-settings","formatVersion":2,"appearance":{"border":{"radius":1.5}}}}""")]
    [InlineData("""{"format":"backpack-profile","formatVersion":1,"id":"p-x0000001","name":"A","settings":{"format":"backpack-settings","formatVersion":2,"xaml":"<Window/>"}}""")]
    public void Profile_InvalidFiles_AreRejectedWholly(string json)
    {
        var r = ProfileSerializer.Read(json);
        Assert.False(r.Ok);
        Assert.Null(r.Profile);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void Import_IsValidatedFirst_NothingIsAddedOnError()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv();
            var before = env.Session.List();
            var settingsBefore = env.Store.Current;
            string bad = Path.Combine(env.Dir, "bad.backpack-profile.json");
            File.WriteAllText(bad, ProfileSerializer.Write(new Profile("p-good000001", "Good", Custom)).Replace("\"size\": 44", "\"size\": 4000"));
            var r = env.Session.Import(bad);
            Assert.False(r.Ok);
            Assert.Equal(before, env.Session.List());
            Assert.Equal(settingsBefore, env.Store.Current);

            string tooBig = Path.Combine(env.Dir, "big.json");
            File.WriteAllText(tooBig, new string(' ', ProfileSerializer.MaxFileBytes + 10));
            Assert.False(env.Session.Import(tooBig).Ok);
            Assert.Equal(before, env.Session.List());
        });
    }

    [Fact]
    public void Import_AssignsNewId_AndKeepsPositionVisibleOnThisScreen()
    {
        Sta.Run(() =>
        {
            using var env = NewEnv(new Rect(0, 0, 1280, 720));
            var foreign = Custom with { Placement = new PlacementSettings(ScreenAnchor.BottomRight, 400, 400, 1900, 1900) };
            string file = Path.Combine(env.Dir, "big-screen.json");
            File.WriteAllText(file, ProfileSerializer.Write(new Profile(env.Session.Active!.Id, "From 4K", foreign))); // тот же id, что у текущего
            var r = env.Session.Import(file);
            Assert.True(r.Ok, string.Join("; ", r.Errors));
            Assert.NotEqual(env.Session.Active!.Id, r.Profile!.Id);             // не заменяет существующий
            Assert.Equal(2, env.Session.List().Count);
            var p = r.Profile.Settings.Placement;
            Assert.True(Math.Abs(p.OffsetX) <= 640 && Math.Abs(p.OffsetY) <= 360, $"{p.OffsetX}, {p.OffsetY}");
            Assert.True(p.EdgeX <= 1280 / 3.0 && p.EdgeY <= 720 / 3.0);
        });
    }

    [Fact]
    public void Switch_WritesPendingChangesOfTheCurrentProfileFirst()
    {
        using var env = NewEnv();
        var other = env.Session.Store.Create("Other", Custom);
        var first = env.Session.Active!;
        env.Store.Update(s => s with { Appearance = s.Appearance with { Size = 50 } });
        Assert.True(env.Session.SwitchTo(other.Id).Switched);
        Assert.Equal(50, env.Session.Store.Load(first.Id).Profile!.Settings.Appearance.Size); // ничего не потеряно
        Assert.Equal(Custom, env.Store.Current);
        Assert.Equal(other.Id, env.Prefs.Current.ActiveProfileId);
        Assert.False(env.Session.HasPendingWrite);

        env.Store.Update(s => s with { Appearance = s.Appearance with { Size = 60 } });
        Assert.True(env.Session.SwitchTo(first.Id).Switched);
        Assert.Equal(60, env.Session.Store.Load(other.Id).Profile!.Settings.Appearance.Size);
        Assert.Equal(50, env.Store.Current.Appearance.Size);
    }

    [Fact]
    public void Switch_ToCorruptProfile_ChangesNothing()
    {
        using var env = NewEnv();
        var other = env.Session.Store.Create("Other", Custom);
        File.WriteAllText(Path.Combine(env.Dir, "profiles", other.Id + ".json"), "{ broken");
        var before = env.Store.Current;
        var (switched, errors) = env.Session.SwitchTo(other.Id);
        Assert.False(switched);
        Assert.NotEmpty(errors);
        Assert.Equal(before, env.Store.Current);
        Assert.DoesNotContain(env.Session.List(), p => p.Id == other.Id); // повреждённый файл не в списке
    }

    [Fact]
    public void Profile_CreateDuplicateRenameDelete_AndSafeWrite()
    {
        using var env = NewEnv();
        var s = env.Session;
        var lesson = s.Store.Create("Lesson", Custom);
        Assert.True(s.SwitchTo(lesson.Id).Switched);
        Assert.Equal("Lesson", s.ActiveName);
        Assert.Equal(lesson.Id, env.Prefs.Current.ActiveProfileId);
        var copy = s.Duplicate(lesson.Id, n => n + " — copy")!;
        Assert.Equal("Lesson — copy", copy.Name);
        var again = s.Duplicate(lesson.Id, n => n + " — copy")!;
        Assert.Equal("Lesson — copy (2)", again.Name); // имена не повторяются
        s.Rename(lesson.Id, "Lesson 2");
        Assert.Equal("Lesson 2", s.ActiveName);
        Assert.Equal(lesson.Id, s.Active!.Id);
        Assert.False(s.Delete(lesson.Id));          // активный не удаляется
        Assert.True(s.Delete(copy.Id));
        Assert.DoesNotContain(s.List(), p => p.Id == copy.Id);
        Assert.Empty(Directory.GetFiles(Path.Combine(env.Dir, "profiles"), "*.tmp"));
        var d = s.CreateDefault("Defaults");
        Assert.Equal(Backpack.Visual.Styles.BrandStyles.Starter, d.Settings); // Phase 4I: новый профиль — фирменные стили
        Assert.Equal("Lesson 2", s.ActiveName);    // создание не переключает
    }

    [Fact]
    public void Every_change_is_saved_to_the_current_profile_automatically()
    {
        using var env = NewEnv();
        int changed = 0;
        env.Session.Changed += () => changed++;
        env.Store.Update(s => s with { Keys = s.Keys with { LingerMs = 1000 } });
        Assert.True(changed > 0);
        Assert.True(env.Session.Flush());
        Assert.Equal(1000, env.Session.Store.Load(env.Session.Active!.Id).Profile!.Settings.Keys.LingerMs);
        Assert.Null(env.Session.SaveError);
    }

    [Fact]
    public void PlacementGuard_KeepsSmallValues_ClampsLargeOnes()
    {
        var area = new Rect(0, 0, 1366, 728);
        var small = LabSettings.Default with { Placement = new PlacementSettings(ScreenAnchor.Center, 100, 100, 30, -40) };
        Assert.Equal(small, PlacementGuard.EnsureVisible(small, area));
        var big = LabSettings.Default with { Placement = new PlacementSettings(ScreenAnchor.TopLeft, 400, 400, -2000, 2000) };
        var p = PlacementGuard.EnsureVisible(big, area).Placement;
        Assert.InRange(p.OffsetX, -683, 683);
        Assert.InRange(p.OffsetY, -364, 364);
        Assert.InRange(p.EdgeX, 0, 1366 / 3.0);
        Assert.InRange(p.EdgeY, 0, 728 / 3.0);
    }

    [Fact]
    public void ProfileSchema_AcceptsWrittenProfiles_AndRejectsExtras()
    {
        // Схема профиля ссылается на схему настроек по $id: она регистрируется при загрузке (один раз на процесс).
        Assert.NotNull(ThemeSchemaTests.SettingsSchema);
        var schema = Json.Schema.JsonSchema.FromFile(Repo.PathOf("docs", "settings", "backpack-profile.schema.json"));
        Json.Schema.EvaluationResults Eval(System.Text.Json.Nodes.JsonNode n) =>
            schema.Evaluate(System.Text.Json.JsonDocument.Parse(n.ToJsonString()).RootElement, new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });

        var ok = System.Text.Json.Nodes.JsonNode.Parse(ProfileSerializer.Write(new Profile("p-schema0001", "Schema", Custom)))!;
        var result = Eval(ok);
        Assert.True(result.IsValid, string.Join("; ", (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Values)));
        var extra = ok.DeepClone().AsObject();
        extra["script"] = "x";
        Assert.False(Eval(extra).IsValid);
        var badId = ok.DeepClone().AsObject();
        badId["id"] = "../x";
        Assert.False(Eval(badId).IsValid);
        var badSettings = ok.DeepClone().AsObject();
        badSettings["settings"]!["keys"]!["filter"] = "letters";
        Assert.False(Eval(badSettings).IsValid); // $ref на схему настроек действует
    }

    [GeneratedRegex(@"(?:Loc\.(?:T|F|TFor)|LocText)\(\s*""(?<key>[A-Za-z0-9_.]+)""|(?<=\(|, |\? |: |\[)""(?<key>(?:Settings\.Nav|LayoutStatus|Diag|Demo)\.[A-Za-z0-9_.]+)""")]
    private static partial Regex LocCall();

    private static HashSet<string> Placeholders(string s) => PlaceholderRegex().Matches(s).Select(m => m.Groups[1].Value).ToHashSet();

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderRegex();
}
