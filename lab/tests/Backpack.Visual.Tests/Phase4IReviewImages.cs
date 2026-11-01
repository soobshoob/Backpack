using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Backpack.Visual.Localization;
using Backpack.Visual.Presentation;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Styles;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4I: снимки для отчёта настоящими окнами и рендерами (вне экрана, без захвата рабочего стола). Пишет PNG только если
/// задана переменная BACKPACK_REVIEW_DIR; в обычном прогоне ничего не делает.
/// </summary>
public class Phase4IReviewImages
{
    private static string? Dir => Environment.GetEnvironmentVariable("BACKPACK_REVIEW_DIR") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    private static void Save(System.Windows.Media.Visual v, double w, double h, string path)
    {
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(path);
        enc.Save(f);
    }

    [Fact]
    public void Drawing_arc_angle_and_transform_handles()
    {
        if (Dir is not { } dir)
        {
            return;
        }

        Sta.Run(() =>
        {
            var w = new DrawingWindow(new PixelRect(-32000, -32000, 1200, 760), 1.0, DrawSettings.Default with { AngleLabels = true },
                DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly });
            var root = (FrameworkElement)w.Content;
            root.Measure(new Size(1200, 760));
            root.Arrange(new Rect(0, 0, 1200, 760));
            try
            {
                // Дуга умным карандашом.
                var arc = Enumerable.Range(0, 70).Select(i => (200 + (140.0 * i / 69)) * Math.PI / 180).Select(a => new Point(300 + (170 * Math.Cos(a)), 380 + (170 * Math.Sin(a)))).ToList();
                w.SmartStroke(arc);

                // Угол 60° с подписью (подписи углов оставлены на экране).
                var v = new Point(560, 560);
                var pts = Enumerable.Range(0, 40).Select(i => new Point(820, 560) + ((v - new Point(820, 560)) * (i / 40.0)))
                    .Concat(Enumerable.Range(0, 41).Select(i => v + ((new Point(560 + 130, 560 - 225) - v) * (i / 40.0)))).ToList();
                w.BeginStrokeForTest(pts[0]);
                foreach (var p in pts.Skip(1))
                {
                    w.ExtendStrokeForTest(p);
                }

                w.EndStrokeForTest();

                // Прямоугольник с Shift (квадрат), повёрнутый на 30°, выбран — видны ручки размера и поворота.
                w.SetSettings(w.Settings with { Tool = DrawTool.Rectangle, Color = DrawingWindow.Palette[1] });
                w.ModifiersForTest = ModifierKeys.Shift;
                w.BeginStrokeForTest(new Point(900, 160));
                w.ExtendStrokeForTest(new Point(1080, 300));
                w.EndStrokeForTest();
                w.SetSettings(w.Settings with { Tool = DrawTool.Select });
                Assert.True(w.SelectAt(new Point(900, 220)));
                var frame = DrawingWindow.FrameOf(w.Shapes.Last());
                w.GestureForTest("rotate", new Point(frame.Center.X + 200, frame.Center.Y - 115));
                w.ModifiersForTest = ModifierKeys.None;

                root.UpdateLayout();
                var stage = new Grid { Width = 1200, Height = 760, Background = new LinearGradientBrush(Color.FromRgb(0x30, 0x34, 0x42), Color.FromRgb(0x5A, 0x60, 0x75), 25) };
                w.Content = null;
                stage.Children.Add(root);
                stage.Measure(new Size(1200, 760));
                stage.Arrange(new Rect(0, 0, 1200, 760));
                stage.UpdateLayout();
                Save(stage, 1200, 760, Path.Combine(dir, "drawing-arc-angle-handles.png"));
                stage.Children.Clear();
            }
            finally
            {
                w.Close();
            }
        });
    }

    [Fact]
    public void Settings_pages_in_several_languages()
    {
        if (Dir is not { } dir)
        {
            return;
        }

        Sta.Run(() =>
        {
            foreach (var (lang, section, width) in new[]
            {
                (UiLanguage.English, SettingsSection.Mouse, 1240.0), (UiLanguage.German, SettingsSection.Mouse, 760.0),
                (UiLanguage.Greek, SettingsSection.Appearance, 1240.0), (UiLanguage.Ukrainian, SettingsSection.Keys, 760.0),
                (UiLanguage.German, SettingsSection.About, 1240.0), (UiLanguage.Greek, SettingsSection.About, 1240.0),
            })
            {
                Loc.Set(lang);
                var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), "bp-review-" + Guid.NewGuid().ToString("N")[..8], "settings.json"));
                var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), section)
                {
                    Left = -32000, Top = -32000, Width = width, Height = 900, ShowActivated = false, ShowInTaskbar = false,
                };
                try
                {
                    w.Show();
                    w.UpdateLayout();
                    Save(w, w.ActualWidth, w.ActualHeight, Path.Combine(dir, $"settings-{section.ToString().ToLowerInvariant()}-{Loc.Code}-{width:0}.png"));
                }
                finally
                {
                    w.ReleaseResources();
                    w.Close();
                    Loc.Set(UiLanguage.English);
                }
            }
        });
    }

    [Fact]
    public void Brand_neon_keycaps_pressed_and_released_with_second_label()
    {
        if (Dir is not { } dir)
        {
            return;
        }

        Sta.Run(() =>
        {
            var clock = new FakeClock();
            var p = new OverlayPresenter(clock, useRenderingLoop: false);
            p.Apply(BrandStyles.Starter.Appearance with { Size = 56 });
            var host = new Window { Content = p.Root, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1C)) };
            host.Show();
            try
            {
                KeycapItem K(int id, string label, string second, bool pressed) => new(id, 0x40 + id, label, false, pressed, Secondary: second);
                foreach (var (name, pressed) in new[] { ("pressed", true), ("released", false) })
                {
                    p.Update([K(1, "Ctrl", "", pressed), K(2, "S", "ы", pressed), K(3, "K", "л", pressed)]);
                    clock.Advance(600);
                    p.Tick();
                    host.UpdateLayout();
                    var e = (FrameworkElement)host.Content;
                    var frame = new Border { Background = host.Background, Padding = new Thickness(24), Child = new System.Windows.Shapes.Rectangle { Width = e.ActualWidth, Height = e.ActualHeight, Fill = new VisualBrush(e) } };
                    frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    frame.Arrange(new Rect(frame.DesiredSize));
                    Save(frame, frame.ActualWidth, frame.ActualHeight, Path.Combine(dir, $"keycaps-neon-{name}.png"));
                }
            }
            finally
            {
                host.Close();
            }
        });
    }
}
