using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Геометрия кейкапа по рендеру. S — размер, r — скругление (доля). Формулы lowprofile, laptop, pbt и minimal — Phase 2A
/// (ST-02…ST-06, GR-02, PC-02, по значениям из кода Keyviz); изометрический — собственный рендер KeyOverlay.
/// lowprofile при S = 32, r = 0.5: высота 80, верхняя грань 72, мин. ширина 72 / 80, радиус 20, ход 8, видимая «юбка» 8.
/// </summary>
public readonly record struct KeycapMetrics(double S, double R, KeycapRenderer Renderer = KeycapRenderer.LowProfile, double Glow = 0)
{
    public static KeycapMetrics From(AppearanceSettings a) => new(a.Size, a.Radius, a.Renderer, a.Glow);

    public double Height => Renderer switch
    {
        KeycapRenderer.Laptop => S * 2.25,
        KeycapRenderer.Pbt => S * 2.75,
        KeycapRenderer.Minimal => S * 1.7,
        KeycapRenderer.Volumetric => VolHeight,
        KeycapRenderer.Neon => S * 2.35,
        _ => S * 2.5,
    };

    /// <summary>Высота верхней грани (lowprofile) или основной части корпуса.</summary>
    public double FaceHeight => Renderer switch
    {
        KeycapRenderer.Laptop => S * 2.25,
        KeycapRenderer.Pbt => S * 2.2,
        KeycapRenderer.Minimal => S * 1.7,
        KeycapRenderer.Volumetric => VolHeight,
        _ => S * 2.25,
    };

    public double MinWidth => Renderer switch
    {
        KeycapRenderer.Pbt => S * 2.75,
        KeycapRenderer.Minimal => S * 0.6,
        KeycapRenderer.Volumetric => VolWidth(1),
        _ => S * 2.25,
    };

    public double ModifierMinWidth => Renderer switch
    {
        KeycapRenderer.Pbt => S * 3,
        KeycapRenderer.Minimal => S * 0.6,
        KeycapRenderer.Volumetric => VolWidth(1),
        _ => S * 2.5,
    };

    public double CornerRadius => R * S * 1.25;
    public double PaddingX => S * (R < 0.75 ? 0.5 : 0.5 + R - 0.75);
    public double PaddingY => S * 0.4;

    /// <summary>
    /// Полный ход нажатия (DIP) — значение <c>PressOffset</c> нажатой клавиши. lowprofile 0.25·S, pbt 0.15·S; у laptop нажатие —
    /// смена внутренней тени и небольшой сдвиг подписи; у minimal — масштаб 0.95 (ход — условная шкала); у изометрического —
    /// опускание верхней грани по вертикали.
    /// </summary>
    public double PressTravel => Renderer switch
    {
        KeycapRenderer.Pbt => S * 0.15,
        KeycapRenderer.Laptop => S * 0.06,
        KeycapRenderer.Minimal => S * 0.1,
        KeycapRenderer.Volumetric => S * 0.28,
        KeycapRenderer.Neon => S * 0.1,
        _ => S * 0.25,
    };

    public double LabelSize => S;
    public double SmallLabelSize => S * 0.5;

    public double KeyGap => Renderer == KeycapRenderer.Minimal ? S * 0.15 : S * 0.3;
    public double GroupPadding => Renderer == KeycapRenderer.Minimal ? S * 0.25 : S * 0.4;
    public double GroupCornerRadius => R * S * 1.75;

    public double BadgeSize => S * 0.75;
    public double BadgeFontSize => S * 0.4;
    public double BadgeCornerRadius => Math.Min(BadgeSize / 2, R * BadgeSize);

    /// <summary>Выход бейджа за верхний и правый край кейкапа: translate(25 %, −25 %).</summary>
    public double BadgeOvershoot => BadgeSize * 0.25;

    public double GroupHeight(bool background) => Height + (background ? 2 * GroupPadding : 0);

    /// <summary>Поле вокруг группы, чтобы бейдж и сглаживание не обрезались краем окна.</summary>
    public double OuterMargin => Math.Max(Math.Max(BadgeOvershoot, 2) + 2, GlowExtent + 2);

    /// <summary>
    /// Вынос свечения Neon за край кейкапа (Phase 4I): размытие свечения при нажатии (самое сильное) — оно учитывается в поле
    /// вокруг группы и в отступах, чтобы край окна оверлея не обрезал свечение.
    /// </summary>
    public double GlowExtent => Renderer == KeycapRenderer.Neon ? GlowBlur(1) : 0;

    /// <summary>Радиус размытия свечения Neon при доле нажатия <paramref name="press"/> (0…1).</summary>
    public double GlowBlur(double press) => S * Math.Clamp(Glow, 0, 1) * (0.32 + (0.18 * press));

    // ── объёмный (вид спереди-сверху): единица клавиши U = 2.2·S по ширине и глубине, высота корпуса 0.7·S ──
    // Проекция: X = u, Y = v·K − z (K — сокращение глубины верхней грани). Верхняя грань — почти прямоугольник, видны передняя
    // грань и скошенные боковины; подпись стоит прямо. Высота не зависит от ширины клавиши.

    public double VolUnit => S * 2.2;
    public double VolDepth => S * 0.7;
    public const double VolK = 0.62;

    /// <summary>Ширина клавиши шириной <paramref name="units"/> единиц.</summary>
    public double VolWidth(double units) => (units * VolUnit) + (S * 0.12);

    public double VolHeight => (VolUnit * VolK) + VolDepth + (S * 0.12);
}
