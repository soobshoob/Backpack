using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Tests;

public class RenderingTests
{
    [Fact]
    public void KeycapMetrics_AtDefaultSize_MatchSpec()
    {
        var m = KeycapMetrics.From(AppearanceSettings.Default);
        Assert.Equal(80, m.Height);
        Assert.Equal(72, m.FaceHeight);
        Assert.Equal(72, m.MinWidth);
        Assert.Equal(80, m.ModifierMinWidth);
        Assert.Equal(20, m.CornerRadius);
        Assert.Equal(8, m.PressTravel);
        Assert.Equal(16, m.PaddingX);
        Assert.Equal(12.8, m.PaddingY, 9);
        Assert.Equal(9.6, m.KeyGap, 9);
        Assert.Equal(28, m.GroupCornerRadius);
        Assert.Equal(24, m.BadgeSize);
        Assert.Equal(12, m.BadgeCornerRadius);   // r = 0.5 → 50 % → круг
    }

    [Fact]
    public void Bezier_HitsKnownPoints()
    {
        Assert.Equal(0, CubicBezier.EaseInOutExpo.Evaluate(0));
        Assert.Equal(1, CubicBezier.EaseInOutExpo.Evaluate(1));
        Assert.True(CubicBezier.EaseOutQuint.Evaluate(0.2) > 0.6);     // быстрый старт

        // Независимая проверка: перебор параметра t кривой Безье с контрольными точками (0,0), P1, P2, (1,1).
        static (double X, double Y) Point(double x1, double y1, double x2, double y2, double t)
        {
            double u = 1 - t;
            return ((3 * u * u * t * x1) + (3 * u * t * t * x2) + (t * t * t), (3 * u * u * t * y1) + (3 * u * t * t * y2) + (t * t * t));
        }

        foreach (var (curve, p) in new[] { (CubicBezier.EaseInOutExpo, (0.86, 0.0, 0.07, 1.0)), (CubicBezier.EaseOutQuint, (0.23, 1.0, 0.32, 1.0)) })
        {
            foreach (double x in new[] { 0.1, 0.25, 0.5, 0.75, 0.9 })
            {
                double bestT = 0, bestErr = double.MaxValue;
                for (int i = 0; i <= 200_000; i++)
                {
                    double t = i / 200_000.0;
                    double err = Math.Abs(Point(p.Item1, p.Item2, p.Item3, p.Item4, t).X - x);
                    if (err < bestErr)
                    {
                        (bestErr, bestT) = (err, t);
                    }
                }

                Assert.Equal(Point(p.Item1, p.Item2, p.Item3, p.Item4, bestT).Y, curve.Evaluate(x), 4);
            }
        }
    }

    [Fact]
    public void InterFont_IsEmbedded_WithThreeWeights()
    {
        Sta.Run(() =>
        {
            VisualFonts.EnsurePackScheme();
            var faces = Fonts.GetTypefaces(new Uri(VisualFonts.FontsBaseUri, "./")).ToList();
            Assert.Contains(faces, f => f.FontFamily.Source.Contains("Inter") && f.Weight == FontWeights.Normal);
            Assert.Contains(faces, f => f.FontFamily.Source.Contains("Inter") && f.Weight == FontWeights.SemiBold);
            Assert.Contains(faces, f => f.FontFamily.Source.Contains("Inter") && f.Weight == FontWeights.Bold);

            var typeface = new Typeface(VisualFonts.Keycap, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyph));
            Assert.Contains("Inter", glyph.FamilyNames.Values.First());
        });
    }

    private static Size Measure(KeycapItem item, AppearanceSettings? s = null)
    {
        var k = new LowProfileKeycap();
        k.Apply(s ?? AppearanceSettings.Default, item, showBadge: false);
        k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return k.DesiredSize;
    }

    [Fact]
    public void Keycap_Measures_AsSpecified_SpecialKeysStayCompact()
    {
        Sta.Run(() =>
        {
            Assert.Equal(new Size(72, 80), Measure(new KeycapItem(1, 1, "A", false, false)));
            Assert.Equal(new Size(80, 80), Measure(new KeycapItem(2, 2, "Ctrl", true, false, KeySide.Left)));
            // Компактные служебные клавиши: в смешанном виде не шире минимальной ширины модификатора.
            foreach (var key in new[] { "Space", "Delete", "Backspace", "Enter", "Tab", "Esc", "ArrowUp", "ArrowLeft", "PageDown", "CapsLock", "MouseLeft" })
            {
                foreach (var set in Enum.GetValues<LabelSet>())
                {
                    var size = Measure(new KeycapItem(3, 3, key, false, false), AppearanceSettings.Default with { LabelSet = set });
                    Assert.True(size.Width <= 80.01, $"{key} ({set}): {size.Width}");
                }
            }
        });
    }

    [Fact]
    public void LongLabels_AreNotClipped()
    {
        Sta.Run(() =>
        {
            var s = AppearanceSettings.Default with { LabelMode = LabelMode.Text };
            var k = new LowProfileKeycap();
            k.Apply(s, new KeycapItem(3, 3, "SC E1 1D", false, false), showBadge: false);
            k.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var text = new FormattedText("SC E1 1D", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(VisualFonts.Keycap, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 32 * 0.4, Brushes.Black, 1); // ужатие не меньше 0.4·S, дальше кейкап расширяется
            Assert.True(k.DesiredSize.Width >= text.WidthIncludingTrailingWhitespace + (2 * 16) + (2 * 2) - 0.5);
            Assert.Equal("SC E1 1D", k.MainText);
        });
    }

    [Fact]
    public void PlacementBand_AndGroup_StayInsideWorkArea_AllAnchors_LargeSizes()
    {
        var work = new Rect(-1920, -200, 1920, 1040); // монитор слева и выше основного: отрицательные координаты
        foreach (var size in new[] { 8.0, 32, 128 })
        {
            foreach (var bg in new[] { true, false })
            {
                var a = AppearanceSettings.Default with { Size = size, BackgroundEnabled = bg };
                var m = KeycapMetrics.From(a);
                foreach (var anchor in Enum.GetValues<ScreenAnchor>())
                {
                    foreach (var (ox, oy) in new[] { (0.0, 0.0), (5000.0, 5000.0), (-5000.0, -5000.0) })
                    {
                        var p = new PlacementSettings(anchor, 100, 100, ox, oy);
                        var band = OverlayPlacement.Band(work, p, a);
                        Assert.True(band.Height >= m.GroupHeight(bg) + (2 * m.BadgeOvershoot));
                        Assert.True(band.Top >= work.Top - 0.001 && band.Bottom <= work.Bottom + 0.001, $"{anchor} S={size} off={ox}");
                        double w = 400;
                        double left = OverlayPlacement.GroupLeft(band.Width, w, p);
                        Assert.InRange(left, 0, band.Width - w);
                    }
                }
            }
        }
    }

    [Fact]
    public void Placement_EdgeAndOffset_AreDifferentThings_ForNineAnchors()
    {
        var work = new Rect(0, 0, 1920, 1040);
        var a = AppearanceSettings.Default;
        double groupW = 300;
        var bandH = OverlayPlacement.Band(work, PlacementSettings.Default, a).Height;
        foreach (var anchor in Enum.GetValues<ScreenAnchor>())
        {
            var basic = new PlacementSettings(anchor, 150, 140, 0, 0); // смещения ниже не упираются в край
            var shifted = basic with { OffsetX = 120, OffsetY = -70 };
            var r0 = OverlayPlacement.GroupRect(work, basic, a, groupW);
            var r1 = OverlayPlacement.GroupRect(work, shifted, a, groupW);
            // Смещение двигает группу при любом якоре, в том числе центральном.
            Assert.Equal(120, r1.Left - r0.Left, 6);
            Assert.Equal(-70, r1.Top - r0.Top, 6);

            var (edgeX, edgeY) = OverlayPlacement.EdgeApplies(anchor);
            var moreEdge = OverlayPlacement.GroupRect(work, basic with { EdgeX = 250, EdgeY = 240 }, a, groupW);
            Assert.Equal(edgeX, Math.Abs(moreEdge.Left - r0.Left) > 0.001);   // расстояние от края — только у «краевых» якорей
            Assert.Equal(edgeY, Math.Abs(moreEdge.Top - r0.Top) > 0.001);
        }

        var center = OverlayPlacement.GroupRect(work, new PlacementSettings(ScreenAnchor.Center, 100, 100, 0, 0), a, groupW);
        Assert.Equal((work.Width - groupW) / 2, center.Left, 6);
        Assert.Equal((work.Height - bandH) / 2, center.Top - KeycapMetrics.From(a).OuterMargin, 6);
        var topLeft = OverlayPlacement.GroupRect(work, new PlacementSettings(ScreenAnchor.TopLeft, 50, 30, 0, 0), a, groupW);
        Assert.Equal(50, topLeft.Left, 6);
    }

    [Fact]
    public void DemoScenarios_HaveReviveStep_TypingReplaces_AndNewIdsEachLoop()
    {
        var chords = DemoScenario.Chords;
        var (_, first) = chords.ItemsAt(TimeSpan.FromSeconds(0.1));
        var (_, nextLoop) = chords.ItemsAt(chords.Length + TimeSpan.FromSeconds(0.1));
        Assert.NotEqual(first.Single().Id, nextLoop.Single().Id);
        var (_, beforeExit) = chords.ItemsAt(TimeSpan.FromSeconds(4.0));
        var (_, revived) = chords.ItemsAt(TimeSpan.FromSeconds(4.75));
        Assert.Equal(beforeExit.Single().MatchKey, revived.Single().MatchKey);
        Assert.NotEqual(beforeExit.Single().Id, revived.Single().Id);

        for (double t = 0; t < 1.7; t += 0.03)
        {
            Assert.True(DemoScenario.Typing.ItemsAt(TimeSpan.FromSeconds(t)).Items.Count <= 1); // печать — по одной клавише
        }
    }

    /// <summary>
    /// Снимки для визуальной проверки (обычный прогон их не пишет): BACKPACK_SNAPSHOTS=папка.
    /// Рендер через RenderTargetBitmap при 96 и 144 DPI; это не сравнение с Keyviz и не проверка реального окна.
    /// </summary>
    [Fact]
    public void Snapshots_RenderWithoutClipping()
    {
        string? dir = Environment.GetEnvironmentVariable("BACKPACK_SNAPSHOTS");
        Sta.Run(() =>
        {
            KeycapItem Mod(string k, KeySide side, bool pressed) => new(Random.Shared.NextInt64(), k.GetHashCode(), k, true, pressed, side);
            KeycapItem Key(string k, bool pressed = false, int count = 1, bool numpad = false) => new(Random.Shared.NextInt64(), k.GetHashCode(), k, false, pressed, Numpad: numpad, PressCount: count);
            KeycapItem[] specials = [Key("Esc"), Key("Tab"), Key("CapsLock"), Key("Space"), Key("Backspace"), Key("Delete"), Key("Enter"), Key("Enter", numpad: true)];
            KeycapItem[] mods = [Mod("Ctrl", KeySide.Left, true), Mod("Shift", KeySide.Left, false), Mod("Alt", KeySide.Right, false), Mod("Win", KeySide.Left, false), Key("K", true, 2)];
            KeycapItem[] nav = [Key("ArrowUp"), Key("ArrowDown"), Key("ArrowLeft"), Key("ArrowRight"), Key("Home"), Key("End"), Key("PageUp"), Key("PageDown")];
            KeycapItem[] mouse = [Key("MouseLeft", true), Key("MouseRight"), Key("MouseMiddle", true), Key("MouseX1", true), new KeycapItem(9, 9, "MouseX2", false, false, IsUnknown: true)];
            var cases = new List<(string Name, AppearanceSettings S, KeycapItem[] Items)>();
            foreach (var set in Enum.GetValues<LabelSet>())
            {
                foreach (var mode in Enum.GetValues<LabelMode>())
                {
                    var s = AppearanceSettings.Default with { LabelSet = set, LabelMode = mode };
                    cases.Add(($"{set}-{mode}-mods", s, mods));
                    cases.Add(($"{set}-{mode}-specials", s, specials));
                }
            }

            cases.Add(("nav-mixed", AppearanceSettings.Default with { LabelSet = LabelSet.MacOS }, nav));
            cases.Add(("mouse", AppearanceSettings.Default, mouse));
            cases.Add(("long-labels", AppearanceSettings.Default, [Key("F12"), Key("PrintScreen"), Key("SC E1 1D"), Key("7", numpad: true)]));
            cases.Add(("unknown", AppearanceSettings.Default, [new KeycapItem(9, 9, "A", false, false, IsUnknown: true), new KeycapItem(10, 10, "Shift", true, false, KeySide.Right, IsUnknown: true)]));
            cases.Add(("large-128", AppearanceSettings.Default with { Size = 128 }, [Mod("Ctrl", KeySide.Right, true), Key("W", false, 3), Key("Enter")]));
            cases.Add(("dark", AppearanceSettings.Default with { Primary = ThemeColor.Parse("#2E3138"), Secondary = ThemeColor.Parse("#16181C"), TextColor = ThemeColor.Parse("#F2E6D0"), BorderEnabled = false, BackgroundColor = ThemeColor.Parse("#0B0C0ECC"), Radius = 0.2 }, mods));

            foreach (var (name, s, items) in cases)
            {
                var p = new OverlayPresenter(new FakeClock(), useRenderingLoop: false);
                p.Apply(s with { Animation = AnimationKind.None }, RowOptions.Default with { Trail = TrailMode.FadeInPlace });
                p.Update(items);
                var m = KeycapMetrics.From(s);
                var host = new Border { Background = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), Padding = new Thickness(m.OuterMargin), Child = p.Root };
                host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                host.Arrange(new Rect(host.DesiredSize));
                host.UpdateLayout();

                foreach (var v in p.Visuals)
                {
                    var b = v.Control.TransformToAncestor(p.Root).TransformBounds(new Rect(v.Control.RenderSize));
                    Assert.True(b.Left >= -0.01 && b.Right <= p.Root.ActualWidth + 0.01, name);
                    Assert.True(v.Control.ActualHeight == m.Height, name);
                }

                Assert.True(p.Root.ActualHeight <= m.GroupHeight(s.BackgroundEnabled) + 0.01, name);
                if (dir is not null)
                {
                    Directory.CreateDirectory(dir);
                    Save(host, Path.Combine(dir, $"{name}-144.png"), 144);
                }
            }
        });
    }

    internal static void Save(FrameworkElement host, string path, int dpi)
    {
        double scale = dpi / 96.0;
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(host.ActualWidth * scale), (int)Math.Ceiling(host.ActualHeight * scale), dpi, dpi, PixelFormats.Pbgra32);
        bmp.Render(host);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(path);
        enc.Save(f);
    }
}
