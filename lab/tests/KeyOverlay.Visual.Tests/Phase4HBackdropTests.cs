using System.Windows;
using System.Windows.Controls;
using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Styles;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4H, п. 7: материал подложки кейкапов и мыши — сохранение, честная замена недоступного, неизменная геометрия.</summary>
public class Phase4HBackdropTests
{
    [Fact]
    public void Backdrop_round_trips_in_profile_and_default_fill_is_not_written()
    {
        var s = LabSettings.Default with
        {
            Appearance = LabSettings.Default.Appearance with
            {
                Backdrop = BackdropMaterial.Clear,
                Mouse = LabSettings.Default.Appearance.MouseLook with { Plate = BackdropMaterial.Clear },
            },
        };
        var back = SettingsSerializer.Read(SettingsSerializer.Write(s));
        Assert.Empty(back.Problems);
        Assert.Equal(s, back.Settings);
        Assert.DoesNotContain("\"material\"", SettingsSerializer.Write(LabSettings.Default));
        Assert.DoesNotContain("indicatorMaterial", SettingsSerializer.Write(LabSettings.Default));
    }

    [Fact]
    public void Backdrop_is_part_of_the_keycap_style()
    {
        var look = AppearanceSettings.Default with { Backdrop = BackdropMaterial.Clear };
        Assert.Equal(BackdropMaterial.Clear, StyleOps.ApplyKeycap(AppearanceSettings.Default, look).Backdrop);
        Assert.Contains("\"material\":\"clear\"", SettingsSerializer.KeycapLookToJson(look).ToJsonString());
    }

    [Theory]
    [InlineData(BackdropMaterial.Acrylic)]
    [InlineData(BackdropMaterial.Mica)]
    public void Unavailable_overlay_materials_fall_back_to_fill_honestly(BackdropMaterial m)
    {
        Assert.False(BackdropMaterials.IsAvailable(m));
        Assert.Equal(BackdropMaterial.Fill, BackdropMaterials.Effective(m));
        var (fill, outline, width) = BackdropMaterials.Paint(m, new ThemeColor(0x10, 0x20, 0x30, 0x80));
        Assert.NotNull(fill);
        Assert.Null(outline);
        Assert.Equal(0, width);
    }

    [Fact]
    public void Clear_backdrop_keeps_group_geometry_and_has_no_fill()
    {
        Sta.Run(() =>
        {
            Size Measure(BackdropMaterial m)
            {
                var p = new OverlayPresenter(new FakeClock(), useRenderingLoop: false);
                p.Apply(AppearanceSettings.Default with { Backdrop = m, Animation = AnimationKind.None });
                p.Update([new KeycapItem(1, 0x11, "Ctrl", true, true, KeySide.Left), new KeycapItem(2, 0x4B, "K", false, true)]);
                p.Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var b = (Border)p.Root;
                if (m == BackdropMaterial.Clear)
                {
                    Assert.Null(b.Background);
                    Assert.NotNull(b.BorderBrush);
                }

                Assert.False(b.IsHitTestVisible);
                return p.Root.DesiredSize;
            }

            Assert.Equal(Measure(BackdropMaterial.Fill), Measure(BackdropMaterial.Clear));
        });
    }
}
