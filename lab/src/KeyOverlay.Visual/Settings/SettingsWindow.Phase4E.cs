using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KeyOverlay.Visual.Layout;
using KeyOverlay.Visual.Localization;

namespace KeyOverlay.Visual.Settings;

/// <summary>
/// Phase 4E: страницы «Оформление» (тема окон, материал, свечение, шрифт интерфейса), «Приватность», «Увеличение у курсора»
/// и карточки шрифта и дополнительных подписей кейкапов на странице «Стиль кейкапов».
/// </summary>
public sealed partial class SettingsWindow
{
    /// <summary>Образец шрифта: латиница, кириллица, греческий, цифры и сочетание (не переводится — проверяет символы шрифта).</summary>
    public const string FontSample = "Backpack · Рюкзак · Ґанок · Σακίδιο · 0123456789 — Ctrl + Shift + S";

    private void SetMaterial(Func<AppearanceMaterial, AppearanceMaterial> f) => SetPrefs4(p => p with { Material = f(p.MaterialOrDefault) });

    private void Fonts(Func<FontSettings, FontSettings> f) => U(x => x with { Fonts = f(x.FontsOrDefault) });

    private void Labels(Func<SecondaryLabelSettings, SecondaryLabelSettings> f) => U(x => x with { Labels = f(x.LabelsOrDefault) });

    // ── «Оформление» ─────────────────────────────────────────────────────────

    private void BuildInterface()
    {
        var p = _prefs!.Current;
        var palettes = UiPalettes.List;
        _cards.Children.Add(_ui.Card(Loc.T("App.Palette.Title"), Loc.T("App.Palette.Hint"),
            _ui.Row(Loc.T("App.Palette"), null, _ui.Segmented(Math.Max(0, palettes.ToList().FindIndex(x => x.Id == p.ColorTheme)), palettes.Select(x => Loc.T(x.NameKey)).ToArray(),
                i => SetPrefs4(x => x with { ColorTheme = palettes[i].Id }))),
            _ui.Row(Loc.T("App.Theme"), Loc.T("App.Theme.Hint"), _ui.Segmented((int)p.Theme, [Loc.T("App.Theme.System"), Loc.T("App.Theme.Light"), Loc.T("App.Theme.Dark")],
                i => SetPrefs(x => x with { Theme = (UiThemeMode)i })))));

        var m = p.MaterialOrDefault;

        // Phase 4H: основа, палитра, стиль поверхностей и материал — независимые настройки, а не набор готовых тем.
        var surfaces = Enum.GetValues<SurfaceStyle>();
        var surfaceRows = new List<UIElement>
        {
            _ui.Row(Loc.T("Surface.Kind"), Loc.T("Surface." + m.Surface + ".Hint"),
                _ui.Segmented(Array.IndexOf(surfaces, m.Surface), surfaces.Select(x => Loc.T("Surface." + x)).ToArray(), i => SetMaterial(x => x with { Surface = surfaces[i] }))),
        };
        if (SystemParameters.HighContrast && m.Surface != SurfaceStyle.Standard)
        {
            surfaceRows.Add(_ui.Note(Loc.T("Surface.HighContrast")));
        }

        _cards.Children.Add(_ui.Card(Loc.T("Surface.Title"), Loc.T("Surface.TitleHint"), surfaceRows.ToArray()));

        var materials = AppearanceMaterial.Choices;
        var intensity = _ui.Row(Loc.T("Material.Intensity"), Loc.T("Material.IntensityHint"),
            _ui.Number(Math.Round(m.Intensity * 100), (0, 100), 5, "%", v => SetMaterial(x => x with { Intensity = v / 100 })));
        intensity.IsEnabled = m.Material != WindowMaterial.Opaque;
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Material.Kind"), Loc.T("Material." + m.Material + ".Hint"),
                _ui.Segmented(Array.IndexOf(materials, m.Material), materials.Select(x => Loc.T("Material." + x)).ToArray(), i => SetMaterial(x => x with { Material = materials[i] }))),
            intensity,
            _ui.Note(MaterialStatus()),
            _ui.Note(Loc.T("Material.Note")),
        };
        _cards.Children.Add(_ui.Card(Loc.T("Material.Title"), Loc.T("Material.Hint"), rows.ToArray()));

        bool glowing = UiPalettes.IsGem(p.ColorTheme) || m.Surface == SurfaceStyle.Neon;
        var glowIntensity = _ui.Row(Loc.T("Glow.Intensity"), null, _ui.Number(Math.Round(m.GlowIntensity * 100), (0, 100), 5, "%", v => SetMaterial(x => x with { GlowIntensity = v / 100 })));
        glowIntensity.IsEnabled = glowing && m.Glow;
        var glow = _ui.Row(Loc.T("Glow.Enable"), glowing ? Loc.T("Glow.EnableHint") : Loc.T("Glow.NotGem"), _ui.Toggle(m.Glow, v => SetMaterial(x => x with { Glow = v })));
        glow.IsEnabled = glowing;
        _cards.Children.Add(_ui.Card(Loc.T("Glow.Title"), null, glow, glowIntensity));

        var f = _store.Current.FontsOrDefault;
        var sample = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16, Margin = new Thickness(0, 6, 0, 0) };
        sample.SetResourceReference(TextBlock.FontFamilyProperty, "Ko.HeadingFont");
        sample.Text = FontSample;
        var fontRows = new List<UIElement>
        {
            _ui.Row(Loc.T("Fonts.Ui"), Loc.T("Fonts.UiHint"), _ui.FontPicker(f.Ui, v => Fonts(x => x with { Ui = v }))),
            sample,
            _ui.Note(Loc.T("Fonts.UiNote")),
        };
        _cards.Children.Add(_ui.Card(Loc.T("Fonts.Ui.Title"), null, fontRows.ToArray()));

        _cards.Children.Add(_ui.Card(Loc.T("App.Home.Title"), null,
            _ui.Row(Loc.T("App.ReducedMotion"), Loc.T("App.ReducedMotionHint"), _ui.Toggle(p.ReducedMotion, v => SetPrefs4(x => x with { ReducedMotion = v }))),
            _ui.Row(Loc.T("App.Home.Animation"), Loc.T("App.Home.AnimationHint"), _ui.Toggle(p.HomeAnimation, v => _prefs.Update(x => x with { HomeAnimation = v })))));
    }

    /// <summary>Честное состояние материала: действует выбранный или почему используется непрозрачный вариант.</summary>
    private static string MaterialStatus() => UiTheme.Fallback switch
    {
        MaterialFallback.NotSupported => Loc.T("Material.Fallback.NotSupported"),
        MaterialFallback.TransparencyOff => Loc.T("Material.Fallback.TransparencyOff"),
        MaterialFallback.HighContrast => Loc.T("Material.Fallback.HighContrast"),
        _ => Loc.F("Material.Active", Loc.T("Material." + UiTheme.EffectiveMaterial)),
    };

    // ── материал подложек кейкапов и мыши (Phase 4H) ─────────────────────────

    /// <summary>
    /// Выбор материала подложки: применимые варианты (заливка, прозрачная) и честное пояснение, почему Acrylic и Mica на окнах
    /// оверлея недоступны. Сохранённый недоступный материал (например, из чужого профиля) показан как замена заливкой.
    /// <paramref name="preview"/> строит живой образец подложки для светлого и тёмного фона.
    /// </summary>
    private UIElement[] BackdropRows(BackdropMaterial current, Action<BackdropMaterial> set, Func<FrameworkElement> preview)
    {
        var choices = new[] { BackdropMaterial.Fill, BackdropMaterial.Clear };
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Backdrop.Kind"), Loc.T("Backdrop." + BackdropMaterials.Effective(current) + ".Hint"),
                _ui.Segmented(Array.IndexOf(choices, BackdropMaterials.Effective(current)), choices.Select(x => Loc.T("Backdrop." + x)).ToArray(), i => set(choices[i]))),
        };
        if (!BackdropMaterials.IsAvailable(current))
        {
            rows.Add(_ui.Note(Loc.F("Backdrop.Fallback", Loc.T("Backdrop." + current))));
        }

        rows.Add(_ui.Note(Loc.T("Backdrop.SystemNote")));
        var stage = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var (back, line) in new[] { (Color.FromRgb(0xF3, 0xF4, 0xF6), Color.FromRgb(0xD8, 0xDC, 0xE0)), (Color.FromRgb(0x16, 0x18, 0x1B), Color.FromRgb(0x30, 0x34, 0x38)) })
        {
            // Полосы фона показывают, что прозрачная подложка действительно пропускает то, что под ней.
            var stripes = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(18, 18), MappingMode = BrushMappingMode.Absolute, SpreadMethod = GradientSpreadMethod.Repeat };
            stripes.GradientStops.Add(new GradientStop(back, 0));
            stripes.GradientStops.Add(new GradientStop(back, 0.5));
            stripes.GradientStops.Add(new GradientStop(line, 0.5));
            stripes.GradientStops.Add(new GradientStop(line, 1));
            stripes.Freeze();
            stage.Children.Add(new Border
            {
                Background = stripes, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14), MinHeight = 96,
                Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = preview() },
            });
        }

        rows.Add(stage);
        return rows.ToArray();
    }

    // ── «Приватность» ────────────────────────────────────────────────────────

    private void BuildPrivacy()
    {
        foreach (var card in PrivacyCards(_prefs!.Current.PrivacyOrDefault))
        {
            _cards.Children.Add(card);
        }
    }

    // ── шрифт и дополнительные подписи кейкапов («Стиль кейкапов») ───────────

    private Border KeycapFontCard(LabSettings s)
    {
        var f = s.FontsOrDefault;
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Fonts.Keycap"), Loc.T("Fonts.KeycapHint"), _ui.FontPicker(f.Keycap, v => Fonts(x => x with { Keycap = v }))),
            _ui.Note(Loc.T("Fonts.KeycapNote")),
        };
        return _ui.Card(Loc.T("Fonts.Keycap.Title"), null, rows.ToArray());
    }

    private Border SecondaryLabelsCard(LabSettings s)
    {
        var l = s.LabelsOrDefault;
        var modes = Enum.GetValues<SecondaryLabelMode>();
        var layouts = KeyboardLayouts.Installed();
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Labels.Secondary"), Loc.T("Labels.SecondaryHint"),
                _ui.Segmented(Array.IndexOf(modes, l.Mode), modes.Select(x => Loc.T("Labels.Secondary." + x)).ToArray(),
                    i =>
                    {
                        Labels(x => x with { Mode = modes[i], Layout = modes[i] == SecondaryLabelMode.FixedLayout ? x.Layout ?? layouts.FirstOrDefault(y => !y.IsEnglish)?.Id ?? layouts.FirstOrDefault()?.Id : x.Layout });
                        Dispatcher.BeginInvoke(BuildSection); // поля места и раскладки — только для включённой подписи
                    })),
        };
        if (l.Mode == SecondaryLabelMode.FixedLayout)
        {
            var box = new ComboBox { Width = 260, Style = UiTheme.Resources.StyleOf("Ko.ComboBox") };
            foreach (var layout in layouts)
            {
                box.Items.Add(new ComboBoxItem { Content = layout.Name, Tag = layout.Id });
            }

            box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == l.Layout);
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedItem is ComboBoxItem item)
                {
                    Labels(x => x with { Layout = (string)item.Tag });
                }
            };
            rows.Add(_ui.Row(Loc.T("Labels.Layout"), Loc.T("Labels.LayoutHint"), box));
            if (l.Layout is { } id && !layouts.Any(x => x.Id == id))
            {
                rows.Add(_ui.Note(Loc.F("Labels.LayoutMissing", id)));
            }
        }

        if (l.Mode != SecondaryLabelMode.Off)
        {
            // Геометрия двух подписей (settings v9): часть вида кейкапа — сохраняется в стиле и профиле; видна при включённой подписи.
            var t = s.Appearance.TwoLabelsOrDefault;
            void Two(Func<TwoLabelLayout, TwoLabelLayout> f) => Ap(x => x with { TwoLabels = f(x.TwoLabelsOrDefault).Sanitized() });
            var corners = new[] { LabelCorner.TopLeft, LabelCorner.TopRight, LabelCorner.BottomLeft, LabelCorner.BottomRight };
            rows.Add(_ui.Row(Loc.T("Labels.Corner"), null, _ui.Segmented(Array.IndexOf(corners, t.Corner), corners.Select(c => Loc.T("Labels.Corner." + c)).ToArray(),
                i => Two(x => x with { Corner = corners[i] }))));
            rows.Add(_ui.Row(Loc.T("Labels.MainSize"), Loc.T("Labels.MainSizeHint"), _ui.Number(Math.Round(t.MainScale * 100), (TwoLabelLayout.MainRange.Min * 100, TwoLabelLayout.MainRange.Max * 100), 2, "%",
                v => Two(x => x with { MainScale = v / 100 }))));
            rows.Add(_ui.Row(Loc.T("Labels.Size"), Loc.T("Labels.SizeHint"), _ui.Number(Math.Round(t.SecondaryScale * 100), (TwoLabelLayout.SecondaryRange.Min * 100, TwoLabelLayout.SecondaryRange.Max * 100), 2, "%",
                v => Two(x => x with { SecondaryScale = v / 100 }))));
            rows.Add(_ui.Row(Loc.T("Labels.OffsetX"), Loc.T("Labels.OffsetHint"), _ui.Number(Math.Round(t.OffsetX * 100), (TwoLabelLayout.OffsetRange.Min * 100, TwoLabelLayout.OffsetRange.Max * 100), 1, "%",
                v => Two(x => x with { OffsetX = v / 100 }))));
            rows.Add(_ui.Row(Loc.T("Labels.OffsetY"), null, _ui.Number(Math.Round(t.OffsetY * 100), (TwoLabelLayout.OffsetRange.Min * 100, TwoLabelLayout.OffsetRange.Max * 100), 1, "%",
                v => Two(x => x with { OffsetY = v / 100 }))));
        }

        rows.Add(_ui.Note(Loc.T("Labels.SecondaryNote")));
        return _ui.Card(Loc.T("Labels.Secondary.Title"), Loc.T("Labels.Secondary.TitleHint"), rows.ToArray());
    }
}
