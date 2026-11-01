using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4I, п. 3: дополнительная подпись раскладки сохраняет настроенное место во всех состояниях кейкапа. Причина ошибки:
/// при смене состояния (нажата → отпущена) подписи пересоздаются, а место считалось только в ArrangeOverride кейкапа, который
/// WPF не вызывает, если размер кейкапа не изменился, — новая подпись оставалась в (0, 0) слоя, то есть у левого верхнего угла.
/// </summary>
public class Phase4ISecondaryLabelTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var renderer in Enum.GetValues<KeycapRenderer>())
        {
            foreach (var corner in Enum.GetValues<LabelCorner>())
            {
                foreach (bool animated in new[] { true, false })
                {
                    yield return [renderer, corner, animated];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Secondary_label_keeps_its_place_pressed_released_fading_and_repressed(KeycapRenderer renderer, LabelCorner corner, bool animated)
    {
        Sta.Run(() =>
        {
            var clock = new FakeClock();
            var p = new OverlayPresenter(clock, useRenderingLoop: false);
            var a = AppearanceSettings.Default with
            {
                Renderer = renderer,
                Size = 40,
                Animation = animated ? AnimationKind.Fade : AnimationKind.None,
                PressAnimation = animated,
                TwoLabels = TwoLabelLayout.Default with { Corner = corner, OffsetX = 0.12, OffsetY = -0.05, SecondaryScale = 0.5 },
            };
            p.Apply(a);
            var host = new Window { Content = p.Root, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                KeycapItem Item(bool pressed) => new(7, 0x25, "K", false, pressed, Secondary: "л");

                Vector Place(string state)
                {
                    host.UpdateLayout();
                    var k = p.Visuals.Single().Control;
                    var second = Descendants<TextBlock>(k.LabelArea).Single(t => (string?)t.Tag == "secondary");
                    var face = k.FaceRect();
                    var s = second.TransformToAncestor(k.LabelArea).TransformBounds(new Rect(second.RenderSize));
                    Assert.True(face.Contains(s), $"{renderer} {corner} {state}: second {s} outside face {face}");
                    return s.TopLeft - face.TopLeft; // относительно лицевой поверхности: грань может двигаться при нажатии
                }

                p.Update([Item(true)]);
                clock.Advance(500);
                p.Tick();
                var pressed = Place("pressed");

                clock.Advance(1000);
                p.Tick();
                var held = Place("held");

                p.Update([Item(false)]);
                clock.Advance(30);
                p.Tick();
                var releasing = Place("releasing");
                clock.Advance(500);
                p.Tick();
                var released = Place("released");

                p.Update([]); // выдержка кончилась — исчезновение
                clock.Advance(40);
                p.Tick();
                var fading = p.Visuals.Count == 1 ? Place("fading") : released;

                p.Update([Item(true)]); // повторное нажатие во время исчезновения
                clock.Advance(500);
                p.Tick();
                var repressed = Place("repressed");

                foreach (var (name, v) in new[] { ("held", held), ("releasing", releasing), ("released", released), ("fading", fading), ("repressed", repressed) })
                {
                    Assert.True((v - pressed).Length < 0.6, $"{renderer} {corner} animated={animated}: {name} {v} differs from pressed {pressed}");
                }
            }
            finally
            {
                host.Close();
                p.Dispose();
            }
        });
    }

    [Fact]
    public void Secondary_label_follows_settings_changed_while_shown()
    {
        Sta.Run(() =>
        {
            var p = new OverlayPresenter(new FakeClock(), useRenderingLoop: false);
            var a = AppearanceSettings.Default with { Renderer = KeycapRenderer.LowProfile, Size = 40, TwoLabels = TwoLabelLayout.Default with { Corner = LabelCorner.BottomRight } };
            p.Apply(a);
            var host = new Window { Content = p.Root, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                p.Update([new KeycapItem(1, 0x25, "K", false, false, Secondary: "л")]);
                Point Corner()
                {
                    host.UpdateLayout();
                    var k = p.Visuals.Single().Control;
                    var second = Descendants<TextBlock>(k.LabelArea).Single(t => (string?)t.Tag == "secondary");
                    var s = second.TransformToAncestor(k.LabelArea).TransformBounds(new Rect(second.RenderSize));
                    var f = k.FaceRect();
                    return new Point(s.Left + (s.Width / 2) - (f.Left + (f.Width / 2)), s.Top + (s.Height / 2) - (f.Top + (f.Height / 2)));
                }

                var before = Corner();
                Assert.True(before.X > 0 && before.Y > 0);
                p.Apply(a with { TwoLabels = TwoLabelLayout.Default with { Corner = LabelCorner.TopLeft } });
                var after = Corner();
                Assert.True(after.X < 0 && after.Y < 0, $"after {after}");
            }
            finally
            {
                host.Close();
                p.Dispose();
            }
        });
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(c))
            {
                yield return d;
            }
        }
    }
}
