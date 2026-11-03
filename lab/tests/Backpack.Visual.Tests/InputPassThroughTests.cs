using System.IO;
using System.Text.RegularExpressions;

namespace Backpack.Visual.Tests;

/// <summary>
/// Backpack только наблюдает ввод: низкоуровневые хуки клавиатуры и мыши всегда передают событие дальше (CallNextHookEx), Raw Input
/// регистрируется без RIDEV_NOLEGACY (ввод не отнимается у программ), источник приватности использует WinEvent-хуки вне контекста
/// и не вызывает функций, способных перехватить или подменить ввод. Проверка по исходному коду: реальные хуки в тестах не ставятся.
/// </summary>
public class InputPassThroughTests
{
    private static string Source(params string[] path) => File.ReadAllText(Repo.PathOf(["lab", "src", .. path]));

    [Fact]
    public void Low_level_hooks_always_call_the_next_hook_and_never_swallow_input()
    {
        string hook = Source("Backpack.Win32", "LowLevelHookSource.cs");
        foreach (var proc in new[] { "KeyboardProc", "MouseProc" })
        {
            var body = Regex.Match(hook, proc + @"\(int code, nint wParam, nint lParam\)\s*\{(?<b>.*?)\n    \}", RegexOptions.Singleline).Groups["b"].Value;
            Assert.False(string.IsNullOrEmpty(body), proc);
            var returns = Regex.Matches(body, @"\breturn\b[^;]*;").Select(m => m.Value.Trim()).ToList();
            Assert.Equal(["return Native.CallNextHookEx(0, code, wParam, lParam);"], returns);
        }
    }

    [Fact]
    public void Raw_input_and_privacy_sources_only_observe()
    {
        string raw = Source("Backpack.Win32", "RawInputSource.cs");
        Assert.DoesNotContain("RIDEV_NOLEGACY", raw);
        Assert.DoesNotContain("RIDEV_NOHOTKEYS", raw);
        Assert.DoesNotContain("RIDEV_CAPTUREMOUSE", raw);

        string privacy = Source("Backpack.Win32", "FocusPrivacySource.cs") + Source("Backpack.App", "UiaFocusEvents.cs");
        foreach (var api in new[] { "SendInput", "keybd_event", "BlockInput", "AttachThreadInput", "SetFocus", "SetForegroundWindow", "SetKeyboardState", "WH_KEYBOARD" })
        {
            Assert.DoesNotContain(api, privacy);
        }

        // WinEvent-хуки приватности — вне контекста (флаги 0): программа с фокусом не ждёт Backpack.
        Assert.Matches(@"SetWinEventHook\(EventObjectFocus, EventObjectFocus, 0, &OnWinEvent, 0, 0, 0\)", privacy);
        Assert.Matches(@"SetWinEventHook\(EventSystemForeground, EventSystemForeground, 0, &OnWinEvent, 0, 0, 0\)", privacy);
    }
}
