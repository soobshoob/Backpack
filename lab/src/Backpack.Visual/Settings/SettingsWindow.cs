using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Backpack.Visual.Brand;
using Backpack.Visual.Help;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Layout;
using Backpack.Visual.Localization;
using Backpack.Visual.Mouse;
using Backpack.Visual.Profiles;
using Backpack.Visual.Rendering;
using Backpack.Visual.Styles;
using Backpack.Visual.Updates;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Settings;

/// <summary>Действия, которые выполняет приложение-хозяин (главное окно не знает об оверлее и захвате).</summary>
public sealed record SettingsWindowActions(
    Action ShowOverlayExample,
    Action ShowLayoutIndicatorExample,
    Func<string> LayoutStatus,
    Action? OpenDiagnostics = null,
    Action? HideToTray = null,
    Func<bool>? DisplayVisible = null,
    Action<bool>? SetDisplayVisible = null,
    Action? Exit = null,
    Commands.CommandRegistry? Commands = null,
    Plugins.PluginManager? Plugins = null,
    Func<IReadOnlyList<MonitorDesc>>? Monitors = null,
    Func<IReadOnlyList<MonitorRef>>? MissingMonitors = null,
    Func<string?>? PrivacyStatus = null,
    Autostart? Autostart = null);

/// <summary>
/// Разделы главного окна. Порядок значений — исторический (не порядок навигации); порядок и вложенность задаёт <c>Sections</c>.
/// </summary>
public enum SettingsSection { Appearance, Keys, Mouse, Layout, Position, Profiles, Application, Home, Hotkeys, Help, About, Lens, Drawing, Plugins, CursorZoom, Interface, Privacy }

/// <summary>
/// Главное окно Backpack (класс сохранил имя окна настроек Phase 2D). Компоновка Phase 4E — по концепции сайта:
/// навигация слева с раскрывающимися группами (подстраницы доступны прямо из меню), крупный заголовок с кнопкой «Назад»,
/// свободная колонка настроек и живое превью справа при достаточной ширине (при узком окне — над настройками).
/// Каждое изменение сразу уходит в <see cref="SettingsStore.Update"/> (оверлей применяет его без перезапуска захвата).
/// Изменения извне (меню трея, загрузка профиля) перестраивают текущий раздел; смена языка, темы и шрифта применяется на месте.
/// F1 открывает раздел справки про текущий раздел.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>
    /// Карта страниц (Phase 4E). Верхний уровень: Главная, Клавиши ▸ (Стиль кейкапов, Положение и мониторы), Мышь ▸
    /// (Увеличение у курсора), Раскладка, Фокус, Рисование, избранные плагины, Плагины ▸ (установленные плагины), Профили,
    /// Настройки приложения ▸ (Оформление, Приватность, Команды и сочетания), Справка, О программе. <c>Parent</c> — группа,
    /// в которой подстраница показана в меню; открытие родителя или подстраницы раскрывает группу.
    /// </summary>
    private static readonly (SettingsSection Section, string TitleKey, string HelpFile, SettingsSection? Parent, string Icon)[] Sections =
    [
        (SettingsSection.Home, "Settings.Nav.Home", "01-quick-start.md", null, "house"),
        (SettingsSection.Keys, "Settings.Nav.Keys", "03-keys-and-labels.md", null, "keyboard"),
        (SettingsSection.Appearance, "Settings.Nav.Appearance", "11-styles.md", SettingsSection.Keys, "palette"),
        (SettingsSection.Position, "Settings.Nav.Position", "06-position.md", SettingsSection.Keys, "move"),
        // Phase 4H: индикатор раскладки и рисование — плагины; здесь остался только каталог стилей индикатора (система стилей).
        (SettingsSection.Layout, "Settings.Nav.IndicatorStyle", "05-keyboard-layout.md", SettingsSection.Keys, "languages"),
        (SettingsSection.Mouse, "Settings.Nav.Mouse", "04-mouse.md", null, "mouse"),
        (SettingsSection.Plugins, "Settings.Nav.Plugins", "15-plugins.md", null, "puzzle"),
        (SettingsSection.Profiles, "Settings.Nav.Profiles", "07-profiles.md", null, "layers"),
        (SettingsSection.Application, "Settings.Nav.Application", "02-interface.md", null, "settings-2"),
        (SettingsSection.Interface, "Settings.Nav.Interface", "02-interface.md", SettingsSection.Application, "sparkles"),
        (SettingsSection.Privacy, "Settings.Nav.Privacy", "16-privacy-monitors-commands.md", SettingsSection.Application, "shield-check"),
        (SettingsSection.Hotkeys, "Settings.Nav.Hotkeys", "12-hotkeys-lens-drawing.md", SettingsSection.Application, "command"),
        (SettingsSection.Help, "Settings.Nav.HelpPage", "README.md", null, "circle-question-mark"),
        (SettingsSection.About, "Settings.Nav.About", "13-about-and-updates.md", null, "info"),
    ];

    /// <summary>Ширина области содержимого, начиная с которой превью стоит справа от настроек (иначе — над ними).</summary>
    public const double WidePreviewWidth = 860;

    private readonly SettingsStore _store;
    private readonly SettingsWindowActions _actions;
    private readonly ProfileSession? _profiles;
    private readonly AppPreferencesStore? _prefs;
    private readonly SettingsUi _ui;
    private readonly IAnimationClock _clock = new StopwatchClock();
    private readonly OverlayPresenter _preview;
    private readonly StackPanel _nav = new() { Margin = new Thickness(12, 4, 12, 12) };
    private readonly PageLayout _cards = new() { Margin = new Thickness(32, 4, 24, 40), WideThreshold = double.PositiveInfinity };
    private readonly ContentControl _previewHost = new() { Focusable = false };
    private readonly TextBlock _title = new();
    private readonly List<(UIElement Element, Func<LabSettings, bool> Enabled)> _enablers = new();

    /// <summary>
    /// Поля, относящиеся только к выбранному варианту (способ отрисовки, режим): у других вариантов скрыты и места не занимают.
    /// Значения не меняются — при возврате к варианту поле показывает сохранённое значение.
    /// </summary>
    private readonly List<(UIElement Element, Func<LabSettings, bool> Shown)> _shownWhen = new();
    private readonly DispatcherTimer _mouseDemo;
    private readonly HashSet<SettingsSection> _expanded = new();
    private readonly List<(SettingsSection Section, string? Plugin)> _history = new();
    private Button? _backButton;
    private ScrollViewer? _scroll;
    private DemoPlayer? _demo;
    private MouseCursorVisual? _mousePreview;
    private LayoutIndicatorVisual? _indicatorPreview;
    private Canvas? _positionPreview;
    private ClipBorder? _previewCard;
    private Grid? _root;
    private DockPanel? _sidebar;
    private Button? _menuButton;
    private RadioButton? _home;
    private ScrollViewer? _navScroll;
    private StackPanel? _bottomActions;
    private Border? _statusBar;
    private Button? _helpButton;
    private Button? _trayButton;
    private Button? _resetButton;
    private PageHeader? _header;

    /// <summary>Шапка страницы (для проверок выравнивания).</summary>
    internal PageHeader? Header => _header;
    private int _mouseDemoStep;
    private readonly StyleStore? _styles;
    private readonly HotkeyManager? _hotkeys;
    private readonly AppInfo? _about;
    private readonly UpdateChecker? _updates;
    private readonly ToggleButton _settingsGroup = new();
    private bool _released;
    private bool _switching;
    private bool _selfChange;
    private bool _noPreview;
    private bool _compactNav;
    private bool _navOpen;

    public SettingsWindow(SettingsStore store, SettingsWindowActions actions, SettingsSection section = SettingsSection.Appearance,
        ProfileSession? profiles = null, AppPreferencesStore? prefs = null, StyleStore? styles = null, HotkeyManager? hotkeys = null,
        AppInfo? about = null, UpdateChecker? updates = null)
    {
        _store = store;
        _actions = actions;
        _profiles = profiles;
        Activated += (_, _) => RefreshAutostart(); // автозапуск могли изменить в «Автозагрузке» Windows, пока окно было неактивно
        _prefs = prefs;
        _styles = styles;
        _hotkeys = hotkeys;
        _about = about;
        _updates = updates;
        UiTheme.Attach(this);
        WindowShell.SetIntegratedTitle(this, true); // Phase 4H: без отдельной полосы с маленьким названием — кнопки окна в верхней строке
        _ui = new SettingsUi(UiTheme.Resources);
        Width = 1280;
        Height = 840;
        // Наименьший размер, при котором страница ещё читается (компактное меню, строки «подпись над элементом»); сама вёрстка
        // адаптивна и не опирается на этот предел.
        MinWidth = MinWindowWidth;
        MinHeight = MinWindowHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _preview = new OverlayPresenter(_clock);
        _mouseDemo = new DispatcherTimer(TimeSpan.FromMilliseconds(700), DispatcherPriority.Normal, (_, _) => StepMouseDemo(), Dispatcher) { IsEnabled = false };
        _title.Style = UiTheme.Resources.StyleOf("Ko.H1");
        _title.VerticalAlignment = VerticalAlignment.Center;
        Section = section;
        Rebuild();

        HelpService.Attach(this, Sections[0].HelpFile);
        _store.Changed += OnStoreChanged;
        Loc.Changed += Rebuild;
        if (_actions.Plugins is { } plugins)
        {
            plugins.Store.Changed += OnPluginsChanged; // избранное, установка, удаление — меню перестраивается
        }
        if (_profiles is not null)
        {
            _profiles.Changed += UpdateStatus;
        }

        _store.Saved += UpdateStatus;
        SizeChanged += (_, e) => ApplyNavMode(e.NewSize.Width);

        // «Назад»: Alt+← и боковая кнопка мыши — как в браузере и Проводнике.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt && CanGoBack)
            {
                e.Handled = true;
                GoBack();
            }
            else if (e.Key == Key.Escape && _navOpen)
            {
                e.Handled = true;
                SetNavOpen(false);
            }
        };
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.XButton1 && CanGoBack)
            {
                e.Handled = true;
                GoBack();
            }
        };
        Closed += (_, _) => ReleaseResources();
    }

    public SettingsSection Section { get; private set; }

    private void OnPluginsChanged() => Dispatcher.BeginInvoke(() =>
    {
        if (!_released)
        {
            RebuildNav();
            BuildSection();
        }
    });

    /// <summary>Раздел справки для F1 в разделе настроек.</summary>
    public static string HelpFile(SettingsSection section) =>
        Sections.FirstOrDefault(x => x.Section == section).HelpFile ?? (section is SettingsSection.Lens or SettingsSection.CursorZoom ? "14-focus.md" : "README.md");

    public OverlayPresenter Preview => _preview;

    /// <summary>Все пункты навигации разделов, включая подпункты раскрывающихся групп (для тестов и автоматизации).</summary>
    public IReadOnlyList<RadioButton> NavItems => NavRadios().Where(rb => rb.Tag is SettingsSection).ToList();

    /// <summary>Пункты внешних плагинов в меню: избранные (верхний уровень) и прочие установленные (в группе «Плагины»).</summary>
    public IReadOnlyList<RadioButton> FavoriteItems => NavRadios().Where(rb => rb.Tag is string && rb.Uid == "favorite").ToList();

    public IReadOnlyList<RadioButton> PluginItems => NavRadios().Where(rb => rb.Tag is string).ToList();

    private IEnumerable<RadioButton> NavRadios() => _home is null ? Descendants<RadioButton>(_nav) : Descendants<RadioButton>(_nav).Prepend(_home);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(child))
            {
                yield return d;
            }
        }
    }

    /// <summary>Группа меню, в которой показан раздел (или сам раздел верхнего уровня).</summary>
    public static SettingsSection NavSection(SettingsSection s) => s;

    /// <summary>Группа (родитель) раздела в меню; null — раздел верхнего уровня.</summary>
    public static SettingsSection? ParentOf(SettingsSection s) => Sections.First(x => x.Section == s).Parent;

    /// <summary>Подстраницы группы в порядке меню.</summary>
    public static IReadOnlyList<SettingsSection> ChildrenOf(SettingsSection s) => Sections.Where(x => x.Parent == s).Select(x => x.Section).ToList();

    /// <summary>Группа раскрыта (подпункты видны).</summary>
    public bool IsExpanded(SettingsSection group) => _expanded.Contains(group);

    /// <summary>Заголовок прежней раскрывающейся группы «Настройки» (совместимость).</summary>
    public ToggleButton SettingsGroup => _settingsGroup;

    /// <summary>Область прокрутки разделов (не пересекается с превью).</summary>
    public FrameworkElement? ScrollArea { get; private set; }

    public ClipBorder? PreviewCard => _previewCard;

    /// <summary>Превью стоит справа от настроек (широкое окно) или над ними (узкое).</summary>
    public bool PreviewOnRight => _previewWide;

    /// <summary>
    /// Две компоновки страницы (Phase 4F); превью в обеих закреплено и не прокручивается — прокручиваются только карточки.
    /// Широкая: превью — колонка фиксированной ширины <see cref="PreviewColumnWidth"/> справа на всю высоту области страницы;
    /// карточки слева не уже <see cref="MinCardsWidth"/>. Если места меньше — вертикальная компоновка: превью — полоса над областью
    /// прокрутки на всю ширину содержимого (высота по пропорциям сцены, не больше 40 % высоты области), карточки ниже во всю ширину.
    /// Элементы не сжимаются, меняется только компоновка; переключение не пересоздаёт страницу и не сбрасывает значения.
    /// </summary>
    public const double PreviewColumnWidth = 400, MinCardsWidth = 480, PreviewGap = 24;

    private bool _previewWide;
    private Grid? _body;
    private Border? _previewColumn;

    /// <summary>Содержимое страницы: карточки и превью в одной раскладке (<see cref="PageLayout"/>).</summary>
    public PageLayout Cards => _cards;

    /// <summary>Меню слева в компактном виде (только значки) — узкое окно.</summary>
    public bool CompactNav => _compactNav;

    /// <summary>Строка уведомления об ошибке записи (видна только при ошибке).</summary>
    public Border? StatusBar => _statusBar;

    /// <summary>Ширина окна, начиная с которой меню показано с подписями.</summary>
    public const double CompactNavWidth = 1000;

    public const double MinWindowWidth = 560, MinWindowHeight = 440;

    public MouseCursorVisual? MousePreview => _mousePreview;

    public LayoutIndicatorVisual? IndicatorPreview => _indicatorPreview;

    public bool DemoRunning => _demo?.IsRunning == true || _mouseDemo.IsEnabled || _livePreview is { IsRunning: true };


    /// <summary>Кнопка «Назад» доступна (есть куда вернуться; на начальной странице — нет).</summary>
    public bool CanGoBack => Section != SettingsSection.Home && _history.Count > 0;

    /// <summary>История переходов (для тестов): без повторов и без начальной страницы в середине.</summary>
    public IReadOnlyList<(SettingsSection Section, string? Plugin)> History => _history;

    /// <summary>Освобождение подписок, таймеров и анимаций; вызывается при закрытии (и тестами напрямую).</summary>
    public void ReleaseResources()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        _store.Changed -= OnStoreChanged;
        Loc.Changed -= Rebuild;
        if (_actions.Plugins is { } plugins)
        {
            plugins.Store.Changed -= OnPluginsChanged;
        }
        if (_profiles is not null)
        {
            _profiles.Changed -= UpdateStatus;
        }

        _store.Saved -= UpdateStatus;
        StopPreviews();
        EndCommandRecording();
        _preview.Dispose();
        _store.Flush();
    }

    /// <summary>Открыть раздел (кнопки страниц, меню, трей, F1, поиск справки): запись в истории для «Назад».</summary>
    public void Show(SettingsSection section) => Navigate(section, section == SettingsSection.Plugins ? _pluginPage : null, record: true);

    /// <summary>Открыть страницу установленного плагина (меню, избранное, кнопка «Подробнее»).</summary>
    public void ShowPlugin(string id) => Navigate(SettingsSection.Plugins, id, record: true);

    /// <summary>Вернуться к предыдущей странице этого окна.</summary>
    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        var (section, plugin) = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        Navigate(section, plugin, record: false);
    }

    /// <summary>
    /// Переход. Несохранённый ввод в поле (число, цвет) подтверждается до перестройки страницы. История: текущая страница
    /// добавляется, прежние вхождения новой страницы удаляются (нет циклов и повторов); переход на «Главную» очищает историю.
    /// </summary>
    private void Navigate(SettingsSection section, string? plugin, bool record)
    {
        if (section == SettingsSection.Help)
        {
            // Отдельной страницы «Справка» больше нет (Phase 4F): справка — один вход внизу меню и F1; диагностика и сведения
            // о файлах настроек — на странице «Настройки приложения».
            section = SettingsSection.Application;
        }

        if (section is SettingsSection.Lens or SettingsSection.CursorZoom)
        {
            // Phase 4G: «Фокус», линза и увеличение у курсора — отдельные плагины; прежние ссылки ведут на страницу плагинов.
            section = SettingsSection.Plugins;
        }

        if (!SectionAvailable(section))
        {
            section = SettingsSection.Keys; // раздел недоступен без профилей или предпочтений
            plugin = null;
        }

        if (section != SettingsSection.Plugins)
        {
            plugin = null;
        }

        SetNavOpen(false);
        CommitPendingInput();
        EndCommandRecording();
        if (record && IsInitializedPage && (Section != section || _pluginPage != plugin))
        {
            _history.Add((Section, Section == SettingsSection.Plugins ? _pluginPage : null));
        }

        _history.RemoveAll(h => h.Section == section && h.Plugin == plugin);
        if (section == SettingsSection.Home)
        {
            _history.Clear();
        }

        while (_history.Count > 32)
        {
            _history.RemoveAt(0);
        }

        Section = section;
        _pluginPage = plugin;
        IsInitializedPage = true;
        var info = Sections.First(s => s.Section == section);
        if (plugin is not null && _actions.Plugins?.Store.Get(plugin) is { } pl)
        {
            SetPageTitle(pl.Manifest.Name.For(Loc.Code), pl.Symbol);
        }
        else
        {
            SetPageTitle(Loc.T(info.TitleKey), null);
        }
        HelpTopic.SetTopic(this, info.HelpFile);
        if (ParentOf(section) is { } parent)
        {
            _expanded.Add(parent);
        }
        else if (ChildrenOf(section).Count > 0 || section == SettingsSection.Plugins)
        {
            _expanded.Add(section);
        }

        SyncNav();
        if (_backButton is not null)
        {
            _backButton.IsEnabled = CanGoBack;
            _backButton.Visibility = section == SettingsSection.Home ? Visibility.Collapsed : Visibility.Visible;
        }

        BuildSection();
    }

    private bool IsInitializedPage { get; set; }

    /// <summary>Поле с фокусом подтверждает введённое значение до ухода со страницы (числа и цвета применяются при потере фокуса).</summary>
    private void CommitPendingInput()
    {
        if (Keyboard.FocusedElement is TextBox box && IsAncestorOf(box))
        {
            foreach (var n in Descendants<NumberSlider>(_cards).Where(n => ReferenceEquals(n.Box, box)))
            {
                n.Commit();
            }

            Keyboard.ClearFocus();
        }
    }

    /// <summary>Отметки меню: выбран ровно один пункт — текущая страница; группы с выбранной страницей раскрыты.</summary>
    private void SyncNav()
    {
        _switching = true;
        foreach (var rb in NavRadios())
        {
            rb.IsChecked = rb.Tag switch
            {
                SettingsSection t => t == Section && _pluginPage is null,
                string id => Section == SettingsSection.Plugins && id == _pluginPage,
                _ => false,
            };
        }

        foreach (var (group, toggle, panel) in _groups)
        {
            bool open = _expanded.Contains(group);
            toggle.IsChecked = open;
            panel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        _switching = false;
        // Выбранный пункт всегда виден (полоса прокрутки скрыта): после раскладки — прокрутить к нему с запасом на затухание краёв.
        if (Descendants<RadioButton>(_nav).FirstOrDefault(r => r.IsChecked == true) is { } selected)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (selected.IsVisible)
                {
                    selected.BringIntoView(new Rect(0, -16, selected.ActualWidth, selected.ActualHeight + 44));
                }
            });
        }
    }

    private readonly List<(SettingsSection Group, ToggleButton Toggle, StackPanel Panel)> _groups = new();

    /// <summary>Полная перестройка на текущем языке (вызывается и при смене языка).</summary>
    private void Rebuild()
    {
        if (_released)
        {
            return;
        }

        Title = Loc.T("Settings.WindowTitle");
        DetachFromParent(_preview.Root);
        Content = BuildLayout();
        RebuildNav();
        UpdateStatus();
        Navigate(Section, _pluginPage, record: false);
        ApplyNavMode(ActualWidth > 0 ? ActualWidth : Width);
    }

    private void RebuildNav()
    {
        _nav.Children.Clear();
        _groups.Clear();
        BuildNav();
        SyncNav();
    }

    private UIElement BuildLayout()
    {
        var sidebar = new DockPanel { Width = 288 };
        _sidebar = sidebar;
        sidebar.SetResourceReference(Panel.BackgroundProperty, "Ko.SidebarBrush");
        // Узкое окно: кнопка меню раскрывает полное меню поверх страницы (в обычном режиме скрыта).
        _menuButton = WindowShell.Interactive(_ui.IconButton("panel-left", Loc.T("Settings.Menu"), () => SetNavOpen(!_navOpen), subtle: true));
        _menuButton.Margin = new Thickness(14, 6, 14, 2);
        _menuButton.HorizontalAlignment = HorizontalAlignment.Left;
        _menuButton.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(_menuButton, Dock.Top);
        sidebar.Children.Add(_menuButton);
        // Логотип над меню открывает «Главную» (отдельного пункта нет): в широком меню — знак и название, в компактном — крупный
        // контурный знак. Подсказка и доступное имя — «Главная», активация — Enter/пробел, как у пунктов меню.
        _home = HomeButton();
        DockPanel.SetDock(_home, Dock.Top);
        sidebar.Children.Add(_home);
        // Внизу один вход в справку — «Справка» (F1 открывает раздел о текущей странице) — и «Свернуть в трей».
        var bottom = new StackPanel { Margin = new Thickness(12, 4, 12, 14) };
        _bottomActions = bottom;
        var helpButton = SmallAction("circle-question-mark", Loc.T("Settings.HelpButton"), () => HelpService.Show(HelpService.ResolveTopic(this)));
        helpButton.ToolTip = Loc.T("Settings.HelpButtonHint");
        _helpButton = helpButton;
        _trayButton = null;
        bottom.Children.Add(helpButton);
        if (_actions.HideToTray is { } hide)
        {
            var trayButton = SmallAction("minimize-2", Loc.T("Settings.ToTray"), hide);
            _trayButton = trayButton;
            trayButton.ToolTip = Loc.T("Settings.ToTrayHint");
            bottom.Children.Add(trayButton);
        }

        DockPanel.SetDock(bottom, Dock.Bottom);
        sidebar.Children.Add(bottom);
        DetachFromParent(_nav);
        // Полоса прокрутки меню скрыта (она перекрывала квадратное выделение в компактном меню), прокрутка остаётся: колесо,
        // тачпад, клавиатура (фокус сам прокручивает к пункту) и показ выбранного пункта при переходе. Края — мягкое затухание
        // (маска прозрачности не перехватывает щелчки); у конца списка нижнего затухания нет — последний пункт виден целиком.
        var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _nav, Focusable = false, PanningMode = PanningMode.VerticalOnly };
        _navScroll = navScroll;
        navScroll.ScrollChanged += (_, _) => EdgeFade(navScroll, 14, 28);
        navScroll.SizeChanged += (_, _) => EdgeFade(navScroll, 14, 28);
        sidebar.Children.Add(navScroll);

        // Заголовок: «Назад», крупное название (переносится, не обрезается) и «Сбросить раздел»; если рядом с названием мало
        // места, кнопка переходит под него.
        // Верхняя строка: «Назад», название страницы, «Сбросить раздел»; справа место для кнопок окна (встроенный заголовок).
        // Phase 4I: общий компонент PageHeader — «Назад» и действия центрированы по первой строке названия, действия у правого
        // края (тот же отступ 24, что у карточек) ниже кнопок окна; название не заходит под кнопки окна. Действует на всех
        // страницах, включая страницы плагинов, без отдельных отступов.
        var header = new PageHeader
        {
            Margin = new Thickness(24, 22, 24, 14), // на одной линии со знаком над меню
            CaptionReserve = WindowShell.CaptionButtonsWidth - 24 + 6,
            CaptionBottom = WindowShell.CaptionButtonsBottom - 22 + 2,
        };
        _header = header;
        _backButton = WindowShell.Interactive(_ui.IconButton("arrow-left", Loc.T("Settings.Back"), GoBack, subtle: true));
        _backButton.Margin = new Thickness(0);
        header.Children.Add(_backButton);
        // Phase 4H: «Сбросить раздел» — значок с подсказкой и доступным именем (раздел назван в заголовке рядом).
        var reset = WindowShell.Interactive(_ui.Action(ActionIcons.ResetSection, Loc.T("Settings.ResetSection"), ResetSection));
        _resetButton = reset;
        reset.Margin = new Thickness(0);
        DetachFromParent(_title);
        header.Children.Add(_title);
        header.Children.Add(reset);

        // Уведомление об ошибке записи профиля или настроек: понятный текст и «Повторить» (успех не изображается).
        _statusBar = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(32, 0, 24, 12), Visibility = Visibility.Collapsed };
        _statusBar.SetResourceReference(Border.BackgroundProperty, "Ko.SelectedBrush");
        _statusBar.SetResourceReference(Border.BorderBrushProperty, "Ko.ErrorBrush");
        DockPanel.SetDock(_statusBar, Dock.Top);

        DetachFromParent(_previewHost);
        _previewCard = new ClipBorder
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = _previewHost,
        };
        PageLayout.SetIsPreview(_previewCard, true);
        _previewCard.SetResourceReference(Border.BorderBrushProperty, "Ko.CardBorderBrush");
        _previewCard.SetResourceReference(Border.BackgroundProperty, "Ko.StageBrush");

        DetachFromParent(_cards);
        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _cards, Focusable = false };
        // Края прокрутки: сверху содержимое мягко растворяется на 18 DIP, снизу — на 32 DIP (переход в фон окна, а не в рабочий
        // стол), только пока в эту сторону есть что прокручивать; в конце последняя карточка видна и нажимается целиком.
        _scroll.ScrollChanged += (_, _) => UpdateScrollFade();
        _scroll.SizeChanged += (_, _) => UpdateScrollFade();
        // Превью над карточками (узкое окно) занимает не больше 40 % видимой высоты — параметры остаются на экране.
        _scroll.SizeChanged += (_, e) => _cards.MaxStackedPreviewHeight = Math.Max(140, e.NewSize.Height * 0.4);
        var scrollArea = new Grid { ClipToBounds = true };
        scrollArea.Children.Add(_scroll);
        ScrollArea = scrollArea;

        // Колонка превью широкой компоновки: на всю высоту области страницы, фиксированная ширина.
        _previewColumn = new Border { Visibility = Visibility.Collapsed };
        _body = new Grid();
        _body.ColumnDefinitions.Add(new ColumnDefinition());
        _body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition());
        _body.Children.Add(scrollArea);
        _body.Children.Add(_previewColumn);
        _body.SizeChanged += (_, e) => PlacePreview(e.NewSize);

        var content = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        content.Children.Add(header);
        content.Children.Add(_statusBar);
        content.Children.Add(_body);
        content.PreviewMouseDown += (_, _) =>
        {
            if (_navOpen)
            {
                SetNavOpen(false); // щелчок по странице закрывает раскрытое поверх неё меню
            }
        };

        var root = new Grid();
        _root = root;
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(288) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Panel.SetZIndex(sidebar, 2);
        root.Children.Add(sidebar);
        return root;
    }

    /// <summary>
    /// Меню слева: в широком окне — с подписями; уже <see cref="CompactNavWidth"/> — компактное (значки с подсказками), а кнопка
    /// меню раскрывает полное меню поверх страницы. Переключение не перестраивает страницу и не сбрасывает значения.
    /// </summary>
    private void ApplyNavMode(double windowWidth)
    {
        bool compact = windowWidth > 0 && windowWidth < CompactNavWidth;
        if (compact != _compactNav)
        {
            _compactNav = compact;
            _navOpen = false;
        }

        UpdateNavLayout();
    }

    private void SetNavOpen(bool open)
    {
        if (_navOpen == open || (open && !_compactNav))
        {
            return;
        }

        _navOpen = open;
        UpdateNavLayout();
    }

    private const double CompactNavPanelWidth = 68, FullNavWidth = 288, NavIconCell = 20, CompactNavItem = 44;

    private void UpdateNavLayout()
    {
        if (_root is null || _sidebar is null)
        {
            return;
        }

        bool icons = _compactNav && !_navOpen;
        _root.ColumnDefinitions[0].Width = new GridLength(_compactNav ? CompactNavPanelWidth : FullNavWidth);
        _sidebar.Width = icons ? CompactNavPanelWidth : FullNavWidth;
        _sidebar.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumnSpan(_sidebar, _compactNav && _navOpen ? 2 : 1);
        _sidebar.Effect = _compactNav && _navOpen ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.35 } : null;
        if (_menuButton is not null)
        {
            _menuButton.Visibility = _compactNav ? Visibility.Visible : Visibility.Collapsed;
            _menuButton.Margin = icons ? new Thickness(14, 6, 0, 2) : new Thickness(14, 6, 14, 2);
        }

        // Компактное меню: центры всех значков на одной вертикали (x = 31 DIP): пункт — поле 8 + рамка 1 + отступ 12, кнопки внизу —
        // поле 6 + рамка 1 + отступ 14, кнопка меню — поле 14 + половина ширины 17.
        _nav.Margin = icons ? new Thickness(8, 4, 8, 12) : new Thickness(12, 4, 12, 12);
        if (_bottomActions is not null)
        {
            _bottomActions.Margin = icons ? new Thickness(6, 4, 6, 14) : new Thickness(12, 4, 12, 14);
        }
        if (_home is { Content: StackPanel { Children: [FrameworkElement logo, TextBlock name] } })
        {
            // Компактное меню: крупный контурный знак по центру столбца значков (ячейка 44, поле 8), без названия.
            name.Visibility = icons ? Visibility.Collapsed : Visibility.Visible;
            logo.Margin = icons ? new Thickness(0) : new Thickness(0, 0, 10, 0);
            logo.Width = logo.Height = icons ? 34 : 40;
            // Фирменный знак над меню — с заметным отступом от верхнего края окна (кнопки окна стоят в той же верхней зоне).
            _home.Margin = icons ? new Thickness(8, 14, 8, 10) : new Thickness(12, 18, 12, 12);
            _home.Padding = icons ? new Thickness(0) : new Thickness(10, 6, 10, 6);
            _home.Width = icons ? CompactNavItem : double.NaN;
            _home.Height = icons ? CompactNavItem + 4 : double.NaN;
            _home.HorizontalAlignment = HorizontalAlignment.Left;
            _home.HorizontalContentAlignment = icons ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        }

        foreach (var rb in NavRadios().Where(r => r != _home))
        {
            if (rb.Content is StackPanel { Children: [_, TextBlock text] })
            {
                text.Visibility = icons ? Visibility.Collapsed : Visibility.Visible;
            }

            rb.ToolTip = icons ? System.Windows.Automation.AutomationProperties.GetName(rb) : null;
            if (icons)
            {
                // Компактное меню: квадратная плашка выделения (44×44), подпункты без отступа вложенности — один столбец значков.
                rb.Padding = new Thickness(0);
                rb.Width = rb.Height = CompactNavItem;
                rb.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                rb.ClearValue(Control.PaddingProperty);
                rb.ClearValue(WidthProperty);
                rb.ClearValue(HeightProperty);
                rb.ClearValue(HorizontalAlignmentProperty);
            }
        }

        foreach (var (_, toggle, _) in _groups)
        {
            toggle.Visibility = icons ? Visibility.Collapsed : Visibility.Visible;
        }

        foreach (var b in new[] { _helpButton, _trayButton })
        {
            if (b?.Content is StackPanel { Children: [_, TextBlock label] })
            {
                label.Visibility = icons ? Visibility.Collapsed : Visibility.Visible;
                b.ToolTip = icons ? System.Windows.Automation.AutomationProperties.GetName(b) : b == _helpButton ? Loc.T("Settings.HelpButtonHint") : Loc.T("Settings.ToTrayHint");
            }
        }
    }

    /// <summary>
    /// Название страницы; у страницы плагина перед ним — значок плагина (тот же, что в меню и карточке; Phase 4I), по центру
    /// первой строки. Доступное имя — только название.
    /// </summary>
    private void SetPageTitle(string text, string? symbol)
    {
        _title.Inlines.Clear();
        if (symbol is not null)
        {
            var icon = Icons.Lucide.Icon(symbol, Math.Round(_title.FontSize * 0.72), "Ko.LogoBrush");
            icon.Margin = new Thickness(0, 0, Math.Round(_title.FontSize * 0.3), 0);
            icon.Tag = "page-symbol";
            _title.Inlines.Add(new System.Windows.Documents.InlineUIContainer(icon) { BaselineAlignment = BaselineAlignment.Center });
        }

        _title.Inlines.Add(new System.Windows.Documents.Run(text));
        System.Windows.Automation.AutomationProperties.SetName(_title, text);
    }

    /// <summary>Значок, показанный перед названием страницы плагина (для проверок), или null.</summary>
    internal string? PageSymbol => _pluginPage is { } id && _actions.Plugins?.Store.Get(id) is { } p ? p.Symbol : null;

    private void BuildNav()
    {
        foreach (var (section, titleKey, _, parent, icon) in Sections)
        {
            if (parent is not null || !SectionAvailable(section) || section is SettingsSection.Help or SettingsSection.Home)
            {
                continue;
            }

            var item = NavButton(section, Loc.T(titleKey), icon, sub: false);
            var children = Sections.Where(x => x.Parent == section && SectionAvailable(x.Section))
                .Select(x => NavButton(x.Section, Loc.T(x.TitleKey), x.Icon, sub: true)).ToList();
            if (section == SettingsSection.Plugins && _actions.Plugins is { } pm)
            {
                children.AddRange(pm.Store.List().Where(p => !p.Favorite).Select(p => NavButton(p.Id, p.Manifest.Name.For(Loc.Code), p.Symbol, sub: true, favorite: false)));
            }

            if (children.Count == 0)
            {
                _nav.Children.Add(item);
            }
            else
            {
                AddGroup(section, item, children);
            }

            if (section == SettingsSection.Mouse && _actions.Plugins is { } plugins)
            {
                foreach (var p in plugins.Store.List().Where(p => p.Favorite))
                {
                    _nav.Children.Add(NavButton(p.Id, p.Manifest.Name.For(Loc.Code), p.Symbol, sub: false, favorite: true));
                }
            }
        }
    }

    /// <summary>Пункт группы и стрелка раскрытия; подпункты — под ним (раскрыты, если группа в <see cref="_expanded"/>).</summary>
    private void AddGroup(SettingsSection group, RadioButton item, IReadOnlyList<RadioButton> children)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(item);
        if (item.Content is StackPanel { Children: [_, TextBlock label] })
        {
            label.MaxWidth = 156; // текст заголовка группы заканчивается до стрелки раскрытия
        }

        var toggle = new ToggleButton { Style = UiTheme.Resources.StyleOf("Ko.NavExpander"), Margin = new Thickness(-36, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        string title = System.Windows.Automation.AutomationProperties.GetName(item);
        System.Windows.Automation.AutomationProperties.SetName(toggle, Loc.F("Settings.ExpandGroup", title));
        toggle.ToolTip = Loc.F("Settings.ExpandGroup", title);
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        var panel = new StackPanel();
        foreach (var c in children)
        {
            panel.Children.Add(c);
        }

        toggle.Checked += (_, _) =>
        {
            if (!_switching)
            {
                _expanded.Add(group);
                panel.Visibility = Visibility.Visible;
            }
        };
        toggle.Unchecked += (_, _) =>
        {
            if (!_switching)
            {
                _expanded.Remove(group);
                panel.Visibility = Visibility.Collapsed;
            }
        };
        _groups.Add((group, toggle, panel));
        _nav.Children.Add(row);
        _nav.Children.Add(panel);
    }

    private string? _pluginPage;

    private RadioButton NavButton(object tag, string title, string icon, bool sub, bool favorite = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Icons.Lucide.Icon(icon, sub ? 16 : 19, "Ko.SecondaryTextBrush");
        glyph.Width = NavIconCell; // одна ширина ячейки у пунктов и подпунктов: в компактном меню значки стоят ровным столбцом
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.Margin = new Thickness(0, 0, 12, 0);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.Uid = icon; // имя значка (проверки; у плагинов — выбранный значок плагина)
        content.Children.Add(glyph);
        // Длинные названия (uk/ru) переносятся на вторую строку, а не обрезаются.
        content.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = sub ? 186 : 206 });
        var rb = new RadioButton { Content = content, GroupName = "nav-" + GetHashCode(), Tag = tag, Style = UiTheme.Resources.StyleOf(sub ? "Ko.NavSubItem" : "Ko.NavItem"), Uid = favorite ? "favorite" : "" };
        System.Windows.Automation.AutomationProperties.SetName(rb, title);
        // Выбранный пункт — значок цвета акцента (Phase 4I), остальные — вторичным цветом текста; цвета берутся из темы.
        rb.Checked += (_, _) => Icons.Lucide.Recolor(glyph, "Ko.LogoBrush");
        rb.Unchecked += (_, _) => Icons.Lucide.Recolor(glyph, "Ko.SecondaryTextBrush");
        rb.Checked += (_, _) =>
        {
            if (_switching)
            {
                return;
            }

            if (tag is SettingsSection section)
            {
                Navigate(section, null, record: true);
            }
            else if (tag is string id)
            {
                ShowPlugin(id);
            }
        };
        return rb;
    }

    private RadioButton HomeButton()
    {
        string title = Loc.T("Settings.Nav.Home");
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var logo = LogoArt.Themed(40);
        logo.Margin = new Thickness(0, 0, 10, 0);
        logo.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(logo, title);
        content.Children.Add(logo);
        var name = Wordmark.Create(30, FontWeights.SemiBold);
        content.Children.Add(name);
        var rb = new RadioButton
        {
            Content = content, GroupName = "nav-" + GetHashCode(), Tag = SettingsSection.Home, Uid = "home",
            Style = UiTheme.Resources.StyleOf("Ko.NavHome"), Margin = new Thickness(12, 18, 12, 12), ToolTip = title,
        };
        System.Windows.Automation.AutomationProperties.SetName(rb, title);
        rb.Checked += (_, _) =>
        {
            if (!_switching)
            {
                Navigate(SettingsSection.Home, null, record: true);
            }
        };
        return WindowShell.Interactive(rb); // логотип стоит в области заголовка окна
    }

    private Button SmallAction(string icon, string text, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Icons.Lucide.Icon(icon, 16, "Ko.SecondaryTextBrush");
        glyph.Width = NavIconCell;
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.Margin = new Thickness(0, 0, 10, 0);
        content.Children.Add(glyph);
        content.Children.Add(new TextBlock { Text = text, FontSize = 13.5 });
        var b = _ui.Button("", click);
        b.Style = UiTheme.Resources.StyleOf("Ko.SubtleButton");
        b.Content = content;
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, 0, 0, 2);
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        return b;
    }

    private bool SectionAvailable(SettingsSection section) => section switch
    {
        SettingsSection.Profiles => _profiles is not null,
        SettingsSection.Lens or SettingsSection.CursorZoom or SettingsSection.Drawing => false, // стали плагинами
        SettingsSection.Application or SettingsSection.Hotkeys
            or SettingsSection.Interface or SettingsSection.Privacy => _prefs is not null,
        SettingsSection.Plugins => _actions.Plugins is not null,
        _ => true,
    };

    /// <summary>
    /// Строка уведомления: показывается только если запись профиля или файла настроек не удалась. Изменения при этом уже
    /// применены и не теряются; «Повторить» записывает снова.
    /// </summary>
    private void UpdateStatus()
    {
        if (_statusBar is null)
        {
            return;
        }

        var error = _profiles?.SaveError ?? _store.LastSaveError;
        if (error is null)
        {
            _statusBar.Visibility = Visibility.Collapsed;
            _statusBar.Child = null;
            return;
        }

        var row = new DockPanel();
        var icon = Icons.Lucide.Icon("triangle-alert", 18, "Ko.ErrorBrush");
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        var retry = _ui.Button(Loc.T("Profiles.Retry"), () =>
        {
            _profiles?.Flush();
            _store.Flush();
            UpdateStatus();
        });
        retry.Margin = new Thickness(0);
        var line = new AdaptiveRow { LabelMinWidth = 200 };
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = Loc.T("Profiles.SaveFailed.Title"), Style = UiTheme.Resources.StyleOf("Ko.Label"), FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = error.ToString(), Style = UiTheme.Resources.StyleOf("Ko.Hint") });
        line.Children.Add(text);
        line.Children.Add(retry);
        row.Children.Add(line);
        _statusBar.Child = row;
        _statusBar.Visibility = Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(_statusBar, Loc.T("Profiles.SaveFailed.Title"));
        System.Windows.Automation.AutomationProperties.SetLiveSetting(_statusBar, System.Windows.Automation.AutomationLiveSetting.Assertive);
    }

    private void BuildSection()
    {
        StopPreviews();
        _cards.Children.Clear();
        _enablers.Clear();
        _shownWhen.Clear();
        var s = _store.Current;
        switch (Section)
        {
            case SettingsSection.Home: BuildHome(s); break;
            case SettingsSection.Appearance: BuildAppearance(s); break;
            case SettingsSection.Keys: BuildKeys(s); break;
            case SettingsSection.Mouse: BuildMouse(s); break;
            case SettingsSection.Layout: BuildLayoutSection(s); break;
            case SettingsSection.Position: BuildPosition(s); break;
            case SettingsSection.Hotkeys: BuildCommands(); break;
            case SettingsSection.Plugins: BuildPlugins(); break;
            case SettingsSection.Profiles: BuildProfiles(); break;
            case SettingsSection.Application: BuildApplication(); break;
            case SettingsSection.Interface: BuildInterface(); break;
            case SettingsSection.Privacy: BuildPrivacy(); break;
            default: BuildAbout(); break;
        }

        AddPhase4Cards(s);
        BuildPreview();
        ApplyEnablers(s);
        if (_resetButton is not null)
        {
            _resetButton.Visibility = Section is SettingsSection.Home or SettingsSection.About or SettingsSection.Profiles or SettingsSection.Plugins
                ? Visibility.Collapsed : Visibility.Visible;
        }

        _scroll?.ScrollToTop();
    }

    // ── разделы ──────────────────────────────────────────────────────────────

    private void BuildAppearance(LabSettings s)
    {
        var a = s.Appearance;
        _cards.Children.Add(StyleCatalogCard(Styles.StyleKind.Keycap, s, "Styles.Keycap.Title", "Styles.Keycap.Hint"));
        var renderers = Enum.GetValues<KeycapRenderer>();
        var gradient = _ui.Row(Loc.T("Appearance.Gradient"), Loc.T("Appearance.GradientHint"), _ui.Toggle(a.UseGradient, v => Ap(x => x with { UseGradient = v })));
        var isoEnter = _ui.Row(Loc.T("Appearance.VolEnter"), Loc.T("Appearance.VolEnterHint"), _ui.Toggle(a.VolEnterShape, v => Ap(x => x with { VolEnterShape = v })));
        _shownWhen.Add((gradient, x => x.Appearance.Renderer is KeycapRenderer.Laptop or KeycapRenderer.Pbt));
        _shownWhen.Add((isoEnter, x => x.Appearance.Renderer == KeycapRenderer.Volumetric));
        // Phase 4I: свечение Neon — интенсивность 0…100 % (0 — выключено); только у рендера Neon.
        var glow = _ui.Row(Loc.T("Appearance.Glow"), Loc.T("Appearance.GlowHint"), _ui.Number(Math.Round(a.Glow * 100), (0, 100), 5, "%", v => Ap(x => x with { Glow = v / 100 })));
        _shownWhen.Add((glow, x => x.Appearance.Renderer == KeycapRenderer.Neon));
        _cards.Children.Add(_ui.Card(Loc.T("Appearance.Renderer.Title"), Loc.T("Appearance.Renderer.Hint"),
            _ui.Row(Loc.T("Appearance.Renderer"), Loc.T("Appearance.RendererHint"),
                _ui.Segmented(Array.IndexOf(renderers, a.Renderer), renderers.Select(r => Loc.T("Renderer." + r)).ToArray(), i => Ap(x => x with { Renderer = renderers[i] }))),
            gradient,
            isoEnter,
            glow));
        _cards.Children.Add(_ui.Card(Loc.T("Appearance.Labels.Title"), Loc.T("Appearance.Labels.Hint"),
            _ui.Row(Loc.T("Appearance.Labels.Set"), Loc.T("Appearance.Labels.SetHint"),
                _ui.Segmented((int)a.LabelSet, ["Windows", "macOS", "Linux"], i => Ap(x => x with { LabelSet = (LabelSet)i }))),
            _ui.Row(Loc.T("Appearance.Labels.Mode"), Loc.T("Appearance.Labels.ModeHint"),
                _ui.Segmented(a.LabelMode switch { LabelMode.Mixed => 0, LabelMode.Symbol => 1, _ => 2 }, [Loc.T("Appearance.Labels.Mixed"), Loc.T("Appearance.Labels.Symbols"), Loc.T("Appearance.Labels.Text")],
                    i => Ap(x => x with { LabelMode = i switch { 0 => LabelMode.Mixed, 1 => LabelMode.Symbol, _ => LabelMode.Text } }))),
            _ui.Row(Loc.T("Appearance.Markers"), Loc.T("Appearance.MarkersHint"), _ui.Toggle(a.SideMarkers, v => Ap(x => x with { SideMarkers = v }))),
            _ui.Row(Loc.T("Appearance.PressCount"), Loc.T("Appearance.PressCountHint"), _ui.Toggle(a.ShowPressCount, v => Ap(x => x with { ShowPressCount = v })))));

        _cards.Children.Add(_ui.Card(Loc.T("Appearance.Keycap.Title"), Loc.T("Appearance.Keycap.Hint"),
            _ui.Row(Loc.T("Appearance.Keycap.Size"), Loc.T("Appearance.Keycap.SizeHint"), _ui.Number(a.Size, Limits.Size, 1, "DIP", v => Ap(x => x with { Size = v }))),
            _ui.Row(Loc.T("Appearance.Keycap.Radius"), Loc.T("Appearance.Keycap.RadiusHint"), _ui.Number(a.Radius, Limits.Radius, 0.01, "", v => Ap(x => x with { Radius = v })))));

        var borderWidth = _ui.Row(Loc.T("Appearance.Colors.BorderWidth"), null, _ui.Number(a.BorderWidth, Limits.BorderWidth, 0.5, "DIP", v => Ap(x => x with { BorderWidth = v })));
        var borderColor = _ui.Row(Loc.T("Appearance.Colors.BorderColor"), null, _ui.Color(a.BorderColor, true, c => Ap(x => x with { BorderColor = c })));
        var bgColor = _ui.Row(Loc.T("Appearance.Colors.BackgroundColor"), Loc.T("Appearance.Colors.AlphaHint"), _ui.Color(a.BackgroundColor, true, c => Ap(x => x with { BackgroundColor = c })));
        _enablers.Add((borderWidth, x => x.Appearance.BorderEnabled));
        _enablers.Add((borderColor, x => x.Appearance.BorderEnabled));
        _enablers.Add((bgColor, x => x.Appearance.BackgroundEnabled));
        _cards.Children.Add(_ui.Card(Loc.T("Appearance.Colors.Title"), null,
            _ui.Row(Loc.T("Appearance.Colors.Face"), null, _ui.Color(a.Primary, true, c => Ap(x => x with { Primary = c }))),
            _ui.Row(Loc.T("Appearance.Colors.Skirt"), Loc.T("Appearance.Colors.SkirtHint"), _ui.Color(a.Secondary, true, c => Ap(x => x with { Secondary = c }))),
            _ui.Row(Loc.T("Appearance.Colors.Text"), null, _ui.Color(a.TextColor, true, c => Ap(x => x with { TextColor = c }))),
            _ui.Row(Loc.T("Appearance.Colors.Border"), null, _ui.Toggle(a.BorderEnabled, v => Ap(x => x with { BorderEnabled = v }))),
            borderWidth,
            borderColor,
            _ui.Row(Loc.T("Appearance.Colors.Background"), null, _ui.Toggle(a.BackgroundEnabled, v => Ap(x => x with { BackgroundEnabled = v }))),
            bgColor));

        // Phase 4H: материал подложки группы (заливка или прозрачная) с образцом на светлом и тёмном фоне.
        var backdrop = _ui.Card(Loc.T("Backdrop.Keycaps.Title"), Loc.T("Backdrop.Keycaps.Hint"),
            BackdropRows(a.Backdrop, v => { Ap(x => x with { Backdrop = v }); Dispatcher.BeginInvoke(BuildSection); }, () => KeycapThumb(_store.Current.Appearance)));
        _enablers.Add((backdrop, x => x.Appearance.BackgroundEnabled));
        _cards.Children.Add(backdrop);

        var duration = _ui.Row(Loc.T("Appearance.Anim.Duration"), null, _ui.Number(a.DurationMs, (Limits.DurationMs.Min, Limits.DurationMs.Max), 10, Loc.T("Unit.Ms"), v => Ap(x => x with { DurationMs = (int)v })));
        _shownWhen.Add((duration, x => x.Appearance.Animation == AnimationKind.Fade));
        _cards.Children.Add(_ui.Card(Loc.T("Appearance.Anim.Title"), null,
            _ui.Row(Loc.T("Appearance.Anim.EnterExit"), null, _ui.Segmented(a.Animation == AnimationKind.Fade ? 0 : 1, [Loc.T("Common.Smooth"), Loc.T("Common.NoAnimation")], i => Ap(x => x with { Animation = i == 0 ? AnimationKind.Fade : AnimationKind.None }))),
            duration,
            _ui.Row(Loc.T("Appearance.Anim.Press"), Loc.T("Appearance.Anim.PressHint"), _ui.Toggle(a.PressAnimation, v => Ap(x => x with { PressAnimation = v })))));
    }

    private void BuildKeys(LabSettings s)
    {
        var k = s.Keys;
        _cards.Children.Add(_ui.Card(Loc.T("Keys.Show.Title"), null,
            _ui.Row(Loc.T("Keys.Filter"), Loc.T("Keys.FilterHint"),
                _ui.Segmented(k.Filter == KeyFilter.All ? 0 : 1, [Loc.T("Keys.Filter.All"), Loc.T("Keys.Filter.Combos")], i => Ks(x => x with { Filter = i == 0 ? KeyFilter.All : KeyFilter.CombosOnly }))),
            _ui.Row(Loc.T("Keys.MouseInRow"), Loc.T("Keys.MouseInRowHint"), _ui.Toggle(k.MouseInRow, v => Ks(x => x with { MouseInRow = v }))),
            _ui.Row(Loc.T("Keys.MouseLabels"), Loc.T("Keys.MouseLabelsHint"),
                _ui.Segmented(k.MouseLabels == MouseLabelMode.Graphic ? 0 : 1, [Loc.T("Keys.MouseLabels.Icon"), Loc.T("Keys.MouseLabels.Text")], i => Ks(x => x with { MouseLabels = i == 0 ? MouseLabelMode.Graphic : MouseLabelMode.Text })))));

        var threshold = _ui.Row(Loc.T("Keys.AnyHeldMs"), Loc.T("Keys.AnyHeldMsHint"),
            _ui.Number(k.AnyHeldMs, (Limits.AnyHeldMs.Min, Limits.AnyHeldMs.Max), 10, Loc.T("Unit.Ms"), v => Ks(x => x with { AnyHeldMs = (int)v })));
        _shownWhen.Add((threshold, x => x.Keys.Chords == ChordMode.AnyHeld));
        _cards.Children.Add(_ui.Card(Loc.T("Keys.Chords.Title"), Loc.T("Keys.Chords.Hint"),
            _ui.Row(Loc.T("Keys.Chords"), null,
                _ui.Segmented(k.Chords == ChordMode.Modifiers ? 0 : 1, [Loc.T("Keys.Chords.Modifiers"), Loc.T("Keys.Chords.AnyHeld")], i => Ks(x => x with { Chords = i == 0 ? ChordMode.Modifiers : ChordMode.AnyHeld }))),
            threshold,
            _ui.Note(Loc.T("Keys.Chords.Note")),
            _ui.Note(Loc.T("Keys.Chords.Limits"))));

        _cards.Children.Add(_ui.Card(Loc.T("Keys.Typing.Title"), null,
            _ui.Row(Loc.T("Keys.Trail"), Loc.T("Keys.TrailHint"),
                _ui.Segmented(k.Trail == TrailMode.None ? 0 : 1, [Loc.T("Keys.Trail.None"), Loc.T("Keys.Trail.Fade")], i => Ks(x => x with { Trail = i == 0 ? TrailMode.None : TrailMode.FadeInPlace }))),
            _ui.Row(Loc.T("Keys.Linger"), Loc.T("Keys.LingerHint"), _ui.Number(k.LingerMs, (0, 20_000), 100, Loc.T("Unit.Ms"), v => Ks(x => x with { LingerMs = (int)v }))),
            _ui.Note(Loc.T("Keys.ChordNote"))));
    }

    private void BuildMouse(LabSettings s)
    {
        var c = s.MouseCursor;
        var m = s.Appearance.MouseLook;
        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Cursor.Title"), null,
            _ui.Row(Loc.T("Mouse.Cursor.Enable"), Loc.T("Mouse.Cursor.EnableHint"), _ui.Toggle(c.Enabled, v => Mc(x => x with { Enabled = v })))));
        _cards.Children.Add(StyleCatalogCard(Styles.StyleKind.Mouse, s, "Styles.Mouse.Title", "Styles.Mouse.Hint"));

        var mouseRenderers = Enum.GetValues<MouseRenderer>();
        var body = _ui.Row(Loc.T("Mouse.Body"), Loc.T("Mouse.BodyHint"), _ui.Color(m.Body, true, col => Mv(x => x with { BodyColor = col })));
        _shownWhen.Add((body, x => x.Appearance.MouseLook.Renderer is MouseRenderer.Retro or MouseRenderer.Sketch or MouseRenderer.Outline or MouseRenderer.Neon));
        var mouseGlow = _ui.Row(Loc.T("Appearance.Glow"), Loc.T("Mouse.GlowHint"), _ui.Number(Math.Round(m.Glow * 100), (0, 100), 5, "%", v => Mv(x => x with { Glow = v / 100 })));
        _shownWhen.Add((mouseGlow, x => x.Appearance.MouseLook.Renderer == MouseRenderer.Neon));
        // Толщина контура — у всех семейств; контур сплошного значка — включается отдельно (Phase 4G).
        var outlineWidth = _ui.Row(Loc.T("Mouse.OutlineWidth"), null, _ui.Number(m.OutlineWidth, Limits.MouseOutline, 0.2, "", v => Mv(x => x with { OutlineWidth = Math.Round(v, 1) })));
        _shownWhen.Add((outlineWidth, x => x.Appearance.MouseLook.Renderer != MouseRenderer.Solid || x.Appearance.MouseLook.SolidOutline is not null));
        var solidOutline = _ui.Row(Loc.T("Mouse.SolidOutline"), Loc.T("Mouse.SolidOutlineHint"),
            _ui.Toggle(m.SolidOutline is not null, v =>
            {
                Mv(x => x with { SolidOutline = v ? x.IconBackground with { A = 0xFF } : null });
                Dispatcher.BeginInvoke(BuildSection); // цвет контура — только когда контур включён
            }));
        var solidOutlineColor = _ui.Row(Loc.T("Mouse.SolidOutlineColor"), null,
            _ui.Color(m.SolidOutline ?? m.IconBackground with { A = 0xFF }, true, col => Mv(x => x with { SolidOutline = col })));
        solidOutline.Visibility = m.Renderer == MouseRenderer.Solid ? Visibility.Visible : Visibility.Collapsed;
        solidOutlineColor.Visibility = m.Renderer == MouseRenderer.Solid && m.SolidOutline is not null ? Visibility.Visible : Visibility.Collapsed;
        var sideWhere = _ui.Row(Loc.T("Mouse.SideButtons.Side"), null,
            _ui.Segmented(m.SideButtonsSide == SideButtonsSide.Left ? 0 : 1, [Loc.T("Mouse.SideButtons.Left"), Loc.T("Mouse.SideButtons.Right")], i => Mv(x => x with { SideButtonsSide = i == 0 ? SideButtonsSide.Left : SideButtonsSide.Right })));
        _shownWhen.Add((sideWhere, x => x.Appearance.MouseLook.SideButtons != SideButtonsMode.Hidden));
        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Look.Title"), Loc.T("Mouse.Look.Hint"),
            _ui.Row(Loc.T("Mouse.Renderer"), null,
                _ui.Segmented(Array.IndexOf(mouseRenderers, m.Renderer), mouseRenderers.Select(r => Loc.T("MouseRenderer." + r)).ToArray(), i =>
                {
                    Mv(x => x with { Renderer = mouseRenderers[i] });
                    Dispatcher.BeginInvoke(BuildSection); // поля, относящиеся только к выбранному семейству
                })),
            body,
            mouseGlow,
            outlineWidth,
            solidOutline,
            solidOutlineColor,
            _ui.Row(Loc.T("Mouse.SideButtons"), Loc.T("Mouse.SideButtonsHint"),
                _ui.Segmented((int)m.SideButtons, [Loc.T("Mouse.SideButtons.Hidden"), Loc.T("Mouse.SideButtons.One"), Loc.T("Mouse.SideButtons.Two")], i => Mv(x => x with { SideButtons = (SideButtonsMode)i }))),
            sideWhere,
            _ui.Note(Loc.T("Mouse.SideButtons.Note"))));

        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Wheel.Title"), Loc.T("Mouse.Wheel.Hint"),
            _ui.Row(Loc.T("Mouse.Wheel.Color"), null, _ui.Color(m.WheelColor, true, col => Mv(x => x with { WheelArrowColor = col }))),
            _ui.Row(Loc.T("Mouse.Wheel.Duration"), Loc.T("Mouse.Wheel.DurationHint"), _ui.Number(m.WheelPulseMs, (Limits.WheelPulseMs.Min, Limits.WheelPulseMs.Max), 50, Loc.T("Unit.Ms"), v => Mv(x => x with { WheelPulseMs = (int)v }))),
            _ui.Note(Loc.T("Mouse.Wheel.Note"))));

        var iconRows = new UIElement[]
        {
            _ui.Row(Loc.T("Mouse.Icon"), Loc.T("Mouse.IconHint"), _ui.Toggle(c.ShowIcon, v => Mc(x => x with { ShowIcon = v }))),
            _ui.Row(Loc.T("Mouse.IconSize"), null, _ui.Number(m.IconSize, Limits.IconSize, 1, "DIP", v => Mv(x => x with { IconSize = v }))),
            _ui.Row(Loc.T("Common.OffsetRight"), Loc.T("Mouse.OffsetRightHint"), _ui.Number(c.OffsetX, Limits.CursorOffset, 1, "DIP", v => Mc(x => x with { OffsetX = v }))),
            _ui.Row(Loc.T("Common.OffsetDown"), Loc.T("Common.OffsetDownHint"), _ui.Number(c.OffsetY, Limits.CursorOffset, 1, "DIP", v => Mc(x => x with { OffsetY = v }))),
            _ui.Row(Loc.T("Mouse.Outline"), null, _ui.Color(m.IconForeground, true, col => Mv(x => x with { IconForeground = col }))),
            _ui.Row(Loc.T("Mouse.IconBackground"), null, _ui.Color(m.IconBackground, true, col => Mv(x => x with { IconBackground = col }))),
            _ui.Row(Loc.T("Mouse.Pressed"), null, _ui.Color(m.IconAccent, true, col => Mv(x => x with { IconAccent = col }))),
        };
        foreach (var r in iconRows.Skip(1))
        {
            _enablers.Add((r, x => x.MouseCursor.Enabled && x.MouseCursor.ShowIcon));
        }

        var plate = _ui.Card(Loc.T("Backdrop.Mouse.Title"), Loc.T("Backdrop.Mouse.Hint"),
            BackdropRows(m.Plate, v => { Mv(x => x with { Plate = v }); Dispatcher.BeginInvoke(BuildSection); }, () => MouseThumb(_store.Current.Appearance.MouseLook)));
        _enablers.Add((plate, x => x.MouseCursor.Enabled && x.MouseCursor.ShowIcon));

        _enablers.Add((iconRows[0], x => x.MouseCursor.Enabled));
        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Icon.Title"), null, iconRows));
        _cards.Children.Add(plate);

        var circleRows = new UIElement[]
        {
            _ui.Row(Loc.T("Mouse.Circle"), null, _ui.Toggle(c.ShowCircle, v => Mc(x => x with { ShowCircle = v }))),
            _ui.Row(Loc.T("Mouse.CircleSize"), null, _ui.Number(m.ClickSize, Limits.ClickSize, 1, "DIP", v => Mv(x => x with { ClickSize = v }))),
            _ui.Row(Loc.T("Mouse.CircleColor"), null, _ui.Color(m.ClickColor with { A = 0xFF }, false, col => Mv(x => x with { ClickColor = col with { A = x.ClickColor.A } }))),
            _ui.Row(Loc.T("Mouse.CircleOpacity"), null, _ui.Number(Math.Round(m.ClickColor.A / 2.55), (0, 100), 1, "%", v => Mv(x => x with { ClickColor = x.ClickColor with { A = (byte)Math.Round(v * 2.55) } }))),
        };
        foreach (var r in circleRows.Skip(1))
        {
            _enablers.Add((r, x => x.MouseCursor.Enabled && x.MouseCursor.ShowCircle));
        }

        _enablers.Add((circleRows[0], x => x.MouseCursor.Enabled));
        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Circle.Title"), Loc.T("Mouse.Circle.Hint"), circleRows));

        var highlightRows = new UIElement[]
        {
            _ui.Row(Loc.T("Mouse.Highlight"), Loc.T("Mouse.HighlightHint"), _ui.Toggle(c.ShowHighlight, v => Mc(x => x with { ShowHighlight = v, Enabled = v || x.Enabled }))),
            _ui.Row(Loc.T("Mouse.HighlightColor"), Loc.T("Appearance.Colors.AlphaHint"), _ui.Color(c.Highlight, true, col => Mc(x => x with { HighlightColor = col }))),
            _ui.Row(Loc.T("Mouse.HighlightSize"), null, _ui.Number(c.HighlightSize, Limits.HighlightSize, 2, "DIP", v => Mc(x => x with { HighlightSize = v }))),
        };
        foreach (var r in highlightRows.Skip(1))
        {
            _enablers.Add((r, x => x.MouseCursor.Enabled && x.MouseCursor.ShowHighlight));
        }

        _cards.Children.Add(_ui.Card(Loc.T("Mouse.Highlight.Title"), null, highlightRows));
        _cards.Children.Add(_ui.Card(Loc.T("Mouse.InRow.Title"), null, _ui.Note(Loc.T("Mouse.InRow.Note"))));
    }

    private void BuildLayoutSection(LabSettings s)
    {
        // Phase 4H: показ индикатора (срок, поведение, место, анимация) — в плагине «Индикатор раскладки». Здесь — стиль индикатора
        // из системы стилей: его использует плагин, если в нём выбран вид «Стиль индикатора».
        var look = s.Appearance.IndicatorLook;
        _cards.Children.Add(_ui.Card(Loc.T("IndicatorStyle.Title"), Loc.T("IndicatorStyle.Hint"),
            _ui.Row(Loc.T("Common.Example"), null, _ui.Button(Loc.T("Layout.ShowExample"), () => _actions.ShowLayoutIndicatorExample()))));

        var custom = new UIElement[]
        {
            _ui.Row(Loc.T("Layout.TextSize"), null, _ui.Number(look.TextSize, Limits.IndicatorTextSize, 1, "DIP", v => Iv(x => x with { TextSize = v }))),
            _ui.Row(Loc.T("Layout.TextColor"), null, _ui.Color(look.Foreground, true, c => Iv(x => x with { Foreground = c }))),
            _ui.Row(Loc.T("Layout.Background"), Loc.T("Appearance.Colors.AlphaHint"), _ui.Color(look.Background, true, c => Iv(x => x with { Background = c }))),
            _ui.Row(Loc.T("Layout.BorderWidth"), Loc.T("Layout.BorderWidthHint"), _ui.Number(look.BorderWidth, Limits.IndicatorBorderWidth, 0.5, "DIP", v => Iv(x => x with { BorderWidth = v }))),
            _ui.Row(Loc.T("Layout.BorderColor"), null, _ui.Color(look.Border, true, c => Iv(x => x with { Border = c }))),
            _ui.Row(Loc.T("Layout.Radius"), Loc.T("Layout.RadiusHint"), _ui.Number(look.Radius, Limits.Radius, 0.01, "", v => Iv(x => x with { Radius = v }))),
        };
        _cards.Children.Add(StyleCatalogCard(Styles.StyleKind.Indicator, s, "Styles.Indicator.Title", "Styles.Indicator.Hint"));
        _cards.Children.Add(_ui.Card(Loc.T("Layout.Look.Title"), null, custom));
    }

    private void BuildPosition(LabSettings s)
    {
        var p = s.Placement;
        var edgeX = _ui.Row(Loc.T("Position.EdgeX"), Loc.T("Position.EdgeXHint"), _ui.Number(p.EdgeX, Limits.Edge, 1, "DIP", v => Pl(x => x with { EdgeX = v })));
        var edgeY = _ui.Row(Loc.T("Position.EdgeY"), Loc.T("Position.EdgeYHint"), _ui.Number(p.EdgeY, Limits.Edge, 1, "DIP", v => Pl(x => x with { EdgeY = v })));
        _enablers.Add((edgeX, x => OverlayPlacement.EdgeApplies(x.Placement.Anchor).X));
        _enablers.Add((edgeY, x => OverlayPlacement.EdgeApplies(x.Placement.Anchor).Y));
        _cards.Children.Add(_ui.Card(Loc.T("Position.Title"), Loc.T("Position.Hint"),
            _ui.Row(Loc.T("Position.Anchor"), null, _ui.AnchorPicker(p.Anchor, a => Pl(x => x with { Anchor = a }))),
            edgeX,
            edgeY,
            _ui.Row(Loc.T("Common.OffsetRight"), Loc.T("Common.OffsetRightHint"), _ui.Number(p.OffsetX, Limits.Offset, 1, "DIP", v => Pl(x => x with { OffsetX = v }))),
            _ui.Row(Loc.T("Common.OffsetDown"), Loc.T("Common.OffsetDownHint"), _ui.Number(p.OffsetY, Limits.Offset, 1, "DIP", v => Pl(x => x with { OffsetY = v }))),
            _ui.Row(Loc.T("Position.Reset"), null, _ui.Action(ActionIcons.Restore, Loc.T("Position.ResetButton"), () => { U(x => x with { Placement = PlacementSettings.Default }); BuildSection(); })),
            _ui.Note(Loc.T("Position.Note"))));
    }

    /// <summary>
    /// «Профили» (Phase 4F): изменения сохраняются в текущий профиль автоматически, поэтому кнопок «Сохранить» и отметок
    /// «не сохранён» нет. Здесь — экспорт текущего профиля, загрузка другого (из списка или из файла) и управление списком.
    /// </summary>
    private void BuildProfiles()
    {
        var session = _profiles!;
        var current = new List<UIElement>
        {
            _ui.Row(Loc.T("Profiles.Active"), Loc.T("Profiles.AutosaveHint"),
                new TextBlock { Text = session.ActiveName, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 }),
        };
        var buttons = _ui.Toolbar(
            [_ui.Button(ActionIcons.Export, Loc.T("Profiles.ExportCurrent"), ExportCurrentProfile, primary: true)],
            [
                _ui.Action(ActionIcons.Import, Loc.T("Profiles.Import"), ImportProfile),
                _ui.Action(ActionIcons.Duplicate, Loc.T("Profiles.Duplicate"), () => { session.Duplicate(session.Active!.Id, n => Loc.F("Profiles.CopyName", n)); BuildSection(); }),
                _ui.Action(ActionIcons.Rename, Loc.T("Profiles.Rename"), () => RenameProfile(new ProfileInfo(session.Active!.Id, session.ActiveName))),
            ]);
        current.Add(buttons);
        if (session.WorkingCopyName is { } copy)
        {
            current.Add(_ui.Note(Loc.F("Profiles.WorkingCopyNote", copy)));
        }

        current.Add(_ui.Note(Loc.T("Profiles.IncludesNote")));
        _cards.Children.Add(_ui.Card(Loc.T("Profiles.Active.Title"), null, current.ToArray()));

        var rows = new List<UIElement>();
        foreach (var info in session.List())
        {
            bool isActive = info.Id == session.Active?.Id;
            // Phase 4H: «Загрузить» — текстом (меняет текущие настройки), повторяющиеся операции — значками с подсказками.
            var load = new List<UIElement>();
            if (!isActive)
            {
                load.Add(_ui.Button(Loc.T("Profiles.Load"), () => SwitchProfile(info.Id)));
            }

            var actions = _ui.Toolbar(load,
                [
                    _ui.Action(ActionIcons.Duplicate, Loc.T("Profiles.Duplicate"), () => { session.Duplicate(info.Id, n => Loc.F("Profiles.CopyName", n)); BuildSection(); }),
                    _ui.Action(ActionIcons.Rename, Loc.T("Profiles.Rename"), () => RenameProfile(info)),
                    _ui.Action(ActionIcons.Export, Loc.T("Profiles.Export"), () => ExportProfile(info.Id)),
                ],
                [_ui.Action(ActionIcons.Delete, Loc.T("Profiles.Delete"), () => DeleteProfile(info), !isActive, Loc.T("Profiles.DeleteActiveHint"), danger: true)]);
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Margin = new Thickness(0);
            rows.Add(_ui.Row(info.Name, isActive ? Loc.T("Profiles.IsActive") : null, actions));
        }

        rows.Add(_ui.Toolbar([_ui.Button("plus", Loc.T("Profiles.New"), NewProfile)], [_ui.Action(ActionIcons.Import, Loc.T("Profiles.Import"), ImportProfile)]));
        rows.Add(_ui.Note(Loc.T("Profiles.FileDialogNote")));
        _cards.Children.Add(_ui.Card(Loc.T("Profiles.All.Title"), null, rows.ToArray()));
    }

    private void BuildApplication()
    {
        var p = _prefs!.Current;
        // Phase 4I: 19 языков — список с самоназваниями (без перевода: каждый узнаёт свой язык), «Системный» — первым.
        var languages = new ComboBox { Width = 240, Style = UiTheme.Resources.StyleOf("Ko.ComboBox"), Tag = "language" };
        languages.Items.Add(new ComboBoxItem { Content = Loc.T("App.Language.System"), Tag = UiLanguage.System });
        foreach (var l in Loc.Supported)
        {
            languages.Items.Add(new ComboBoxItem { Content = l.NativeName, Tag = l.Language, Language = System.Windows.Markup.XmlLanguage.GetLanguage(l.Code) });
        }

        languages.SelectedItem = languages.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (UiLanguage)i.Tag == p.Language) ?? languages.Items[0];
        languages.SelectionChanged += (_, _) =>
        {
            if (languages.SelectedItem is ComboBoxItem { Tag: UiLanguage l } && l != _prefs!.Current.Language)
            {
                SetPrefs(x => x with { Language = l });
            }
        };
        _cards.Children.Add(_ui.Card(Loc.T("App.Language.Title"), Loc.T("App.Language.Hint"),
            _ui.Row(Loc.T("App.Language"), null, languages)));
        _cards.Children.Add(_ui.Card(Loc.T("App.Tray.Title"), Loc.T("App.Tray.Hint"),
            _ui.Row(Loc.T("App.Tray.Close"), Loc.T("App.Tray.CloseHint"), _ui.Segmented(p.CloseToTray ? 0 : 1, [Loc.T("App.Tray.Close.ToTray"), Loc.T("App.Tray.Close.Exit")],
                i => SetPrefs(x => x with { CloseToTray = i == 0 }))),
            _ui.Note(Loc.T("App.Tray.Note"))));
        if (_actions.Autostart is { } autostart)
        {
            _cards.Children.Add(AutostartCard(autostart));
        }

        var parts = p.HideParts;
        _cards.Children.Add(_ui.Card(Loc.T("Hotkeys.Hide.Title"), Loc.T("Hotkeys.Hide.Hint"),
            _ui.Row(Loc.T("Hotkeys.Hide.Keycaps"), null, _ui.Toggle(parts.HasFlag(DisplayParts.Keycaps), v => SetParts(DisplayParts.Keycaps, v))),
            _ui.Row(Loc.T("Hotkeys.Hide.Indicator"), null, _ui.Toggle(parts.HasFlag(DisplayParts.Indicator), v => SetParts(DisplayParts.Indicator, v))),
            _ui.Row(Loc.T("Hotkeys.Hide.Mouse"), null, _ui.Toggle(parts.HasFlag(DisplayParts.Mouse), v => SetParts(DisplayParts.Mouse, v)))));
        // Папка снимков — настройка компьютера: холст рисования (плагин «Рисование», overlay.draw) сохраняет снимки сюда.
        var folder = p.ScreenshotFolderOrDefault;
        var choose = _ui.Button(Loc.T("Draw.Shot.Choose"), ChooseShotFolder);
        choose.Margin = new Thickness(0, 0, 8, 4);
        var folderBar = _ui.Toolbar([choose, _ui.Action(ActionIcons.OpenFolder, Loc.T("Styles.OpenFolder"), () => OpenFolder(folder))]);
        _cards.Children.Add(_ui.Card(Loc.T("Draw.Shot.Title"), Loc.T("App.Shot.Hint"), _ui.Row(Loc.T("Draw.Shot.Folder"), folder, folderBar)));
        AddDiagnosticsCards(); // бывшая страница «Справка»: проверки, диагностика, файлы настроек, полный сброс
    }

    // ── автозапуск ──

    private CheckBox? _autostartToggle;
    private TextBlock? _autostartNote;
    private string? _autostartError;
    private bool _autostartSync;

    /// <summary>
    /// «Запускать вместе с Windows»: переключатель показывает настоящее состояние записи автозапуска (читается из реестра при
    /// каждом показе страницы и активации окна), а не сохранённую настройку; пояснение — если запись другой копии, отключена в
    /// Windows или изменить её не удалось.
    /// </summary>
    private Border AutostartCard(Autostart autostart)
    {
        _autostartToggle = _ui.Toggle(false, on =>
        {
            if (_autostartSync)
            {
                return;
            }

            _autostartError = autostart.TrySet(on, out var error) ? null : error;
            RefreshAutostart();
        });
        _autostartToggle.Tag = "autostart";
        System.Windows.Automation.AutomationProperties.SetName(_autostartToggle, Loc.T("App.Autostart"));
        _autostartNote = _ui.Note("");
        _autostartError = null;
        RefreshAutostart();
        return _ui.Card(Loc.T("App.Autostart.Title"), null,
            _ui.Row(Loc.T("App.Autostart"), Loc.T("App.Autostart.Hint"), _autostartToggle),
            _autostartNote);
    }

    /// <summary>Перечитать состояние автозапуска (после переключения, при активации окна — его могли изменить в Windows).</summary>
    public void RefreshAutostart()
    {
        if (_actions.Autostart is not { } autostart || _autostartToggle is null || _autostartNote is null)
        {
            return;
        }

        var status = autostart.Status();
        _autostartSync = true;
        try
        {
            _autostartToggle.IsChecked = status.State == AutostartState.On;
        }
        finally
        {
            _autostartSync = false;
        }

        string? note = _autostartError is { } error ? Loc.F("App.Autostart.Failed", error) : status.State switch
        {
            AutostartState.OtherCopy => Loc.F("App.Autostart.OtherCopy", status.RegisteredPath ?? ""),
            AutostartState.Missing => Loc.F("App.Autostart.Missing", status.RegisteredPath ?? ""),
            AutostartState.DisabledInWindows => Loc.T("App.Autostart.Disabled"),
            _ => null,
        };
        _autostartNote.Text = note ?? "";
        _autostartNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Состояние переключателя автозапуска на странице (тесты и самопроверка); null — страница не показана.</summary>
    public bool? AutostartShown => _autostartToggle?.IsChecked;

    /// <summary>Нажать переключатель автозапуска (тесты и самопроверка).</summary>
    public void ClickAutostart() => _autostartToggle!.IsChecked = !(_autostartToggle.IsChecked ?? false);

    // ── профили ──

    private void NewProfile()
    {
        var name = KoDialog.Prompt(this, Loc.T("Profiles.New.Title"), Loc.T("Profiles.NameLabel"), Loc.T("Profiles.NewName"), ValidateName);
        if (name is not null)
        {
            _profiles!.CreateDefault(name);
            BuildSection();
        }
    }

    private void RenameProfile(ProfileInfo info)
    {
        var name = KoDialog.Prompt(this, Loc.T("Profiles.Rename.Title"), Loc.T("Profiles.NameLabel"), info.Name, ValidateName);
        if (name is not null)
        {
            _profiles!.Rename(info.Id, name);
            BuildSection();
        }
    }

    private void DeleteProfile(ProfileInfo info)
    {
        var (choice, _) = KoDialog.Ask(this, Loc.T("Profiles.Delete.Title"), Loc.F("Profiles.Delete.Message", info.Name),
            [new("delete", Loc.T("Profiles.Delete"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
        if (choice == "delete")
        {
            _profiles!.Delete(info.Id);
            BuildSection();
        }
    }

    private void SwitchProfile(string id)
    {
        CommitPendingInput();
        var (ok, errors) = _profiles!.SwitchTo(id);
        if (!ok && errors.Count > 0)
        {
            KoDialog.Ask(this, Loc.T("Profiles.LoadFailed.Title"), Loc.T("Profiles.LoadFailed.Message") + "\n\n" + string.Join("\n", errors.Take(8)), [new("ok", Loc.T("Dialog.Ok"), true)]);
        }

        BuildSection();
    }

    /// <summary>Части окна, на которые ссылается справка (номера на снимке «Окно настроек»).</summary>
    public IReadOnlyList<(int Number, FrameworkElement Element)> Landmarks()
    {
        var list = new List<(int, FrameworkElement)>();
        if (NavItems.FirstOrDefault() is { } nav)
        {
            list.Add((1, nav));
        }

        if (_previewCard is not null)
        {
            list.Add((2, _previewCard));
        }

        list.Add((3, _cards));
        list.Add((4, _title));
        if (_resetButton is not null)
        {
            list.Add((5, _resetButton));
        }

        if (_helpButton is not null)
        {
            list.Add((6, _helpButton));
        }

        if (_trayButton is not null)
        {
            list.Add((7, _trayButton));
        }

        return list;
    }

    private void ImportProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Loc.T("Profiles.FileFilter") + "|*.json", Title = Loc.T("Profiles.Import") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var r = _profiles!.Import(dialog.FileName);
        if (r.Ok)
        {
            var (choice, _) = KoDialog.Ask(this, Loc.T("Profiles.Imported.Title"), Loc.F("Profiles.Imported.Message", r.Profile!.Name),
                [new("load", Loc.T("Profiles.Load"), true), new("later", Loc.T("Profiles.Imported.Later"))]);
            if (choice == "load")
            {
                SwitchProfile(r.Profile.Id);
                return;
            }
        }
        else
        {
            KoDialog.Ask(this, Loc.T("Profiles.ImportFailed.Title"), Loc.T("Profiles.ImportFailed.Message") + "\n\n" + string.Join("\n", r.Errors.Take(8)), [new("ok", Loc.T("Dialog.Ok"), true)]);
        }

        BuildSection();
    }

    /// <summary>Экспорт текущего профиля: актуальное состояние всех настроек (ожидающая запись выполняется до экспорта).</summary>
    private void ExportCurrentProfile()
    {
        CommitPendingInput();
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("Profiles.FileFilter") + "|*.json", FileName = _profiles!.ActiveName + ".backpack-profile.json" };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                _profiles.ExportCurrent(dialog.FileName);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                KoDialog.Ask(this, Loc.T("Profiles.ExportCurrent"), Loc.F("Profiles.Error.ExportFailed", ex.GetType().Name), [new("ok", Loc.T("Dialog.Ok"), true)]);
            }
        }
    }

    private void ExportProfile(string id)
    {
        if (id == _profiles!.Active?.Id)
        {
            ExportCurrentProfile();
            return;
        }

        var r = _profiles.Store.Load(id);
        if (!r.Ok)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("Profiles.FileFilter") + "|*.json", FileName = r.Profile!.Name + ".backpack-profile.json" };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                _profiles.Store.Export(r.Profile, dialog.FileName);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                KoDialog.Ask(this, Loc.T("Profiles.Export"), Loc.F("Profiles.Error.ExportFailed", ex.GetType().Name), [new("ok", Loc.T("Dialog.Ok"), true)]);
            }
        }
    }

    private static string? ValidateName(string name) => ProfileSerializer.IsValidName(name) ? null : Loc.T("Profiles.Error.Name");

    // ── превью ───────────────────────────────────────────────────────────────

    private LivePreview? _livePreview;

    private void BuildPreview()
    {
        var pluginPreview = PluginPreviewOf();
        _noPreview = Section is SettingsSection.Profiles or SettingsSection.Application or SettingsSection.Home or SettingsSection.Hotkeys
            or SettingsSection.Help or SettingsSection.About or SettingsSection.Interface
            || (Section == SettingsSection.Plugins && pluginPreview is null);
        // Пропорции сцены раздела: высота превью в узком окне и наименьшая высота логического блока в широком.
        _cards.PreviewAspect = Section switch
        {
            SettingsSection.Appearance or SettingsSection.Keys => 0.5,
            SettingsSection.Layout => 0.45,
            SettingsSection.Position => 0.55,
            SettingsSection.Plugins when pluginPreview is { Preview.Scene: "drawing" } => DrawingPreviewScreen().Height / DrawingPreviewScreen().Width,
            SettingsSection.Mouse => 0.6,
            _ => LivePreview.H / LivePreview.W,
        };
        // Вводные карточки во всю ширину: каталоги стилей сами состоят из миниатюр; превью стоит рядом с параметрами.
        _cards.PreviewStart = Section switch
        {
            SettingsSection.Appearance => 1,
            SettingsSection.Mouse => IndexOfCatalog() + 1,
            SettingsSection.Layout => IndexOfCatalog() + 1,
            SettingsSection.Plugins => 1, // карточка сведений о плагине — во всю ширину
            _ => 0,
        };
        _mousePreview = null;
        _indicatorPreview = null;
        _positionPreview = null;
        var s = _store.Current;
        switch (Section)
        {
            case SettingsSection.Appearance:
            case SettingsSection.Keys:
                ApplyPreviewPresenter(s);
                // Один и тот же элемент группы используется превью двух разделов: перед вставкой в новый контейнер
                // его нужно отсоединить от прежнего, иначе WPF бросает InvalidOperationException (падение Phase 2C).
                DetachFromParent(_preview.Root);
                _previewHost.Content = Stage.Wrap(new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(16), Child = _preview.Root });
                StartDemo(Section == SettingsSection.Keys ? DemoScenario.Typing : DemoScenario.Chords);
                break;
            case SettingsSection.Mouse:
                _mousePreview = new MouseCursorVisual();
                var pointer = PreviewPointer();
                var stage = new Canvas();
                stage.Children.Add(_mousePreview);
                stage.Children.Add(pointer);
                stage.Loaded += (_, _) => PlaceMousePreview(stage, pointer);
                stage.SizeChanged += (_, _) => PlaceMousePreview(stage, pointer);
                _previewHost.Content = Stage.Wrap(stage);
                _mouseDemoStep = 0;
                StepMouseDemo();
                _mouseDemo.Start();
                break;
            case SettingsSection.Layout:
                _indicatorPreview = new LayoutIndicatorVisual { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                _indicatorPreview.Set(new LayoutLabel("RU", null), IndicatorLook.Resolve(s.Appearance, LayoutIndicatorSettings.Default with { CustomStyle = true }));
                _previewHost.Content = Stage.Wrap(new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(16), Child = _indicatorPreview });
                break;
            case SettingsSection.Position:
                _positionPreview = new Canvas { Width = 320, Height = 160 };
                DrawPosition(s);
                _previewHost.Content = Stage.Wrap(new Viewbox { Margin = new Thickness(16), Child = _positionPreview }, grid: false);
                break;
            case SettingsSection.Privacy:
                _livePreview = LivePreview.Privacy(() => _store.Current, AnimateDemos);
                _previewHost.Content = Stage.Wrap(_livePreview);
                break;
            case SettingsSection.Plugins when pluginPreview is { } scene:
                // Демонстрация плагина рисуется хостом по манифесту (код плагина не запускается).
                _previewHost.Content = PluginScene(scene.Preview);
                break;
            default:
                _previewHost.Content = null;
                break;
        }

        PlacePreview(_body?.RenderSize ?? default);
    }

    /// <summary>
    /// Выбор компоновки по ширине области страницы: широкая — превью в колонке справа на всю высоту; иначе — превью в странице над
    /// карточками. Превью переносится между контейнерами как есть (его сцена и анимация продолжаются).
    /// </summary>
    private void PlacePreview(Size body)
    {
        if (_previewCard is null || _previewColumn is null || ScrollArea is null)
        {
            return;
        }

        bool wide = body.Width >= 32 + MinCardsWidth + PreviewGap + PreviewColumnWidth + 24;
        _previewWide = !_noPreview && wide;
        if (_noPreview)
        {
            _previewColumn.Child = null;
            _previewColumn.Visibility = Visibility.Collapsed;
            Grid.SetRow(ScrollArea, 0);
            Grid.SetRowSpan(ScrollArea, 2);
            Grid.SetColumnSpan(ScrollArea, 2);
            _cards.Margin = new Thickness(32, 4, 24, 40);
            return;
        }

        if (!ReferenceEquals(_previewCard.Parent, _previewColumn))
        {
            DetachFromParent(_previewCard);
            _previewColumn.Child = _previewCard;
        }

        _previewColumn.Visibility = Visibility.Visible;
        if (wide)
        {
            Grid.SetRow(_previewColumn, 0);
            Grid.SetRowSpan(_previewColumn, 2);
            Grid.SetColumn(_previewColumn, 1);
            _previewColumn.Width = PreviewColumnWidth;
            _previewColumn.Height = double.NaN;
            _previewColumn.Margin = new Thickness(0, 0, 24, 24);
            Grid.SetRow(ScrollArea, 0);
            Grid.SetRowSpan(ScrollArea, 2);
            Grid.SetColumnSpan(ScrollArea, 1);
            _cards.Margin = new Thickness(32, 0, PreviewGap, 40);
        }
        else
        {
            double w = Math.Max(0, body.Width - 32 - 24);
            Grid.SetRow(_previewColumn, 0);
            Grid.SetRowSpan(_previewColumn, 1);
            Grid.SetColumn(_previewColumn, 0);
            _previewColumn.Width = double.NaN;
            _previewColumn.Height = Math.Max(120, Math.Min(w * _cards.PreviewAspect, body.Height * 0.4));
            _previewColumn.Margin = new Thickness(32, 0, 24, 12);
            Grid.SetColumnSpan(_previewColumn, 2);
            Grid.SetRow(ScrollArea, 1);
            Grid.SetRowSpan(ScrollArea, 1);
            Grid.SetColumnSpan(ScrollArea, 2);
            _cards.Margin = new Thickness(32, 4, 24, 40);
        }
    }

    private void UpdateScrollFade()
    {
        if (_scroll is not null)
        {
            EdgeFade(_scroll, 18, 32);
        }
    }

    /// <summary>
    /// Мягкие края прокрутки: маска прозрачности на <paramref name="top"/> DIP сверху, если прокручено, и на
    /// <paramref name="bottom"/> DIP снизу, если ниже ещё есть содержимое. Маска только рисуется — щелчки проходят как обычно.
    /// </summary>
    internal static void EdgeFade(ScrollViewer sv, double top, double bottom)
    {
        double h = sv.ActualHeight;
        if (h <= 0)
        {
            return;
        }

        bool fadeTop = sv.VerticalOffset > 0.5;
        bool fadeBottom = sv.ScrollableHeight - sv.VerticalOffset > 0.5;
        if (!fadeTop && !fadeBottom)
        {
            sv.OpacityMask = null;
            return;
        }

        var black = System.Windows.Media.Colors.Black;
        var clear = System.Windows.Media.Colors.Transparent;
        var mask = new System.Windows.Media.LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        mask.GradientStops.Add(new System.Windows.Media.GradientStop(fadeTop ? clear : black, 0));
        mask.GradientStops.Add(new System.Windows.Media.GradientStop(black, fadeTop ? Math.Min(0.45, top / h) : 0));
        mask.GradientStops.Add(new System.Windows.Media.GradientStop(black, fadeBottom ? Math.Max(0.55, 1 - (bottom / h)) : 1));
        mask.GradientStops.Add(new System.Windows.Media.GradientStop(fadeBottom ? clear : black, 1));
        mask.Freeze();
        sv.OpacityMask = mask;
    }

    /// <summary>Позиция карточки каталога стилей на странице (её отмечает <see cref="CatalogTag"/>).</summary>
    private int IndexOfCatalog() => Math.Max(0, _cards.Children.OfType<FrameworkElement>().ToList().FindIndex(c => Equals(c.Tag, CatalogTag)));

    internal const string CatalogTag = "style-catalog";

    /// <summary>Анимации демонстраций разрешены: настройка «Главной», «меньше движения» и анимации Windows.</summary>
    private bool AnimateDemos => (_prefs?.Current.HomeAnimation ?? true) && !(_prefs?.Current.ReducedMotion ?? false) && SystemParameters.ClientAreaAnimation;

    /// <summary>Стрелка указателя для превью (собственная геометрия).</summary>
    public static Path PreviewPointer() => new()
    {
        Data = Geometry.Parse("M0,0 L0,17 L4.5,13 L7.5,20 L10,19 L7,12.2 L12.5,12.2 Z"),
        Fill = Brushes.White,
        Stroke = Brushes.Black,
        StrokeThickness = 1,
    };

    internal static void DetachFromParent(FrameworkElement e)
    {
        switch (e.Parent)
        {
            case Decorator d:
                d.Child = null;
                break;
            case Panel p:
                p.Children.Remove(e);
                break;
            case ContentControl c:
                c.Content = null;
                break;
        }
    }

    private void ApplyPreviewPresenter(LabSettings s) =>
        _preview.Apply(s.Appearance, new RowOptions(s.Keys.Trail, s.Keys.MouseLabels, HorizontalAlignment.Center));

    private void StartDemo(DemoScenario scenario)
    {
        _demo?.Dispose();
        _demo = new DemoPlayer(_preview, _clock, scenario: scenario, filter: items => RowFilter.Visible(items, _store.Current.Keys.Filter, _store.Current.Keys.MouseInRow, _store.Current.Keys.Chords));
    }

    private void StopPreviews()
    {
        _demo?.Dispose();
        _demo = null;
        _homeDemo?.Dispose(); // таймер и анимации демонстрации «Главной» — только пока она открыта
        _homeDemo = null;
        _livePreview?.Dispose();
        _livePreview = null;
        _mouseDemo.Stop();
        if (!_released)
        {
            _preview.Update([]);
        }
    }

    private void StepMouseDemo()
    {
        if (_mousePreview is null)
        {
            return;
        }

        (MouseButtons Pressed, int WheelV, int WheelH)[] steps =
        [
            (MouseButtons.None, 0, 0), (MouseButtons.Left, 0, 0), (MouseButtons.None, 0, 0), (MouseButtons.Right, 0, 0), (MouseButtons.None, 1, 0),
            (MouseButtons.None, -1, 0), (MouseButtons.Middle, 0, 0), (MouseButtons.X1, 0, 0), (MouseButtons.X2, 0, 0), (MouseButtons.None, 0, 1),
        ];
        var step = steps[_mouseDemoStep++ % steps.Length];
        var s = _store.Current;
        var shown = s.MouseCursor with { Enabled = true };
        _mousePreview.Apply(shown, s.Appearance.MouseLook, new Mouse.MouseButtonState(step.Pressed, MouseButtons.None, step.WheelV, step.WheelH));
        _mousePreview.Opacity = s.MouseCursor.Enabled ? 1 : 0.35;
    }

    private void PlaceMousePreview(Canvas stage, Path pointer)
    {
        if (_mousePreview is null)
        {
            return;
        }

        var o = _mousePreview.Origin;
        double cx = (stage.ActualWidth / 2) - 30, cy = (stage.ActualHeight / 2) - 30;
        Canvas.SetLeft(_mousePreview, cx - o.X);
        Canvas.SetTop(_mousePreview, cy - o.Y);
        Canvas.SetLeft(pointer, cx);
        Canvas.SetTop(pointer, cy);
    }

    private void DrawPosition(LabSettings s)
    {
        var c = _positionPreview!;
        c.Children.Clear();
        var wa = SystemParameters.WorkArea;
        double scale = Math.Min(c.Width / wa.Width, c.Height / wa.Height);
        var screen = new Rectangle { Width = wa.Width * scale, Height = wa.Height * scale, RadiusX = 6, RadiusY = 6 };
        screen.SetResourceReference(Shape.FillProperty, "Ko.SegmentBackBrush");
        screen.SetResourceReference(Shape.StrokeProperty, "Ko.ControlBorderBrush");
        c.Children.Add(screen);
        var m = KeycapMetrics.From(s.Appearance);
        double sampleWidth = (3 * m.ModifierMinWidth) + (2 * m.KeyGap) + (s.Appearance.BackgroundEnabled ? 2 * m.GroupPadding : 0);
        var g = OverlayPlacement.GroupRect(wa, s.Placement, s.Appearance, sampleWidth);
        var group = new Rectangle { Width = Math.Max(4, g.Width * scale), Height = Math.Max(3, g.Height * scale), RadiusX = 3, RadiusY = 3 };
        group.SetResourceReference(Shape.FillProperty, "Ko.AccentBrush");
        Canvas.SetLeft(group, (g.Left - wa.Left) * scale);
        Canvas.SetTop(group, (g.Top - wa.Top) * scale);
        c.Children.Add(group);
        var label = new TextBlock { Text = Loc.T("Position.PreviewLabel"), FontSize = 9 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Ko.SecondaryTextBrush");
        Canvas.SetLeft(label, 6);
        Canvas.SetTop(label, 4);
        c.Children.Add(label);
    }

    // ── изменения ────────────────────────────────────────────────────────────

    private void OnStoreChanged(LabSettings s)
    {
        if (_released)
        {
            return;
        }

        if (!_selfChange)
        {
            // Изменение пришло не из этого окна (меню трея, загрузка профиля): элементы раздела показывают новые значения.
            BuildSection();
            return;
        }

        ApplyEnablers(s);
        switch (Section)
        {
            case SettingsSection.Appearance:
            case SettingsSection.Keys:
                ApplyPreviewPresenter(s);
                _demo?.Refresh();
                break;
            case SettingsSection.Mouse:
                _mouseDemoStep = Math.Max(0, _mouseDemoStep - 1);
                StepMouseDemo();
                break;
            case SettingsSection.Layout:
                _indicatorPreview?.Set(new LayoutLabel("RU", null), IndicatorLook.Resolve(s.Appearance, LayoutIndicatorSettings.Default with { CustomStyle = true }));
                break;
            case SettingsSection.Position:
                DrawPosition(s);
                break;
            case SettingsSection.Privacy:
                _livePreview?.Refresh();
                break;
            case SettingsSection.Plugins:
                RefreshPluginValues();
                RefreshPluginScene();
                break;
        }
    }

    private void ApplyEnablers(LabSettings s)
    {
        foreach (var (element, enabled) in _enablers)
        {
            element.IsEnabled = enabled(s);
        }

        foreach (var (element, shown) in _shownWhen)
        {
            element.Visibility = shown(s) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ResetSection()
    {
        var d = Styles.BrandStyles.Starter; // Phase 4I: сброс возвращает фирменные пресеты Backpack Neon Emerald
        U(x => Section switch
        {
            SettingsSection.Appearance => x with
            {
                Appearance = d.Appearance with { Mouse = x.Appearance.Mouse, Indicator = x.Appearance.Indicator },
                Fonts = x.FontsOrDefault with { Keycap = d.FontsOrDefault.Keycap },
                Labels = SecondaryLabelSettings.Default,
            },
            SettingsSection.Mouse => x with { MouseCursor = d.MouseCursor, Appearance = x.Appearance with { Mouse = d.Appearance.MouseLook } },
            SettingsSection.Keys => x with { Keys = d.Keys },
            SettingsSection.Layout => x with { Appearance = x.Appearance with { Indicator = IndicatorVisual.Default } },
            SettingsSection.Position => x with { Placement = d.Placement },
            _ => x,
        });
        var dp = AppPreferences.Default;
        if (Section == SettingsSection.Hotkeys && _prefs is not null)
        {
            _prefs.Update(p => p with { Bindings = null }); // все команды — сочетания по умолчанию
        }

        if (Section == SettingsSection.Application && _prefs is not null)
        {
            _prefs.Update(p => p with { HideParts = dp.HideParts, ScreenshotFolder = null });
            SetPrefs(p => p with { Language = AppPreferences.Default.Language, CloseToTray = AppPreferences.Default.CloseToTray });
        }

        if (Section == SettingsSection.Interface && _prefs is not null)
        {
            _prefs.Update(p => p with { Material = null, ColorTheme = UiPalettes.DefaultId, HomeAnimation = dp.HomeAnimation, ReducedMotion = dp.ReducedMotion });
            SetPrefs(p => p with { Theme = AppPreferences.Default.Theme });
            U(x => x with { Fonts = x.FontsOrDefault with { Ui = d.FontsOrDefault.Ui } });
        }

        if (Section == SettingsSection.Privacy && _prefs is not null)
        {
            _prefs.Update(p => p with { Privacy = null });
        }

        BuildSection();
    }

    private void ResetAll()
    {
        var (choice, _) = KoDialog.Ask(this, Loc.T("HelpSection.ResetAll"), Loc.T("HelpSection.ResetAllConfirm"),
            [new("reset", Loc.T("HelpSection.ResetAllButton"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
        if (choice == "reset")
        {
            U(_ => Styles.BrandStyles.Starter);
            if (_prefs is not null)
            {
                // «Все разделы» — и оформление окон: Backpack Neon Emerald, светлая/тёмная как у системы.
                _prefs.Update(p => p with { Material = null, ColorTheme = UiPalettes.DefaultId });
                SetPrefs(p => p with { Theme = AppPreferences.Default.Theme });
            }

            BuildSection();
        }
    }

    private void SetPrefs(Func<AppPreferences, AppPreferences> f)
    {
        _prefs!.Update(f);
        var p = _prefs.Current;
        UiTheme.Set(p.Theme);
        Loc.Set(p.Language); // перестраивает окна, если язык изменился
    }

    private void U(Func<LabSettings, LabSettings> f)
    {
        _selfChange = true;
        try
        {
            _store.Update(f);
        }
        finally
        {
            _selfChange = false;
        }
    }

    private void Ap(Func<AppearanceSettings, AppearanceSettings> f) => U(x => x with { Appearance = f(x.Appearance) });

    private void Ks(Func<KeysSettings, KeysSettings> f) => U(x => x with { Keys = f(x.Keys) });

    private void Pl(Func<PlacementSettings, PlacementSettings> f) => U(x => x with { Placement = f(x.Placement) });

    private void Li(Func<LayoutIndicatorSettings, LayoutIndicatorSettings> f) => U(x => x with { LayoutIndicator = f(x.LayoutIndicator) });

    private void Mc(Func<MouseCursorSettings, MouseCursorSettings> f) => U(x => x with { MouseCursor = f(x.MouseCursor) });

    private void Mv(Func<MouseVisual, MouseVisual> f) => U(x => x with { Appearance = x.Appearance with { Mouse = f(x.Appearance.MouseLook) } });

    private void Iv(Func<IndicatorVisual, IndicatorVisual> f) => U(x => x with { Appearance = x.Appearance with { Indicator = f(x.Appearance.IndicatorLook) } });
}
