using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Backpack.Visual.Brand;
using Backpack.Visual.Localization;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Styles;

namespace Backpack.Visual.Tests;

/// <summary>Phase 4H: значок окна, встроенный заголовок, фирменная точка, геометрия двух подписей и её миграция.</summary>
public class Phase4HTests
{
    public static IEnumerable<object[]> TwoLabelCases()
    {
        foreach (var renderer in Enum.GetValues<KeycapRenderer>())
        {
            foreach (double size in new[] { 20.0, 32.0, 56.0 })
            {
                foreach (var corner in Enum.GetValues<LabelCorner>())
                {
                    yield return [renderer, size, corner];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TwoLabelCases))]
    public void Two_labels_stay_on_the_face_and_keep_a_readable_gap(KeycapRenderer renderer, double size, LabelCorner corner)
    {
        Sta.Run(() =>
        {
            foreach (string? font in new[] { null, FontSettings.Spectral })
            {
                VisualFonts.SetKeycapFamily(font);
                try
                {
                    var a = AppearanceSettings.Default with { Renderer = renderer, Size = size, TwoLabels = TwoLabelLayout.Default with { Corner = corner } };
                    var k = KeycapFactory.Create(renderer);
                    k.Apply(a, new KeycapItem(1, 1, "K", false, false, PressCount: 3, Secondary: "л"), showBadge: true);
                    var host = new Window { Content = k, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                    host.Show();
                    host.UpdateLayout();
                    try
                    {
                        var second = Descendants<TextBlock>(k.LabelArea).Single(t => (string?)t.Tag == "secondary");
                        var main = k.LabelArea.Children.OfType<TextBlock>().Single(t => t.Text == "K");
                        var face = k.FaceRect();
                        var s = second.TransformToAncestor(k.LabelArea).TransformBounds(new Rect(second.RenderSize));
                        var m = main.TransformToAncestor(k.LabelArea).TransformBounds(new Rect(main.RenderSize));
                        string at = $"{renderer} {size} {corner} {font ?? "standard"}: face {face} second {s} main {m}";
                        Assert.True(face.Contains(s), at);                                                    // внутри лицевой поверхности
                        Assert.True(m.Left >= face.Left - 0.5 && m.Right <= face.Right + 0.5, at);
                        // Глифы (не строки текста) не соприкасаются: промежуток не меньше 0,05·S по одной из осей.
                        var mg = new Rect(m.X, m.Y + ((m.Height - (main.FontSize * 0.75)) / 2), m.Width, main.FontSize * 0.75);
                        var sg = new Rect(s.X, s.Y + (s.Height * 0.1), s.Width, s.Height * 0.8);
                        double gap = Math.Max(Math.Max(sg.Left - mg.Right, mg.Left - sg.Right), Math.Max(sg.Top - mg.Bottom, mg.Top - sg.Bottom));
                        Assert.True(gap >= size * 0.05, $"{at} gap {gap:0.0}");
                        bool right = corner is LabelCorner.TopRight or LabelCorner.BottomRight, bottom = corner is LabelCorner.BottomLeft or LabelCorner.BottomRight;
                        Assert.True(right ? s.Left + (s.Width / 2) > face.Left + (face.Width / 2) : s.Left + (s.Width / 2) < face.Left + (face.Width / 2), at);
                        Assert.True(bottom ? s.Top + (s.Height / 2) > face.Top + (face.Height / 2) : s.Top + (s.Height / 2) < face.Top + (face.Height / 2), at);
                    }
                    finally
                    {
                        host.Close();
                    }
                }
                finally
                {
                    VisualFonts.SetKeycapFamily(null);
                }
            }
        });
    }

    [Fact]
    public void Main_label_is_smaller_with_two_labels_and_offsets_have_a_wide_range()
    {
        Assert.True(TwoLabelLayout.Default.MainScale < 1);
        Assert.Equal(LabelCorner.BottomRight, TwoLabelLayout.Default.Corner);
        Assert.Equal(0, TwoLabelLayout.Default.OffsetX);
        Assert.True(TwoLabelLayout.OffsetRange.Max - TwoLabelLayout.OffsetRange.Min >= 0.5);
        var bad = new TwoLabelLayout(9, -1, LabelCorner.TopLeft, double.NaN, 5).Sanitized();
        Assert.Equal(TwoLabelLayout.MainRange.Max, bad.MainScale);
        Assert.Equal(TwoLabelLayout.SecondaryRange.Min, bad.SecondaryScale);
        Assert.Equal(0, bad.OffsetX);
        Assert.Equal(TwoLabelLayout.OffsetRange.Max, bad.OffsetY);
    }

    [Fact]
    public void Settings_v8_label_geometry_moves_to_the_keycap_look_and_old_offsets_are_reset()
    {
        var o = JsonNode.Parse(SettingsSerializer.Write(LabSettings.Default))!.AsObject();
        o["formatVersion"] = 8;
        o["appearance"]!["keycap"]!.AsObject().Remove("twoLabels");
        o["labels"] = new JsonObject { ["secondary"] = "active-layout", ["corner"] = "top-left", ["size"] = 0.6, ["offsetX"] = -0.1, ["offsetY"] = -0.1 };
        var r = SettingsSerializer.Read(o.ToJsonString());
        Assert.Equal(8, r.MigratedFrom);
        Assert.Equal(SecondaryLabelMode.ActiveLayout, r.Settings.LabelsOrDefault.Mode);
        var t = r.Settings.Appearance.TwoLabelsOrDefault;
        Assert.Equal(LabelCorner.TopLeft, t.Corner);                 // угол переносится
        Assert.Equal(0, t.OffsetX);                                   // прежние −10 % не выталкивают подпись к краю
        Assert.Equal(0, t.OffsetY);
        Assert.InRange(t.SecondaryScale, TwoLabelLayout.SecondaryRange.Min, TwoLabelLayout.SecondaryRange.Max);
        Assert.Equal(9, SettingsSerializer.FormatVersion);
    }

    [Fact]
    public void Two_label_geometry_round_trips_in_profile_and_keycap_style()
    {
        var t = new TwoLabelLayout(0.7, 0.5, LabelCorner.TopRight, 0.1, -0.05);
        var s = LabSettings.Default with { Appearance = LabSettings.Default.Appearance with { TwoLabels = t } };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(t, back.Settings.Appearance.TwoLabelsOrDefault);

        var style = StyleOps.Capture(s, StyleKind.Keycap, "u-twolabels1", "Two labels", null);
        var read = StyleSerializer.Read(StyleSerializer.Write(style));
        Assert.True(read.Ok, string.Join("; ", read.Errors));
        Assert.Equal(t, read.Style!.Keycap!.TwoLabelsOrDefault);
        var applied = StyleOps.Apply(LabSettings.Default, read.Style);
        Assert.Equal(t, applied.Appearance.TwoLabelsOrDefault);
        Assert.True(StyleOps.Matches(applied, read.Style));
        Assert.False(StyleOps.Matches(applied with { Appearance = applied.Appearance with { TwoLabels = t with { OffsetX = 0.2 } } }, read.Style));
    }

    [Fact]
    public void Layout_ids_of_variant_layouts_keep_the_sign_of_the_hkl()
    {
        // «Українська (розширена)»: HKL FFFFFFFF_F0A80422 на 64-битной Windows; ID хранит младшие 32 бита.
        nint enhanced = unchecked((nint)(long)0xFFFFFFFFF0A80422UL);
        Assert.Equal("F0A80422", Backpack.Visual.Layout.KeyboardLayouts.IdOf(enhanced));
        Assert.Equal(enhanced, Backpack.Visual.Layout.KeyboardLayouts.Parse("F0A80422"));
        Assert.Equal((nint)0x04220422, Backpack.Visual.Layout.KeyboardLayouts.Parse("04220422"));
    }

    [Fact]
    public void Window_icon_has_real_frames_for_taskbar_sizes_and_fills_them()
    {
        Sta.Run(() =>
        {
            var icon = LogoArt.WindowIcon();
            var frames = icon.Decoder.Frames;
            foreach (int px in new[] { 24, 32, 36, 48, 256 })
            {
                var f = frames.FirstOrDefault(x => x.PixelWidth == px);
                Assert.True(f is not null, $"{px}: no frame");
                var bmp = new FormatConvertedBitmap(f, PixelFormats.Bgra32, null, 0);
                var pixels = new byte[px * px * 4];
                bmp.CopyPixels(pixels, px * 4, 0);
                int top = px, bottom = -1;
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] > 40)
                    {
                        int y = i / 4 / px;
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }

                // 220438.png: прежний векторный значок занимал в 48×48 только 30 пикселей по высоте.
                Assert.True(bottom - top + 1 >= px * 0.9, $"{px}: {bottom - top + 1}");
            }
        });
    }

    [Fact]
    public void Wordmark_has_the_brand_dot_on_the_baseline_and_an_accessible_name_without_it()
    {
        Sta.Run(() =>
        {
            var w = Wordmark.Create(30, FontWeights.SemiBold);
            Assert.Equal("Backpack", System.Windows.Automation.AutomationProperties.GetName(w));
            var dot = Wordmark.Dot(w);
            Assert.NotNull(dot);
            Assert.Equal(30 * Wordmark.DotSize, dot!.Width, 1);
            Assert.Equal(BaselineAlignment.Baseline, ((System.Windows.Documents.InlineUIContainer)w.Inlines.LastInline).BaselineAlignment);
        });
    }

    [Fact]
    public void Main_window_uses_the_integrated_title_and_header_controls_take_clicks()
    {
        Sta.Run(() =>
        {
            var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Keys);
            try
            {
                Assert.True(WindowShell.GetIntegratedTitle(w));
                Assert.Equal(Loc.T("Settings.WindowTitle"), w.Title);                 // системное имя для Alt+Tab сохраняется
                var home = w.NavItems.Single(n => n.Uid == "home");
                Assert.True(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(home));
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    [Fact]
    public void Style_actions_are_one_compact_icon_toolbar_with_names_tips_and_delete_apart()
    {
        Sta.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "bp-tb-" + Guid.NewGuid().ToString("N")[..8]);
            var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Appearance, styles: new StyleStore(Path.Combine(dir, "styles")));
            try
            {
                var bar = LogicalDescendants<WrapPanel>(w.Cards).First(p => (string?)p.Tag == "style-toolbar");
                var groups = bar.Children.OfType<StackPanel>().ToList();
                Assert.Equal(5, groups.Count);                                                             // + группа «Ещё» (видна при нехватке места)
                var buttons = groups.SelectMany(g => g.Children.OfType<Button>()).ToList();
                Assert.Equal(10, buttons.Count);                                                           // 9 действий (ни одно не потеряно) + «Ещё»
                Assert.All(buttons, b =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(b)));
                    Assert.False(string.IsNullOrWhiteSpace(b.ToolTip as string));
                    Assert.True(ToolTipService.GetShowOnDisabled(b));
                    Assert.IsNotType<string>(b.Content);                                                   // векторный значок, не текст
                });
                var delete = groups[^1].Children.OfType<Button>().Single();
                Assert.Equal(Loc.T("Profiles.Delete"), System.Windows.Automation.AutomationProperties.GetName(delete));
                Assert.False(delete.IsEnabled);                                                            // встроенный стиль
                Assert.Contains(Loc.T("Styles.Why.BuiltIn"), (string)delete.ToolTip);                       // причина недоступности
            }
            finally
            {
                w.ReleaseResources();
                w.Close();
            }
        });
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (c is T t)
            {
                yield return t;
            }

            foreach (var d in LogicalDescendants<T>(c))
            {
                yield return d;
            }
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(c))
            {
                yield return d;
            }
        }
    }
}
