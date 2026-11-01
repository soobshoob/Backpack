using Backpack.Core.Input;

namespace Backpack.Core.Tests;

/// <summary>Phase 4G: мультимедийные клавиши и F13–F24 получают имена вместо «SC E0 20».</summary>
public class Phase4GKeyTests
{
    [Theory]
    [InlineData(ScanPrefix.E0, 0x20, "VolumeMute")]   // Fn+F1 на ноутбуке пользователя приходит именно так
    [InlineData(ScanPrefix.E0, 0x2E, "VolumeDown")]
    [InlineData(ScanPrefix.E0, 0x30, "VolumeUp")]
    [InlineData(ScanPrefix.E0, 0x22, "MediaPlayPause")]
    [InlineData(ScanPrefix.E0, 0x19, "MediaNext")]
    [InlineData(ScanPrefix.E0, 0x10, "MediaPrev")]
    [InlineData(ScanPrefix.E0, 0x24, "MediaStop")]
    [InlineData(ScanPrefix.None, 0x64, "F13")]
    [InlineData(ScanPrefix.None, 0x76, "F24")]
    public void Media_and_extended_function_keys_have_names(ScanPrefix prefix, byte scan, string expected) =>
        Assert.Equal(expected, KeyInfo.Label(new PhysicalKey(prefix, scan).Slot));

    [Fact]
    public void Media_keys_do_not_shadow_ordinary_keys()
    {
        Assert.Equal("D", KeyInfo.Label(new PhysicalKey(ScanPrefix.None, 0x20).Slot));     // тот же scan без E0 — буква D
        Assert.Equal("Q", KeyInfo.Label(new PhysicalKey(ScanPrefix.None, 0x10).Slot));
        Assert.StartsWith("SC ", KeyInfo.Label(new PhysicalKey(ScanPrefix.E0, 0x7A).Slot)); // неизвестное остаётся честным кодом
    }
}
