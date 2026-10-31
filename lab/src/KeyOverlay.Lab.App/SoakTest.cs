using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyOverlay.Lab.Core.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;

namespace KeyOverlay.Lab.App;

/// <summary>
/// Длительная проверка отображения (Phase 4I, п. 4): <c>--lab --soak=минуты [--display-trace=файл] --settings=…</c>.
/// <para>
/// Ввод — только синтетические нажатия Q, W, E, Tab и Shift в собственном тестовом окне и только пока оно на переднем плане
/// (иначе шаг пропускается и считается). Личный ввод не пишется: журнал содержит этап конвейера и счётчики, без клавиш.
/// </para>
/// Циклы: печать и сочетания; поле пароля и обратно (скрытое не появляется); второе окно (смена приложения/фокуса); скрытие и
/// показ отображения; смена профиля; смена стиля во время показа; уведомления о смене мониторов, блокировке и выходе из сна
/// (вызов тех же обработчиков, что у системных сообщений — сама система не блокируется); простой. После каждого нажатия в
/// обычном поле проверяется, что кейкап дошёл до окна оверлея, окно видимо и поверх; при сбое пишется этап остановки.
/// </summary>
public partial class App
{
    private const ushort VkQ = 0x51, VkW = 0x57, VkE = 0x45, VkTab = 0x09, VkShift = 0x10;
    private const ushort ScQ = 0x10, ScW = 0x11, ScE = 0x12, ScTab = 0x0F, ScShift = 0x2A;

    private void RunSoak()
    {
        var c = _controller!;
        var end = DateTime.UtcNow.AddMinutes(_options!.SoakMinutes);
        var sw = Stopwatch.StartNew();
        int taps = 0, verified = 0, failures = 0, skipped = 0, leaks = 0, cycles = 0, pauses = 0;
        var paused = TimeSpan.Zero;
        long lastInject = 0;
        void Inject(ushort vk, ushort sc, bool down)
        {
            lastInject = Environment.TickCount64;
            SelfInput.Key(vk, sc, down);
        }

        var failStages = new Dictionary<string, int>();
        var first = new TextBox { Width = 260 };
        var password = new PasswordBox { Width = 260, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Backpack soak test: synthetic Q/W/E/Tab/Shift only while this window is in front." , TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(first);
        panel.Children.Add(password);
        var w = new Window { Title = "Backpack soak test", Content = panel, Width = 340, Height = 190, Left = 40, Top = 40, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = true };
        var other = new Window { Title = "Backpack soak test — second window", Content = new TextBox { Margin = new Thickness(16) }, Width = 300, Height = 120, Left = 400, Top = 40, WindowStartupLocation = WindowStartupLocation.Manual };
        w.Show();
        other.Show();
        nint hw = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        nint ho = new System.Windows.Interop.WindowInteropHelper(other).Handle;
        var store = _store!;
        var original = store.Current;

        void Note(string s)
        {
            c.ShutdownNotes.Add("soak: " + s);
            if (_options.DisplayTrace is { } p)
            {
                try
                {
                    System.IO.File.AppendAllText(p, $"{c.Uptime.TotalSeconds:F1}\tsoak\t{s}{Environment.NewLine}");
                }
                catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        // Пользователь работает за компьютером (ввод не от этой проверки) — пауза до 30 с тишины: окно проверки не выводится
        // вперёд и ничего не нажимается, пока человек печатает или двигает мышь.
        bool UserActive()
        {
            long idle = KeyOverlay.Lab.Win32.KeyStateProbe.IdleMilliseconds() ?? long.MaxValue;
            long lastInput = Environment.TickCount64 - idle;
            return idle < 30_000 && lastInput > lastInject + 400;
        }

        bool Front(nint h, Window win, UIElement focus)
        {
            if (UserActive())
            {
                pauses++;
                var p0 = Stopwatch.StartNew();
                while (UserActive() && !_exiting)
                {
                    PumpFor(1000);
                }

                paused += p0.Elapsed;
            }

            for (int i = 0; i < 20 && (SelfInput.Foreground() != h || !focus.IsKeyboardFocused); i++)
            {
                if (SelfInput.Foreground() != h)
                {
                    win.Activate();
                }

                Keyboard.Focus(focus);
                PumpFor(100);
            }

            return SelfInput.Foreground() == h && focus.IsKeyboardFocused;
        }

        // Нажатие в обычном поле: кейкап должен дойти до окна оверлея (новое поколение записей, окно видимо и поверх).
        void TapVisible(ushort vk, ushort sc, nint h, Window win, UIElement focus, string step)
        {
            if (!Front(h, win, focus))
            {
                skipped++;
                return;
            }

            long refreshes = c.Watch.Refreshes, shown = c.Watch.Shown;
            Inject(vk, sc, true);
            taps++;
            bool ok = false;
            for (int t = 0; t < 1500 && !ok; t += 25)
            {
                PumpFor(25);
                ok = c.Watch.Shown > shown && c.Watch.Refreshes > refreshes && _overlays.Count > 0 && _overlays[0].Presenter.Visuals.Count > 0
                    && c.OverlayState is { Visible: true, Topmost: true };
            }

            Inject(vk, sc, false);
            PumpFor(60);
            if (ok)
            {
                verified++;
                return;
            }

            if (SelfInput.Foreground() != h)
            {
                skipped++; // фокус увела не проверка (например, уведомление) — не считается сбоем отображения
                return;
            }

            failures++;
            string stage = $"{step}: shown+{c.Watch.Shown - shown} refresh+{c.Watch.Refreshes - refreshes} visuals={(_overlays.Count > 0 ? _overlays[0].Presenter.Visuals.Count : -1)} " +
                $"window={c.OverlayState} privacy={c.Privacy.State}/{c.Privacy.Reason} stage={c.Watch.Stage}";
            failStages[step] = failStages.GetValueOrDefault(step) + 1;
            Note("FAIL " + stage);
        }

        // Набор в поле пароля: ничего не должно дойти до модели отображения ни сразу, ни после возврата в обычное поле.
        void TapHidden(ushort vk, ushort sc)
        {
            if (!Front(hw, w, password))
            {
                skipped++;
                return;
            }

            PumpFor(700); // проверка фокуса UIA
            long shown = c.Watch.Shown;
            Inject(vk, sc, true);
            PumpFor(80);
            Inject(vk, sc, false);
            PumpFor(400);
            if (c.Watch.Shown != shown)
            {
                leaks++;
                Note($"LEAK: key from the password field reached the display; privacy={c.Privacy.State}/{c.Privacy.Reason}; history={string.Join(" ", c.Privacy.History.TakeLast(8))}");
            }
        }

        Note($"start: {_options.SoakMinutes} min");
        for (int i = 0; i < 100 && !(c.Privacy.Open && c.FocusSource is not null); i++)
        {
            PumpFor(100); // подписка UIA и первая проверка фокуса (до этого показ штатно закрыт)
        }
        var profileA = _profiles?.Active?.Id;
        string? profileB = _profiles?.CreateDefault("Soak B").Id;
        while (DateTime.UtcNow < end && !_exiting)
        {
            cycles++;
            // 1. Печать и сочетания.
            foreach (var (vk, sc) in new[] { (VkQ, ScQ), (VkW, ScW), (VkE, ScE) })
            {
                TapVisible(vk, sc, hw, w, first, "type");
            }

            if (Front(hw, w, first))
            {
                Inject(VkShift, ScShift, true);
                PumpFor(60);
                TapVisible(VkQ, ScQ, hw, w, first, "shift+q");
                Inject(VkShift, ScShift, false);
                PumpFor(60);
            }

            // 2. Защищённое поле и обратно: скрытое не появляется, обычное поле снова показывается.
            TapHidden(VkW, ScW);
            TapVisible(VkE, ScE, hw, w, first, "after-password");

            // 3. Другое окно (смена переднего плана) и обратно.
            TapVisible(VkQ, ScQ, ho, other, (UIElement)other.Content, "second-window");
            TapVisible(VkW, ScW, hw, w, first, "back-to-first");

            // 4. Скрытие и возвращение отображения.
            SetDisplayVisible(false);
            PumpFor(300);
            SetDisplayVisible(true);
            PumpFor(200);
            TapVisible(VkE, ScE, hw, w, first, "after-hide-show");

            // 5. Смена стиля во время показа и смена профиля.
            if (Front(hw, w, first))
            {
                Inject(VkQ, ScQ, true);
                PumpFor(100);
                store.Update(s => s with { Appearance = s.Appearance with { Renderer = s.Appearance.Renderer == KeyOverlay.Visual.Settings.KeycapRenderer.Pbt ? KeyOverlay.Visual.Settings.KeycapRenderer.LowProfile : KeyOverlay.Visual.Settings.KeycapRenderer.Pbt } });
                PumpFor(100);
                Inject(VkQ, ScQ, false);
                PumpFor(100);
            }

            if (_profiles is not null && profileA is not null && profileB is not null)
            {
                _profiles.SwitchTo(cycles % 2 == 0 ? profileA : profileB);
                PumpFor(300);
                TapVisible(VkW, ScW, hw, w, first, "after-profile");
            }

            // 6. Системные уведомления (те же обработчики, что у сообщений Windows): мониторы, блокировка, сон.
            c.OnDisplayChange();
            PumpFor(300);
            TapVisible(VkE, ScE, hw, w, first, "after-display-change");
            if (cycles % 3 == 0)
            {
                c.OnSessionChange(7);
                PumpFor(200);
                c.OnSessionChange(8);
                PumpFor(300);
                c.OnPower(4);
                c.OnPower(0x12);
                PumpFor(300);
                TapVisible(VkQ, ScQ, hw, w, first, "after-lock-resume");
            }

            // 7. Простой (дольше выдержки и проверки фокуса) и возобновление.
            PumpFor(cycles % 5 == 0 ? 45_000 : 6_000);
            TapVisible(VkW, ScW, hw, w, first, "after-idle");

            if (cycles % 10 == 0)
            {
                Note($"cycle {cycles}: {sw.Elapsed.TotalMinutes:F1} min, taps {taps}, verified {verified}, failures {failures}, skipped {skipped}, leaks {leaks}; {c.Watch.Line(c.Uptime)}");
            }
        }

        _profiles?.SwitchTo(profileA ?? "");
        if (profileB is not null)
        {
            _profiles?.Delete(profileB);
        }

        store.Update(_ => original);
        Note($"end: {sw.Elapsed.TotalMinutes:F1} min, cycles {cycles}, taps {taps}, verified {verified}, failures {failures}, skipped {skipped}, leaks {leaks}, user pauses {pauses} ({paused.TotalMinutes:F1} min), " +
            $"fail steps [{string.Join(",", failStages.Select(f => f.Key + ":" + f.Value))}], recovery {c.Recovery.Attempts}/{c.Recovery.Refused}; {c.Watch.Line(c.Uptime)}");
        w.Close();
        other.Close();
        ExitApplication();
    }
}
