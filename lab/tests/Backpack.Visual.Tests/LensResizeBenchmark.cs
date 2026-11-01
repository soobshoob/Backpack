using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Tests;

/// <summary>
/// Замер изменения размера линзы на настоящем мониторе (Magnification API, окна рамки/источника/ручек). Запускается только
/// с переменной окружения <c>BACKPACK_LENS_BENCH</c> = путь файла отчёта: открывает окна на экране, поэтому не входит в
/// обычный прогон. Два сценария: (1) шаги размера по одному с ожиданием простоя диспетчера — стоимость одного шага;
/// (2) поток событий мыши быстрее кадра (как мышь 1000 Гц) — сколько времени UI-поток догоняет указатель.
/// </summary>
public class LensResizeBenchmark
{
    [Fact]
    public void Lens_resize_cost()
    {
        string? report = Environment.GetEnvironmentVariable("BACKPACK_LENS_BENCH");
        if (string.IsNullOrEmpty(report))
        {
            return;
        }

        var lines = new List<string>();
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                var monitor = DisplayMonitors.Enumerate().First(m => m.IsPrimary);
                lines.Add($"monitor {monitor.Bounds.Width}x{monitor.Bounds.Height} scale {monitor.Scale}");
                var settings = FocusSettings.Default with { Mode = FocusMode.Lens, ShowSource = true, Shadow = true, AppearMs = 0, Zoom = 2 };
                var f = new FocusController(() => settings, () => [monitor]);
                f.Start(FocusMode.Lens);
                var b = monitor.Bounds;
                f.Activate(new PixelRect(b.Left + 100, b.Top + 100, 300, 180));
                Pump();

                // (1) по одному шагу: синхронная стоимость + обработка до простоя.
                const int steps = 240;
                long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                var per = new List<double>(steps);
                for (int i = 1; i <= steps; i++)
                {
                    var t0 = sw.Elapsed.TotalMilliseconds;
                    f.DragBy(FocusGripKind.Resize, i * 4, i * 3);
                    Pump();
                    per.Add(sw.Elapsed.TotalMilliseconds - t0);
                }

                f.EndDrag();
                long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;
                per.Sort();
                lines.Add(FormattableString.Invariant($"step: total {sw.Elapsed.TotalMilliseconds:0} ms, mean {per.Average():0.00} ms, p50 {per[steps / 2]:0.00}, p95 {per[(int)(steps * 0.95)]:0.00}, max {per[^1]:0.00}, alloc {alloc / 1024} KB ({alloc / steps / 1024.0:0.0} KB/step)"));

                // (2) поток: 8 событий на 8 мс (≈1000 Гц) в течение ~1 с, затем — сколько ждать, пока линза догонит последнее.
                var start = f.Region!.Output;
                var grip = f.GripOf(FocusGripKind.Resize)!;
                var d = Dispatcher.CurrentDispatcher;
                int last = 0;
                sw.Restart();
                var frame = new DispatcherFrame();
                var feeder = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(8) };
                int n = 0;
                double fedEnd = 0;
                feeder.Tick += (_, _) =>
                {
                    for (int k = 0; k < 8 && n < 1000; k++)
                    {
                        n++;
                        int dx = -n / 2, dy = -n / 3;
                        last = n;
                        d.BeginInvoke(DispatcherPriority.Input, () => f.OnGripDragged(grip, dx, dy));
                    }

                    if (n >= 1000)
                    {
                        feeder.Stop();
                        fedEnd = sw.Elapsed.TotalMilliseconds;
                        d.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
                    }
                };
                feeder.Start();
                Dispatcher.PushFrame(frame);
                while (f.HasPendingDrag)
                {
                    Pump(); // последнее смещение применяется перед кадром отрисовки
                    var tick = new DispatcherFrame();
                    void Done(object? o, EventArgs a) => tick.Continue = false;
                    System.Windows.Media.CompositionTarget.Rendering += Done;
                    Dispatcher.PushFrame(tick);
                    System.Windows.Media.CompositionTarget.Rendering -= Done;
                }

                double fed = sw.Elapsed.TotalMilliseconds;
                lines.Add(FormattableString.Invariant($"flood: 1000 events fed over {fedEnd:0} ms, idle after {fed:0} ms (lag behind input {fed - fedEnd:0} ms), output {start.Width}x{start.Height} → {f.Region!.Output.Width}x{f.Region.Output.Height}, frames {f.Surface?.Frames}"));
                f.EndDrag();
                f.Dispose();
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
        File.WriteAllLines(report, lines);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
