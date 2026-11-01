using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Backpack.Visual.Brand;
using Backpack.Visual.Presentation;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Styles;

namespace Backpack.App;

/// <summary>
/// Файлы для проверки внешнего вида без захвата ввода и без снимков экрана: всё рисуется собственными элементами WPF
/// (те же классы, что в оверлее) в растр через RenderTargetBitmap.
/// <list type="bullet">
/// <item><c>--render-brand=папка</c> — исходник логотипа (SVG, полный и упрощённый), ICO приложения и трея, PNG логотипа и
///       иконок 16–48 px на светлой и тёмной «панели задач» (увеличены без сглаживания для просмотра пикселей).</item>
/// <item><c>--render-sheet=папка</c> — сравнительный лист: все рендеры кейкапов (обычное и нажатое состояние, светлый и тёмный
///       фон, малый размер), все четыре семейства мыши (кнопки, боковые кнопки, прокрутка, Unknown), логотип.</item>
/// </list>
/// Это проверка отрисовки, а не проверка реального click-through или поведения окон.
/// </summary>
internal static class ReviewSheets
{
    private static readonly Color Light = Color.FromRgb(0xEE, 0xEE, 0xF0);
    private static readonly Color Dark = Color.FromRgb(0x2B, 0x2B, 0x30);

    public static int RenderBrand(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "backpack-logo.svg"), LogoArt.ToSvg());
        File.WriteAllText(Path.Combine(dir, "backpack-logo-small.svg"), LogoArt.ToSvg(simplified: true));
        File.WriteAllBytes(Path.Combine(dir, "Backpack.ico"), LogoArt.Ico(LogoArt.IconSizes));
        File.WriteAllBytes(Path.Combine(dir, "BackpackTray.ico"), LogoArt.Ico([16, 20, 24, 32, 40, 48], simplifiedOnly: true));
        File.WriteAllBytes(Path.Combine(dir, "backpack-logo-512.png"), LogoArt.Png(LogoArt.Render(512)));

        // Малые размеры на светлой и тёмной панели задач, увеличены ×6 без сглаживания: значок программы (изумрудный контур) и
        // значок трея (цвет выбирается по теме панели задач: тёмный на светлой, белый на тёмной). Подложки у логотипа нет.
        var row = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.White };
        foreach (var (bg, name, tray) in new[] { (Color.FromRgb(0xF3, 0xF3, 0xF3), "light taskbar", Color.FromRgb(0x1B, 0x1B, 0x1B)), (Color.FromRgb(0x20, 0x20, 0x20), "dark taskbar", Colors.White) })
        {
            bool dark = bg == Color.FromRgb(0x20, 0x20, 0x20);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(bg) };
            line.Children.Add(Caption(name, dark));
            foreach (int px in new[] { 16, 20, 24, 32, 48 })
            {
                foreach (var (color, label) in new[] { (tray, "tray"), ((Color)ColorConverter.ConvertFromString(LogoArt.IconColor), "app") })
                {
                    var img = new Image { Source = LogoArt.Render(px, label == "tray" || px <= 24, color, iconFrame: true), Width = px * 6, Height = px * 6, Margin = new Thickness(10) };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                    var cell = new StackPanel();
                    cell.Children.Add(img);
                    cell.Children.Add(Caption($"{px}px {label}", dark));
                    line.Children.Add(cell);
                }
            }

            row.Children.Add(line);
        }

        // Логотип в цветах тем окна (контур акцента; «Классическая» — чёрный и белый).
        foreach (bool dark in new[] { false, true })
        {
            var bg = dark ? Color.FromRgb(0x11, 0x15, 0x13) : Color.FromRgb(0xF6, 0xF7, 0xF6);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(bg) };
            line.Children.Add(Caption(dark ? "dark theme" : "light theme", dark));
            foreach (var palette in Backpack.Visual.Settings.UiPalettes.List)
            {
                uint c = Backpack.Visual.Settings.UiPalettes.LogoColor(palette.Id, dark);
                var cell = new StackPanel();
                cell.Children.Add(new Image { Source = LogoArt.Render(96, false, Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c)), Width = 96, Height = 96, Margin = new Thickness(14) });
                cell.Children.Add(Caption(palette.Id, dark));
                line.Children.Add(cell);
            }

            row.Children.Add(line);
        }

        Save(row, Path.Combine(dir, "backpack-icon-sizes.png"));
        return 0;
    }

    public static int RenderComparison(string dir)
    {
        Directory.CreateDirectory(dir);
        Save(KeycapSheet(32), Path.Combine(dir, "sheet-keycaps.png"));
        Save(KeycapSheet(16), Path.Combine(dir, "sheet-keycaps-small.png"));
        Save(VolumetricCloseUp(), Path.Combine(dir, "sheet-volumetric-large.png"));
        Save(MouseSheet(), Path.Combine(dir, "sheet-mouse.png"));
        Save(BrandSheet(), Path.Combine(dir, "sheet-brand-neon.png"));
        Save(BrandSheet(), Path.Combine(dir, "sheet-brand-neon-150.png"), 1.5);
        var logo = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Light) };
        logo.Children.Add(new Image { Source = LogoArt.Render(256), Width = 256, Height = 256, Margin = new Thickness(16) });
        foreach (int px in new[] { 16, 24, 32, 48 })
        {
            logo.Children.Add(new Image { Source = LogoArt.Render(px, px <= 24), Width = px, Height = px, Margin = new Thickness(16), VerticalAlignment = VerticalAlignment.Center });
        }

        Save(logo, Path.Combine(dir, "sheet-logo.png"));
        RenderDrawingModes(dir);
        return 0;
    }

    /// <summary>
    /// Окно рисования во всех режимах панели — настоящее <see cref="DrawingWindow"/> (не показывается на экране; его содержимое
    /// раскладывается и рисуется в растр): постоянная панель, автоскрытие у четырёх краёв (раскрытая), только радиальное меню,
    /// с фигурами, стрелкой, текстом (подтверждённым и в процессе ввода).
    /// </summary>
    private static void RenderDrawingModes(string dir)
    {
        var cases = new (string Name, DrawingProfile Profile)[]
        {
            ("docked", DrawingProfile.Default),
            ("docked-moved", DrawingProfile.Default with { ToolbarX = 0.3, ToolbarY = 0.8 }),
            ("autohide-top", DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = ScreenEdge.Top }),
            ("autohide-bottom", DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = ScreenEdge.Bottom }),
            ("autohide-left", DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = ScreenEdge.Left }),
            ("autohide-right", DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.AutoHide, AutoHideEdge = ScreenEdge.Right }),
            ("radial-only", DrawingProfile.Default with { ToolbarMode = DrawToolbarMode.RadialOnly }),
        };
        foreach (var (name, profile) in cases)
        {
            var w = new DrawingWindow(new PixelRect(-32000, -32000, 1100, 620), 1.0, DrawSettings.Default with { Tool = DrawTool.Text }, profile);
            var root = (FrameworkElement)w.Content;
            root.Width = 1100;
            root.Height = 620;
            root.Measure(new Size(1100, 620));
            root.Arrange(new Rect(0, 0, 1100, 620));
            w.ApplyProfile(profile);
            if (profile.ToolbarMode == DrawToolbarMode.AutoHide)
            {
                w.RevealToolbarForPreview();
            }

            w.SetSettings(w.Settings with { Tool = DrawTool.Arrow });
            w.Draw(new Point(200, 420), new Point(470, 300));
            w.SetSettings(w.Settings with { Tool = DrawTool.Rectangle, Color = DrawingWindow.Palette[1], Dash = DrawDash.Dashed });
            w.Draw(new Point(520, 230), new Point(780, 380));
            w.SetSettings(w.Settings with { Tool = DrawTool.Text, Color = DrawingWindow.Palette[4] });
            w.BeginText(new Point(540, 410));
            w.TypeForPreview("Text tool" + Environment.NewLine + "second line");
            w.CommitText();
            w.BeginText(new Point(200, 470));
            w.TypeForPreview("editing…");
            if (profile.ToolbarMode == DrawToolbarMode.RadialOnly)
            {
                w.ShowRadial(new Point(1000, 560)); // у края — сдвигается внутрь
            }

            root.UpdateLayout();
            var stage = new Grid { Width = 1100, Height = 620, Background = new LinearGradientBrush(Color.FromRgb(0x30, 0x34, 0x42), Color.FromRgb(0x5A, 0x60, 0x75), 25) };
            w.Content = null;
            stage.Children.Add(root);
            Save(stage, Path.Combine(dir, $"drawing-{name}.png"));
            stage.Children.Clear();
            w.Close();
        }
    }

    /// <summary>
    /// Крупный объёмный рендер (S = 96) для проверки граней Г-образного Enter и центрирования подписей: покой и нажатие,
    /// со стороной (R) и без, длинные подписи.
    /// </summary>
    private static FrameworkElement VolumetricCloseUp()
    {
        var items = new[]
        {
            new KeycapItem(1, 1, "Ctrl", true, false, KeySide.Left), new KeycapItem(2, 2, "Shift", true, false, KeySide.Left),
            new KeycapItem(3, 3, "Shift", true, false, KeySide.Right), new KeycapItem(4, 4, "Win", true, false, KeySide.Left),
            new KeycapItem(5, 5, "Enter", false, false), new KeycapItem(6, 6, "Space", false, false), new KeycapItem(7, 7, "S", false, false),
            new KeycapItem(8, 8, "PrintScreen", false, false),
        };
        var root = new StackPanel { Background = Brushes.White };
        foreach (var id in new[] { "backpack.volumetric.rose", "backpack.volumetric.mint" })
        {
            var style = BuiltInStyles.All.First(x => x.Id == id);
            var a = StyleOps.ApplyKeycap(AppearanceSettings.Default with { Size = 96 }, style.Keycap!);
            root.Children.Add(Caption($"{id}  ·  S=96", false));
            foreach (bool pressed in new[] { false, true })
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Dark), Margin = new Thickness(0, 0, 0, 4) };
                foreach (var item in items)
                {
                    row.Children.Add(Keycap(a, item with { IsPressed = pressed }));
                }

                root.Children.Add(row);
            }
        }

        return root;
    }

    private static FrameworkElement KeycapSheet(double size)
    {
        var items = new[]
        {
            new KeycapItem(1, 1, "Ctrl", true, false, KeySide.Left), new KeycapItem(2, 2, "Alt", true, false, KeySide.Left),
            new KeycapItem(3, 3, "Win", true, false, KeySide.Left), new KeycapItem(4, 4, "Esc", false, false),
            new KeycapItem(5, 5, "ArrowUp", false, false), new KeycapItem(6, 6, "ArrowLeft", false, false),
            new KeycapItem(7, 7, "Enter", false, false), new KeycapItem(8, 8, "Space", false, false),
            new KeycapItem(9, 9, "Backspace", false, false), new KeycapItem(10, 10, "A", false, false),
        };
        var root = new StackPanel { Background = Brushes.White };
        foreach (var style in BuiltInStyles.All.Where(s => s.Kind == StyleKind.Keycap))
        {
            // Подложка группы видна только у minimal (без неё светлый текст на светлом фоне не читается — так и задумано стилем).
            var a = StyleOps.ApplyKeycap(AppearanceSettings.Default with { Size = size }, style.Keycap!);
            bool groupBackground = a.Renderer == KeycapRenderer.Minimal;
            root.Children.Add(Caption($"{style.Id}  ·  {a.Renderer}  ·  S={size}", false));
            foreach (bool pressed in new[] { false, true })
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var bg in new[] { Light, Dark })
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    foreach (var item in items)
                    {
                        row.Children.Add(Keycap(a, item with { IsPressed = pressed }));
                    }

                    // Код языка в кейкапе — проверка коротких надписей на малых размерах.
                    row.Children.Add(Keycap(a, new KeycapItem(11, 11, "EN", false, pressed)));
                    var group = new Border
                    {
                        Child = row,
                        Background = groupBackground && a.BackgroundEnabled ? a.BackgroundColor.ToFrozenBrush() : null,
                        CornerRadius = new CornerRadius(KeycapMetrics.From(a).GroupCornerRadius),
                        Padding = new Thickness(4),
                    };
                    line.Children.Add(new Border { Child = group, Padding = new Thickness(10), Background = new SolidColorBrush(bg) });
                }

                root.Children.Add(line);
            }
        }

        return root;
    }

    private static FrameworkElement Keycap(AppearanceSettings a, KeycapItem item)
    {
        var k = KeycapFactory.Create(a.Renderer);
        k.Apply(a, item, showBadge: false);
        k.PressOffset = item.IsPressed ? k.PressTravel : 0;
        k.Margin = new Thickness(KeycapMetrics.From(a).KeyGap / 2, 4, KeycapMetrics.From(a).KeyGap / 2, 4);
        return k;
    }

    private static FrameworkElement MouseSheet()
    {
        var root = new StackPanel { Background = Brushes.White };
        var cases = new (string Name, MouseIconState State)[]
        {
            ("idle", new(MouseButtons.None, MouseButtons.None, 0, 0)),
            ("left", new(MouseButtons.Left, MouseButtons.None, 0, 0)),
            ("right", new(MouseButtons.Right, MouseButtons.None, 0, 0)),
            ("middle", new(MouseButtons.Middle, MouseButtons.None, 0, 0)),
            ("X1", new(MouseButtons.X1, MouseButtons.None, 0, 0)),
            ("X2", new(MouseButtons.X2, MouseButtons.None, 0, 0)),
            ("wheel up", new(MouseButtons.None, MouseButtons.None, 1, 0)),
            ("wheel down", new(MouseButtons.None, MouseButtons.None, -1, 0)),
            ("wheel right", new(MouseButtons.None, MouseButtons.None, 0, 1)),
            ("left + wheel", new(MouseButtons.Left, MouseButtons.None, -1, 0)),
            ("unknown", new(MouseButtons.None, MouseButtons.Left | MouseButtons.Middle, 0, 0)),
        };
        foreach (var style in BuiltInStyles.All.Where(s => s.Kind == StyleKind.Mouse))
        {
            root.Children.Add(Caption(style.Id, false));
            foreach (var (side, sideName) in new[] { (SideButtonsMode.Two, "two, left"), (SideButtonsMode.One, "one, left"), (SideButtonsMode.Hidden, "hidden") })
            {
                var look = style.Mouse! with { SideButtons = side };
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var bg in new[] { Light, Dark })
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(bg) };
                    foreach (var (name, st) in cases)
                    {
                        var cell = new StackPanel { Margin = new Thickness(6) };
                        cell.Children.Add(new Border { Background = look.IconBackground.ToFrozenBrush(), CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Child = MouseIcon.Create(48, look, st) });
                        cell.Children.Add(Caption(name, bg == Dark));
                        row.Children.Add(cell);
                    }

                    line.Children.Add(row);
                }

                root.Children.Add(Caption("side buttons: " + sideName, false));
                root.Children.Add(line);
            }

            var right = style.Mouse! with { SideButtonsSide = SideButtonsSide.Right };
            var r = new StackPanel { Orientation = Orientation.Horizontal, Background = new SolidColorBrush(Light) };
            r.Children.Add(Caption("side buttons: two, right", false));
            r.Children.Add(new Border { Background = right.IconBackground.ToFrozenBrush(), CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Margin = new Thickness(6), Child = MouseIcon.Create(48, right, new(MouseButtons.X2, MouseButtons.None, 0, 0)) });
            root.Children.Add(r);
        }

        return root;
    }

    /// <summary>
    /// Phase 4I: фирменные пресеты «Backpack Neon Emerald» настоящим рендером — сочетание с двумя подписями раскладки, метка R,
    /// счётчик повторов; обычное и нажатое состояние; значок мыши во всех состояниях; светлый, тёмный и пёстрый фон.
    /// </summary>
    private static FrameworkElement BrandSheet()
    {
        var busy = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var (c, o) in new[] { (Color.FromRgb(0xF5, 0xC5, 0x42), 0.0), (Color.FromRgb(0x2E, 0x86, 0xDE), 0.3), (Color.FromRgb(0xF8, 0xF8, 0xF8), 0.5), (Color.FromRgb(0x1B, 0x5E, 0x20), 0.7), (Color.FromRgb(0xE9, 0x1E, 0x63), 1.0) })
        {
            busy.GradientStops.Add(new GradientStop(c, o));
        }

        var backgrounds = new (string Name, Brush Brush, bool Dark)[]
        {
            ("light", new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF4)), false),
            ("dark", new SolidColorBrush(Color.FromRgb(0x1C, 0x1D, 0x21)), true),
            ("busy", busy, false),
        };
        var root = new StackPanel { Background = Brushes.White };
        var labelsBefore = Backpack.Visual.Layout.SecondaryLabels.Settings;
        Backpack.Visual.Layout.SecondaryLabels.Settings = labelsBefore with { Mode = SecondaryLabelMode.Off }; // подписи задаются явно
        foreach (double size in new[] { 24.0, 32.0, 48.0 })
        {
            var a = StyleOps.ApplyKeycap(AppearanceSettings.Default with { Size = size }, BrandStyles.Keycap);
            root.Children.Add(Caption($"{BrandStyles.Name} · keycaps · S={size} · glow {a.Glow:0.##}", false));
            foreach (bool pressed in new[] { false, true })
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var (_, brush, _) in backgrounds)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(Keycap(a, new KeycapItem(1, 1, "Ctrl", true, pressed, KeySide.Right)));
                    row.Children.Add(Keycap(a, new KeycapItem(2, 2, "Shift", true, pressed, KeySide.Left)));
                    var k = KeycapFactory.Create(a.Renderer);
                    k.Apply(a, new KeycapItem(3, 3, "K", false, pressed, PressCount: 3, Secondary: "л"), showBadge: true);
                    k.PressOffset = pressed ? k.PressTravel : 0;
                    k.Margin = new Thickness(KeycapMetrics.From(a).KeyGap / 2, 4, KeycapMetrics.From(a).KeyGap / 2, 4);
                    row.Children.Add(k);
                    row.Children.Add(Keycap(a, new KeycapItem(4, 4, "Enter", false, pressed)));
                    row.Children.Add(Keycap(a, new KeycapItem(5, 5, "7", false, pressed, Numpad: true)));
                    line.Children.Add(new Border { Child = row, Padding = new Thickness(KeycapMetrics.From(a).OuterMargin + 8), Background = brush });
                }

                root.Children.Add(line);
            }
        }

        var cases = new (string Name, MouseIconState State)[]
        {
            ("idle", new(MouseButtons.None, MouseButtons.None, 0, 0)), ("left", new(MouseButtons.Left, MouseButtons.None, 0, 0)),
            ("right", new(MouseButtons.Right, MouseButtons.None, 0, 0)), ("middle", new(MouseButtons.Middle, MouseButtons.None, 0, 0)),
            ("wheel up", new(MouseButtons.None, MouseButtons.None, 1, 0)), ("wheel down", new(MouseButtons.None, MouseButtons.None, -1, 0)),
            ("X1", new(MouseButtons.X1, MouseButtons.None, 0, 0)), ("X2", new(MouseButtons.X2, MouseButtons.None, 0, 0)),
            ("unknown", new(MouseButtons.None, MouseButtons.Left | MouseButtons.Middle, 0, 0)),
        };
        foreach (var (side, sideName) in new[] { (SideButtonsMode.Two, "two side buttons"), (SideButtonsMode.One, "one"), (SideButtonsMode.Hidden, "none") })
        {
            root.Children.Add(Caption($"{BrandStyles.Name} · mouse · {sideName}", false));
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (_, brush, dark) in backgrounds)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Background = brush };
                foreach (var (name, st) in cases)
                {
                    var cell = new StackPanel { Margin = new Thickness(6) };
                    cell.Children.Add(new Border { Padding = new Thickness(8), Child = MouseIcon.Create(48, BrandStyles.Mouse with { SideButtons = side }, st) });
                    cell.Children.Add(Caption(name, dark));
                    row.Children.Add(cell);
                }

                line.Children.Add(row);
            }

            root.Children.Add(line);
        }

        Backpack.Visual.Layout.SecondaryLabels.Settings = labelsBefore;
        return root;
    }

    private static TextBlock Caption(string text, bool dark) => new()
    {
        Text = text,
        Margin = new Thickness(8, 4, 8, 4),
        FontSize = 11,
        Foreground = dark ? Brushes.White : Brushes.Black,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Save(FrameworkElement e, string path, double scale = 1)
    {
        e.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        e.Arrange(new Rect(e.DesiredSize));
        e.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth * scale), (int)Math.Ceiling(e.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(e);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
