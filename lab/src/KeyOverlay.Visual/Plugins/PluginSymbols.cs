using System.Text.RegularExpressions;

namespace KeyOverlay.Visual.Plugins;

/// <summary>
/// Значки плагинов (Phase 4I). Автор задаёт значок по умолчанию в manifest (<c>"symbol": "pen-tool"</c>), пользователь может
/// выбрать другой из того же каталога и вернуть значок автора. Значок — только имя из встроенного набора Lucide: хосту не
/// нужно запускать код плагина, загружать файлы, разметку XAML или удалённые ресурсы. Звёздочка в каталог не входит — она
/// означает только «в избранном».
/// </summary>
public static partial class PluginSymbols
{
    /// <summary>Значок плагина без своего значка.</summary>
    public const string Fallback = "puzzle";

    /// <summary>Каталог выбора: имя Lucide и слова для поиска (английские, плюс синонимы).</summary>
    public static IReadOnlyList<(string Name, string Tags)> Catalog { get; } =
    [
        ("pen-tool", "draw drawing vector pen"), ("pencil", "draw edit write"), ("pencil-line", "draw annotate edit"), ("brush", "paint draw art"),
        ("paintbrush", "paint color art"), ("highlighter", "mark highlight annotate"), ("pen-line", "write sign draw"), ("eraser", "erase clean"),
        ("shapes", "shape figure draw"), ("spline", "curve line arc"), ("type", "text font write"), ("sticky-note", "note memo"),
        ("notebook-pen", "notes write journal"), ("languages", "language layout keyboard translate"), ("keyboard", "keys typing layout"),
        ("globe", "world language web"), ("scan-search", "lens magnify search zoom"), ("search", "find magnify lens"), ("zoom-in", "magnify zoom enlarge cursor"),
        ("telescope", "look far zoom"), ("aperture", "camera lens focus"), ("focus", "focus attention spotlight"), ("locate-fixed", "target focus locate"),
        ("crosshair", "aim target pointer"), ("scan", "scan region area"), ("target", "goal aim focus"), ("eye", "view see watch"),
        ("lamp", "light spotlight focus"), ("lightbulb", "idea light tip"), ("timer", "time countdown stopwatch"), ("hourglass", "time wait timer"),
        ("alarm-clock", "alarm time timer"), ("clock", "time hours"), ("calendar", "date schedule"), ("presentation", "slides talk present"),
        ("screen-share", "screen share present"), ("monitor", "screen display"), ("app-window", "window app"), ("mouse", "mouse click"),
        ("mouse-pointer", "cursor pointer"), ("mouse-pointer-click", "click cursor"), ("hand", "hand pointer grab"), ("magnet", "snap attract"),
        ("camera", "photo screenshot capture"), ("image", "picture photo"), ("film", "video movie"), ("clapperboard", "video record film"),
        ("video", "camera record"), ("mic", "microphone voice audio"), ("headphones", "audio sound"), ("volume-2", "sound volume audio"),
        ("radio", "broadcast stream"), ("tv", "television stream"), ("megaphone", "announce broadcast"), ("message-square", "chat comment"),
        ("bell", "notify alert"), ("send", "send share"), ("link", "link url chain"), ("paperclip", "attach"), ("clipboard", "copy paste clipboard"),
        ("file-text", "document text file"), ("book-open", "read book docs"), ("bookmark", "save mark"), ("tag", "label tag"), ("flag", "mark flag"),
        ("list-ordered", "steps numbers list"), ("layout-grid", "grid layout tiles"), ("layers", "stack layers"), ("sliders-horizontal", "settings adjust"),
        ("gauge", "speed meter performance"), ("chart-column", "chart stats graph"), ("calculator", "math calculate"), ("ruler", "measure size"),
        ("scissors", "cut trim"), ("wrench", "tool fix settings"), ("code", "code developer"), ("terminal", "console command shell"),
        ("git-branch", "git branch version"), ("bug", "debug bug test"), ("cpu", "processor hardware"), ("server", "server host"),
        ("database", "data storage"), ("cloud", "cloud sync"), ("wifi", "network wireless"), ("bot", "robot automation ai"), ("brain", "ai think mind"),
        ("sparkles", "magic ai effects"), ("wand-sparkles", "magic wand effects"), ("wand", "magic wand"), ("zap", "fast power flash"),
        ("flame", "fire hot"), ("rocket", "launch fast"), ("trophy", "award win"), ("gift", "present gift"), ("heart", "love like"),
        ("thumbs-up", "like approve"), ("coffee", "break coffee"), ("gamepad-2", "game controller play"), ("dice-5", "random game"),
        ("graduation-cap", "learn teach education"), ("briefcase", "work business"), ("shopping-cart", "shop cart"), ("map", "map navigation"),
        ("compass", "direction navigation"), ("leaf", "nature eco"), ("thermometer", "temperature"), ("shield", "security protect"),
        ("lock", "lock privacy secure"), ("user", "person profile"), ("users", "people team"), ("phone", "phone call"), ("printer", "print"),
        ("box", "box package"), ("package", "package box plugin"), ("puzzle", "plugin extension puzzle"),
    ];

    private static readonly HashSet<string> Names = new(Catalog.Select(c => c.Name), StringComparer.Ordinal);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+){0,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>Имя значка имеет допустимый вид (строчные латинские буквы, цифры и дефисы, до 40 знаков).</summary>
    public static bool IsWellFormed(string? name) => name is { Length: > 0 and <= 40 } && NamePattern().IsMatch(name);

    /// <summary>Значок можно выбрать (есть в каталоге этой версии Backpack и нарисован).</summary>
    public static bool IsChoosable(string? name) => name is not null && Names.Contains(name) && Icons.Lucide.Has(name);

    /// <summary>Значок для показа: выбор пользователя, иначе значок автора, иначе <see cref="Fallback"/>.</summary>
    public static string Resolve(string? manifestSymbol, string? userSymbol) =>
        IsChoosable(userSymbol) ? userSymbol! : IsChoosable(manifestSymbol) ? manifestSymbol! : Fallback;

    /// <summary>Поиск по имени и словам (без учёта регистра); пустой запрос — весь каталог.</summary>
    public static IEnumerable<string> Search(string? query)
    {
        var words = (query ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Catalog.Where(c => words.All(w => c.Name.Contains(w, StringComparison.Ordinal) || c.Tags.Contains(w, StringComparison.Ordinal))).Select(c => c.Name);
    }
}
