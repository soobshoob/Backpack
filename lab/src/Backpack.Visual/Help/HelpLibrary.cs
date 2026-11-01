using System.IO;
using System.Windows.Media.Imaging;

namespace Backpack.Visual.Help;

public sealed record HelpPage(string File, string Title, MdDocument Document, string Source);

public sealed record HelpTocEntry(string Title, string File);

public sealed record HelpSearchHit(string File, string PageTitle, string? Anchor, string SectionTitle, string Snippet);

/// <summary>
/// Разделы справки — встроенные ресурсы <c>help/&lt;язык&gt;/*.md</c> из docs/help репозитория (один источник для программы
/// и GitHub). Изображения — <c>help/images/…png</c>. Оглавление — нумерованный список ссылок в README.md каждого языка.
/// Имена файлов одинаковы во всех языках, поэтому раздел для F1 задаётся именем файла.
/// </summary>
public sealed class HelpLibrary
{
    public const string IndexFile = "README.md";
    public static readonly string[] Languages = ["en", "uk", "ru", "de", "fr", "es", "it", "pt", "nl", "pl", "cs", "hu", "ro", "sv", "da", "nb", "fi", "lt", "el"];

    /// <summary>Языки, на которых справка написана (docs/help/&lt;язык&gt;); остальные из <see cref="Languages"/> показывают английскую.</summary>
    public static readonly string[] Written = ["en", "uk", "ru"];

    private readonly Dictionary<string, HelpPage> _pages;

    public HelpLibrary(IEnumerable<(string File, string Text)> sources, string language = "ru")
    {
        Language = language;
        _pages = sources.ToDictionary(
            s => s.File,
            s =>
            {
                var doc = Markdown.Parse(s.Text);
                string title = doc.Blocks.OfType<MdHeading>().FirstOrDefault()?.Text ?? s.File;
                return new HelpPage(s.File, title, doc, s.Text);
            },
            StringComparer.OrdinalIgnoreCase);
        Toc = BuildToc();
    }

    public string Language { get; }

    /// <summary>Справка на языке <paramref name="language"/> («uk», «en», «ru»); неизвестный язык — английская.</summary>
    public static HelpLibrary FromResources(string language = "en")
    {
        if (!Languages.Contains(language))
        {
            language = "en";
        }

        var asm = typeof(HelpLibrary).Assembly;
        string prefix = $"help/{language}/";
        var sources = new List<(string, string)>();
        foreach (var raw in asm.GetManifestResourceNames())
        {
            string name = raw.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".md", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = asm.GetManifestResourceStream(raw)!;
            using var reader = new StreamReader(stream);
            sources.Add((name[prefix.Length..], reader.ReadToEnd()));
        }

        if (sources.Count == 0 && language != "en")
        {
            // Phase 4I: справки на этом языке нет — английская, но с видимым уведомлением (HelpWindow), а не молча.
            var english = FromResources("en");
            english.RequestedLanguage = language;
            return english;
        }

        return new HelpLibrary(sources, language);
    }

    /// <summary>Запрошенный язык справки; отличается от <see cref="Language"/>, если справка на нём ещё не переведена.</summary>
    public string? RequestedLanguage { get; private set; }

    /// <summary>Показана английская справка вместо непереведённой.</summary>
    public bool IsFallback => RequestedLanguage is not null && RequestedLanguage != Language;

    /// <summary>Встроенные изображения справки: путь вида «images/en/settings-keys.png».</summary>
    public static IReadOnlyCollection<string> ImageNames() => typeof(HelpLibrary).Assembly.GetManifestResourceNames()
        .Select(n => n.Replace('\\', '/'))
        .Where(n => n.StartsWith("help/images/", StringComparison.Ordinal))
        .Select(n => n["help/".Length..])
        .ToList();

    /// <summary>Загрузка встроенного изображения (относительный путь из Markdown). null — нет такого ресурса.</summary>
    public static BitmapSource? LoadImage(string target)
    {
        var asm = typeof(HelpLibrary).Assembly;
        string want = "help/" + target.TrimStart('.', '/').Replace("../", "");
        var names = asm.GetManifestResourceNames();
        // Иллюстрация в теме окна: в тёмной теме — вариант «*.dark.png», если он есть.
        string? raw = Settings.UiTheme.IsDark && want.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? names.FirstOrDefault(n => n.Replace('\\', '/') == want[..^4] + ".dark.png")
            : null;
        raw ??= names.FirstOrDefault(n => n.Replace('\\', '/') == want);
        if (raw is null)
        {
            return null;
        }

        using var stream = asm.GetManifestResourceStream(raw)!;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public IReadOnlyCollection<HelpPage> Pages => _pages.Values;

    public IReadOnlyList<HelpTocEntry> Toc { get; }

    public HelpPage? Find(string file) => _pages.GetValueOrDefault(file);

    public static (string File, string? Anchor) SplitTopic(string topic, string currentFile = IndexFile)
    {
        int hash = topic.IndexOf('#');
        string file = hash < 0 ? topic : topic[..hash];
        string? anchor = hash < 0 ? null : topic[(hash + 1)..];
        return (string.IsNullOrEmpty(file) ? currentFile : file, string.IsNullOrEmpty(anchor) ? null : anchor);
    }

    /// <summary>Без учёта регистра; «ё» и «е» не различаются; ищутся и подписи изображений.</summary>
    public IReadOnlyList<HelpSearchHit> Search(string query, int max = 50)
    {
        string q = Normalize(query.Trim());
        if (q.Length < 2)
        {
            return [];
        }

        var hits = new List<HelpSearchHit>();
        foreach (var page in Toc.Select(t => Find(t.File)).Where(p => p is not null).Concat(_pages.Values).Distinct())
        {
            string? anchor = null;
            string section = page!.Title;
            foreach (var block in page.Document.Blocks)
            {
                if (block is MdHeading h)
                {
                    (anchor, section) = (h.Level == 1 ? null : h.Slug, h.Text);
                }

                foreach (string text in Texts(block))
                {
                    string plain = Markdown.Plain(text);
                    int at = Normalize(plain).IndexOf(q, StringComparison.Ordinal);
                    if (at >= 0)
                    {
                        int from = Math.Max(0, at - 40);
                        int len = Math.Min(plain.Length - from, 120);
                        hits.Add(new HelpSearchHit(page.File, page.Title, anchor, section, (from > 0 ? "…" : "") + plain.Substring(from, len) + (from + len < plain.Length ? "…" : "")));
                        break;
                    }
                }

                if (hits.Count >= max)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static string Normalize(string s) => s.ToLowerInvariant().Replace('ё', 'е');

    private static IEnumerable<string> Texts(MdBlock b) => b switch
    {
        MdHeading h => [h.Text],
        MdParagraph p => [p.Text],
        MdList l => l.Items,
        MdTable t => t.Header.Concat(t.Rows.SelectMany(r => r)),
        MdCode c => [c.Text],
        MdImage i => [i.Alt],
        _ => [],
    };

    private List<HelpTocEntry> BuildToc()
    {
        var toc = new List<HelpTocEntry>();
        if (Find(IndexFile) is { } index)
        {
            toc.Add(new HelpTocEntry(index.Title, IndexFile));
            foreach (var list in index.Document.Blocks.OfType<MdList>().Where(l => l.Ordered).Take(1))
            {
                foreach (var item in list.Items)
                {
                    if (Markdown.Inlines(item).OfType<MdLink>().FirstOrDefault() is { } link && Find(SplitTopic(link.Target).File) is not null)
                    {
                        toc.Add(new HelpTocEntry(link.Text, SplitTopic(link.Target).File));
                    }
                }
            }
        }

        return toc;
    }
}
