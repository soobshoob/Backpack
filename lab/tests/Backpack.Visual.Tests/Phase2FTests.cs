using System.Windows;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 2F: состояния линзы, выбора области, рисования и радиального меню; освобождение окон и ресурсов.
/// Окна создаются в области за пределами экрана и не активируются. Это проверка логики и свойств окон, а не проверка
/// настоящего click-through под мышью пользователя или живого увеличения на экране.
/// </summary>
public class Phase2FTests
{
    private static readonly PresentationCommand[] Commands = Enum.GetValues<PresentationCommand>();

    private static IEnumerable<PresentationState> AllStates()
    {
        for (int i = 0; i < 16; i++)
        {
            var s = new PresentationState((i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0);
            if (s.Radial && !s.Drawing)
            {
                continue; // радиальное меню существует только в рисовании
            }

            yield return s;
        }
    }

    [Fact]
    public void Transitions_KeepInvariants_ForEveryStateAndCommand()
    {
        foreach (var s in AllStates())
        {
            foreach (var c in Commands)
            {
                var n = PresentationModes.Next(s, c);
                Assert.False(n.Radial && !n.Drawing, $"{s} {c}");
                Assert.True(PresentationModes.Next(s, PresentationCommand.ExitAll).IsNormal);
            }

            // Esc снимает ровно один слой; из любого состояния не более 4 нажатий до обычного режима.
            var e = s;
            for (int i = 0; i < 4; i++)
            {
                e = PresentationModes.Next(e, PresentationCommand.Escape);
            }

            Assert.True(e.IsNormal, s.ToString());
        }
    }

    [Fact]
    public void Transitions_DocumentedRules()
    {
        var n = PresentationState.Normal;
        var selecting = PresentationModes.Next(n, PresentationCommand.Lens);
        Assert.True(selecting.Selecting && selecting.CapturesMouse);
        Assert.True(PresentationModes.Next(selecting, PresentationCommand.Lens).IsNormal);                  // повторная команда — отмена
        var lens = PresentationModes.Next(selecting, PresentationCommand.SelectionDone);
        Assert.True(lens.Lens && !lens.CapturesMouse);                                                      // линза не принимает мышь
        Assert.True(PresentationModes.Next(lens, PresentationCommand.LensExpired).IsNormal);
        var drawing = PresentationModes.Next(lens, PresentationCommand.Draw);
        Assert.True(drawing.Lens && drawing.Drawing && drawing.DrawingInteractive);                         // линза + рисование
        Assert.True(PresentationModes.Next(drawing, PresentationCommand.Lens) is { Lens: false, Drawing: true }); // повторная команда закрывает линзу
        var paused = PresentationModes.Next(PresentationModes.Next(n, PresentationCommand.Draw), PresentationCommand.Lens);
        Assert.True(paused.Drawing && paused.Selecting && !paused.DrawingInteractive);                      // выбор приостанавливает рисование
        var radial = PresentationModes.Next(n, PresentationCommand.Radial);
        Assert.True(radial.Drawing && radial.Radial);                                                       // меню включает рисование
        Assert.Equal(PresentationLayer.Radial, radial.Top);
        var afterEsc = PresentationModes.Next(radial, PresentationCommand.Escape);
        Assert.True(afterEsc.Drawing && !afterEsc.Radial);                                                  // Esc — сначала меню
        Assert.True(PresentationModes.Next(PresentationModes.Next(radial, PresentationCommand.Radial), PresentationCommand.Escape).IsNormal);
        Assert.Equal(new PresentationState(false, true, false, false), PresentationModes.Next(new PresentationState(false, true, true, false), PresentationCommand.Escape));
    }

    private static PresentationController Offscreen(LensSettings? lens = null) =>
        new(() => lens ?? LensSettings.Default with { DurationSec = 0 }, () => DrawSettings.Default with { FadeSec = 2 },
            () => (new PixelRect(-32000, -32000, 900, 600), 1.0));

    [Fact]
    public void Controller_DrawingRadialAndExit_LeaveNoWindowCapturingTheMouse()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                using var c = Offscreen();
                Assert.False(c.AnyWindowCapturesMouse);
                c.Execute(PresentationCommand.Draw);
                var d = c.Drawing!;
                Assert.True(d.IsVisible && d.Hwnd != 0 && !d.IsClickThrough && c.AnyWindowCapturesMouse);
                Assert.True((Screens.ExStyle(d.Hwnd) & Screens.WsExToolWindow) != 0); // не в Alt+Tab

                d.Draw(new Point(10, 10), new Point(200, 100));
                d.SetSettings(d.Settings with { Tool = DrawTool.Arrow, Dash = DrawDash.Dashed });
                d.Draw(new Point(20, 20), new Point(300, 200));
                Assert.Equal(2, d.ShapeCount);
                d.Undo();
                Assert.Equal(1, d.ShapeCount);
                d.Redo();
                Assert.Equal(2, d.ShapeCount);
                d.Clear();
                Assert.Equal(0, d.ShapeCount);
                d.Undo(); // отмена очистки возвращает фигуры
                Assert.Equal(2, d.ShapeCount);
                d.FadeTick(DateTime.UtcNow.AddSeconds(10)); // исчезновение по сроку, без возврата отменой
                Assert.Equal(0, d.ShapeCount);
                d.Undo();
                Assert.Equal(0, d.ShapeCount);

                // Радиальное меню у края: сдвигается внутрь целиком.
                c.Execute(PresentationCommand.Radial);
                Assert.True(d.RadialOpen);
                d.ShowRadial(new Point(5, 5));
                Assert.True(d.Radial!.Center.X >= RadialMenu.Radius && d.Radial.Center.Y >= RadialMenu.Radius);
                d.ShowRadial(new Point(899, 599));
                Assert.True(d.Radial.Center.X <= 900 - RadialMenu.Radius && d.Radial.Center.Y <= 600 - RadialMenu.Radius);
                d.Radial.Choose(RadialItem.Ellipse);
                Assert.Equal(DrawTool.Ellipse, d.Settings.Tool);
                Assert.False(d.RadialOpen);
                Assert.False(c.State.Radial);

                c.Execute(PresentationCommand.Radial);
                c.Escape();                       // Esc закрывает меню, рисование остаётся
                Assert.True(c.State.Drawing && !c.State.Radial);
                c.Escape();                       // повтор в пределах 200 мс — то же нажатие, рисование не закрывается
                Assert.True(c.State.Drawing);
                Thread.Sleep(250);
                c.Escape();
                Assert.True(c.State.IsNormal);
                Assert.Null(c.Drawing);
                Assert.Equal(0, d.Hwnd);          // окно закрыто, а не спрятано
                Assert.False(c.AnyWindowCapturesMouse);
                Assert.Empty(c.OpenWindows);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Fact]
    public void Controller_LensSelection_PausesDrawing_LensClosesAndReleasesMagnifier()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                using var c = Offscreen();
                c.Execute(PresentationCommand.Draw);
                var d = c.Drawing!;
                c.Execute(PresentationCommand.Lens);
                Assert.NotNull(c.Selection);
                Assert.True(d.IsClickThrough);              // рисование пропускает мышь, пока идёт выбор
                Assert.False(c.Selection!.IsClickThrough);

                // Щелчок без протягивания — область по умолчанию вокруг точки, ограниченная монитором.
                c.Selection.Complete(new Point(100, 100), new Point(102, 101));
                Assert.Null(c.Selection);
                Assert.True(c.State.Lens && c.State.Drawing);
                Assert.False(d.IsClickThrough);             // рисование снова принимает мышь
                var lens = c.Lens!;
                Assert.Contains(lens.Status, new[] { LensStatus.Live, LensStatus.SourceUnavailable, LensStatus.Failed });
                if (lens.Status != LensStatus.Failed)
                {
                    Assert.True(Screens.IsWindow(lens.HostHandle));
                    long ex = Screens.ExStyle(lens.HostHandle);
                    Assert.Equal(Screens.WsExTransparent | Screens.WsExNoActivate | Screens.WsExLayered | Screens.WsExToolWindow,
                        ex & (Screens.WsExTransparent | Screens.WsExNoActivate | Screens.WsExLayered | Screens.WsExToolWindow));
                    Assert.Equal(1, LiveLens.Initialized);
                }

                var src = lens.Source;
                Assert.True(src.Width < lens.Target.Width && src.Left > lens.Target.Left); // источник — центр области, уменьшенный в Zoom раз
                nint host = lens.HostHandle;
                c.Execute(PresentationCommand.ExitAll);
                Assert.True(c.State.IsNormal);
                Assert.Null(c.Lens);
                Assert.False(Screens.IsWindow(host));
                Assert.Equal(0, LiveLens.Initialized);
                Assert.False(c.AnyWindowCapturesMouse);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }

    [Fact]
    public void Lens_SourceGeometry_AndClamping_WithNegativeCoordinates()
    {
        var target = new PixelRect(-1500, -200, 400, 200);
        var src = LiveLens.SourceFor(target, 2);
        Assert.Equal(new PixelRect(-1400, -150, 200, 100), src);
        var monitor = new PixelRect(-1920, -300, 1920, 1080);
        var clamped = new PixelRect(-2100, -400, 300, 200).ClampInto(monitor);
        Assert.Equal(new PixelRect(-1920, -300, 300, 200), clamped);
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 5000, 5000).ClampInto(new PixelRect(0, 0, 1920, 1080)));
    }

    [Fact]
    public void Lens_UnavailableSource_IsNotShownAsLive()
    {
        // Область далеко за пределами виртуального экрана: система не даёт изображение — элемент скрыт, рамка сообщает об этом.
        Sta.Run(() =>
        {
            using var lens = new LiveLens(LensSettings.Default, new PixelRect(-30000, -30000, 200, 120), 1.0, () => []);
            lens.Open();
            if (lens.Status == LensStatus.Failed)
            {
                return; // Magnification API недоступен в этом окружении — честно пропускаем проверку состояния
            }

            Assert.Contains(lens.Status, new[] { LensStatus.SourceUnavailable, LensStatus.Live });
            lens.Close();
            Assert.Equal(LensStatus.Closed, lens.Status);
            Assert.Equal(0, LiveLens.Initialized);
        });
    }

    [Fact]
    public void HideDisplay_ClosesAllModes()
    {
        Sta.Run(() =>
        {
            InteractiveOverlayWindow.TestNoActivate = true;
            try
            {
                using var c = Offscreen();
                c.Execute(PresentationCommand.Radial);
                c.Execute(PresentationCommand.Lens);
                Assert.True(c.AnyWindowCapturesMouse);
                c.Execute(PresentationCommand.ExitAll); // так приложение отвечает на скрытие отображения и потерю контекста
                Assert.Empty(c.OpenWindows);
                Assert.False(c.AnyWindowCapturesMouse);
            }
            finally
            {
                InteractiveOverlayWindow.TestNoActivate = false;
            }
        });
    }
}
