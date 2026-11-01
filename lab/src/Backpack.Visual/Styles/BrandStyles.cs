using Backpack.Visual.Settings;

namespace Backpack.Visual.Styles;

/// <summary>
/// Фирменные пресеты «Backpack Neon Emerald» (Phase 4I) — кейкапы и значок мыши в одном характере с оформлением Backpack Neon +
/// Emerald: почти чёрная поверхность с изумрудным оттенком, тонкий изумрудный контур, мягкое свечение, светлые подписи.
/// Цвета — те же константы, что у палитры интерфейса <see cref="UiPalettes.Emerald"/>, но записаны в стиль значениями: смена
/// темы приложения выбранные кейкапы и мышь не перекрашивает. Толщина контура, скругление и интенсивность свечения согласованы
/// между кейкапом и мышью.
/// <para>
/// Чистая установка и «Сбросить раздел» выбирают эти пресеты (<see cref="Starter"/>). Существующие настройки не меняются:
/// <see cref="AppearanceSettings.Default"/> остаётся прежним, потому что из него читаются отсутствующие поля старых файлов.
/// </para>
/// </summary>
public static class BrandStyles
{
    public const string Name = "Backpack Neon Emerald";
    public const string KeycapId = "backpack.neon.emerald";
    public const string MouseId = "backpack.mouse.neon-emerald";

    /// <summary>Свечение по умолчанию (0…1) — одно для кейкапа и мыши.</summary>
    public const double Glow = 0.6;

    private static ThemeColor Rgb(uint rgb, byte a = 0xFF) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

    /// <summary>Почти чёрная поверхность с лёгким изумрудным оттенком (темнее фона окна Emerald 0x111513).</summary>
    public static ThemeColor Surface => new(0x0B, 0x12, 0x0F);

    public static ThemeColor Line => Rgb(UiPalettes.Emerald.Glow);

    public static ThemeColor Text => Rgb(UiPalettes.Emerald.Text);

    public static AppearanceSettings Keycap { get; } = AppearanceSettings.Default with
    {
        KeycapStyleId = null,
        Renderer = KeycapRenderer.Neon,
        Primary = Surface,
        Secondary = Line,          // умеренная заливка нажатой грани
        TextColor = Text,
        BorderEnabled = true,
        BorderWidth = 1.25,
        BorderColor = Line,
        Radius = 0.4,
        BackgroundEnabled = false, // группа без подложки: свечение не прячется под плашкой
        UseGradient = false,
        Glow = Glow,
    };

    public static MouseVisual Mouse { get; } = MouseVisual.Default with
    {
        StyleId = null,
        Renderer = MouseRenderer.Neon,
        BodyColor = Surface,
        IconForeground = Line,
        IconAccent = Line,
        IconBackground = new ThemeColor(0, 0, 0, 0),
        WheelArrowColor = Rgb(0x8FF5D2),
        ClickColor = Rgb(UiPalettes.Emerald.Glow, 0x59),
        OutlineWidth = 2.0,
        Glow = Glow,
    };

    /// <summary>Шрифт оформления Backpack Neon Emerald: встроенный Spectral и в интерфейсе, и на кейкапах.</summary>
    public static FontSettings Fonts { get; } = new(FontSettings.Spectral, FontSettings.Spectral);

    /// <summary>
    /// Настройки чистой установки, нового профиля и сброса: значения по умолчанию с фирменными кейкапами, мышью и шрифтом Spectral.
    /// </summary>
    public static LabSettings Starter => s_starter ??= CreateStarter(); // лениво: каталог стилей сам ссылается на пресеты

    private static LabSettings CreateStarter()
    {
        var s = WithBrand(LabSettings.Default);
        // Круг щелчка у указателя стиль не хранит (это настройка пользователя) — у чистой установки он изумрудный, как мышь.
        return s with { Fonts = Fonts, Appearance = s.Appearance with { Mouse = s.Appearance.MouseLook with { ClickColor = Mouse.ClickColor } } };
    }

    private static LabSettings? s_starter;

    /// <summary>Применить фирменные кейкапы и мышь (только стили — включение инструментов и плагинов не меняется).</summary>
    public static LabSettings WithBrand(LabSettings s)
    {
        var withKeycap = StyleOps.Apply(s, BuiltInStyles.Find(KeycapId)!);
        return StyleOps.Apply(withKeycap, BuiltInStyles.Find(MouseId)!);
    }
}
