using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Settings;

public enum AnimationKind { Fade, None }

/// <summary>
/// Встроенный способ отрисовки кейкапа — геометрия, реализованная в коде. Добавляется только разработчиком; стиль и пресет
/// выбирают рендер и задают его параметры. lowprofile, laptop, pbt, minimal — по исследованным рендерам Keyviz (собственный
/// WPF-код); изометрический — собственный рендер KeyOverlay.
/// </summary>
public enum KeycapRenderer { LowProfile, Laptop, Pbt, Minimal, Volumetric, Neon }

/// <summary>Способ отрисовки значка мыши (собственная геометрия, без проводов).</summary>
public enum MouseRenderer { Outline, Solid, Retro, Sketch, Neon }

/// <summary>Боковые кнопки на значке мыши: ручная настройка (автоопределения количества кнопок нет — нет достоверных данных).</summary>
public enum SideButtonsMode { Hidden, One, Two }

public enum SideButtonsSide { Left, Right }

/// <summary>Девять якорей (окно оверлея, индикатор на экране). Это не выравнивание текста внутри кейкапа.</summary>
public enum ScreenAnchor { TopLeft, TopCenter, TopRight, CenterLeft, Center, CenterRight, BottomLeft, BottomCenter, BottomRight }

/// <summary>Все клавиши или только сочетания (модификатор + другая клавиша).</summary>
public enum KeyFilter { All, CombosOnly }

/// <summary>
/// След предыдущих клавиш. <see cref="None"/> — рекомендуемый: новое нажатие сразу заменяет прежнее на том же месте,
/// исчезающие кейкапы не занимают ряд. <see cref="FadeInPlace"/> — прежнее поведение: исчезают на месте, рядом с новыми.
/// </summary>
public enum TrailMode { None, FadeInPlace }

public enum MouseLabelMode { Graphic, Text }

public enum IndicatorAnimation { Inherit, Fade, None }

public enum IndicatorPosition { AboveOverlay, Screen }

/// <summary>
/// Материал подложки группы кейкапов и значка мыши (Phase 4H). Относится только к подложке: подписи, символы и индикаторы
/// кнопок не меняются. <see cref="Fill"/> — заливка цветом подложки (прозрачность — альфа цвета); <see cref="Clear"/> —
/// прозрачная подложка: без заливки, форма видна по тонкому контуру того же цвета.
/// <see cref="Acrylic"/> и <see cref="Mica"/> на окнах оверлея недоступны (см. <see cref="BackdropMaterials"/>) и заменяются
/// заливкой — честно, с пояснением в настройках, без имитации размытия.
/// </summary>
public enum BackdropMaterial { Fill, Clear, Acrylic, Mica }

/// <summary>Возможности материалов подложек на окнах оверлея (кейкапы и мышь — layered click-through окна WPF).</summary>
public static class BackdropMaterials
{
    /// <summary>
    /// Материал применим к оверлею. Проверено в Phase 4H на Windows 11 (lab/docs/Phase4H-Implementation-Report.md, п. 7):
    /// DWMWA_SYSTEMBACKDROP_TYPE (Mica, Acrylic) на layered-окне закрашивает весь прямоугольник окна непрозрачной подложкой;
    /// размытие SetWindowCompositionAttribute (недокументированное) действует на весь прямоугольник окна, область окна
    /// (SetWindowRgn) его не обрезает, скругление группы не повторяет и не исчезает плавно вместе с группой. Ни один вариант
    /// не выполняет требований к оверлею (без прямоугольника, со скруглением и исчезновением), поэтому оба недоступны.
    /// </summary>
    public static bool IsAvailable(BackdropMaterial m) => m is BackdropMaterial.Fill or BackdropMaterial.Clear;

    /// <summary>Материал, который действует: недоступный заменяется заливкой.</summary>
    public static BackdropMaterial Effective(BackdropMaterial m) => IsAvailable(m) ? m : BackdropMaterial.Fill;

    /// <summary>Контур прозрачной подложки, DIP.</summary>
    public const double ClearOutline = 1.25;

    /// <summary>Заливка и контур подложки по материалу (цвет — цвет подложки с его альфой).</summary>
    public static (System.Windows.Media.Brush? Fill, System.Windows.Media.Brush? Outline, double OutlineWidth) Paint(BackdropMaterial m, ThemeColor color) =>
        Effective(m) == BackdropMaterial.Clear
            ? (null, (color with { A = (byte)Math.Max((int)color.A, 0x66) }).ToFrozenBrush(), ClearOutline)
            : (color.ToFrozenBrush(), null, 0);
}

/// <summary>
/// Вид значка мыши у курсора и в ряду (поля схемы темы <c>mouse.*</c>). Цвета по рендеру: <see cref="IconForeground"/> — контур и
/// перегородки, <see cref="IconBackground"/> — подложка значка, <see cref="Body"/> — заливка корпуса (сплошной, ретро), <see cref="IconAccent"/> —
/// нажатая часть, <see cref="WheelColor"/> — стрелка прокрутки. Боковые кнопки — настройка оборудования: стиль их не меняет.
/// </summary>
public sealed record MouseVisual(
    ThemeColor ClickColor,      // круг под указателем; прозрачность — альфа цвета
    double ClickSize,           // диаметр круга, DIP
    double IconSize,            // высота значка, DIP
    ThemeColor IconBackground,
    ThemeColor IconForeground,
    ThemeColor IconAccent,      // цвет нажатой кнопки
    MouseRenderer Renderer = MouseRenderer.Outline,
    ThemeColor? BodyColor = null,
    ThemeColor? WheelArrowColor = null,
    int WheelPulseMs = 450,
    SideButtonsMode SideButtons = SideButtonsMode.Two,
    SideButtonsSide SideButtonsSide = SideButtonsSide.Left,
    string? StyleId = null,
    double OutlineWidth = 2.6,
    ThemeColor? SolidOutline = null,
    BackdropMaterial Plate = BackdropMaterial.Fill,
    double Glow = 0)
{
    /// <summary>
    /// <see cref="OutlineWidth"/> — толщина контура значка (DIP сетки 40×56) у всех семейств; линии перегородок и колеса
    /// меняются пропорционально. <see cref="SolidOutline"/> — контур корпуса у сплошного семейства (null — без контура).
    /// </summary>
    public ThemeColor Body => BodyColor ?? new ThemeColor(0xFF, 0xFF, 0xFF, 0x00);

    public ThemeColor WheelColor => WheelArrowColor ?? new ThemeColor(0x38, 0xBD, 0xF8);

    public static MouseVisual Default { get; } = new(
        new ThemeColor(0xFF, 0xD4, 0x00, 0x59), 48, 30,
        new ThemeColor(0x00, 0x00, 0x00, 0x99), new ThemeColor(0xFF, 0xFF, 0xFF), new ThemeColor(0x22, 0xC5, 0x5E),
        StyleId: "keyoverlay.mouse.outline-light");
}

/// <summary>Собственное оформление индикатора раскладки (поля схемы темы <c>languageIndicator.*</c>).</summary>
public sealed record IndicatorVisual(
    ThemeColor Background,
    ThemeColor Foreground,
    ThemeColor Border,
    double BorderWidth,         // 0 — без рамки
    double Radius,              // доля 0…1 от половины высоты (1 — «пилюля»)
    double TextSize,            // DIP
    string? StyleId = null)
{
    public static IndicatorVisual Default { get; } = new(
        new ThemeColor(0x1F, 0x1F, 0x1F, 0xE6), new ThemeColor(0xFF, 0xFF, 0xFF), new ThemeColor(0xFF, 0xFF, 0xFF, 0x33), 1, 0.5, 36,
        "keyoverlay.indicator.dark-pill");
}

/// <summary>
/// Внешний вид. Поля и единицы совпадают с разделами схемы темы (docs/themes/keyoverlay-theme.schema.json):
/// размеры — DIP, <see cref="Radius"/> — доля 0…1, длительность — мс, цвета — <see cref="ThemeColor"/>.
/// </summary>
public sealed record AppearanceSettings(
    double Size,
    double Radius,
    ThemeColor Primary,
    ThemeColor Secondary,
    ThemeColor TextColor,
    bool BorderEnabled,
    double BorderWidth,
    ThemeColor BorderColor,
    bool BackgroundEnabled,
    ThemeColor BackgroundColor,
    AnimationKind Animation,
    int DurationMs,
    LabelSet LabelSet = LabelSet.Windows,
    LabelMode LabelMode = LabelMode.Mixed,
    bool SideMarkers = true,
    bool ShowPressCount = true,
    bool PressAnimation = true,
    MouseVisual? Mouse = null,
    IndicatorVisual? Indicator = null,
    KeycapRenderer Renderer = KeycapRenderer.LowProfile,
    bool UseGradient = true,
    bool VolEnterShape = true,
    string? KeycapStyleId = "keyoverlay.lowprofile.classic",
    TwoLabelLayout? TwoLabels = null,
    BackdropMaterial Backdrop = BackdropMaterial.Fill,
    double Glow = 0)
{
    public MouseVisual MouseLook => Mouse ?? MouseVisual.Default;

    /// <summary>Геометрия двух подписей (settings v9); входит в стиль кейкапа.</summary>
    public TwoLabelLayout TwoLabelsOrDefault => TwoLabels ?? TwoLabelLayout.Default;

    public IndicatorVisual IndicatorLook => Indicator ?? IndicatorVisual.Default;

    /// <summary>Значения по умолчанию lowprofile (= Keyviz v2.1.1 по умолчанию, Phase 2A §3.3).</summary>
    public static AppearanceSettings Default { get; } = new(
        Size: 32,
        Radius: 0.5,
        Primary: new ThemeColor(0xFF, 0xFF, 0xFF),
        Secondary: new ThemeColor(0x1A, 0x1A, 0x1A),
        TextColor: new ThemeColor(0x00, 0x00, 0x00),
        BorderEnabled: true,
        BorderWidth: 2,
        BorderColor: new ThemeColor(0x1A, 0x1A, 0x1A),
        BackgroundEnabled: true,
        BackgroundColor: new ThemeColor(0xFF, 0xFF, 0xFF, 0x99),
        Animation: AnimationKind.Fade,
        DurationMs: 250,
        Mouse: MouseVisual.Default,
        Indicator: IndicatorVisual.Default);
}

/// <summary>Что и как показывать в ряду кейкапов.</summary>
/// <summary>
/// Что объединяется в группу: только сочетания с модификатором или любые одновременно удерживаемые клавиши (A+B тоже).
/// Независимо от фильтра «все клавиши / только сочетания».
/// </summary>
public enum ChordMode { Modifiers, AnyHeld }

/// <param name="AnyHeldMs">Порог одновременности для «любых одновременных клавиш», мс (0 — без порога).</param>
public sealed record KeysSettings(KeyFilter Filter, TrailMode Trail, int LingerMs, bool MouseInRow, MouseLabelMode MouseLabels, ChordMode Chords = ChordMode.Modifiers, int AnyHeldMs = 150)
{
    public static KeysSettings Default { get; } = new(KeyFilter.All, TrailMode.None, 5000, true, MouseLabelMode.Graphic);
}

/// <summary>
/// Положение группы: якорь в рабочей области, <b>расстояние от края</b> (действует только по осям, где якорь у края)
/// и <b>смещение от якоря</b> (действует всегда; X — вправо, Y — вниз; отрицательное — влево/вверх).
/// </summary>
public sealed record PlacementSettings(ScreenAnchor Anchor, double EdgeX, double EdgeY, double OffsetX, double OffsetY)
{
    public static PlacementSettings Default { get; } = new(ScreenAnchor.BottomCenter, 100, 100, 0, 0);
}

/// <summary>
/// Мышь у курсора: значок, круг щелчка и подсветка курсора включаются независимо; смещение значка — от острия указателя, DIP.
/// Подсветка (settings v6) — постоянный мягкий ореол вокруг указателя: цвет с прозрачностью и диаметр, DIP.
/// </summary>
public sealed record MouseCursorSettings(bool Enabled, bool ShowIcon, bool ShowCircle, double OffsetX, double OffsetY,
    bool ShowHighlight = false, ThemeColor? HighlightColor = null, double HighlightSize = 72)
{
    public static MouseCursorSettings Default { get; } = new(false, true, true, 22, 22);

    public static readonly ThemeColor DefaultHighlight = new(0x31, 0xC9, 0x9A, 0x55);

    public ThemeColor Highlight => HighlightColor ?? DefaultHighlight;
}

public sealed record LayoutIndicatorSettings(
    bool Enabled,
    int DurationMs,
    bool ShowOnWindowSwitch,
    bool CustomStyle = false,
    IndicatorAnimation Animation = IndicatorAnimation.Inherit,
    IndicatorPosition Position = IndicatorPosition.AboveOverlay,
    ScreenAnchor Anchor = ScreenAnchor.Center,
    double OffsetX = 0,
    double OffsetY = 0)
{
    public static LayoutIndicatorSettings Default { get; } = new(true, 1200, true);
}

/// <summary>Как показывается панель инструментов рисования.</summary>
public enum DrawToolbarMode { Docked, AutoHide, RadialOnly }

public enum ScreenEdge { Top, Bottom, Left, Right }

/// <summary>
/// Поведение рисования в профиле: режим панели (постоянная, автоскрытие у края, только радиальное меню), край для автоскрытия,
/// положение постоянной панели (доля ширины и высоты монитора, центр панели; null — сверху по центру), действие снимка и что
/// делать с рисунками после успешного снимка. <see cref="Compact"/> (settings v6) — компактная панель: инструменты собраны по
/// категориям во всплывающие группы, текущий инструмент и цвет всегда видны.
/// </summary>
public sealed record DrawingProfile(
    DrawToolbarMode ToolbarMode,
    ScreenEdge AutoHideEdge,
    double? ToolbarX,
    double? ToolbarY,
    ScreenshotAction ShotAction,
    AfterScreenshot AfterShot,
    bool Compact = false)
{
    public static DrawingProfile Default { get; } = new(DrawToolbarMode.Docked, ScreenEdge.Top, null, null, ScreenshotAction.Save, AfterScreenshot.KeepDrawings);
}

public sealed record LabSettings(
    AppearanceSettings Appearance,
    PlacementSettings Placement,
    KeysSettings Keys,
    LayoutIndicatorSettings LayoutIndicator,
    MouseCursorSettings MouseCursor,
    DrawingProfile? Drawing = null,
    DisplaySettings? Displays = null,
    FontSettings? Fonts = null,
    SecondaryLabelSettings? Labels = null,
    PluginProfileValues? Plugins = null)
{
    /// <summary>Настройки плагинов в профиле (settings v8). Всегда заполнено.</summary>
    public PluginProfileValues? Plugins { get; init; } = Plugins ?? PluginProfileValues.Empty;

    public PluginProfileValues PluginsOrEmpty => Plugins ?? PluginProfileValues.Empty;

    /// <summary>Шрифты интерфейса и кейкапов (settings v6). Всегда заполнено.</summary>
    public FontSettings? Fonts { get; init; } = Fonts ?? FontSettings.Default;

    public FontSettings FontsOrDefault => Fonts ?? FontSettings.Default;

    /// <summary>Дополнительные подписи раскладки (settings v6). Всегда заполнено.</summary>
    public SecondaryLabelSettings? Labels { get; init; } = Labels ?? SecondaryLabelSettings.Default;

    public SecondaryLabelSettings LabelsOrDefault => Labels ?? SecondaryLabelSettings.Default;

    /// <summary>Всегда заполнено (null при создании означает значения по умолчанию) — профили сравниваются по значениям.</summary>
    public DrawingProfile? Drawing { get; init; } = Drawing ?? DrawingProfile.Default;

    public DrawingProfile DrawingOrDefault => Drawing ?? DrawingProfile.Default;

    /// <summary>Мониторы для кейкапов и индикатора раскладки (settings v5). Всегда заполнено.</summary>
    public DisplaySettings? Displays { get; init; } = Displays ?? DisplaySettings.Default;

    public DisplaySettings DisplaysOrDefault => Displays ?? DisplaySettings.Default;

    public static LabSettings Default { get; } = new(
        AppearanceSettings.Default, PlacementSettings.Default, KeysSettings.Default, LayoutIndicatorSettings.Default, MouseCursorSettings.Default,
        DrawingProfile.Default);
}

/// <summary>Допустимые диапазоны. Для общих с темой полей совпадают со схемой темы.</summary>
public static class Limits
{
    public static readonly (double Min, double Max) Size = (8, 128);
    public static readonly (double Min, double Max) Radius = (0, 1);

    /// <summary>Интенсивность свечения кейкапа и значка мыши (Phase 4I, стиль Neon): 0 — без свечения.</summary>
    public static readonly (double Min, double Max) Glow = (0, 1);
    public static readonly (double Min, double Max) BorderWidth = (0.5, 16);
    public static readonly (double Min, double Max) IndicatorBorderWidth = (0, 16);
    public static readonly (int Min, int Max) DurationMs = (50, 1000);
    public static readonly (int Min, int Max) LingerMs = (0, 60_000);
    public static readonly (double Min, double Max) Edge = (0, 400);
    public static readonly (double Min, double Max) Offset = (-2000, 2000);
    public static readonly (int Min, int Max) IndicatorDurationMs = (300, 5000);
    public static readonly (double Min, double Max) IndicatorTextSize = (12, 160);
    public static readonly (double Min, double Max) ClickSize = (20, 400);
    public static readonly (double Min, double Max) IconSize = (16, 200);
    public static readonly (double Min, double Max) CursorOffset = (-200, 200);
    public static readonly (int Min, int Max) WheelPulseMs = (100, 2000);

    /// <summary>Толщина контура значка мыши (сетка 40×56).</summary>
    public static readonly (double Min, double Max) MouseOutline = (0.5, 6);
    public static readonly (int Min, int Max) AnyHeldMs = (0, 500);
    public static readonly (double Min, double Max) HighlightSize = (24, 300);
}
