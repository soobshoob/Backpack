using Backpack.Core.Model;
using Backpack.Core.Privacy;

namespace Backpack.Core.Tests;

/// <summary>
/// Регрессия 0.4.0-preview.8 (динамическая приватность и CAD-программы): проверки фокуса через UI Automation — только по событиям
/// и ограниченное число раз, без периодического опроса программы при ответе «нет данных» (3D-вид без IsPassword); повторные
/// уведомления о фокусе на том же элементе не начинают новую проверку. Фильтр приватности решает только, что Backpack
/// <i>показывает</i>: одиночные клавиши, сочетания с Ctrl/Alt/Shift, автоповтор и отпускания обрабатываются последовательно.
/// Реальные хуки Windows и SOLIDWORKS здесь не участвуют — их проверка ручная (docs/release/RELEASE-NOTES-0.4.0-preview.8.md).
/// </summary>
public class PrivacyInterferenceTests
{
    private const int Ctrl = 0x1D, Shift = 0x2A, Alt = 0x38, S = 0x1F, D = 0x20, Digit1 = 0x02, Space = 0x39;
    private static readonly TimeSpan T0 = TimeSpan.FromSeconds(5);

    /// <summary>Модель рабочего потока проверки: сколько раз он обратится к программе за <paramref name="span"/> после одного события.</summary>
    private static int ProbesAfterOneFocusEvent(ProbeVerdict verdict, TimeSpan span)
    {
        int probes = 1, retries = 0; // первая проверка сразу по событию
        if (verdict == ProbeVerdict.NotPassword)
        {
            return probes;           // открыто — перепроверять нечего
        }

        double t = 0;
        while (true)
        {
            int wait = FocusProbePolicy.NextWaitMs(retries);
            if (wait == Timeout.Infinite || (t += wait) > span.TotalMilliseconds)
            {
                return probes;
            }

            probes++;
            retries++;
        }
    }

    [Theory]
    [InlineData(ProbeVerdict.NoData)]
    [InlineData(ProbeVerdict.Password)]
    [InlineData(ProbeVerdict.BlockedApp)]
    public void A_refusing_answer_is_rechecked_a_few_times_and_then_never_polled_again(ProbeVerdict verdict)
    {
        // Фокус остаётся в 3D-виде час: прежде это давало проверку каждые 1,5 с (≈2400 обращений к программе), теперь — 4.
        Assert.Equal(FocusProbePolicy.MaxProbesPerFocusChange, ProbesAfterOneFocusEvent(verdict, TimeSpan.FromHours(1)));
        Assert.Equal(4, FocusProbePolicy.MaxProbesPerFocusChange);
        Assert.Equal(Timeout.Infinite, FocusProbePolicy.NextWaitMs(FocusProbePolicy.RetryDelaysMs.Count));
        Assert.Equal(Timeout.Infinite, FocusProbePolicy.NextWaitMs(int.MaxValue));
    }

    [Fact]
    public void An_ordinary_field_is_checked_once()
    {
        Assert.Equal(1, ProbesAfterOneFocusEvent(ProbeVerdict.NotPassword, TimeSpan.FromHours(1)));
        Assert.Equal(new[] { 120, 400, 1200 }, FocusProbePolicy.RetryDelaysMs);
        Assert.All(FocusProbePolicy.RetryDelaysMs, d => Assert.InRange(d, 1, 2000)); // ограниченные, а не периодические
    }

    [Fact]
    public void Repeated_focus_notifications_for_the_same_element_are_noise()
    {
        var f = new FocusNoiseFilter();
        string viewport = FocusNoiseFilter.WinEventKey(0x1234, -4, 0);
        Assert.True(f.Accept(FocusSource.WinEvent, viewport, T0));
        // Программа повторно объявляет фокус на том же 3D-виде при каждой команде с клавиатуры (каждые 50 мс, 10 с).
        int accepted = 0;
        for (int i = 1; i <= 200; i++)
        {
            accepted += f.Accept(FocusSource.WinEvent, viewport, T0 + TimeSpan.FromMilliseconds(50 * i)) ? 1 : 0;
        }

        Assert.Equal(0, accepted);
        Assert.Equal(200, f.Suppressed);
        // После паузы дольше окна — обычное уведомление (новая проверка).
        Assert.True(f.Accept(FocusSource.WinEvent, viewport, T0 + TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void A_different_element_a_window_switch_or_an_unknown_element_is_always_a_new_context()
    {
        var f = new FocusNoiseFilter();
        string a = FocusNoiseFilter.WinEventKey(0x10, -4, 0), b = FocusNoiseFilter.WinEventKey(0x20, -4, 0);
        Assert.True(f.Accept(FocusSource.WinEvent, a, T0));
        Assert.True(f.Accept(FocusSource.WinEvent, b, T0));                        // поле размера после 3D-вида
        Assert.True(f.Accept(FocusSource.WinEvent, a, T0));                        // и обратно
        f.OnForegroundChanged();
        Assert.True(f.Accept(FocusSource.WinEvent, a, T0));                        // другое окно и снова то же — проверить
        Assert.True(f.Accept(FocusSource.WinEvent, null, T0));
        Assert.True(f.Accept(FocusSource.WinEvent, null, T0));                     // неизвестный элемент не отсеивается

        // Внутри одного окна (WPF): элементы различаются только по RuntimeId событий UIA.
        string text = FocusNoiseFilter.RuntimeIdKey([42, 7, 1])!, password = FocusNoiseFilter.RuntimeIdKey([42, 7, 2])!;
        Assert.True(f.Accept(FocusSource.UiAutomation, text, T0));
        Assert.True(f.Accept(FocusSource.UiAutomation, password, T0));             // переход в поле пароля всегда проверяется
        Assert.False(f.Accept(FocusSource.UiAutomation, password, T0));
        // Новый элемент от одного источника сбрасывает память другого: возврат фокуса в окно снова проверяется.
        Assert.True(f.Accept(FocusSource.WinEvent, a, T0));
        Assert.True(f.Accept(FocusSource.UiAutomation, password, T0));
        Assert.Null(FocusNoiseFilter.RuntimeIdKey(null));
        Assert.Null(FocusNoiseFilter.RuntimeIdKey([]));
    }

    // ── фильтр показа: клавиши обрабатываются последовательно, без «залипания» модификаторов ──

    private static (PrivacyGate Gate, List<KeyTransition> Shown) Gate(bool enabled = true)
    {
        var g = new PrivacyGate(TimeSpan.FromMilliseconds(400)) { Enabled = enabled };
        return (g, new List<KeyTransition>());
    }

    private static void Key(PrivacyGate g, List<KeyTransition> shown, int slot, bool down)
    {
        var t = new KeyTransition(slot, down ? KeyState.Down : KeyState.Up);
        if (g.Filter(t))
        {
            shown.Add(t);
        }
    }

    [Fact]
    public void Disabled_privacy_shows_every_key_combination_repeat_and_release()
    {
        var (g, shown) = Gate(enabled: false);
        int[] sequence = [S, Ctrl, S, Shift, Alt, D, Space, Digit1];
        foreach (var k in sequence)
        {
            Key(g, shown, k, true);
            Key(g, shown, k, true); // автоповтор
        }

        foreach (var k in sequence.Reverse())
        {
            Key(g, shown, k, false);
        }

        Assert.Equal(sequence.Length * 3, shown.Count);
    }

    [Fact]
    public void In_an_ordinary_field_shortcuts_single_keys_and_modifiers_are_shown_in_order()
    {
        var (g, shown) = Gate();
        long v = g.OnFocusChanged(T0);
        g.OnProbeResult(v, ProbeVerdict.NotPassword);
        Key(g, shown, Ctrl, true);
        Key(g, shown, Shift, true);
        Key(g, shown, S, true);
        Key(g, shown, S, true);  // автоповтор
        Key(g, shown, S, false);
        Key(g, shown, Shift, false);
        Key(g, shown, Ctrl, false);
        Key(g, shown, Digit1, true);
        Key(g, shown, Digit1, false);
        Assert.Equal(9, shown.Count);
        Assert.Equal([Ctrl, Shift, S, S, S, Shift, Ctrl, Digit1, Digit1], shown.Select(t => t.KeySlot));
        Assert.True(g.Open);
    }

    [Fact]
    public void Without_data_new_keys_are_hidden_but_held_modifiers_are_still_released()
    {
        var (g, shown) = Gate();
        long v = g.OnFocusChanged(T0);
        g.OnProbeResult(v, ProbeVerdict.NotPassword);
        Key(g, shown, Ctrl, true);                      // нажат в обычном поле
        long v2 = g.OnFocusChanged(T0);                 // фокус ушёл в 3D-вид без IsPassword
        g.OnProbeResult(v2, ProbeVerdict.NoData);
        Key(g, shown, S, true);
        Key(g, shown, S, false);
        Key(g, shown, Ctrl, false);                     // отпускание показанного модификатора проходит — он не «залипает»
        Assert.Equal([(Ctrl, KeyState.Down), (Ctrl, KeyState.Up)], shown.Select(t => (t.KeySlot, t.State)));
        Assert.False(g.Open);
    }

    [Fact]
    public void Suppressed_duplicate_focus_notifications_keep_an_open_display_open()
    {
        // Без отсева каждый повтор фокуса начинал новую версию: показ закрывался до новой проверки и клавиши 3D-вида пропадали.
        var (g, shown) = Gate();
        var noise = new FocusNoiseFilter();
        string viewport = FocusNoiseFilter.WinEventKey(0x99, -4, 0);
        if (noise.Accept(FocusSource.WinEvent, viewport, T0))
        {
            g.OnProbeResult(g.OnFocusChanged(T0), ProbeVerdict.NotPassword);
        }

        for (int i = 0; i < 10; i++)
        {
            if (noise.Accept(FocusSource.WinEvent, viewport, T0 + TimeSpan.FromMilliseconds(20 * i)))
            {
                g.OnFocusChanged(T0); // не должно произойти
            }

            Key(g, shown, S, true);
            Key(g, shown, S, false);
        }

        Assert.Equal(20, shown.Count);
        Assert.Equal(1, g.Version);
    }
}
