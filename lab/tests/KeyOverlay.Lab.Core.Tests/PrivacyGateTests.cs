using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;
using KeyOverlay.Lab.Core.Privacy;

namespace KeyOverlay.Lab.Core.Tests;

/// <summary>
/// Динамическая приватность на уровне логики: версии контекста, таймауты, устаревшие результаты, удержания, отсутствие
/// воспроизведения. Это не проверка реальных UIA-провайдеров (см. самопроверку окон и отчёт Phase 4A).
/// </summary>
public class PrivacyGateTests
{
    private const int Ctrl = 0x1D, V = 0x2F, A = 0x1E, Shift = 0x2A;
    private static readonly TimeSpan T0 = TimeSpan.FromSeconds(10);

    /// <summary>Фильтр + модель отображения, как в контроллере: всё, что прошло фильтр, попадает в модель.</summary>
    private sealed class Pipeline
    {
        public readonly PrivacyGate Gate = new(TimeSpan.FromMilliseconds(400));
        public readonly DisplayModel Display = new(TimeSpan.FromSeconds(5));
        public readonly List<KeyTransition> Delivered = new();
        public int Clears;

        public Pipeline()
        {
            Gate.ClearRequested += () =>
            {
                Clears++;
                Display.Clear();
            };
        }

        public void Key(int slot, bool down, long at = 0)
        {
            var t = new KeyTransition(slot, down ? KeyState.Down : KeyState.Up, at);
            if (Gate.Filter(t))
            {
                Deliver(t);
            }
        }

        private void Deliver(KeyTransition t)
        {
            Delivered.Add(t);
            Display.OnTransition(t, T0);
        }

        public void Tap(int slot, long at = 0)
        {
            Key(slot, true, at);
            Key(slot, false, at == 0 ? 0 : at + 1);
        }

        /// <summary>Результат проверки, как в контроллере: подтверждённое обычное поле передаёт ожидавшие переходы.</summary>
        public void Result(long version, ProbeVerdict verdict)
        {
            Gate.OnProbeResult(version, verdict);
            foreach (var t in Gate.TakeReleased())
            {
                Deliver(t);
            }
        }

        public long FocusAt(long start) => Gate.OnFocusChanged(T0, start);

        public long Focus() => Gate.OnFocusChanged(T0);

        public void Allow() => Gate.OnProbeResult(Focus(), ProbeVerdict.NotPassword);
    }

    [Fact]
    public void Before_first_check_nothing_is_shown()
    {
        var p = new Pipeline();
        Assert.False(p.Gate.Open);
        Assert.Equal(PrivacyState.Checking, p.Gate.State);
        p.Tap(A);
        Assert.Empty(p.Delivered);
        Assert.Empty(p.Display.Entries);
    }

    [Fact]
    public void Allowed_context_shows_input()
    {
        var p = new Pipeline();
        p.Allow();
        Assert.True(p.Gate.Open);
        p.Key(A, true);
        Assert.Single(p.Display.Entries);
    }

    [Fact]
    public void New_focus_closes_before_input_of_the_same_batch()
    {
        // Смена контекста замечена перед применением порции: ввод этой порции не показывается до проверки.
        var p = new Pipeline();
        p.Allow();
        p.Focus();
        p.Tap(A);
        Assert.Empty(p.Delivered);
        Assert.Equal(PrivacyReason.Checking, p.Gate.Reason);
    }

    [Fact]
    public void Stale_result_does_not_open_the_new_context()
    {
        var p = new Pipeline();
        long v1 = p.Focus();
        long v2 = p.Focus();
        Assert.False(p.Gate.OnProbeResult(v1, ProbeVerdict.NotPassword));
        Assert.False(p.Gate.Open);
        Assert.Equal(1, p.Gate.StaleResults);
        Assert.True(p.Gate.OnProbeResult(v2, ProbeVerdict.Password));
        Assert.Equal(PrivacyState.Protected, p.Gate.State);
    }

    [Fact]
    public void Slow_probe_times_out_to_no_data_and_stays_closed()
    {
        var p = new Pipeline();
        long v = p.Gate.OnFocusChanged(T0);
        Assert.False(p.Gate.Tick(T0 + TimeSpan.FromMilliseconds(399)));
        Assert.True(p.Gate.Tick(T0 + TimeSpan.FromMilliseconds(400)));
        Assert.Equal(PrivacyState.Unknown, p.Gate.State);
        Assert.Equal(PrivacyReason.Timeout, p.Gate.Reason);
        p.Tap(A);
        Assert.Empty(p.Delivered);

        // Поздний ответ той же версии (контекст не менялся) открывает показ; новое нажатие видно.
        Assert.True(p.Gate.OnProbeResult(v, ProbeVerdict.NotPassword));
        p.Key(A, true);
        Assert.Single(p.Delivered);
    }

    [Fact]
    public void Missing_property_or_unavailable_provider_is_not_false()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.NoData);
        Assert.Equal(PrivacyState.Unknown, p.Gate.State);
        Assert.Equal(PrivacyReason.NoData, p.Gate.Reason);
        Assert.False(p.Gate.Open);
        p.Tap(A);
        Assert.Empty(p.Delivered);
    }

    [Fact]
    public void Password_field_hides_all_keys_including_ctrl_v()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.Password);
        p.Key(Ctrl, true);
        p.Key(V, true);
        p.Key(V, false);
        p.Key(Ctrl, false);
        Assert.Empty(p.Delivered);
        Assert.Empty(p.Display.Entries);
        Assert.Equal(PrivacyReason.PasswordField, p.Gate.Reason);
    }

    [Fact]
    public void Blocked_application_is_protected_with_its_own_reason()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.BlockedApp);
        Assert.Equal(PrivacyState.Protected, p.Gate.State);
        Assert.Equal(PrivacyReason.BlockedApp, p.Gate.Reason);
    }

    [Fact]
    public void Closing_clears_display_and_held_keys_do_not_reappear()
    {
        var p = new Pipeline();
        p.Allow();
        p.Key(Shift, true);
        Assert.Single(p.Display.Entries);

        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.Password);
        Assert.Equal(1, p.Clears);
        Assert.Empty(p.Display.Entries);

        // Защита снята, Shift всё ещё держится: он не появляется как новое нажатие.
        p.Allow();
        Assert.Equal(1, p.Gate.HiddenHeldCount);
        p.Key(A, true);
        Assert.Single(p.Display.Entries);
        Assert.DoesNotContain(p.Display.Entries, e => e.KeySlot == Shift);

        // Граница — отпускание: следующее нажатие Shift видно.
        p.Key(Shift, false);
        Assert.Equal(0, p.Gate.HiddenHeldCount);
        p.Key(Shift, true);
        Assert.Contains(p.Display.Entries, e => e.KeySlot == Shift);
    }

    [Fact]
    public void Ctrl_held_in_password_field_does_not_form_a_chord_after_unlock()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.Password);
        p.Key(Ctrl, true);
        p.Allow();
        p.Key(V, true);
        Assert.Single(p.Display.Entries);
        Assert.Equal(V, p.Display.Entries[0].KeySlot); // не «Ctrl+V»: удержание Ctrl скрыто до отпускания
        p.Key(V, false);
        p.Key(Ctrl, false);
        Assert.DoesNotContain(p.Delivered, t => t.KeySlot == Ctrl);
    }

    [Fact]
    public void Hidden_input_is_never_replayed()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.Password);
        for (int i = 0; i < 8; i++)
        {
            p.Tap(A + i);
        }

        p.Allow();
        Assert.Empty(p.Delivered);
        Assert.Empty(p.Display.Entries);
        Assert.Equal(0, p.Gate.HiddenHeldCount);
    }

    [Fact]
    public void Dynamic_type_change_closes_without_showing_first()
    {
        // Поле сменило тип на парольный: событие фокуса/свойства → новая версия → закрыто до результата.
        var p = new Pipeline();
        p.Allow();
        long v = p.Focus();
        p.Key(A, true); // ввод между сменой и результатом
        p.Gate.OnProbeResult(v, ProbeVerdict.Password);
        p.Key(A, false);
        Assert.Empty(p.Delivered);
    }

    [Fact]
    public void Mouse_buttons_pass_while_closed()
    {
        var p = new Pipeline();
        p.Gate.OnProbeResult(p.Focus(), ProbeVerdict.Password);
        p.Key(Slots.Mouse(MouseButton.Left), true);
        Assert.Single(p.Delivered);
    }

    [Fact]
    public void Disabled_gate_is_open_and_enabling_closes_and_clears()
    {
        var p = new Pipeline();
        p.Gate.Enabled = false;
        Assert.True(p.Gate.Open);
        p.Key(A, true);
        Assert.Single(p.Display.Entries);

        p.Gate.Enabled = true; // состояние — проверка, показ закрыт
        Assert.False(p.Gate.Open);
        Assert.Empty(p.Display.Entries);
        p.Key(A, false);
        Assert.Single(p.Delivered);
    }

    [Fact]
    public void Repeated_focus_changes_create_no_backlog()
    {
        var p = new Pipeline();
        long last = 0;
        for (int i = 0; i < 1000; i++)
        {
            last = p.Focus();
        }

        for (long v = 1; v < last; v++)
        {
            Assert.False(p.Gate.OnProbeResult(v, ProbeVerdict.NotPassword));
        }

        Assert.False(p.Gate.Open);
        Assert.True(p.Gate.OnProbeResult(last, ProbeVerdict.NotPassword));
        Assert.True(p.Gate.Open);
    }

    // ── Phase 4E: контекст по времени нажатия ────────────────────────────────

    private const int Tab = 0x0F, LWin = (1 << 7) | 0x5B, E = 0x12, B = 0x30, P = 0x19, W = 0x11;

    [Fact]
    public void Tab_pressed_in_confirmed_normal_field_is_shown_although_its_focus_change_is_processed_first()
    {
        // Tab захвачен в обычном поле (t=200) и сам переводит фокус; уведомление (t=250) обработано раньше порции ввода.
        var p = new Pipeline();
        p.Result(p.FocusAt(100), ProbeVerdict.NotPassword);
        long next = p.FocusAt(250);
        p.Key(Tab, true, 200);
        Assert.Single(p.Delivered);
        p.Key(Tab, false, 260); // отпускание показанной клавиши проходит и в проверяемом контексте
        Assert.Equal(2, p.Delivered.Count);

        // Ввод в новом поле ждёт решения и показывается только после подтверждения именно этой версии.
        p.Key(A, true, 270);
        p.Key(A, false, 280);
        Assert.Equal(2, p.Delivered.Count);
        Assert.Equal(2, p.Gate.PendingCount);
        p.Result(next, ProbeVerdict.NotPassword);
        Assert.Equal(new[] { Tab, Tab, A, A }, p.Delivered.Select(t => t.KeySlot));
        Assert.Equal(0, p.Clears);
    }

    [Fact]
    public void Win_combination_opening_a_system_surface_is_shown_and_released()
    {
        // Win+E в обычном поле: Проводник получает фокус, проверка — «нет данных». Сочетание видно, отпускания проходят,
        // отображение не очищается; новый ввод без достоверных данных — скрыт.
        var p = new Pipeline();
        p.Result(p.FocusAt(100), ProbeVerdict.NotPassword);
        p.Key(LWin, true, 200);
        p.Key(E, true, 210);
        long v = p.FocusAt(240);
        p.Result(v, ProbeVerdict.NoData);
        p.Key(E, false, 250);
        p.Key(LWin, false, 260);
        Assert.Equal(new[] { LWin, E, E, LWin }, p.Delivered.Select(t => t.KeySlot));
        Assert.Equal(0, p.Clears);
        Assert.Contains(p.Display.Entries, e => e.KeySlot == E);
        p.Tap(B, 300);
        Assert.Equal(4, p.Delivered.Count);
    }

    [Fact]
    public void Confirmed_normal_field_shows_exactly_what_disabled_privacy_shows()
    {
        var keys = new[] { (Ctrl, true), (V, true), (V, false), (Ctrl, false), (Tab, true), (Tab, false), (LWin, true), (E, true), (E, false), (LWin, false), (Shift, true), (A, true), (A, false), (Shift, false) };
        var off = new Pipeline();
        off.Gate.Enabled = false;
        var on = new Pipeline();
        on.Result(on.FocusAt(1), ProbeVerdict.NotPassword);
        long at = 10;
        foreach (var (slot, down) in keys)
        {
            off.Key(slot, down, at);
            on.Key(slot, down, at);
            at += 10;
        }

        Assert.Equal(off.Delivered, on.Delivered);
        Assert.Equal(off.Display.Entries.Select(e => e.KeySlot), on.Display.Entries.Select(e => e.KeySlot));
    }

    [Fact]
    public void Normal_to_protected_to_normal_never_shows_protected_input_later()
    {
        var p = new Pipeline();
        p.Result(p.FocusAt(100), ProbeVerdict.NotPassword);
        p.Tap(A, 150);                                         // обычное поле
        long protectedVersion = p.FocusAt(200);
        p.Key(P, true, 205);                                   // набор до ответа проверки — ждёт
        p.Result(protectedVersion, ProbeVerdict.Password);     // защищённое поле: ожидавшее отброшено, отображение очищено
        p.Key(P, false, 215);
        p.Tap(W, 220);
        p.Key(Tab, true, 230);                                 // Tab в защищённом поле — скрыт
        long normal = p.FocusAt(240);
        p.Key(Tab, false, 245);                                // отпускание скрытого Tab — скрыто
        p.Tap(B, 260);
        p.Result(normal, ProbeVerdict.NotPassword);
        p.Key(P, true, 270);                                   // новое нажатие после подтверждения — видно как обычно
        Assert.Equal(new[] { A, A, B, B, P }, p.Delivered.Select(t => t.KeySlot));
        Assert.Equal(1, p.Clears);
    }

    [Fact]
    public void Pending_input_of_an_unconfirmed_version_is_discarded_on_the_next_focus_change()
    {
        var p = new Pipeline();
        long first = p.FocusAt(100);
        p.Key(A, true, 110);
        long second = p.FocusAt(150);               // версия first так и не подтверждена
        p.Result(first, ProbeVerdict.NotPassword);  // поздний ответ старой версии не открывает показ и не возвращает ввод
        p.Key(A, false, 160);
        p.Key(B, true, 120);                        // поздно доставленное событие старого неподтверждённого контекста — скрыто
        p.Result(second, ProbeVerdict.NotPassword);
        Assert.Empty(p.Delivered);
        Assert.Equal(1, p.Gate.PendingDiscards);
    }

    [Fact]
    public void Pending_input_never_appears_after_timeout_even_if_the_late_answer_is_normal()
    {
        var p = new Pipeline();
        long v = p.FocusAt(100);
        p.Tap(A, 110);
        Assert.True(p.Gate.Tick(T0 + TimeSpan.FromMilliseconds(400)));
        p.Result(v, ProbeVerdict.NotPassword);
        Assert.Empty(p.Delivered);
        p.Tap(B, 500);
        Assert.Equal(new[] { B, B }, p.Delivered.Select(t => t.KeySlot));
    }

    [Fact]
    public void Pending_overflow_discards_everything_waiting()
    {
        var p = new Pipeline();
        long v = p.FocusAt(100);
        for (int i = 0; i < PrivacyGate.MaxPending + 4; i++)
        {
            p.Key(A + (i / 2 % 8), i % 2 == 0, 110 + i); // пары нажатие–отпускание
        }

        p.Result(v, ProbeVerdict.NotPassword);
        Assert.True(p.Delivered.Count < PrivacyGate.MaxPending);
        Assert.True(p.Gate.PendingDiscards >= 1);
    }

    [Fact]
    public void Contexts_with_unknown_boundary_show_input_only_if_all_were_normal()
    {
        // Две смены с одинаковой (неизвестной) границей: ввод между ними неотличим — если хоть одна версия не обычное поле, скрыт.
        var p = new Pipeline();
        p.Result(p.FocusAt(100), ProbeVerdict.Password);
        p.Result(p.FocusAt(100), ProbeVerdict.NotPassword);
        p.FocusAt(300);
        p.Key(A, true, 200);
        Assert.Empty(p.Delivered);
    }
}
