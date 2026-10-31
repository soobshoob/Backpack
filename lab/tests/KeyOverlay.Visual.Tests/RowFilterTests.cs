using KeyOverlay.Visual.Rendering;
using KeyOverlay.Visual.Settings;
using static KeyOverlay.Visual.Tests.OverlayPresenterTests;

namespace KeyOverlay.Visual.Tests;

public class RowFilterTests
{
    private static RowFilter Make(KeyFilter mode, bool mouse = true)
    {
        var f = new RowFilter();
        f.Configure(mode, mouse, []);
        return f;
    }

    [Fact]
    public void CombosOnly_HidesLoneModifierAndTyping_ShowsModifierPlusKey()
    {
        var f = Make(KeyFilter.CombosOnly);
        Assert.Empty(f.Apply([M(1, 1, "Ctrl")]));                          // одиночный модификатор
        Assert.Empty(f.Apply([K(2, 2, "A", pressed: true)]));             // обычная печать
        var shown = f.Apply([M(1, 1, "Ctrl"), K(3, 3, "C", pressed: true)]);
        Assert.Equal(["Ctrl", "C"], shown.Select(i => i.Key));
        Assert.Equal(2, f.Apply([M(1, 1, "Shift"), K(4, 4, "A", pressed: true)]).Count); // Shift + буква — тоже сочетание
    }

    [Fact]
    public void CombosOnly_HeldModifierStillFormsNextChord_AndReleaseRemovesIt()
    {
        var f = Make(KeyFilter.CombosOnly);
        Assert.Empty(f.Apply([M(1, 1, "Ctrl")]));                          // Ctrl удерживается — не показан, но не забыт моделью
        Assert.Equal(2, f.Apply([M(1, 1, "Ctrl"), K(2, 2, "S", pressed: true)]).Count);
        Assert.Equal(2, f.Apply([M(1, 1, "Ctrl", false), K(2, 2, "S")]).Count); // отпущено, ещё в выдержке
        Assert.Empty(f.Apply([M(1, 1, "Ctrl", false)]));                     // S истекла — одиночный Ctrl не показывается
        Assert.Empty(f.Apply([]));
    }

    [Fact]
    public void MouseInRow_Off_RemovesMouse_EvenFromCombos()
    {
        var f = Make(KeyFilter.All, mouse: false);
        Assert.Empty(f.Apply([K(1, 1, "MouseLeft", pressed: true)]));
        var combos = Make(KeyFilter.CombosOnly, mouse: false);
        Assert.Empty(combos.Apply([M(1, 1, "Ctrl"), K(2, 2, "MouseLeft", pressed: true)]));
        var withMouse = Make(KeyFilter.CombosOnly, mouse: true);
        Assert.Equal(2, withMouse.Apply([M(1, 1, "Ctrl"), K(2, 2, "MouseLeft", pressed: true)]).Count); // Ctrl+клик
    }

    [Fact]
    public void SwitchingCombosToAll_DoesNotBringBackReleasedTyping()
    {
        var f = Make(KeyFilter.CombosOnly);
        IReadOnlyList<KeycapItem> model = [K(5, 5, "A")];                    // буква отпущена, ещё в выдержке
        Assert.Empty(f.Apply(model));
        f.Configure(KeyFilter.All, true, model);
        Assert.Empty(f.Apply(model));                                         // старое нажатие не возвращается
        Assert.Single(f.Apply([K(6, 6, "B", pressed: true)]));                // новое — показывается
        Assert.Empty(f.Suppressed);                                            // запись истекла — подавление снято
    }

    [Fact]
    public void SwitchingCombosToAll_ShowsHeldKeysImmediately()
    {
        var f = Make(KeyFilter.CombosOnly);
        IReadOnlyList<KeycapItem> model = [M(1, 1, "Ctrl")];
        Assert.Empty(f.Apply(model));
        f.Configure(KeyFilter.All, true, model);
        Assert.Single(f.Apply(model));                                        // удерживаемая клавиша видна сразу
    }

    [Fact]
    public void SwitchingAllToCombos_HidesTyping_NoStuckItems()
    {
        var f = Make(KeyFilter.All);
        IReadOnlyList<KeycapItem> model = [K(1, 1, "A", pressed: true)];
        Assert.Single(f.Apply(model));
        f.Configure(KeyFilter.CombosOnly, true, model);
        Assert.Empty(f.Apply(model));
        f.Configure(KeyFilter.All, true, model);                              // назад: клавиша нажата — видна
        Assert.Single(f.Apply(model));
    }

    [Fact]
    public void Presenter_WithFilterSwitch_LeavesNoStuckKeycaps()
    {
        Sta.Run(() =>
        {
            var c = new FakeClock();
            var p = new OverlayPresenter(c, useRenderingLoop: false);
            p.Apply(AppearanceSettings.Default with { Animation = AnimationKind.None });
            var f = Make(KeyFilter.All);
            IReadOnlyList<KeycapItem> model = [K(1, 1, "A", pressed: true)];
            p.Update(f.Apply(model));
            Assert.Single(p.Visuals);
            f.Configure(KeyFilter.CombosOnly, true, model);
            p.Update(f.Apply(model));
            Assert.Empty(p.Visuals);
            model = [];
            p.Update(f.Apply(model));
            f.Configure(KeyFilter.All, true, model);
            p.Update(f.Apply(model));
            Assert.Empty(p.Visuals);
        });
    }
}
