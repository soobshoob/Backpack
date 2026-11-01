using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Tests;

public class SettingsTests
{
    /// <summary>Файл настроек Phase 2B (формат 1) — как его записывала прежняя версия.</summary>
    internal const string V1File = """
        {
          "format": "backpack-settings",
          "formatVersion": 1,
          "appearance": {
            "keycap": { "style": "lowprofile", "size": 40 },
            "colors": { "primary": "#2E3138", "secondary": "#16181C" },
            "text": { "color": "#F2E6D0" },
            "border": { "enabled": true, "width": 1.5, "color": "#0B0C0E", "radius": 0.35 },
            "group": { "background": { "enabled": true, "color": "#0B0C0ECC" } },
            "animation": { "type": "none", "durationMs": 300 }
          },
          "placement": { "anchor": "top-right", "marginX": 60, "marginY": 30 },
          "behavior": { "lingerMs": 1500 },
          "layoutIndicator": { "enabled": false, "durationMs": 2000, "position": "screen-center", "showOnWindowSwitch": false }
        }
        """;

    [Fact]
    public void ThemeColor_UsesAlphaLast_AndConvertsToWpfArgb()
    {
        var c = ThemeColor.Parse("#FFFFFF99");
        Assert.Equal(new ThemeColor(0xFF, 0xFF, 0xFF, 0x99), c);
        var w = c.ToWpf();
        Assert.Equal((byte)0x99, w.A);
        Assert.Equal("#99FFFFFF", w.ToString());          // WPF: #AARRGGBB
        Assert.Equal("#FFFFFF99", ThemeColor.FromWpf(w).ToString());
        Assert.Equal("#1A1A1A", ThemeColor.Parse("#1a1a1a").ToString());
        Assert.False(ThemeColor.TryParse("red", out _));
        Assert.False(ThemeColor.TryParse("#FFF", out _));
        Assert.False(ThemeColor.TryParse("#GGGGGG", out _));
        Assert.False(ThemeColor.TryParse("#FFFFFFF", out _));
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var s = new LabSettings(
            AppearanceSettings.Default with
            {
                Size = 44, Radius = 0.3, Primary = ThemeColor.Parse("#2E3138"), BackgroundColor = ThemeColor.Parse("#0B0C0ECC"),
                Animation = AnimationKind.None, DurationMs = 400, BorderEnabled = false, LabelSet = LabelSet.Linux, LabelMode = LabelMode.Symbol,
                SideMarkers = false, ShowPressCount = false, PressAnimation = false,
                Mouse = new MouseVisual(ThemeColor.Parse("#FF000080"), 60, 40, ThemeColor.Parse("#00000040"), ThemeColor.Parse("#EEEEEE"), ThemeColor.Parse("#00FF00")),
                Indicator = new IndicatorVisual(ThemeColor.Parse("#202020CC"), ThemeColor.Parse("#FAFAFA"), ThemeColor.Parse("#FFFFFF44"), 0, 1, 52),
            },
            new PlacementSettings(ScreenAnchor.Center, 12, 34, -150, 220),
            new KeysSettings(KeyFilter.CombosOnly, TrailMode.FadeInPlace, 1500, false, MouseLabelMode.Text),
            new LayoutIndicatorSettings(false, 2000, false, true, IndicatorAnimation.None, IndicatorPosition.Screen, ScreenAnchor.TopRight, 30, -40),
            new MouseCursorSettings(true, false, true, -30, 45));
        var read = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(read.Problems);
        Assert.False(read.FileUnusable);
        Assert.Null(read.MigratedFrom);
        Assert.Equal(s, read.Settings);
    }

    [Fact]
    public void AllLabelModes_RoundTripThroughThemeShapedFields()
    {
        foreach (var mode in Enum.GetValues<LabelMode>())
        {
            var s = LabSettings.Default with { Appearance = AppearanceSettings.Default with { LabelMode = mode } };
            Assert.Equal(mode, SettingsSerializer.Read(SettingsSerializer.Write(s)).Settings.Appearance.LabelMode);
        }
    }

    [Fact]
    public void FormatOne_IsMigrated_KeepingScreenPositionAndValues()
    {
        var read = SettingsSerializer.Read(V1File);
        Assert.False(read.FileUnusable);
        Assert.Equal(1, read.MigratedFrom);
        Assert.Empty(read.Problems);
        var s = read.Settings;
        Assert.Equal(40, s.Appearance.Size);
        Assert.Equal(AnimationKind.None, s.Appearance.Animation);
        Assert.Equal(1500, s.Keys.LingerMs);                        // behavior.lingerMs → keys.lingerMs
        Assert.Equal(new PlacementSettings(ScreenAnchor.TopRight, 60, 30, 0, 0), s.Placement);
        Assert.False(s.LayoutIndicator.Enabled);
        Assert.Equal(IndicatorPosition.Screen, s.LayoutIndicator.Position); // screen-center → экран, центр
        Assert.Equal(ScreenAnchor.Center, s.LayoutIndicator.Anchor);
        Assert.False(s.LayoutIndicator.ShowOnWindowSwitch);
        Assert.Equal(TrailMode.None, s.Keys.Trail);                 // новый рекомендуемый режим

        // То же место на экране, что давала формула 2B: полоса шириной (рабочая область − 2·marginX), группа справа.
        var work = new Rect(0, 0, 1920, 1040);
        double groupW = 300;
        var g = OverlayPlacement.GroupRect(work, s.Placement, s.Appearance, groupW);
        Assert.Equal(1920 - 60 - groupW, g.Left, 6);
        Assert.Equal(30 + KeycapMetrics.From(s.Appearance).OuterMargin, g.Top, 6);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{\"format\":\"keyviz\",\"formatVersion\":1}")]
    [InlineData("{\"format\":\"backpack-settings\"}")]
    [InlineData("{\"format\":\"backpack-settings\",\"formatVersion\":99}")]
    [InlineData("{\"format\":\"backpack-settings\",\"formatVersion\":0}")]
    public void UnusableFile_GivesDefaults_AndAMessage(string json)
    {
        var read = SettingsSerializer.Read(json);
        Assert.True(read.FileUnusable);
        Assert.Equal(Styles.BrandStyles.Starter, read.Settings); // как чистая установка (0.4.0-preview.7)
        Assert.Single(read.Problems);
    }

    [Fact]
    public void InvalidField_FallsBackPerField_KeepingTheRest()
    {
        var json = SettingsSerializer.ToJson(LabSettings.Default with { Keys = KeysSettings.Default with { LingerMs = 1234 } });
        json["appearance"]!["border"]!["radius"] = 1.5;
        json["appearance"]!["colors"]!["primary"] = "red";
        json["appearance"]!["text"]!["labelSet"] = "amiga";
        json["keys"]!["filter"] = "letters";
        json["placement"]!["offsetX"] = 99999;
        json["mouseCursor"]!["iconOffsetY"] = "far";
        json["extra"] = 1;
        var read = SettingsSerializer.Read(json.ToJsonString());

        Assert.False(read.FileUnusable);
        Assert.Equal(1234, read.Settings.Keys.LingerMs);
        Assert.Equal(0.5, read.Settings.Appearance.Radius);
        Assert.Equal(AppearanceSettings.Default.Primary, read.Settings.Appearance.Primary);
        Assert.Equal(LabelSet.Windows, read.Settings.Appearance.LabelSet);
        Assert.Equal(KeyFilter.All, read.Settings.Keys.Filter);
        Assert.Equal(0, read.Settings.Placement.OffsetX);
        Assert.Equal(MouseCursorSettings.Default.OffsetY, read.Settings.MouseCursor.OffsetY);
        Assert.Equal(7, read.Problems.Count);
        Localization.Loc.Set(Localization.UiLanguage.English); // сообщения переводятся при показе; числа — по культуре языка
        Assert.Contains(read.Problems, p => p.Key == "Settings.Problem.Range" && p.ToString().Contains("appearance.border.radius") && p.ToString().Contains("1.5"));
        Assert.Contains(read.Problems, p => p.ToString().Contains("extra"));
    }

    [Fact]
    public void AllNineAnchors_RoundTrip()
    {
        foreach (var a in Enum.GetValues<ScreenAnchor>())
        {
            var s = LabSettings.Default with { Placement = PlacementSettings.Default with { Anchor = a }, LayoutIndicator = LayoutIndicatorSettings.Default with { Anchor = a } };
            var read = SettingsSerializer.Read(SettingsSerializer.Write(s)).Settings;
            Assert.Equal(a, read.Placement.Anchor);
            Assert.Equal(a, read.LayoutIndicator.Anchor);
        }
    }

    [Fact]
    public void Store_SavesAtomically_AndBacksUpUnreadableFileBeforeFirstSave()
    {
        string dir = NewDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ broken");
            var store = SettingsStore.Load(path);
            Assert.Equal(Styles.BrandStyles.Starter, store.Current);
            Assert.NotEmpty(store.LoadProblems);
            Assert.Equal("{ broken", File.ReadAllText(path)); // загрузка ничего не перезаписывает

            int changes = 0;
            store.Changed += _ => changes++;
            Assert.Equal(1, store.SubscriberCount);
            store.Update(s => s with { Keys = s.Keys with { LingerMs = 777 } });
            store.Update(s => s);                   // без изменений — без события
            Assert.Equal(1, changes);
            store.Flush();

            Assert.Equal("{ broken", File.ReadAllText(Path.Combine(dir, "settings.unreadable.bak.json")));
            Assert.False(File.Exists(path + ".tmp"));
            var reloaded = SettingsStore.Load(path);
            Assert.Empty(reloaded.LoadProblems);
            Assert.Equal(777, reloaded.Current.Keys.LingerMs);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Store_MigratesFormatOne_BacksUpOriginal_WritesCurrentFormat()
    {
        string dir = NewDir();
        try
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, V1File);
            var store = SettingsStore.Load(path);
            Assert.Equal(1, store.MigratedFrom);
            Assert.Equal(V1File, File.ReadAllText(path));      // до изменения файл не трогается
            store.Update(s => s with { Keys = s.Keys with { Filter = KeyFilter.CombosOnly } });
            store.Flush();
            Assert.Equal(V1File, File.ReadAllText(Path.Combine(dir, "settings.v1.bak.json")));
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.Equal(SettingsSerializer.FormatVersion, (int)json["formatVersion"]!);
            var reloaded = SettingsStore.Load(path);
            Assert.Null(reloaded.MigratedFrom);
            Assert.Equal(new PlacementSettings(ScreenAnchor.TopRight, 60, 30, 0, 0), reloaded.Current.Placement);
            Assert.Equal(KeyFilter.CombosOnly, reloaded.Current.Keys.Filter);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Store_MissingFile_IsDefaultsWithoutProblems()
    {
        var store = SettingsStore.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        Assert.Equal(Backpack.Visual.Styles.BrandStyles.Starter, store.Current); // Phase 4I: чистая установка — фирменные стили
        Assert.Empty(store.LoadProblems);
    }

    [Fact]
    public void SettingsFile_HasNoPositionHistoryOrKeys()
    {
        var text = SettingsSerializer.Write(LabSettings.Default with { MouseCursor = MouseCursorSettings.Default with { Enabled = true } });
        foreach (var forbidden in new[] { "cursorX", "lastPosition", "history", "keysPressed", "hwnd", "title" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string NewDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Backpack.Visual.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
