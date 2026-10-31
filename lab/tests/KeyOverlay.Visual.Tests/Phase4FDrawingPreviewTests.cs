using System.IO;
using System.Windows;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4F, п. 5: панель в превью рисования — та же функция положения, что в окне рисования; ровные отступы; внутри экрана.</summary>
public class Phase4FDrawingPreviewTests
{
    public static IEnumerable<object[]> Modes()
    {
        foreach (bool compact in new[] { false, true })
        {
            yield return [DrawToolbarMode.Docked, ScreenEdge.Top, compact];
            foreach (var edge in Enum.GetValues<ScreenEdge>())
            {
                yield return [DrawToolbarMode.AutoHide, edge, compact];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Toolbar_is_inside_the_preview_screen_with_even_margins(DrawToolbarMode mode, ScreenEdge edge, bool compact)
    {
        Sta.Run(() =>
        {
            var profile = DrawingProfile.Default with { ToolbarMode = mode, AutoHideEdge = edge, Compact = compact };
            foreach (var area in new[] { new Size(1920, 1080), new Size(1707, 1067), new Size(1280, 720), new Size(1152, 2048) })
            {
                var bar = DrawingWindow.PreviewToolbar(DrawSettings.Default, profile, area);
                bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var size = bar.DesiredSize;
                var o = DrawingWindow.ToolbarOrigin(profile, area, size);
                var r = new Rect(o, size);
                string at = $"{mode}/{edge}/{compact} {area}: {r}";
                Assert.True(r.Left >= 0 && r.Top >= 0 && r.Right <= area.Width + 0.5 && r.Bottom <= area.Height + 0.5, at);
                bool vertical = mode == DrawToolbarMode.AutoHide && edge is ScreenEdge.Left or ScreenEdge.Right;
                if (vertical)
                {
                    Assert.True(size.Height >= size.Width, at);                                   // вертикальная панель у бокового края
                    Assert.Equal(r.Top, area.Height - r.Bottom, 1);                               // по центру края
                    Assert.Equal(DrawingWindow.EdgeMargin, edge == ScreenEdge.Left ? r.Left : area.Width - r.Right, 1);
                }
                else
                {
                    Assert.Equal(r.Left, area.Width - r.Right, 1);                                // по центру
                    double expected = mode == DrawToolbarMode.Docked ? DrawingWindow.DockedTop : DrawingWindow.EdgeMargin;
                    Assert.Equal(expected, edge == ScreenEdge.Bottom && mode == DrawToolbarMode.AutoHide ? area.Height - r.Bottom : r.Top, 1);
                }
            }
        });
    }

    [Fact]
    public void Settings_preview_places_the_real_toolbar_exactly_like_the_drawing_window()
    {
        Sta.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "bp-drawprev-" + Guid.NewGuid().ToString("N")[..8]);
            var store = SettingsStore.Load(Path.Combine(dir, "settings.json"));
            var prefs = AppPreferencesStore.Load(Path.Combine(dir, "app.json"));
            var w = new SettingsWindow(store, new SettingsWindowActions(() => { }, () => { }, () => ""), SettingsSection.Drawing, prefs: prefs);
            foreach (var (mode, edge) in new[] { (DrawToolbarMode.Docked, ScreenEdge.Top), (DrawToolbarMode.AutoHide, ScreenEdge.Bottom), (DrawToolbarMode.AutoHide, ScreenEdge.Left) })
            {
                var p = DrawingProfile.Default with { ToolbarMode = mode, AutoHideEdge = edge };
                var preview = w.DrawingPreview(DrawSettings.Default, p);
                preview.Measure(new Size(520, 400));
                preview.Arrange(new Rect(0, 0, 520, 400));
                var bar = w.DrawingPreviewBar!;
                var area = w.DrawingPreviewArea;                       // Phase 4G: виртуальный монитор — область превью
                Assert.Equal(520 / 400.0, area.Width / area.Height, 2);
                var expected = DrawingWindow.ToolbarOrigin(p, area, bar.DesiredSize);
                Assert.Equal(expected.X, System.Windows.Controls.Canvas.GetLeft(bar), 1);
                Assert.Equal(expected.Y, System.Windows.Controls.Canvas.GetTop(bar), 1);
                Assert.Equal(1, bar.Opacity);
            }

            var radial = w.DrawingPreview(DrawSettings.Default, DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly });
            radial.Measure(new Size(520, 400));
            Assert.Null(w.DrawingPreviewBar);
            w.ReleaseResources();
            w.Close();
        });
    }
}
