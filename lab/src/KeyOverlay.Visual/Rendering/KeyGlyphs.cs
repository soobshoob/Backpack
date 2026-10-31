using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KeyOverlay.Visual.Rendering;

public enum Glyph
{
    None, Shift, CapsLock, Control, Option, Command, WindowsGrid, SuperDiamond, Enter, Return, Backspace, ForwardDelete,
    Tab, Escape, ArrowUp, ArrowDown, ArrowLeft, ArrowRight, Space, Menu, Home, End, PageUp, PageDown, Mouse,
    VolumeMute, VolumeDown, VolumeUp, PlayPause, NextTrack, PrevTrack, MediaStop,
}

[Flags]
public enum MouseButtons { None = 0, Left = 1, Right = 2, Middle = 4, X1 = 8, X2 = 16 }

/// <summary>
/// Собственная векторная геометрия обозначений клавиш (KeyOverlay, лицензия проекта; не взята из Keyviz, Lucide или шрифтов).
/// Сетка 24×24, линии толщиной 2 с круглыми концами; масштабируется через <see cref="Viewbox"/> вместе с толщиной.
/// Не зависит от наличия символов (⇧ ⌘ ⌫ …) в системных шрифтах.
/// </summary>
public static class KeyGlyphs
{
    public const double Grid = 24;

    private static readonly Dictionary<Glyph, string> Strokes = new()
    {
        [Glyph.Shift] = "M12 4 L4 12 H8.5 V20 H15.5 V12 H20 Z",
        [Glyph.VolumeMute] = "M3.5 9.5 H7.5 L12.5 5 V19 L7.5 14.5 H3.5 Z M15.5 9.5 L20.5 14.5 M20.5 9.5 L15.5 14.5",
        [Glyph.VolumeDown] = "M3.5 9.5 H7.5 L12.5 5 V19 L7.5 14.5 H3.5 Z M16 9 A4 4 0 0 1 16 15",
        [Glyph.VolumeUp] = "M3.5 9.5 H7.5 L12.5 5 V19 L7.5 14.5 H3.5 Z M16 9 A4 4 0 0 1 16 15 M18.5 6 A8 8 0 0 1 18.5 18",
        [Glyph.PlayPause] = "M4 6 L11 12 L4 18 Z M15 6 V18 M19.5 6 V18",
        [Glyph.NextTrack] = "M5 6 L13 12 L5 18 Z M17.5 6 V18",
        [Glyph.PrevTrack] = "M19 6 L11 12 L19 18 Z M6.5 6 V18",
        [Glyph.MediaStop] = "M6.5 6.5 H17.5 V17.5 H6.5 Z",
        [Glyph.CapsLock] = "M12 3 L4.5 10.5 H8.5 V15 H15.5 V10.5 H19.5 Z M8.5 19.5 H15.5",
        [Glyph.Control] = "M6 14.5 L12 8.5 L18 14.5",
        [Glyph.Option] = "M3.5 7 H8.5 L15.5 17 H20.5 M13.5 7 H20.5",
        [Glyph.Command] = "M9 9 H15 V15 H9 Z M9 9 V7 A2.5 2.5 0 1 0 7 9 Z M15 9 H17 A2.5 2.5 0 1 0 15 7 Z M15 15 V17 A2.5 2.5 0 1 0 17 15 Z M9 15 H7 A2.5 2.5 0 1 0 9 17 Z",
        [Glyph.WindowsGrid] = "M4.5 4.5 H11 V11 H4.5 Z M13 4.5 H19.5 V11 H13 Z M4.5 13 H11 V19.5 H4.5 Z M13 13 H19.5 V19.5 H13 Z",
        [Glyph.SuperDiamond] = "M12 4 L20 12 L12 20 L4 12 Z",
        [Glyph.Enter] = "M19 5 V12 A2.5 2.5 0 0 1 16.5 14.5 H6 M10 10.5 L6 14.5 L10 18.5",
        [Glyph.Return] = "M9.5 6 L5.5 10 L9.5 14 M5.5 10 H15 A4 4 0 0 1 15 18 H12",
        [Glyph.Backspace] = "M9 6 H20 V18 H9 L3 12 Z M12.5 9.5 L16.5 14.5 M16.5 9.5 L12.5 14.5",
        [Glyph.ForwardDelete] = "M4 6 H15 L21 12 L15 18 H4 Z M7.5 9.5 L11.5 14.5 M11.5 9.5 L7.5 14.5",
        [Glyph.Tab] = "M3 12 H17.5 M12.5 7 L17.5 12 L12.5 17 M21 6 V18",
        [Glyph.Escape] = "M10 4 H4 V10 M4 4 L11 11 M13.5 4.4 A8 8 0 1 1 4.4 13.5",
        [Glyph.ArrowUp] = "M12 19 V5 M6 11 L12 5 L18 11",
        [Glyph.ArrowDown] = "M12 5 V19 M6 13 L12 19 L18 13",
        [Glyph.ArrowLeft] = "M19 12 H5 M11 6 L5 12 L11 18",
        [Glyph.ArrowRight] = "M5 12 H19 M13 6 L19 12 L13 18",
        [Glyph.Space] = "M4 10 V15 H20 V10",
        [Glyph.Menu] = "M5 7 H19 M5 12 H19 M5 17 H19",
        [Glyph.Home] = "M6 6 H14 M6 6 V14 M6 6 L18 18",
        [Glyph.End] = "M18 18 H10 M18 18 V10 M18 18 L6 6",
        [Glyph.PageUp] = "M12 20 V7 M7 12 L12 7 L17 12 M7 3.5 H17",
        [Glyph.PageDown] = "M12 4 V17 M7 12 L12 17 L17 12 M7 20.5 H17",
        [Glyph.Mouse] = MouseBody + " M6 10 H18 M12 3 V10",
    };

    private const string MouseBody = "M12 3 A6 6 0 0 0 6 9 V15 A6 6 0 0 0 18 15 V9 A6 6 0 0 0 12 3 Z";

    private static readonly Dictionary<string, Geometry> Cache = new();

    public static bool Has(Glyph g) => Strokes.ContainsKey(g);

    public static Geometry StrokeGeometry(Glyph g) => Parse(Strokes[g]);

    /// <summary>Векторный символ размером <paramref name="size"/>×<paramref name="size"/> DIP.</summary>
    public static FrameworkElement Create(Glyph g, double size, Brush stroke)
    {
        var canvas = new Canvas { Width = Grid, Height = Grid };
        canvas.Children.Add(StrokePath(Strokes[g], stroke));
        return Box(canvas, size);
    }

    private static Path StrokePath(string data, Brush stroke) => new()
    {
        Data = Parse(data),
        Stroke = stroke,
        StrokeThickness = 2,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };

    private static Viewbox Box(Canvas canvas, double size) => new()
    {
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        Child = canvas,
        IsHitTestVisible = false,
        SnapsToDevicePixels = false,
    };

    private static Geometry Parse(string data)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(data, out var g))
            {
                g = Geometry.Parse(data);
                g.Freeze();
                Cache[data] = g;
            }

            return g;
        }
    }
}
