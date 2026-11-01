using System.Runtime.InteropServices;
using System.Windows;
using Backpack.Sdk;
using Backpack.Visual.Plugins;
using Backpack.Visual.Presentation;
using Backpack.Visual.Windows;

namespace Backpack.App;

/// <summary>
/// Самопроверка Phase 4H (п. 8 — незакрытая проверка Phase 4G): управление линзы, «Фокуса» и панели таймера при настоящем
/// указателе. Указатель Windows перемещается (SetCursorPos) — щелчков и нажатий нет — и только пока на переднем плане окно
/// самой самопроверки (иначе шаг пропускается с отметкой, указатель пользователя не трогается). Проверяется: скрытое
/// управление прозрачно для мыши и не перехватывает щелчки (WindowFromPoint), наведение на оверлей показывает его, переход с
/// оверлея на окно управления не скрывает, удержание (перетаскивание или открытое меню) держит видимым, уход — скрывает
/// после задержки. Положение указателя возвращается.
/// </summary>
public partial class App
{
    private void RunLiveHoverSelfTest(Action<bool, string> check, List<string> notes)
    {
        if (_tools is null || _monitors.Count == 0)
        {
            return;
        }

        var m = _monitors.First(x => x.IsPrimary);
        var b = m.Bounds;
        var guard = new Window
        {
            Title = "Backpack hover self-test", Width = 260, Height = 90, ShowInTaskbar = false, Topmost = true, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = 8, Top = 8,
            Content = new System.Windows.Controls.TextBlock { Text = "Hover self-test: the pointer moves by itself", Margin = new Thickness(10), TextWrapping = TextWrapping.Wrap },
        };
        guard.Show();
        guard.Activate();
        PumpFor(300);
        var start = Screens.Cursor();
        int moves = 0, skipped = 0;
        bool Ours() => GetWindowThreadProcessId(GetForegroundWindow(), out uint pid) != 0 && pid == (uint)Environment.ProcessId;
        bool MoveTo(int x, int y)
        {
            if (!Ours())
            {
                skipped++;
                return false; // на переднем плане чужое окно — указатель пользователя не трогаем
            }

            moves++;
            SetCursorPos(x, y);
            PumpFor(140); // опрос HoverReveal — 40 мс, появление — 150 мс
            return true;
        }

        (int X, int Y) Center(PixelRect r) => (r.Left + (r.Width / 2), r.Top + (r.Height / 2));
        (int X, int Y) Far() => (b.Left + 40, b.Bottom - 40);
        static bool ClickThrough(NoActivateWindow w) => w.Hwnd == 0 || (Screens.ExStyle(w.Hwnd) & Screens.WsExTransparent) != 0;
        bool NotCaught(int x, int y, IEnumerable<NoActivateWindow> controls)
        {
            nint at = WindowFromPoint(new NativePoint { X = x, Y = y });
            return controls.All(c => c.Hwnd == 0 || at != c.Hwnd);
        }

        void Case(string name, PixelRect target, Func<bool> visible, Func<IReadOnlyList<NoActivateWindow>> controls, Action<bool>? hold)
        {
            var (fx, fy) = Far();
            if (!MoveTo(fx, fy))
            {
                notes.Add($"selftest-ui: hover {name} skipped — foreground is not Backpack");
                return;
            }

            PumpFor((int)HoverReveal.HideDelay.TotalMilliseconds + 300);
            var c = controls();
            check(!visible() && c.All(ClickThrough), $"hover {name}: hidden away from the overlay, controls let clicks through");
            var (cx, cy) = Center(target);
            if (c.FirstOrDefault(w => w.Hwnd != 0 && PhysicalPlacement.TargetOf(w) is not null) is { } grip && PhysicalPlacement.TargetOf(grip) is { } gr)
            {
                var (gx, gy) = Center(gr);
                check(NotCaught(gx, gy, c), $"hover {name}: hidden control does not catch a click at its place");
            }

            if (!MoveTo(cx, cy))
            {
                return;
            }

            check(visible() && controls().All(w => !ClickThrough(w)), $"hover {name}: pointer over the overlay shows controls");
            if (controls().FirstOrDefault(w => PhysicalPlacement.TargetOf(w) is not null) is { } first && PhysicalPlacement.TargetOf(first) is { } fr)
            {
                // Переход с оверлея на окно управления (ручку, кнопки) — управление не исчезает.
                var (tx, ty) = Center(fr);
                MoveTo(tx, ty);
                PumpFor((int)HoverReveal.HideDelay.TotalMilliseconds + 200);
                check(visible(), $"hover {name}: moving from the overlay onto a control keeps it visible");
            }

            if (hold is not null)
            {
                hold(true); // перетаскивание или открытое меню
                MoveTo(fx, fy);
                PumpFor((int)HoverReveal.HideDelay.TotalMilliseconds + 300);
                check(visible(), $"hover {name}: held (drag / open menu) stays visible away from the overlay");
                hold(false);
            }
            else
            {
                MoveTo(fx, fy);
            }

            PumpFor((int)HoverReveal.HideDelay.TotalMilliseconds + 400);
            check(!visible() && controls().All(ClickThrough), $"hover {name}: hides after the pointer leaves");
        }

        const string plugin = "backpack.selftest.hover";
        try
        {
            // Линза (увеличение region) — ручки линзы и источника.
            var lensArea = new PixelRect(b.Left + (b.Width / 3), b.Top + (b.Height / 3), b.Width / 5, b.Height / 6);
            _tools.Open(plugin, new PluginToolRequest("lens", new MagnifierRequest(MagnifierMode.Region), null));
            if (_tools.SessionsOf(plugin).SingleOrDefault().Focus is { } lens)
            {
                lens.Activate(lensArea);
                PumpFor(250);
                Case("lens", lens.Region?.Output ?? lensArea, () => lens.ControlsVisible, () => lens.Grips.Cast<NoActivateWindow>().ToList(), null);
            }

            _tools.CloseAll(plugin);
            PumpFor(150);

            // «Фокус» (затемнение) — ручки области; удержание — перетаскивание ручки.
            _tools.Open(plugin, new PluginToolRequest("dim", null, new DimRequest()));
            if (_tools.SessionsOf(plugin).SingleOrDefault().Focus is { } dim)
            {
                dim.Activate(lensArea);
                PumpFor(250);
                Case("focus", dim.Region?.Output ?? lensArea, () => dim.ControlsVisible, () => dim.Grips.Cast<NoActivateWindow>().ToList(),
                    on =>
                    {
                        if (on)
                        {
                            dim.DragBy(FocusGripKind.Move, 1, 0);
                        }
                        else
                        {
                            dim.EndDrag();
                        }
                    });
            }

            _tools.CloseAll(plugin);
            PumpFor(150);

            // Панель таймера (кнопки, перемещение) — та же панель хоста, что у плагина «Таймер презентации».
            using var panel = new PluginPanelHost(() => _monitors, new PixelRect(b.Left + (b.Width / 2), b.Top + (b.Height / 4), 360, 150));
            panel.Set(new PluginPanelSpec("Timer", "04:59", 0.2, 40, 0.9, null, "normal",
                [new PanelButton("toggle", "pause", "Pause"), new PanelButton("reset", "list-restart", "Reset")], true), true);
            PumpFor(300);
            Case("timer panel", panel.Bounds, () => panel.ControlsVisible, () => panel.ControlWindows, on =>
            {
                if (panel.RevealState is { } r)
                {
                    r.Hold = on;
                }
            });
        }
        finally
        {
            _tools.CloseAll(plugin);
            if (Ours())
            {
                SetCursorPos(start.X, start.Y);
            }

            guard.Close();
            notes.Add($"selftest-ui: live hover pointer moves {moves}, skipped {skipped}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint p);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
}
