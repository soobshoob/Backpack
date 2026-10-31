namespace KeyOverlay.Visual.Settings;

/// <summary>Что скрывает команда «показать/скрыть отображение» (горячая клавиша, главное окно, трей).</summary>
[Flags]
public enum DisplayParts { None = 0, Keycaps = 1, Indicator = 2, Mouse = 4, All = Keycaps | Indicator | Mouse }

/// <summary>Канал обновлений: Stable не предлагает предварительные версии.</summary>
public enum UpdateChannel { Stable, Preview }

/// <summary>Проверка обновлений: канал и необязательная периодическая проверка (по умолчанию выключена).</summary>
public sealed record UpdatePreferences(UpdateChannel Channel, bool AutoCheck)
{
    public static UpdatePreferences Default { get; } = new(UpdateChannel.Stable, false);
}

/// <summary>
/// Живая линза (Phase 2F): увеличение, размер области по умолчанию (если выделение — щелчок без протягивания), скругление,
/// рамка, тень, длительность (0 — пока не закрыта по Esc или повторной команде).
/// </summary>
public sealed record LensSettings(double Zoom, double DefaultWidth, double DefaultHeight, double Radius, ThemeColor BorderColor, double BorderWidth, bool Shadow, int DurationSec)
{
    public static LensSettings Default { get; } = new(1.6, 420, 240, 16, new ThemeColor(0xFF, 0xFF, 0xFF), 3, true, 15);

    public static readonly (double Min, double Max) ZoomRange = (1.1, 4);
    public static readonly (double Min, double Max) SizeRange = (80, 1600);
    public static readonly (double Min, double Max) RadiusRange = (0, 64);
    public static readonly (double Min, double Max) BorderRange = (0, 12);
    public static readonly (int Min, int Max) DurationRange = (0, 600);
}

/// <summary>
/// Инструменты рисования. SmartPen — карандаш, превращающий прямую, эллипс, прямоугольник и многоугольник в фигуру;
/// Marker — нумерованные отметки; RoundedRectangle — прямоугольник со скруглением <see cref="DrawSettings.CornerRadius"/>;
/// Polygon — правильный многоугольник с <see cref="DrawSettings.PolygonSides"/> вершинами (строится от центра);
/// Select — выбор и перемещение нарисованного, Delete удаляет (шаги истории). Новые значения добавлены в конец — имена в
/// app.json не меняются.
/// </summary>
public enum DrawTool { Pen, Line, Arrow, Rectangle, Ellipse, Text, SmartPen, Marker, RoundedRectangle, Polygon, Select }

/// <summary>Категории компактной панели и вложенных колец радиального меню.</summary>
public enum DrawCategory { Draw, Shapes, Annotate }

/// <summary>Каталог инструментов рисования: категория, ID команды, иконка Lucide (одни данные для панели, меню и сочетаний).</summary>
public static class DrawTools
{
    public static readonly (DrawTool Tool, DrawCategory Category, string Command, string Icon)[] All =
    [
        (DrawTool.Select, DrawCategory.Draw, "tool.drawing.select", "mouse-pointer-2"),
        (DrawTool.SmartPen, DrawCategory.Draw, "tool.drawing.smartpen", "wand-sparkles"),
        (DrawTool.Pen, DrawCategory.Draw, "tool.drawing.pen", "pencil"),
        (DrawTool.Line, DrawCategory.Shapes, "tool.drawing.line", "slash"),
        (DrawTool.Arrow, DrawCategory.Shapes, "tool.drawing.arrow", "move-up-right"),
        (DrawTool.Rectangle, DrawCategory.Shapes, "tool.drawing.rectangle", "square"),
        (DrawTool.RoundedRectangle, DrawCategory.Shapes, "tool.drawing.roundrect", "bp-roundrect"),
        (DrawTool.Ellipse, DrawCategory.Shapes, "tool.drawing.ellipse", "circle"),
        (DrawTool.Polygon, DrawCategory.Shapes, "tool.drawing.polygon", "hexagon"),
        (DrawTool.Text, DrawCategory.Annotate, "tool.drawing.text", "type"),
        (DrawTool.Marker, DrawCategory.Annotate, "tool.drawing.marker", "bp-marker"),
    ];

    /// <summary>Команды выбора инструмента для реестра.</summary>
    public static IEnumerable<(DrawTool Tool, string Command, string Icon)> Commands => All.Select(x => (x.Tool, x.Command, x.Icon));

    public static DrawCategory CategoryOf(DrawTool t) => All.First(x => x.Tool == t).Category;

    public static string IconOf(DrawTool t) => All.First(x => x.Tool == t).Icon;

    public static string CategoryIcon(DrawCategory c) => c switch { DrawCategory.Draw => "pencil", DrawCategory.Shapes => "shapes", _ => "type" };
}

public enum DrawDash { Solid, Dashed, Dotted }

/// <summary>
/// Временное рисование: инструмент, цвет (с прозрачностью), толщина, штрих, исчезновение (0 — до выхода из режима), размер
/// шрифта инструмента «Текст». Это значения по умолчанию на этом компьютере; панель и радиальное меню меняют их на время режима.
/// </summary>
public sealed record DrawSettings(DrawTool Tool, ThemeColor Color, double Thickness, DrawDash Dash, int FadeSec, double TextSize = 28,
    double CornerRadius = 16, int PolygonSides = 6, bool AngleLabels = false)
{
    /// <summary>
    /// Новая конфигурация и сброс: умный карандаш, неоновый изумрудный цвет (сохранённый в app.json выбор пользователя не меняется).
    /// </summary>
    public static DrawSettings Default { get; } = new(DrawTool.SmartPen, new ThemeColor(0x00, 0xFF, 0x9C), 4, DrawDash.Solid, 0);

    public static readonly (double Min, double Max) ThicknessRange = (1, 40);
    public static readonly (int Min, int Max) FadeRange = (0, 120);
    public static readonly (double Min, double Max) TextSizeRange = (10, 96);

    /// <summary>Скругление прямоугольника, DIP (ограничивается и половиной меньшей стороны фигуры).</summary>
    public static readonly (double Min, double Max) CornerRange = (0, 120);

    /// <summary>Вершин у правильного многоугольника.</summary>
    public static readonly (int Min, int Max) SidesRange = (3, 12);

    public DrawSettings Sanitized() => this with
    {
        Thickness = FocusSettings.Clamp(Thickness, ThicknessRange, 4),
        TextSize = FocusSettings.Clamp(TextSize, TextSizeRange, 28),
        FadeSec = Math.Clamp(FadeSec, FadeRange.Min, FadeRange.Max),
        CornerRadius = FocusSettings.Clamp(CornerRadius, CornerRange, 16),
        PolygonSides = Math.Clamp(PolygonSides, SidesRange.Min, SidesRange.Max),
    };
}

/// <summary>Материал поверхностей окон Backpack.</summary>
public enum WindowMaterial
{
    /// <summary>Непрозрачные поверхности (работает везде; выбор при высокой контрастности и отключённой прозрачности).</summary>
    Opaque,

    /// <summary>Системный Mica (DWMWA_SYSTEMBACKDROP_TYPE = 2, Windows 11 22H2+).</summary>
    Mica,

    /// <summary>Системный Acrylic (DWMWA_SYSTEMBACKDROP_TYPE = 3, Windows 11 22H2+).</summary>
    Acrylic,

    /// <summary>
    /// Устаревшее значение (app.json до Phase 4H): Liquid Glass теперь стиль поверхностей (<see cref="SurfaceStyle.LiquidGlass"/>)
    /// поверх Acrylic. При чтении превращается в Acrylic + LiquidGlass; в интерфейсе материалом не предлагается.
    /// </summary>
    LiquidGlass,
}

/// <summary>
/// Визуальный стиль поверхностей окон (Phase 4H) — независим от светлой/тёмной основы, палитры и материала.
/// </summary>
public enum SurfaceStyle
{
    /// <summary>Обычные карточки и поля: нейтральные границы палитры.</summary>
    Standard,

    /// <summary>Стеклянные карточки с бликом и светлой градиентной границей (собственное оформление, не системный эффект).</summary>
    LiquidGlass,

    /// <summary>
    /// Backpack Neon: самая тёмная основа, тонкие границы цвета свечения палитры (карточки спокойнее, поля заметнее,
    /// выбранное ярче всего), мягкое свечение активных элементов. Текст не светится.
    /// </summary>
    Neon,
}

/// <summary>
/// Материал окон, стиль поверхностей и свечение акцентов (app.json v4). <see cref="Intensity"/> — сила прозрачности поверхностей
/// (0 — почти непрозрачно, 1 — максимум; текст и элементы всегда непрозрачны). <see cref="Surface"/> — стиль поверхностей.
/// <see cref="Glow"/> — свечение акцентных элементов в темах, названных драгоценными камнями, и в стиле Neon при любой палитре
/// (по умолчанию включено, умеренно); <see cref="GlowIntensity"/> — его сила.
/// </summary>
public sealed record AppearanceMaterial(WindowMaterial Material = WindowMaterial.Mica, double Intensity = 0.35, bool Glow = true, double GlowIntensity = 0.45,
    SurfaceStyle Surface = SurfaceStyle.Standard)
{
    public static AppearanceMaterial Default { get; } = new();

    public static readonly (double Min, double Max) IntensityRange = (0, 1);

    /// <summary>Материалы, которые предлагает интерфейс (без устаревшего <see cref="WindowMaterial.LiquidGlass"/>).</summary>
    public static readonly WindowMaterial[] Choices = [WindowMaterial.Opaque, WindowMaterial.Mica, WindowMaterial.Acrylic];

    public AppearanceMaterial Sanitized() => this with
    {
        Material = Material == WindowMaterial.LiquidGlass ? WindowMaterial.Acrylic : Enum.IsDefined(Material) ? Material : WindowMaterial.Mica,
        Surface = Material == WindowMaterial.LiquidGlass ? SurfaceStyle.LiquidGlass : Enum.IsDefined(Surface) ? Surface : SurfaceStyle.Standard,
        Intensity = FocusSettings.Clamp(Intensity, IntensityRange, 0.35),
        GlowIntensity = FocusSettings.Clamp(GlowIntensity, IntensityRange, 0.45),
    };
}

/// <summary>Что делает снимок: сохранить PNG, скопировать в буфер обмена или и то и другое.</summary>
public enum ScreenshotAction { Save, Copy, SaveAndCopy }

/// <summary>Что происходит с рисунками после успешного снимка (при ошибке сохранения рисунки не трогаются).</summary>
public enum AfterScreenshot { KeepDrawings, ClearDrawings, ExitDrawing }
