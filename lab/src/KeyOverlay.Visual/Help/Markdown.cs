using System.Text;
using System.Text.RegularExpressions;

namespace KeyOverlay.Visual.Help;

public abstract record MdBlock;
public sealed record MdHeading(int Level, string Text, string Slug) : MdBlock;
public sealed record MdParagraph(string Text) : MdBlock;
public sealed record MdList(bool Ordered, IReadOnlyList<string> Items) : MdBlock;
public sealed record MdTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) : MdBlock;
public sealed record MdCode(string Language, string Text) : MdBlock;

/// <summary>Изображение отдельной строкой: <c>![подпись](images/…png)</c>. Подпись — для чтения с экрана и как текст под картинкой.</summary>
public sealed record MdImage(string Alt, string Target) : MdBlock;

public abstract record MdInline;
public sealed record MdText(string Text, bool Bold = false, bool Italic = false) : MdInline;
public sealed record MdCodeSpan(string Text) : MdInline;
public sealed record MdLink(string Text, string Target) : MdInline;

public sealed record MdDocument(IReadOnlyList<MdBlock> Blocks, IReadOnlyList<string> Unsupported);

/// <summary>
/// Разбор подмножества Markdown, которым написана docs/help: заголовки #–###, абзацы, списки (- и 1.), таблицы,
/// блоки кода ```, **жирный**, *курсив*, `код`, [ссылка](цель), изображение отдельной строкой ![подпись](images/…). HTML, цитаты и прочее не поддерживаются и
/// попадают в <see cref="MdDocument.Unsupported"/> (тест требует, чтобы этот список был пуст для всех разделов).
/// Никакой HTML/XAML из текста не интерпретируется.
/// </summary>
public static partial class Markdown
{
    public static MdDocument Parse(string text)
    {
        var blocks = new List<MdBlock>();
        var unsupported = new List<string>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var para = new StringBuilder();

        void FlushPara()
        {
            if (para.Length > 0)
            {
                blocks.Add(new MdParagraph(para.ToString().Trim()));
                para.Clear();
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushPara();
                string lang = trimmed[3..].Trim();
                var code = new StringBuilder();
                int indent = line.Length - trimmed.Length;
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal); i++)
                {
                    string l = lines[i];
                    code.AppendLine(l.Length >= indent && string.IsNullOrWhiteSpace(l[..indent]) ? l[indent..] : l.TrimStart());
                }

                blocks.Add(new MdCode(lang, code.ToString().TrimEnd('\n', '\r')));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushPara();
                continue;
            }

            if (HeadingRegex().Match(line) is { Success: true } h)
            {
                FlushPara();
                int level = h.Groups[1].Value.Length;
                string ht = h.Groups[2].Value.Trim();
                if (level > 3)
                {
                    unsupported.Add($"заголовок уровня {level}: {ht}");
                }

                blocks.Add(new MdHeading(level, ht, Slug(ht)));
                continue;
            }

            if (trimmed.StartsWith('|'))
            {
                FlushPara();
                var rows = new List<IReadOnlyList<string>>();
                for (; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
                {
                    rows.Add(Cells(lines[i]));
                }

                i--;
                if (rows.Count < 2 || !rows[1].All(c => SeparatorRegex().IsMatch(c)))
                {
                    unsupported.Add("таблица без строки-разделителя");
                    continue;
                }

                blocks.Add(new MdTable(rows[0], rows.Skip(2).ToList()));
                continue;
            }

            bool bullet = trimmed.StartsWith("- ", StringComparison.Ordinal);
            bool ordered = OrderedRegex().IsMatch(trimmed);
            if (bullet || ordered)
            {
                FlushPara();
                var items = new List<string>();
                for (; i < lines.Length; i++)
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith("- ", StringComparison.Ordinal) && !ordered)
                    {
                        items.Add(t[2..].Trim());
                    }
                    else if (ordered && OrderedRegex().Match(t) is { Success: true } om)
                    {
                        items.Add(t[om.Length..].Trim());
                    }
                    else if (!string.IsNullOrWhiteSpace(lines[i]) && lines[i].StartsWith("  ", StringComparison.Ordinal) && items.Count > 0)
                    {
                        items[^1] += " " + t.Trim(); // продолжение пункта
                    }
                    else
                    {
                        break;
                    }
                }

                i--;
                blocks.Add(new MdList(ordered, items));
                continue;
            }

            if (ImageRegex().Match(trimmed) is { Success: true } img)
            {
                FlushPara();
                blocks.Add(new MdImage(img.Groups[1].Value, img.Groups[2].Value));
                continue;
            }

            if (trimmed.StartsWith('>') || trimmed.StartsWith('<') || trimmed.StartsWith("![", StringComparison.Ordinal) || trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                unsupported.Add($"строка {i + 1}: {trimmed[..Math.Min(trimmed.Length, 40)]}");
            }

            para.Append(line.Trim()).Append(' ');
        }

        FlushPara();
        return new MdDocument(blocks, unsupported);
    }

    public static IReadOnlyList<MdInline> Inlines(string text)
    {
        var result = new List<MdInline>();
        int pos = 0;
        foreach (Match m in InlineRegex().Matches(text))
        {
            if (m.Index > pos)
            {
                result.Add(new MdText(text[pos..m.Index]));
            }

            if (m.Groups["code"].Success)
            {
                result.Add(new MdCodeSpan(m.Groups["code"].Value));
            }
            else if (m.Groups["link"].Success)
            {
                result.Add(new MdLink(m.Groups["link"].Value, m.Groups["target"].Value));
            }
            else if (m.Groups["bold"].Success)
            {
                result.Add(new MdText(m.Groups["bold"].Value, Bold: true));
            }
            else
            {
                result.Add(new MdText(m.Groups["italic"].Value, Italic: true));
            }

            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
        {
            result.Add(new MdText(text[pos..]));
        }

        return result;
    }

    /// <summary>Текст без разметки (поиск).</summary>
    public static string Plain(string text) => string.Concat(Inlines(text).Select(i => i switch
    {
        MdText t => t.Text,
        MdCodeSpan c => c.Text,
        MdLink l => l.Text,
        _ => "",
    }));

    /// <summary>Якорь заголовка как на GitHub: строчные, без пунктуации, пробел → дефис; кириллица сохраняется.</summary>
    public static string Slug(string heading)
    {
        var sb = new StringBuilder();
        foreach (char c in Plain(heading).Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                sb.Append(c);
            }
            else if (c == ' ')
            {
                sb.Append('-');
            }
        }

        return sb.ToString();
    }

    private static List<string> Cells(string line)
    {
        string t = line.Trim();
        if (t.StartsWith('|'))
        {
            t = t[1..];
        }

        if (t.EndsWith('|'))
        {
            t = t[..^1];
        }

        return t.Split('|').Select(c => c.Trim()).ToList();
    }

    [GeneratedRegex(@"^!\[([^\]]*)\]\(([^)\s]+\.png)\)$")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^:?-{3,}:?$")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"^\d+\.\s+")]
    private static partial Regex OrderedRegex();

    [GeneratedRegex(@"`(?<code>[^`]+)`|\[(?<link>[^\]]+)\]\((?<target>[^)\s]+)\)|\*\*(?<bold>[^*]+)\*\*|\*(?<italic>[^*\s][^*]*)\*")]
    private static partial Regex InlineRegex();
}
