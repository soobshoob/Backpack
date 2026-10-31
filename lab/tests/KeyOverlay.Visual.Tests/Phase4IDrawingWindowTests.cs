using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyOverlay.Visual.Presentation;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Tests;

/// <summary>Phase 4I, п. 5–7 в настоящем окне рисования: дуга и угол, Shift/Alt, рамка с ручками, поворот, отмена и повтор.</summary>
public class Phase4IDrawingWindowTests
{
    private static DrawingWindow Window(DrawSettings? s = null)
    {
        var w = new DrawingWindow(new PixelRect(-32000, -32000, 1200, 800), 1.0, s ?? DrawSettings.Default, DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly });
        if (w.Content is FrameworkElement root)
        {
            root.Measure(new Size(1200, 800));
            root.Arrange(new Rect(0, 0, 1200, 800));
        }

        return w;
    }

    private static List<Point> Arc(Point c, double r, double from, double to) =>
        Enumerable.Range(0, 70).Select(i => (from + ((to - from) * i / 69.0)) * Math.PI / 180).Select(a => new Point(c.X + (r * Math.Cos(a)), c.Y + (r * Math.Sin(a)))).ToList();

    private static List<Point> Rays(Point a, Point v, Point b) =>
        Enumerable.Range(0, 40).Select(i => a + ((v - a) * (i / 40.0))).Concat(Enumerable.Range(0, 41).Select(i => v + ((b - v) * (i / 40.0)))).ToList();

    [Fact]
    public void Smart_pen_arc_replaces_the_stroke_once_and_undo_returns_the_stroke()
    {
        Sta.Run(() =>
        {
            var w = Window();
            try
            {
                w.SmartStroke(Arc(new Point(500, 400), 160, 200, 340));
                var arc = Assert.IsType<Path>(w.Shapes.Single());
                Assert.NotNull(DrawingWindow.ArcOf(arc));
                w.Undo();
                Assert.IsType<Polyline>(w.Shapes.Single());     // исходный штрих
                w.Redo();
                Assert.IsType<Path>(w.Shapes.Single());
                w.Undo();
                w.Undo();
                Assert.Empty(w.Shapes);                         // предпросмотр не создавал лишних записей истории
                Assert.False(w.CanUndo);
            }
            finally
            {
                w.Close();
            }
        });
    }

    [Fact]
    public void Angle_degrees_are_shown_only_while_drawing_unless_persistent_labels_are_on()
    {
        Sta.Run(() =>
        {
            foreach (bool persistent in new[] { false, true })
            {
                var w = Window(DrawSettings.Default with { AngleLabels = persistent });
                try
                {
                    var pts = Rays(new Point(800, 500), new Point(500, 500), new Point(500, 200));
                    w.BeginStrokeForTest(pts[0]);
                    string? during = null;
                    foreach (var p in pts.Skip(1))
                    {
                        w.ExtendStrokeForTest(p);
                        during ??= w.AngleLabelText;
                    }

                    Assert.Equal("90°", w.AngleLabelText);       // устойчиво к концу штриха
                    Assert.NotNull(during);
                    w.EndStrokeForTest();
                    Assert.Null(w.AngleLabelText);               // временная подпись скрыта после отпускания
                    Assert.Contains(w.Shapes, s => s is Polyline { Points.Count: 3 });
                    Assert.Equal(persistent, w.Shapes.OfType<TextBlock>().Any(t => t.Text == "90°"));
                    w.Undo();
                    Assert.IsType<Polyline>(w.Shapes.Single());  // одна запись: и угол, и подпись уходят вместе
                    Assert.True(((Polyline)w.Shapes.Single()).Points.Count > 3);
                }
                finally
                {
                    w.Close();
                }
            }
        });
    }

    [Theory]
    [InlineData(DrawTool.Rectangle)]
    [InlineData(DrawTool.Ellipse)]
    [InlineData(DrawTool.RoundedRectangle)]
    public void Shift_and_Alt_change_the_preview_during_the_gesture_without_a_new_shape(DrawTool tool)
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = tool });
            try
            {
                w.ModifiersForTest = ModifierKeys.None;
                w.BeginStrokeForTest(new Point(300, 300));
                w.ExtendStrokeForTest(new Point(420, 360));
                var shape = (FrameworkElement)w.CurrentShapeForTest!;
                Assert.Equal((120.0, 60.0), (shape.Width, shape.Height));
                w.SetModifiersForTest(ModifierKeys.Shift);
                Assert.Equal((120.0, 120.0), (shape.Width, shape.Height));                         // квадрат / окружность
                Assert.Equal((300.0, 300.0), (Canvas.GetLeft(shape), Canvas.GetTop(shape)));       // исходная точка на месте
                w.SetModifiersForTest(ModifierKeys.Alt);
                Assert.Equal((240.0, 120.0), (shape.Width, shape.Height));                         // от центра
                Assert.Equal((180.0, 240.0), (Canvas.GetLeft(shape), Canvas.GetTop(shape)));
                w.SetModifiersForTest(ModifierKeys.Shift | ModifierKeys.Alt);
                Assert.Equal((240.0, 240.0), (shape.Width, shape.Height));
                w.SetModifiersForTest(ModifierKeys.None);
                Assert.Equal((120.0, 60.0), (shape.Width, shape.Height));                          // отпускание — как было
                Assert.Same(shape, w.CurrentShapeForTest);                                          // та же фигура, не новая
                w.EndStrokeForTest();
                Assert.Single(w.Shapes);
            }
            finally
            {
                w.Close();
            }
        });
    }

    [Fact]
    public void Resize_and_rotate_are_one_history_step_each_and_escape_cancels()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = DrawTool.Rectangle, Thickness = 6 });
            try
            {
                w.ModifiersForTest = ModifierKeys.None;
                w.BeginStrokeForTest(new Point(300, 300));
                w.ExtendStrokeForTest(new Point(500, 400));
                w.EndStrokeForTest();
                w.SetSettings(w.Settings with { Tool = DrawTool.Select });
                Assert.True(w.SelectAt(new Point(300, 350)));
                var before = DrawingWindow.BoundsOf(w.Shapes.Single());

                // Esc во время жеста — ничего не меняется.
                w.GestureForTest("resize:1,1", new Point(700, 600), commit: false);
                Assert.Equal(TransformGesture.Resize, w.Gesture);
                Assert.True(w.CancelGesture());
                Assert.Equal(before, DrawingWindow.BoundsOf(w.Shapes.Single()));

                // Изменение размера: одна запись истории, толщина линии прежняя.
                w.GestureForTest("resize:1,1", new Point(600, 500));
                var resized = (Shape)w.Shapes.Single();
                Assert.Equal(6, resized.StrokeThickness);
                Assert.True(DrawingWindow.BoundsOf(resized).Width > before.Width + 50);

                // Поворот на 90° (Shift — шаг 15°): рамка повёрнута, попадание по новому положению.
                var frame = DrawingWindow.FrameOf(resized);
                w.ModifiersForTest = ModifierKeys.Shift;
                w.GestureForTest("rotate", new Point(frame.Center.X + 300, frame.Center.Y + 5));
                var rotated = w.Shapes.Single();
                Assert.Equal(90, DrawingWindow.OrientationOf(rotated), 3);
                Assert.Equal(6, ((Shape)rotated).StrokeThickness);
                var f2 = DrawingWindow.FrameOf(rotated);
                Assert.Equal(frame.Center.X, f2.Center.X, 1);
                Assert.Equal(frame.Center.Y, f2.Center.Y, 1);
                Assert.Equal(frame.Size.Width, f2.Size.Width, 1);                                 // рамка в осях объекта
                var edge = new Point(f2.Center.X + (frame.Size.Height / 2) - 3, f2.Center.Y);       // бывший верхний край стал правым
                Assert.NotNull(w.HitTest(edge));

                w.Undo();
                Assert.Equal(0, DrawingWindow.OrientationOf(w.Shapes.Single()));
                w.Undo();
                Assert.Equal(before, DrawingWindow.BoundsOf(w.Shapes.Single()));
                w.Redo();
                w.Redo();
                Assert.Equal(90, DrawingWindow.OrientationOf(w.Shapes.Single()), 3);
            }
            finally
            {
                w.Close();
            }
        });
    }

    [Fact]
    public void Arc_point_handles_reshape_the_arc_and_the_frame_scales_it_whole()
    {
        Sta.Run(() =>
        {
            var w = Window();
            try
            {
                w.SmartStroke(Arc(new Point(500, 400), 160, 200, 340));
                w.SetSettings(w.Settings with { Tool = DrawTool.Select });
                var arc = w.Shapes.Single();
                var pts = DrawingWindow.ArcOf(arc)!;
                Assert.True(w.SelectAt(pts.M));
                w.GestureForTest("arc:1", new Point(pts.M.X, pts.M.Y - 60));
                var reshaped = DrawingWindow.ArcOf(w.Shapes.Single())!;
                Assert.Equal(pts.A, reshaped.A);
                Assert.Equal(pts.B, reshaped.B);
                Assert.Equal(pts.M.Y - 60, reshaped.M.Y, 3);

                // Почти прямая через ручки — отрезок без скачка.
                w.GestureForTest("arc:1", new Point((pts.A.X + pts.B.X) / 2, (pts.A.Y + pts.B.Y) / 2));
                Assert.IsType<LineGeometry>(((Path)w.Shapes.Single()).Data);

                w.Undo();
                w.ModifiersForTest = ModifierKeys.None;
                w.SelectAt(DrawingWindow.ArcOf(w.Shapes.Single())!.M);
                var before = DrawingWindow.FrameOf(w.Shapes.Single());
                w.GestureForTest("resize:1,1", new Point(before.Center.X + before.Size.Width, before.Center.Y + before.Size.Height));
                var scaled = DrawingWindow.ArcOf(w.Shapes.Single())!;
                Assert.True((scaled.B - scaled.A).Length > (reshaped.B - reshaped.A).Length * 1.3); // дуга целиком больше
                Assert.Equal(4, w.UndoDepthForTest);
            }
            finally
            {
                w.Close();
            }
        });
    }

    [Fact]
    public void Text_scales_uniformly_and_numbers_do_not_rotate()
    {
        Sta.Run(() =>
        {
            var w = Window(DrawSettings.Default with { Tool = DrawTool.Text });
            try
            {
                w.TypeTextForTest(new Point(300, 300), "Note");
                w.SetSettings(w.Settings with { Tool = DrawTool.Select });
                var text = w.Shapes.OfType<TextBlock>().Single();
                var b = DrawingWindow.BoundsOf(text);
                Assert.True(w.SelectAt(new Point(b.X + 5, b.Y + 5)));
                w.ModifiersForTest = ModifierKeys.None;
                w.GestureForTest("resize:1,0", new Point(b.Right + b.Width, b.Y + (b.Height / 2)));
                var scaled = w.Shapes.Single();
                var m = scaled.RenderTransform.Value;
                Assert.Equal(m.M11, m.M22, 6);                                                  // только равномерно
                Assert.True(m.M11 > 1.5);

                w.Clear();
                w.SetSettings(w.Settings with { Tool = DrawTool.Marker });
                w.PlaceMarkerForTest(new Point(600, 400));
                w.SetSettings(w.Settings with { Tool = DrawTool.Select });
                Assert.True(w.SelectAt(new Point(600, 400)));
                Assert.Null(w.HandleTags.FirstOrDefault(t => t == "rotate"));                    // номер не поворачивается
            }
            finally
            {
                w.Close();
            }
        });
    }
}
