using System.IO;
using System.Text.RegularExpressions;
using Backpack.Visual.Help;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>Справка: единый источник docs/help/&lt;язык&gt;, одинаковый набор разделов на трёх языках, ссылки, картинки, примеры, F1.</summary>
public class HelpTests
{
    private static readonly Dictionary<string, HelpLibrary> Libraries = HelpLibrary.Written.ToDictionary(l => l, HelpLibrary.FromResources);

    public static TheoryData<string> Langs() => new(HelpLibrary.Written);

    private static IEnumerable<string> Texts(MdBlock b) => b switch
    {
        MdParagraph p => [p.Text],
        MdList l => l.Items,
        MdTable t => t.Header.Concat(t.Rows.SelectMany(r => r)),
        MdHeading h => [h.Text],
        _ => [],
    };

    private static IEnumerable<(HelpPage Page, MdLink Link)> Links(HelpLibrary lib) =>
        lib.Pages.SelectMany(p => p.Document.Blocks.SelectMany(Texts).SelectMany(t => Markdown.Inlines(t).OfType<MdLink>()).Select(l => (p, l)));

    [Theory]
    [MemberData(nameof(Langs))]
    public void AllHelpFiles_AreEmbedded_FromDocsHelp(string lang)
    {
        var lib = Libraries[lang];
        var onDisk = Directory.GetFiles(Repo.PathOf("docs", "help", lang), "*.md").Select(f => Path.GetFileName(f)!).Order().ToList();
        Assert.Equal(onDisk, lib.Pages.Select(p => p.File).Order().ToList());
        foreach (var page in lib.Pages)
        {
            Assert.Equal(File.ReadAllText(Repo.PathOf("docs", "help", lang, page.File)).ReplaceLineEndings(), page.Source.ReplaceLineEndings());
        }
    }

    [Fact]
    public void AllLanguages_HaveTheSamePages_AndTheSameStructure()
    {
        var files = Libraries["en"].Pages.Select(p => p.File).Order().ToList();
        Assert.Equal(18, files.Count);
        Assert.Empty(Directory.GetFiles(Repo.PathOf("docs", "help"), "*.md")); // старые страницы без языка не остались
        foreach (var lang in HelpLibrary.Written)
        {
            var lib = Libraries[lang];
            Assert.Equal(files, lib.Pages.Select(p => p.File).Order().ToList());
            foreach (var file in files)
            {
                var en = Libraries["en"].Find(file)!.Document.Blocks;
                var other = lib.Find(file)!.Document.Blocks;
                // Переводы не теряют разделы, картинки и примеры.
                Assert.True(en.OfType<MdHeading>().Count() == other.OfType<MdHeading>().Count(), $"{lang}/{file}: заголовки");
                Assert.Equal(en.OfType<MdImage>().Select(i => Path.GetFileName(i.Target)), other.OfType<MdImage>().Select(i => Path.GetFileName(i.Target)));
                Assert.Equal(Links(Libraries["en"]).Where(x => x.Page.File == file).Count(x => x.Link.Target.StartsWith("demo:", StringComparison.Ordinal)),
                    Links(lib).Where(x => x.Page.File == file).Count(x => x.Link.Target.StartsWith("demo:", StringComparison.Ordinal)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Langs))]
    public void AllPages_UseOnlySupportedMarkdown(string lang)
    {
        foreach (var page in Libraries[lang].Pages)
        {
            Assert.True(page.Document.Unsupported.Count == 0, $"{lang}/{page.File}: " + string.Join("; ", page.Document.Unsupported));
        }
    }

    [Theory]
    [MemberData(nameof(Langs))]
    public void AllInternalLinks_ResolveToFilesAnchorsAndDemos(string lang)
    {
        var lib = Libraries[lang];
        int links = 0, demos = 0;
        foreach (var (page, link) in Links(lib))
        {
            Assert.False(link.Target.StartsWith("http", StringComparison.Ordinal), $"{lang}/{page.File}: внешняя ссылка {link.Target}");
            if (link.Target.StartsWith("demo:", StringComparison.Ordinal))
            {
                demos++;
                Assert.Contains(link.Target["demo:".Length..], HelpDemoWindow.Demos);
                continue;
            }

            links++;
            var (file, anchor) = HelpLibrary.SplitTopic(link.Target, page.File);
            var target = lib.Find(file);
            Assert.True(target is not null, $"{lang}/{page.File}: нет файла {link.Target}");
            if (anchor is not null)
            {
                Assert.True(target!.Document.Blocks.OfType<MdHeading>().Any(h => h.Slug == anchor), $"{lang}/{page.File}: нет якоря {link.Target}");
            }
        }

        Assert.True(links >= 15, $"{lang}: ссылок {links}");
        Assert.Equal(HelpDemoWindow.Demos.Length, Links(lib).Where(x => x.Link.Target.StartsWith("demo:", StringComparison.Ordinal)).Select(x => x.Link.Target).Distinct().Count());
        Assert.True(demos >= HelpDemoWindow.Demos.Length);
    }

    [Theory]
    [MemberData(nameof(Langs))]
    public void AllImages_AreEmbedded_LoadAndHaveCaptions(string lang)
    {
        var images = Libraries[lang].Pages.SelectMany(p => p.Document.Blocks.OfType<MdImage>().Select(i => (p.File, i))).ToList();
        Assert.True(images.Count >= 15);
        foreach (var (file, img) in images)
        {
            Assert.False(string.IsNullOrWhiteSpace(img.Alt), $"{lang}/{file}: нет подписи у {img.Target}");
            Assert.True(img.Target.StartsWith("../images/common/", StringComparison.Ordinal) || img.Target.StartsWith($"../images/{lang}/", StringComparison.Ordinal),
                $"{lang}/{file}: картинка другого языка {img.Target}");
            Assert.True(File.Exists(Repo.PathOf("docs", "help", "ru", img.Target)), $"{lang}/{file}: нет файла {img.Target}");
        }

        Sta.Run(() =>
        {
            foreach (var (file, img) in images)
            {
                var bmp = HelpLibrary.LoadImage(img.Target);
                Assert.True(bmp is { PixelWidth: > 50, PixelHeight: > 30 }, $"{lang}/{file}: не загружается {img.Target}");
            }
        });
    }

    [Fact]
    public void EveryImageOnDisk_IsUsed_AndEmbedded()
    {
        var used = Libraries.Values.SelectMany(l => l.Pages).SelectMany(p => p.Document.Blocks.OfType<MdImage>())
            .Select(i => i.Target.Replace("../", "")).ToHashSet();
        var onDisk = Directory.GetFiles(Repo.PathOf("docs", "help", "images"), "*.png", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Repo.PathOf("docs", "help"), f).Replace('\\', '/')).ToList();
        Assert.NotEmpty(onDisk);
        foreach (var f in onDisk)
        {
            // Тёмный вариант (*.dark.png) показывается вместо светлого в тёмной теме: сам он в тексте не упоминается.
            Assert.Contains(f.EndsWith(".dark.png") ? f.Replace(".dark.png", ".png") : f, used);
        }

        Assert.Equal(onDisk.Order(), HelpLibrary.ImageNames().Order());
    }

    /// <summary>Подписи к картинкам — на языке страницы и помечены как пример.</summary>
    [Fact]
    public void ImageCaptions_AreMarkedAsExamples_InPageLanguage()
    {
        var prefix = new Dictionary<string, string> { ["uk"] = "Приклад", ["en"] = "Example", ["ru"] = "Пример" };
        foreach (var lang in HelpLibrary.Written)
        {
            foreach (var img in Libraries[lang].Pages.SelectMany(p => p.Document.Blocks.OfType<MdImage>()))
            {
                Assert.StartsWith(prefix[lang], img.Alt);
            }
        }
    }

    /// <summary>Разделы, на которые ссылается программа (F1 из окон и вкладок), есть на каждом языке.</summary>
    [Fact]
    public void ProgramTopics_Exist_InEveryLanguage()
    {
        var topics = Enum.GetValues<SettingsSection>().Select(SettingsWindow.HelpFile)
            .Append("09-diagnostics-and-privacy.md").Append(HelpLibrary.IndexFile).Distinct().ToList();
        foreach (var lang in HelpLibrary.Written)
        {
            foreach (var topic in topics)
            {
                Assert.True(Libraries[lang].Find(topic) is not null, $"{lang}: нет {topic}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Langs))]
    public void Toc_ComesFromReadme_InOrder(string lang)
    {
        var lib = Libraries[lang];
        Assert.Equal("README.md", lib.Toc[0].File);
        Assert.Equal(18, lib.Toc.Count); // Phase 4E: + оформление, навигация и шрифты
        Assert.Equal("01-quick-start.md", lib.Toc[1].File);
        Assert.Equal("10-planned.md", lib.Toc[^1].File);
    }

    /// <summary>Планы — только на отдельной странице: в остальных разделах нет «этапов» и «запланировано».</summary>
    [Fact]
    public void Plans_AreOnlyOnThePlannedPage()
    {
        var words = new Regex("запланирован|заплановано|заплановані|planned|этап \\d|етап \\d|stage \\d", RegexOptions.IgnoreCase);
        foreach (var lang in HelpLibrary.Written)
        {
            foreach (var page in Libraries[lang].Pages.Where(p => p.File is not "10-planned.md" and not "README.md"))
            {
                foreach (var text in page.Document.Blocks.SelectMany(Texts))
                {
                    Assert.False(words.IsMatch(text) && !text.Contains("10-planned.md", StringComparison.Ordinal) && !text.Contains("(запланирована)") && !text.Contains("(запланована)") && !text.Contains("(planned)"),
                        $"{lang}/{page.File}: {text}");
                }
            }
        }
    }

    [Fact]
    public void Search_IgnoresCase_AndYo_InEachLanguage()
    {
        var ru = Libraries["ru"];
        Assert.NotEmpty(ru.Search("РАСКЛАДК"));
        Assert.Equal(ru.Search("ещё").Count, ru.Search("еще").Count);
        Assert.NotEmpty(ru.Search("ещё"));
        Assert.NotEmpty(Libraries["uk"].Search("ПРОФІЛ"));
        Assert.NotEmpty(Libraries["uk"].Search("трей"));
        Assert.NotEmpty(Libraries["en"].Search("Tray"));
        Assert.NotEmpty(Libraries["en"].Search("example: the tray icon menu")); // подписи картинок тоже ищутся
        Assert.Empty(ru.Search("x"));
        Assert.Empty(Libraries["en"].Search("nonexistentword"));
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglish()
    {
        Assert.Equal("en", HelpLibrary.FromResources("de").Language);
        Assert.Equal(Libraries["en"].Toc.Select(t => t.Title), HelpLibrary.FromResources("de").Toc.Select(t => t.Title));
    }

    [Fact]
    public void Markdown_Slugs_MatchGitHubStyle()
    {
        Assert.Equal("как-пользоваться-справкой", Markdown.Slug("Как пользоваться справкой"));
        Assert.Equal("мышь-этап-6", Markdown.Slug("Мышь (этап 6)"));
        Assert.Equal("якщо-індикатор-не-зявляється", Markdown.Slug("Якщо індикатор не з’являється"));
        Assert.Equal("if-the-indicator-does-not-appear", Markdown.Slug("If the indicator does not appear"));
    }

    [Fact]
    public void Markdown_RejectsUnsupportedConstructs_AndParsesImages()
    {
        var doc = Markdown.Parse("# A\n\n> цитата\n\n<b>html</b>\n\n#### глубокий\n\n![подпись](../images/x.svg)\n\n![Пример](../images/common/a.png)\n");
        Assert.Equal(4, doc.Unsupported.Count); // svg и прочее — не картинка справки
        Assert.Single(doc.Blocks.OfType<MdImage>());
    }

    [Fact]
    public void HelpWindow_NavigatesWithHistory_WithoutShowing()
    {
        Sta.Run(() =>
        {
            Loc.Set(UiLanguage.Russian);
            var w = new HelpWindow(Libraries["ru"]);
            w.Navigate("README.md");
            w.Navigate("05-keyboard-layout.md#ограничения");
            w.Navigate("#настройки");                // якорь на текущей странице
            Assert.Equal("05-keyboard-layout.md#настройки", w.CurrentTopic);
            Assert.Equal(2, w.BackDepth);
            w.GoBack();
            Assert.Equal("05-keyboard-layout.md#ограничения", w.CurrentTopic);
            w.GoBack();
            Assert.Equal("README.md", w.CurrentTopic);
            w.GoForward();
            Assert.Equal("05-keyboard-layout.md#ограничения", w.CurrentTopic);
            Assert.NotNull(w.Document);
            w.Navigate("нет-такого.md");             // неизвестный раздел → оглавление, без исключения
            Assert.Equal("README.md", w.CurrentFile);
            w.Close();
        });
    }

    /// <summary>Смена языка интерфейса: открытая справка показывает тот же раздел на новом языке, окно одно.</summary>
    [Fact]
    public void HelpService_FollowsInterfaceLanguage_KeepingTopic()
    {
        Sta.Run(() =>
        {
            HelpService.ShowOnScreen = false;
            try
            {
                Loc.Set(UiLanguage.Ukrainian);
                var w = HelpService.Show("07-profiles.md");
                Assert.Equal("uk", w.Library.Language);
                string ukTitle = w.Title;
                Loc.Set(UiLanguage.English);
                Assert.Same(w, HelpService.OpenWindow);
                Assert.Equal("en", w.Library.Language);
                Assert.Equal("07-profiles.md", w.CurrentFile);
                Assert.NotEqual(ukTitle, w.Title);
                Assert.Contains("Profiles", w.Title);
                HelpService.Shutdown();
                Assert.Null(HelpService.OpenWindow);
            }
            finally
            {
                HelpService.ShowOnScreen = true;
            }
        });
    }

    [Fact]
    public void ImagesAndDemos_OpenInSingleWindows_AndStopOnClose()
    {
        Sta.Run(() =>
        {
            HelpService.ShowOnScreen = false;
            try
            {
                Loc.Set(UiLanguage.English);
                var a = HelpImageWindow.Show(null, "../images/common/trail-none.png", "Example");
                var b = HelpImageWindow.Show(null, "../images/en/tray-menu.png", "Example 2");
                Assert.NotNull(a);
                Assert.Same(a, b);
                Assert.Null(HelpImageWindow.Show(null, "../images/none.png", ""));
                a!.Close();

                HelpDemoWindow? first = null;
                foreach (var demo in HelpDemoWindow.Demos)
                {
                    var d = HelpDemoWindow.Show(null, demo);
                    first ??= d;
                    Assert.Same(first, d);
                    Assert.Equal(demo, d!.Demo);
                    Assert.True(d.IsRunning);
                }

                Assert.Null(HelpDemoWindow.Show(null, "run:calc")); // только известные примеры
                int baseline = Rendering.OverlayPresenter.ActiveRenderingSubscriptions;
                HelpDemoWindow.Show(null, "trail-fade");
                first!.Close();
                Assert.False(first.IsRunning);
                Assert.Null(HelpDemoWindow.Current);
                Assert.True(Rendering.OverlayPresenter.ActiveRenderingSubscriptions <= baseline);
            }
            finally
            {
                HelpService.ShowOnScreen = true;
            }
        });
    }

    [Fact]
    public void Highlight_MarksMatches_AndThemeColorsApply()
    {
        Sta.Run(() =>
        {
            UiTheme.Set(UiThemeMode.Dark);
            var page = Libraries["en"].Find("05-keyboard-layout.md")!;
            var theme = new HelpDocTheme(UiTheme.Brush("Ko.TextBrush"), UiTheme.Brush("Ko.WindowBrush"), UiTheme.Brush("Ko.CodeBrush"),
                UiTheme.Brush("Ko.BorderBrush"), UiTheme.Brush("Ko.LinkBrush"), UiTheme.Brush("Ko.SecondaryTextBrush"));
            var (doc, anchors) = FlowDocumentBuilder.Build(page.Document, _ => { }, theme);
            Assert.Contains("limitations", anchors.Keys);
            Assert.True(FlowDocumentBuilder.Highlight(doc, "IME") > 0);
            Assert.Same(theme.Background, doc.Background);
            Assert.True(doc.Blocks.OfType<System.Windows.Documents.BlockUIContainer>().Any()); // картинки
        });
    }
}
