using System.Windows;
using Backpack.Visual.Layout;
using Backpack.Visual.Mouse;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

public class MouseAndIndicatorTests
{
    private sealed class FakeProbe : ILayoutProbe
    {
        public LayoutSample Next { get; set; }

        public LayoutSample Sample() => Next;

        public IReadOnlyList<nint> InstalledLayouts() => [];
    }

    [Fact]
    public void CursorVisual_IconAndCircle_AreIndependent_OriginIsPointer()
    {
        Sta.Run(() =>
        {
            var v = new MouseCursorVisual();
            var look = MouseVisual.Default;
            v.Apply(MouseCursorSettings.Default with { Enabled = true, ShowCircle = true, ShowIcon = false }, look, default);
            Assert.False(v.CircleShown);                                       // Phase 4E: круг щелчка — только при нажатии
            Assert.Equal(look.ClickSize / 2 + 1, v.Origin.X, 6);              // место под круг есть всегда: окно не «прыгает»
            v.Apply(MouseCursorSettings.Default with { Enabled = true, ShowCircle = true, ShowIcon = false }, look, new MouseButtonState(MouseButtons.Left, MouseButtons.None));
            Assert.True(v.CircleShown);
            Assert.False(v.IconShown);
            Assert.Equal(look.ClickSize / 2 + 1, v.Origin.X, 6);              // круг центрирован на острие указателя
            v.Apply(MouseCursorSettings.Default with { Enabled = true, ShowCircle = false, ShowIcon = false, ShowHighlight = true, HighlightSize = 100 }, look, default);
            Assert.True(v.HighlightShown);                                     // подсветка курсора — постоянно
            Assert.Equal(51, v.Origin.X, 6);

            v.Apply(MouseCursorSettings.Default with { Enabled = true, ShowCircle = false, ShowIcon = true, OffsetX = 22, OffsetY = 22 }, look, default);
            Assert.False(v.CircleShown);
            Assert.True(v.IconShown);
            Assert.Equal(1, v.Origin.X, 6);                                    // значок справа-снизу, острие — в углу

            v.Apply(MouseCursorSettings.Default with { Enabled = true, OffsetX = -60, OffsetY = -60 }, look, default);
            Assert.True(v.Origin.X > 50 && v.Origin.Y > 50);                   // отрицательное смещение — значок слева сверху
        });
    }

    [Fact]
    public void CursorVisual_PressedHighlighted_UnknownNotShownAsPressed()
    {
        Sta.Run(() =>
        {
            var v = new MouseCursorVisual();
            var s = MouseCursorSettings.Default with { Enabled = true };
            v.Apply(s, MouseVisual.Default, new MouseButtonState(MouseButtons.Left, MouseButtons.None));
            Assert.Equal(MouseButtons.Left, v.State.Pressed);
            v.Apply(s, MouseVisual.Default, new MouseButtonState(MouseButtons.None, MouseButtons.Right));
            Assert.Equal(MouseButtons.None, v.State.Pressed);                  // Unknown — не нажатие
            Assert.Equal(MouseButtons.Right, v.State.Unknown);
            v.Apply(s, MouseVisual.Default, default);                           // отпускание снимает подсветку
            Assert.Equal(default, v.State);
        });
    }

    [Fact]
    public void CursorController_RunsOnlyWhileEnabled_AndReleasesOnDispose()
    {
        Sta.Run(() =>
        {
            var state = new MouseButtonState(MouseButtons.Middle, MouseButtons.None);
            var c = new MouseCursorController(() => state) { ShowOnScreen = false };
            c.Apply(MouseCursorSettings.Default, MouseVisual.Default);         // по умолчанию выключено
            Assert.False(c.IsRunning);
            c.Apply(MouseCursorSettings.Default with { Enabled = true }, MouseVisual.Default);
            Assert.True(c.IsRunning);
            Assert.Equal(MouseButtons.Middle, c.Window!.Visual.State.Pressed);
            c.Apply(MouseCursorSettings.Default with { Enabled = true, ShowIcon = false, ShowCircle = false }, MouseVisual.Default);
            Assert.False(c.IsRunning);                                          // нечего показывать — таймер остановлен
            c.Apply(MouseCursorSettings.Default with { Enabled = true }, MouseVisual.Default);
            c.Dispose();
            Assert.False(c.IsRunning);
            c.Apply(MouseCursorSettings.Default with { Enabled = true }, MouseVisual.Default);
            Assert.False(c.IsRunning);                                          // после Dispose не оживает
        });
    }

    [Fact]
    public void DpiScaleAt_ReturnsPositiveScale_EvenForFarNegativePoint()
    {
        Assert.True(MouseCursorController.DpiScaleAt(-30000, -30000) >= 1); // MONITOR_DEFAULTTONEAREST
        Assert.True(MouseCursorController.DpiScaleAt(0, 0) >= 1);
    }

    private static LayoutIndicatorController Indicator(FakeClock clock) =>
        new(new FakeProbe(), () => new Rect(600, 900, 300, 100), () => new Rect(0, 0, 1920, 1040), clock) { ShowOnScreen = false };

    [Fact]
    public void Indicator_StyleChangeWhileShown_AppliesImmediately_WithoutExtendingHold()
    {
        Sta.Run(() =>
        {
            var clock = new FakeClock();
            var c = Indicator(clock);
            var none = AppearanceSettings.Default with { Animation = AnimationKind.None };
            c.Apply(LayoutIndicatorSettings.Default with { DurationMs = 1200 }, none);
            c.ShowLabel(new LayoutLabel("RU", null));
            Assert.True(c.IsVisible);
            Assert.Equal(TimeSpan.FromMilliseconds(1200), c.HideAt);
            Assert.Equal(36, c.Window!.Visual.CodeSize);                       // наследование: 1.125·S

            clock.Advance(500);
            c.Apply(LayoutIndicatorSettings.Default with { DurationMs = 1200, CustomStyle = true }, none with { Indicator = IndicatorVisual.Default with { TextSize = 60 } });
            Assert.Equal(60, c.Window.Visual.CodeSize);                        // новое оформление — сразу
            Assert.Equal(TimeSpan.FromMilliseconds(1200), c.HideAt);           // срок не продлён

            c.Apply(LayoutIndicatorSettings.Default with { DurationMs = 2000, CustomStyle = true }, none);
            Assert.Equal(TimeSpan.FromMilliseconds(2000), c.HideAt);           // от момента показа, не от момента изменения

            clock.Advance(1499);
            c.CheckHide();
            Assert.True(c.IsVisible);
            clock.Advance(2);
            c.CheckHide();
            Assert.False(c.IsVisible);
            c.Dispose();
        });
    }

    [Fact]
    public void Indicator_Position_AboveGroup_AndScreenAnchorWithOffsets()
    {
        Sta.Run(() =>
        {
            var c = Indicator(new FakeClock());
            c.Apply(LayoutIndicatorSettings.Default, AppearanceSettings.Default);
            var above = c.Position(new Size(100, 60));
            Assert.Equal(600 + 100, above.X, 6);                              // по центру группы шириной 300
            Assert.Equal(900 - 60 - 8, above.Y, 6);                            // над группой
            c.Apply(LayoutIndicatorSettings.Default with { OffsetX = 40, OffsetY = -20 }, AppearanceSettings.Default);
            Assert.Equal(new Point(740, 812), c.Position(new Size(100, 60)));
            c.Apply(LayoutIndicatorSettings.Default with { Position = IndicatorPosition.Screen, Anchor = ScreenAnchor.Center, OffsetX = 15, OffsetY = 25 }, AppearanceSettings.Default);
            Assert.Equal(new Point(((1920 - 100) / 2.0) + 15, ((1040 - 60) / 2.0) + 25), c.Position(new Size(100, 60)));
            c.Apply(LayoutIndicatorSettings.Default with { Position = IndicatorPosition.Screen, Anchor = ScreenAnchor.TopLeft, OffsetX = -5000 }, AppearanceSettings.Default);
            Assert.Equal(0, c.Position(new Size(100, 60)).X);                 // не выходит за рабочую область
            c.Dispose();
        });
    }

    [Fact]
    public void IndicatorLook_InheritsKeycapOrUsesCustom()
    {
        var a = AppearanceSettings.Default with { Size = 40, Primary = ThemeColor.Parse("#112233"), Animation = AnimationKind.None };
        var inherit = IndicatorLook.Resolve(a, LayoutIndicatorSettings.Default);
        Assert.Equal(45, inherit.TextSize);
        Assert.Equal(a.Primary, inherit.Background);
        Assert.Equal(AnimationKind.None, inherit.Animation);
        var custom = IndicatorLook.Resolve(a, LayoutIndicatorSettings.Default with { CustomStyle = true, Animation = IndicatorAnimation.Fade });
        Assert.Equal(IndicatorVisual.Default.TextSize, custom.TextSize);
        Assert.Equal(IndicatorVisual.Default.Background, custom.Background);
        Assert.Equal(AnimationKind.Fade, custom.Animation);
    }
}
