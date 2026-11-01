using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Tests;

/// <summary>
/// Изменение размера линзы без торможения: события мыши объединяются до одного применения на кадр (последнее смещение не
/// теряется), прозрачные окна и окно лупы на время перетаскивания меняют размер шагами с запасом, после отпускания размер
/// лупы снова точный. Замер скорости — <see cref="LensResizeBenchmark"/>.
/// </summary>
public class LensResizeTests
{
    private static readonly MonitorDesc Primary = new("p", "Primary", new PixelRect(0, 0, 2560, 1600), new PixelRect(0, 0, 2560, 1540), 1.5, true);

    [Fact]
    public void Window_size_grows_in_steps_keeps_slack_and_stays_on_the_monitor()
    {
        const int step = LayeredWindowSize.Step;
        Assert.Equal(step, LayeredWindowSize.Fit(0, 100, 5000));             // первый размер — с округлением вверх
        Assert.Equal(step, LayeredWindowSize.Fit(step, 101, 5000));          // вмещает — без изменения
        Assert.Equal(step, LayeredWindowSize.Fit(step, step, 5000));
        Assert.Equal(2 * step, LayeredWindowSize.Fit(step, step + 1, 5000)); // не вмещает — следующий шаг
        Assert.Equal(3 * step, LayeredWindowSize.Fit(3 * step, step, 5000)); // уменьшение в пределах двух шагов — без изменения
        Assert.Equal(step, LayeredWindowSize.Fit(4 * step, step - 10, 5000)); // запас больше двух шагов — уменьшение
        Assert.Equal(300, LayeredWindowSize.Fit(0, 300, 300));               // у края монитора — без запаса
        Assert.Equal(400, LayeredWindowSize.Fit(0, 400, 300));               // содержимое за краем — точно по размеру
    }

    [Fact]
    public void Drag_events_are_coalesced_and_the_last_offset_is_applied_on_release()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                var settings = FocusSettings.Default with { Mode = FocusMode.Lens, ShowSource = true, AppearMs = 0, Zoom = 2 };
                var f = new FocusController(() => settings, () => [Primary]);
                f.Start(FocusMode.Lens);
                f.Activate(new PixelRect(400, 300, 300, 200));
                var start = f.Region!.Output;
                var grip = f.GripOf(FocusGripKind.Resize)!;

                f.OnGripDragged(grip, 10, 10);
                f.OnGripDragged(grip, 60, 40);
                f.OnGripDragged(grip, 120, 90);
                Assert.True(f.HasPendingDrag);
                Assert.Equal(start, f.Region!.Output);                       // до кадра ничего не перекладывается

                f.EndDrag();                                                 // отпускание применяет последнее смещение
                Assert.False(f.HasPendingDrag);
                Assert.Equal(start.Width + 120, f.Region!.Output.Width);
                Assert.Equal(start.Height + 90, f.Region.Output.Height);

                f.OnGripDragged(grip, 5, 5);
                f.Stop();                                                    // выход не оставляет подписку на кадры
                Assert.False(f.HasPendingDrag);
                f.Dispose();
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Fact]
    public void Magnifier_window_has_slack_while_dragging_and_exact_size_after_release()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                var settings = FocusSettings.Default with { Mode = FocusMode.Lens, ShowSource = false, AppearMs = 0, Zoom = 2 };
                var f = new FocusController(() => settings, () => [Primary]);
                f.Start(FocusMode.Lens);
                f.Activate(new PixelRect(400, 300, 300, 200));
                var surface = f.Surface!;
                if (!surface.IsOpen)
                {
                    f.Dispose();
                    return; // нет Magnification API в этом сеансе
                }

                Assert.Equal((f.Region!.Output.Width, f.Region.Output.Height), surface.HostSize);
                f.DragBy(FocusGripKind.Resize, 7, 5);
                Assert.True(surface.Interactive);
                var during = surface.HostSize;
                Assert.True(during.Width >= f.Region!.Output.Width && during.Height >= f.Region.Output.Height);
                Assert.Equal(0, during.Width % LayeredWindowSize.Step);
                f.DragBy(FocusGripKind.Resize, 15, 9);
                Assert.Equal(during, surface.HostSize);                       // небольшое изменение — окно лупы того же размера

                f.EndDrag();
                Assert.False(surface.Interactive);
                Assert.Equal((f.Region!.Output.Width, f.Region.Output.Height), surface.HostSize);
                Assert.Equal(f.Region.Output, surface.OutputRect);
                f.Dispose();
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }
}
