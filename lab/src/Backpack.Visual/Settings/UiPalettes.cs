namespace Backpack.Visual.Settings;

/// <summary>
/// Именованные цветовые темы окон Backpack. Независимы от стилей инструментов (кейкапы, мышь, индикатор, рамка «Фокуса»):
/// смена темы их не меняет. Светлая/тёмная/системная — отдельная настройка (<see cref="UiThemeMode"/>); у каждой темы есть
/// оба варианта. Значения — RGB 0xRRGGBB или ARGB 0xAARRGGBB (если старший байт ненулевой).
/// </summary>
public static class UiPalettes
{
    public const string DefaultId = "emerald";
    public const string ClassicId = "classic";

    /// <summary>Базовые токены Backpack Emerald из референса палитры (значения заданы текстом, не взяты с образца на снимке).</summary>
    public static class Emerald
    {
        public const uint Background = 0x111513;
        public const uint Card = 0x222825;
        public const uint Accent = 0x087F5B;
        public const uint Glow = 0x31C99A;
        public const uint Text = 0xF2F5F3;
    }

    public sealed record Palette(string Id, string NameKey, IReadOnlyDictionary<string, uint> Light, IReadOnlyDictionary<string, uint> Dark);

    private static readonly Palette[] All = [BuildEmerald(), BuildClassic(), BuildAccented("sapphire", "Theme.Sapphire", 0x2457C5, 0x1D47A3, 0x173A85, 0x6EA1FF, 0x101318, 0x1E232B),
        BuildAccented("amber", "Theme.Amber", 0x9A5B00, 0x7F4B00, 0x663C00, 0xF5B43C, 0x15130F, 0x27231D)];

    public static IReadOnlyList<Palette> List => All;

    public static bool Exists(string? id) => All.Any(p => p.Id == id);

    public static Palette Get(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];

    /// <summary>Темы, названные драгоценными камнями (у них есть свечение акцентов): изумруд, сапфир, янтарь.</summary>
    public static bool IsGem(string? id) => id is DefaultId or "sapphire" or "amber";

    /// <summary>
    /// Цвет контурного логотипа: в цветных темах — акцент (на тёмном фоне — светлый вариант акцента, чтобы контур читался);
    /// в «Классической» — чёрный на светлом и белый на тёмном фоне.
    /// </summary>
    public static uint LogoColor(string? id, bool dark) => Get(id).Id switch
    {
        DefaultId => dark ? Emerald.Glow : Emerald.Accent,
        "sapphire" => dark ? 0x6EA1FFu : 0x2457C5u,
        "amber" => dark ? 0xF5B43Cu : 0x9A5B00u,
        _ => dark ? 0xF2F2F4u : 0x111113u,
    };

    /// <summary>Общие нейтральные токены светлого варианта (поверх них — акцент темы).</summary>
    private static Dictionary<string, uint> LightBase() => new()
    {
        ["Ko.WindowBrush"] = 0xF6F7F6,
        ["Ko.SidebarBrush"] = 0xEEF0EF,
        ["Ko.CardBrush"] = 0xFFFFFF,
        ["Ko.ControlBrush"] = 0xFFFFFF,
        ["Ko.BorderBrush"] = 0xE1E4E2,
        ["Ko.ControlBorderBrush"] = 0xCDD2CF,
        ["Ko.HoverBrush"] = 0xE8EBE9,
        ["Ko.TextBrush"] = 0x111513,
        ["Ko.SecondaryTextBrush"] = 0x4E5752,
        ["Ko.MutedTextBrush"] = 0x6E7772,
        ["Ko.DisabledTextBrush"] = 0x9AA19D,
        ["Ko.TrackBrush"] = 0xDADFDC,
        ["Ko.ToggleOffBrush"] = 0xBFC6C2,
        ["Ko.ThumbBrush"] = 0xFFFFFF,
        ["Ko.ThumbBorderBrush"] = 0x8E9792,
        ["Ko.SegmentBackBrush"] = 0xECEFED,
        ["Ko.SegmentCheckedBrush"] = 0xFFFFFF,
        ["Ko.ErrorBrush"] = 0xB3261E,
        ["Ko.CodeBrush"] = 0xF0F2F1,
        ["Ko.HighlightBrush"] = 0xFFE880,
        ["Ko.ScrollThumbBrush"] = 0xBCC3BF,
        ["Ko.AccentTextBrush"] = 0xFFFFFF,
        ["Ko.DemoBrush"] = 0x0B0E0D,
    };

    private static Dictionary<string, uint> DarkBase(uint background, uint card) => new()
    {
        ["Ko.WindowBrush"] = background,
        ["Ko.SidebarBrush"] = Mix(background, 0x000000, 0.18),
        ["Ko.CardBrush"] = card,
        ["Ko.ControlBrush"] = Mix(card, 0xFFFFFF, 0.05),
        ["Ko.BorderBrush"] = Mix(card, 0xFFFFFF, 0.08),
        ["Ko.ControlBorderBrush"] = Mix(card, 0xFFFFFF, 0.16),
        ["Ko.HoverBrush"] = Mix(card, 0xFFFFFF, 0.07),
        ["Ko.TextBrush"] = 0xF2F5F3,
        ["Ko.SecondaryTextBrush"] = 0xB6BFBA,
        ["Ko.MutedTextBrush"] = 0x8E9792,
        ["Ko.DisabledTextBrush"] = 0x6B736F,
        ["Ko.TrackBrush"] = Mix(card, 0xFFFFFF, 0.14),
        ["Ko.ToggleOffBrush"] = Mix(card, 0xFFFFFF, 0.22),
        ["Ko.ThumbBrush"] = 0xF2F5F3,
        ["Ko.ThumbBorderBrush"] = 0x8E9792,
        ["Ko.SegmentBackBrush"] = Mix(background, card, 0.6),
        ["Ko.SegmentCheckedBrush"] = Mix(card, 0xFFFFFF, 0.12),
        ["Ko.ErrorBrush"] = 0xFF8A80,
        ["Ko.CodeBrush"] = Mix(card, 0xFFFFFF, 0.04),
        ["Ko.HighlightBrush"] = 0x6B5A12,
        ["Ko.ScrollThumbBrush"] = Mix(card, 0xFFFFFF, 0.25),
        ["Ko.AccentTextBrush"] = 0xFFFFFF,
        ["Ko.DemoBrush"] = 0x0B0E0D,
    };

    private static Palette BuildEmerald()
    {
        var light = LightBase();
        light["Ko.AccentBrush"] = Emerald.Accent;
        light["Ko.AccentHoverBrush"] = 0x066B4D;
        light["Ko.AccentPressedBrush"] = 0x055A40;
        light["Ko.SelectedBrush"] = 0xDDF2EA;
        light["Ko.SelectedBorderBrush"] = 0x7BC9AE;
        light["Ko.SliderFillBrush"] = Emerald.Accent;
        light["Ko.FocusBrush"] = 0x087F5B;
        light["Ko.LinkBrush"] = 0x066B4D;
        light["Ko.GlowBrush"] = Emerald.Glow;

        var dark = DarkBase(Emerald.Background, Emerald.Card);
        dark["Ko.AccentBrush"] = Emerald.Accent;
        dark["Ko.AccentHoverBrush"] = 0x066B4D;
        dark["Ko.AccentPressedBrush"] = 0x055A40;
        dark["Ko.SelectedBrush"] = 0x13372B;
        dark["Ko.SelectedBorderBrush"] = 0x2E7A5F;
        dark["Ko.SliderFillBrush"] = 0x1FA67C;
        dark["Ko.FocusBrush"] = Emerald.Glow;
        dark["Ko.LinkBrush"] = Emerald.Glow;
        dark["Ko.GlowBrush"] = Emerald.Glow;
        dark["Ko.TextBrush"] = Emerald.Text;
        return new Palette(DefaultId, "Theme.Emerald", light, dark);
    }

    /// <summary>Прежнее оформление Backpack 0.3 (нейтральное, чёрно-белый акцент) — для возврата после миграции.</summary>
    private static Palette BuildClassic()
    {
        var light = new Dictionary<string, uint>
        {
            ["Ko.WindowBrush"] = 0xFFFFFF, ["Ko.SidebarBrush"] = 0xF7F7F8, ["Ko.CardBrush"] = 0xFFFFFF, ["Ko.ControlBrush"] = 0xFFFFFF,
            ["Ko.BorderBrush"] = 0xE5E5E7, ["Ko.ControlBorderBrush"] = 0xD4D4D8, ["Ko.HoverBrush"] = 0xEFEFF1, ["Ko.SelectedBrush"] = 0xE7E7EA,
            ["Ko.TextBrush"] = 0x111113, ["Ko.SecondaryTextBrush"] = 0x5F5F66, ["Ko.MutedTextBrush"] = 0x8E8E95, ["Ko.AccentBrush"] = 0x111113,
            ["Ko.AccentHoverBrush"] = 0x3A3A40, ["Ko.AccentTextBrush"] = 0xFFFFFF, ["Ko.FocusBrush"] = 0x2F6FEB, ["Ko.TrackBrush"] = 0xE3E3E6,
            ["Ko.ToggleOffBrush"] = 0xC9C9CF, ["Ko.ThumbBrush"] = 0xFFFFFF, ["Ko.ThumbBorderBrush"] = 0x9A9AA1, ["Ko.SegmentBackBrush"] = 0xF0F0F2,
            ["Ko.SegmentCheckedBrush"] = 0xFFFFFF, ["Ko.ErrorBrush"] = 0xC42B1C, ["Ko.CodeBrush"] = 0xF3F3F5, ["Ko.LinkBrush"] = 0x1F5FD1,
            ["Ko.HighlightBrush"] = 0xFFE880, ["Ko.ScrollThumbBrush"] = 0xC4C4CA, ["Ko.AccentPressedBrush"] = 0x000000,
            ["Ko.SelectedBorderBrush"] = 0xE7E7EA, ["Ko.SliderFillBrush"] = 0x111113, ["Ko.GlowBrush"] = 0x6EA1FF,
            ["Ko.DisabledTextBrush"] = 0x8E8E95, ["Ko.DemoBrush"] = 0x16161A,
        };
        var dark = new Dictionary<string, uint>
        {
            ["Ko.WindowBrush"] = 0x1F1F21, ["Ko.SidebarBrush"] = 0x18181A, ["Ko.CardBrush"] = 0x2A2A2D, ["Ko.ControlBrush"] = 0x333337,
            ["Ko.BorderBrush"] = 0x3B3B40, ["Ko.ControlBorderBrush"] = 0x4A4A50, ["Ko.HoverBrush"] = 0x37373C, ["Ko.SelectedBrush"] = 0x404046,
            ["Ko.TextBrush"] = 0xECECEE, ["Ko.SecondaryTextBrush"] = 0xB4B4BB, ["Ko.MutedTextBrush"] = 0x8E8E95, ["Ko.AccentBrush"] = 0xECECEE,
            ["Ko.AccentHoverBrush"] = 0xC8C8CE, ["Ko.AccentTextBrush"] = 0x111113, ["Ko.FocusBrush"] = 0x6EA1FF, ["Ko.TrackBrush"] = 0x45454B,
            ["Ko.ToggleOffBrush"] = 0x55555C, ["Ko.ThumbBrush"] = 0xF2F2F4, ["Ko.ThumbBorderBrush"] = 0x8A8A92, ["Ko.SegmentBackBrush"] = 0x2A2A2D,
            ["Ko.SegmentCheckedBrush"] = 0x45454B, ["Ko.ErrorBrush"] = 0xFF7B70, ["Ko.CodeBrush"] = 0x2E2E33, ["Ko.LinkBrush"] = 0x7FB0FF,
            ["Ko.HighlightBrush"] = 0x6B5A12, ["Ko.ScrollThumbBrush"] = 0x5A5A61, ["Ko.AccentPressedBrush"] = 0xA8A8AE,
            ["Ko.SelectedBorderBrush"] = 0x404046, ["Ko.SliderFillBrush"] = 0xECECEE, ["Ko.GlowBrush"] = 0x6EA1FF,
            ["Ko.DisabledTextBrush"] = 0x6E6E75, ["Ko.DemoBrush"] = 0x111113,
        };
        return new Palette(ClassicId, "Theme.Classic", light, dark);
    }

    private static Palette BuildAccented(string id, string nameKey, uint accent, uint hover, uint pressed, uint glow, uint darkBg, uint darkCard)
    {
        var light = LightBase();
        var dark = DarkBase(darkBg, darkCard);
        foreach (var (d, isDark) in new[] { (light, false), (dark, true) })
        {
            d["Ko.AccentBrush"] = accent;
            d["Ko.AccentHoverBrush"] = hover;
            d["Ko.AccentPressedBrush"] = pressed;
            d["Ko.SelectedBrush"] = isDark ? Mix(darkCard, accent, 0.28) : Mix(0xFFFFFF, accent, 0.14);
            d["Ko.SelectedBorderBrush"] = isDark ? Mix(darkCard, accent, 0.65) : Mix(0xFFFFFF, accent, 0.45);
            d["Ko.SliderFillBrush"] = isDark ? Mix(accent, glow, 0.35) : accent;
            d["Ko.FocusBrush"] = isDark ? glow : accent;
            d["Ko.LinkBrush"] = isDark ? glow : hover;
            d["Ko.GlowBrush"] = glow;
        }

        return new Palette(id, nameKey, light, dark);
    }

    /// <summary>Непрозрачность рамок Neon: карточки и разделители спокойнее, поля заметнее, выбранное и фокус — ярче всего.</summary>
    public static class NeonEdges
    {
        public const byte Divider = 0x2E;
        public const byte Card = 0x40;
        public const byte Field = 0x66;
        public const byte FieldHover = 0x99;
        public const byte Selected = 0xCC;
    }

    /// <summary>
    /// Стиль Backpack Neon поверх палитры (Phase 4H). Тёмный: почти чёрная основа — самая тёмная из предлагаемых (окно ≈ 30 %
    /// яркости тёмной основы палитры), тонкие рамки цвета свечения палитры с иерархией <see cref="NeonEdges"/>. Светлый — свои
    /// цвета, не инверсия тёмного: холодная белая основа с лёгким оттенком свечения и рамки цвета акцента (свечение на белом
    /// не читается). Текстовые токены не меняются — текст остаётся резким и контрастным.
    /// </summary>
    public static void ApplyNeon(IDictionary<string, uint> v, bool dark)
    {
        uint glow = v["Ko.GlowBrush"], accent = v["Ko.AccentBrush"];
        static uint A(uint rgb, byte a) => ((uint)a << 24) | (rgb & 0xFFFFFF);
        if (dark)
        {
            uint bg = v["Ko.WindowBrush"] & 0xFFFFFF, card = Mix(v["Ko.CardBrush"] & 0xFFFFFF, 0x000000, 0.6);
            v["Ko.WindowBrush"] = Mix(bg, 0x000000, 0.7);
            v["Ko.SidebarBrush"] = Mix(bg, 0x000000, 0.8);
            v["Ko.CardBrush"] = card;
            v["Ko.ControlBrush"] = Mix(card, glow, 0.04);
            v["Ko.HoverBrush"] = Mix(card, glow, 0.10);
            v["Ko.SelectedBrush"] = Mix(card, glow, 0.14);
            v["Ko.SegmentBackBrush"] = Mix(Mix(bg, 0x000000, 0.7), card, 0.5);
            v["Ko.SegmentCheckedBrush"] = Mix(card, glow, 0.18);
            v["Ko.TrackBrush"] = Mix(card, 0xFFFFFF, 0.12);
            v["Ko.ToggleOffBrush"] = Mix(card, 0xFFFFFF, 0.22);
            v["Ko.CodeBrush"] = Mix(card, 0xFFFFFF, 0.05);
            v["Ko.ScrollThumbBrush"] = Mix(card, glow, 0.30);
            v["Ko.FocusBrush"] = glow;
            v["Ko.SliderFillBrush"] = glow;
            v["Ko.BorderBrush"] = A(glow, NeonEdges.Divider);
            v["Ko.CardBorderBrush"] = A(glow, NeonEdges.Card);
            v["Ko.ControlBorderBrush"] = A(glow, NeonEdges.Field);
            v["Ko.ThumbBorderBrush"] = A(glow, NeonEdges.FieldHover);
            v["Ko.SelectedBorderBrush"] = A(glow, NeonEdges.Selected);
        }
        else
        {
            v["Ko.WindowBrush"] = Mix(0xF7F9F8, glow, 0.035);
            v["Ko.SidebarBrush"] = Mix(0xEEF2F0, glow, 0.05);
            v["Ko.CardBrush"] = 0xFFFFFF;
            v["Ko.ControlBrush"] = 0xFFFFFF;
            v["Ko.HoverBrush"] = Mix(0xFFFFFF, accent, 0.07);
            v["Ko.SelectedBrush"] = Mix(0xFFFFFF, accent, 0.10);
            v["Ko.SegmentBackBrush"] = Mix(0xF3F6F5, glow, 0.05);
            v["Ko.SegmentCheckedBrush"] = 0xFFFFFF;
            v["Ko.FocusBrush"] = accent;
            v["Ko.BorderBrush"] = A(accent, 0x26);
            v["Ko.CardBorderBrush"] = A(accent, 0x38);
            v["Ko.ControlBorderBrush"] = A(accent, 0x5C);
            v["Ko.ThumbBorderBrush"] = A(accent, 0x8C);
            v["Ko.SelectedBorderBrush"] = A(accent, 0xB3);
        }
    }

    /// <summary>Линейное смешение двух RGB: t = 0 — <paramref name="a"/>, t = 1 — <paramref name="b"/>.</summary>
    public static uint Mix(uint a, uint b, double t)
    {
        static byte C(uint v, int shift) => (byte)((v >> shift) & 0xFF);
        byte M(int shift) => (byte)Math.Round(C(a, shift) + ((C(b, shift) - C(a, shift)) * t));
        return ((uint)M(16) << 16) | ((uint)M(8) << 8) | M(0);
    }

    /// <summary>Относительная яркость по WCAG 2.x (0…1).</summary>
    public static double Luminance(uint rgb)
    {
        static double Ch(uint v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Ch((rgb >> 16) & 0xFF)) + (0.7152 * Ch((rgb >> 8) & 0xFF)) + (0.0722 * Ch(rgb & 0xFF));
    }

    /// <summary>Контраст WCAG между двумя цветами (1…21).</summary>
    public static double Contrast(uint a, uint b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
