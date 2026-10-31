using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4F, п. 6: прямое перемещение линзы, источника и области затемнения ручками. Источник показан — линза и источник
/// независимы; скрыт — линза увеличивает то, что под ней, источник следует за ней. Края экрана, перенос между мониторами с
/// разным DPI, выход без оставшихся окон.
/// </summary>
public class Phase4FFocusDragTests
{
    private static readonly MonitorDesc Primary = new("p", "Primary", new PixelRect(0, 0, 2560, 1600), new PixelRect(0, 0, 2560, 1540), 1.5, true);
    private static readonly MonitorDesc Second = new("s", "Second", new PixelRect(-1440, -974, 1440, 2560), new PixelRect(-1440, -974, 1440, 2500), 1.25, false);

    private static (int X, int Y) Center(PixelRect r) => (r.Left + (r.Width / 2), r.Top + (r.Height / 2));

    [Fact]
    public void Region_math_follow_independent_and_dpi_rescale()
    {
        var output = new PixelRect(1000, 600, 500, 300);
        var under = FocusRegion.UnderOutput(output, 2);
        Assert.Equal(Center(output), Center(under.Source));
        Assert.Equal(250, under.Source.Width);

        var moved = under.MoveOutputFollowing(300, 200, Primary.Bounds, 2);
        Assert.Equal(Center(moved.Output), Center(moved.Source));            // источник под линзой
        var independent = under.MoveOutput(300, 200, Primary.Bounds);
        Assert.Equal(under.Source, independent.Source);                      // перемещение линзы не меняет источник

        var src = under.MoveSource(-400, 100, Primary.Bounds);
        Assert.Equal(under.Output, src.Output);                              // перемещение источника не двигает линзу

        // 150 % → 125 %: физический размер × 1.25/1.5, размер в DIP тот же.
        var r = under.RescaleOutput(1.25 / 1.5, new PixelRect(-1440, -974, 4000, 2600), 2, follow: true);
        Assert.InRange(r.Output.Width, 416, 417);
        Assert.Equal(Center(r.Output), Center(r.Source));
        // Край экрана: линза остаётся целиком на экране.
        var edge = under.MoveOutputFollowing(5000, 0, Primary.Bounds, 2);
        Assert.Equal(Primary.Bounds.Right, edge.Output.Right);
    }

    private static FocusController Controller(FocusSettings settings, params MonitorDesc[] monitors) =>
        new(() => settings, () => monitors);

    [Fact]
    public void Dim_area_moves_and_resizes_by_grips_and_nothing_is_left_after_exit()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                var f = Controller(FocusSettings.Default with { Mode = FocusMode.Dim, AppearMs = 0 }, Primary);
                f.Start(FocusMode.Dim);
                f.Activate(new PixelRect(1000, 600, 500, 300));
                Assert.Equal(FocusState.Active, f.State);
                Assert.False(f.CapturesMouse);                                   // мышь принимают только ручки
                Assert.NotNull(f.GripOf(FocusGripKind.Move));
                Assert.NotNull(f.GripOf(FocusGripKind.Resize));
                Assert.Null(f.GripOf(FocusGripKind.Source));
                // Phase 4G: ручки скрыты до наведения (и пропускают щелчки); клавиатурный вызов показывает их и включает мышь.
                Assert.False(f.ControlsVisible);
                f.RevealControls();
                Assert.True(f.ControlsVisible);
                Assert.All(f.Grips, g => Assert.False(NativeWindow.HasNoActivateClickThrough(g.Hwnd)));

                f.DragBy(FocusGripKind.Move, -350, -250);
                f.EndDrag();
                Assert.Equal(new PixelRect(650, 350, 500, 300), f.Region!.Output);
                Assert.Equal(f.Region.Output, f.Region.Source);                  // затемнение: окно = область
                f.DragBy(FocusGripKind.Resize, 200, 150);
                f.EndDrag();
                Assert.Equal(700, f.Region!.Output.Width);
                Assert.Equal(450, f.Region.Output.Height);
                var grip = f.GripOf(FocusGripKind.Move)!;
                Assert.True(grip.Rect.Bottom <= f.Region.Output.Top || grip.Rect.Top >= f.Region.Output.Top); // над кромкой (или внутри у края)

                f.ToggleEdit();
                Assert.Empty(f.Grips);                                           // в редактировании ручек нет (окно редактирования)
                f.Escape();
                Assert.NotEmpty(f.Grips);
                f.Escape();
                Assert.Equal(FocusState.Off, f.State);
                Assert.Empty(f.Grips);
                Assert.Empty(f.DimWindows);
                Assert.False(f.CapturesMouse);
                f.Dispose();
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Lens_with_shown_source_is_independent_and_with_hidden_source_follows(bool showSource)
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            var settings = FocusSettings.Default with { Mode = FocusMode.Lens, Zoom = 2, ShowSource = showSource };
            var f = new FocusController(() => settings, () => [Primary, Second]);
            try
            {
                f.Start(FocusMode.Lens);
                f.Activate(new PixelRect(1000, 600, 500, 300));
                var start = f.Region!;
                Assert.Equal(showSource, f.GripOf(FocusGripKind.Source) is not null);

                f.DragBy(FocusGripKind.Move, 300, 200);
                f.EndDrag();
                if (showSource)
                {
                    Assert.Equal(start.Source, f.Region!.Source);
                    f.DragBy(FocusGripKind.Source, -200, 100);
                    f.EndDrag();
                    Assert.Equal(start.Output with { Left = 1300, Top = 800 }, f.Region!.Output); // линза на месте
                    Assert.Equal(start.Source.Left - 200, f.Region.Source.Left);

                    // Источник скрыли во время работы: он снова под линзой, прежняя область не остаётся источником.
                    settings = settings with { ShowSource = false };
                    f.ApplySettings();
                    Assert.Equal(Center(f.Region!.Output), Center(f.Region.Source));
                    Assert.Null(f.GripOf(FocusGripKind.Source));
                }
                else
                {
                    Assert.Equal(Center(f.Region!.Output), Center(f.Region.Source));
                }

                // Перенос на монитор 125 %: центр линзы на втором мониторе — размер в DIP сохраняется, всё на мониторе.
                f.DragBy(FocusGripKind.Move, -2000, 0);
                f.EndDrag();
                var o = f.Region!.Output;
                Assert.InRange(o.Left, Second.Bounds.Left, Second.Bounds.Right - o.Width);
                Assert.InRange(o.Width, 416, 417);                                // 500 px × 1.25 / 1.5

                f.Stop();
                Assert.Empty(f.Grips);
                Assert.Null(f.SourceOutlineWindow);
                Assert.Null(f.Surface);
            }
            finally
            {
                f.Dispose();
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }
}
