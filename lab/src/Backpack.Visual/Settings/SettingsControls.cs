using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Backpack.Visual.Localization;

namespace Backpack.Visual.Settings;

/// <summary>
/// Группа кнопок: одинаковые интервалы 8 DIP по горизонтали и по вертикали; при нехватке ширины кнопки переносятся на следующую
/// строку (без наложения и обрезки). Используется для всех групп действий окна.
/// </summary>
public sealed class ButtonBar : WrapPanel
{
    public const double Gap = 8;

    protected override void OnVisualChildrenChanged(DependencyObject? visualAdded, DependencyObject? visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualAdded is FrameworkElement e)
        {
            e.Margin = new Thickness(0, 0, Gap, Gap);
        }
    }
}

/// <summary>Значки операций (Lucide): одна операция — один значок во всей программе (стили, профили, плагины, превью).</summary>
public static class ActionIcons
{
    public const string Save = "save", SaveAsNew = "copy-plus", Duplicate = "copy", Rename = "text-cursor-input", Delete = "trash",
        Restore = "rotate-ccw", Import = "file-down", Export = "file-up", OpenFolder = "folder-open", More = "ellipsis",
        ResetSection = "list-restart", Details = "info", Settings = "settings-2", Pause = "pause", Play = "play", Disable = "power",
        Update = "refresh-cw", Remove = "x", Clear = "eraser";
}

/// <summary>Построение строк настроек в общем стиле (<see cref="SettingsTheme"/>).</summary>
public sealed class SettingsUi(SettingsTheme theme)
{
    private static int s_groups;

    public SettingsTheme Theme => theme;

    public Border Card(string title, string? hint, params UIElement[] rows)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Style = theme.StyleOf("Ko.H2") });
        if (hint is not null)
        {
            panel.Children.Add(new TextBlock { Text = hint, Style = theme.StyleOf("Ko.Hint"), Margin = new Thickness(0, -4, 0, 8) });
        }

        foreach (var r in rows)
        {
            panel.Children.Add(r);
        }

        return new Border { Style = theme.StyleOf("Ko.Card"), Child = panel };
    }

    /// <summary>
    /// Строка: подпись и пояснение слева, элемент справа; в узкой карточке элемент переносится под подпись
    /// (<see cref="AdaptiveRow"/>). Подпись связана с элементом для экранного диктора.
    /// </summary>
    public AdaptiveRow Row(string label, string? hint, FrameworkElement control)
    {
        var g = new AdaptiveRow { Margin = new Thickness(0, 6, 0, 6) };
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = label, Style = theme.StyleOf("Ko.Label") });
        TextBlock? hintBlock = null;
        if (hint is not null)
        {
            hintBlock = new TextBlock { Text = hint, Style = theme.StyleOf("Ko.Hint") };
            left.Children.Add(hintBlock);
        }

        System.Windows.Automation.AutomationProperties.SetName(control, label);
        control.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(left);
        g.Children.Add(control);
        g.Tag = hintBlock;
        return g;
    }

    public TextBlock Note(string text) => new() { Text = text, Style = theme.StyleOf("Ko.Hint"), Margin = new Thickness(0, 6, 0, 0) };

    public CheckBox Toggle(bool value, Action<bool> set)
    {
        var cb = new CheckBox { IsChecked = value, Style = theme.StyleOf("Ko.Toggle") };
        cb.Checked += (_, _) => set(true);
        cb.Unchecked += (_, _) => set(false);
        return cb;
    }

    public Button Button(string text, Action click, bool primary = false)
    {
        var b = new Button { Content = text, Style = theme.StyleOf(primary ? "Ko.PrimaryButton" : "Ko.Button") };
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>
    /// Иконочная кнопка действия панели инструментов (Phase 4H): квадрат 34×34, локализованная подсказка (с сочетанием и
    /// причиной недоступности), доступное имя; подсказка показывается и на недоступной кнопке, и при клавиатурном фокусе.
    /// <paramref name="primary"/> — рекомендуемое сейчас действие (кнопка с рамкой и значком акцента); <paramref name="danger"/>
    /// — разрушительное (значок цвета ошибки; ставится отдельной группой в конце панели).
    /// </summary>
    public Button Action(string icon, string text, Action click, bool enabled = true, string? disabledReason = null, string? hotkey = null,
        bool primary = false, bool danger = false)
    {
        var b = new Button
        {
            Content = Icons.Lucide.Icon(icon, 18, danger ? "Ko.ErrorBrush" : primary ? "Ko.LogoBrush" : "Ko.TextBrush"),
            Style = theme.StyleOf(primary ? "Ko.IconButton" : "Ko.SubtleIconButton"),
            IsEnabled = enabled,
            Margin = new Thickness(0, 0, 4, 4),
            Uid = icon,
        };
        string tip = hotkey is { Length: > 0 } ? $"{text} ({hotkey})" : text;
        if (!enabled && disabledReason is { Length: > 0 })
        {
            tip += Environment.NewLine + disabledReason;
            System.Windows.Automation.AutomationProperties.SetHelpText(b, disabledReason);
        }

        b.ToolTip = tip;
        ToolTipService.SetShowOnDisabled(b, true);
        ToolTipService.SetShowsToolTipOnKeyboardFocus(b, true);
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Панель действий: группы по смыслу через тонкий разделитель; при нехватке места группы переносятся целиком.</summary>
    public WrapPanel Toolbar(params IReadOnlyList<UIElement>[] groups)
    {
        var bar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        bool first = true;
        foreach (var group in groups.Where(g => g.Count > 0))
        {
            var g = new StackPanel { Orientation = Orientation.Horizontal };
            if (!first)
            {
                var sep = new Border { Width = 1, Height = 22, Margin = new Thickness(4, 6, 8, 10), VerticalAlignment = VerticalAlignment.Center, Tag = "separator" };
                sep.SetResourceReference(Border.BackgroundProperty, "Ko.BorderBrush");
                g.Children.Add(sep);
            }

            foreach (var e in group)
            {
                g.Children.Add(e);
            }

            bar.Children.Add(g);
            first = false;
        }

        return bar;
    }

    /// <summary>
    /// Панель действий с меню «Ещё»: если все группы не помещаются в одну строку, редкие действия (<paramref name="rare"/>)
    /// уходят в меню «…» с текстовыми названиями и значками (недоступные пункты там тоже недоступны), а частые и
    /// разрушительные (<paramref name="last"/>, отдельной группой в конце) остаются на панели. Кнопки не уменьшаются.
    /// </summary>
    public WrapPanel Toolbar(IReadOnlyList<UIElement>[] groups, IReadOnlyList<Button> rare, IReadOnlyList<UIElement> last)
    {
        var more = Action(ActionIcons.More, Loc.T("Common.More"), () => { });
        more.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = more, Placement = PlacementMode.Bottom };
            foreach (var b in rare)
            {
                var item = new MenuItem
                {
                    Header = System.Windows.Automation.AutomationProperties.GetName(b),
                    Icon = new ContentControl { Content = Icons.Lucide.Icon(IconOf(b), 16, "Ko.TextBrush"), Opacity = b.IsEnabled ? 1 : 0.45, Focusable = false },
                    IsEnabled = b.IsEnabled,
                };
                item.Click += (_, _) => b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, b));
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        };
        foreach (var b in rare)
        {
            b.Tag ??= "rare";
        }

        var bar = Toolbar([.. groups, rare.Cast<UIElement>().ToList(), [more], last]);
        var rareGroup = (FrameworkElement)bar.Children[groups.Count(g => g.Count > 0)];
        var moreGroup = (FrameworkElement)bar.Children[groups.Count(g => g.Count > 0) + 1];
        moreGroup.Visibility = Visibility.Collapsed;
        double Full()
        {
            double w = 0;
            foreach (FrameworkElement g in bar.Children)
            {
                if (g == moreGroup)
                {
                    continue;
                }

                foreach (UIElement c in ((Panel)g).Children)
                {
                    c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    w += c.DesiredSize.Width + (c is FrameworkElement fe ? fe.Margin.Left + fe.Margin.Right : 0);
                }
            }

            return w;
        }

        bar.SizeChanged += (_, e) =>
        {
            // Решение по ширине всех групп в одну строку — без колебаний при смене видимости.
            bool narrow = e.NewSize.Width > 0 && Full() > e.NewSize.Width;
            rareGroup.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            moreGroup.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        };
        return bar;
    }

    /// <summary>Имя значка иконочной кнопки <see cref="Action"/> (хранится в <c>Uid</c>).</summary>
    private static string IconOf(Button b) => b.Uid;

    /// <summary>Квадратная иконочная кнопка 34×34 (скругление 8); подпись — подсказка и имя для экранного диктора.</summary>
    public Button IconButton(string icon, string tip, Action click, bool subtle = false)
    {
        var b = new Button
        {
            Content = Icons.Lucide.Icon(icon, 18, "Ko.TextBrush"),
            Style = theme.StyleOf(subtle ? "Ko.SubtleIconButton" : "Ko.IconButton"),
            ToolTip = tip,
        };
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Кнопка с иконкой и текстом (та же форма, что у обычной кнопки).</summary>
    public Button Button(string icon, string text, Action click, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Icons.Lucide.Icon(icon, 16, primary ? "Ko.AccentTextBrush" : "Ko.TextBrush");
        glyph.Margin = new Thickness(0, 0, 8, 0);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(glyph);
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var b = Button("", click, primary);
        b.Content = content;
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        return b;
    }

    /// <summary>
    /// Выбор шрифта (Phase 4F): «Стандартный» (null — штатный шрифт области) или встроенный «Spectral». Каждый вариант показан
    /// своим шрифтом. Сегменты, а не список: вариантов два, и оба видны сразу.
    /// </summary>
    public Border FontPicker(string? current, Action<string?> set)
    {
        var choices = FontSettings.Choices;
        int index = Math.Max(0, choices.ToList().IndexOf(FontSettings.NormalizeName(current)));
        var host = Segmented(index, [Loc.T("Fonts.Standard"), FontSettings.Spectral], i => set(choices[i]));
        if (host.Child is Panel panel)
        {
            for (int i = 0; i < panel.Children.Count && i < choices.Count; i++)
            {
                ((Control)panel.Children[i]).FontFamily = UiTheme.UiFontFamily(choices[i]);
            }
        }

        return host;
    }

    /// <summary>
    /// Сегментированный выбор из нескольких вариантов (клавиатура: Tab к группе, стрелки внутри). При нехватке ширины варианты
    /// переносятся на следующую строку внутри той же подложки.
    /// </summary>
    public Border Segmented(int index, string[] options, Action<int> set)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        string group = "seg" + Interlocked.Increment(ref s_groups).ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < options.Length; i++)
        {
            int n = i;
            var rb = new RadioButton { Content = options[i], GroupName = group, IsChecked = i == index, Style = theme.StyleOf("Ko.Segment") };
            rb.Checked += (_, _) => set(n);
            panel.Children.Add(rb);
        }

        return new Border { Style = theme.StyleOf("Ko.SegmentHost"), Child = panel };
    }

    public NumberSlider Number(double value, (double Min, double Max) range, double step, string unit, Action<double> set) =>
        new(theme, value, range, step, unit, set);

    public ColorField Color(ThemeColor value, bool allowAlpha, Action<ThemeColor> set) => new(theme, value, allowAlpha, set);

    public UniformGrid AnchorPicker(ScreenAnchor current, Action<ScreenAnchor> set)
    {
        var grid = new UniformGrid { Rows = 3, Columns = 3, Width = 132, Height = 96 };
        string group = "anchor" + Interlocked.Increment(ref s_groups).ToString(CultureInfo.InvariantCulture);
        string[] arrows = ["↖", "↑", "↗", "←", "•", "→", "↙", "↓", "↘"];
        string[] names = [Loc.T("Anchor.TopLeft"), Loc.T("Anchor.TopCenter"), Loc.T("Anchor.TopRight"), Loc.T("Anchor.CenterLeft"), Loc.T("Anchor.Center"), Loc.T("Anchor.CenterRight"), Loc.T("Anchor.BottomLeft"), Loc.T("Anchor.BottomCenter"), Loc.T("Anchor.BottomRight")];
        for (int i = 0; i < 9; i++)
        {
            var a = (ScreenAnchor)i;
            var rb = new RadioButton
            {
                Content = arrows[i],
                GroupName = group,
                IsChecked = a == current,
                Style = theme.StyleOf("Ko.Segment"),
                Margin = new Thickness(1),
                ToolTip = names[i],
                FontSize = 15,
            };
            System.Windows.Automation.AutomationProperties.SetName(rb, names[i]);
            rb.Checked += (_, _) => set(a);
            grid.Children.Add(rb);
        }

        return grid;
    }
}

/// <summary>Ползунок и числовое поле рядом; ввод проверяется и ограничивается диапазоном, применяется по Enter или при уходе фокуса.</summary>
public sealed class NumberSlider : StackPanel
{
    private readonly Slider _slider;
    private readonly TextBox _box;
    private readonly (double Min, double Max) _range;
    private readonly double _step;
    private readonly Action<double> _set;
    private bool _syncing;

    /// <summary>Естественная ширина ползунка; в узкой строке он сужается до <see cref="MinSliderWidth"/>.</summary>
    public const double SliderWidth = 190, MinSliderWidth = 80;

    public NumberSlider(SettingsTheme theme, double value, (double Min, double Max) range, double step, string unit, Action<double> set)
    {
        Orientation = Orientation.Horizontal;
        _range = range;
        _step = step;
        _set = set;
        _slider = new Slider
        {
            Minimum = range.Min,
            Maximum = range.Max,
            Value = Math.Clamp(value, range.Min, range.Max),
            SmallChange = step,
            LargeChange = step * 10,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
            Width = SliderWidth,
            Style = theme.StyleOf("Ko.Slider"),
        };
        _box = new TextBox { Width = 72, Margin = new Thickness(10, 0, 6, 0), Style = theme.StyleOf("Ko.TextBox"), TextAlignment = TextAlignment.Right };
        var unitText = new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center, Width = 34, Style = theme.StyleOf("Ko.Hint"), Margin = new Thickness(0) };
        Children.Add(_slider);
        Children.Add(_box);
        Children.Add(unitText);
        ShowText(_slider.Value);

        _slider.ValueChanged += (_, e) =>
        {
            if (_syncing)
            {
                return;
            }

            ShowText(e.NewValue);
            _set(Math.Round(e.NewValue, 4));
        };
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
        _box.LostKeyboardFocus += (_, _) => Commit();
    }

    public double Value => _slider.Value;

    /// <summary>Ползунок занимает оставшуюся ширину: поле и единица измерения всегда видны, ничего не выходит за строку.</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        double rest = 0;
        foreach (UIElement c in Children)
        {
            if (!ReferenceEquals(c, _slider) && c is FrameworkElement fe)
            {
                rest += (double.IsNaN(fe.Width) ? 0 : fe.Width) + fe.Margin.Left + fe.Margin.Right;
            }
        }

        double w = double.IsPositiveInfinity(constraint.Width) ? SliderWidth : Math.Clamp(constraint.Width - rest, MinSliderWidth, SliderWidth);
        if (Math.Abs(_slider.Width - w) > 0.5)
        {
            _slider.Width = w;
        }

        return base.MeasureOverride(constraint);
    }

    public Slider Slider => _slider;

    public TextBox Box => _box;

    /// <summary>Ввод текста: число в текущей или инвариантной культуре; вне диапазона — ограничивается.</summary>
    public void Commit()
    {
        // Принимается и запятая, и точка (uk/ru пишут дробь через запятую, en — через точку); группировка разрядов не используется.
        string t = _box.Text.Trim().Replace(" ", "").Replace(" ", "").Replace(',', '.');
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || !double.IsFinite(v))
        {
            ShowText(_slider.Value); // неверный ввод — вернуть текущее значение
            return;
        }

        v = Math.Clamp(Math.Round(v / _step) * _step, _range.Min, _range.Max);
        _syncing = true;
        _slider.Value = v;
        _syncing = false;
        ShowText(v);
        _set(Math.Round(v, 4));
    }

    private void ShowText(double v) => _box.Text = v.ToString(_step < 1 ? "0.##" : "0", Loc.Culture);
}

/// <summary>
/// Выбор цвета: поле HEX (#RRGGBB или #RRGGBBAA, альфа последней) и образец, открывающий палитру
/// (насыщенность/яркость, оттенок, прозрачность, готовые цвета). Всё доступно с клавиатуры.
/// </summary>
public sealed class ColorField : StackPanel
{
    private static readonly string[] Presets =
    [
        "#FFFFFF", "#E5E5E7", "#8E8E95", "#3A3A40", "#111113", "#2F6FEB", "#22C55E", "#FFD400", "#F97316", "#E11D48",
    ];

    private readonly TextBox _hex;
    private readonly Border _swatch;
    private readonly Button _open;
    private readonly bool _allowAlpha;
    private readonly Action<ThemeColor> _set;
    private readonly SettingsTheme _theme;
    private ThemeColor _value;
    private bool _updating;

    public ColorField(SettingsTheme theme, ThemeColor value, bool allowAlpha, Action<ThemeColor> set)
    {
        _theme = theme;
        _allowAlpha = allowAlpha;
        _set = set;
        _value = value;
        Orientation = Orientation.Horizontal;

        _swatch = new Border { Width = 26, Height = 20, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Background = Checker() };
        _swatch.SetResourceReference(Border.BorderBrushProperty, "Ko.ControlBorderBrush");
        var swatchInner = new Border { CornerRadius = new CornerRadius(4) };
        _swatch.Child = swatchInner;
        _open = new Button { Style = theme.StyleOf("Ko.Button"), Padding = new Thickness(6, 4, 6, 4), Content = _swatch, ToolTip = Loc.T("Color.OpenPalette") };
        System.Windows.Automation.AutomationProperties.SetName(_open, Loc.T("Color.OpenPalette"));
        _hex = new TextBox { Width = 122, Style = theme.StyleOf("Ko.TextBox"), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 0, 0, 0) };
        Children.Add(_open);
        Children.Add(_hex);
        _open.Click += (_, _) => OpenPopup();
        _hex.TextChanged += (_, _) => OnHexChanged(commit: false);
        _hex.LostKeyboardFocus += (_, _) => OnHexChanged(commit: true);
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OnHexChanged(commit: true);
                e.Handled = true;
            }
        };
        Show(value, updateText: true);
    }

    public ThemeColor Value => _value;

    public TextBox Hex => _hex;

    public void SetFromPicker(ThemeColor c)
    {
        if (!_allowAlpha)
        {
            c = c with { A = 0xFF };
        }

        Show(c, updateText: true);
        _set(c);
    }

    /// <summary>
    /// Ввод HEX. Во время набора применяется только полная запись (#RRGGBB или #RRGGBBAA) — промежуточные значения («#F», «#FF0»)
    /// не попадают ни в оформление, ни в профиль. По Enter или при уходе фокуса принимается любая верная запись; неверная —
    /// отметка ошибки, затем возврат к действующему цвету (в профиль не попадает).
    /// </summary>
    private void OnHexChanged(bool commit)
    {
        if (_updating)
        {
            return;
        }

        string text = _hex.Text.Trim();
        bool complete = text.Length is 7 or 9 && text.StartsWith('#');
        if (ThemeColor.TryParse(text, out var c) && (_allowAlpha || c.A == 0xFF) && (commit || complete))
        {
            _hex.Tag = null;
            _hex.ToolTip = null;
            bool changed = c != _value;
            Show(c, updateText: commit);
            if (changed)
            {
                _set(c);
            }
        }
        else if (commit)
        {
            Show(_value, updateText: true); // неверная запись не применяется; поле показывает действующий цвет
        }
        else
        {
            _hex.Tag = "error";
            _hex.ToolTip = Loc.T(_allowAlpha ? "Color.ExpectedAlpha" : "Color.Expected");
        }
    }

    private void Show(ThemeColor c, bool updateText)
    {
        _value = c;
        ((Border)_swatch.Child).Background = c.ToFrozenBrush();
        if (updateText)
        {
            _updating = true;
            _hex.Text = c.ToString();
            _hex.Tag = null;
            _updating = false;
        }
    }

    private void OpenPopup()
    {
        var picker = new ColorPicker(_value, _allowAlpha, SetFromPicker);
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(picker);
        var presets = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), Width = 240 };
        foreach (var p in Presets)
        {
            var c = ThemeColor.Parse(p);
            var b = new Button { Style = _theme.StyleOf("Ko.Button"), Padding = new Thickness(0), Margin = new Thickness(0, 0, 4, 4), Width = 20, Height = 20, Background = c.ToFrozenBrush(), ToolTip = p };
            System.Windows.Automation.AutomationProperties.SetName(b, p);
            b.Click += (_, _) =>
            {
                var withAlpha = c with { A = _value.A };
                picker.SetColor(withAlpha);
                SetFromPicker(withAlpha);
            };
            presets.Children.Add(b);
        }

        panel.Children.Add(presets);
        var popup = new Popup
        {
            PlacementTarget = _open,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = Themed(new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = panel,
            }),
        };
        popup.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                popup.IsOpen = false;
                _open.Focus();
            }
        };
        popup.Opened += (_, _) => picker.FocusSurface();
        popup.IsOpen = true;
    }

    private Border Themed(Border b)
    {
        b.Resources.MergedDictionaries.Add(_theme);
        b.SetResourceReference(Border.BackgroundProperty, "Ko.CardBrush");
        b.SetResourceReference(Border.BorderBrushProperty, "Ko.BorderBrush");
        return b;
    }

    internal static Brush Checker()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        g.Children.Add(new GeometryDrawing(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD8, 0xD8, 0xDC)), null, new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, 4, 4)), new RectangleGeometry(new Rect(4, 4, 4, 4)) } }));
        var brush = new DrawingBrush(g) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute };
        brush.Freeze();
        return brush;
    }
}

/// <summary>Палитра HSV: область насыщенности и яркости, полосы оттенка и прозрачности. Стрелки клавиатуры двигают маркеры.</summary>
public sealed class ColorPicker : StackPanel
{
    private const double AreaW = 240, AreaH = 150;
    private readonly Action<ThemeColor> _changed;
    private readonly bool _alpha;
    private readonly Border _area = new() { Width = AreaW, Height = AreaH, CornerRadius = new CornerRadius(8), Focusable = true, ClipToBounds = true };
    private readonly Ellipse _marker = new() { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
    private readonly GradientBar _hueBar;
    private readonly GradientBar? _alphaBar;
    private double _h, _s, _v, _a;

    public ColorPicker(ThemeColor initial, bool allowAlpha, Action<ThemeColor> changed)
    {
        _changed = changed;
        _alpha = allowAlpha;
        var canvas = new Canvas { Width = AreaW, Height = AreaH };
        var white = new Border { Width = AreaW, Height = AreaH, Background = new LinearGradientBrush(Colors.White, System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 0) };
        var black = new Border { Width = AreaW, Height = AreaH, Background = new LinearGradientBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
        canvas.Children.Add(white);
        canvas.Children.Add(black);
        canvas.Children.Add(_marker);
        _area.Child = canvas;
        System.Windows.Automation.AutomationProperties.SetName(_area, Loc.T("Color.SaturationValue"));
        Children.Add(_area);

        var hue = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++)
        {
            hue.GradientStops.Add(new GradientStop(FromHsv(i * 60 % 360, 1, 1, 1).ToWpf(), i / 6.0));
        }

        _hueBar = new GradientBar(hue, v => { _h = v * 359.999; Emit(); }) { Margin = new Thickness(0, 10, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(_hueBar, Loc.T("Color.Hue"));
        Children.Add(_hueBar);
        if (allowAlpha)
        {
            _alphaBar = new GradientBar(Brushes.Transparent, v => { _a = v; Emit(); }, checker: true) { Margin = new Thickness(0, 8, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(_alphaBar, Loc.T("Color.Alpha"));
            Children.Add(_alphaBar);
        }

        _area.MouseLeftButtonDown += (_, e) => { _area.CaptureMouse(); _area.Focus(); PickArea(e.GetPosition(_area)); };
        _area.MouseMove += (_, e) => { if (_area.IsMouseCaptured) { PickArea(e.GetPosition(_area)); } };
        _area.MouseLeftButtonUp += (_, _) => _area.ReleaseMouseCapture();
        _area.KeyDown += OnAreaKey;
        _area.FocusVisualStyle = null;
        _area.GotKeyboardFocus += (_, _) => _area.SetResourceReference(Border.BorderBrushProperty, "Ko.FocusBrush");
        _area.LostKeyboardFocus += (_, _) => _area.BorderBrush = null;
        _area.BorderThickness = new Thickness(2);
        SetColor(initial);
    }

    public void FocusSurface() => _area.Focus();

    public void SetColor(ThemeColor c)
    {
        (_h, _s, _v) = ToHsv(c);
        _a = c.A / 255.0;
        Refresh();
    }

    public static ThemeColor FromHsv(double h, double s, double v, double a)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60 % 2) - 1)), m = v - c;
        var (r, g, b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        static byte B(double d) => (byte)Math.Clamp(Math.Round(d * 255), 0, 255);
        return new ThemeColor(B(r + m), B(g + m), B(b + m), B(a));
    }

    public static (double H, double S, double V) ToHsv(ThemeColor c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * (((b - r) / d) + 2) : 60 * (((r - g) / d) + 4);
        if (h < 0)
        {
            h += 360;
        }

        return (h, max == 0 ? 0 : d / max, max);
    }

    private void PickArea(Point p)
    {
        _s = Math.Clamp(p.X / AreaW, 0, 1);
        _v = 1 - Math.Clamp(p.Y / AreaH, 0, 1);
        Emit();
    }

    private void OnAreaKey(object sender, KeyEventArgs e)
    {
        const double d = 0.02;
        switch (e.Key)
        {
            case Key.Left: _s = Math.Max(0, _s - d); break;
            case Key.Right: _s = Math.Min(1, _s + d); break;
            case Key.Up: _v = Math.Min(1, _v + d); break;
            case Key.Down: _v = Math.Max(0, _v - d); break;
            default: return;
        }

        e.Handled = true;
        Emit();
    }

    private void Emit()
    {
        Refresh();
        _changed(FromHsv(_h, _s, _v, _alpha ? _a : 1));
    }

    private void Refresh()
    {
        _area.Background = new SolidColorBrush(FromHsv(_h, 1, 1, 1).ToWpf());
        Canvas.SetLeft(_marker, (_s * AreaW) - 6);
        Canvas.SetTop(_marker, ((1 - _v) * AreaH) - 6);
        _hueBar.SetValue(_h / 360, raise: false);
        if (_alphaBar is not null)
        {
            var solid = FromHsv(_h, _s, _v, 1).ToWpf();
            _alphaBar.SetBrush(new LinearGradientBrush(System.Windows.Media.Color.FromArgb(0, solid.R, solid.G, solid.B), solid, 0));
            _alphaBar.SetValue(_a, raise: false);
        }
    }
}

/// <summary>Горизонтальная полоса выбора значения 0…1 (оттенок, прозрачность): мышь и стрелки клавиатуры.</summary>
public sealed class GradientBar : Grid
{
    private readonly Border _fill = new() { CornerRadius = new CornerRadius(6) };
    private readonly Border _thumb = new() { Width = 8, Height = 18, CornerRadius = new CornerRadius(3), BorderBrush = Brushes.White, BorderThickness = new Thickness(2), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    private readonly Action<double> _changed;
    private double _value;

    public GradientBar(Brush brush, Action<double> changed, bool checker = false)
    {
        _changed = changed;
        Width = 240;
        Height = 18;
        Focusable = true;
        FocusVisualStyle = null;
        if (checker)
        {
            Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = ColorField.Checker() });
        }

        _fill.Background = brush;
        Children.Add(_fill);
        _thumb.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.6 };
        Children.Add(_thumb);
        MouseLeftButtonDown += (_, e) => { CaptureMouse(); Focus(); Pick(e.GetPosition(this).X); };
        MouseMove += (_, e) => { if (IsMouseCaptured) { Pick(e.GetPosition(this).X); } };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        GotKeyboardFocus += (_, _) => _thumb.SetResourceReference(Border.BorderBrushProperty, "Ko.FocusBrush");
        LostKeyboardFocus += (_, _) => _thumb.BorderBrush = Brushes.White;
        KeyDown += (_, e) =>
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.01;
            if (e.Key is Key.Left or Key.Down)
            {
                SetValue(Math.Max(0, _value - step), raise: true);
                e.Handled = true;
            }
            else if (e.Key is Key.Right or Key.Up)
            {
                SetValue(Math.Min(1, _value + step), raise: true);
                e.Handled = true;
            }
        };
    }

    public double Value => _value;

    public void SetBrush(Brush b) => _fill.Background = b;

    public void SetValue(double v, bool raise)
    {
        _value = Math.Clamp(v, 0, 1);
        _thumb.Margin = new Thickness((_value * (Width - 8)), 0, 0, 0);
        if (raise)
        {
            _changed(_value);
        }
    }

    private void Pick(double x) => SetValue(x / Width, raise: true);
}
