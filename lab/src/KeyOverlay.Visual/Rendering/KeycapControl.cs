using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Rendering;

/// <summary>
/// Общая часть кейкапа любого рендера: содержимое по <see cref="KeyLabels"/> (символ, подпись, значок мыши), мелкая метка
/// («R», «num», «?»), бейдж счётчика повторов и ход нажатия <see cref="PressOffset"/>. Рендер задаёт только геометрию и место,
/// куда помещается содержимое. Элемент создаётся один раз на появление и дальше только обновляется: смена параметров не
/// пересоздаёт его, а смена рендера заменяет элемент в презентере без потери идентичности записи.
/// </summary>
public abstract class KeycapControl : Grid
{
    private readonly Border _badge = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
    private readonly TextBlock _badgeText = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };
    private readonly TranslateTransform _badgeShift = new();
    private (KeyCapContent Content, AppearanceSettings S, bool Pressed, bool Unknown, double Width, int Fonts, string? Secondary)? _built;
    private double _press;

    protected KeycapControl()
    {
        UseLayoutRounding = true;
        IsHitTestVisible = false;
        Focusable = false;
        _badge.Child = _badgeText;
        _badge.RenderTransform = _badgeShift;
    }

    public abstract KeycapRenderer Renderer { get; }

    public KeycapItem? Item { get; private set; }

    public KeyCapContent? Content { get; private set; }

    /// <summary>Содержимое (подпись, символ, значок): рендер помещает его на свою грань.</summary>
    protected Grid ContentHost { get; } = new();

    /// <summary>Область подписи (для проверок центрирования).</summary>
    internal Grid LabelArea => ContentHost;

    protected TextBlock Marker { get; } = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

    protected KeycapMetrics M { get; private set; }

    protected AppearanceSettings S { get; private set; } = AppearanceSettings.Default;

    /// <summary>Ход нажатия, DIP: 0 — отпущена, <see cref="PressTravel"/> — нажата. Анимирует презентер.</summary>
    public double PressOffset
    {
        get => _press;
        set
        {
            if (_press != value)
            {
                _press = value;
                OnPressChanged(value);
            }
        }
    }

    public double PressTravel => M.PressTravel;

    /// <summary>Доля хода 0…1.</summary>
    protected double PressFraction => M.PressTravel > 0 ? Math.Clamp(_press / M.PressTravel, 0, 1) : 0;

    /// <summary>Подпись содержимого (для тестов и проверки длинных подписей).</summary>
    public string MainText => Content?.Text ?? "";

    public string? MarkerText => Marker.Visibility == Visibility.Visible ? Marker.Text : null;

    public bool BadgeVisible => _badge.Visibility == Visibility.Visible;

    /// <summary>Шрифт подписи после ужатия (для тестов малых размеров).</summary>
    public double LabelFontSize { get; private set; }

    /// <summary>Доступная ширина содержимого на грани, DIP (рендер может её задать, иначе — по минимальной ширине модификатора).</summary>
    protected virtual double ContentWidth(KeycapItem item) => M.ModifierMinWidth - (2 * M.PaddingX) - (2 * BorderWidth);

    protected double BorderWidth => S.BorderEnabled ? S.BorderWidth : 0;

    protected virtual bool SupportsBadge => true;

    public void Apply(AppearanceSettings s, KeycapItem item, bool showBadge, bool mouseGraphic = true)
    {
        Item = item;
        S = s;
        M = KeycapMetrics.From(s);
        var text = s.TextColor.ToFrozenBrush();

        ApplyShape(s, item);

        var content = KeyLabels.Resolve(item, s.LabelSet, s.LabelMode, s.SideMarkers, mouseGraphic: mouseGraphic);
        Content = content;
        double width = ContentWidth(item);
        var key = (content, s, item.IsPressed, item.IsUnknown, width, VisualFonts.Generation, content.Marker is null && item.Secondary is { } sec ? sec + "|" + Layout.SecondaryLabels.Settings : null);
        if (_built != key)
        {
            _built = key;
            BuildContent(content, s, item, text, width);
        }

        ContentHost.Opacity = item.IsUnknown ? 0.55 : 1.0;

        Marker.Text = content.Marker ?? "";
        Marker.Visibility = content.Marker is null ? Visibility.Collapsed : Visibility.Visible;
        Marker.FontFamily = VisualFonts.Keycap;
        Marker.FontWeight = FontWeights.SemiBold;
        Marker.FontSize = Math.Max(7, M.S * 0.3);
        Marker.Foreground = text;
        Marker.Opacity = 0.7;

        if (!Children.Contains(_badge))
        {
            Children.Add(_badge);
        }

        _badge.Width = _badge.Height = M.BadgeSize;
        _badge.CornerRadius = new CornerRadius(M.BadgeCornerRadius);
        _badge.Background = text;
        _badgeText.Foreground = s.Primary.ToFrozenBrush();
        _badgeText.FontFamily = VisualFonts.Keycap;
        _badgeText.FontSize = M.BadgeFontSize;
        _badgeText.Text = item.PressCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _badgeShift.X = M.BadgeOvershoot;
        _badgeShift.Y = -M.BadgeOvershoot;
        _badge.Visibility = showBadge && SupportsBadge ? Visibility.Visible : Visibility.Collapsed;

        AfterApply(s, item);
        OnPressChanged(_press);
        if (_second is not null)
        {
            // Phase 4I: подписи пересоздаются при смене состояния (нажата → отпущена) и параметров, а размер кейкапа обычно тот же —
            // WPF тогда не вызывает ArrangeOverride кейкапа, и новая дополнительная подпись оставалась в (0, 0) слоя (у левого
            // верхнего угла). Место пересчитывается и при раскладке самого слоя (SecondaryLayer).
            InvalidateArrange();
        }
    }

    /// <summary>Геометрия и цвета рендера (вызывается при каждом обновлении).</summary>
    protected abstract void ApplyShape(AppearanceSettings s, KeycapItem item);

    protected virtual void AfterApply(AppearanceSettings s, KeycapItem item)
    {
    }

    /// <summary>Отрисовать ход нажатия (без пересоздания элементов).</summary>
    protected abstract void OnPressChanged(double offset);

    /// <summary>
    /// Горизонтальное выравнивание группы «символ + подпись». Phase 4A: по центру во всех рендерах (прежнее «справа снизу»
    /// у модификаторов давало смещённые подписи на объёмных кейкапах).
    /// </summary>
    protected virtual HorizontalAlignment SmallAlign(KeycapItem item) => HorizontalAlignment.Center;

    // ── две подписи (Phase 4H) ──
    private SecondaryLayer? _secondLayerField;

    private SecondaryLayer _secondLayer => _secondLayerField ??= new SecondaryLayer(PlaceSecondary) { IsHitTestVisible = false };

    /// <summary>Слой дополнительной подписи: после каждой своей раскладки (новая подпись, новый размер) пересчитывает её место.</summary>
    private sealed class SecondaryLayer(Action arranged) : Canvas
    {
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            // Сначала место (Canvas.Left/Top), затем раскладка детей: изменение места во время собственной раскладки слоя WPF
            // не перепланирует. Основная подпись к этому моменту уже размещена (она раньше в списке детей области подписи).
            arranged();
            return base.ArrangeOverride(arrangeSize);
        }
    }
    private readonly TranslateTransform _mainShift = new();
    private TextBlock? _main;
    private TextBlock? _second;
    private Size? _arranged;

    /// <summary>Лицевая поверхность, от которой отсчитывается место дополнительной подписи (по умолчанию — область подписи).</summary>
    protected virtual FrameworkElement LabelFace => ContentHost;

    /// <summary>Прямоугольник лицевой поверхности (внутри рамки) в координатах области подписи — после раскладки.</summary>
    internal Rect FaceRect()
    {
        var face = LabelFace;
        var off = default(Vector);
        for (DependencyObject? d = ContentHost; d is System.Windows.Media.Visual v && d != face; d = VisualTreeHelper.GetParent(d))
        {
            off += VisualTreeHelper.GetOffset(v);
        }

        var b = face is Border border ? border.BorderThickness : default;
        // Сам кейкап как грань (minimal): во время ArrangeOverride его ActualWidth ещё прежний — берётся размер раскладки.
        var size = face == this && _arranged is { } arranged ? arranged : new Size(face.ActualWidth, face.ActualHeight);
        return new Rect(b.Left - off.X, b.Top - off.Y, Math.Max(0, size.Width - b.Left - b.Right), Math.Max(0, size.Height - b.Top - b.Bottom));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = base.ArrangeOverride(arrangeSize);
        _arranged = size;
        PlaceSecondary();
        return size;
    }

    /// <summary>
    /// Место дополнительной подписи и сдвиг основной. Начало координат — безопасная точка у выбранного угла грани: отступ от края
    /// 0,3 радиуса скругления + 0,08·S (буква не заходит в скругление); <see cref="TwoLabelLayout.OffsetX"/>/<c>Y</c> — доли
    /// ширины и высоты грани к её центру; у правого верхнего угла — ещё место под счётчик повторов. Подпись всегда остаётся
    /// внутри грани. Основная подпись оптически сдвигается от дополнительной (0,05·S по обеим осям), а если глифы ближе
    /// 0,08·S — дальше, пока не появится промежуток или не кончится место на грани.
    /// </summary>
    private void PlaceSecondary()
    {
        if (_second is null || _main is null || ContentHost.ActualWidth <= 0)
        {
            return;
        }

        var t = S.TwoLabelsOrDefault;
        var f = FaceRect();
        bool right = t.Corner is LabelCorner.TopRight or LabelCorner.BottomRight;
        bool bottom = t.Corner is LabelCorner.BottomLeft or LabelCorner.BottomRight;
        double cr = LabelFace is Border fb ? fb.CornerRadius.TopLeft : M.CornerRadius * 0.5;
        double inset = (0.3 * cr) + (0.08 * M.S), edge = 0.03 * M.S;
        var sz = _second.DesiredSize;
        double x = right ? f.Right - inset - sz.Width - (t.OffsetX * f.Width) : f.Left + inset + (t.OffsetX * f.Width);
        double y = bottom ? f.Bottom - inset - sz.Height - (t.OffsetY * f.Height) : f.Top + inset + (t.OffsetY * f.Height);
        if (t.Corner == LabelCorner.TopRight && _badge.Visibility == Visibility.Visible)
        {
            y += M.BadgeSize * 0.3;
        }

        x = Math.Clamp(x, f.Left + edge, Math.Max(f.Left + edge, f.Right - edge - sz.Width));
        y = Math.Clamp(y, f.Top + edge, Math.Max(f.Top + edge, f.Bottom - edge - sz.Height));
        Canvas.SetLeft(_second, x);
        Canvas.SetTop(_second, y);

        // Глифы, а не строки текста: прописная основная — около 0,75 кегля по высоте, дополнительная — 0,8 высоты строки.
        var mo = VisualTreeHelper.GetOffset(_main);
        double mh = _main.FontSize * 0.75, mcy = mo.Y + (_main.RenderSize.Height / 2);
        var main = new Rect(mo.X, mcy - (mh / 2), _main.RenderSize.Width, mh);
        var sec = new Rect(x, y + (sz.Height * 0.1), sz.Width, sz.Height * 0.8);
        sec.Inflate(0.08 * M.S, 0.08 * M.S);
        double dx = right ? -1 : 1, dy = bottom ? -1 : 1, step = 0.05 * M.S;
        double sx = dx * step, sy = dy * step;
        for (int i = 0; i < 12 && Rect.Offset(main, sx, sy).IntersectsWith(sec); i++)
        {
            sx += dx * 0.02 * M.S;
            sy += dy * 0.02 * M.S;
        }

        // Основная подпись не выходит за грань.
        sx = Math.Clamp(sx, f.Left + edge - main.Left, Math.Max(f.Left + edge - main.Left, f.Right - edge - main.Right));
        sy = Math.Clamp(sy, f.Top + edge - main.Top, Math.Max(f.Top + edge - main.Top, f.Bottom - edge - main.Bottom));
        _mainShift.X = sx;
        _mainShift.Y = sy;
    }

    private void BuildContent(KeyCapContent c, AppearanceSettings s, KeycapItem item, Brush text, double available)
    {
        ContentHost.Children.Clear();
        _secondLayer.Children.Clear();
        _main = _second = null;
        _mainShift.X = _mainShift.Y = 0;
        var align = SmallAlign(item);
        LabelFontSize = M.LabelSize;
        switch (c.Layout)
        {
            case CapLayout.Character when item.Secondary is { } extra && c.Marker is null:
                // Две подписи (Phase 4H): основная (английская) — по центру области подписи, уменьшена (TwoLabels.MainScale);
                // дополнительная — в отдельном слое, место считается в ArrangeOverride от лицевой поверхности (PlaceSecondary),
                // а не от тесной области основной подписи, как в 4G (там «л» прилипала к «K»). Метки R/num здесь не бывает
                // (c.Marker is null); у правого верхнего угла учитывается счётчик повторов.
                var tl = s.TwoLabelsOrDefault;
                LabelFontSize = Fit(c.Text, M.LabelSize * tl.MainScale, available * 0.7, M.S * 0.35);
                _main = Label(c.Text, LabelFontSize, text, HorizontalAlignment.Center, VerticalAlignment.Center);
                _main.RenderTransform = _mainShift;
                ContentHost.Children.Add(_main);
                double secondSize = Math.Max(7, M.LabelSize * tl.SecondaryScale);
                _second = Label(extra, secondSize, text, HorizontalAlignment.Left, VerticalAlignment.Top);
                _second.FontWeight = FontWeights.SemiBold;
                _second.Opacity = 0.8;
                _second.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                _second.LineHeight = secondSize * 1.1;
                _second.Tag = "secondary";
                _secondLayer.Children.Add(_second);
                ContentHost.Children.Add(_secondLayer);
                break;

            case CapLayout.Character:
                LabelFontSize = Fit(c.Text, M.LabelSize, available, M.S * 0.45);
                ContentHost.Children.Add(Label(c.Text, LabelFontSize, text, HorizontalAlignment.Center, VerticalAlignment.Center));
                break;

            case CapLayout.GlyphOnly:
                ContentHost.Children.Add(Center(KeyGlyphs.Create(c.Glyph, M.S * 0.8, text)));
                break;

            case CapLayout.GlyphAndText:
                // Символ и подпись — одна группа, центрированная в области подписи верхней грани (а не символ у верхнего края
                // и подпись у нижнего). Метка стороны (R, Num) — отдельный элемент и группу не сдвигает.
                var glyph = KeyGlyphs.Create(c.Glyph, M.SmallLabelSize, text);
                glyph.HorizontalAlignment = HorizontalAlignment.Center;
                glyph.VerticalAlignment = VerticalAlignment.Center;
                LabelFontSize = Fit(c.Text, M.SmallLabelSize, available, M.S * 0.4);
                var caption = Label(c.Text, LabelFontSize, text, HorizontalAlignment.Center, VerticalAlignment.Center);
                caption.Margin = new Thickness(0, M.S * 0.04, 0, 0);
                var group = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = align == HorizontalAlignment.Center ? HorizontalAlignment.Center : align, VerticalAlignment = VerticalAlignment.Center };
                group.Children.Add(glyph);
                group.Children.Add(caption);
                ContentHost.Children.Add(group);
                break;

            case CapLayout.TextOnly:
                // Служебная подпись: единственный элемент — по центру области подписи (у модификаторов — уменьшенным шрифтом).
                if (item.IsModifier)
                {
                    LabelFontSize = Fit(c.Text, M.SmallLabelSize, available, M.S * 0.4);
                    ContentHost.Children.Add(Label(c.Text, LabelFontSize, text, HorizontalAlignment.Center, VerticalAlignment.Center));
                }
                else
                {
                    LabelFontSize = Fit(c.Text, M.S * 0.56, available, M.S * 0.4);
                    ContentHost.Children.Add(Label(c.Text, LabelFontSize, text, HorizontalAlignment.Center, VerticalAlignment.Center));
                }

                break;

            case CapLayout.MouseGlyph:
                var pressed = item.IsPressed ? c.MouseButton : MouseButtons.None;
                var unknown = item.IsUnknown ? c.MouseButton : MouseButtons.None;
                ContentHost.Children.Add(Center(MouseIcon.Create(M.S * 0.85, s.MouseLook with { Renderer = MouseRenderer.Outline, BodyColor = null },
                    new MouseIconState(pressed, unknown, 0, 0), outlineOverride: s.TextColor)));
                break;
        }
    }

    /// <summary>
    /// Компактность: подпись ужимается до <paramref name="min"/>, если иначе не помещается в доступную ширину грани
    /// («Return», «Option» не раздувают ряд). Подпись не обрезается: если не хватает и минимального размера, кейкап шире.
    /// </summary>
    protected static double Fit(string label, double preferred, double available, double min)
    {
        if (available <= 0 || string.IsNullOrEmpty(label))
        {
            return preferred;
        }

        double width = MeasureWidth(label, preferred);
        return width <= available || width <= 0 ? preferred : Math.Max(min, preferred * available / width);
    }

    public static double MeasureWidth(string label, double size)
    {
        var ft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(VisualFonts.Keycap, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Brushes.Black, 1.0);
        return ft.WidthIncludingTrailingWhitespace;
    }

    protected static FrameworkElement Center(FrameworkElement e)
    {
        e.HorizontalAlignment = HorizontalAlignment.Center;
        e.VerticalAlignment = VerticalAlignment.Center;
        return e;
    }

    protected static TextBlock Label(string s, double size, Brush brush, HorizontalAlignment h, VerticalAlignment v) => new()
    {
        Text = s,
        FontFamily = VisualFonts.Keycap,
        FontSize = size,
        Foreground = brush,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        LineHeight = size * 1.2,
        TextWrapping = TextWrapping.NoWrap,
        HorizontalAlignment = h,
        VerticalAlignment = v,
    };

    protected static void Detach(FrameworkElement e)
    {
        switch (e.Parent)
        {
            case Decorator d:
                d.Child = null;
                break;
            case Panel p:
                p.Children.Remove(e);
                break;
        }
    }
}

/// <summary>Создание кейкапа по рендеру.</summary>
public static class KeycapFactory
{
    public static KeycapControl Create(KeycapRenderer renderer) => renderer switch
    {
        KeycapRenderer.Laptop => new LaptopKeycap(),
        KeycapRenderer.Pbt => new PbtKeycap(),
        KeycapRenderer.Minimal => new MinimalKeycap(),
        KeycapRenderer.Volumetric => new VolumetricKeycap(),
        KeycapRenderer.Neon => new NeonKeycap(),
        _ => new LowProfileKeycap(),
    };
}

/// <summary>
/// lowprofile (Phase 2A ST-02/ST-03): нижняя «юбка» вторичного цвета и верхняя грань основного цвета, при нажатии грань
/// смещается вниз на S·0.25. Рамка — внутри размера (как border-box в CSS).
/// </summary>
public sealed class LowProfileKeycap : KeycapControl
{
    private readonly Border _skirt = new() { VerticalAlignment = VerticalAlignment.Bottom };
    private readonly Border _face = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly TranslateTransform _press = new();

    public LowProfileKeycap()
    {
        _face.RenderTransform = _press;
        _face.Child = ContentHost;
        Marker.RenderTransform = _press;
        Children.Add(_skirt);
        Children.Add(_face);
        Children.Add(Marker);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.LowProfile;

    protected override FrameworkElement LabelFace => _face;

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        var border = s.BorderEnabled ? s.BorderColor.ToFrozenBrush() : null;
        var thickness = new Thickness(BorderWidth);
        var radius = new CornerRadius(M.CornerRadius);
        Height = M.Height;
        MinWidth = item.IsModifier ? M.ModifierMinWidth : M.MinWidth;
        foreach (var b in new[] { _skirt, _face })
        {
            b.Height = M.FaceHeight;
            b.CornerRadius = radius;
            b.BorderThickness = thickness;
            b.BorderBrush = border;
        }

        _skirt.Background = s.Secondary.ToFrozenBrush();
        _face.Background = s.Primary.ToFrozenBrush();
        _face.Padding = new Thickness(M.PaddingX, M.PaddingY, M.PaddingX, M.PaddingY);
        Marker.Margin = new Thickness((M.S * 0.3) + BorderWidth, (M.S * 0.18) + BorderWidth, 0, 0);
    }

    protected override void OnPressChanged(double offset) => _press.Y = offset;
}

/// <summary>
/// laptop (Phase 2A ST-04): плоская клавиша высотой 2.25·S с вертикальным градиентом (светлее сверху, при «градиенте»),
/// внутренним бликом сверху и внешней тенью; при нажатии блик сменяется внутренней тенью, подпись чуть смещается.
/// Рамка — снаружи (как spread-тень). Тень — смещённая подложка без размытия (дешевле DropShadowEffect).
/// </summary>
public sealed class LaptopKeycap : KeycapControl
{
    private readonly Border _shadow = new();
    private readonly Border _frame = new();
    private readonly Border _body = new();
    private readonly Border _shine = new() { IsHitTestVisible = false };
    private readonly Border _inset = new() { IsHitTestVisible = false };
    private readonly Grid _bodyGrid = new();
    private readonly TranslateTransform _contentShift = new();
    private readonly TranslateTransform _shadowShift = new();

    public LaptopKeycap()
    {
        _shadow.RenderTransform = _shadowShift;
        ContentHost.RenderTransform = _contentShift;
        Marker.RenderTransform = _contentShift;
        _bodyGrid.Children.Add(_shine);
        _bodyGrid.Children.Add(_inset);
        _bodyGrid.Children.Add(ContentHost);
        _body.Child = _bodyGrid;
        _frame.Child = _body;
        Children.Add(_shadow);
        Children.Add(_frame);
        Children.Add(Marker);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.Laptop;

    protected override FrameworkElement LabelFace => _body;

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        double bw = BorderWidth;
        Height = M.Height; // рамка снаружи грани, но внутри высоты кейкапа: размещение ряда считает ту же высоту
        MinWidth = (item.IsModifier ? M.ModifierMinWidth : M.MinWidth) + (2 * bw);
        double r = M.CornerRadius;
        _frame.Margin = new Thickness(0, 0, 0, Math.Max(bw, M.S * 0.1)); // поле вмещает сдвиг тени: она не обрезается
        _frame.CornerRadius = new CornerRadius(r + bw);
        _frame.Padding = new Thickness(bw);
        _frame.Background = s.BorderEnabled ? s.BorderColor.ToFrozenBrush() : Brushes.Transparent;
        _shadow.CornerRadius = _frame.CornerRadius;
        _shadow.Margin = _frame.Margin;
        _shadow.Background = new ThemeColor(0, 0, 0, 0x55).ToFrozenBrush();
        _shadowShift.Y = M.S * 0.1;

        _body.CornerRadius = new CornerRadius(r);
        _body.Background = s.UseGradient
            ? Freeze(new LinearGradientBrush(ColorMath.Lighten(s.Primary, 0.1).ToWpf(), s.Primary.ToWpf(), 90))
            : s.Primary.ToFrozenBrush();
        _body.Padding = new Thickness(M.PaddingX, M.PaddingY, M.PaddingX, M.PaddingY);
        var light = ColorMath.WithAlpha(ColorMath.Lighten(s.Primary, 0.2), 0.9);
        var dark = ColorMath.WithAlpha(ColorMath.Darken(s.Primary, 0.25), 0.85);
        _shine.Margin = _inset.Margin = new Thickness(-M.PaddingX, -M.PaddingY, -M.PaddingX, -M.PaddingY);
        _shine.CornerRadius = _inset.CornerRadius = new CornerRadius(r);
        _shine.Background = Freeze(Fade(light, 0.12));
        _inset.Background = Freeze(Fade(dark, 0.22));
        Marker.Margin = new Thickness((M.S * 0.3) + bw, (M.S * 0.18) + bw, 0, 0);
    }

    protected override void OnPressChanged(double offset)
    {
        double f = PressFraction;
        _shine.Opacity = 1 - f;
        _inset.Opacity = f;
        _contentShift.Y = offset;
        _shadowShift.Y = M.S * (0.1 - (0.05 * f));
    }

    private static LinearGradientBrush Fade(ThemeColor c, double until) => new(
        new GradientStopCollection { new(c.ToWpf(), 0), new((c with { A = 0 }).ToWpf(), until) }, new Point(0, 0), new Point(0, 1));

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }
}

/// <summary>
/// pbt (Phase 2A ST-05): высокий корпус 2.75·S с градиентом «вниз-вправо» от вторичного цвета к затемнённому и вставленная
/// верхняя часть 2.2·S с отступами 0.3·S по бокам, градиентом слева направо и нижней кромкой основного цвета.
/// Нажатие опускает верхнюю часть на 0.15·S. Рамка — снаружи корпуса.
/// </summary>
public sealed class PbtKeycap : KeycapControl
{
    private readonly Border _body = new();
    private readonly Border _top = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly TranslateTransform _press = new();

    public PbtKeycap()
    {
        _top.RenderTransform = _press;
        Marker.RenderTransform = _press;
        _top.Child = ContentHost;
        Children.Add(_body);
        Children.Add(_top);
        Children.Add(Marker);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.Pbt;

    protected override FrameworkElement LabelFace => _top;

    protected override double ContentWidth(KeycapItem item) => M.ModifierMinWidth - (M.S * 0.6) - (2 * M.S * 0.35) - (2 * BorderWidth);

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        double bw = BorderWidth;
        Height = M.Height;
        MinWidth = item.IsModifier ? M.ModifierMinWidth : M.MinWidth;
        double r = M.CornerRadius;
        _body.CornerRadius = new CornerRadius(r);
        _body.BorderThickness = new Thickness(bw);
        _body.BorderBrush = s.BorderEnabled ? s.BorderColor.ToFrozenBrush() : null;
        var sec = s.Secondary;
        _body.Background = s.UseGradient
            ? Frozen(new LinearGradientBrush(sec.ToWpf(), ColorMath.Darken(sec, 0.2).ToWpf(), new Point(0, 0), new Point(1, 1)))
            : sec.ToFrozenBrush();

        _top.Height = M.FaceHeight - (M.S * 0.12);
        _top.MinWidth = M.S * 2;
        _top.Margin = new Thickness((M.S * 0.3) + bw, (M.S * 0.12) + bw, (M.S * 0.3) + bw, 0);
        _top.CornerRadius = new CornerRadius(Math.Max(0, r - (M.S * 0.15)));
        _top.BorderThickness = new Thickness(0, 0, 0, M.S * 0.06);
        _top.BorderBrush = s.Primary.ToFrozenBrush();
        _top.Background = s.UseGradient
            ? Frozen(new LinearGradientBrush(ColorMath.Darken(s.Primary, 0.1).ToWpf(), s.Primary.ToWpf(), 0))
            : s.Primary.ToFrozenBrush();
        _top.Padding = new Thickness(M.S * 0.35, M.S * 0.25, M.S * 0.35, M.S * 0.25);
        Marker.Margin = new Thickness((M.S * 0.5) + bw, (M.S * 0.25) + bw, 0, 0);
    }

    protected override void OnPressChanged(double offset) => _press.Y = offset;

    private static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }
}

/// <summary>
/// minimal (Phase 2A ST-06): только подпись или символ, без фона кейкапа, рамки и бейджа; символ размером S; при нажатии —
/// масштаб 1 → 0.95. Подложка группы, если включена, остаётся.
/// </summary>
public sealed class MinimalKeycap : KeycapControl
{
    private readonly ScaleTransform _scale = new();

    public MinimalKeycap()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _scale;
        Children.Add(ContentHost);
        Children.Add(Marker);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.Minimal;

    protected override FrameworkElement LabelFace => this;

    protected override bool SupportsBadge => false;

    protected override double ContentWidth(KeycapItem item) => 0; // без ужатия: ширина по подписи

    protected override HorizontalAlignment SmallAlign(KeycapItem item) => HorizontalAlignment.Center;

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        Height = M.Height;
        // Две подписи: ширина не меньше высоты, иначе дополнительной подписи некуда встать рядом с основной.
        MinWidth = item.Secondary is not null ? Math.Max(M.MinWidth, M.Height) : M.MinWidth;
        ContentHost.Margin = new Thickness(M.S * 0.1, M.S * 0.15, M.S * 0.1, M.S * 0.15);
        Marker.Margin = new Thickness(0);
        Marker.Visibility = Visibility.Collapsed;
    }

    protected override void AfterApply(AppearanceSettings s, KeycapItem item) => Marker.Visibility = Visibility.Collapsed;

    protected override void OnPressChanged(double offset)
    {
        double k = 1 - (0.05 * PressFraction);
        _scale.ScaleX = _scale.ScaleY = k;
    }
}
