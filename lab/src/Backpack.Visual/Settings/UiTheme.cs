using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Backpack.Visual.Settings;

/// <summary>Оформление окон приложения. Не влияет на оформление кейкапов, индикатора и мыши у курсора.</summary>
public enum UiThemeMode { System, Light, Dark }

/// <summary>Почему действующий материал отличается от выбранного.</summary>
public enum MaterialFallback { None, NotSupported, TransparencyOff, HighContrast }

/// <summary>
/// Тема интерфейса обычных окон Backpack: один общий словарь <see cref="SettingsTheme"/> на UI-поток, в котором подменяются
/// значения кистей Ko.*, материал поверхностей, свечение акцентов и шрифт интерфейса. Стили ссылаются на них через
/// DynamicResource, поэтому смена применяется к уже открытым окнам. «Системная» тема читает
/// HKCU\…\Themes\Personalize\AppsUseLightTheme и следит за её изменением; там же — признак «Эффекты прозрачности».
/// <para>
/// Материалы: Mica и Acrylic — системные подложки DWM (DWMWA_SYSTEMBACKDROP_TYPE, Windows 11 22H2 и новее); Liquid Glass —
/// собственное оформление Backpack поверх системного Acrylic (стеклянные карточки, мягкие градиентные границы и блик), а не
/// системный эффект. Прозрачность регулирует только заливку поверхностей: текст и элементы управления всегда непрозрачны.
/// При отключённых эффектах прозрачности, высокой контрастности или старой Windows действует непрозрачный вариант.
/// Меню и подсказки — всплывающие окна без подложки DWM: для них используется плотная заливка Ko.FlyoutBrush.
/// </para>
/// Системные файловые диалоги Windows этой темой не управляются.
/// </summary>
public static partial class UiTheme
{
    public const string DefaultFontUri = "pack://application:,,,/Backpack.Visual;component/Fonts/#Inter";

    [ThreadStatic] private static SettingsTheme? t_resources;
    [ThreadStatic] private static UiThemeMode t_mode;
    [ThreadStatic] private static string? t_palette;
    [ThreadStatic] private static bool t_dark;
    [ThreadStatic] private static bool t_listening;
    [ThreadStatic] private static bool t_watchingSystem;
    [ThreadStatic] private static Action? t_changed;
    [ThreadStatic] private static System.Windows.Threading.Dispatcher? t_dispatcher;
    [ThreadStatic] private static AppearanceMaterial? t_material;
    [ThreadStatic] private static string? t_uiFont;

    /// <summary>Общий словарь ресурсов этого UI-потока (подключается к окнам через <see cref="Attach"/>).</summary>
    public static SettingsTheme Resources => t_resources ??= CreateResources();

    public static UiThemeMode Mode => t_mode;

    /// <summary>Цветовая тема окон (<see cref="UiPalettes"/>), независимая от светлого/тёмного режима.</summary>
    public static string PaletteId => t_palette ?? UiPalettes.DefaultId;

    /// <summary>Выбранный материал и свечение (как в настройках).</summary>
    public static AppearanceMaterial Material => t_material ?? AppearanceMaterial.Default;

    /// <summary>Системные подложки DWM доступны: Windows 11 22H2 (сборка 22621) и новее.</summary>
    public static bool SystemBackdropSupported { get; } = Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Почему действует непрозрачный вариант вместо выбранного материала (None — выбранный действует).</summary>
    public static MaterialFallback Fallback =>
        Material.Material == WindowMaterial.Opaque ? MaterialFallback.None
        : SystemParameters.HighContrast ? MaterialFallback.HighContrast
        : !TransparencyEnabled() ? MaterialFallback.TransparencyOff
        : !SystemBackdropSupported ? MaterialFallback.NotSupported
        : MaterialFallback.None;

    /// <summary>Материал, который действует сейчас (с учётом ограничений системы).</summary>
    public static WindowMaterial EffectiveMaterial => Fallback == MaterialFallback.None ? Material.Material : WindowMaterial.Opaque;

    /// <summary>Цвет рамки окна DWM в стиле Neon (свечение палитры, приглушённое к фону); null — системный цвет.</summary>
    public static Color? WindowEdge => t_edge;

    [ThreadStatic] private static Color? t_edge;

    /// <summary>Стиль поверхностей, который действует сейчас: при высокой контрастности — всегда стандартный.</summary>
    public static SurfaceStyle EffectiveSurface => SystemParameters.HighContrast ? SurfaceStyle.Standard : Material.Surface;

    /// <summary>
    /// Свечение акцентов действует: тема — драгоценный камень или стиль Neon (при любой палитре), свечение включено и не высокая
    /// контрастность.
    /// </summary>
    public static bool GlowActive => (UiPalettes.IsGem(PaletteId) || EffectiveSurface == SurfaceStyle.Neon) && Material.Glow && !SystemParameters.HighContrast;

    /// <summary>Шрифт интерфейса: null — «Стандартный» (встроенный Inter), «Spectral» — встроенный Spectral.</summary>
    public static string? UiFont => t_uiFont;

    /// <summary>Сменить цветовую тему; режим (светлый/тёмный/системный) сохраняется.</summary>
    public static void SetPalette(string? id)
    {
        t_palette = UiPalettes.Get(id).Id;
        if (t_resources is not null)
        {
            Apply(t_dark);
        }
    }

    /// <summary>Материал, его интенсивность и свечение — применяются ко всем открытым окнам сразу.</summary>
    public static void SetMaterial(AppearanceMaterial material)
    {
        t_material = material.Sanitized();
        WatchSystem();
        if (t_resources is not null)
        {
            Apply(t_dark);
        }
    }

    /// <summary>
    /// Шрифт заголовков и названия программы во всех окнах (null — встроенный Inter). Основной текст, подписи и элементы
    /// управления всегда стандартным шрифтом — для читаемости (Phase 4F). Векторные иконки шрифт не меняет.
    /// </summary>
    public static void SetUiFont(string? family)
    {
        t_uiFont = FontSettings.NormalizeName(family);
        Resources["Ko.HeadingFont"] = UiFontFamily(t_uiFont);
        t_changed?.Invoke();
    }

    /// <summary>
    /// Семейство с цепочкой замен: [встроенный Spectral →] встроенный Inter → Segoe UI → Segoe UI Symbol. Символов, которых нет в
    /// Spectral (стрелки, ⌘ ⌥ ⇧, греческий), WPF берёт из следующих семейств — замена предсказуема. Оба шрифта встроены
    /// в сборку и в системе не устанавливаются.
    /// </summary>
    public static FontFamily UiFontFamily(string? family)
    {
        Rendering.VisualFonts.EnsurePackScheme();
        return new FontFamily(Rendering.VisualFonts.FontsBaseUri, Rendering.VisualFonts.Chain(family, "Segoe UI Variable Text, Segoe UI, Segoe UI Symbol"));
    }

    public static bool IsDark => t_dark;

    public static event Action Changed
    {
        add => t_changed += value;
        remove => t_changed -= value;
    }

    public static int SubscriberCount => t_changed?.GetInvocationList().Length ?? 0;

    public static void Set(UiThemeMode mode)
    {
        t_mode = mode;
        WatchSystem();
        if (mode == UiThemeMode.System && !t_listening)
        {
            t_listening = true;
        }
        else if (mode != UiThemeMode.System)
        {
            t_listening = false;
        }

        Apply(mode switch { UiThemeMode.Dark => true, UiThemeMode.Light => false, _ => SystemPrefersDark() });
    }

    /// <summary>Отписка от системных уведомлений (выход из приложения).</summary>
    public static void StopListening()
    {
        if (t_watchingSystem)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            t_watchingSystem = false;
        }

        t_listening = false;
    }

    /// <summary>Следить за темой Windows, эффектами прозрачности и высокой контрастностью (один подписчик на поток).</summary>
    private static void WatchSystem()
    {
        if (t_watchingSystem)
        {
            return;
        }

        t_watchingSystem = true;
        t_dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static bool SystemPrefersDark() => ReadPersonalize("AppsUseLightTheme") is 0;

    /// <summary>Панель задач в светлой теме (для значка в трее): SystemUsesLightTheme = 1.</summary>
    public static bool TaskbarIsLight() => ReadPersonalize("SystemUsesLightTheme") is 1;

    /// <summary>«Параметры → Персонализация → Цвета → Эффекты прозрачности».</summary>
    public static bool TransparencyEnabled() => ReadPersonalize("EnableTransparency") is not 0;

    private static int? ReadPersonalize(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue(name) is int v ? v : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Подключить общие ресурсы, тему, материал и оболочку окна (полоса заголовка в оформлении Backpack).</summary>
    public static void Attach(Window window)
    {
        if (!window.Resources.MergedDictionaries.Contains(Resources))
        {
            window.Resources.MergedDictionaries.Add(Resources);
        }

        window.Style = Resources.StyleOf("Ko.Window");
        WindowShell.Attach(window);
        window.Icon = Brand.LogoArt.WindowIcon(LogoColor());
        void OnChanged()
        {
            WindowShell.ApplyMaterial(window);
            window.Icon = Brand.LogoArt.WindowIcon(LogoColor());
        }

        Changed += OnChanged;
        window.Closed += (_, _) => Changed -= OnChanged;
    }

    public static Brush Brush(string key) => (Brush)Resources[key];

    /// <summary>Цвет логотипа: акцент темы; у «Классической» — чёрный на светлом и белый на тёмном фоне.</summary>
    public static Color LogoColor()
    {
        uint c = UiPalettes.LogoColor(PaletteId, t_dark);
        return Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }

    private static Color Rgb(uint c, byte a = 0xFF) => Color.FromArgb(a, (byte)(c >> 16), (byte)(c >> 8), (byte)c);

    private static SolidColorBrush Solid(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void Apply(bool dark)
    {
        var r = Resources;
        var palette = UiPalettes.Get(PaletteId);
        var values = new Dictionary<string, uint>(dark ? palette.Dark : palette.Light);
        var surface = EffectiveSurface;
        if (surface == SurfaceStyle.Neon)
        {
            UiPalettes.ApplyNeon(values, dark);
        }

        foreach (var (key, c) in values)
        {
            byte a = (byte)(c >> 24);
            r[key] = Solid(Rgb(c, a == 0 ? (byte)0xFF : a));
        }

        t_dark = dark;
        uint window = values["Ko.WindowBrush"], sidebar = values["Ko.SidebarBrush"], card = values["Ko.CardBrush"], border = values["Ko.BorderBrush"];
        uint hover = values["Ko.HoverBrush"], text = values["Ko.TextBrush"], accent = values["Ko.AccentBrush"];
        r["Ko.PressedBrush"] = Solid(Rgb(UiPalettes.Mix(hover, text, dark ? 0.08 : 0.06)));
        r["Ko.LogoBrush"] = Solid(LogoColor());
        r["Ko.CaptionCloseBrush"] = Solid(Rgb(0xC42B1C));
        r["Ko.StageBrush"] = Solid(Rgb(dark ? UiPalettes.Mix(window, 0x000000, 0.25) : UiPalettes.Mix(window, accent, 0.05)));
        r["Ko.StagePanelBrush"] = Solid(Rgb(dark ? UiPalettes.Mix(card, 0xFFFFFF, 0.04) : 0xFFFFFF));
        uint stageLine = dark ? UiPalettes.Mix(card, 0xFFFFFF, 0.16) : UiPalettes.Mix(0xFFFFFF, text, 0.16);
        r["Ko.StageLineBrush"] = Solid(Rgb(stageLine));
        r["Ko.StageGridBrush"] = StageGrid(Rgb(stageLine, 0x55));
        r["Ko.DemoBrush"] = r["Ko.StageBrush"];
        uint glowColor = values["Ko.GlowBrush"];
        var stageGlow = new RadialGradientBrush(Rgb(glowColor, dark ? (byte)0x55 : (byte)0x30), Rgb(glowColor, 0)) { Center = new Point(0.78, 0.25), GradientOrigin = new Point(0.78, 0.25), RadiusX = 0.55, RadiusY = 0.9 };
        stageGlow.Freeze();
        r["Ko.StageGlowBrush"] = stageGlow;
        r["Ko.FlyoutBrush"] = Solid(Rgb(card));
        t_edge = surface == SurfaceStyle.Neon ? Rgb(UiPalettes.Mix(window, dark ? glowColor : accent, dark ? 0.45 : 0.35)) : null;

        ApplyMaterial(r, dark, surface, window, sidebar, card, border, values.GetValueOrDefault("Ko.CardBorderBrush", border));
        // Светлый Neon: свечение на белом заметнее — мягче, чтобы не превращать светлую основу в сияние.
        double glow = Material.GlowIntensity * (surface == SurfaceStyle.Neon && !dark ? 0.7 : 1);
        r["Ko.AccentGlowEffect"] = GlowActive ? Glow(values["Ko.GlowBrush"], glow) : null;
        t_changed?.Invoke();
    }

    /// <summary>Сетка «рабочего стола» сцены превью: тонкие линии с шагом 28 DIP.</summary>
    private static DrawingBrush StageGrid(Color line)
    {
        var drawing = new GeometryDrawing(null, new Pen(new SolidColorBrush(line), 0.75), Geometry.Parse("M0,0.5 H28 M0.5,0 V28"));
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 28, 28),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 28, 28),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Мягкое свечение акцента: радиус и непрозрачность растут с интенсивностью, но остаются умеренными.</summary>
    internal static DropShadowEffect Glow(uint color, double intensity)
    {
        var e = new DropShadowEffect
        {
            Color = Rgb(color),
            ShadowDepth = 0,
            BlurRadius = 6 + (14 * intensity),
            Opacity = 0.2 + (0.45 * intensity),
            RenderingBias = RenderingBias.Performance,
        };
        e.Freeze();
        return e;
    }

    /// <summary>
    /// Заливки поверхностей по материалу. Непрозрачный — цвета палитры как есть. Mica/Acrylic — окно и боковая панель тонированы
    /// поверх подложки (чем выше интенсивность, тем прозрачнее), карточки плотнее окна, чтобы текст на них оставался читаемым.
    /// Стиль Liquid Glass — карточки из полупрозрачного градиента с бликом сверху и светлой градиентной границей (при непрозрачном
    /// материале — та же граница и блик на плотной заливке). Стиль Neon — граница карточек цвета свечения (<paramref name="cardBorder"/>).
    /// </summary>
    private static void ApplyMaterial(SettingsTheme r, bool dark, SurfaceStyle surface, uint window, uint sidebar, uint card, uint border, uint cardBorder)
    {
        var m = EffectiveMaterial;
        bool glass = surface == SurfaceStyle.LiquidGlass;
        double k = m == WindowMaterial.Opaque ? 0 : Material.Intensity;
        r["Ko.CardRadius"] = new CornerRadius(glass ? 18 : 14);
        r["Ko.CardBorderBrush"] = Solid(Argb(cardBorder));
        r["Ko.CardSheenBrush"] = Solid(Colors.Transparent);
        if (m == WindowMaterial.Opaque && !glass)
        {
            r["Ko.FlyoutBrush"] = Solid(Rgb(card));
            return;
        }

        byte A(double opaque, double clear) => (byte)Math.Round(255 * (opaque + ((clear - opaque) * k)));
        if (m != WindowMaterial.Opaque)
        {
            r["Ko.WindowBrush"] = Solid(Rgb(window, A(0.88, 0.30)));
            r["Ko.SidebarBrush"] = Solid(Rgb(sidebar, A(0.70, 0.18)));
            r["Ko.FlyoutBrush"] = Solid(Rgb(card, 0xF5));
        }

        if (glass)
        {
            uint top = dark ? UiPalettes.Mix(card, 0xFFFFFF, 0.06) : 0xFFFFFF;
            var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0.35, 1) };
            fill.GradientStops.Add(new GradientStop(Rgb(top,A(dark ? 0.82 : 0.90, dark ? 0.44 : 0.52)), 0));
            fill.GradientStops.Add(new GradientStop(Rgb(dark ? card : 0xF4F7F5, A(dark ? 0.86 : 0.92, dark ? 0.50 : 0.58)), 1));
            fill.Freeze();
            r["Ko.CardBrush"] = fill;
            var edge = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(dark ? (byte)0x66 : (byte)0xCC, 0xFF, 0xFF, 0xFF), 0));
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(dark ? (byte)0x14 : (byte)0x40, 0xFF, 0xFF, 0xFF), 0.55));
            edge.GradientStops.Add(new GradientStop(Rgb(border, 0x80), 1));
            edge.Freeze();
            r["Ko.CardBorderBrush"] = edge;
            var sheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            sheen.GradientStops.Add(new GradientStop(Color.FromArgb(dark ? (byte)0x1F : (byte)0x55, 0xFF, 0xFF, 0xFF), 0));
            sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0.45));
            sheen.Freeze();
            r["Ko.CardSheenBrush"] = sheen;
        }
        else
        {
            r["Ko.CardBrush"] = Solid(Rgb(card, A(0.96, 0.66)));
        }
    }

    /// <summary>ARGB 0xAARRGGBB; старший байт 0 — непрозрачный RGB.</summary>
    private static Color Argb(uint c) => Rgb(c, (byte)(c >> 24) is var a && a != 0 ? a : (byte)0xFF);

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Accessibility)
        {
            var d = t_dispatcher;
            d?.BeginInvoke(() =>
            {
                Apply(t_mode == UiThemeMode.System ? SystemPrefersDark() : t_dark);
            });
        }
    }

    private static SettingsTheme CreateResources()
    {
        var r = new SettingsTheme();
        t_resources = r;
        if (t_uiFont is not null)
        {
            r["Ko.HeadingFont"] = UiFontFamily(t_uiFont);
        }

        Apply(t_mode == UiThemeMode.Dark || (t_mode == UiThemeMode.System && SystemPrefersDark()));
        return r;
    }
}
