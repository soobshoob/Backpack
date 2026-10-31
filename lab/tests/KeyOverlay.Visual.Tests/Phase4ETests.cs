using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using KeyOverlay.Visual.Commands;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Layout;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Tray;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4E: команды «Фокуса» и миграция сочетаний, предварительное распознавание умного карандаша и история, многоугольники,
/// правильный многоугольник и скруглённый прямоугольник, выбор и перемещение, вложенное радиальное меню, settings v6 и
/// app.json v4 (шрифты, дополнительные подписи, подсветка курсора, материал), дополнительные подписи раскладки.
/// Окна рисования создаются за пределами экрана; ввод в систему не вводится.
/// </summary>
public class Phase4ETests
{
    private static DrawingWindow Window(DrawSettings? s = null)
    {
        var w = new DrawingWindow(new PixelRect(-32000, -32000, 1200, 800), 1.0, s ?? DrawSettings.Default, DrawingProfile.Default);
        Layout(w);
        return w;
    }

    private static void Layout(Window w)
    {
        if (w.Content is FrameworkElement root)
        {
            root.Measure(new Size(1200, 800));
            root.Arrange(new Rect(0, 0, 1200, 800));
        }
    }

    private static List<Point> Circle(double cx, double cy, double r, int n = 72, double wobble = 0.03) =>
        Enumerable.Range(0, n + 1).Select(i =>
        {
            double a = 2 * Math.PI * i / n;
            double k = 1 + (wobble * Math.Sin(a * 7));
            return new Point(cx + (r * k * Math.Cos(a)), cy + (r * k * Math.Sin(a)));
        }).ToList();

    /// <summary>Замкнутый штрих по вершинам многоугольника с дрожанием руки.</summary>
    private static List<Point> PolygonPath(IReadOnlyList<Point> v, double jitter = 2.5, int perSide = 18)
    {
        var rnd = new Random(11);
        var pts = new List<Point>();
        for (int s = 0; s < v.Count; s++)
        {
            var a = v[s];
            var b = v[(s + 1) % v.Count];
            for (int i = 0; i < perSide; i++)
            {
                var p = a + ((b - a) * (i / (double)perSide));
                pts.Add(new Point(p.X + ((rnd.NextDouble() - 0.5) * jitter), p.Y + ((rnd.NextDouble() - 0.5) * jitter)));
            }
        }

        pts.Add(new Point(v[0].X + 2, v[0].Y + 1));
        return pts;
    }

    private static IReadOnlyList<Point> Regular(int n, double cx = 300, double cy = 300, double r = 120, double rot = -Math.PI / 2) =>
        Enumerable.Range(0, n).Select(i => new Point(cx + (r * Math.Cos(rot + (2 * Math.PI * i / n))), cy + (r * Math.Sin(rot + (2 * Math.PI * i / n))))).ToList();

    // ── распознавание многоугольников ────────────────────────────────────────

    [Fact]
    public void Recognizer_finds_drawn_polygons_but_keeps_circles_and_rectangles_as_before()
    {
        foreach (int n in new[] { 3, 5, 6 })
        {
            var r = ShapeRecognizer.Recognize(PolygonPath(Regular(n)));
            Assert.Equal(RecognizedKind.Polygon, r?.Kind);
            Assert.Equal(n, r!.Vertices!.Count);
        }

        // Повёрнутый квадрат («ромб») — многоугольник; прямоугольник по осям — прямоугольник; окружность — эллипс.
        var diamond = ShapeRecognizer.Recognize(PolygonPath(Regular(4, rot: 0)));
        Assert.Equal(RecognizedKind.Polygon, diamond?.Kind);
        Assert.Equal(4, diamond!.Vertices!.Count);
        Assert.Equal(RecognizedKind.Rectangle, ShapeRecognizer.Recognize(PolygonPath([new(100, 100), new(400, 100), new(400, 260), new(100, 260)]))?.Kind);
        Assert.Equal(RecognizedKind.Ellipse, ShapeRecognizer.Recognize(Circle(300, 300, 110))?.Kind);

        // Масштабонезависимо.
        foreach (double k in new[] { 0.5, 2.0 })
        {
            var scaled = Regular(5, 300 * k, 300 * k, 120 * k);
            Assert.Equal(RecognizedKind.Polygon, ShapeRecognizer.Recognize(PolygonPath(scaled, jitter: 2.5 * k))?.Kind);
        }
    }

    // ── предварительное распознавание ────────────────────────────────────────

    [Fact]
    public void Smart_pen_preview_is_stable_adds_nothing_to_history_and_release_commits_one_step()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default);
            Assert.Equal(DrawTool.SmartPen, w.Settings.Tool); // новая конфигурация: умный карандаш по умолчанию
            var pts = Circle(500, 400, 140, n: 120);
            w.BeginStrokeForTest(pts[0]);
            int shown = -1;
            foreach (var p in pts.Skip(1))
            {
                w.ExtendStrokeForTest(p);
                if (w.Candidate is not null && shown < 0)
                {
                    shown = pts.IndexOf(p);
                }
            }

            // До отпускания: кандидат — эллипс, показан отдельно (пунктир), исходный штрих виден приглушённым; истории ещё нет.
            Assert.Equal(RecognizedKind.Ellipse, w.Candidate?.Kind);
            Assert.True(shown > 0, "кандидат появился до отпускания");
            Assert.True(w.CandidateSwitches <= 2, $"кандидат не мигает: {w.CandidateSwitches}");
            Assert.NotNull(w.CandidateElement);
            Assert.NotNull(w.CandidateElement!.StrokeDashArray);
            Assert.False(w.CanUndo);
            Assert.Equal(0, w.ShapeCount);

            w.EndStrokeForTest();
            Assert.Null(w.CandidateElement);                  // показ убран
            Assert.IsType<Ellipse>(w.Shapes.Single());        // результат зафиксирован
            w.Undo();
            Assert.IsType<Polyline>(w.Shapes.Single());       // отмена — исходный штрих (прежняя модель отмены)
            Assert.Equal(1.0, w.Shapes.Single().Opacity);
            w.Redo();
            Assert.IsType<Ellipse>(w.Shapes.Single());

            // Низкая уверенность: каракули — кандидата нет, остаётся штрих.
            var rnd = new Random(5);
            var scribble = Enumerable.Range(0, 90).Select(_ => new Point(rnd.Next(100, 300), rnd.Next(100, 300))).ToList();
            w.BeginStrokeForTest(scribble[0]);
            foreach (var p in scribble.Skip(1))
            {
                w.ExtendStrokeForTest(p);
            }

            Assert.Null(w.Candidate);
            w.EndStrokeForTest();
            Assert.IsType<Polyline>(w.Shapes.Last());
            w.Close();
        });
    }

    [Fact]
    public void Smart_pen_turns_a_drawn_pentagon_into_an_editable_polygon_with_undo()
    {
        Sta.Run(() =>
        {
            var w = Window();
            w.SmartStroke(PolygonPath(Regular(5)));
            var poly = Assert.IsType<Polygon>(w.Shapes.Single());
            Assert.Equal(5, poly.Points.Count);
            w.Undo();
            Assert.IsType<Polyline>(w.Shapes.Single());
            w.Close();
        });
    }

    // ── правильный многоугольник и скруглённый прямоугольник ─────────────────

    [Fact]
    public void Regular_polygon_tool_builds_n_vertices_validates_range_and_supports_undo()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = DrawTool.Polygon, PolygonSides = 7 });
            w.Draw(new Point(300, 300), new Point(420, 300));
            var poly = Assert.IsType<Polygon>(w.Shapes.Single());
            Assert.Equal(7, poly.Points.Count);
            Assert.All(poly.Points, p => Assert.Equal(120, (p - new Point(300, 300)).Length, 3));
            w.Undo();
            Assert.Equal(0, w.ShapeCount);
            w.Redo();
            Assert.Equal(1, w.ShapeCount);

            // Диапазон 3–12 проверяется и при чтении, и при построении.
            Assert.Equal(3, (DrawSettings.Default with { PolygonSides = 1 }).Sanitized().PolygonSides);
            Assert.Equal(12, (DrawSettings.Default with { PolygonSides = 40 }).Sanitized().PolygonSides);
            Assert.Equal(12, DrawingWindow.RegularPolygon(new Point(0, 0), new Point(10, 0), 99).Count);

            // Щелчок без протягивания фигуры не создаёт.
            w.Draw(new Point(500, 500), new Point(500.5, 500));
            Assert.Equal(1, w.ShapeCount);
            w.Close();
        });
    }

    [Fact]
    public void Rounded_rectangle_uses_radius_limited_by_half_of_the_smaller_side()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = DrawTool.RoundedRectangle, CornerRadius = 24 });
            w.Draw(new Point(100, 100), new Point(300, 200));
            var r = Assert.IsType<Rectangle>(w.Shapes.Single());
            Assert.Equal(24, r.RadiusX);
            w.SetSettings(w.Settings with { CornerRadius = 120 });
            w.Draw(new Point(100, 300), new Point(160, 330));
            Assert.Equal(15, ((Rectangle)w.Shapes.Last()).RadiusX); // не больше половины меньшей стороны (30 / 2)
            Assert.Equal(120, (DrawSettings.Default with { CornerRadius = 999 }).Sanitized().CornerRadius);
            w.Close();
        });
    }

    // ── выбор, перемещение и удаление ────────────────────────────────────────

    [Fact]
    public void Select_tool_moves_and_deletes_any_shape_as_undoable_steps()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = DrawTool.Polygon, PolygonSides = 6 });
            w.Draw(new Point(300, 300), new Point(380, 300));
            w.SetSettings(w.Settings with { Tool = DrawTool.RoundedRectangle });
            w.Draw(new Point(600, 100), new Point(700, 160));
            Layout(w);
            w.SetSettings(w.Settings with { Tool = DrawTool.Select });

            Assert.True(w.SelectAt(new Point(380, 300)));        // вершина шестиугольника
            Assert.IsType<Polygon>(w.SelectedElement);
            Assert.True(w.MoveSelected(new Vector(50, 20)));
            // Phase 4I: перемещение входит в геометрию (как размер и поворот), смещение элемента не используется.
            Assert.Equal(new Point(430, 320), w.Shapes.OfType<Polygon>().Single().Points[0]);
            Assert.Equal(2, w.ShapeCount);
            w.Undo();
            Assert.Equal(new Point(380, 300), w.Shapes.OfType<Polygon>().Single().Points[0]);
            w.Redo();

            Layout(w);
            Assert.True(w.SelectAt(new Point(600, 130)));        // край скруглённого прямоугольника
            Assert.True(w.DeleteSelected());
            Assert.Equal(1, w.ShapeCount);
            w.Undo();
            Assert.Equal(2, w.ShapeCount);
            Assert.False(w.SelectAt(new Point(1100, 700)));      // пусто — выбор снят
            w.Close();
        });
    }

    // ── радиальное меню ──────────────────────────────────────────────────────

    [Fact]
    public void Radial_menu_nested_rings_back_and_escape_and_no_accidental_neighbour()
    {
        Sta.Run(() =>
        {
            int executed = 0;
            bool available = false;
            var root = new List<RadialNode>
            {
                new("cat.a", "A", "pencil", Children: [new("a.1", "A1", "pencil", () => executed++), new("a.2", "A2", "square", () => executed += 10)]),
                new("off", "Off", "x", () => executed += 100, IsAvailable: () => available),
                new("b", "B", "circle", () => executed += 1000),
            };
            var menu = new RadialMenu(root);
            Assert.False(menu.SubRingOpen);
            Assert.True(menu.Activate("cat.a"));                  // категория раскрывает внешнее кольцо
            Assert.True(menu.SubRingOpen);
            Assert.Equal("cat.a", menu.SelectedCategory!.Id);      // выбранный сектор отмечен
            menu.Back();                                           // Esc/центр: сначала закрывается внешнее кольцо
            Assert.False(menu.SubRingOpen);
            bool dismissed = false;
            menu.Dismissed += () => dismissed = true;
            menu.Back();                                           // затем — само меню
            Assert.True(dismissed);

            Assert.True(menu.Activate("a.2"));
            Assert.Equal(10, executed);
            Assert.True(menu.Activate("off"));                     // недоступная по контексту команда не выполняется
            Assert.Equal(10, executed);
            available = true;
            menu.Activate("off");
            Assert.Equal(110, executed);

            // Нажатие на одном секторе и отпускание на соседнем ничего не выполняет.
            menu.ResetLevel();
            var wedges = menu.Children.OfType<Canvas>().SelectMany(c => c.Children.OfType<System.Windows.Shapes.Path>()).Where(p => p.Tag is RadialNode).ToList();
            var first = wedges.First(p => ((RadialNode)p.Tag).Id == "b");
            var other = wedges.First(p => ((RadialNode)p.Tag).Id == "off");
            first.RaiseEvent(new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            other.RaiseEvent(new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Assert.Equal(110, executed);
            first.RaiseEvent(new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            first.RaiseEvent(new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Assert.Equal(1110, executed);
        });
    }

    [Fact]
    public void Radial_menu_stays_fully_reachable_at_edges_and_shrinks_on_small_areas()
    {
        var area = new Size(1600, 900);
        foreach (var p in new[] { new Point(0, 0), new Point(1600, 900), new Point(-500, 450), new Point(800, 2000) })
        {
            var (c, scale) = RadialMenu.Fit(p, area);
            Assert.Equal(1, scale);
            Assert.InRange(c.X, RadialMenu.FullRadius, area.Width - RadialMenu.FullRadius);
            Assert.InRange(c.Y, RadialMenu.FullRadius, area.Height - RadialMenu.FullRadius);
        }

        var small = new Size(300, 260);
        var (center, s) = RadialMenu.Fit(new Point(10, 10), small);
        Assert.True(s < 1);
        Assert.True(center.X - (RadialMenu.FullRadius * s) >= 0 && center.Y - (RadialMenu.FullRadius * s) >= -0.5);
    }

    [Fact]
    public void Drawing_radial_catalog_contains_all_tools_categories_and_app_commands()
    {
        Sta.Run(() =>
        {
            var w = Window();
            w.RadialCatalog = () => [new RadialNode("cat.focus", "Focus", "focus", Children: [new RadialNode("tool.focus", "Focus", "focus", () => { })])];
            var catalog = w.DrawingCatalog();
            var all = catalog.SelectMany(n => n.Children ?? [n]).Select(n => n.Id).ToList();
            foreach (var t in Enum.GetValues<DrawTool>())
            {
                Assert.Contains("tool." + t, all);
            }

            Assert.Contains("tool.focus", all);
            Assert.Contains(catalog, n => n.Id == "cat.color" && n.Children!.Any(c => c.Swatch is not null));
            w.ShowRadial(new Point(5, 5));
            Assert.True(w.RadialOpen);
            Assert.True(w.Radial!.Activate("tool.Polygon"));
            Assert.Equal(DrawTool.Polygon, w.Settings.Tool);
            Assert.False(w.RadialOpen);
            w.Close();
        });
    }

    // ── компактная панель, цвет, запоминание ─────────────────────────────────

    [Fact]
    public void Compact_toolbar_keeps_tool_and_colour_visible_and_changes_are_reported()
    {
        Sta.Run(() =>
        {
            var w = new DrawingWindow(new PixelRect(-32000, -32000, 1200, 800), 1.0, DrawSettings.Default with { Tool = DrawTool.Polygon }, DrawingProfile.Default with { Compact = true });
            var changes = new List<DrawSettings>();
            w.SettingsChanged += changes.Add;
            var buttons = w.ToolbarHost.Child is Panel p ? p.Children.OfType<Button>().ToList() : [];
            // Категория «Фигуры» показывает текущий инструмент и отмечена; кнопка цвета показывает текущий цвет.
            Assert.Contains(buttons, b => (string?)b.Tag == "selected" && ((string)b.ToolTip).Contains(Localization.Loc.T("Draw.Tool.Polygon")));
            Assert.Contains(buttons, b => b.Content is Ellipse e && ((System.Windows.Media.SolidColorBrush)e.Fill).Color.R == DrawSettings.Default.Color.R);
            Assert.True(buttons.Count < 14, $"компактная панель: {buttons.Count}");
            w.Radial?.Dismiss();
            w.ShowRadial(new Point(600, 400));
            w.Radial!.Activate("tool.Text");
            Assert.Equal(DrawTool.Text, changes.Last().Tool);   // приложение запоминает последний выбор
            w.Close();
        });
    }

    // ── форматы: settings v6, app.json v4 ────────────────────────────────────

    [Fact]
    public void Settings_v6_round_trip_fonts_labels_highlight_compact_and_v5_migrates_to_defaults()
    {
        var s = LabSettings.Default with
        {
            Fonts = new FontSettings(FontSettings.Spectral, null),
            Labels = new SecondaryLabelSettings(SecondaryLabelMode.FixedLayout, "04220422"),
            MouseCursor = LabSettings.Default.MouseCursor with { ShowHighlight = true, HighlightColor = new ThemeColor(0xFF, 0xD4, 0x00, 0x66), HighlightSize = 90 },
            Drawing = DrawingProfile.Default with { Compact = true },
        };
        var r = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(r.Problems);
        Assert.Equal(s, r.Settings);

        // v5 (без новых разделов) — значения по умолчанию, без ошибок.
        var json = SettingsSerializer.ToJson(LabSettings.Default);
        json["formatVersion"] = 5;
        json.Remove("fonts");
        json.Remove("labels");
        var old = SettingsSerializer.Read(json.ToJsonString());
        Assert.Equal(5, old.MigratedFrom);
        Assert.Equal(FontSettings.Default, old.Settings.FontsOrDefault);
        Assert.Equal(SecondaryLabelMode.Off, old.Settings.LabelsOrDefault.Mode);
        Assert.False(old.Settings.MouseCursor.ShowHighlight);

        // Включённый постоянный «круг щелчка» v5 становится подсветкой курсора с теми же цветом и размером (вид не меняется молча).
        var withCircle = SettingsSerializer.ToJson(LabSettings.Default with { MouseCursor = LabSettings.Default.MouseCursor with { Enabled = true, ShowCircle = true } });
        withCircle["formatVersion"] = 5;
        var migrated = SettingsSerializer.Read(withCircle.ToJsonString()).Settings;
        Assert.True(migrated.MouseCursor.ShowHighlight);
        Assert.Equal(MouseVisual.Default.ClickColor, migrated.MouseCursor.Highlight);
        Assert.Equal(MouseVisual.Default.ClickSize, migrated.MouseCursor.HighlightSize);

        // Неверные значения: имя шрифта с запрещёнными символами и неверная раскладка заменяются безопасными.
        Assert.Null(FontSettings.NormalizeName("bad,font"));
        Assert.Equal(SecondaryLabelMode.ActiveLayout, new SecondaryLabelSettings(SecondaryLabelMode.FixedLayout, "zz").Sanitized().Mode);
    }

    [Fact]
    public void App_v4_material_round_trip_sanitizes_and_defaults_glow_on()
    {
        Assert.True(AppPreferences.Default.MaterialOrDefault.Glow);
        var p = AppPreferences.Default with { Material = new AppearanceMaterial(WindowMaterial.Acrylic, 0.8, false, 0.2, SurfaceStyle.Neon) };
        Assert.Equal(p, AppPreferencesStore.Parse(AppPreferencesStore.Write(p)));
        // Phase 4H: прежний материал Liquid Glass — теперь стиль поверхностей поверх Acrylic.
        var glass = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":4,"material":{"material":"liquidGlass","intensity":0.5,"glow":true,"glowIntensity":0.4}}""")!;
        Assert.Equal(WindowMaterial.Acrylic, glass.MaterialOrDefault.Material);
        Assert.Equal(SurfaceStyle.LiquidGlass, glass.MaterialOrDefault.Surface);
        var bad = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":4,"material":{"material":"mica","intensity":7,"glow":true,"glowIntensity":-1}}""")!;
        Assert.Equal(1, bad.MaterialOrDefault.Intensity);
        Assert.Equal(0, bad.MaterialOrDefault.GlowIntensity);
        Assert.Equal(DrawTool.SmartPen, DrawSettings.Default.Tool);
        // Явный выбор пользователя в прежнем файле сохраняется (не переводится на умный карандаш).
        var old = AppPreferencesStore.Parse("""{"format":"keyoverlay-lab-app","formatVersion":3,"draw":{"tool":"pen","color":"#FF3B30","thickness":4,"dash":"solid","fadeSec":0}}""")!;
        Assert.Equal(DrawTool.Pen, old.DrawOrDefault.Tool);
    }

    [Fact]
    public void Material_and_glow_follow_gem_themes_and_fall_back_honestly()
    {
        Sta.Run(() =>
        {
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque));
            Assert.Equal(WindowMaterial.Opaque, UiTheme.EffectiveMaterial);
            Assert.Equal(MaterialFallback.None, UiTheme.Fallback);
            UiTheme.SetPalette("emerald");
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque, Glow: true));
            Assert.True(UiTheme.GlowActive == !SystemParameters.HighContrast);
            Assert.Equal(UiTheme.GlowActive, UiTheme.Resources["Ko.AccentGlowEffect"] is System.Windows.Media.Effects.DropShadowEffect);
            UiTheme.SetPalette("classic");                       // «Классическая» — не драгоценный камень: свечения нет
            Assert.False(UiTheme.GlowActive);
            Assert.Null(UiTheme.Resources["Ko.AccentGlowEffect"]);
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque, Surface: SurfaceStyle.Neon)); // Neon светится при любой палитре
            Assert.True(UiTheme.GlowActive == !SystemParameters.HighContrast);
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Mica));
            Assert.Equal(UiTheme.SystemBackdropSupported && UiTheme.TransparencyEnabled() && !SystemParameters.HighContrast ? WindowMaterial.Mica : WindowMaterial.Opaque, UiTheme.EffectiveMaterial);
            UiTheme.SetMaterial(new AppearanceMaterial(WindowMaterial.Opaque));
            UiTheme.SetPalette("emerald");
        });
    }

    [Theory]
    [InlineData("emerald")]
    [InlineData("classic")]
    [InlineData("sapphire")]
    [InlineData("amber")]
    public void Neon_is_the_darkest_base_with_edge_hierarchy_and_readable_text(string id)
    {
        var palette = UiPalettes.Get(id);
        foreach (bool dark in new[] { false, true })
        {
            var v = new Dictionary<string, uint>(dark ? palette.Dark : palette.Light);
            UiPalettes.ApplyNeon(v, dark);
            static byte A(uint c) => (byte)(c >> 24);
            Assert.True(A(v["Ko.BorderBrush"]) < A(v["Ko.CardBorderBrush"]));
            Assert.True(A(v["Ko.CardBorderBrush"]) < A(v["Ko.ControlBorderBrush"]));
            Assert.True(A(v["Ko.ControlBorderBrush"]) < A(v["Ko.SelectedBorderBrush"]));
            Assert.True(UiPalettes.Contrast(v["Ko.TextBrush"], v["Ko.CardBrush"]) >= 7);
            Assert.True(UiPalettes.Contrast(v["Ko.SecondaryTextBrush"], v["Ko.WindowBrush"] & 0xFFFFFF) >= 4.5);
            if (dark)
            {
                // Самая тёмная основа среди предлагаемых: темнее тёмной основы любой палитры.
                double neon = UiPalettes.Luminance(v["Ko.WindowBrush"]);
                Assert.All(UiPalettes.List, p => Assert.True(neon < UiPalettes.Luminance(p.Dark["Ko.WindowBrush"])));
            }
            else
            {
                Assert.NotEqual(palette.Light["Ko.WindowBrush"], v["Ko.WindowBrush"]); // свои светлые цвета, а не исходные
            }
        }
    }

    [Fact]
    public void Logo_colour_is_the_theme_accent_and_classic_is_black_or_white()
    {
        Assert.Equal(UiPalettes.Emerald.Accent, UiPalettes.LogoColor("emerald", dark: false));
        Assert.Equal(UiPalettes.Emerald.Glow, UiPalettes.LogoColor("emerald", dark: true));
        Assert.Equal(0x111113u, UiPalettes.LogoColor("classic", dark: false));
        Assert.Equal(0xF2F2F4u, UiPalettes.LogoColor("classic", dark: true));
        Assert.NotEqual(UiPalettes.LogoColor("sapphire", false), UiPalettes.LogoColor("amber", false));
    }

    // ── команды ──────────────────────────────────────────────────────────────

    [Fact]
    public void Plugin_commands_run_from_hotkey_tray_and_radial_through_one_registry_entry()
    {
        Sta.Run(() =>
        {
            int runs = 0;
            var reg = new CommandRegistry();
            var hk = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'Z');
            reg.Register(new CommandDef("plugin.com.example.focus.start", "Focus: dim", "Commands.Category.Plugins", CommandScope.Global, CommandKind.Action, () => runs++,
                DefaultHotkey: hk, Source: "com.example.focus"));

            // 1) сочетание (регистрация и WM_HOTKEY), 2) подменю «Плагины» трея, 3) радиальное меню — одна команда реестра.
            using var manager = new HotkeyManager(new FakeHotkeys(), createWindow: false);
            manager.CommandPressed += id => reg.Execute(id);
            manager.Apply(HotkeySettings.None);
            manager.ApplyCommands(reg.Bindings(CommandBindings.Empty));
            Assert.True(manager.RaiseCommand("plugin.com.example.focus.start"));
            var store = SettingsStore.Load(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ko-4e-" + Guid.NewGuid().ToString("N")[..8], "settings.json"));
            var menu = TrayMenu.Build(store, null, new TrayCommands(() => { }, () => { }, () => true, _ => { }, () => { }, () => { }, reg));
            Assert.Null(TrayMenu.Find(menu, "tray.tool-focus")); // прежнего встроенного пункта нет
            var plugins = TrayMenu.Find(menu, "tray_plugins")!;
            ((System.Windows.Controls.MenuItem)plugins.Items[0]!).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            var radial = new RadialMenu([new RadialNode("plugin.com.example.focus.start", "Focus", "focus", () => reg.Execute("plugin.com.example.focus.start"))]);
            radial.Activate("plugin.com.example.focus.start");
            Assert.Equal(3, runs);
        });
    }

    private sealed class FakeHotkeys : IHotkeyApi
    {
        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            return true;
        }

        public bool Unregister(nint hwnd, int id) => true;
    }

    // ── дополнительные подписи раскладки ─────────────────────────────────────

    [Fact]
    public void Secondary_labels_come_from_installed_layouts_and_are_suppressed_when_equal()
    {
        var layouts = KeyboardLayouts.Installed();
        SecondaryLabels.Settings = new SecondaryLabelSettings(SecondaryLabelMode.Off);
        Assert.Null(SecondaryLabels.For("A", false));            // выключено
        try
        {
            if (layouts.FirstOrDefault(l => l.Id.EndsWith("0422", StringComparison.Ordinal)) is { } uk)
            {
                SecondaryLabels.Settings = new SecondaryLabelSettings(SecondaryLabelMode.FixedLayout, uk.Id);
                Assert.Equal("Ф", SecondaryLabels.For("A", false));
                Assert.Equal("Й", SecondaryLabels.For("Q", false));
                Assert.Equal("Х", SecondaryLabels.For("[", false));
                Assert.Equal("І", SecondaryLabels.For("S", false));
                Assert.Equal(SecondaryLabels.For("A", false), SecondaryLabels.For("A", false)); // повтор — то же (состояние не меняется)
                Assert.Null(SecondaryLabels.For("1", false));        // совпадает с основной подписью — не дублируется
                Assert.Null(SecondaryLabels.For("Enter", false));    // служебная клавиша
                Assert.Null(SecondaryLabels.For("7", true));         // цифровой блок
            }

            if (layouts.FirstOrDefault(l => l.IsEnglish) is { } en)
            {
                SecondaryLabels.Settings = new SecondaryLabelSettings(SecondaryLabelMode.FixedLayout, en.Id);
                Assert.Null(SecondaryLabels.For("A", false));        // английская раскладка — нет повтора
            }

            SecondaryLabels.Settings = new SecondaryLabelSettings(SecondaryLabelMode.ActiveLayout);
            SecondaryLabels.ActiveLayout = () => 0;
            Assert.Null(SecondaryLabels.For("A", false));            // нет данных о раскладке — нет подписи
        }
        finally
        {
            SecondaryLabels.Settings = SecondaryLabelSettings.Default;
            SecondaryLabels.ActiveLayout = null;
        }
    }

    [Fact]
    public void Keycap_font_change_rebuilds_labels_and_unknown_fonts_become_standard()
    {
        Sta.Run(() =>
        {
            try
            {
                VisualFonts.SetKeycapFamily("Font That Does Not Exist 4E");
                Assert.Null(VisualFonts.KeycapFamilyName);                       // прежнее произвольное семейство → «Стандартный»
                Assert.DoesNotContain("Font That Does Not Exist", VisualFonts.Keycap.Source);
                VisualFonts.SetKeycapFamily("spectral");
                Assert.Equal(FontSettings.Spectral, VisualFonts.KeycapFamilyName);
                Assert.StartsWith("./#Spectral, ./#Inter", VisualFonts.Keycap.Source); // замена отсутствующих символов — Inter
                var k = KeycapFactory.Create(KeycapRenderer.LowProfile);
                k.Apply(AppearanceSettings.Default, new KeycapItem(1, 1, "Ж", false, false), showBadge: false);
                Assert.Equal(VisualFonts.Keycap, k.LabelArea.Children.OfType<TextBlock>().First().FontFamily);
                Assert.DoesNotContain("Spectral", UiTheme.UiFontFamily("Missing Family").Source);
            }
            finally
            {
                VisualFonts.SetKeycapFamily(null);
            }
        });
    }
}
