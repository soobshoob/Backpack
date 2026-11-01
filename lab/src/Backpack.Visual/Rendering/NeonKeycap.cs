using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Rendering;

/// <summary>
/// neon (Phase 4I, «Backpack Neon Emerald»): почти чёрная плоская грань (основной цвет), тонкая рамка цвета рамки и мягкое
/// внешнее свечение того же цвета. Свечение — отдельный слой за гранью (рамка с <see cref="DropShadowEffect"/>), поэтому
/// подписи, метки R/num и счётчик не размываются, а непрозрачная грань закрывает внутреннюю часть свечения. Нажатие: рамка
/// ярче, свечение сильнее, грань получает умеренную заливку вторичным цветом (до 22 %) и опускается на 0,1·S;
/// отпускание возвращает всё плавно — по той же анимации хода, что у остальных рендеров (без постоянного пульсирования).
/// Интенсивность свечения — <see cref="AppearanceSettings.Glow"/> (0 — без свечения); вынос свечения учтён в
/// <see cref="KeycapMetrics.OuterMargin"/>, чтобы край окна оверлея его не обрезал.
/// </summary>
public sealed class NeonKeycap : KeycapControl
{
    private readonly Border _halo = new() { VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly Border _face = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly TranslateTransform _press = new();
    private readonly DropShadowEffect _glow = new() { ShadowDepth = 0, RenderingBias = RenderingBias.Quality };
    private Color _faceColor, _fillColor, _borderColor, _borderPressed;

    public NeonKeycap()
    {
        _face.RenderTransform = _press;
        _halo.RenderTransform = _press;
        _face.Child = ContentHost;
        Marker.RenderTransform = _press;
        Children.Add(_halo);
        Children.Add(_face);
        Children.Add(Marker);
    }

    public override KeycapRenderer Renderer => KeycapRenderer.Neon;

    protected override FrameworkElement LabelFace => _face;

    /// <summary>Свечение включено (для проверок).</summary>
    public bool GlowVisible => _halo.Visibility == Visibility.Visible && _halo.Effect is not null;

    /// <summary>Текущий радиус размытия свечения (для проверок нажатия).</summary>
    public double GlowRadius => _glow.BlurRadius;

    protected override void ApplyShape(AppearanceSettings s, KeycapItem item)
    {
        Height = M.Height;
        MinWidth = item.IsModifier ? M.ModifierMinWidth : M.MinWidth;
        var radius = new CornerRadius(M.CornerRadius);
        foreach (var b in new[] { _halo, _face })
        {
            b.Height = M.FaceHeight;
            b.CornerRadius = radius;
        }

        _faceColor = s.Primary.ToWpf();
        _fillColor = s.Secondary.ToWpf();
        _borderColor = s.BorderColor.ToWpf();
        _borderPressed = ColorMath.Lighten(s.BorderColor, 0.35).ToWpf();
        _face.Padding = new Thickness(M.PaddingX, M.PaddingY, M.PaddingX, M.PaddingY);
        _halo.Background = null;
        bool glow = s.Glow > 0 && s.BorderEnabled;
        _halo.Visibility = glow ? Visibility.Visible : Visibility.Collapsed;
        _halo.Effect = glow ? _glow : null;
        _glow.Color = _borderColor;
        Marker.Margin = new Thickness((M.S * 0.3) + BorderWidth, (M.S * 0.18) + BorderWidth, 0, 0);
    }

    protected override void OnPressChanged(double offset)
    {
        _press.Y = offset;
        double f = PressFraction;
        _face.Background = new SolidColorBrush(Mix(_faceColor, _fillColor, 0.22 * f));
        if (S.BorderEnabled)
        {
            _face.BorderBrush = new SolidColorBrush(Mix(_borderColor, _borderPressed, f));
            _face.BorderThickness = new Thickness(BorderWidth); // толщина постоянна: нажатие не сдвигает подписи (ярче — цветом и свечением)
            _halo.BorderBrush = _face.BorderBrush;
            _halo.BorderThickness = _face.BorderThickness;
        }
        else
        {
            _face.BorderBrush = null;
            _face.BorderThickness = default;
        }

        double g = Math.Clamp(S.Glow, 0, 1);
        _glow.BlurRadius = M.GlowBlur(f);
        _glow.Opacity = Math.Clamp(g * (1.0 + (0.6 * f)), 0, 1);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte L(byte x, byte y) => (byte)Math.Round(x + ((y - x) * t));
        return Color.FromArgb(L(a.A, b.A), L(a.R, b.R), L(a.G, b.G), L(a.B, b.B));
    }
}
