using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shape = System.Windows.Shapes.Shape;
using WpfPath = System.Windows.Shapes.Path;
using KeyOverlay.Visual.Brand;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Mouse;
using KeyOverlay.Visual.Profiles;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Styles;
using KeyOverlay.Visual.Updates;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 2E: рендеры, мышь, фильтр, каталог стилей, форматы, горячие клавиши, обновления, данные, логотип, главное окно.</summary>
public class Phase2ETests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ko-2e-" + Guid.NewGuid().ToString("N")[..8]);

    // ── рендеры кейкапов ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(KeycapRenderer.LowProfile)]
    [InlineData(KeycapRenderer.Laptop)]
    [InlineData(KeycapRenderer.Pbt)]
    [InlineData(KeycapRenderer.Minimal)]
    [InlineData(KeycapRenderer.Volumetric)]
    public void EveryRenderer_DrawsLabels_AndPressChangesOnlyTheFace(KeycapRenderer renderer)
    {
        Sta.Run(() =>
        {
            var a = AppearanceSettings.Default with { Renderer = renderer };
            foreach (var key in new[] { "Ctrl", "Alt", "Esc", "ArrowUp", "Win", "Enter", "Backspace", "A" })
            {
                var item = new KeycapItem(1, 1, key, key is "Ctrl" or "Alt" or "Win", false, KeySide.Left);
                var k = KeycapFactory.Create(renderer);
                Assert.Equal(renderer, k.Renderer);
                k.Apply(a, item, showBadge: false);
                k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(k.DesiredSize.Height > 0 && k.DesiredSize.Width > 0, key);
                Assert.Equal(KeycapMetrics.From(a).Height, k.Height, 6);
                k.PressOffset = k.PressTravel;
                Assert.Equal(k.PressTravel, k.PressOffset);
            }
        });
    }

    [Theory]
    [InlineData(KeycapRenderer.LowProfile)]
    [InlineData(KeycapRenderer.Laptop)]
    [InlineData(KeycapRenderer.Pbt)]
    [InlineData(KeycapRenderer.Volumetric)]
    public void SmallSize_LongLabels_AreNotClipped(KeycapRenderer renderer)
    {
        // Малый размер (S = 12): подпись ужимается не ниже 0.4·S, а если не помещается — кейкап шире; текст не обрезается.
        Sta.Run(() =>
        {
            var a = AppearanceSettings.Default with { Renderer = renderer, Size = 12, LabelMode = LabelMode.Text };
            foreach (var key in new[] { "Backspace", "CapsLock", "Ctrl", "Esc", "PrintScreen" })
            {
                var k = KeycapFactory.Create(renderer);
                k.Apply(a, new KeycapItem(1, 1, key, key == "Ctrl", false), showBadge: false);
                k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                k.Arrange(new Rect(k.DesiredSize));
                Assert.True(k.LabelFontSize >= 12 * 0.4 - 1e-9, key);
                var text = FindText(k, k.MainText);
                Assert.NotNull(text);
                Assert.Equal(TextTrimming.None, text!.TextTrimming);
                Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
            }
        });
    }

    [Fact]
    public void Volumetric_Shapes_SquareCompactAndLEnter_PressMovesFaceAndLabel()
    {
        Sta.Run(() =>
        {
            var a = AppearanceSettings.Default with { Renderer = KeycapRenderer.Volumetric };
            VolumetricKeycap Make(string key, bool modifier = false, AppearanceSettings? look = null)
            {
                var k = new VolumetricKeycap();
                k.Apply(look ?? a, new KeycapItem(1, 1, key, modifier, false), showBadge: false);
                return k;
            }

            Assert.Equal(1, Make("A").Units);
            Assert.Equal(1.5, Make("Ctrl", true).Units);
            Assert.Equal(1.5, Make("Space").Units); // пробел — не длиннее компактной клавиши
            Assert.True(Make("Enter").IsLShape);
            Assert.False(Make("Enter", look: a with { VolEnterShape = false }).IsLShape);
            Assert.False(Make("A").IsLShape);

            var k = Make("A");
            double topBefore = k.TopPolygon.Min(p => p.Y);
            k.PressOffset = k.PressTravel;
            double topAfter = k.TopPolygon.Min(p => p.Y);
            Assert.Equal(topBefore + k.PressTravel, topAfter, 6); // грань опустилась на ход нажатия (вид спереди: X не меняется)
            Assert.True(k.TopZ < KeycapMetrics.From(a).VolDepth);
        });
    }

    [Fact]
    public void RendererSwap_KeepsIdentity_PhaseAndRejectsStaleCompletion()
    {
        Sta.Run(() =>
        {
            var clock = new FakeClock();
            var p = new OverlayPresenter(clock, useRenderingLoop: false);
            p.Apply(AppearanceSettings.Default, RowOptions.Default with { Trail = TrailMode.FadeInPlace });
            p.Update([new KeycapItem(1, 30, "A", false, true)]);
            clock.Advance(1000);
            p.Tick();
            p.Update([]); // начало исчезновения
            clock.Advance(50);
            p.Tick();
            var v = p.Visuals.Single();
            int generation = v.Generation;
            Assert.Equal(KeycapPhase.Exiting, v.Phase);

            p.Apply(AppearanceSettings.Default with { Renderer = KeycapRenderer.Volumetric });
            Assert.Same(v, p.Visuals.Single());
            Assert.IsType<VolumetricKeycap>(v.Control);
            Assert.Same(v.Control, p.Row.Children.Cast<UIElement>().Single());
            Assert.Equal(KeycapPhase.Exiting, v.Phase);

            // Повторное нажатие той же клавиши во время исчезновения — оживает тот же элемент, устаревшее завершение отклоняется.
            p.Update([new KeycapItem(2, 30, "A", false, true)]);
            Assert.Same(v, p.Visuals.Single());
            Assert.False(p.TryCompleteExit(v, generation));
            Assert.Single(p.Visuals);
            p.Dispose();
        });
    }

    private static TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock t && t.Text == text)
        {
            return t;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindText(VisualTreeHelper.GetChild(root, i), text) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // ── фильтр: любые одновременные клавиши ──────────────────────────────────

    [Fact]
    public void RowFilter_AnyHeld_ShowsLetterPairs_ModifiersRuleDoesNot()
    {
        var pair = new[] { new KeycapItem(1, 1, "A", false, true), new KeycapItem(2, 2, "B", false, true) };
        Assert.Empty(RowFilter.Visible(pair, KeyFilter.CombosOnly, true, ChordMode.Modifiers));
        Assert.Equal(2, RowFilter.Visible(pair, KeyFilter.CombosOnly, true, ChordMode.AnyHeld).Count);
        Assert.Empty(RowFilter.Visible([pair[0]], KeyFilter.CombosOnly, true, ChordMode.AnyHeld));
        Assert.Equal(2, RowFilter.Visible(pair, KeyFilter.All, true, ChordMode.AnyHeld).Count);
    }

    [Fact]
    public void RowFilter_SwitchingChordMode_DoesNotReturnHiddenReleasedEntries()
    {
        var f = new RowFilter();
        var released = new[] { new KeycapItem(1, 1, "A", false, false), new KeycapItem(2, 2, "B", false, false) };
        f.Configure(KeyFilter.CombosOnly, true, released, ChordMode.Modifiers);
        Assert.Empty(f.Apply(released));
        f.Configure(KeyFilter.CombosOnly, true, released, ChordMode.AnyHeld);
        Assert.Empty(f.Apply(released)); // отпущенные и скрытые до смены — не возвращаются
        var pressed = new[] { new KeycapItem(3, 3, "C", false, true), new KeycapItem(4, 4, "D", false, true) };
        Assert.Equal(2, f.Apply(pressed).Count);
    }

    // ── мышь ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(MouseRenderer.Outline)]
    [InlineData(MouseRenderer.Solid)]
    [InlineData(MouseRenderer.Retro)]
    [InlineData(MouseRenderer.Sketch)]
    public void MouseIcon_PressChangesOnlyItsPart_WheelArrowAndSideButtons(MouseRenderer renderer)
    {
        Sta.Run(() =>
        {
            var look = MouseVisual.Default with { Renderer = renderer, BodyColor = new ThemeColor(0xB8, 0xC4, 0xD6) };
            Canvas Icon(MouseIconState st, MouseVisual? l = null) => (Canvas)((Viewbox)MouseIcon.Create(40, l ?? look, st)).Child;
            static int Arrows(Canvas c) => c.Children.OfType<WpfPath>().Count(p => (string?)p.Tag == "wheel-arrow");
            static int Accent(Canvas c, Brush accent) => c.Children.OfType<Shape>().Count(s => s.Fill is SolidColorBrush b && b.Color == ((SolidColorBrush)accent).Color);

            var accent = look.IconAccent.ToFrozenBrush();
            var idle = Icon(new MouseIconState(MouseButtons.None, MouseButtons.None, 0, 0));
            var left = Icon(new MouseIconState(MouseButtons.Left, MouseButtons.None, 0, 0));
            Assert.Equal(0, Accent(idle, accent));
            Assert.Equal(1, Accent(left, accent)); // ровно одна часть, а не весь корпус
            Assert.Equal(1, Accent(Icon(new MouseIconState(MouseButtons.Middle, MouseButtons.None, 0, 0)), accent));
            Assert.Equal(0, Arrows(idle));
            Assert.Equal(1, Arrows(Icon(new MouseIconState(MouseButtons.None, MouseButtons.None, 1, 0))));
            Assert.Equal(1, Arrows(Icon(new MouseIconState(MouseButtons.None, MouseButtons.None, 0, -1))));
            // Средняя кнопка и прокрутка независимы: нажатие + стрелка одновременно.
            var both = Icon(new MouseIconState(MouseButtons.Middle, MouseButtons.None, -1, 0));
            Assert.Equal(1, Arrows(both));
            Assert.Equal(1, Accent(both, accent));

            int Side(SideButtonsMode mode) => Icon(new MouseIconState(MouseButtons.None, MouseButtons.None, 0, 0), look with { SideButtons = mode })
                .Children.OfType<WpfPath>().Count(p => p.Data is RectangleGeometry r && r.Rect.Width < 6);
            Assert.Equal(0, Side(SideButtonsMode.Hidden));
            Assert.Equal(1, Side(SideButtonsMode.One));
            Assert.Equal(2, Side(SideButtonsMode.Two));
            Assert.True(MouseIcon.SideButtonRect(true, SideButtonsSide.Right).Left > MouseIcon.SideButtonRect(true, SideButtonsSide.Left).Left);
            Assert.Equal(1, Accent(Icon(new MouseIconState(MouseButtons.X2, MouseButtons.None, 0, 0), look with { SideButtons = SideButtonsMode.One }), accent));
        });
    }

    [Fact]
    public void MouseCursor_WheelPulse_ShowsDirection_Expires_AndSuppressionHidesWindow()
    {
        Sta.Run(() =>
        {
            using var c = new MouseCursorController(() => new MouseButtonState(MouseButtons.None, MouseButtons.None)) { ShowOnScreen = false };
            c.Apply(MouseCursorSettings.Default with { Enabled = true }, MouseVisual.Default with { WheelPulseMs = 100 });
            c.Pulse(horizontal: false, delta: -15); // мелкое смещение высокоточного колеса
            Assert.Equal((-1, 0), c.WheelShown);
            c.Pulse(horizontal: true, delta: 120);
            Assert.Equal((0, 1), c.WheelShown);
            Assert.Equal(2, c.WheelPulses);
            Assert.True(c.IsRunning);
            c.Suppressed = true;
            Assert.False(c.IsRunning);
            Assert.False(c.WindowVisible);
            c.Suppressed = false;
            Assert.True(c.IsRunning);
        });
    }

    // ── стили ────────────────────────────────────────────────────────────────

    [Fact]
    public void BuiltIns_CoverAllRenderers_AreImmutable_AndHaveTranslatedNames()
    {
        Assert.All(Enum.GetValues<KeycapRenderer>(), r => Assert.Contains(BuiltInStyles.All, s => s.Kind == StyleKind.Keycap && s.Keycap!.Renderer == r));
        Assert.All(Enum.GetValues<MouseRenderer>(), r => Assert.Contains(BuiltInStyles.All, s => s.Kind == StyleKind.Mouse && s.Mouse!.Renderer == r));
        Assert.Equal(4, BuiltInStyles.All.Count(s => s.Kind == StyleKind.Mouse && s.Mouse!.Renderer == MouseRenderer.Retro));
        Assert.Equal(3, BuiltInStyles.All.Count(s => s.Kind == StyleKind.Keycap && s.Keycap!.Renderer == KeycapRenderer.Volumetric));
        Assert.All(BuiltInStyles.All, s => Assert.True(StyleIds.IsBuiltIn(s.Id) && s.BuiltIn));
        var store = new StyleStore(TempDir());
        Assert.Throws<InvalidOperationException>(() => store.Save(BuiltInStyles.All[0]));
        Assert.False(store.Delete(BuiltInStyles.All[0].Id));
        foreach (var lang in new[] { UiLanguage.Ukrainian, UiLanguage.English, UiLanguage.Russian })
        {
            Loc.Set(lang);
            Assert.All(BuiltInStyles.All, s => Assert.DoesNotContain("[", BuiltInStyles.LocalizedName(s)));
        }

        Loc.Set(UiLanguage.English);
    }

    [Fact]
    public void ApplyMatchesCapture_RoundTrip_KeepsSizeLabelsAndSideButtons()
    {
        var s = LabSettings.Default with
        {
            Appearance = AppearanceSettings.Default with
            {
                Size = 22,
                LabelSet = LabelSet.MacOS,
                Mouse = MouseVisual.Default with { SideButtons = SideButtonsMode.One, SideButtonsSide = SideButtonsSide.Right, IconSize = 40 },
            },
        };
        var iso = BuiltInStyles.Find("keyoverlay.volumetric.mint")!;
        var applied = StyleOps.Apply(s, iso);
        Assert.Equal(KeycapRenderer.Volumetric, applied.Appearance.Renderer);
        Assert.Equal(22, applied.Appearance.Size);
        Assert.Equal(LabelSet.MacOS, applied.Appearance.LabelSet);
        Assert.True(StyleOps.Matches(applied, iso));
        Assert.False(StyleOps.Matches(applied with { Appearance = applied.Appearance with { Primary = new ThemeColor(1, 2, 3) } }, iso));

        var retro = BuiltInStyles.Find("keyoverlay.mouse.retro-beige")!;
        var withMouse = StyleOps.Apply(applied, retro);
        Assert.Equal(SideButtonsMode.One, withMouse.Appearance.MouseLook.SideButtons);
        Assert.Equal(SideButtonsSide.Right, withMouse.Appearance.MouseLook.SideButtonsSide);
        Assert.Equal(40, withMouse.Appearance.MouseLook.IconSize);
        Assert.True(StyleOps.Matches(withMouse, retro));

        var indicator = BuiltInStyles.Find("keyoverlay.indicator.accent")!;
        var withIndicator = StyleOps.Apply(withMouse, indicator);
        Assert.True(withIndicator.LayoutIndicator.CustomStyle);
        Assert.True(StyleOps.Matches(withIndicator, indicator));
        // Phase 4H: выбор «как у кейкапов» — настройка плагина индикатора; стиль индикатора остаётся выбранным в каталоге.
        Assert.Equal(indicator.Id, StyleOps.SelectedId(withIndicator with { LayoutIndicator = withIndicator.LayoutIndicator with { CustomStyle = false } }, StyleKind.Indicator));
    }

    [Fact]
    public void StyleStore_SaveRenameDelete_ImportConflicts_AndRejectsDamagedFiles()
    {
        string dir = TempDir();
        try
        {
            var store = new StyleStore(Path.Combine(dir, "styles"));
            var mine = StyleOps.Capture(LabSettings.Default, StyleKind.Keycap, "u-mine00001", "Mine", "keyoverlay.lowprofile.classic");
            store.Save(mine);
            Assert.Single(store.List(StyleKind.Keycap));
            Assert.Equal("Renamed", store.Rename(mine.Id, "Renamed")!.Name);

            // Экспорт → импорт в тот же каталог: дубль не создаётся.
            string file = Path.Combine(dir, "x.keyoverlay-style.json");
            store.Export(store.Find(mine.Id)!, file);
            var again = store.Import(file);
            Assert.Equal(ImportOutcome.AlreadyPresent, again.Outcome);
            Assert.Single(store.List(StyleKind.Keycap));

            // Тот же id, другие параметры — новый id, ничего не перезаписано.
            var node = JsonNode.Parse(File.ReadAllText(file))!;
            node["look"]!["colors"]!["primary"] = "#123456";
            File.WriteAllText(file, node.ToJsonString());
            var renamed = store.Import(file);
            Assert.Equal(ImportOutcome.Renamed, renamed.Outcome);
            Assert.NotEqual(mine.Id, renamed.Style!.Id);
            Assert.Equal(new ThemeColor(0xFF, 0xFF, 0xFF), store.Find(mine.Id)!.Keycap!.Primary);
            Assert.Equal(2, store.List(StyleKind.Keycap).Count);

            // id встроенного стиля → сохраняется под новым id, встроенный не меняется.
            node["id"] = "keyoverlay.lowprofile.classic";
            File.WriteAllText(file, node.ToJsonString());
            var builtInClash = store.Import(file);
            Assert.True(builtInClash.Ok);
            Assert.False(StyleIds.IsBuiltIn(builtInClash.Style!.Id));

            // Повреждённые и опасные файлы отклоняются целиком.
            string[] bad =
            [
                "{",
                """{"format":"keyoverlay-style","formatVersion":2,"id":"u-a","name":"x","kind":"keycap","look":{}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"u-aa1","name":"x","kind":"keycap","look":{"colors":{"primary":"red"}}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"u-aa1","name":"x","kind":"keycap","look":{"xaml":"<Grid/>"}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"u-aa1","name":"x","kind":"keycap","assembly":"evil.dll","look":{}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"../x","name":"x","kind":"keycap","look":{}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"u-aa1","name":"x","kind":"script","look":{}}""",
                """{"format":"keyoverlay-style","formatVersion":1,"id":"u-aa1","name":"x","kind":"keycap","look":{"keycap":{"size":30}}}""",
            ];
            int before = store.List(StyleKind.Keycap).Count;
            foreach (var json in bad)
            {
                File.WriteAllText(file, json);
                Assert.False(store.Import(file).Ok, json);
            }

            Assert.Equal(before, store.List(StyleKind.Keycap).Count);
            File.WriteAllText(file, new string(' ', StyleSerializer.MaxFileBytes + 10));
            Assert.False(store.Import(file).Ok);

            // Повреждённый файл в папке пропускается и не удаляется.
            File.WriteAllText(Path.Combine(dir, "styles", "u-broken0001.json"), "{");
            Assert.Equal(before, store.List(StyleKind.Keycap).Count);
            Assert.True(store.SkippedFiles >= 1);
            Assert.True(File.Exists(Path.Combine(dir, "styles", "u-broken0001.json")));

            Assert.True(store.Delete(mine.Id));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ProfileExport_EmbedsUserStyles_ImportOnOtherComputer_KeepsLook_AndResolvesIdConflicts()
    {
        string a = TempDir(), b = TempDir();
        try
        {
            var stylesA = new StyleStore(Path.Combine(a, "styles"));
            var profilesA = new ProfileStore(Path.Combine(a, "profiles"), stylesA);
            var look = AppearanceSettings.Default with { Renderer = KeycapRenderer.Pbt, Primary = new ThemeColor(0x11, 0x22, 0x33) };
            var style = StyleOps.Capture(LabSettings.Default with { Appearance = look }, StyleKind.Keycap, "u-shared0001", "Shared", null);
            stylesA.Save(style);
            var settings = StyleOps.Apply(LabSettings.Default, style);
            var profile = profilesA.Create("With style", settings);
            string file = Path.Combine(a, "export.json");
            profilesA.Export(profile, file);
            Assert.Contains("\"styles\"", File.ReadAllText(file));

            // На другом компьютере уже есть стиль с тем же id, но другими параметрами.
            var stylesB = new StyleStore(Path.Combine(b, "styles"));
            stylesB.Save(style with { Keycap = style.Keycap! with { Primary = new ThemeColor(0xFF, 0, 0) } });
            var profilesB = new ProfileStore(Path.Combine(b, "profiles"), stylesB);
            var imported = profilesB.Import(file, new Rect(0, 0, 1920, 1040));
            Assert.True(imported.Ok, string.Join("; ", imported.Errors));
            var s = imported.Profile!.Settings;
            Assert.Equal(new ThemeColor(0x11, 0x22, 0x33), s.Appearance.Primary);         // оформление не потеряно
            Assert.NotEqual("u-shared0001", s.Appearance.KeycapStyleId);                   // ссылка на переименованный стиль
            Assert.True(StyleOps.Matches(s, stylesB.Find(s.Appearance.KeycapStyleId)!));
            Assert.Equal(new ThemeColor(0xFF, 0, 0), stylesB.Find("u-shared0001")!.Keycap!.Primary); // прежний не перезаписан

            // Без каталога (или без вложенного стиля) параметры всё равно в профиле.
            var bare = new ProfileStore(Path.Combine(b, "bare"));
            var r = bare.Import(file, new Rect(0, 0, 1920, 1040));
            Assert.True(r.Ok);
            Assert.Equal(new ThemeColor(0x11, 0x22, 0x33), r.Profile!.Settings.Appearance.Primary);
        }
        finally
        {
            Directory.Delete(a, true);
            Directory.Delete(b, true);
        }
    }

    private static readonly Lazy<Json.Schema.JsonSchema> StyleSchema = new(() => Json.Schema.JsonSchema.FromFile(Repo.PathOf("docs", "settings", "keyoverlay-style.schema.json")));

    [Fact]
    public void StyleFile_IsValid_AgainstStyleSchema()
    {
        Assert.NotNull(ThemeSchemaTests.SettingsSchema); // статическая инициализация регистрирует и схему темы по $id
        var schema = StyleSchema.Value;
        foreach (var e in BuiltInStyles.All)
        {
            var json = StyleSerializer.ToJson(e with { Id = "u-test000001", BuiltIn = false });
            var r = schema.Evaluate(System.Text.Json.JsonDocument.Parse(json.ToJsonString()).RootElement, new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });
            Assert.True(r.IsValid, e.Id + ": " + json.ToJsonString() + " " + string.Join("; ", (r.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(x => d.InstanceLocation + " " + x.Value))));
            Assert.True(StyleSerializer.Read(json.ToJsonString()).Ok, e.Id);
            var read = StyleSerializer.Read(json); // объект из памяти читается так же, как файл
            Assert.True(read.Ok, e.Id + ": " + string.Join("; ", read.Errors));
            Assert.True(StyleStore.SameLook(e, read.Style!), e.Id);
        }
    }

    // ── форматы ──────────────────────────────────────────────────────────────

    [Fact]
    public void SettingsFormat2_MigratesToThree_WithoutFakeStyleReferences()
    {
        var v2 = SettingsSerializer.ToJson(LabSettings.Default);
        v2["formatVersion"] = 2;
        ((JsonObject)v2["appearance"]!["keycap"]!).Remove("styleId");
        ((JsonObject)v2["appearance"]!["keycap"]!).Remove("volEnterShape");
        ((JsonObject)v2["appearance"]!["colors"]!).Remove("useGradient");
        var r = SettingsSerializer.Read(v2.ToJsonString());
        Assert.False(r.FileUnusable);
        Assert.Equal(2, r.MigratedFrom);
        Assert.Null(r.Settings.Appearance.KeycapStyleId);
        Assert.Equal(KeycapRenderer.LowProfile, r.Settings.Appearance.Renderer);
        Assert.Equal(ChordMode.Modifiers, r.Settings.Keys.Chords);

        var full = LabSettings.Default with
        {
            Keys = KeysSettings.Default with { Chords = ChordMode.AnyHeld },
            Appearance = AppearanceSettings.Default with
            {
                Renderer = KeycapRenderer.Volumetric,
                VolEnterShape = false,
                UseGradient = false,
                Mouse = MouseVisual.Default with { Renderer = MouseRenderer.Sketch, SideButtons = SideButtonsMode.Hidden, WheelPulseMs = 900, BodyColor = new ThemeColor(1, 2, 3, 4) },
            },
        };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(full));
        Assert.Empty(back.Problems);
        Assert.Equal(full, back.Settings);
    }

    [Fact]
    public void AppPreferences_FormatOne_GetsDefaults_LegacyHotkeysBecomeCommands_EmptyMeansNone()
    {
        var v1 = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":1,"language":"uk"}""")!;
        Assert.Equal(DisplayParts.All, v1.HideParts);
        Assert.False(v1.UpdatesOrDefault.AutoCheck);
        Assert.Empty(v1.BindingsOrEmpty.Map); // без прежнего раздела — сочетания по умолчанию у команд

        // Прежние сочетания (v1–v3): изменённое переносится в команду, снятое — пустой строкой, по умолчанию — не пишется.
        var v3 = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":3,"hotkeys":{"toggleDisplay":"Ctrl+Win+F8","lens":"","draw":"Ctrl+Alt+Shift+D","radial":"Ctrl+Alt+Shift+R","screenshot":"F3"}}""")!;
        Assert.Equal("Ctrl+Win+F8", v3.BindingsOrEmpty["display.toggle"]);
        Assert.Equal("", v3.BindingsOrEmpty["tool.focus"]);
        Assert.Null(v3.BindingsOrEmpty["tool.drawing"]);
        Assert.Null(v3.BindingsOrEmpty["tool.radial"]);
        Assert.Equal("F3", v3.BindingsOrEmpty["tool.drawing.screenshot"]);

        // Назначение пользователя для новой ID не перезаписывается.
        var both = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":3,"hotkeys":{"lens":"Ctrl+Alt+L"},"bindings":{"tool.focus":"Ctrl+Alt+F"}}""")!;
        Assert.Equal("Ctrl+Alt+F", both.BindingsOrEmpty["tool.focus"]);

        var p = AppPreferences.Default with
        {
            Bindings = CommandBindings.Empty.With("tool.focus", "").With("display.toggle", "Ctrl+Win+F8"),
            HideParts = DisplayParts.Keycaps | DisplayParts.Mouse,
            Updates = new UpdatePreferences(UpdateChannel.Preview, true),
        };
        string written = AppPreferencesStore.Write(p);
        Assert.DoesNotContain("\"hotkeys\"", written);
        var back = AppPreferencesStore.Parse(written)!;
        Assert.Equal(p, back);
        Assert.Equal("", back.BindingsOrEmpty["tool.focus"]);
    }

    // ── горячие клавиши ──────────────────────────────────────────────────────

    private sealed class FakeApi : IHotkeyApi
    {
        public HashSet<(uint Mods, uint Vk)> Busy { get; } = new();
        public Dictionary<int, (uint Mods, uint Vk)> Registered { get; } = new();
        public int Unregistrations { get; private set; }

        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            if (Busy.Contains((modifiers & 0xF, vk)))
            {
                error = 1409;
                return false;
            }

            Registered[id] = (modifiers, vk);
            return true;
        }

        public bool Unregister(nint hwnd, int id)
        {
            Unregistrations++;
            return Registered.Remove(id);
        }
    }

    [Fact]
    public void Hotkey_ParseFormat_RoundTrip_AndRules()
    {
        foreach (var text in new[] { "Ctrl+Alt+Shift+O", "Ctrl+Win+F24", "Alt+PageDown", "Ctrl+Shift+5", "Ctrl+Alt+Space" })
        {
            Assert.True(Hotkey.TryParse(text, out var h), text);
            Assert.Equal(text, h.ToString());
        }

        Assert.False(Hotkey.TryParse("Ctrl+Alt", out _));
        Assert.False(Hotkey.TryParse("Ctrl+A+B", out _));
        Assert.False(Hotkey.TryParse("", out _));
        Assert.True(Hotkey.TryParse("Win+Space", out var winSpace));
        Assert.True(HotkeyRules.IsReserved(winSpace));
        Assert.True(Hotkey.TryParse("Alt+Tab", out var altTab));
        Assert.True(HotkeyRules.IsReserved(altTab));
        Assert.False(HotkeyRules.IsAcceptable(new Hotkey(HotkeyModifiers.Shift, 'A')));
        Assert.False(HotkeyRules.IsAcceptable(new Hotkey(HotkeyModifiers.None, 0x70)));
        Assert.All(new[] { HotkeySettings.Default.ToggleDisplay, HotkeySettings.Default.Lens, HotkeySettings.Default.Draw, HotkeySettings.Default.Radial },
            h => Assert.True(HotkeyRules.IsAcceptable(h!.Value) && !HotkeyRules.IsReserved(h.Value)));
        Assert.Equal(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'K'), Hotkey.FromKey(System.Windows.Input.Key.K, System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt));
        Assert.Null(Hotkey.FromKey(System.Windows.Input.Key.LeftCtrl, System.Windows.Input.ModifierKeys.Control));
    }

    [Fact]
    public void HotkeyManager_RegistersWithNoRepeat_DetectsConflictsBusyReserved_SuspendsAndReleases()
    {
        var api = new FakeApi();
        var h = new HotkeyManager(api, createWindow: false);
        var pressed = new List<HotkeyAction>();
        h.Pressed += pressed.Add;
        var same = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 'Q');
        api.Busy.Add(((uint)(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift), 'B'));
        h.Apply(new HotkeySettings(same, same, new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 'B'), new Hotkey(HotkeyModifiers.Win, 0x20)));
        Assert.Equal(HotkeyStatus.Registered, h.StatusOf(HotkeyAction.ToggleDisplay));
        Assert.Equal(HotkeyStatus.Conflict, h.StatusOf(HotkeyAction.Lens));
        Assert.Equal(HotkeyStatus.Busy, h.StatusOf(HotkeyAction.Draw));
        Assert.Equal(HotkeyStatus.Reserved, h.StatusOf(HotkeyAction.Radial));
        Assert.Equal(1, h.RegisteredCount);
        Assert.All(api.Registered.Values, r => Assert.Equal(0x4000u, r.Mods & 0x4000)); // MOD_NOREPEAT: автоповтор не повторяет действие

        h.Raise(1);
        h.Raise(2); // не зарегистрировано — не срабатывает
        Assert.Equal(new[] { HotkeyAction.ToggleDisplay }, pressed);

        h.Suspend();
        Assert.Equal(0, h.RegisteredCount);
        Assert.Equal(HotkeyStatus.Suspended, h.StatusOf(HotkeyAction.ToggleDisplay));
        h.Resume();
        Assert.Equal(1, h.RegisteredCount);

        h.Apply(HotkeySettings.Default); // применение без перезапуска: старое снято, новое зарегистрировано
        Assert.Equal(4, h.RegisteredCount);
        h.Dispose();
        Assert.Equal(0, h.RegisteredCount);
        Assert.Empty(api.Registered);
    }

    [Fact]
    public void HotkeyManager_RealRegistration_ReleasesOnDispose()
    {
        // Настоящий RegisterHotKey с редким сочетанием; ввод не инжектируется. Если сочетание занято — «занято», без падения.
        Sta.Run(() =>
        {
            var hk = new HotkeySettings(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x87 /* F24 */), null, null, null);
            using (var m = new HotkeyManager())
            {
                m.Apply(hk);
                Assert.Contains(m.StatusOf(HotkeyAction.ToggleDisplay), new[] { HotkeyStatus.Registered, HotkeyStatus.Busy });
            }

            using var again = new HotkeyManager();
            again.Apply(hk);
            Assert.Contains(again.StatusOf(HotkeyAction.ToggleDisplay), new[] { HotkeyStatus.Registered, HotkeyStatus.Busy });
        });
    }

    // ── обновления ───────────────────────────────────────────────────────────

    private static readonly UpdateSource Source = UpdateSource.Parse("owner/repo")!;

    private static string Release(string tag, bool pre, string? asset = "KeyOverlay-{0}-win-x64.zip", string? page = null) =>
        $$"""{"tag_name":"{{tag}}","prerelease":{{(pre ? "true" : "false")}},"draft":false,"published_at":"2026-10-20T10:00:00Z","body":"notes {{tag}}","html_url":"{{page ?? "https://github.com/owner/repo/releases/tag/" + tag}}","assets":[{{(asset is null ? "" : "{\"name\":\"" + string.Format(asset, tag) + "\"}")}}]}""";

    [Fact]
    public void UpdateChecker_ChannelsVersionsPackagesAndForeignPages()
    {
        var current = new SemVersion(0, 3, 0, "preview.1");
        string json = "[" + string.Join(",", Release("v0.4.0-preview.2", true), Release("v0.3.5", false), Release("v0.2.0", false), Release("v9.9.9", false, page: "https://evil.example/x")) + "]";
        var stable = UpdateChecker.Evaluate(json, current, UpdateChannel.Stable, Source);
        Assert.Equal(UpdateState.Available, stable.State);
        Assert.Equal("0.3.5", stable.Release!.Version.ToString()); // предварительная и чужая страница отброшены
        var preview = UpdateChecker.Evaluate(json, current, UpdateChannel.Preview, Source);
        Assert.Equal("0.4.0-preview.2", preview.Release!.Version.ToString());
        Assert.Equal(UpdateState.UpToDate, UpdateChecker.Evaluate("[" + Release("v0.2.0", false) + "]", current, UpdateChannel.Stable, Source).State);
        Assert.Equal(UpdateState.NoSuitablePackage, UpdateChecker.Evaluate("[" + Release("v1.0.0", false, asset: "src-{0}.tar.gz") + "]", current, UpdateChannel.Stable, Source).State);
        Assert.Equal(UpdateState.BadResponse, UpdateChecker.Evaluate("{\"message\":\"x\"}", current, UpdateChannel.Stable, Source).State);
        Assert.Equal(UpdateState.BadResponse, UpdateChecker.Evaluate("not json", current, UpdateChannel.Stable, Source).State);
        Assert.True(new SemVersion(1, 0, 0, null).CompareTo(new SemVersion(1, 0, 0, "rc.1")) > 0);
        Assert.True(new SemVersion(1, 0, 0, "beta.11").CompareTo(new SemVersion(1, 0, 0, "beta.2")) > 0);
        Assert.Null(UpdateSource.Parse("https://github.com/soobshoob"));
        Assert.Null(UpdateSource.Parse(""));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return respond(request, ct);
        }
    }

    [Fact]
    public async Task UpdateChecker_NetworkFailures_AreReportedHonestly_AndRequestCarriesNoData()
    {
        var current = new SemVersion(0, 3, 0, null);
        Assert.Equal(UpdateState.NotConfigured, (await new UpdateChecker(null).CheckAsync(current, UpdateChannel.Stable)).State);

        var ok = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + Release("v0.3.1", false) + "]") }));
        var r = await new UpdateChecker(Source, ok).CheckAsync(current, UpdateChannel.Stable);
        Assert.Equal(UpdateState.Available, r.State);
        var req = ok.Requests.Single();
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Null(req.Content);
        Assert.Equal("https://api.github.com/repos/owner/repo/releases?per_page=30", req.RequestUri!.ToString());
        Assert.Equal("", req.RequestUri.Query.Replace("?per_page=30", "")); // ничего, кроме размера страницы

        var limited = new StubHandler((_, _) =>
        {
            var m = new HttpResponseMessage(HttpStatusCode.Forbidden);
            m.Headers.Add("X-RateLimit-Remaining", "0");
            return Task.FromResult(m);
        });
        Assert.Equal(UpdateState.RateLimited, (await new UpdateChecker(Source, limited).CheckAsync(current, UpdateChannel.Stable)).State);
        var offline = new StubHandler((_, _) => throw new HttpRequestException("no network"));
        Assert.Equal(UpdateState.Offline, (await new UpdateChecker(Source, offline).CheckAsync(current, UpdateChannel.Stable)).State);
        var slow = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(5000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        Assert.Equal(UpdateState.Timeout, (await new UpdateChecker(Source, slow, TimeSpan.FromMilliseconds(100)).CheckAsync(current, UpdateChannel.Stable)).State);
        var server = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        Assert.Equal(UpdateState.Failed, (await new UpdateChecker(Source, server).CheckAsync(current, UpdateChannel.Stable)).State);
    }

    // ── данные и перенос ─────────────────────────────────────────────────────

    [Fact]
    public void DataLocation_ExplicitPortableRoamingAndLegacyMigration()
    {
        string root = TempDir();
        try
        {
            string program = Path.Combine(root, "Program Files", "KeyOverlay"), appData = Path.Combine(root, "AppData");
            Directory.CreateDirectory(program);
            Assert.Equal(Path.Combine(root, "x"), DataLocations.Resolve(Path.Combine(root, "x", "settings.json"), program, appData).Directory);

            // Прежняя папка стенда: копируется, оригинал не удаляется.
            string legacy = Path.Combine(appData, "KeyOverlay.Lab");
            Directory.CreateDirectory(Path.Combine(legacy, "profiles"));
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "{\"old\":1}");
            File.WriteAllText(Path.Combine(legacy, "app.json"), "{}");
            File.WriteAllText(Path.Combine(legacy, "profiles", "p-abc.json"), "{}");
            var loc = DataLocations.Resolve(null, program, appData);
            Assert.Equal(Path.Combine(appData, "Backpack"), loc.Directory);
            Assert.Equal(legacy, loc.MigratedFrom);
            Assert.Equal("{\"old\":1}", File.ReadAllText(loc.SettingsPath));
            Assert.True(File.Exists(Path.Combine(loc.Directory, "profiles", "p-abc.json")));
            Assert.True(File.Exists(Path.Combine(legacy, "settings.json")));

            // Повторный запуск: новые настройки не перезаписываются.
            File.WriteAllText(loc.SettingsPath, "{\"new\":1}");
            var second = DataLocations.Resolve(null, program, appData);
            Assert.Null(second.MigratedFrom);
            Assert.Equal("{\"new\":1}", File.ReadAllText(second.SettingsPath));

            // Portable: метка + доступная папка → Data рядом с программой.
            File.WriteAllText(Path.Combine(program, DataLocations.PortableMarker), "");
            var portable = DataLocations.Resolve(null, program, appData);
            Assert.True(portable.Portable);
            Assert.Equal(Path.Combine(program, "Data"), portable.Directory);

            // Метка есть, но Data создать нельзя (на её месте файл) → %APPDATA% и признак.
            Directory.Delete(Path.Combine(program, "Data"));
            File.WriteAllText(Path.Combine(program, "Data"), "");
            var fallback = DataLocations.Resolve(null, program, appData);
            Assert.False(fallback.Portable);
            Assert.True(fallback.PortableUnavailable);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // ── логотип ──────────────────────────────────────────────────────────────

    [Fact]
    public void Logo_IcoHasAllSizes_SvgHasThreeEmptyFramesAndNoText()
    {
        Sta.Run(() =>
        {
            int[] sizes = [16, 20, 24, 32, 48, 256];
            var ico = LogoArt.Ico(sizes);
            Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
            Assert.Equal(sizes.Length, BitConverter.ToUInt16(ico, 4));
            for (int i = 0; i < sizes.Length; i++)
            {
                int w = ico[6 + (16 * i)];
                Assert.Equal(sizes[i] == 256 ? 0 : sizes[i], w);
            }

            string svg = LogoArt.ToSvg();
            Assert.DoesNotContain("<text", svg);
            // Backpack (Phase 4E): только контур рюкзака без подложки — линии без заливки, без градиента и прямоугольника фона.
            Assert.Contains("<title>Backpack</title>", svg);
            Assert.DoesNotContain("<rect", svg);
            Assert.DoesNotContain("linearGradient", svg);
            Assert.Equal(LogoArt.Spec.Full.Paths.Count, System.Text.RegularExpressions.Regex.Matches(svg, "<path ").Count);
            Assert.Contains("fill=\"none\" stroke=\"" + LogoArt.IconColor + "\"", svg);

            var bmp = LogoArt.Render(16, simplified: true);
            var px = new byte[16 * 16 * 4];
            bmp.CopyPixels(px, 16 * 4, 0);
            int opaque = 0, corners = px[3] + px[(15 * 4) + 3] + px[(15 * 16 * 4) + 3] + px[(255 * 4) + 3];
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] > 200)
                {
                    opaque++;
                }
            }

            Assert.Equal(0, corners);                // углы прозрачны: подложки нет
            Assert.InRange(opaque, 20, 140);         // контур виден и на 16 px, но не заливает значок
        });
    }

    // ── главное окно ─────────────────────────────────────────────────────────

    [Fact]
    public void MainWindow_GroupExpands_ScrollAreaNeverOverlapsPreview_AtSmallAndLargeHeights()
    {
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(TempDir(), "settings.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Home)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            w.Show();
            try
            {
                // Phase 4E: подстраница («Положение и мониторы») — свой пункт раскрытой группы «Клавиши».
                w.Show(SettingsSection.Mouse);
                Assert.True(w.NavItems.Single(n => (SettingsSection)n.Tag == SettingsSection.Mouse).IsChecked);
                w.Show(SettingsSection.Position);
                Assert.True(w.NavItems.Single(n => (SettingsSection)n.Tag == SettingsSection.Position).IsChecked);
                Assert.True(w.IsExpanded(SettingsSection.Keys));
                w.Show(SettingsSection.Mouse);

                // Phase 4F: две компоновки. Широкая — превью колонкой фиксированной ширины справа на всю высоту области страницы
                // (не пересекает прокручиваемую область); узкая — превью внутри страницы над карточками, на всю ширину содержимого.
                foreach (var (width, height) in new[] { (1400.0, 480.0), (1400.0, 820.0), (820.0, 480.0), (820.0, 820.0) })
                {
                    w.Width = width;
                    w.Height = height;
                    w.UpdateLayout();
                    // The native off-screen HWND can be constrained by a hosted runner's virtual screen.
                    // This test checks logical content layout at each requested size, independent of that screen.
                    var layoutRoot = (FrameworkElement)w.Content;
                    layoutRoot.Measure(new Size(width, height));
                    layoutRoot.Arrange(new Rect(0, 0, width, height));
                    layoutRoot.UpdateLayout();
                    var card = w.PreviewCard!;
                    var page = w.Cards;
                    Assert.Equal(width >= 1200, w.PreviewOnRight);
                    // Превью закреплено в обеих компоновках: вне прокручиваемой области и не пересекается с ней.
                    var c = card.TransformToAncestor(layoutRoot).TransformBounds(new Rect(card.RenderSize));
                    var a = w.ScrollArea!.TransformToAncestor(layoutRoot).TransformBounds(new Rect(w.ScrollArea.RenderSize));
                    Assert.False(Rect.Intersect(c, a) is { IsEmpty: false, Width: > 0.5, Height: > 0.5 }, $"{width}×{height}: {a} ∩ {c}");
                    Assert.False(page.IsAncestorOf(card));
                    if (w.PreviewOnRight)
                    {
                        Assert.Equal(SettingsWindow.PreviewColumnWidth, c.Width, 1);
                        Assert.Equal(a.Height - 24, c.Height, 1);        // на всю высоту области страницы (минус нижний отступ)
                        Assert.Equal(a.Top, c.Top, 1);
                    }
                    else
                    {
                        Assert.True(c.Bottom <= a.Top + 0.5);           // полоса над областью прокрутки
                        Assert.Equal(a.Width - 32 - 24, c.Width, 1);       // ширина содержимого (поля как у карточек)
                    }

                    Assert.True(w.ScrollArea!.ClipToBounds);
                    Assert.IsType<RectangleGeometry>(((FrameworkElement)card.Child).Clip); // настоящая обрезка по скруглению
                }
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    [Fact]
    public void Home_DisplaySwitch_CallsHost_AndHotkeyRecordingSuspendsManager()
    {
        Sta.Run(() =>
        {
            string dir = TempDir();
            var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
            bool visible = true;
            var api = new FakeApi();
            using var hk = new HotkeyManager(api, createWindow: false);
            hk.Apply(HotkeySettings.None);
            var reg = new KeyOverlay.Visual.Commands.CommandRegistry();
            foreach (var (id, c) in new[] { ("display.toggle", 'O'), ("tool.focus", 'Z'), ("tool.drawing", 'D'), ("tool.radial", 'R') })
            {
                reg.Register(new KeyOverlay.Visual.Commands.CommandDef(id, id, "Commands.Category.App", KeyOverlay.Visual.Commands.CommandScope.Global,
                    KeyOverlay.Visual.Commands.CommandKind.Action, () => { }, DefaultHotkey: new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, c)));
            }

            hk.ApplyCommands(reg.Bindings(prefs.Current.BindingsOrEmpty));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => "", DisplayVisible: () => visible, SetDisplayVisible: v => visible = v, Commands: reg),
                SettingsSection.Home, prefs: prefs, hotkeys: hk);
            var toggle = Descendants<CheckBox>(w.Cards).First();
            toggle.IsChecked = false;
            Assert.False(visible);

            w.Show(SettingsSection.Hotkeys);
            var box = Descendants<TextBox>(w.Cards).First(t => t.Tag is "tool.focus");
            Assert.Equal(4, hk.RegisteredCount);
            box.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, null, box) { RoutedEvent = UIElement.GotKeyboardFocusEvent });
            Assert.Equal("tool.focus", w.RecordingCommand);
            Assert.True(hk.IsSuspended);
            Assert.Equal(0, hk.RegisteredCount);
            w.ReleaseResources(); // закрытие во время записи возвращает регистрации
            Assert.False(hk.IsSuspended);
            Assert.Equal(4, hk.RegisteredCount);
            w.Close();
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
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
