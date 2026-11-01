using System.Windows;
using System.Windows.Controls;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

public class OverlayPresenterTests
{
    private static readonly AppearanceSettings Fade = AppearanceSettings.Default;           // fade, 250 мс
    private static readonly AppearanceSettings NoAnim = Fade with { Animation = AnimationKind.None };
    private static readonly RowOptions Calm = RowOptions.Default;                             // без следа (по умолчанию)
    private static readonly RowOptions Trail = RowOptions.Default with { Trail = TrailMode.FadeInPlace };

    internal static KeycapItem K(long id, int key, string name, bool pressed = false, int count = 1) =>
        new(id, key, name, false, pressed, PressCount: count);

    internal static KeycapItem M(long id, int key, string name, bool pressed = true) => new(id, key, name, true, pressed, KeySide.Left);

    private static (OverlayPresenter P, FakeClock C) Make(AppearanceSettings? s = null, RowOptions? o = null)
    {
        var c = new FakeClock();
        var p = new OverlayPresenter(c, useRenderingLoop: false);
        p.Apply(s ?? Fade, o ?? Calm);
        return (p, c);
    }

    // ── прежние гарантии ─────────────────────────────────────────────────────

    [Fact]
    public void Enter_FadesInOverDuration_WithEaseOutQuint()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([K(1, 10, "A", pressed: true)]);
            var v = p.Visuals.Single();
            Assert.Equal(KeycapPhase.Entering, v.Phase);
            Assert.Equal(0, v.Opacity);

            c.Advance(125);
            p.Tick();
            Assert.Equal(CubicBezier.EaseOutQuint.Evaluate(0.5), v.Opacity, 6);
            c.Advance(125);
            p.Tick();
            Assert.Equal(KeycapPhase.Visible, v.Phase);
            Assert.Equal(1, v.Opacity);
            Assert.False(p.IsAnimating);
        });
    }

    [Fact]
    public void SameId_UpdatesInPlace_WithoutRecreatingControl()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([K(1, 10, "A", pressed: true)]);
            var control = p.Visuals.Single().Control;
            c.Advance(300);
            p.Update([K(1, 10, "A", pressed: false)]);
            c.Advance(50);
            p.Update([K(1, 10, "A", pressed: true, count: 2)]);
            Assert.Same(control, p.Visuals.Single().Control);
            Assert.Single(p.Row.Children);
        });
    }

    [Fact]
    public void Press_MovesFaceDown8_In100ms_AndReleaseReturns()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make(NoAnim);
            p.Update([K(1, 10, "A", pressed: false)]);
            var v = p.Visuals.Single();
            Assert.Equal(0, v.Control.PressOffset);

            p.Update([K(1, 10, "A", pressed: true)]);
            c.Advance(50);
            p.Tick();
            Assert.Equal(8 * CubicBezier.EaseInOutExpo.Evaluate(0.5), v.Control.PressOffset, 6);
            c.Advance(50);
            p.Tick();
            Assert.Equal(8, v.Control.PressOffset);

            p.Update([K(1, 10, "A", pressed: false)]);
            c.Advance(100);
            p.Tick();
            Assert.Equal(0, v.Control.PressOffset);
            Assert.False(p.IsAnimating);
        });
    }

    [Fact]
    public void PressAnimationOff_MovesImmediately_EnterFadeUnaffected()
    {
        Sta.Run(() =>
        {
            var (p, _) = Make(Fade with { PressAnimation = false });
            p.Update([K(1, 10, "A", pressed: false)]);
            var v = p.Visuals.Single();
            p.Update([K(1, 10, "A", pressed: true)]);
            Assert.Equal(8, v.Control.PressOffset);   // сразу, без 100 мс
            Assert.Equal(KeycapPhase.Entering, v.Phase); // появление по-прежнему плавное
        });
    }

    [Fact]
    public void RepressDuringExit_RevivesSameControl_NoDuplicate_NoJump()
    {
        Sta.Run(() =>
        {
            foreach (var mode in new[] { Calm, Trail })
            {
                var (p, c) = Make(o: mode);
                p.Update([K(1, 10, "A")]);
                c.Advance(300);
                p.Tick();
                var control = p.Visuals.Single().Control;

                p.Update([]);                          // выдержка истекла — исчезновение
                c.Advance(100);
                p.Tick();
                double before = p.Visuals.Single().Opacity;
                Assert.InRange(before, 0.01, 0.99);

                p.Update([K(7, 10, "A", pressed: true)]); // новое появление той же клавиши, новый Id
                var v = p.Visuals.Single();
                Assert.Same(control, v.Control);
                Assert.Equal(7, v.Id);
                Assert.Equal(KeycapPhase.Entering, v.Phase);
                Assert.Equal(before, v.Opacity, 9);    // без скачка

                c.Advance(500);
                p.Tick();
                Assert.Equal(1, v.Opacity);
                Assert.Single(p.Visuals);
            }
        });
    }

    [Fact]
    public void StaleExitCompletion_IsIgnored_AfterRevive()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([K(1, 10, "A")]);
            c.Advance(300);
            p.Tick();
            p.Update([]);
            var v = p.Visuals.Single();
            int exitGeneration = v.Generation;

            c.Advance(100);
            p.Update([K(2, 10, "A")]);             // ожил
            Assert.False(p.TryCompleteExit(v, exitGeneration)); // устаревшее завершение не удаляет
            Assert.Single(p.Visuals);

            c.Advance(400);                        // время старого исчезновения давно прошло
            p.Tick();
            Assert.Single(p.Visuals);
            Assert.Equal(KeycapPhase.Visible, v.Phase);
        });
    }

    [Fact]
    public void DifferentKeys_HaveIndependentLifetimes()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make(o: Trail);
            p.Update([M(1, 10, "Ctrl"), K(2, 11, "C", pressed: true)]);
            c.Advance(300);
            p.Tick();
            p.Update([M(1, 10, "Ctrl")]);          // C истекла в модели, Ctrl держится
            c.Advance(260);
            p.Tick();
            var only = Assert.Single(p.Visuals);
            Assert.Equal(1, only.Id);
            Assert.Equal(1, only.Opacity);
        });
    }

    [Fact]
    public void ParameterChangeDuringAnimation_ContinuesWithoutJump()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([K(1, 10, "A", pressed: true)]);
            c.Advance(60);
            p.Tick();
            var v = p.Visuals.Single();
            var control = v.Control;
            double before = v.Opacity;

            p.Apply(Fade with { Size = 48, Primary = ThemeColor.Parse("#2F6FEB"), DurationMs = 1000, LabelSet = LabelSet.MacOS }, Calm);
            Assert.Same(control, v.Control);
            Assert.Equal(KeycapPhase.Entering, v.Phase);
            Assert.Equal(before, v.Opacity, 9);
            Assert.Equal(48 * 2.5, control.Height);
            Assert.Equal(48 * 0.25, control.PressOffset, 6); // ход нажатия пересчитан под новый размер

            c.Advance(10);
            p.Tick();
            Assert.InRange(v.Opacity, before, 1);
            c.Advance(1000);
            p.Tick();
            Assert.Equal(1, v.Opacity);
        });
    }

    [Fact]
    public void SwitchFadeToNone_FinishesTransitions_AndRemovesExiting()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make(o: Trail);
            p.Update([K(1, 10, "A")]);
            c.Advance(300);
            p.Tick();
            p.Update([K(2, 11, "B")]);             // со следом: A исчезает на месте, B появляется
            c.Advance(50);
            p.Tick();

            p.Apply(NoAnim, Trail);
            var v = Assert.Single(p.Visuals);
            Assert.Equal(2, v.Id);
            Assert.Equal(KeycapPhase.Visible, v.Phase);
            Assert.Equal(1, v.Opacity);
            Assert.False(p.IsAnimating);

            p.Update([]);                          // в режиме none — удаление сразу
            Assert.Empty(p.Visuals);
        });
    }

    [Fact]
    public void NoneMode_AppearsAtFullOpacityImmediately()
    {
        Sta.Run(() =>
        {
            var (p, _) = Make(NoAnim);
            p.Update([K(1, 10, "A", pressed: true)]);
            Assert.Equal(1, p.Visuals.Single().Opacity);
            Assert.Equal(KeycapPhase.Visible, p.Visuals.Single().Phase);
            Assert.Equal(8, p.Visuals.Single().Control.PressOffset); // нажатая клавиша сразу нажата
        });
    }

    [Fact]
    public void FadeInPlace_ExitingKeepSlots_OrderFollowsModel()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make(o: Trail);
            p.Update([K(1, 10, "A")]);
            c.Advance(300);
            p.Tick();
            p.Update([K(2, 11, "K", pressed: true)]);                          // A исчезает на месте
            p.Update([M(3, 12, "Ctrl"), K(2, 11, "K", pressed: true)]);        // Ctrl перед K
            Assert.Equal(new[] { "A", "Ctrl", "K" }, p.Visuals.Select(v => v.Item.Key));
            Assert.Equal(p.Visuals.Select(v => (object)v.Control), p.Row.Children.Cast<object>());
            c.Advance(250);
            p.Tick();
            Assert.Equal(new[] { "Ctrl", "K" }, p.Visuals.Select(v => v.Item.Key));
        });
    }

    [Fact]
    public void Badge_OnlyOnLastLiveKeycap_AndCanBeTurnedOff()
    {
        Sta.Run(() =>
        {
            var (p, _) = Make(NoAnim);
            p.Update([M(1, 10, "Ctrl"), K(2, 11, "K", pressed: true, count: 2)]);
            Assert.False(p.Visuals[0].Control.BadgeVisible);
            Assert.True(p.Visuals[1].Control.BadgeVisible);
            p.Apply(NoAnim with { ShowPressCount = false }, Calm);
            Assert.False(p.Visuals[1].Control.BadgeVisible);
        });
    }

    [Fact]
    public void ExitingVisuals_AreCapped_InTrailMode()
    {
        Sta.Run(() =>
        {
            var (p, _) = Make(o: Trail);
            for (int i = 0; i < 200; i++)
            {
                p.Update([K(i + 1, i, "A")]);       // каждое — новая клавиша, предыдущая уходит
            }

            Assert.True(p.Visuals.Count <= OverlayPresenter.MaxExiting + 1);
        });
    }

    [Fact]
    public void RenderingLoop_SubscribesOnlyWhileAnimating_AndDisposeReleases()
    {
        Sta.Run(() =>
        {
            int baseline = OverlayPresenter.ActiveRenderingSubscriptions;
            var c = new FakeClock();
            var p = new OverlayPresenter(c);
            p.Apply(Fade);
            Assert.Equal(baseline, OverlayPresenter.ActiveRenderingSubscriptions);
            p.Update([K(1, 10, "A")]);
            Assert.Equal(baseline + 1, OverlayPresenter.ActiveRenderingSubscriptions);
            p.Update([K(1, 10, "A")]);              // повторное обновление не добавляет подписку
            Assert.Equal(baseline + 1, OverlayPresenter.ActiveRenderingSubscriptions);
            p.Dispose();
            Assert.Equal(baseline, OverlayPresenter.ActiveRenderingSubscriptions);
            Assert.Throws<ObjectDisposedException>(() => p.Update([]));
        });
    }

    [Fact]
    public void GroupBackground_FadesWithKeycaps()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            Assert.Equal(Visibility.Collapsed, p.Root.Visibility);
            p.Update([K(1, 10, "A")]);
            c.Advance(125);
            p.Tick();
            Assert.Equal(p.Visuals.Single().Opacity, p.Root.Opacity, 9);
            c.Advance(200);
            p.Tick();
            p.Update([]);
            c.Advance(300);
            p.Tick();
            Assert.Equal(Visibility.Collapsed, p.Root.Visibility);
        });
    }

    // ── без следа: быстрая печать ────────────────────────────────────────────

    [Fact]
    public void Calm_SequentialTyping_ReplacesInPlace_RowNeverGrows()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            string text = "HELLO WORLD";
            for (int i = 0; i < text.Length; i++)
            {
                string key = text[i] == ' ' ? "Space" : text[i].ToString();
                p.Update([K(i + 1, 100 + text[i], key, pressed: true)]);   // новая буква — замена группы в модели
                Assert.Single(p.Visuals);                                   // прежняя не занимает место рядом
                Assert.Single(p.Row.Children);
                if (i > 0)
                {
                    Assert.Equal(1, p.Visuals[0].Opacity);                  // замена видна сразу, без мигания
                }

                c.Advance(40);
                p.Update([K(i + 1, 100 + text[i], key, pressed: false)]);
                c.Advance(60);
                p.Tick();
            }

            Assert.Equal("D", p.Visuals.Single().Item.Key);
        });
    }

    [Fact]
    public void Calm_DifferentLabelWidths_KeepKeycapCenterStable()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make(NoAnim);
            var host = new Grid { Width = 900, Height = 200 };
            p.Root.HorizontalAlignment = HorizontalAlignment.Center;
            p.Root.VerticalAlignment = VerticalAlignment.Center;
            host.Children.Add(p.Root);

            double? groupLeft = null, groupWidth = null;
            var centers = new List<double>();
            string[] keys = ["SC E1 1D", "I", "F12", "W", "Backspace", "M"]; // самая широкая подпись — первой
            for (int i = 0; i < keys.Length; i++)
            {
                p.Update([K(i + 1, 200 + i, keys[i], pressed: true)]);
                host.Measure(new Size(900, 200));
                host.Arrange(new Rect(0, 0, 900, 200));
                host.UpdateLayout();
                var k = p.Visuals.Single().Control;
                var r = k.TransformToAncestor(host).TransformBounds(new Rect(k.RenderSize));
                centers.Add(r.Left + (r.Width / 2));
                var g = p.Root.TransformToAncestor(host).TransformBounds(new Rect(p.Root.RenderSize));
                if (i == 0)
                {
                    Assert.True(k.ActualWidth > 85, "подпись должна быть шире минимальной ширины кейкапа");
                }
                else
                {
                    // После широкой подписи группа не сужается и не сдвигается.
                    groupLeft ??= g.Left;
                    groupWidth ??= g.Width;
                    Assert.Equal(groupLeft.Value, g.Left, 3);
                    Assert.Equal(groupWidth.Value, g.Width, 3);
                }
            }

            // Центр кейкапа не дрожит при смене узких и широких подписей.
            Assert.True(centers.Max() - centers.Min() < 0.5, string.Join(", ", centers));
        });
    }

    [Fact]
    public void Calm_StickyWidth_ResetsWhenGroupDisappears()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([K(1, 1, "Backspace")]);
            double wide = p.StickyWidth;
            p.Update([K(2, 2, "I")]);
            Assert.Equal(wide, p.StickyWidth);
            p.Update([]);
            c.Advance(300);
            p.Tick();
            Assert.Empty(p.Visuals);
            Assert.Equal(0, p.StickyWidth);
            p.Update([K(3, 3, "I")]);
            Assert.True(p.StickyWidth < wide);
        });
    }

    [Fact]
    public void Calm_ChordExtendsRow_ReleasedChordDoesNotAccumulateIntoNext()
    {
        Sta.Run(() =>
        {
            var (p, c) = Make();
            p.Update([M(1, 10, "Ctrl")]);
            c.Advance(300);
            p.Tick();
            p.Update([M(1, 10, "Ctrl"), K(2, 11, "C", pressed: true)]);       // сочетание расширяет ряд
            Assert.Equal(2, p.Visuals.Count);
            Assert.Equal(KeycapPhase.Entering, p.Visuals[1].Phase);          // добавление в сочетание — плавно
            p.Update([M(1, 10, "Ctrl", false), K(2, 11, "C")]);               // отпущено, ещё в выдержке
            p.Update([K(3, 12, "D", pressed: true)]);                         // следующее нажатие — новая группа в модели
            var only = Assert.Single(p.Visuals);
            Assert.Equal("D", only.Item.Key);
            Assert.Equal(1, only.Opacity);
        });
    }
}
