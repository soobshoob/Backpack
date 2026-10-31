using KeyOverlay.Visual.Rendering;

namespace KeyOverlay.Visual.Tests;

public class KeyLabelsTests
{
    private static KeyCapContent R(string key, LabelSet set = LabelSet.Windows, LabelMode mode = LabelMode.Mixed, bool modifier = false,
        KeySide side = KeySide.None, bool numpad = false, bool unknown = false, bool markers = true) =>
        KeyLabels.Resolve(new KeycapItem(1, 1, key, modifier, false, side, numpad, unknown), set, mode, markers);

    [Theory]
    [InlineData(LabelSet.Windows, "Win", "Win", Glyph.WindowsGrid)]
    [InlineData(LabelSet.MacOS, "Win", "Cmd", Glyph.Command)]
    [InlineData(LabelSet.Linux, "Win", "Super", Glyph.SuperDiamond)]
    [InlineData(LabelSet.Windows, "Alt", "Alt", Glyph.None)]
    [InlineData(LabelSet.MacOS, "Alt", "Opt", Glyph.Option)]
    [InlineData(LabelSet.MacOS, "Ctrl", "Ctrl", Glyph.Control)]
    [InlineData(LabelSet.Windows, "Enter", "Enter", Glyph.Enter)]
    [InlineData(LabelSet.MacOS, "Enter", "Return", Glyph.Return)]
    [InlineData(LabelSet.Windows, "Backspace", "Bksp", Glyph.Backspace)]
    [InlineData(LabelSet.MacOS, "Backspace", "Delete", Glyph.Backspace)] // в символьном режиме запасная подпись — однозначная
    [InlineData(LabelSet.Windows, "Delete", "Del", Glyph.None)]
    [InlineData(LabelSet.MacOS, "Delete", "Fwd Del", Glyph.ForwardDelete)]
    [InlineData(LabelSet.Linux, "Esc", "Esc", Glyph.None)]
    [InlineData(LabelSet.MacOS, "Esc", "Esc", Glyph.Escape)]
    public void Sets_HaveExplicitNamesAndGlyphs(LabelSet set, string key, string text, Glyph glyph)
    {
        var c = R(key, set, LabelMode.Symbol, modifier: KeyLabels.IsModifierKey(key));
        Assert.Equal(text, c.Text);
        if (glyph == Glyph.None)
        {
            Assert.Equal(CapLayout.TextOnly, c.Layout);   // нет символа → короткая подпись, а не пустое место
        }
        else
        {
            Assert.Equal(CapLayout.GlyphOnly, c.Layout);
            Assert.Equal(glyph, c.Glyph);
        }
    }

    [Fact]
    public void EveryKeyResolves_InEverySetAndMode_WithDrawableGlyph()
    {
        var keys = KeyLabels.SpecialKeys.Concat(KeyLabels.ArrowKeys).Concat(KeyLabels.MouseKeys).Concat(["A", "7", "F12", "-", "SC 7E"]);
        foreach (var key in keys)
        {
            foreach (var set in Enum.GetValues<LabelSet>())
            {
                foreach (var mode in Enum.GetValues<LabelMode>())
                {
                    var c = R(key, set, mode);
                    Assert.False(string.IsNullOrWhiteSpace(c.Text), $"{key} {set} {mode}");
                    Assert.True(c.Glyph == Glyph.None || KeyGlyphs.Has(c.Glyph), $"{key}: {c.Glyph}");
                    if (mode == LabelMode.Text && !KeyLabels.IsMouse(key))
                    {
                        Assert.Equal(Glyph.None, c.Glyph);  // текстовый режим — без символов
                    }
                }
            }
        }
    }

    [Fact]
    public void MacDeleteKeys_AreUnambiguousInTextMode()
    {
        Assert.Equal("Delete", R("Backspace", LabelSet.MacOS, LabelMode.Text).Text);
        Assert.Equal("Fwd Del", R("Delete", LabelSet.MacOS, LabelMode.Text).Text);
        Assert.NotEqual(R("Backspace", LabelSet.MacOS).Glyph, R("Delete", LabelSet.MacOS).Glyph); // в смешанном различаются символом
    }

    [Fact]
    public void Space_IsCompactGlyph_DeleteIsDel_ArrowsAreGlyphs()
    {
        Assert.Equal(CapLayout.GlyphOnly, R("Space").Layout);
        Assert.Equal(Glyph.Space, R("Space").Glyph);
        Assert.Equal("Del", R("Delete").Text);
        Assert.Equal(CapLayout.GlyphOnly, R("ArrowUp").Layout);
        Assert.Equal(CapLayout.TextOnly, R("ArrowUp", mode: LabelMode.Text).Layout);
        Assert.Equal(CapLayout.GlyphAndText, R("Shift", modifier: true).Layout);
        Assert.Equal(CapLayout.Character, R("A").Layout);
    }

    [Fact]
    public void Markers_DistinguishRightAndNumpad_WithoutBulkyLabels()
    {
        Assert.Null(R("Ctrl", modifier: true, side: KeySide.Left).Marker);
        Assert.Equal("R", R("Ctrl", modifier: true, side: KeySide.Right).Marker);
        Assert.Equal("num", R("Enter", numpad: true).Marker);
        Assert.Equal("num", R("7", numpad: true).Marker);
        Assert.Null(R("Enter").Marker);
        Assert.Equal("?", R("A", unknown: true).Marker);
        Assert.Null(R("Ctrl", modifier: true, side: KeySide.Right, markers: false).Marker);
        Assert.Equal(R("Enter").Text, R("Enter", numpad: true).Text); // одна подпись, различие — меткой
    }

    [Fact]
    public void Mouse_GraphicOrText()
    {
        var g = KeyLabels.Resolve(new KeycapItem(1, 1, "MouseRight", false, true), LabelSet.Windows, LabelMode.Mixed, true, mouseGraphic: true);
        Assert.Equal(CapLayout.MouseGlyph, g.Layout);
        Assert.Equal(MouseButtons.Right, g.MouseButton);
        var t = KeyLabels.Resolve(new KeycapItem(1, 1, "MouseX1", false, true), LabelSet.Windows, LabelMode.Mixed, true, mouseGraphic: false);
        Assert.Equal(CapLayout.TextOnly, t.Layout);
        Assert.Equal("Back", t.Text);
    }

    [Fact]
    public void Glyphs_AreOwnVectorGeometry_NotFontCharacters()
    {
        foreach (var g in Enum.GetValues<Glyph>().Where(KeyGlyphs.Has))
        {
            var geometry = KeyGlyphs.StrokeGeometry(g);
            Assert.False(geometry.IsEmpty(), g.ToString());
            Assert.InRange(geometry.Bounds.Left, -0.5, 24.5);
            Assert.InRange(geometry.Bounds.Right, -0.5, 24.5);
            Assert.InRange(geometry.Bounds.Top, -0.5, 24.5);
            Assert.InRange(geometry.Bounds.Bottom, -0.5, 24.5);
        }
    }
}
