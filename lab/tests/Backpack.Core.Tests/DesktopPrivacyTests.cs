using Backpack.Core.Input;
using Backpack.Core.Model;
using Backpack.Core.Privacy;

namespace Backpack.Core.Tests;

/// <summary>
/// Phase 4F: ложное срабатывание приватности на рабочем столе Windows. Причина (воспроизведена на настоящем рабочем столе):
/// фокус — список значков <c>Progman → SHELLDLL_DefView → SysListView32</c>, ControlType List, FrameworkId Win32, а свойство
/// IsPassword у прокси MSAA не поддерживается. Это давало «нет данных» (новый ввод скрыт), событий фокуса больше не приходило —
/// защита «залипала» до перехода в другое окно.
/// </summary>
public class DesktopPrivacyTests
{
    private const int Ctrl = 0x1D, A = 0x1E, C = 0x2E, Q = 0x10, W = 0x11, Tab = 0x0F;
    private static readonly TimeSpan T0 = TimeSpan.FromSeconds(10);

    /// <summary>То, что сообщает настоящий рабочий стол Windows 11 (снято зондом UIA).</summary>
    private static readonly FocusFacts Desktop = new(PasswordProperty.NotSupported, FocusClassifier.ControlTypeList, "Win32",
        "SysListView32", "SHELLDLL_DefView", "Progman", RootIsForeground: true, "explorer.exe");

    [Fact]
    public void Desktop_icon_list_without_IsPassword_is_an_ordinary_element()
    {
        Assert.Equal(ProbeVerdict.NotPassword, FocusClassifier.Classify(Desktop));
        Assert.Equal(ProbeVerdict.NotPassword, FocusClassifier.Classify(Desktop with { ControlType = FocusClassifier.ControlTypeListItem }));
        Assert.Equal(ProbeVerdict.NotPassword, FocusClassifier.Classify(Desktop with { RootClass = "WorkerW" }));
    }

    [Theory]
    [InlineData("process")]
    [InlineData("root")]
    [InlineData("parent")]
    [InlineData("class")]
    [InlineData("framework")]
    [InlineData("control")]
    [InlineData("foreground")]
    public void Exception_is_limited_to_the_desktop_icon_list(string differs)
    {
        var f = differs switch
        {
            "process" => Desktop with { Process = "notexplorer.exe" },
            "root" => Desktop with { RootClass = "CabinetWClass" }, // окно Проводника — не рабочий стол
            "parent" => Desktop with { FocusParentClass = "DirectUIHWND" },
            "class" => Desktop with { FocusClass = "Edit" },
            "framework" => Desktop with { FrameworkId = "XAML" },
            "control" => Desktop with { ControlType = 50004 /* Edit */ }, // поле переименования значка
            _ => Desktop with { RootIsForeground = false },
        };
        Assert.Equal(ProbeVerdict.NoData, FocusClassifier.Classify(f));
    }

    [Fact]
    public void Missing_element_and_errors_stay_no_data_and_password_always_wins()
    {
        Assert.Equal(ProbeVerdict.NoData, FocusClassifier.Classify(new FocusFacts(PasswordProperty.Error)));
        Assert.Equal(ProbeVerdict.NoData, FocusClassifier.Classify(Desktop with { Password = PasswordProperty.Error }));
        Assert.Equal(ProbeVerdict.Password, FocusClassifier.Classify(Desktop with { Password = PasswordProperty.True }));
        Assert.Equal(ProbeVerdict.NotPassword, FocusClassifier.Classify(new FocusFacts(PasswordProperty.False)));
        // Панель задач (XAML) без IsPassword — не исключение: «нет данных».
        Assert.Equal(ProbeVerdict.NoData, FocusClassifier.Classify(new FocusFacts(PasswordProperty.NotSupported, 50033, "XAML", "Windows.UI.Input.InputSite.WindowClass", "Shell_TrayWnd", "Shell_TrayWnd", true, "explorer.exe")));
    }

    private sealed class Pipeline
    {
        public readonly PrivacyGate Gate = new(TimeSpan.FromMilliseconds(400));
        public readonly DisplayModel Display = new(TimeSpan.FromSeconds(5));
        public readonly List<int> Shown = new();
        public int Clears;

        public Pipeline() => Gate.ClearRequested += () =>
        {
            Clears++;
            Display.Clear();
        };

        public void Key(int slot, bool down)
        {
            var t = new KeyTransition(slot, down ? KeyState.Down : KeyState.Up, 0);
            if (Gate.Filter(t))
            {
                Deliver(t);
            }
        }

        private void Deliver(KeyTransition t)
        {
            if (t.State == KeyState.Down)
            {
                Shown.Add(t.KeySlot);
            }

            Display.OnTransition(t, T0);
        }

        public void Chord(params int[] slots)
        {
            foreach (int s in slots)
            {
                Key(s, true);
            }

            foreach (int s in slots.Reverse())
            {
                Key(s, false);
            }
        }

        /// <summary>Смена фокуса и ответ проверки, как в контроллере (с передачей ожидавших переходов).</summary>
        public long Focus(FocusFacts facts) => Focus(FocusClassifier.Classify(facts));

        public long Focus(ProbeVerdict verdict)
        {
            long v = Gate.OnFocusChanged(T0);
            Result(v, verdict);
            return v;
        }

        public void Result(long version, ProbeVerdict verdict)
        {
            Gate.OnProbeResult(version, verdict);
            foreach (var t in Gate.TakeReleased())
            {
                Deliver(t);
            }
        }
    }

    [Fact]
    public void Normal_field_desktop_ctrl_combos_password_field_normal_field()
    {
        var p = new Pipeline();
        p.Focus(new FocusFacts(PasswordProperty.False)); // обычное поле
        p.Chord(Ctrl, A);
        Assert.Equal([Ctrl, A], p.Shown);

        p.Focus(Desktop); // рабочий стол
        Assert.True(p.Gate.Open);
        p.Chord(Ctrl, A);
        p.Chord(Ctrl, C);
        p.Key(Ctrl, true);
        p.Key(Ctrl, false);
        Assert.Equal([Ctrl, A, Ctrl, A, Ctrl, C, Ctrl], p.Shown);
        Assert.Equal(0, p.Clears);

        p.Focus(new FocusFacts(PasswordProperty.True)); // тестовое поле пароля
        Assert.False(p.Gate.Open);
        Assert.Equal(PrivacyReason.PasswordField, p.Gate.Reason);
        int before = p.Shown.Count;
        p.Chord(Q);
        p.Chord(Ctrl, W);
        Assert.Equal(before, p.Shown.Count);
        Assert.Empty(p.Display.Entries);

        p.Focus(new FocusFacts(PasswordProperty.False)); // снова обычное поле
        p.Chord(Tab);
        Assert.Equal([.. p.Shown.Take(before), Tab], p.Shown); // скрытое (Q, Ctrl+W) не воспроизводится
    }

    [Fact]
    public void Old_behaviour_no_data_on_desktop_recovers_by_recheck_without_switching_windows()
    {
        var p = new Pipeline();
        long v = p.Focus(ProbeVerdict.NoData); // ответ без исключения: «нет данных»
        Assert.Equal(PrivacyState.Unknown, p.Gate.State);
        p.Chord(Ctrl, A);
        Assert.Empty(p.Shown);

        // Редкая перепроверка той же версии (фокус не менялся) получила ответ «обычный элемент» — показ открыт без перехода в другое окно.
        p.Result(v, ProbeVerdict.NotPassword);
        Assert.True(p.Gate.Open);
        p.Chord(Ctrl, C);
        Assert.Equal([Ctrl, C], p.Shown); // прежнее скрытое Ctrl+A не появилось
    }

    [Fact]
    public void Repeated_identical_recheck_does_not_clear_again()
    {
        var p = new Pipeline();
        long v = p.Focus(ProbeVerdict.Password);
        Assert.Equal(1, p.Clears);
        long closures = p.Gate.Closures;
        int changes = 0;
        p.Gate.Changed += () => changes++;
        p.Result(v, ProbeVerdict.Password);
        p.Result(v, ProbeVerdict.Password);
        Assert.Equal(1, p.Clears);
        Assert.Equal(closures, p.Gate.Closures);
        Assert.Equal(0, changes);
    }
}
