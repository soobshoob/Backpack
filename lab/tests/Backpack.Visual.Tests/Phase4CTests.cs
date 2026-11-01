using System.Windows;
using Backpack.Visual.Commands;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Mouse;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tests;

/// <summary>
/// Phase 4B/4C: реестр команд и сочетания по областям, «Фокус» (раздельные источник и вывод), увеличение у курсора
/// (геометрия), умный карандаш и нумерованные отметки. Окна рисования создаются за пределами экрана, ввод не вводится.
/// </summary>
public class Phase4CTests
{
    private static List<Point> Circle(double cx, double cy, double rx, double ry, int n = 64, double wobble = 0) =>
        Enumerable.Range(0, n + 1).Select(i =>
        {
            double a = 2 * Math.PI * i / n;
            double k = 1 + (wobble * Math.Sin(a * 7));
            return new Point(cx + (rx * k * Math.Cos(a)), cy + (ry * k * Math.Sin(a)));
        }).ToList();

    private static List<Point> RectPath(double x, double y, double w, double h, double jitter = 0)
    {
        var pts = new List<Point>();
        var rnd = new Random(3);
        Point[] corners = [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h), new(x, y + 2)];
        for (int c = 0; c < 4; c++)
        {
            for (int i = 0; i < 20; i++)
            {
                double t = i / 20.0;
                var p = corners[c] + ((corners[c + 1] - corners[c]) * t);
                pts.Add(new Point(p.X + ((rnd.NextDouble() - 0.5) * jitter), p.Y + ((rnd.NextDouble() - 0.5) * jitter)));
            }
        }

        pts.Add(corners[4]);
        return pts;
    }

    [Fact]
    public void Recognizer_finds_line_circle_ellipse_and_rectangle()
    {
        var line = Enumerable.Range(0, 40).Select(i => new Point(10 + (i * 8), 50 + (i * 3) + (i % 2 == 0 ? 1.5 : -1.5))).ToList();
        Assert.Equal(RecognizedKind.Line, ShapeRecognizer.Recognize(line)?.Kind);
        Assert.Equal(RecognizedKind.Ellipse, ShapeRecognizer.Recognize(Circle(200, 200, 80, 80, wobble: 0.04))?.Kind);
        Assert.Equal(RecognizedKind.Ellipse, ShapeRecognizer.Recognize(Circle(200, 200, 160, 60))?.Kind);
        Assert.Equal(RecognizedKind.Rectangle, ShapeRecognizer.Recognize(RectPath(50, 60, 220, 120, jitter: 4))?.Kind);
    }

    [Fact]
    public void Recognizer_keeps_low_confidence_strokes()
    {
        // Каракули, буква «S», слишком короткий штрих, открытая дуга — без замены.
        var rnd = new Random(7);
        var scribble = Enumerable.Range(0, 80).Select(_ => new Point(rnd.Next(0, 200), rnd.Next(0, 200))).ToList();
        Assert.Null(ShapeRecognizer.Recognize(scribble));
        var s = Enumerable.Range(0, 60).Select(i => new Point(100 + (60 * Math.Sin(i / 60.0 * 2 * Math.PI)), i * 4.0)).ToList();
        Assert.Null(ShapeRecognizer.Recognize(s));
        Assert.Null(ShapeRecognizer.Recognize([new Point(0, 0), new Point(5, 5), new Point(10, 9)]));
        // Phase 4I: открытая дуга — отдельная фигура (была «без замены» в 4C).
        var arc = Circle(200, 200, 80, 80).Take(24).ToList();
        Assert.Equal(RecognizedKind.Arc, ShapeRecognizer.Recognize(arc)?.Kind);
    }

    [Fact]
    public void Recognizer_is_scale_invariant()
    {
        foreach (double k in new[] { 0.6, 1, 2.5 })
        {
            Assert.Equal(RecognizedKind.Rectangle, ShapeRecognizer.Recognize(RectPath(10 * k, 10 * k, 200 * k, 140 * k, jitter: 3 * k))?.Kind);
            Assert.Equal(RecognizedKind.Ellipse, ShapeRecognizer.Recognize(Circle(300 * k, 300 * k, 100 * k, 70 * k))?.Kind);
        }
    }

    private static DrawingWindow Window() =>
        new(new PixelRect(-32000, -32000, 1200, 800), 1.0, DrawSettings.Default with { Color = new ThemeColor(0x22, 0xC5, 0x5E, 0x99), Thickness = 6, Dash = DrawDash.Dashed }, DrawingProfile.Default);

    [Fact]
    public void Smart_pen_replaces_the_stroke_with_an_editable_shape_and_undo_restores_the_stroke()
    {
        Sta.Run(() =>
        {
            var w = Window();
            w.SmartStroke(Circle(400, 300, 120, 80));
            Assert.Equal(1, w.SmartReplacements);
            Assert.Equal(1, w.ShapeCount);
            var shape = Assert.IsType<System.Windows.Shapes.Ellipse>(w.Shapes.Single());
            Assert.Equal(6, shape.StrokeThickness);
            Assert.NotNull(shape.StrokeDashArray);
            Assert.Equal(0x99, ((System.Windows.Media.SolidColorBrush)shape.Stroke).Color.A);

            w.Undo(); // отмена преобразования — исходный штрих
            Assert.IsType<System.Windows.Shapes.Polyline>(w.Shapes.Single());
            w.Redo();
            Assert.IsType<System.Windows.Shapes.Ellipse>(w.Shapes.Single());
            w.Undo();
            w.Undo(); // затем — и сам штрих
            Assert.Equal(0, w.ShapeCount);

            w.SmartStroke([new Point(10, 10), new Point(40, 90), new Point(80, 15), new Point(120, 95), new Point(160, 12)]);
            Assert.IsType<System.Windows.Shapes.Polyline>(w.Shapes.Single()); // зигзаг остаётся линией
            w.Close();
        });
    }

    [Fact]
    public void Markers_number_consistently_after_undo_reset_and_move()
    {
        Sta.Run(() =>
        {
            var w = Window();
            w.SetMarkerStart(3);
            w.PlaceMarker(new Point(100, 100));
            w.PlaceMarker(new Point(200, 100));
            w.PlaceMarker(new Point(300, 100));
            Assert.Equal([3, 4, 5], w.MarkerNumbers);
            w.Undo();
            Assert.Equal(5, w.NextMarkerNumber); // отменённый номер выдаётся снова
            w.Redo();
            Assert.Equal(6, w.NextMarkerNumber);

            w.MoveMarker(4, new Point(250, 300));
            Assert.Equal([3, 4, 5], w.MarkerNumbers);
            w.Undo(); // перемещение отменяется одним шагом
            Assert.Equal([3, 4, 5], w.MarkerNumbers);
            Assert.Equal(3, w.ShapeCount);

            w.ResetMarkers(1);
            w.PlaceMarker(new Point(400, 400));
            Assert.Equal([1], w.MarkerNumbers);
            Assert.Equal(4, w.ShapeCount);
            w.Close();
        });
    }

    [Fact]
    public void Focus_source_and_output_are_independent()
    {
        var bounds = new PixelRect(-1920, 0, 1920, 1080);
        var r = FocusRegion.FromSelection(new PixelRect(-1600, 200, 600, 300), 2.0);
        Assert.Equal(300, r.Source.Width);
        Assert.Equal(150, r.Source.Height);
        Assert.Equal(-1600 + 150, r.Source.Left); // центр выделения

        var moved = r.MoveOutput(400, 100, bounds);
        Assert.Equal(r.Source, moved.Source); // перемещение вывода не меняет источник
        Assert.Equal(-1200, moved.Output.Left);

        var src = moved.MoveSource(-50, 20, bounds);
        Assert.Equal(moved.Output, src.Output);
        Assert.Equal(moved.Source.Left - 50, src.Source.Left);

        var bigger = src.ResizeOutput(src.Output with { Width = 900 }, 3.0, bounds, 48);
        Assert.Equal(300, bigger.Source.Width); // источник = вывод / увеличение
        var clamped = bigger.MoveOutput(5000, 5000, bounds);
        Assert.True(clamped.Output.Right <= bounds.Right && clamped.Output.Bottom <= bounds.Bottom);

        var zoomed = r.WithZoom(1.0, bounds);
        Assert.Equal(r.Output.Width, zoomed.Source.Width);
    }

    [Fact]
    public void Focus_edit_hit_test_distinguishes_move_source_and_resize()
    {
        var o = new Rect(100, 100, 300, 200);
        Assert.Equal("move", FocusEditWindow.HitTest(o, new Point(200, 200), false));
        Assert.Equal("source", FocusEditWindow.HitTest(o, new Point(200, 200), true));
        Assert.Equal("e", FocusEditWindow.HitTest(o, new Point(400, 200), false));
        Assert.Equal("se", FocusEditWindow.HitTest(o, new Point(400, 300), false));
        Assert.Null(FocusEditWindow.HitTest(o, new Point(10, 10), false));
    }

    [Fact]
    public void Cursor_zoom_geometry_follows_smoothly_stays_on_the_monitor_and_centers_on_the_real_pointer()
    {
        Assert.Equal(1, CursorZoomGeometry.Alpha(16, 0));
        double a = CursorZoomGeometry.Alpha(16, 50);
        Assert.InRange(a, 0.2, 0.35);
        Assert.True(CursorZoomGeometry.Alpha(50, 50) > a);

        var mon = new PixelRect(-1920, -200, 1920, 1080);
        int size = CursorZoomGeometry.SizePx(220, 1.25);
        Assert.Equal(275, size);
        var output = CursorZoomGeometry.Output(-5, -190, size, mon); // у угла монитора — сдвинуто внутрь
        Assert.True(output.Left >= mon.Left && output.Right <= mon.Right && output.Top >= mon.Top);
        var source = CursorZoomGeometry.Source(-900, 300, CursorZoomGeometry.Output(-900, 300, size, mon), 1.5, mon);
        Assert.InRange(source.Left + (source.Width / 2), -901, -899);
        Assert.InRange(source.Top + (source.Height / 2), 299, 301);
        Assert.InRange(source.Width, (int)(size / 1.5) - 1, (int)(size / 1.5) + 1); // целые физические пиксели
    }

    [Fact]
    public void Command_registry_executes_one_implementation_and_respects_availability()
    {
        int runs = 0;
        bool available = true;
        var reg = new CommandRegistry();
        reg.Register(new CommandDef("tool.focus.lens", "Focus.Lens", "Cat.Focus", CommandScope.Global, CommandKind.Action, () => runs++, () => available));
        Assert.True(reg.Execute("tool.focus.lens"));
        available = false;
        Assert.False(reg.Execute("tool.focus.lens"));
        Assert.False(reg.Execute("missing"));
        Assert.Equal(1, runs);
        Assert.Empty(reg.Bindings(CommandBindings.Empty)); // недоступная команда не регистрируется
        available = true;
        Assert.Empty(reg.Bindings(CommandBindings.Empty)); // новых сочетаний по умолчанию нет
        var b = CommandBindings.Empty.With("tool.focus.lens", "Ctrl+Alt+Shift+F");
        Assert.Single(reg.Bindings(b));
        Assert.Empty(reg.Bindings(b.With("tool.focus.lens", ""))); // пустая строка — снято
        Assert.Equal(1, reg.RemoveWhere(d => d.Id.StartsWith("tool.", StringComparison.Ordinal)));
    }

    [Fact]
    public void Command_hotkeys_scopes_conflicts_and_letters()
    {
        Sta.Run(() =>
        {
            var api = new RecordingApi();
            using var m = new HotkeyManager(api, createWindow: false);
            m.Apply(HotkeySettings.Default);
            Hotkey.TryParse("Ctrl+Alt+Shift+F", out var f);
            Hotkey.TryParse("F3", out var f3);
            Hotkey.TryParse("Shift+A", out var letter);
            m.ApplyCommands([
                ("tool.focus.lens", f, CommandScope.Global, CommandTool.None, true),
                ("draw.marker", f3, CommandScope.Tool, CommandTool.Drawing, true),
                ("bad.letter", letter, CommandScope.Global, CommandTool.None, true),
                ("dup.display", HotkeySettings.Default.ToggleDisplay!.Value, CommandScope.Global, CommandTool.None, true),
                ("focus.edit", f3, CommandScope.Tool, CommandTool.Focus, true),
            ]);
            Assert.Equal(HotkeyStatus.Registered, m.StatusOf("tool.focus.lens"));
            Assert.Equal(HotkeyStatus.WaitingForMode, m.StatusOf("draw.marker")); // F3 — только при рисовании
            Assert.Equal(HotkeyStatus.Invalid, m.StatusOf("bad.letter")); // обычные буквы не становятся глобальными
            Assert.Equal(HotkeyStatus.Conflict, m.StatusOf("dup.display")); // занято прежним действием
            m.SetDrawingActive(true);
            Assert.Equal(HotkeyStatus.Registered, m.StatusOf("draw.marker"));
            Assert.Equal(HotkeyStatus.Conflict, m.StatusOf("focus.edit")); // то же F3
            string? pressed = null;
            m.CommandPressed += id => pressed = id;
            Assert.True(m.RaiseCommand("draw.marker"));
            Assert.Equal("draw.marker", pressed);
            m.SetDrawingActive(false);
            Assert.Equal(HotkeyStatus.WaitingForMode, m.StatusOf("draw.marker"));
            Assert.False(m.RaiseCommand("draw.marker")); // после выхода регистрация освобождена
            Assert.All(api.Registered.Values, r => Assert.NotEqual(0x72u, r.Vk));
            Assert.True(api.Registered.Values.All(r => (r.Mods & 0x4000) != 0)); // MOD_NOREPEAT: удержание не повторяет команду
        });
    }

    private sealed class RecordingApi : IHotkeyApi
    {
        public Dictionary<int, (uint Mods, uint Vk)> Registered { get; } = new();

        public bool Register(nint hwnd, int id, uint modifiers, uint vk, out int error)
        {
            error = 0;
            Registered[id] = (modifiers, vk);
            return true;
        }

        public bool Unregister(nint hwnd, int id) => Registered.Remove(id);
    }
}
