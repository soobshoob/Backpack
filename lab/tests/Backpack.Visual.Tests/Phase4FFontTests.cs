using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Backpack.Visual.Profiles;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>Phase 4F, п. 9: только «Стандартный» и встроенный Spectral; покрытие символов и замена; миграция прежних шрифтов.</summary>
public class Phase4FFontTests
{
    /// <summary>Украинский, английский, русский алфавиты (обе регистра), цифры и знаки.</summary>
    private const string Text =
        "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюяʼ’" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "ЁЪЫЭёъыэ0123456789.,:;!?\"'«»—–-+=()[]{}<>/|@#$%^&*_~`№";

    /// <summary>Символы раскладок и клавиш, которых может не быть в текстовом шрифте.</summary>
    private const string Symbols = "⌘⌥⇧⌃⏎↵⌫⇥←↑→↓⎋§±";

    private static GlyphTypeface Glyphs(string family, FontWeight weight, FontStyle style)
    {
        VisualFonts.EnsurePackScheme();
        var face = new Typeface(new FontFamily(VisualFonts.FontsBaseUri, "./#" + family), style, weight, FontStretches.Normal);
        Assert.True(face.TryGetGlyphTypeface(out var g), family + " " + weight);
        Assert.Contains(family, g.Win32FamilyNames.Values.First() + " " + string.Join(" ", g.FamilyNames.Values));
        return g;
    }

    [Fact]
    public void Spectral_is_embedded_in_the_weights_the_interface_uses_and_covers_uk_en_ru()
    {
        Sta.Run(() =>
        {
            foreach (var (w, st) in new[] { (FontWeights.Normal, FontStyles.Normal), (FontWeights.SemiBold, FontStyles.Normal), (FontWeights.Bold, FontStyles.Normal), (FontWeights.Normal, FontStyles.Italic) })
            {
                var g = Glyphs("Spectral", w, st);
                Assert.Equal(w, g.Weight);
                var missing = Text.Where(c => !g.CharacterToGlyphMap.ContainsKey(c)).ToArray();
                Assert.True(missing.Length == 0, $"Spectral {w} {st}: нет {new string(missing)}");
            }
        });
    }

    [Fact]
    public void Characters_missing_from_Spectral_come_from_the_fallback_chain_not_tofu()
    {
        Sta.Run(() =>
        {
            var spectral = Glyphs("Spectral", FontWeights.Normal, FontStyles.Normal);
            var inter = Glyphs("Inter", FontWeights.Normal, FontStyles.Normal);
            var segoe = new Typeface(new FontFamily("Segoe UI Symbol"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            segoe.TryGetGlyphTypeface(out var sym);
            foreach (char c in Symbols)
            {
                bool covered = spectral.CharacterToGlyphMap.ContainsKey(c) || inter.CharacterToGlyphMap.ContainsKey(c)
                    || (sym?.CharacterToGlyphMap.ContainsKey(c) ?? false) || new Typeface("Segoe UI").TryGetGlyphTypeface(out var ui) && ui.CharacterToGlyphMap.ContainsKey(c);
                Assert.True(covered, $"U+{(int)c:X4} нет ни в одном семействе цепочки");
            }

            // Цепочки интерфейса и кейкапов: Spectral первым, затем Inter и системные семейства.
            Assert.Equal("./#Spectral, ./#Inter, Segoe UI Variable Text, Segoe UI, Segoe UI Symbol", UiTheme.UiFontFamily(FontSettings.Spectral).Source);
            Assert.Equal("./#Inter, Segoe UI Variable Text, Segoe UI, Segoe UI Symbol", UiTheme.UiFontFamily(null).Source);
        });
    }

    [Fact]
    public void Only_standard_and_spectral_are_offered_and_interface_and_keycap_fonts_are_independent()
    {
        Assert.Equal([null, FontSettings.Spectral], FontSettings.Choices);
        var s = LabSettings.Default with { Fonts = new FontSettings(null, FontSettings.Spectral) };
        var r = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(r.Problems);
        Assert.Null(r.Settings.FontsOrDefault.Ui);
        Assert.Equal(FontSettings.Spectral, r.Settings.FontsOrDefault.Keycap);
        var json = SettingsSerializer.ToJson(s);
        Assert.Equal(SettingsSerializer.FormatVersion, (int)json["formatVersion"]!);
        Assert.Equal("Spectral", (string?)json["fonts"]!["keycap"]);
        Assert.Null(json["fonts"]!["ui"]);
    }

    [Fact]
    public void Earlier_font_choices_migrate_safely_to_standard()
    {
        var json = SettingsSerializer.ToJson(LabSettings.Default);
        json["formatVersion"] = 6;
        json["fonts"] = new JsonObject { ["ui"] = "Segoe UI", ["keycap"] = "spectral" };
        var r = SettingsSerializer.Read(json.ToJsonString());
        Assert.Empty(r.Problems);
        Assert.Equal(6, r.MigratedFrom);
        Assert.Null(r.Settings.FontsOrDefault.Ui);                              // установленный шрифт → «Стандартный»
        Assert.Equal(FontSettings.Spectral, r.Settings.FontsOrDefault.Keycap);

        // Профиль прежней версии с другим шрифтом импортируется без ошибок.
        var profile = new JsonObject
        {
            ["format"] = ProfileSerializer.Format, ["formatVersion"] = 2, ["id"] = "p-old-font", ["name"] = "Old", ["settings"] = JsonNode.Parse(json.ToJsonString()),
        };
        profile["settings"]!["fonts"] = new JsonObject { ["ui"] = "Consolas", ["keycap"] = "Comic Sans MS" };
        var p = ProfileSerializer.Read(profile.ToJsonString());
        Assert.True(p.Ok, string.Join(", ", p.Errors));
        Assert.Equal(FontSettings.Default, p.Profile!.Settings.FontsOrDefault);

        // v7 строже: неизвестное значение — сообщение и «Стандартный».
        json["formatVersion"] = 7;
        json["fonts"] = new JsonObject { ["ui"] = "Segoe UI" };
        var strict = SettingsSerializer.Read(json.ToJsonString());
        Assert.NotEmpty(strict.Problems);
        Assert.Null(strict.Settings.FontsOrDefault.Ui);
    }
}

/// <summary>Phase 4F, п. 1: справка в узком окне сворачивает оглавление; широкое окно — оглавление слева.</summary>
public class Phase4FHelpLayoutTests
{
    [Fact]
    public void Help_contents_collapse_in_a_narrow_window()
    {
        Sta.Run(() =>
        {
            var help = new Help.HelpWindow(Help.HelpLibrary.FromResources())
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 600,
                Height = 600,
            };
            help.Show();
            try
            {
                help.UpdateLayout();
                Assert.True(help.ContentsCollapsed);
                help.Width = 1040;
                help.UpdateLayout();
                Assert.False(help.ContentsCollapsed);
            }
            finally
            {
                help.Close();
            }
        });
    }
}
