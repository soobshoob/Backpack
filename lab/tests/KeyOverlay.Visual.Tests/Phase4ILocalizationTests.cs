using System.Globalization;
using KeyOverlay.Visual.Help;
using KeyOverlay.Visual.Localization;

namespace KeyOverlay.Visual.Tests;

/// <summary>
/// Phase 4I, п. 10: все языки сайта плюс русский. Каждый язык берёт строки из своей сборки-спутника (а не из английского запасного
/// варианта), «Сделано в Украине» переведено, справка без перевода честно возвращает английскую с пометкой.
/// </summary>
public class Phase4ILocalizationTests
{
    public static IEnumerable<object[]> Languages() => Loc.Supported.Select(s => new object[] { s.Language, s.Code });

    [Theory]
    [MemberData(nameof(Languages))]
    public void Each_language_loads_its_own_strings(UiLanguage language, string code)
    {
        Sta.Run(() =>
        {
            try
            {
                Loc.Set(language);
                Assert.Equal(code, Loc.Code);
                string madeIn = Loc.T("About.MadeIn");
                string profiles = Loc.T("Settings.Nav.Profiles");
                if (code == "en")
                {
                    Assert.Equal("Made in Ukraine", madeIn);
                }
                else
                {
                    Assert.NotEqual("Made in Ukraine", madeIn);
                    Assert.NotEqual(Loc.TFor("Help.NotTranslated", CultureInfo.GetCultureInfo("en-US")), Loc.T("Help.NotTranslated"));
                }

                Assert.False(string.IsNullOrWhiteSpace(profiles));
                Assert.DoesNotContain("[", profiles); // отсутствующий ключ Loc помечает скобками
            }
            finally
            {
                Loc.Set(UiLanguage.English);
            }
        });
    }

    [Fact]
    public void Language_list_matches_the_website_and_keeps_russian()
    {
        string[] site = ["en", "uk", "de", "fr", "es", "it", "pt", "nl", "pl", "cs", "hu", "ro", "sv", "da", "nb", "fi", "lt", "el"];
        var codes = Loc.Supported.Select(s => s.Code).ToList();
        Assert.All(site, c => Assert.Contains(c, codes));
        Assert.Contains("ru", codes);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(codes.Order(), HelpLibrary.Languages.Order());
    }

    [Fact]
    public void Help_without_a_translation_is_english_and_says_so()
    {
        int englishPages = HelpLibrary.FromResources("en").Pages.Count;
        foreach (string code in HelpLibrary.Languages)
        {
            var lib = HelpLibrary.FromResources(code);
            bool written = HelpLibrary.Written.Contains(code);
            Assert.Equal(written ? code : "en", lib.Language);
            Assert.Equal(!written, lib.IsFallback);
            Assert.Equal(englishPages, lib.Pages.Count);
        }
    }

    [Theory]
    [InlineData("Ukraine", "en")]
    [InlineData("Україні", "uk")]
    [InlineData("Украине", "ru")]
    [InlineData("Ukraine", "de")]
    [InlineData("Ουκρανία", "el")]
    [InlineData("Ukrajině", "cs")]
    public void Made_in_ukraine_is_translated(string fragment, string code)
    {
        var culture = Loc.Resolve(Loc.Supported.First(s => s.Code == code).Language);
        Assert.Contains(fragment, Loc.TFor("About.MadeIn", culture));
    }
}
