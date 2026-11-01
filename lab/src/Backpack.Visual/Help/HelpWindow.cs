using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Help;

/// <summary>
/// Окно справки: слева поиск и оглавление (или результаты поиска), справа раздел в FlowDocument.
/// Внутренние ссылки — переходы с историей; «demo:…» — безопасная демонстрация в отдельном окне примера;
/// внешние (http/https) — только по подтверждению. Изображения увеличиваются по щелчку.
/// Тема и язык меняются на месте; при смене языка открывается тот же раздел на новом языке.
/// </summary>
public sealed class HelpWindow : Window
{
    private HelpLibrary _library;
    private readonly FlowDocumentScrollViewer _viewer = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsToolBarVisible = false, Zoom = 100 };
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly TextBlock _searchLabel = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly ListBox _list = new();
    private readonly Button _back = new() { Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button _forward = new() { Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button _zoomOut = new() { Content = "A−", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 4, 0) };
    private readonly Button _zoomIn = new() { Content = "A+", Padding = new Thickness(10, 3, 10, 3) };
    private readonly TextBlock _status = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _contents = new() { Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0), Visibility = Visibility.Collapsed };
    private bool _contentsOpen;
    private Border? _leftHost;
    private bool _narrow;

    /// <summary>Ширина окна, ниже которой оглавление свёрнуто (кнопка «Содержание» открывает его поверх раздела).</summary>
    public const double NarrowWidth = 760;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private Dictionary<string, Block> _anchors = new();
    private string? _highlight;

    public HelpWindow(HelpLibrary library)
    {
        _library = library;
        UiTheme.Attach(this);
        Width = 1040;
        Height = 780;
        MinWidth = 480;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var left = new DockPanel { Margin = new Thickness(10), Width = 270 };
        DockPanel.SetDock(_searchLabel, Dock.Top);
        DockPanel.SetDock(_search, Dock.Top);
        _searchLabel.Style = UiTheme.Resources.StyleOf("Ko.Hint");
        left.Children.Add(_searchLabel);
        left.Children.Add(_search);
        left.Children.Add(_list);
        var leftHost = new Border { Child = left };
        _leftHost = leftHost;
        leftHost.SetResourceReference(Border.BackgroundProperty, "Ko.SidebarBrush");

        // Кнопки сверху переносятся на следующую строку в узком окне; «Содержание» видна, только когда оглавление свёрнуто.
        var top = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 10, 12, 6) };
        top.Children.Add(_contents);
        top.Children.Add(_back);
        top.Children.Add(_forward);
        top.Children.Add(_zoomOut);
        top.Children.Add(_zoomIn);
        top.Children.Add(_status);
        _status.Style = UiTheme.Resources.StyleOf("Ko.Hint");
        var right = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        right.Children.Add(top);
        right.Children.Add(_viewer);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Panel.SetZIndex(leftHost, 1);
        grid.Children.Add(leftHost);
        Content = grid;
        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        _contents.Click += (_, _) =>
        {
            _contentsOpen = !_contentsOpen;
            ApplyWidth(ActualWidth);
        };

        _search.TextChanged += (_, _) => ShowListForQuery();
        _list.SelectionChanged += (_, _) => OnListSelection();
        _back.Click += (_, _) => GoBack();
        _forward.Click += (_, _) => GoForward();
        _zoomIn.Click += (_, _) => _viewer.Zoom = Math.Min(200, _viewer.Zoom + 15);
        _zoomOut.Click += (_, _) => _viewer.Zoom = Math.Max(70, _viewer.Zoom - 15);

        CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, (_, _) => GoBack(), (_, e) => e.CanExecute = _backStack.Count > 0));
        CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, (_, _) => GoForward(), (_, e) => e.CanExecute = _forwardStack.Count > 0));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, (_, _) => { _search.Focus(); _search.SelectAll(); }));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Help, (_, e) => { Navigate(HelpLibrary.IndexFile); e.Handled = true; }));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.Escape, ModifierKeys.None));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => Close()));

        UiTheme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            UiTheme.Changed -= OnThemeChanged;
            HelpDemoWindow.CloseIfOpen();
        };
        ApplyTexts();
        ShowListForQuery();
    }

    public string CurrentTopic { get; private set; } = "";

    public string CurrentFile => HelpLibrary.SplitTopic(CurrentTopic).File;

    public int BackDepth => _backStack.Count;

    public FlowDocument? Document => _viewer.Document;

    public HelpLibrary Library => _library;

    public void Navigate(string topic) => Navigate(topic, addToHistory: true);

    /// <summary>Новый язык: тот же раздел на новом языке, история сохраняется (имена файлов одинаковы во всех языках).</summary>
    public void SetLibrary(HelpLibrary library)
    {
        _library = library;
        ApplyTexts();
        _search.Text = "";
        ShowListForQuery();
        Reload();
    }

    public void GoBack()
    {
        if (_backStack.Count > 0)
        {
            _forwardStack.Push(CurrentTopic);
            Navigate(_backStack.Pop(), addToHistory: false);
        }
    }

    public void GoForward()
    {
        if (_forwardStack.Count > 0)
        {
            _backStack.Push(CurrentTopic);
            Navigate(_forwardStack.Pop(), addToHistory: false);
        }
    }

    /// <summary>
    /// Узкое окно: оглавление свёрнуто, раздел на всю ширину; «Содержание» открывает оглавление поверх раздела (выбор темы
    /// закрывает его). Широкое — оглавление слева, как раньше.
    /// </summary>
    private void ApplyWidth(double width)
    {
        if (_leftHost is null)
        {
            return;
        }

        bool narrow = width > 0 && width < NarrowWidth;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            _contentsOpen = false;
        }

        _contents.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumnSpan(_leftHost, 1);
        _leftHost.Visibility = !narrow || _contentsOpen ? Visibility.Visible : Visibility.Collapsed;
        _leftHost.Effect = narrow ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Opacity = 0.35 } : null;
        if (narrow)
        {
            // Поверх раздела: колонка оглавления не отнимает ширину у текста.
            ((Grid)Content).ColumnDefinitions[0].Width = new GridLength(0);
            Grid.SetColumnSpan(_leftHost, 2);
            _leftHost.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            ((Grid)Content).ColumnDefinitions[0].Width = GridLength.Auto;
            _leftHost.ClearValue(HorizontalAlignmentProperty);
        }
    }

    /// <summary>Оглавление свёрнуто (узкое окно).</summary>
    public bool ContentsCollapsed => _narrow && !_contentsOpen;

    private void ApplyTexts()
    {
        _contents.Content = Loc.T("Help.Contents");
        System.Windows.Automation.AutomationProperties.SetName(_contents, Loc.T("Help.Contents"));
        _searchLabel.Text = Loc.T("Help.Search");
        _back.Content = Loc.T("Help.Back");
        _forward.Content = Loc.T("Help.Forward");
        _zoomIn.ToolTip = Loc.T("Help.ZoomIn");
        _zoomOut.ToolTip = Loc.T("Help.ZoomOut");
        System.Windows.Automation.AutomationProperties.SetName(_search, Loc.T("Help.Search"));
        System.Windows.Automation.AutomationProperties.SetName(_zoomIn, Loc.T("Help.ZoomIn"));
        System.Windows.Automation.AutomationProperties.SetName(_zoomOut, Loc.T("Help.ZoomOut"));
    }

    private void OnThemeChanged() => Reload();

    private void Reload()
    {
        if (CurrentTopic.Length > 0)
        {
            string topic = CurrentTopic;
            CurrentTopic = "";
            Navigate(topic, addToHistory: false, _highlight);
        }
    }

    private void Navigate(string topic, bool addToHistory, string? highlight = null)
    {
        var (file, anchor) = HelpLibrary.SplitTopic(topic, CurrentTopic.Length == 0 ? HelpLibrary.IndexFile : CurrentFile);
        var page = _library.Find(file) ?? _library.Find(HelpLibrary.IndexFile);
        if (page is null)
        {
            return;
        }

        string normalized = page.File + (anchor is null ? "" : "#" + anchor);
        if (addToHistory && CurrentTopic.Length > 0 && normalized != CurrentTopic)
        {
            _backStack.Push(CurrentTopic);
            _forwardStack.Clear();
        }

        if (page.File != CurrentFile || CurrentTopic.Length == 0 || highlight is not null)
        {
            _highlight = highlight;
            var (doc, anchors) = FlowDocumentBuilder.Build(page.Document, OnLink, Theme());
            _anchors = anchors;
            if (highlight is not null)
            {
                FlowDocumentBuilder.Highlight(doc, highlight, UiTheme.Brush("Ko.HighlightBrush"));
            }

            _viewer.Document = doc;
        }

        CurrentTopic = normalized;
        Title = Loc.F("Help.WindowTitle", page.Title);
        _status.Text = anchor is not null && !_anchors.ContainsKey(anchor) ? Loc.F("Help.AnchorMissing", anchor) : "";
        if (_library.IsFallback)
        {
            _status.Text = Loc.T("Help.NotTranslated") + (_status.Text.Length > 0 ? " · " + _status.Text : "");
        }
        _back.IsEnabled = _backStack.Count > 0;
        _forward.IsEnabled = _forwardStack.Count > 0;

        Dispatcher.BeginInvoke(() =>
        {
            if (anchor is not null && _anchors.TryGetValue(anchor, out var block))
            {
                block.BringIntoView();
            }
            else
            {
                _viewer.Document?.Blocks.FirstBlock?.BringIntoView();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static HelpDocTheme Theme() => new(
        UiTheme.Brush("Ko.TextBrush"), UiTheme.Brush("Ko.WindowBrush"), UiTheme.Brush("Ko.CodeBrush"),
        UiTheme.Brush("Ko.BorderBrush"), UiTheme.Brush("Ko.LinkBrush"), UiTheme.Brush("Ko.SecondaryTextBrush"));

    private void OnLink(string target)
    {
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var (choice, _) = KoDialog.Ask(this, Loc.T("Help.External.Title"), Loc.F("Help.External.Message", target),
                [new("open", Loc.T("Common.Open"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
            if (choice == "open")
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }

            return;
        }

        if (target.StartsWith("demo:", StringComparison.Ordinal))
        {
            HelpDemoWindow.Show(this, target["demo:".Length..]);
            return;
        }

        if (target.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            HelpImageWindow.Show(this, target, "");
            return;
        }

        Navigate(target);
    }

    private void ShowListForQuery()
    {
        _list.Items.Clear();
        string q = _search.Text;
        if (q.Trim().Length < 2)
        {
            foreach (var t in _library.Toc)
            {
                _list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = t.Title, TextWrapping = TextWrapping.Wrap }, Tag = t.File });
            }

            return;
        }

        var hits = _library.Search(q);
        if (hits.Count == 0)
        {
            _list.Items.Add(new ListBoxItem { Content = Loc.T("Help.NothingFound"), IsEnabled = false });
            return;
        }

        foreach (var h in hits)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
            panel.Children.Add(new TextBlock { Text = h.PageTitle + (h.SectionTitle == h.PageTitle ? "" : " — " + h.SectionTitle), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = h.Snippet, TextWrapping = TextWrapping.Wrap, Style = UiTheme.Resources.StyleOf("Ko.Hint") });
            _list.Items.Add(new ListBoxItem { Content = panel, Tag = new SearchTag(h.File + (h.Anchor is null ? "" : "#" + h.Anchor), q) });
        }
    }

    private void OnListSelection()
    {
        switch ((_list.SelectedItem as ListBoxItem)?.Tag)
        {
            case string file:
                Navigate(file);
                break;
            case SearchTag s:
                Navigate(s.Topic, addToHistory: true, highlight: s.Query.Trim());
                break;
        }

        if (_narrow)
        {
            _contentsOpen = false; // выбранная тема открыта — оглавление снова свёрнуто
            ApplyWidth(ActualWidth);
        }
    }

    private sealed record SearchTag(string Topic, string Query);
}

/// <summary>Цвета документа справки из темы интерфейса.</summary>
public sealed record HelpDocTheme(Brush Text, Brush Background, Brush Code, Brush Border, Brush Link, Brush Secondary)
{
    public static HelpDocTheme Light { get; } = new(Brushes.Black, Brushes.White, new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF5)), Brushes.LightGray, Brushes.RoyalBlue, Brushes.DimGray);
}

/// <summary>Построение FlowDocument из разобранного Markdown. Только элементы WPF-документа; XAML не загружается.</summary>
public static class FlowDocumentBuilder
{
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    public static (FlowDocument Document, Dictionary<string, Block> Anchors) Build(MdDocument md, Action<string> onLink, HelpDocTheme? theme = null)
    {
        theme ??= HelpDocTheme.Light;
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 15,
            PagePadding = new Thickness(28, 12, 28, 28),
            TextAlignment = TextAlignment.Left,
            Foreground = theme.Text,
            Background = theme.Background,
            LineHeight = 22,
        };
        var anchors = new Dictionary<string, Block>();

        foreach (var block in md.Blocks)
        {
            switch (block)
            {
                case MdHeading h:
                    var hp = new Paragraph { FontSize = h.Level switch { 1 => 27, 2 => 21, _ => 17 }, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, h.Level == 1 ? 0 : 18, 0, 6) };
                    if (h.Level == 1)
                    {
                        hp.SetResourceReference(TextElement.FontFamilyProperty, "Ko.HeadingFont"); // только крупный заголовок раздела
                    }
                    AddInlines(hp.Inlines, h.Text, onLink, theme);
                    doc.Blocks.Add(hp);
                    anchors.TryAdd(h.Slug, hp);
                    break;
                case MdParagraph p:
                    var para = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
                    AddInlines(para.Inlines, p.Text, onLink, theme);
                    doc.Blocks.Add(para);
                    break;
                case MdList l:
                    var list = new List { MarkerStyle = l.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 10) };
                    foreach (var item in l.Items)
                    {
                        var ip = new Paragraph { Margin = new Thickness(0, 0, 0, 3) };
                        AddInlines(ip.Inlines, item, onLink, theme);
                        list.ListItems.Add(new ListItem(ip));
                    }

                    doc.Blocks.Add(list);
                    break;
                case MdTable t:
                    doc.Blocks.Add(BuildTable(t, onLink, theme));
                    break;
                case MdCode c:
                    doc.Blocks.Add(new Paragraph(new Run(c.Text)) { FontFamily = Mono, FontSize = 13, Background = theme.Code, Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12) });
                    break;
                case MdImage img:
                    doc.Blocks.Add(BuildImage(img, onLink, theme));
                    break;
            }
        }

        return (doc, anchors);
    }

    /// <summary>Изображение по ширине документа (не больше исходного размера); щелчок — увеличение; подпись — текст под ним.</summary>
    private static Block BuildImage(MdImage img, Action<string> onLink, HelpDocTheme theme)
    {
        var source = HelpLibrary.LoadImage(img.Target);
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 14), HorizontalAlignment = HorizontalAlignment.Left };
        if (source is null)
        {
            panel.Children.Add(new TextBlock { Text = img.Alt, FontStyle = FontStyles.Italic, Foreground = theme.Secondary, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                MaxWidth = source.PixelWidth / (source.DpiX > 0 ? source.DpiX / 96.0 : 1),
                Cursor = Cursors.Hand,
                ToolTip = Loc.T("Help.ImageZoomHint"),
            };
            System.Windows.Automation.AutomationProperties.SetName(image, img.Alt);
            var frame = new Border { BorderBrush = theme.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = image, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
            image.MouseLeftButtonUp += (_, _) => HelpImageWindow.Show(Window.GetWindow(frame), img.Target, img.Alt);
            panel.Children.Add(frame);
            if (img.Alt.Length > 0)
            {
                panel.Children.Add(new TextBlock { Text = img.Alt, FontSize = 13, Foreground = theme.Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            }
        }

        return new BlockUIContainer(panel);
    }

    /// <summary>Подсветка всех вхождений (без учёта регистра, ё = е) в Run-ах документа.</summary>
    public static int Highlight(FlowDocument doc, string query, Brush? brush = null)
    {
        brush ??= Brushes.Khaki;
        string q = HelpLibrary.Normalize(query);
        if (q.Length == 0)
        {
            return 0;
        }

        var runs = new List<Run>();
        Collect(doc.Blocks, runs);
        int count = 0;
        foreach (var run in runs)
        {
            string text = run.Text;
            int at = HelpLibrary.Normalize(text).IndexOf(q, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var start = run.ContentStart.GetPositionAtOffset(at);
            var end = start?.GetPositionAtOffset(q.Length);
            if (start is not null && end is not null)
            {
                new TextRange(start, end).ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                count++;
            }
        }

        return count;
    }

    private static void Collect(IEnumerable<Block> blocks, List<Run> runs)
    {
        foreach (var b in blocks)
        {
            switch (b)
            {
                case Paragraph p:
                    CollectInlines(p.Inlines, runs);
                    break;
                case List l:
                    foreach (var li in l.ListItems)
                    {
                        Collect(li.Blocks, runs);
                    }

                    break;
                case Table t:
                    foreach (var g in t.RowGroups)
                    {
                        foreach (var r in g.Rows)
                        {
                            foreach (var c in r.Cells)
                            {
                                Collect(c.Blocks, runs);
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectInlines(InlineCollection inlines, List<Run> runs)
    {
        foreach (var i in inlines.ToList())
        {
            if (i is Run r)
            {
                runs.Add(r);
            }
            else if (i is Span s)
            {
                CollectInlines(s.Inlines, runs);
            }
        }
    }

    private static Table BuildTable(MdTable t, Action<string> onLink, HelpDocTheme theme)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12), BorderBrush = theme.Border, BorderThickness = new Thickness(1, 1, 0, 0) };
        for (int i = 0; i < t.Header.Count; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        group.Rows.Add(Row(t.Header, true));
        foreach (var r in t.Rows)
        {
            group.Rows.Add(Row(r, false));
        }

        table.RowGroups.Add(group);
        return table;

        TableRow Row(IReadOnlyList<string> cells, bool header)
        {
            var row = new TableRow();
            for (int i = 0; i < t.Header.Count; i++)
            {
                var p = new Paragraph { Margin = new Thickness(0) };
                AddInlines(p.Inlines, i < cells.Count ? cells[i] : "", onLink, theme);
                row.Cells.Add(new TableCell(p)
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = theme.Border,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    Background = header ? theme.Code : null,
                });
            }

            return row;
        }
    }

    private static void AddInlines(InlineCollection target, string text, Action<string> onLink, HelpDocTheme theme)
    {
        foreach (var inline in Markdown.Inlines(text))
        {
            switch (inline)
            {
                case MdText { Bold: true } b:
                    target.Add(new Bold(new Run(b.Text)));
                    break;
                case MdText { Italic: true } it:
                    target.Add(new Italic(new Run(it.Text)));
                    break;
                case MdText plain:
                    target.Add(new Run(plain.Text));
                    break;
                case MdCodeSpan c:
                    target.Add(new Run(c.Text) { FontFamily = Mono, Background = theme.Code });
                    break;
                case MdLink l:
                    var link = new Hyperlink(new Run(l.Text)) { ToolTip = l.Target.StartsWith("demo:", StringComparison.Ordinal) ? Loc.T("Help.DemoHint") : l.Target, Foreground = theme.Link };
                    string targetText = l.Target;
                    link.Click += (_, _) => onLink(targetText);
                    target.Add(link);
                    break;
            }
        }
    }
}
