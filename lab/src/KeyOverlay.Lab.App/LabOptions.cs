using KeyOverlay.Lab.Core.Input;

namespace KeyOverlay.Lab.App;

public enum CaptureMode { Raw, LowLevel, Compare }

public sealed record LabOptions(
    CaptureMode Mode,
    SourceKind Primary,
    TimeSpan? LingerOverride,
    int RingCapacity,
    TimeSpan? AutoExit,
    string? SummaryOut,
    string? SettingsPath = null,
    bool Demo = false,
    bool SelfTestUi = false,
    string? RenderHelpImages = null,
    bool Lab = false,
    bool Minimized = false,
    string? RenderBrand = null,
    string? RenderSheet = null,
    bool NoPlugins = false,
    string? PrivacyTrace = null,
    string? InputTrace = null,
    string? FnTrace = null,
    string? DisplayTrace = null,
    int SoakMinutes = 0,
    bool SelfTestAllLanguages = false)
{
    public const string Usage =
        "Backpack [--settings=путь.json] [--minimized] [--no-plugins]\n" +
        "Backpack --lab [--source=raw|ll|compare] [--primary=raw|ll] [--linger-ms=N] [--ring=1024]\n" +
        "               [--auto-exit-ms=N] [--summary-out=путь.json] [--settings=путь.json] [--demo]\n" +
        "               [--selftest-ui] [--render-help-images=папка] [--render-brand=папка] [--render-sheet=папка]\n\n" +
        "  --minimized    запуск в области уведомлений, без главного окна\n" +
        "  --no-plugins   запуск без плагинов\n" +
        "  --lab          лабораторный режим: окно диагностики при запуске; параметры ниже работают только вместе с ним\n" +
        "  --source       источник: raw (Raw Input), ll (LL-хук), compare (оба, в отдельных потоках)\n" +
        "  --primary      в compare: какой источник питает отображение (по умолчанию raw)\n" +
        "  --linger-ms    выдержка после отпускания, мс, только на этот запуск (иначе — из настроек, по умолчанию 5000)\n" +
        "  --ring         ёмкость кольца событий на источник (степень двойки)\n" +
        "  --auto-exit-ms закрыть стенд через N мс (проверка старта и завершения)\n" +
        "  --summary-out  после завершения записать агрегированный JSON (без кодов клавиш)\n" +
        "  --settings     файл настроек вместо %APPDATA%\\Backpack\\settings.json (или папки portable)\n" +
        "  --demo         при запуске показать пример на оверлее и индикатор раскладки (проверка внешнего вида)\n" +
        "  --fn-trace=файл  короткая проверка Fn (45 с или до Esc): только служебные клавиши, без букв, цифр и знаков\n" +
        "  --selftest-ui  самопроверка окон без ввода: разделы, языки, темы, справка, трей; затем выход\n" +
        "  --selftest-ui=languages  то же, но раскладка всех страниц на всех 19 языках и четырёх ширинах (долго)\n" +
        "  --render-help-images=папка  снимки окон на примерных данных для docs/help/images; захват не запускается\n" +
        "  --render-brand=папка        логотип: SVG, ICO (приложение и трей), PNG на светлом и тёмном фоне; захват не запускается\n" +
        "  --render-sheet=папка        сравнительный лист рендеров (кейкапы, мышь, логотип) настоящим рендером; захват не запускается";

    public string ModeName => Mode switch { CaptureMode.Raw => "raw", CaptureMode.LowLevel => "ll", _ => "compare" };

    public static LabOptions Parse(string[] args)
    {
        var mode = CaptureMode.Raw;
        var primary = SourceKind.RawInput;
        bool primaryGiven = false;
        TimeSpan? linger = null;
        int ring = 1024;
        TimeSpan? autoExit = null;
        string? summary = null;
        string? settings = null;
        bool demo = false;
        bool selfTestUi = false, selfTestAllLanguages = false;
        string? renderHelpImages = null;
        bool lab = args.Contains("--lab");
        bool minimized = false;
        bool noPlugins = false;
        string? renderBrand = null, renderSheet = null, privacyTrace = null, inputTrace = null, fnTrace = null, displayTrace = null;
        int soak = 0;

        foreach (var arg in args)
        {
            var (key, value) = Split(arg);
            if (!lab && key is not ("--settings" or "--minimized" or "--lab" or "--no-plugins" or "--display-trace"))
            {
                // Инструменты стенда изолированы от обычного запуска: без --lab они не принимаются.
                throw new ArgumentException($"{key}: параметр стенда, доступен только с --lab");
            }

            switch (key)
            {
                case "--lab":
                    break;
                case "--no-plugins":
                    noPlugins = true;
                    break;
                case "--minimized":
                    minimized = true;
                    break;
                case "--render-brand":
                    renderBrand = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--render-brand: пустой путь") : value;
                    break;
                case "--render-sheet":
                    renderSheet = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--render-sheet: пустой путь") : value;
                    break;
                case "--source":
                    mode = value switch
                    {
                        "raw" => CaptureMode.Raw,
                        "ll" => CaptureMode.LowLevel,
                        "compare" => CaptureMode.Compare,
                        _ => throw new ArgumentException($"Неизвестный источник: {value}"),
                    };
                    break;
                case "--primary":
                    primaryGiven = true;
                    primary = value switch
                    {
                        "raw" => SourceKind.RawInput,
                        "ll" => SourceKind.LowLevelHook,
                        _ => throw new ArgumentException($"Неизвестный основной источник: {value}"),
                    };
                    break;
                case "--linger-ms":
                    linger = TimeSpan.FromMilliseconds(PositiveInt(value, key));
                    break;
                case "--ring":
                    ring = PositiveInt(value, key);
                    if ((ring & (ring - 1)) != 0 || ring < 2)
                    {
                        throw new ArgumentException("--ring должен быть степенью двойки ≥ 2");
                    }

                    break;
                case "--auto-exit-ms":
                    autoExit = TimeSpan.FromMilliseconds(PositiveInt(value, key));
                    break;
                case "--settings":
                    settings = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--settings: пустой путь") : value;
                    break;
                case "--demo":
                    demo = true;
                    break;
                case "--render-help-images":
                    renderHelpImages = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--render-help-images: пустой путь") : value;
                    break;
                case "--selftest-ui":
                    selfTestUi = true;
                    selfTestAllLanguages = value == "languages";
                    break;
                case "--privacy-trace":
                    privacyTrace = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--privacy-trace: пустой путь") : value;
                    break;
                case "--fn-trace":
                    fnTrace = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--fn-trace: пустой путь") : value;
                    break;
                case "--display-trace":
                    displayTrace = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--display-trace: пустой путь") : value;
                    break;
                case "--soak":
                    soak = PositiveInt(value, key);
                    break;
                case "--input-trace":
                    inputTrace = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--input-trace: пустой путь") : value;
                    break;
                case "--summary-out":
                    summary = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("--summary-out: пустой путь") : value;
                    break;
                default:
                    throw new ArgumentException($"Неизвестный аргумент: {arg}");
            }
        }

        if (mode != CaptureMode.Compare)
        {
            if (primaryGiven)
            {
                throw new ArgumentException("--primary допустим только с --source=compare");
            }

            primary = mode == CaptureMode.LowLevel ? SourceKind.LowLevelHook : SourceKind.RawInput;
        }

        return new LabOptions(mode, primary, linger, ring, autoExit, summary, settings, demo, selfTestUi, renderHelpImages, lab, minimized, renderBrand, renderSheet, noPlugins, privacyTrace, inputTrace, fnTrace, displayTrace, soak, selfTestAllLanguages);
    }

    private static (string Key, string Value) Split(string arg)
    {
        int eq = arg.IndexOf('=');
        return eq < 0 ? (arg, "") : (arg[..eq], arg[(eq + 1)..]);
    }

    private static int PositiveInt(string value, string key) =>
        int.TryParse(value, out int v) && v > 0 ? v : throw new ArgumentException($"{key}: ожидается положительное число");
}
