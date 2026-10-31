using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyOverlay.Visual.Localization;

namespace KeyOverlay.Visual.Presentation;

/// <summary>Пункты прежнего плоского меню — сохранены для совместимости (тесты, самопроверка, снимки справки).</summary>
public enum RadialItem { Pen, Line, Arrow, Rectangle, Ellipse, Text, Color, Undo, Redo, Clear, Screenshot, Exit }

/// <summary>
/// Пункт радиального меню: действие (<see cref="Execute"/>) или категория с дочерними пунктами (<see cref="Children"/> — кольцо
/// второго уровня). <see cref="IsAvailable"/> зависит от контекста (выключенный инструмент, нет плагина); недоступный пункт
/// виден, но не выбирается. <see cref="Swatch"/> — образец цвета вместо иконки.
/// </summary>
public sealed record RadialNode(
    string Id,
    string Title,
    string Icon,
    Action? Execute = null,
    IReadOnlyList<RadialNode>? Children = null,
    Func<bool>? IsAvailable = null,
    Func<bool>? IsChecked = null,
    Color? Swatch = null,
    RadialItem? Legacy = null)
{
    public bool Available => IsAvailable?.Invoke() ?? true;

    public bool HasChildren => Children is { Count: > 0 };
}

/// <summary>
/// Радиальное меню с вложенными кольцами (Phase 4E). Внутреннее кольцо — основные инструменты и категории; выбор категории
/// раскрывает внешнее кольцо с её действиями (выбранный сектор остаётся подсвеченным). Центр: при открытом внешнем кольце —
/// «назад» на уровень, иначе — закрыть меню. Esc закрывает последовательно: внешнее кольцо, затем меню.
/// <list type="bullet">
/// <item>щелчок срабатывает только если нажатие и отпускание пришлись на один и тот же сектор — соседний пункт случайно
///       не выполняется; между секторами — зазор;</item>
/// <item>клавиатура: стрелки — по кругу внутри активного кольца, Enter/Пробел — выбрать, Backspace/Esc — назад;</item>
/// <item>у края экрана центр сдвигается так, чтобы оба кольца целиком помещались; на маленькой области меню уменьшается.</item>
/// </list>
/// Каталог пунктов строит владелец (окно рисования, приложение — из общего реестра команд), поэтому сочетания, трей и меню
/// вызывают одну и ту же реализацию команды.
/// </summary>
public sealed class RadialMenu : Canvas
{
    /// <summary>Внешний радиус внутреннего кольца.</summary>
    public const double Radius = 136;

    /// <summary>Радиус центральной кнопки.</summary>
    public const double Inner = 42;

    /// <summary>Толщина внешнего кольца и зазор до него.</summary>
    public const double OuterWidth = 70, OuterGap = 6;

    /// <summary>Полный радиус с внешним кольцом — по нему меню удерживается внутри экрана.</summary>
    public const double FullRadius = Radius + OuterGap + OuterWidth;

    private const double SectorGapDegrees = 1.4;

    private static readonly Brush IdleFill = Frozen(Color.FromArgb(0xF0, 0x1E, 0x22, 0x21));
    private static readonly Brush HoverFill = Frozen(Color.FromArgb(0xFF, 0x2E, 0x35, 0x33));
    private static readonly Brush SelectedFill = Frozen(Color.FromArgb(0xFF, 0x0B, 0x6B, 0x4D));
    private static readonly Brush FocusRing = Frozen(Color.FromRgb(0x31, 0xC9, 0x9A));
    private static readonly Brush Edge = Frozen(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Ink = Frozen(Color.FromRgb(0xF2, 0xF5, 0xF3));

    private readonly TranslateTransform _at = new();
    private readonly ScaleTransform _scale = new(1, 1, FullRadius, FullRadius);
    private readonly IReadOnlyList<RadialNode> _root;
    private readonly Canvas _innerLayer = new();
    private readonly Canvas _outerLayer = new();
    private readonly Grid _center = new();
    private readonly TextBlock _caption = new() { Foreground = Ink, FontSize = 12.5, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
    private readonly List<(RadialNode Node, Path Wedge, int Ring)> _sectors = new();
    private Path? _pressed;
    private int _selectedRoot = -1;
    private int _keyIndex;
    private bool _keyInOuter;

    public RadialMenu()
        : this(DefaultModel())
    {
    }

    public RadialMenu(IReadOnlyList<RadialNode> root)
    {
        _root = root;
        Width = Height = 2 * FullRadius;
        var transform = new TransformGroup();
        transform.Children.Add(_scale);
        transform.Children.Add(_at);
        RenderTransform = transform;
        Cursor = Cursors.Hand;
        Focusable = true;
        Children.Add(_outerLayer);
        Children.Add(_innerLayer);
        BuildCenter();
        Children.Add(_center);
        Children.Add(_caption);
        BuildInner();
        KeyDown += OnKeyDown;
        System.Windows.Automation.AutomationProperties.SetName(this, Loc.T("Radial.Name"));
    }

    /// <summary>Выбран пункт прежнего плоского меню (совместимость: окно рисования выполняет действие).</summary>
    public event Action<RadialItem>? Chosen;

    /// <summary>Выполнен пункт каталога (меню закрывается владельцем).</summary>
    public event Action<RadialNode>? Executed;

    public event Action? Dismissed;

    public Point Center => new(_at.X + FullRadius, _at.Y + FullRadius);

    /// <summary>Открыто внешнее кольцо (Esc сначала закрывает его).</summary>
    public bool SubRingOpen => _selectedRoot >= 0 && _root[_selectedRoot].HasChildren;

    /// <summary>Выбранная категория внутреннего кольца (−1 — нет).</summary>
    public RadialNode? SelectedCategory => _selectedRoot >= 0 ? _root[_selectedRoot] : null;

    public IReadOnlyList<RadialNode> Root => _root;

    /// <summary>Масштаб меню (меньше 1 — на маленькой области).</summary>
    public double Scale => _scale.ScaleX;

    public static IReadOnlyList<RadialItem> Order => Enum.GetValues<RadialItem>();

    public void Place(Point center, double scale = 1)
    {
        _scale.ScaleX = _scale.ScaleY = scale;
        _at.X = center.X - FullRadius;
        _at.Y = center.Y - FullRadius;
    }

    /// <summary>
    /// Центр меню у края экрана сдвигается внутрь, чтобы оба кольца были видны и доступны. Если область меньше меню, оно
    /// уменьшается (масштаб) и ставится в центр области.
    /// </summary>
    public static (Point Center, double Scale) Fit(Point p, Size area)
    {
        double need = FullRadius + 4;
        double scale = Math.Min(1, Math.Min(area.Width, area.Height) / (2 * need));
        double r = need * scale;
        return (new Point(
            area.Width < 2 * r ? area.Width / 2 : Math.Clamp(p.X, r, area.Width - r),
            area.Height < 2 * r ? area.Height / 2 : Math.Clamp(p.Y, r, area.Height - r)), scale);
    }

    /// <summary>Совместимость: только позиция (масштаб — <see cref="Fit"/>).</summary>
    public static Point Clamp(Point p, Size area) => Fit(p, area).Center;

    /// <summary>Выбрать пункт прежнего меню программно (тесты, самопроверка).</summary>
    public void Choose(RadialItem item) => Chosen?.Invoke(item);

    public void Dismiss() => Dismissed?.Invoke();

    /// <summary>Назад на уровень: закрыть внешнее кольцо; если его нет — закрыть меню.</summary>
    public void Back()
    {
        if (_selectedRoot >= 0)
        {
            _selectedRoot = -1;
            _keyInOuter = false;
            BuildOuter();
            Refresh();
            return;
        }

        Dismiss();
    }

    /// <summary>Сбросить уровень (при каждом открытии меню начинается с внутреннего кольца).</summary>
    public void ResetLevel()
    {
        _selectedRoot = -1;
        _keyInOuter = false;
        _keyIndex = 0;
        BuildOuter();
        Refresh();
    }

    /// <summary>Активировать пункт по идентификатору (тесты и клавиатура): категория раскрывает кольцо, действие выполняется.</summary>
    public bool Activate(string id)
    {
        int ri = IndexOf(_root, id);
        if (ri >= 0)
        {
            Activate(_root[ri], ring: 0);
            return true;
        }

        if (_selectedRoot >= 0 && _root[_selectedRoot].Children is { } kids && IndexOf(kids, id) is var ci and >= 0)
        {
            Activate(kids[ci], ring: 1);
            return true;
        }

        foreach (var (node, i) in _root.Select((n, i) => (n, i)))
        {
            if (node.Children is { } c && IndexOf(c, id) is var k and >= 0)
            {
                OpenCategory(i);
                Activate(c[k], ring: 1);
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<RadialNode> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void Activate(RadialNode node, int ring)
    {
        if (!node.Available)
        {
            return;
        }

        if (node.HasChildren)
        {
            OpenCategory(IndexOf(_root, node.Id));
            return;
        }

        if (node.Legacy is { } legacy && node.Execute is null)
        {
            Chosen?.Invoke(legacy);
            return;
        }

        node.Execute?.Invoke();
        Executed?.Invoke(node);
    }

    private void OpenCategory(int index)
    {
        _selectedRoot = index;
        _keyInOuter = true;
        _keyIndex = 0;
        BuildOuter();
        Refresh();
    }

    // ── построение ───────────────────────────────────────────────────────────

    private void BuildCenter()
    {
        var disc = new Ellipse { Width = 2 * (Inner - 3), Height = 2 * (Inner - 3), Fill = Frozen(Color.FromArgb(0xF5, 0x17, 0x1B, 0x1A)), Stroke = Edge, StrokeThickness = 1 };
        _center.Children.Add(disc);
        _center.Width = _center.Height = 2 * (Inner - 3);
        SetLeft(_center, FullRadius - Inner + 3);
        SetTop(_center, FullRadius - Inner + 3);
        _center.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Back();
        };
        System.Windows.Automation.AutomationProperties.SetName(_center, Loc.T("Draw.CloseMenu"));
    }

    private void BuildInner()
    {
        _innerLayer.Children.Clear();
        _sectors.RemoveAll(s => s.Ring == 0);
        AddRing(_root, 0, Inner, Radius, 0, 360);
    }

    private void BuildOuter()
    {
        _outerLayer.Children.Clear();
        _sectors.RemoveAll(s => s.Ring == 1);
        if (_selectedRoot < 0 || _root[_selectedRoot].Children is not { Count: > 0 } kids)
        {
            return;
        }

        // Внешнее кольцо — веер с центром на выбранной категории (не больше полного круга).
        double step = 360.0 / _root.Count;
        double center = -90 + (_selectedRoot * step);
        double span = Math.Min(360, Math.Max(step * 1.6, kids.Count * 34));
        AddRing(kids, 1, Radius + OuterGap, FullRadius, center - (span / 2), span);
    }

    private void AddRing(IReadOnlyList<RadialNode> nodes, int ring, double r0, double r1, double start, double span)
    {
        var layer = ring == 0 ? _innerLayer : _outerLayer;
        double step = span / nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            double a0 = (ring == 0 ? -90 - (step / 2) : start) + (i * step), a1 = a0 + step;
            var wedge = new Path
            {
                Data = Wedge(r0, r1, a0 + (SectorGapDegrees / 2), a1 - (SectorGapDegrees / 2)),
                Fill = IdleFill,
                Stroke = Edge,
                StrokeThickness = 1,
                ToolTip = node.Title,
                Tag = node,
            };
            System.Windows.Automation.AutomationProperties.SetName(wedge, node.Title);
            wedge.MouseEnter += (_, _) => Refresh(hover: wedge);
            wedge.MouseLeave += (_, _) => Refresh();
            wedge.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _pressed = wedge;
            };
            wedge.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                bool same = ReferenceEquals(_pressed, wedge);
                _pressed = null;
                if (same)
                {
                    Activate(node, ring);
                }
            };
            layer.Children.Add(wedge);
            _sectors.Add((node, wedge, ring));

            double mid = (a0 + a1) / 2 * Math.PI / 180, rr = (r0 + r1) / 2;
            FrameworkElement glyph = node.Swatch is { } sw
                ? new Ellipse { Width = 20, Height = 20, Fill = Frozen(sw), Stroke = Ink, StrokeThickness = 1.5 }
                : Icons.Lucide.Icon(node.Icon, ring == 0 ? 22 : 19, Ink);
            glyph.IsHitTestVisible = false;
            glyph.Opacity = node.Available ? 1 : 0.35;
            double size = node.Swatch is not null ? 20 : ring == 0 ? 22 : 19;
            SetLeft(glyph, FullRadius + (rr * Math.Cos(mid)) - (size / 2));
            SetTop(glyph, FullRadius + (rr * Math.Sin(mid)) - (size / 2) - (node.HasChildren ? 4 : 0));
            layer.Children.Add(glyph);
            if (node.HasChildren)
            {
                // Признак категории: маленькая стрелка наружу под иконкой.
                var chevron = new Path { Data = Geometry.Parse("M0,0 L3,3 L6,0"), Stroke = Ink, StrokeThickness = 1.4, Opacity = 0.7, IsHitTestVisible = false };
                SetLeft(chevron, FullRadius + (rr * Math.Cos(mid)) - 3);
                SetTop(chevron, FullRadius + (rr * Math.Sin(mid)) + 10);
                layer.Children.Add(chevron);
            }
        }
    }

    /// <summary>Подсветка: выбранная категория — акцентом, наведение — светлее, фокус клавиатуры — рамкой; подпись в центре.</summary>
    private void Refresh(Path? hover = null)
    {
        Path? keyWedge = null;
        var ring = _keyInOuter ? _sectors.Where(s => s.Ring == 1).ToList() : _sectors.Where(s => s.Ring == 0).ToList();
        if (ring.Count > 0)
        {
            keyWedge = ring[Math.Clamp(_keyIndex, 0, ring.Count - 1)].Wedge;
        }

        foreach (var (node, wedge, r) in _sectors)
        {
            bool selected = r == 0 && _selectedRoot >= 0 && ReferenceEquals(_root[_selectedRoot], node);
            bool isChecked = node.IsChecked?.Invoke() == true;
            wedge.Fill = selected || isChecked ? SelectedFill : ReferenceEquals(wedge, hover) && node.Available ? HoverFill : IdleFill;
            wedge.Stroke = ReferenceEquals(wedge, keyWedge) && IsKeyboardFocusWithin ? FocusRing : Edge;
            wedge.StrokeThickness = ReferenceEquals(wedge, keyWedge) && IsKeyboardFocusWithin ? 2 : 1;
            wedge.Opacity = node.Available ? 1 : 0.55;
            wedge.Cursor = node.Available ? Cursors.Hand : Cursors.No;
        }

        var shown = hover?.Tag as RadialNode ?? (keyWedge?.Tag as RadialNode) ?? SelectedCategory;
        _caption.Text = shown?.Title ?? "";
        _caption.Width = 2 * Inner + 120;
        _caption.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_caption, FullRadius - (_caption.Width / 2));
        SetTop(_caption, FullRadius + Inner + 2);
        _caption.Visibility = Visibility.Collapsed; // подпись — во всплывающей подсказке; в центре — иконка «назад»/«закрыть»
        UpdateCenterGlyph();
    }

    private FrameworkElement? _centerGlyph;

    private void UpdateCenterGlyph()
    {
        if (_centerGlyph is not null)
        {
            _center.Children.Remove(_centerGlyph);
        }

        _centerGlyph = Icons.Lucide.Icon(_selectedRoot >= 0 ? "arrow-left" : "x", 18, Ink);
        _centerGlyph.IsHitTestVisible = false;
        _centerGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _centerGlyph.VerticalAlignment = VerticalAlignment.Center;
        _center.Children.Add(_centerGlyph);
        _center.ToolTip = _selectedRoot >= 0 ? Loc.T("Radial.Back") : Loc.T("Draw.CloseMenu");
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var ring = _keyInOuter && _selectedRoot >= 0 ? _root[_selectedRoot].Children! : _root;
        switch (e.Key)
        {
            case Key.Right or Key.Down:
                _keyIndex = (_keyIndex + 1) % ring.Count;
                break;
            case Key.Left or Key.Up:
                _keyIndex = (_keyIndex - 1 + ring.Count) % ring.Count;
                break;
            case Key.Enter or Key.Space:
                Activate(ring[Math.Clamp(_keyIndex, 0, ring.Count - 1)], _keyInOuter ? 1 : 0);
                break;
            case Key.Back:
                Back();
                break;
            default:
                return;
        }

        e.Handled = true;
        Refresh();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Refresh();
    }

    private static Geometry Wedge(double r0, double r1, double a0, double a1)
    {
        Point P(double r, double a) => new(FullRadius + (r * Math.Cos(a * Math.PI / 180)), FullRadius + (r * Math.Sin(a * Math.PI / 180)));
        bool large = a1 - a0 > 180;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(P(r0, a0), true, true);
            c.LineTo(P(r1, a0), true, false);
            c.ArcTo(P(r1, a1), new Size(r1, r1), 0, large, SweepDirection.Clockwise, true, false);
            c.LineTo(P(r0, a1), true, false);
            c.ArcTo(P(r0, a0), new Size(r0, r0), 0, large, SweepDirection.Counterclockwise, true, false);
        }

        g.Freeze();
        return g;
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>Каталог по умолчанию (только рисование) — для окон без приложения: тесты, превью, снимки справки.</summary>
    public static IReadOnlyList<RadialNode> DefaultModel() =>
    [
        new("cat.draw", Loc.T("Draw.Category.Draw"), "pencil", Children: [new("draw.pen", Loc.T("Draw.Tool.Pen"), "pencil", Legacy: RadialItem.Pen)]),
        new("cat.shapes", Loc.T("Draw.Category.Shapes"), "shapes", Children:
        [
            new("shape.line", Loc.T("Draw.Tool.Line"), "slash", Legacy: RadialItem.Line), new("shape.arrow", Loc.T("Draw.Tool.Arrow"), "move-up-right", Legacy: RadialItem.Arrow),
            new("shape.rect", Loc.T("Draw.Tool.Rectangle"), "square", Legacy: RadialItem.Rectangle), new("shape.ellipse", Loc.T("Draw.Tool.Ellipse"), "circle", Legacy: RadialItem.Ellipse),
        ]),
        new("annotate.text", Loc.T("Draw.Tool.Text"), "type", Legacy: RadialItem.Text),
        new("color.next", Loc.T("Draw.NextColor"), "palette", Legacy: RadialItem.Color),
        new("edit.undo", Loc.T("Draw.Undo"), "undo-2", Legacy: RadialItem.Undo),
        new("edit.redo", Loc.T("Draw.Redo"), "redo-2", Legacy: RadialItem.Redo),
        new("edit.clear", Loc.T("Draw.Clear"), "eraser", Legacy: RadialItem.Clear),
        new("app.screenshot", Loc.T("Draw.Screenshot"), "camera", Legacy: RadialItem.Screenshot),
        new("app.exit", Loc.T("Draw.Exit"), "power", Legacy: RadialItem.Exit),
    ];
}
