using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Backpack.Visual.Plugins;
using Backpack.Core.Input;
using Backpack.Core.Model;
using Backpack.Core.Privacy;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;
using Backpack.Visual.Tray;

namespace Backpack.App;

/// <summary>
/// Самопроверка Phase 4E (шаг 12): команда «Фокус» из всех точек входа, приватность при настоящем вводе в собственном окне,
/// новые инструменты рисования и вложенное радиальное меню в настоящем окне рисования.
/// <para>
/// Ввод: только синтетические нажатия (SendInput) клавиш Tab, Q, W, E и только пока активно собственное тестовое окно — перед
/// каждым нажатием это проверяется (иначе шаг пропускается с отметкой). Клавиша Win не нажимается (открыла бы системное меню
/// пользователя): Win-сочетания проверены логикой фильтра (тесты PrivacyGateTests).
/// </para>
/// </summary>
public partial class App
{
    private void RunPhase4ESelfTest(Action<bool, string> check, List<string> notes)
    {
        RunFocusCommandSelfTest(check, notes);
        RunPrivacyInputSelfTest(check, notes);
        RunDrawing4ESelfTest(check, notes);
    }

    /// <summary>
    /// Движок инструментов плагинов: Esc закрывает последний открытый инструмент и сообщает причину «escape»; закрытие
    /// всего плагина не оставляет окон; блокировка и сон — причина «system».
    /// </summary>
    private void RunFocusCommandSelfTest(Action<bool, string> check, List<string> notes)
    {
        if (_tools is null || _monitors.Count == 0)
        {
            return;
        }

        const string plugin = "backpack.selftest";
        var closed = new List<string>();
        void OnClosed(string p, string t, string reason)
        {
            if (p == plugin)
            {
                closed.Add(t + ":" + reason);
            }
        }

        _tools.Closed += OnClosed;
        try
        {
            _tools.Open(plugin, new PluginToolRequest("follow", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Follow), null));
            _tools.Open(plugin, new PluginToolRequest("pin", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Pinned), null));
            PumpFor(150);
            check(_tools.SessionsOf(plugin).Count == 2, "tools: two magnifier sessions of one plugin");
            _tools.Open(plugin, new PluginToolRequest("pin", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Pinned, Zoom: 2), null));
            check(_tools.SessionsOf(plugin).Count == 2, "tools: repeated open updates, no duplicate lens");
            _tools.Escape();
            check(closed.SequenceEqual(["pin:escape"]) && _tools.IsOpen(plugin, "follow"), "tools: Esc closes the last opened and reports escape");
            _tools.CloseAllSystem("system");
            check(closed.Contains("follow:system") && _tools.Count == 0 && Backpack.Visual.Presentation.MagnifierSurface.OpenCount == 0, "tools: system close releases all magnifiers");
            notes.Add("selftest-ui: plugin tool engine Esc/system routing");
        }
        finally
        {
            _tools.Closed -= OnClosed;
            _tools.CloseAll(plugin);
        }
    }

    /// <summary>
    /// Настоящая цепочка «захват → фокус (UIA) → фильтр → отображение» с синтетическими нажатиями в собственном окне:
    /// Tab в обычном поле виден, хотя переводит фокус в поле пароля; набор в поле пароля и Tab из него не видны ни сразу, ни
    /// позже; ввод в следующем обычном поле виден после подтверждения.
    /// </summary>
    private void RunPrivacyInputSelfTest(Action<bool, string> check, List<string> notes)
    {
        var c = _controller!;
        var original = _prefs!.Current;
        _prefs.Update(x => x with { Privacy = new PrivacySettings(true, true, []) });
        var first = new TextBox { Width = 220 };
        var password = new PasswordBox { Width = 220 };
        var last = new TextBox { Width = 220 };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(first);
        panel.Children.Add(password);
        panel.Children.Add(last);
        var w = new Window { Title = "Backpack privacy input self-test", Content = panel, Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false, Topmost = true };
        var seen = new List<(int Slot, KeyState State)>();
        void Record(KeyTransition t) => seen.Add((t.KeySlot, t.State));
        c.VisibleTransition += Record;
        w.Show();
        w.Activate();
        nint hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        bool Wait(Func<bool> condition, int ms = 2000)
        {
            for (int t = 0; t < ms; t += 25)
            {
                if (condition())
                {
                    return true;
                }

                PumpFor(25);
            }

            return condition();
        }

        bool Tap(ushort vk, ushort scan)
        {
            if (SelfInput.Foreground() != hwnd)
            {
                return false; // окно не активно — синтетическое нажатие не отправляется (не попадёт в чужое приложение)
            }

            SelfInput.Key(vk, scan, down: true);
            SelfInput.Key(vk, scan, down: false);
            PumpFor(60);
            return true;
        }

        const int Tab = 0x0F, Q = 0x10, W = 0x11, E = 0x12;
        try
        {
            first.Focus();
            if (!Wait(() => c.Privacy.State == PrivacyState.Allowed, 3000) || !Wait(() => SelfInput.Foreground() == hwnd, 1000))
            {
                notes.Add("selftest-ui: privacy input skipped (test window not active or field not confirmed)");
                return;
            }

            bool sent = Tap(0x09, Tab);                                        // Tab в обычном поле → фокус в поле пароля
            sent &= Wait(() => password.IsKeyboardFocused, 800);
            Wait(() => c.Privacy.State == PrivacyState.Protected);
            sent &= Tap(0x51, Q) & Tap(0x57, W);                               // «пароль» — синтетические буквы
            sent &= Tap(0x09, Tab);                                            // Tab в защищённом поле → обычное поле
            sent &= Wait(() => last.IsKeyboardFocused, 800);
            Wait(() => c.Privacy.State == PrivacyState.Allowed);
            sent &= Tap(0x45, E);
            Wait(() => seen.Any(s => s.Slot == E), 1500);
            PumpFor(400);                                                      // «позже» тоже ничего скрытого не появляется
            if (!sent)
            {
                notes.Add("selftest-ui: privacy input incomplete (window lost activation)");
                return;
            }

            int tabsShown = seen.Count(s => s.Slot == Tab && s.State == KeyState.Down);
            check(tabsShown == 1, $"privacy input: Tab in the ordinary field is shown once, Tab in the protected field is hidden ({tabsShown})");
            check(!seen.Any(s => s.Slot is Q or W), "privacy input: protected input is never shown");
            check(seen.Any(s => s.Slot == E && s.State == KeyState.Down), "privacy input: input in the next ordinary field is shown");
            notes.Add($"selftest-ui: privacy input transitions {seen.Count}, releases {c.Privacy.PendingReleases}, discards {c.Privacy.PendingDiscards}");
        }
        finally
        {
            c.VisibleTransition -= Record;
            w.Close();
            _prefs.Update(_ => original);
        }
    }

    /// <summary>Новые инструменты и вложенное меню в настоящем окне рисования (без ввода мыши: вызовы окна).</summary>
    private void RunDrawing4ESelfTest(Action<bool, string> check, List<string> notes)
    {
        if (_presentation is null || _prefs is null)
        {
            return;
        }

        var original = _prefs.Current;
        try
        {
            _prefs.Update(x => x with { Tools = x.ToolsOrDefault with { Drawing = true } });
            _presentation.Execute(PresentationCommand.Draw);
            PumpFor(150);
            var d = _presentation.Drawing;
            check(d is not null, "drawing 4E: window open");
            if (d is null)
            {
                return;
            }

            d.SetSettings(d.Settings with { Tool = DrawTool.Polygon, PolygonSides = 5 });
            d.Draw(new Point(300, 300), new Point(380, 300));
            d.SetSettings(d.Settings with { Tool = DrawTool.RoundedRectangle, CornerRadius = 18 });
            d.Draw(new Point(500, 200), new Point(700, 320));
            PumpFor(80);
            check(d.ShapeCount == 2, $"drawing 4E: polygon and rounded rectangle ({d.ShapeCount})");
            d.SetSettings(d.Settings with { Tool = DrawTool.Select });
            check(d.SelectAt(new Point(500, 260)) && d.MoveSelected(new Vector(30, 10)), "drawing 4E: select and move");
            d.Undo();
            check(d.ShapeCount == 2, "drawing 4E: undo move keeps both shapes");
            _presentation.Execute(PresentationCommand.Radial);
            PumpFor(80);
            check(d.RadialOpen && d.Radial!.Activate("cat.shapes") && d.Radial.SubRingOpen, "drawing 4E: radial category opens its ring");
            _presentation.Escape();
            PumpFor(60);
            check(d.RadialOpen && !d.Radial!.SubRingOpen, "drawing 4E: Esc closes the ring first");
            System.Threading.Thread.Sleep(220);
            _presentation.Escape();
            PumpFor(60);
            check(!d.RadialOpen, "drawing 4E: second Esc closes the menu");
            notes.Add($"selftest-ui: drawing 4E radial categories {d.Radial?.Root.Count ?? 0}");
        }
        finally
        {
            _presentation.Execute(PresentationCommand.ExitAll);
            _prefs.Update(_ => original);
        }
    }
}

/// <summary>Синтетические нажатия для самопроверки (только в собственное активное окно).</summary>
internal static partial class SelfInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Ki;
        private readonly long _pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    public static void Key(ushort vk, ushort scan, bool down)
    {
        var input = new Input { Type = 1, Ki = new KeyboardInput { Vk = vk, Scan = scan, Flags = down ? 0u : 2u } };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static nint Foreground() => GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();
}
