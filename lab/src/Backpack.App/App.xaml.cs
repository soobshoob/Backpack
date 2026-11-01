using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Backpack.Core.Diagnostics;
using Backpack.Core.Input;
using Backpack.Visual.Brand;
using Backpack.Visual.Commands;
using Backpack.Visual.Plugins;
using Backpack.Visual.Help;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Layout;
using Backpack.Visual.Localization;
using Backpack.Visual.Mouse;
using Backpack.Visual.Presentation;
using Backpack.Visual.Profiles;
using Backpack.Visual.Rendering;
using Backpack.Visual.Settings;
using Backpack.Visual.Styles;
using Backpack.Visual.Tray;
using Backpack.Visual.Updates;
using Backpack.Visual.Windows;

namespace Backpack.App;

/// <summary>
/// Жизненный цикл Backpack. Обычный запуск открывает главное окно (раздел «Главная»; с --minimized — только значок в трее);
/// лабораторный (--lab) — как прежде окно диагностики и инструменты проверки. Завершение только явное
/// (<see cref="ExitApplication"/>: «Выйти» в меню трея или окне диагностики, × главного окна при выборе «Выйти», --auto-exit-ms).
/// «Свернуть в трей» и × главного окна (по умолчанию) прячут окна: оверлей, захват, горячие клавиши продолжают работать.
/// </summary>
public partial class App : Application
{
    private LabController? _controller;
    private LabOptions? _options;
    private OverlayWindow? _overlay;
    private readonly List<OverlayWindow> _overlays = [];
    private readonly CommandRegistry _commands = new();
    private PluginToolEngine? _tools;
    private PluginDrawHost? _drawHost;
    private bool _hadSettings;
    private PluginManager? _plugins;
    private DispatcherTimer? _cursorFollow;
    private IReadOnlyList<MonitorDesc> _monitors = [];
    private DiagnosticsWindow? _diagnostics;
    private SettingsStore? _store;
    private AppPreferencesStore? _prefs;
    private ProfileSession? _profiles;
    private LayoutIndicatorController? _indicator;
    private MouseCursorController? _mouse;
    private SettingsWindow? _settingsWindow;
    private TrayIcon? _tray;
    private StyleStore? _styles;
    private HotkeyManager? _hotkeys;
    private PresentationController? _presentation;
    private AppInfo? _appInfo;
    private UpdateChecker? _updates;
    private DispatcherTimer? _updateTimer;
    private DataLocation? _data;
    private SingleInstance? _instance;
    private Autostart? _autostart;

    /// <summary>Раздел реестра автозапуска для --selftest-ui (не настоящий автозапуск; удаляется при выходе).</summary>
    internal const string SelfTestAutostartKey = @"Software\Backpack\SelfTest\Autostart";
    private bool _displayVisible = true;
    private readonly List<Window> _hiddenToTray = [];
    private int _settingsWindowsOpened;
    private bool _exiting;

    /// <summary>Окна стенда спрятаны в трей.</summary>
    internal bool InTray { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (e.Args is ["--plugin-host", var pluginDir])
        {
            // Процесс одного плагина (ADR-0001): без окон, захвата и настроек Backpack.
            Shutdown(PluginHostMode.Run(pluginDir));
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        // Может прийти из любого потока: только снятие источников (PostMessage + Join), без таймеров UI.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => _controller?.EmergencyStopSources();

        try
        {
            _options = LabOptions.Parse(e.Args);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message + "\n\n" + LabOptions.Usage, "Backpack", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(2);
            return;
        }

        if (_options.RenderReadmeImages is { } readmeDir)
        {
            Shutdown(HelpImages.RunReadme(readmeDir));
            return;
        }

        if (_options.RenderHelpImages is { } imagesDir)
        {
            Shutdown(HelpImages.Run(imagesDir));
            return;
        }

        if (_options.RenderBrand is { } brandDir)
        {
            Shutdown(ReviewSheets.RenderBrand(brandDir));
            return;
        }

        if (_options.RenderSheet is { } sheetDir)
        {
            Shutdown(ReviewSheets.RenderComparison(sheetDir));
            return;
        }

        // Папка данных: --settings, portable рядом с программой или %APPDATA%\Backpack.
        _data = DataLocations.Resolve(_options.SettingsPath, AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        // Один экземпляр на папку данных: повторный запуск показывает окно уже работающего, повторный автозапуск — ничего.
        _instance = SingleInstance.TryAcquire(_data.Directory, showExisting: !_options.Autostart);
        if (_instance is null)
        {
            Shutdown(0);
            return;
        }

        // Автозапуск: самопроверка окон работает со своим разделом реестра (переключатель не меняет настоящий автозапуск).
        _autostart = _options.SelfTestUi
            ? new Autostart(Environment.ProcessPath ?? "Backpack.exe", SelfTestAutostartKey + @"\Run", SelfTestAutostartKey + @"\Approved")
            : new Autostart(Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Backpack.exe"));
        if (_options.SettingsPath is null && !_options.Lab)
        {
            _autostart.Repair(); // папку перенесли или новую версию распаковали в другое место — запись ведёт на эту копию
        }
        // Повреждённые, чужие или старые настройки не мешают запуску: Load не бросает исключений, форматы 1 и 2 мигрируют.
        _hadSettings = File.Exists(_options.SettingsPath ?? _data.SettingsPath); // обновление, а не чистая установка
        _store = SettingsStore.Load(_options.SettingsPath ?? _data.SettingsPath);
        // Общие предпочтения (язык, тема интерфейса, трей) — отдельный файл рядом с настройками, не часть профиля.
        _prefs = AppPreferencesStore.Load(AppPreferencesStore.PathNextTo(_store.FilePath));
        Loc.Set(_prefs.Current.Language);
        UiTheme.SetPalette(_prefs.Current.ColorTheme);
        UiTheme.SetMaterial(_prefs.Current.MaterialOrDefault);
        UiTheme.Set(_prefs.Current.Theme);
        var settings = _store.Current;
        var layoutProbe = new Win32LayoutProbe();
        SecondaryLabels.ActiveLayout = () => layoutProbe.Sample().Hkl; // раскладка окна переднего плана (целевого приложения)
        ApplyFonts(settings);

        _controller = new LabController(_options, Dispatcher, TimeSpan.FromMilliseconds(settings.Keys.LingerMs));
        if (_store.MigratedFrom is { } v)
        {
            _controller.StartNotes.Add(new LocText("Note.Migrated", v, SettingsSerializer.FormatVersion));
        }

        foreach (var problem in _store.LoadProblems)
        {
            _controller.StartNotes.Add(new LocText("Note.SettingsProblem", problem));
        }

        if (_prefs.Unreadable)
        {
            _controller.StartNotes.Add(new LocText("Note.PrefsUnreadable"));
        }

        if (_prefs.LoadedVersion is 1 or 2)
        {
            // app.json v1–v2 (ещё без темы окон): тема окон стала Backpack Emerald; как вернуть — в сообщении.
            _controller.StartNotes.Add(new LocText("Note.ThemeMigrated"));
        }

        if (_data.PortableUnavailable)
        {
            _controller.StartNotes.Add(new LocText("Note.PortableUnavailable"));
        }

        _styles = new StyleStore(StyleStore.DirectoryNextTo(_store.FilePath));
        _profiles = new ProfileSession(new ProfileStore(ProfileStore.DirectoryNextTo(_store.FilePath), _styles), _store, _prefs,
            () => SystemParameters.WorkArea, Loc.T("Profiles.DefaultName"), n => Loc.F("Profiles.WorkingCopyName", n));
        if (_profiles.LegacyBackupName is { } backup)
        {
            // Перенос со старой модели: несохранённые изменения стали профилем, прежняя сохранённая версия — отдельным профилем.
            _controller.StartNotes.Add(new LocText("Note.ProfilesAutosave", _profiles.ActiveName, backup));
        }
        _appInfo = AppInfo.FromAssembly(typeof(App).Assembly);
        _updates = new UpdateChecker(UpdateSource.Parse(_appInfo.UpdateRepository));

        if (_options.Lab)
        {
            _diagnostics = CreateDiagnostics();
            MainWindow = _diagnostics;
        }

        _monitors = DisplayMonitors.Enumerate();
        _overlay = new OverlayWindow(_controller, KeycapMonitors(settings).FirstOrDefault());
        _overlay.ApplySettings(settings);
        _overlays.Add(_overlay);
        _controller.AttachOverlay(_overlay);
        _controller.OverlaysProvider = () => _overlays;
        _controller.DisplaysChanged += OnDisplaysChanged;
        ApplyPrivacy(_prefs.Current);
        _prefs.Changed += OnPrefsChanged;

        _indicator = new LayoutIndicatorController(new Win32LayoutProbe(),
            m => _overlays.FirstOrDefault(o => o.Monitor.Id == m.Id) is { } o && _controller.OverlayIntendedVisible ? o.GroupLocalRect : null,
            IndicatorMonitors);
        // Phase 4H: показ решает плагин «Индикатор раскладки» (input.layout + overlay.indicator); наблюдение и ребазлайн — ядро.
        _indicator.UsePlugins(false, settings.Appearance);
        _indicator.LayoutChanged += (label, reason) => _plugins?.ForwardLayout(label.Code, label.Detail, reason == LayoutChangeReason.WindowSwitch ? "window" : "switch");
        _controller.ContextReset += _indicator.Rebaseline;

        _mouse = new MouseCursorController(() =>
        {
            var (pressed, unknown) = _controller.MouseButtonsState();
            return new MouseButtonState((MouseButtons)pressed, (MouseButtons)unknown);
        });
        _controller.VisualStatsProvider = BuildVisualStats;
        _controller.Wheel += OnWheel;
        _controller.EscapePressed += OnEscape;
        _controller.ContextReset += OnContextReset;
        _store.Changed += ApplySettings;

        // Phase 4H: механизм рисования — холст хоста для плагина «Рисование» (overlay.draw): параметры из запроса плагина,
        // изменения на панели уходят плагину (он запоминает их в своих настройках).
        _drawHost = new PluginDrawHost();
        _presentation = new PresentationController(() => _prefs!.Current.LensOrDefault, () => _drawHost.Settings,
            profile: () => _drawHost.Profile,
            saveProfile: _drawHost.SaveProfile,
            shotFolder: () => _prefs!.Current.ScreenshotFolderOrDefault);
        _presentation.StateChanged += OnPresentationState;
        _presentation.ScreenshotDone += OnScreenshotDone;
        _presentation.RadialCatalog = RadialCatalog;
        _drawHost.Attach(_presentation);
        _hotkeys = new HotkeyManager();
        _hotkeys.CommandPressed += OnCommandHotkey;
        // Все сочетания — команды реестра (app.json v4): прежние пять действий перенесены в команды при чтении файла.
        _hotkeys.Apply(HotkeySettings.None);

        // Инструменты, которые хост рисует по запросам плагинов (увеличение у курсора, линза, затемнение): Phase 4G.
        _tools = new PluginToolEngine(() => _monitors, OwnWindows, (hide, px) => _mouse?.SetZoomInteraction(hide, px)) { Animate = () => SystemParameters.ClientAreaAnimation, Drawing = _drawHost };
        _tools.Changed += SyncPluginToolHotkeys;
        RegisterBuiltInCommands();
        var pluginStore = new PluginStore(PluginStore.DirectoryNextTo(_store.FilePath))
        {
            // Настройки плагинов — в активном профиле (settings v8); прежний файл рядом с пакетами читается до первой записи.
            ProfileLoad = id => _store!.Current.PluginsOrEmpty.Get(id),
            ProfileSave = (id, values) => _store!.Update(x => x with { Plugins = x.PluginsOrEmpty.With(id, values) }),
        };
        _plugins = new PluginManager(pluginStore, _commands, Dispatcher, Environment.ProcessPath ?? "Backpack.exe", () => Loc.Code, _options.NoPlugins, _tools)
        {
            IsHeld = HotkeyHeld,
            Monitors = () => _monitors,
            Animate = () => SystemParameters.ClientAreaAnimation,
        };
        _plugins.Indicator = new IndicatorBridge(_indicator);
        _plugins.Changed += () => _indicator?.UsePlugins(_plugins.WantsLayout, _store!.Current.Appearance);
        _store.Changed += PluginProfileChanged;
        _plugins.Notice += text => _tray?.Notify(text);
        SyncBundledPlugins();
        _plugins.Changed += ApplyCommandHotkeys;
        _commands.Changed += ApplyCommandHotkeys;
        _controller.VisibleTransition += t =>
        {
            if (!Backpack.Core.Input.Slots.IsMouse(t.KeySlot))
            {
                _plugins?.ForwardVisibleKey(KeyInfo.Label(t.KeySlot), t.State == Backpack.Core.Model.KeyState.Down);
            }
        };

        _tray = new TrayIcon(_store, _profiles, TrayCommands());
        _instance.Listen(() => Dispatcher.BeginInvoke(() =>
        {
            if (!_exiting)
            {
                OpenMain();
                _settingsWindow?.Activate();
            }
        }));
        _controller.OverlayVisibilityChanged += OnOverlayVisibilityChanged;

        _overlay.Show(); // ShowActivated=False: фокус не уходит к оверлею
        _controller.Start();
        LayoutOverlays();
        _mouse.Apply(settings.MouseCursor, settings.Appearance.MouseLook);
        if (_diagnostics is not null)
        {
            _diagnostics.Show();
            _diagnostics.RefreshStats();
        }
        else if (!_options.Minimized)
        {
            OpenSettings(SettingsSection.Home);
        }

        StartUpdateTimer();
        ApplyCommandHotkeys();
        _plugins.StartEnabled();

        if (_options.SelfTestUi)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, RunUiSelfTest);
        }

        if (_options.SoakMinutes > 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, RunSoak);
        }

        if (_options.Demo)
        {
            _overlay.ShowExample(TimeSpan.FromSeconds(13));
            _indicator.ShowLabel(new LayoutLabel("EN", null));
        }

        if (_options.AutoExit is { } delay)
        {
            // Самопроверка окон без ввода: стили no-activate/click-through и то, что передний план не стал нашим окном.
            var check = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Min(1500, delay.TotalMilliseconds / 2)) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                SelfCheckWindows();
            };
            check.Start();

            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ExitApplication();
            };
            timer.Start();
        }
    }

    private TrayCommands TrayCommands() => new(
        ShowWindows: OpenMain,
        OpenSettings: OpenMain,
        OverlayVisible: () => _displayVisible,
        SetOverlayVisible: SetDisplayVisible,
        OpenHelp: () => HelpService.Show(HelpLibrary.IndexFile),
        Exit: ExitApplication,
        Registry: _commands);

    /// <summary>«Открыть Backpack»: вернуть спрятанные окна и показать главное окно (одна команда вместо двух прежних).</summary>
    private void OpenMain()
    {
        ShowWindows();
        OpenSettings(_settingsWindow?.Section ?? SettingsSection.Home);
    }

    private IEnumerable<nint> OwnWindows() =>
        _overlays.Select(o => o.Hwnd).Concat([_mouse?.WindowHandle ?? 0, _indicator?.WindowHandle ?? 0]).Where(h => h != 0);

    internal CommandRegistry Commands => _commands;

    internal PluginToolEngine? Tools => _tools;

    internal PluginManager? Plugins => _plugins;

    /// <summary>
    /// Встроенные команды — единый каталог для кнопок, меню трея, радиального меню и горячих клавиш (одна реализация).
    /// Сочетания по умолчанию — только у пяти прежних действий (показ, «Фокус», рисование, радиальное меню, снимок).
    /// </summary>
    private void RegisterBuiltInCommands()
    {
        const string App = "Commands.Category.App", Keys = "Commands.Category.Keys", Mouse = "Commands.Category.Mouse";
        var s = _store!;
        var p = _prefs!;

        void Add(string id, string title, string cat, CommandScope scope, CommandKind kind, Action run, Func<bool>? available = null, Func<bool>? check = null,
            CommandTool tool = CommandTool.None, string? icon = null, Hotkey? hotkey = null) =>
            _commands.Register(new CommandDef(id, title, cat, scope, kind, run, available, check, hotkey, tool, icon));

        static Hotkey Cas(char c) => new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, c);
        Add("app.open", "Commands.AppOpen", App, CommandScope.Global, CommandKind.Action, OpenMain, icon: "app-window");
        Add("display.toggle", "Commands.DisplayToggle", App, CommandScope.Global, CommandKind.Toggle, () => SetDisplayVisible(!_displayVisible), check: () => _displayVisible, icon: "eye-off", hotkey: Cas('O'));
        Add("keys.combosOnly", "Tray.CombosOnly", Keys, CommandScope.Global, CommandKind.Toggle,
            () => s.Update(x => x with { Keys = x.Keys with { Filter = x.Keys.Filter == KeyFilter.CombosOnly ? KeyFilter.All : KeyFilter.CombosOnly } }), check: () => s.Current.Keys.Filter == KeyFilter.CombosOnly, icon: "keyboard");
        Add("keys.mouseInRow", "Tray.MouseInRow", Keys, CommandScope.Global, CommandKind.Toggle,
            () => s.Update(x => x with { Keys = x.Keys with { MouseInRow = !x.Keys.MouseInRow } }), check: () => s.Current.Keys.MouseInRow, icon: "mouse-pointer-click");
        Add("mouse.icon", "Tray.MouseIcon", Mouse, CommandScope.Global, CommandKind.Toggle,
            () => s.Update(x => TrayMenu.SetMouseIcon(x, !TrayMenu.MouseIconOn(x))), check: () => TrayMenu.MouseIconOn(s.Current), icon: "mouse");
        Add("mouse.circle", "Tray.MouseCircle", Mouse, CommandScope.Global, CommandKind.Toggle,
            () => s.Update(x => TrayMenu.SetMouseCircle(x, !TrayMenu.MouseCircleOn(x))), check: () => TrayMenu.MouseCircleOn(s.Current), icon: "circle-dot");
        Add("mouse.highlight", "Commands.MouseHighlight", Mouse, CommandScope.Global, CommandKind.Toggle,
            () => s.Update(x => TrayMenu.SetMouseHighlight(x, !TrayMenu.MouseHighlightOn(x))), check: () => TrayMenu.MouseHighlightOn(s.Current), icon: "sun");

        Add("profile.next", "Commands.ProfileNext", App, CommandScope.Global, CommandKind.Choice, NextProfile, () => _profiles is not null, icon: "layers");
        foreach (var (id, section, title, icon) in new[]
        {
            ("settings.keys", SettingsSection.Keys, "Settings.Nav.Keys", "keyboard"), ("settings.mouse", SettingsSection.Mouse, "Settings.Nav.Mouse", "mouse"),

            ("settings.plugins", SettingsSection.Plugins, "Settings.Nav.Plugins", "puzzle"), ("settings.commands", SettingsSection.Hotkeys, "Settings.Nav.Hotkeys", "command"),
        })
        {
            Add(id, title, "Commands.Category.Settings", CommandScope.Global, CommandKind.OpenSettings, () =>
            {
                ShowWindows();
                OpenSettings(section);
            }, icon: icon);
        }

        Add("app.help", "Tray.Help", App, CommandScope.Local, CommandKind.Action, () => HelpService.Show(HelpLibrary.IndexFile), icon: "circle-question-mark");
        Add("app.exit", "Tray.Exit", App, CommandScope.Global, CommandKind.Action, ExitApplication, icon: "power");
    }

    /// <summary>
    /// Категории радиального меню из общего реестра команд (та же реализация, что у сочетаний и трея): «Фокус», «Показ»,
    /// «Мышь» и команды включённых плагинов. Доступность — по реестру (выключенный инструмент — недоступен).
    /// </summary>
    private IReadOnlyList<RadialNode> RadialCatalog()
    {
        RadialNode Cmd(string id)
        {
            var d = _commands.Get(id);
            return new RadialNode(id, d?.Title ?? id, d?.Icon ?? "command", () => _commands.Execute(id), IsAvailable: () => _commands.Get(id)?.Available == true,
                IsChecked: d?.IsChecked);
        }

        RadialNode Group(string id, string titleKey, string icon, params string[] ids) =>
            new(id, Loc.T(titleKey), icon, Children: ids.Where(x => _commands.Get(x) is not null).Select(Cmd).ToList());
        var list = new List<RadialNode>
        {
            Group("cat.show", "Radial.Show", "eye-off", "display.toggle", "keys.combosOnly", "mouse.icon", "mouse.highlight", "profile.next"),
        };
        var plugins = _commands.All.Where(d => d.Source is not null && d.Id.StartsWith("plugin.", StringComparison.Ordinal)).Select(d => Cmd(d.Id)).ToList();
        if (plugins.Count > 0)
        {
            list.Add(new RadialNode("cat.plugins", Loc.T("Commands.Category.Plugins"), "puzzle", Children: plugins));
        }

        return list;
    }


    /// <summary>Следующий профиль; ожидающие изменения текущего записываются до переключения (автосохранение).</summary>
    private void NextProfile()
    {
        if (_profiles is null)
        {
            return;
        }

        var list = _profiles.List();
        if (list.Count < 2)
        {
            return;
        }

        int i = list.ToList().FindIndex(x => x.Id == _profiles.Active?.Id);
        var next = list[(i + 1) % list.Count];
        _profiles.SwitchTo(next.Id);
    }

    /// <summary>Удерживается ли назначенное сочетание команды (опрос состояния клавиш; RegisterHotKey не сообщает об отпускании).</summary>
    private bool HotkeyHeld(string id)
    {
        if (_prefs is null || _commands.HotkeyOf(id, _prefs.Current.BindingsOrEmpty) is not { } h)
        {
            return false;
        }

        static bool Down(int vk) => (SelfCheck.KeyState(vk) & 0x8000) != 0;
        return Down(h.Vk)
            && (!h.Modifiers.HasFlag(HotkeyModifiers.Ctrl) || Down(0x11))
            && (!h.Modifiers.HasFlag(HotkeyModifiers.Alt) || Down(0x12))
            && (!h.Modifiers.HasFlag(HotkeyModifiers.Shift) || Down(0x10))
            && (!h.Modifiers.HasFlag(HotkeyModifiers.Win) || Down(0x5B) || Down(0x5C));
    }

    private void OnCommandHotkey(string id) => _commands.Execute(id);

    /// <summary>Сочетания всех команд реестра: применяются заново при каждом изменении.</summary>
    private void ApplyCommandHotkeys()
    {
        if (_hotkeys is null || _prefs is null || _exiting)
        {
            return;
        }

        _hotkeys.ApplyCommands(_commands.Bindings(_prefs.Current.BindingsOrEmpty));
    }

    private DiagnosticsWindow CreateDiagnostics()
    {
        var d = new DiagnosticsWindow(_controller!, new DiagnosticsActions(() => OpenSettings(SettingsSection.Home), () => HideToTray(), ExitApplication, LayoutLines));
        d.Closing += OnMainWindowClosing;
        d.Closed += (_, _) =>
        {
            if (ReferenceEquals(_diagnostics, d))
            {
                _diagnostics = null;
            }
        };
        return d;
    }

    /// <summary>Окно диагностики по запросу (раздел «Справка и диагностика»): в обычном режиме не открывается при запуске.</summary>
    private void OpenDiagnostics()
    {
        if (_exiting || _controller is null)
        {
            return;
        }

        _diagnostics ??= CreateDiagnostics();
        _hiddenToTray.Remove(_diagnostics);
        _diagnostics.Show();
        _diagnostics.Activate();
        _diagnostics.RefreshStats();
    }

    /// <summary>
    /// Показ и скрытие отображения (горячая клавиша, главное окно, трей). Скрываются части из «Что скрывает» (по умолчанию —
    /// кейкапы, индикатор раскладки и мышь у курсора). Захват и модель продолжают работать; при возврате отпущенные клавиши
    /// убираются, удерживаемые видны сразу. Скрытие закрывает линзу, рисование и меню — невидимого перехвата мыши не остаётся.
    /// </summary>
    internal void SetDisplayVisible(bool visible)
    {
        if (_controller is null || _prefs is null || _exiting)
        {
            return;
        }

        _displayVisible = visible;
        var parts = _prefs.Current.HideParts;
        if (visible)
        {
            _controller.DropReleasedEntries();
        }
        else
        {
            _presentation?.Execute(PresentationCommand.ExitAll);
            _tools?.CloseAllSystem("host");
        }

        _controller.SetOverlayVisible((visible || !parts.HasFlag(DisplayParts.Keycaps)) && _prefs.Current.ToolsOrDefault.Keycaps);
        if (_indicator is not null)
        {
            _indicator.Suppressed = !visible && parts.HasFlag(DisplayParts.Indicator);
        }

        if (_mouse is not null)
        {
            _mouse.Suppressed = !visible && parts.HasFlag(DisplayParts.Mouse);
        }

        _tray?.UpdateText();
    }

    private void OnEscape()
    {
        _presentation?.Escape();
        _tools?.Escape(); // инструменты плагинов: шаг назад или закрыть последний открытый
    }

    /// <summary>Сочетания режима рисования (снимок, по умолчанию F2) регистрируются только пока рисование включено.</summary>
    private void OnPresentationState(PresentationState s) => _hotkeys?.SetDrawingActive(s.Drawing);

    /// <summary>Если рисование уже закрыто («Завершить рисование» после снимка), итог — коротким уведомлением у значка.</summary>
    private void OnScreenshotDone(ScreenshotResult r)
    {
        if (_presentation?.Drawing is null && _tray is not null)
        {
            _tray.Notify(r.Ok
                ? r.FilePath is { } path ? Loc.F("Draw.ShotSaved", Path.GetFileName(path)) : Loc.T("Draw.ShotCopied")
                : Loc.F("Draw.ShotFailed", r.Error ?? "?"));
        }
    }

    private void OnWheel(Backpack.Core.Model.WheelPulse p) => _mouse?.Pulse(p.Axis == WheelAxis.Horizontal, p.Delta);

    /// <summary>Блокировка, смена рабочего стола, сон: инструменты презентации закрываются (источник линзы недоступен).</summary>
    private void OnContextReset()
    {
        _presentation?.Execute(PresentationCommand.ExitAll);
        _tools?.CloseAllSystem("system");
    }

    /// <summary>Периодическая проверка обновлений — только если включена в «О программе» и источник задан. Ничего не скачивает.</summary>
    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromHours(24) };
        _updateTimer.Tick += async (_, _) =>
        {
            if (_prefs?.Current.UpdatesOrDefault.AutoCheck == true && _updates?.Source is not null && _appInfo?.SemVer is { } v)
            {
                var r = await _updates.CheckAsync(v, _prefs.Current.UpdatesOrDefault.Channel);
                if (r.State == UpdateState.Available && _tray is not null)
                {
                    _tray.Notify(Loc.F("Update.Available", r.Release!.Version));
                }
            }
        };
        _updateTimer.Start();
    }

    /// <summary>Полный выход: окна, значок, таймеры, подписки и источники освобождаются в <see cref="OnExit"/>.</summary>
    public void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _tray?.CloseMenu();
        _presentation?.Execute(PresentationCommand.ExitAll);
        _tools?.CloseAllSystem("host");
        _plugins?.Dispose();
        _settingsWindow?.Close();
        HelpImageWindow.Current?.Close();
        HelpService.Shutdown(); // закрывает и окно примеров
        _diagnostics?.Close();
        _tray?.Dispose();
        _tray = null;
        Shutdown(0);
    }

    /// <summary>Спрятать окна стенда в трей; оверлей, индикатор, мышь у курсора и захват продолжают работать.</summary>
    internal void HideToTray(bool fromClose = false)
    {
        if (_prefs is null || _exiting)
        {
            return;
        }

        if (!_prefs.Current.TrayHintShown && _options?.SelfTestUi != true)
        {
            // Объяснение при первом использовании: где значок и как выйти полностью.
            var owner = _settingsWindow is { IsVisible: true } ? _settingsWindow : _diagnostics is { IsVisible: true } ? (Window)_diagnostics : null;
            var (choice, _) = KoDialog.Ask(owner, Loc.T("Tray.FirstHide.Title"), Loc.T("Tray.FirstHide.Message"),
                [new("ok", Loc.T("Tray.FirstHide.Ok"), true), new("exit", Loc.T("Tray.FirstHide.Exit"))]);
            _prefs.Update(p => p with { TrayHintShown = true });
            if (choice == "exit")
            {
                if (fromClose)
                {
                    Dispatcher.BeginInvoke(ExitApplication);
                }
                else
                {
                    ExitApplication();
                }

                return;
            }
        }

        foreach (var w in Windows.OfType<Window>().ToList())
        {
            if (w.IsVisible && w is DiagnosticsWindow or SettingsWindow or HelpWindow or HelpDemoWindow or HelpImageWindow)
            {
                _hiddenToTray.Add(w);
                w.Hide();
            }
        }

        InTray = true;
    }

    /// <summary>Вернуть спрятанные окна; если их нет — показать окно диагностики.</summary>
    internal void ShowWindows()
    {
        if (_exiting)
        {
            return;
        }

        var restore = _hiddenToTray.Where(w => w.IsLoaded || w == _diagnostics).ToList();
        _hiddenToTray.Clear();
        InTray = false;
        if (restore.Count == 0)
        {
            if (_diagnostics is not null)
            {
                restore.Add(_diagnostics);
            }
            else
            {
                OpenSettings(SettingsSection.Home);
                return;
            }
        }

        foreach (var w in restore)
        {
            w.Show();
            if (w.WindowState == WindowState.Minimized)
            {
                w.WindowState = WindowState.Normal;
            }
        }

        restore.Last().Activate();
        _diagnostics?.RefreshStats();
    }

    /// <summary>× главного окна: по умолчанию прячет окна в трей (главное окно сохраняется), иначе — полный выход.</summary>
    private void OnSettingsWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting || _options?.Lab == true)
        {
            return; // в стенде главное окно — диагностика; окно настроек закрывается само по себе
        }

        e.Cancel = true;
        if (_prefs?.Current.CloseToTray != false)
        {
            HideToTray(fromClose: true);
        }
        else
        {
            Dispatcher.BeginInvoke(ExitApplication);
        }
    }

    /// <summary>× окна диагностики: в стенде (--lab) это главное окно; в обычном режиме окно диагностики просто закрывается.</summary>
    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting || _options?.Lab != true)
        {
            return;
        }

        e.Cancel = true;
        if (_prefs?.Current.CloseToTray == true)
        {
            HideToTray(fromClose: true);
        }
        else
        {
            Dispatcher.BeginInvoke(ExitApplication);
        }
    }

    private void OnOverlayVisibilityChanged(bool visible)
    {
        foreach (var o in _overlays.Skip(1))
        {
            if (visible)
            {
                o.ShowOverlay();
            }
            else
            {
                o.Hide();
            }
        }

        _tray?.UpdateText();
    }

    // ── мониторы ──────────────────────────────────────────────────────────────

    /// <summary>Мониторы кейкапов по настройке профиля (недоступный сохранённый монитор → основной).</summary>
    private IReadOnlyList<MonitorDesc> KeycapMonitors(LabSettings s) =>
        MonitorResolver.Resolve(s.DisplaysOrDefault.Keycaps, _monitors, Screens.Cursor()).Monitors;

    private IReadOnlyList<MonitorDesc> IndicatorMonitors() =>
        _store is null ? [] : MonitorResolver.Resolve(_store.Current.DisplaysOrDefault.IndicatorTarget, _monitors, Screens.Cursor()).Monitors;

    /// <summary>Сохранённые мониторы, которых сейчас нет (для понятного сообщения на странице «Положение»).</summary>
    internal IReadOnlyList<MonitorRef> MissingMonitors()
    {
        if (_store is null)
        {
            return [];
        }

        var d = _store.Current.DisplaysOrDefault;
        var cur = Screens.Cursor();
        return MonitorResolver.Resolve(d.Keycaps, _monitors, cur).Missing.Concat(MonitorResolver.Resolve(d.IndicatorTarget, _monitors, cur).Missing).Distinct().ToList();
    }

    /// <summary>
    /// Одно окно оверлея на каждый выбранный монитор (одна модель отображения, один источник ввода). Первое окно не
    /// пересоздаётся: оно принимает системные уведомления. Лишние окна закрываются, недостающие создаются.
    /// </summary>
    private void LayoutOverlays()
    {
        if (_store is null || _controller is null || _overlay is null || _exiting)
        {
            return;
        }

        var settings = _store.Current;
        var targets = KeycapMonitors(settings);
        if (targets.Count == 0)
        {
            return;
        }

        if (_overlay.Monitor.Id != targets[0].Id || _overlay.Monitor != targets[0])
        {
            _overlay.PlaceOn(targets[0]);
        }

        for (int i = 1; i < targets.Count; i++)
        {
            if (i < _overlays.Count)
            {
                if (_overlays[i].Monitor != targets[i])
                {
                    _overlays[i].PlaceOn(targets[i]);
                }

                continue;
            }

            var w = new OverlayWindow(_controller, targets[i], systemNotifications: false);
            w.ApplySettings(settings);
            w.ShowPrivacyBadge = _overlay.ShowPrivacyBadge;
            _overlays.Add(w);
            if (_controller.OverlayIntendedVisible)
            {
                w.ShowOverlay();
            }
        }

        while (_overlays.Count > targets.Count)
        {
            var extra = _overlays[^1];
            _overlays.RemoveAt(_overlays.Count - 1);
            extra.Close();
        }

        _indicator?.Relayout();
        UpdateCursorFollow();
    }

    /// <summary>
    /// «Монитор под курсором» (динамически, не монитор активного окна): лёгкий таймер переносит окно, когда указатель
    /// переходит на другой монитор. Окно не пересоздаётся — удержания и анимации продолжаются, дублей нет.
    /// </summary>
    private void UpdateCursorFollow()
    {
        var d = _store?.Current.DisplaysOrDefault;
        bool need = d is not null && (d.Keycaps.Mode == MonitorMode.UnderCursor || d.IndicatorTarget.Mode == MonitorMode.UnderCursor);
        if (need && _cursorFollow is null)
        {
            _cursorFollow = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
            _cursorFollow.Tick += (_, _) => FollowCursor();
            _cursorFollow.Start();
        }
        else if (!need && _cursorFollow is not null)
        {
            _cursorFollow.Stop();
            _cursorFollow = null;
        }
    }

    private void FollowCursor()
    {
        if (_store is null || _overlay is null)
        {
            return;
        }

        var d = _store.Current.DisplaysOrDefault;
        var under = MonitorResolver.UnderCursor(_monitors, Screens.Cursor());
        if (under is null)
        {
            return;
        }

        if (d.Keycaps.Mode == MonitorMode.UnderCursor && _overlay.Monitor.Id != under.Id)
        {
            _overlay.PlaceOn(under);
            _indicator?.Relayout();
        }
        else if (d.IndicatorTarget.Mode == MonitorMode.UnderCursor)
        {
            _indicator?.Relayout();
        }
    }

    /// <summary>Подключение/отключение, разрешение, основной экран, рабочая область: перечислить и разместить заново.</summary>
    private void OnDisplaysChanged()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _monitors = DisplayMonitors.Enumerate();
            LayoutOverlays();
        });
    }

    internal IReadOnlyList<MonitorDesc> Monitors => _monitors;

    internal IReadOnlyList<OverlayWindow> Overlays => _overlays;

    // ── предпочтения компьютера: приватность, тема ────────────────────────────

    private void OnPrefsChanged(AppPreferences p)
    {
        ApplyPrivacy(p);
        ApplyTools(p);
        ApplyCommandHotkeys();
        if (UiTheme.PaletteId != p.ColorTheme)
        {
            UiTheme.SetPalette(p.ColorTheme);
        }

        if (UiTheme.Material != p.MaterialOrDefault)
        {
            UiTheme.SetMaterial(p.MaterialOrDefault);
        }
    }

    /// <summary>Шрифты профиля: интерфейс всех окон и меню, подписи кейкапов (оверлей и превью) — без перезапуска.</summary>
    private static void ApplyFonts(LabSettings s)
    {
        SecondaryLabels.Settings = s.LabelsOrDefault;
        var f = s.FontsOrDefault;
        if (UiTheme.UiFont != f.Ui)
        {
            UiTheme.SetUiFont(f.Ui);
        }

        VisualFonts.SetKeycapFamily(f.Keycap);
    }

    /// <summary>
    /// Выключенный инструмент: окна закрываются, активный режим завершается, его сочетания снимаются (команды недоступны),
    /// настройки сохраняются. Общий источник ввода продолжает работать для остальных компонентов.
    /// </summary>
    private void ApplyTools(AppPreferences p)
    {
        var t = p.ToolsOrDefault;
        if (_controller is not null)
        {
            _controller.SetOverlayVisible(t.Keycaps && (_displayVisible || !p.HideParts.HasFlag(DisplayParts.Keycaps)));
        }
    }

    private void ApplyPrivacy(AppPreferences p)
    {
        var pr = p.PrivacyOrDefault;
        _controller?.SetPrivacy(pr.Enabled, pr.Apps);
        foreach (var o in _overlays)
        {
            o.ShowPrivacyBadge = pr.ShowBadge;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        _updateTimer?.Stop();
        _cursorFollow?.Stop();
        _plugins?.Dispose();
        _tools?.Dispose();
        if (_prefs is not null)
        {
            _prefs.Changed -= OnPrefsChanged;
        }

        if (_controller is not null)
        {
            _controller.DisplaysChanged -= OnDisplaysChanged;
        }

        foreach (var o in _overlays.Skip(1).ToList())
        {
            o.Close();
        }

        _presentation?.Dispose();
        int hotkeysLeft = 0;
        if (_hotkeys is not null)
        {
            _hotkeys.Dispose();
            hotkeysLeft = _hotkeys.RegisteredCount;
        }

        _tray?.Dispose();
        _tray = null;
        if (_store is not null)
        {
            _store.Changed -= ApplySettings;
            _store.Changed -= PluginProfileChanged;
            _profiles?.Dispose();
            _store.Flush();
            if (_store.LastSaveError is { } err)
            {
                _controller?.ShutdownNotes.Add("settings: " + err.Key);
            }
        }

        _settingsWindow?.Close();
        HelpService.Shutdown();
        UiTheme.StopListening();
        _instance?.Dispose();
        _instance = null;
        if (_options?.SelfTestUi == true)
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Backpack\SelfTest", throwOnMissingSubKey: false);
        }
        var visualStats = BuildVisualStats();
        if (_controller is not null)
        {
            _controller.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
            _controller.VisualStatsProvider = () => visualStats; // окна закрываются ниже; в экспорт — последнее состояние
        }

        if (_indicator is not null)
        {
            if (_controller is not null)
            {
                _controller.ContextReset -= _indicator.Rebaseline;
            }

            _indicator.Dispose();
        }

        _mouse?.Dispose();

        if (_controller is not null)
        {
            _controller.Wheel -= OnWheel;
            _controller.EscapePressed -= OnEscape;
            _controller.ContextReset -= OnContextReset;
            _controller.Stop();
            if (hotkeysLeft != 0)
            {
                _controller.ShutdownNotes.Add($"hotkeys: registrations left after close: {hotkeysLeft}");
            }

            if (LiveLens.Initialized != 0)
            {
                _controller.ShutdownNotes.Add($"lens: magnifier still initialized after close: {LiveLens.Initialized}");
            }
            if (_overlay is not null && !_overlay.StylesApplied)
            {
                _controller.ShutdownNotes.Add("overlay: click-through styles were not confirmed");
            }

            if (OverlayPresenter.ActiveRenderingSubscriptions != 0)
            {
                _controller.ShutdownNotes.Add($"visual: rendering subscriptions left after close: {OverlayPresenter.ActiveRenderingSubscriptions}");
            }

            if (_mouse?.IsRunning == true)
            {
                _controller.ShutdownNotes.Add("visual: mouse cursor timer still running after close");
            }

            if (_store is not null && _store.SubscriberCount != 0)
            {
                _controller.ShutdownNotes.Add($"visual: settings subscribers left after close: {_store.SubscriberCount}");
            }

            if (Loc.SubscriberCount != 0 || UiTheme.SubscriberCount != 0)
            {
                _controller.ShutdownNotes.Add($"visual: language/theme subscribers left after close: {Loc.SubscriberCount}/{UiTheme.SubscriberCount}");
            }

            if (_options?.SummaryOut is { } path)
            {
                try
                {
                    File.WriteAllText(path, _controller.BuildExport().ToJson());
                }
                catch (IOException)
                {
                    e.ApplicationExitCode = 4;
                }
                catch (UnauthorizedAccessException)
                {
                    e.ApplicationExitCode = 4;
                }
            }
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Phase 4H: поставляемые плагины (рисование, индикатор раскладки, лупа, линза, фокус, таймер) появляются в каталоге без
    /// ручного импорта: проверенные пакеты индекса поставки устанавливаются, код не запускается, новые остаются выключенными.
    /// Для впервые установленных переносятся прежние настройки, сочетания и «избранное» встроенных инструментов. При обновлении
    /// существующей установки — уведомление о том, что инструменты стали плагинами и ждут включения.
    /// </summary>
    private void SyncBundledPlugins()
    {
        if (_plugins is null || _prefs is null || _store is null)
        {
            return;
        }

        var added = PluginMigration.SyncBundled(_plugins.Store, BundledIndex.Load());
        if (!_hadSettings)
        {
            return; // чистая установка: переносить нечего (значения по умолчанию — не выбор пользователя), меню не меняется
        }

        var migrated = added.Where(m => m.Legacy is not null && PluginLegacyMigration.Apply(_plugins, _prefs, _store, m)).ToList();
        if (migrated.Count > 0)
        {
            _controller?.StartNotes.Add(new LocText("Note.ToolsArePlugins", string.Join(", ", migrated.Select(m => m.Name.For(Loc.Code)))));
        }
    }

    /// <summary>Сочетания whileTool (SDK 1.2): регистрируются, пока у плагина открыт инструмент, и освобождаются после.</summary>
    private void SyncPluginToolHotkeys()
    {
        if (_hotkeys is null || _tools is null || _plugins is null)
        {
            return;
        }

        var open = _tools.PluginsWithOpenTools;
        foreach (var id in _plugins.Running.Keys)
        {
            _hotkeys.SetPluginToolActive(id, open.Contains(id));
        }
    }

    /// <summary>Индикатор для плагинов (overlay.indicator): показывает последний запрос; скрыть может только тот, кто показал.</summary>
    private sealed class IndicatorBridge(LayoutIndicatorController indicator) : IPluginIndicatorHost
    {
        private string? _owner;

        public void Show(string pluginId, Backpack.Sdk.IndicatorRequest? request)
        {
            if (request is null)
            {
                if (_owner == pluginId)
                {
                    indicator.Hide();
                    _owner = null;
                }

                return;
            }

            _owner = pluginId;
            indicator.Show(request);
        }
    }

    /// <summary>Профиль сменился или изменился: плагины перечитывают свои значения из него (settings v8).</summary>
    private void PluginProfileChanged(LabSettings _) => _plugins?.ProfileChanged();

    private void ApplySettings(LabSettings s)
    {
        ApplyFonts(s);

        foreach (var o in _overlays)
        {
            o.ApplySettings(s);
        }

        LayoutOverlays();
        _controller?.ApplyLinger(TimeSpan.FromMilliseconds(s.Keys.LingerMs));
        _indicator?.UsePlugins(_plugins?.WantsLayout == true, s.Appearance);
        _mouse?.Apply(s.MouseCursor, s.Appearance.MouseLook);
    }

    private VisualStats? BuildVisualStats()
    {
        if (_store is null || _indicator is null)
        {
            return null;
        }

        var s = _store.Current;
        var d = _indicator.Detector;
        var c = d.Counters;
        return new VisualStats(
            s.Keys.Filter.ToString(),
            s.Keys.Trail.ToString(),
            s.Keys.MouseInRow,
            new LayoutIndicatorStats(_plugins?.WantsLayout == true, _indicator.IsPolling, d.LastOutcome.ToString(), d.LastDecision.ToString(),
                c.Samples, c.NoForeground, c.Unavailable, c.OwnWindow, c.FocusThread, c.ActiveThread, c.OwnerThreadFallback,
                c.ThreadsDiffer, c.OwnerHklDiffers, c.Baselines, c.ShownInWindow, c.ShownWindowSwitch, c.SuppressedWindowSwitch, _indicator.Shown),
            new MouseCursorStats(s.MouseCursor.Enabled, _mouse?.WindowVisible == true, _mouse?.Moves ?? 0));
    }

    private void OpenSettings() => OpenSettings(SettingsSection.Home);

    private void OpenSettings(SettingsSection section)
    {
        if (_store is null || _exiting)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Show(section);
            if (!_settingsWindow.IsVisible)
            {
                _hiddenToTray.Remove(_settingsWindow);
                _settingsWindow.Show();
            }

            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_store, new SettingsWindowActions(
            ShowOverlayExample: () =>
            {
                foreach (var o in _overlays)
                {
                    o.ShowExample(TimeSpan.FromSeconds(7.5));
                }
            },
            ShowLayoutIndicatorExample: () => _indicator?.ShowLabel(new LayoutLabel("RU", Loc.T("Layout.ExampleDetail"))),
            LayoutStatus: LayoutStatusText,
            OpenDiagnostics: OpenDiagnostics,
            HideToTray: () => HideToTray(),
            DisplayVisible: () => _displayVisible,
            SetDisplayVisible: SetDisplayVisible,

            Exit: ExitApplication,
            Commands: _commands,
            Plugins: _plugins,
            Monitors: () => _monitors,
            MissingMonitors: MissingMonitors,
            PrivacyStatus: () => _controller is { } c ? OverlayWindow.PrivacyStatusText(c.Privacy) ?? Loc.T("Privacy.Status.Allowed") : null,
            Autostart: _autostart), section, _profiles, _prefs, _styles, _hotkeys, _appInfo, _updates);
        _settingsWindowsOpened++;
        var w = _settingsWindow;
        w.Closing += OnSettingsWindowClosing;
        if (_diagnostics is null)
        {
            MainWindow = w;
        }

        w.Closed += (_, _) =>
        {
            _hiddenToTray.Remove(w);
            if (ReferenceEquals(_settingsWindow, w))
            {
                _settingsWindow = null;
            }
        };
        w.Show();
    }

    /// <summary>Состояние наблюдения простыми словами — для окна настроек (без технических подробностей). Целые фразы из ресурсов.</summary>
    private string LayoutStatusText()
    {
        if (_indicator is null || _store is null)
        {
            return "";
        }

        if (_plugins?.WantsLayout != true)
        {
            return Loc.T("LayoutStatus.Off"); // наблюдение ведётся, только пока включён плагин с input.layout
        }

        string key = _indicator.Detector.LastDecision switch
        {
            LayoutOutcome.ShownInWindow or LayoutOutcome.ShownWindowSwitch => "LayoutStatus.Shown",
            LayoutOutcome.SuppressedWindowSwitch => "LayoutStatus.Suppressed",
            LayoutOutcome.Baseline => "LayoutStatus.Baseline",
            LayoutOutcome.Unchanged => "LayoutStatus.Unchanged",
            _ => "LayoutStatus.Waiting",
        };
        return Loc.F(key, _indicator.Shown);
    }

    /// <summary>Подробные строки для окна диагностики (без окон, заголовков, процессов и клавиш).</summary>
    private IEnumerable<string> LayoutLines()
    {
        if (_indicator is null || _store is null)
        {
            yield break;
        }

        var d = _indicator.Detector;
        var c = d.Counters;
        string state = _plugins?.WantsLayout == true ? (_indicator.IsPolling ? Loc.T("Diag.Layout.Polling") : Loc.T("Diag.Layout.NotStarted")) : Loc.T("Diag.Layout.Off");
        yield return Loc.F("Diag.Layout.Line", state, Outcome(d.LastOutcome), Outcome(d.LastDecision));
        yield return Loc.F("Diag.Layout.Source", c.FocusThread, c.ActiveThread, c.OwnerThreadFallback, c.ThreadsDiffer, c.OwnerHklDiffers);
        yield return Loc.F("Diag.Layout.Samples", c.Samples, c.NoForeground, c.Unavailable, c.OwnWindow, c.Baselines, c.ShownInWindow, c.ShownWindowSwitch, c.SuppressedWindowSwitch);
        yield return Loc.F("Diag.Mouse", _store.Current.MouseCursor.Enabled ? Loc.T("Diag.On") : Loc.T("Diag.Off"),
            _mouse?.WindowVisible == true ? Loc.T("Diag.Visible") : Loc.T("Diag.Hidden"), _mouse?.Moves ?? 0);
    }

    private static string Outcome(LayoutOutcome o) => Enum.IsDefined(o) ? Loc.T("Outcome." + o) : o.ToString();

    private void SelfCheckWindows()
    {
        if (_controller is null || _overlay is null || _indicator is null)
        {
            return;
        }

        var notes = _controller.ShutdownNotes;
        nint fg = SelfCheck.ForegroundWindow();
        notes.Add($"selfcheck: overlay styles {(_overlay.StylesApplied ? "ok" : "MISSING")}, foreground is overlay: {(fg != 0 && fg == _overlay.Hwnd ? "YES" : "no")}");
        if (_indicator.WindowStylesApplied is { } applied)
        {
            notes.Add($"selfcheck: layout indicator styles {(applied ? "ok" : "MISSING")}, foreground is indicator: {(fg != 0 && fg == _indicator.WindowHandle ? "YES" : "no")}, visible: {_indicator.IsVisible}");
        }

        if (_mouse?.WindowStylesApplied is { } mouseApplied)
        {
            notes.Add($"selfcheck: mouse cursor styles {(mouseApplied ? "ok" : "MISSING")}, foreground is mouse window: {(fg != 0 && fg == _mouse.WindowHandle ? "YES" : "no")}, visible: {_mouse.WindowVisible}, moves: {_mouse.Moves}");
        }

        var d = _indicator.Detector;
        notes.Add($"selfcheck: layout polling {(_indicator.IsPolling ? "on" : "off")}, samples {d.Samples}, last {d.LastOutcome}, focus-thread {d.Counters.FocusThread}, owner-fallback {d.Counters.OwnerThreadFallback}, threads-differ {d.Counters.ThreadsDiffer}, own-window {d.Counters.OwnWindow}");
        notes.Add($"selfcheck: overlay keycaps {_overlay.Presenter.Visuals.Count}, example running: {_overlay.ExampleRunning}, settings windows opened: {_settingsWindowsOpened}, tray icon: {(_tray?.Visible == true ? "on" : "off")}");
    }

    /// <summary>
    /// Самопроверка интерфейса в реальных окнах без ввода пользователя: все разделы настроек в трёх языках и двух темах,
    /// повторное открытие окна, справка на каждом языке со всеми разделами и примерами, меню трея и синхронизация переключателей
    /// с окном настроек, скрытие в трей и восстановление. Исходные настройки и предпочтения восстанавливаются; затем выход.
    /// Результат — строки «selftest-ui» в ShutdownNotes (попадают в --summary-out).
    /// </summary>
    /// <summary>Страницы главного окна (Phase 4F: отдельной страницы «Справка» нет — её вход один, внизу меню).</summary>
    // Phase 4G: «Линза» и «Увеличение у курсора» — плагины, своих страниц нет (переход ведёт на «Плагины»).
    private static IEnumerable<SettingsSection> Pages() => Enum.GetValues<SettingsSection>().Where(s => s is not (SettingsSection.Help or SettingsSection.Lens or SettingsSection.CursorZoom or SettingsSection.Drawing));

    private void RunUiSelfTest()
    {
        var notes = _controller!.ShutdownNotes;
        var originalSettings = _store!.Current;
        var originalPrefs = _prefs!.Current;
        int steps = 0;
        var problems = new List<string>();
        void Check(bool ok, string what)
        {
            steps++;
            if (!ok)
            {
                problems.Add(what);
            }
        }

        // 1. Разделы в прямом и обратном порядке, окно открывается заново (исходное падение — переход «Внешний вид» → «Клавиши»).
        for (int round = 0; round < 2; round++)
        {
            OpenSettings();
            var w = _settingsWindow!;
            foreach (var section in Pages().Concat(Pages().Reverse()))
            {
                w.Show(section);
                w.UpdateLayout();
                Check(w.Section == section, $"section {section}");
            }

            w.Close();
            Check(_settingsWindow is null, "settings window released");
        }

        // 1a. Автозапуск (свой раздел реестра самопроверки): переключатель показывает запись и меняет её.
        if (_autostart is { } autostart)
        {
            OpenSettings(SettingsSection.Application);
            var aw = _settingsWindow!;
            autostart.Disable();
            aw.RefreshAutostart();
            Check(aw.AutostartShown == false, "autostart: off shown");
            aw.ClickAutostart();
            Check(autostart.Status().State == AutostartState.On && aw.AutostartShown == true, "autostart: turned on");
            aw.ClickAutostart();
            Check(autostart.Status().State == AutostartState.Off && aw.AutostartShown == false, "autostart: turned off");
        }

        // 2. Три языка × две темы × все разделы в одном открытом окне (смена применяется на месте).
        OpenSettings();
        var sw = _settingsWindow!;
        foreach (var lang in new[] { UiLanguage.Ukrainian, UiLanguage.English, UiLanguage.Russian })
        {
            foreach (var theme in new[] { UiThemeMode.Dark, UiThemeMode.Light })
            {
                _prefs.Update(p => p with { Language = lang, Theme = theme });
                Loc.Set(lang);
                UiTheme.Set(theme);
                foreach (var section in Pages())
                {
                    sw.Show(section);
                    sw.UpdateLayout();
                    Check(sw.Section == section && sw.Title == Loc.T("Settings.WindowTitle"), $"{lang}/{theme}/{section}");
                }

                Check(_diagnostics!.Title == Loc.T("Diag.WindowTitle"), $"diagnostics title {lang}");
            }

            // Справка на этом языке: все разделы оглавления и все примеры в одном окне.
            var help = HelpService.Show(HelpLibrary.IndexFile);
            foreach (var t in help.Library.Toc)
            {
                help.Navigate(t.File);
                help.UpdateLayout();
                Check(help.CurrentFile == t.File && help.Document is not null, $"help {lang} {t.File}");
            }

            foreach (var demo in HelpDemoWindow.Demos)
            {
                var d = HelpDemoWindow.Show(help, demo);
                d?.UpdateLayout();
                Check(d is { IsRunning: true } && d.Demo == demo, $"demo {demo}");
            }

            HelpDemoWindow.CloseIfOpen();
            Check(HelpService.WindowsCreated == 1, "help window is single");
        }

        // Снимки собственных окон в тёмной теме (по желанию): для ручного просмотра читаемости.
        if (Environment.GetEnvironmentVariable("BACKPACK_SELFTEST_SHOTS") is { Length: > 0 } shots)
        {
            Directory.CreateDirectory(shots);
            foreach (var lang in new[] { UiLanguage.Ukrainian, UiLanguage.English })
            {
                Loc.Set(lang);
                UiTheme.Set(UiThemeMode.Dark);
                sw.Show(SettingsSection.Keys);
                var help = HelpService.Show("07-profiles.md");
                foreach (var (w, name) in new (Window, string)[] { (sw, "settings"), (_diagnostics!, "diagnostics"), (help, "help") })
                {
                    w.Activate();
                    w.UpdateLayout();
                    PumpFor(400);
                    SelfCheck.SaveWindow(w, Path.Combine(shots, $"{Loc.Code}-dark-{name}.png"));
                }
            }
        }

        // 3. Меню трея: переключатели меняют настройки, открытое окно настроек видит то же значение.
        sw.Show(SettingsSection.Keys);
        var menu = _tray!.BuildMenu();
        var combos = TrayMenu.Find(menu, "tray.combos")!;
        bool before = _store.Current.Keys.Filter == KeyFilter.CombosOnly;
        Check(combos.IsChecked == before, "tray reflects filter");
        combos.IsChecked = !before;
        combos.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check((_store.Current.Keys.Filter == KeyFilter.CombosOnly) == !before, "tray toggles filter");
        Check(TrayMenu.Find(_tray.BuildMenu(), "tray.combos")!.IsChecked == !before, "tray menu rebuilt with new state");
        _store.Update(_ => originalSettings);
        var overlayItem = TrayMenu.Find(_tray.BuildMenu(), "tray.overlay")!;
        overlayItem.IsChecked = false;
        overlayItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(!_controller.OverlayIntendedVisible && !_controller.ShuttingDown, "overlay hidden, capture running");
        _controller.SetOverlayVisible(true);
        Check(_controller.OverlayIntendedVisible, "overlay shown again");

        // 4. Скрытие в трей и восстановление.
        HideToTray();
        Check(InTray && !_diagnostics!.IsVisible && !sw.IsVisible && HelpService.OpenWindow?.IsVisible != true, "hidden to tray");
        ShowWindows();
        Check(!InTray && _diagnostics!.IsVisible && sw.IsVisible, "restored from tray");

        // 5. Показ и скрытие отображения: захват работает, части скрываются и возвращаются.
        SetDisplayVisible(false);
        Check(!_controller.OverlayIntendedVisible && _indicator!.Suppressed && _mouse!.Suppressed && !_controller.ShuttingDown, "display hidden, capture running");
        SetDisplayVisible(true);
        Check(_controller.OverlayIntendedVisible && !_indicator!.Suppressed && !_mouse!.Suppressed, "display shown again");
        Check(_presentation?.AnyWindowCapturesMouse == false, "no mode window captures the mouse");
        notes.Add("selftest-ui: hotkeys " + string.Join(", ", _commands.All.Where(d => _hotkeys!.StatusOf(d.Id) != HotkeyStatus.NotSet).Select(d => $"{d.Id}={_hotkeys!.StatusOf(d.Id)}")));

        // 6. Технический прототип линзы: настоящий Magnification API на небольшой области основного монитора, ~0.6 с.
        //    Пиксели не читаются и не сохраняются — только статус API и число обновлений.
        var (_, work, scale) = Screens.MonitorAt(0, 0);
        using (var probe = new LiveLens(LensSettings.Default with { Zoom = 1.5 }, new PixelRect(work.Left + 40, work.Top + 40, (int)(240 * scale), (int)(135 * scale)), scale, () => []))
        {
            probe.Open();
            PumpFor(600);
            notes.Add($"selftest-ui: lens status {probe.Status}, frames {probe.Frames}, source failures {probe.SourceFailures}, host window {(Screens.IsWindow(probe.HostHandle) ? "yes" : "no")}");
            probe.Close();
        }

        Check(LiveLens.Initialized == 0, "lens released");

        // 7. Режим рисования в настоящих окнах: варианты панели, текст, область видимости F2, настоящий снимок.
        RunDrawingSelfTest(Check, notes);

        // 8. Динамическая приватность через настоящий UIA-провайдер WPF в собственном окне (искусственные поля, ввод не вводится).
        RunPrivacySelfTest(Check, notes);

        // 9. Мониторы: одно окно на монитор, перенос без пересоздания, возврат к исходному выбору.
        RunMonitorSelfTest(Check, notes);

        // 10. «Фокус» и увеличение у курсора в настоящих окнах (Magnification API), без ввода.
        RunToolsSelfTest(Check, notes);

        // 11. Плагин: полный путь на собственном примере (если задан пакет BACKPACK_SELFTEST_PLUGIN).
        if (Environment.GetEnvironmentVariable("BACKPACK_SELFTEST_PLUGIN") is { Length: > 0 } pkg)
        {
            RunPluginSelfTest(Check, notes, pkg);
        }

        // 11a. Phase 4G: поставляемые плагины-инструменты (папка Plugins рядом с программой или BACKPACK_BUNDLED_PLUGINS).
        RunToolPluginsSelfTest(Check, notes);

        // 12. Phase 4E: команда «Фокус» из всех точек входа, приватность при синтетическом вводе в собственном окне, новые
        // инструменты рисования и вложенное радиальное меню.
        RunPhase4ESelfTest(Check, notes);

        // 13. Phase 4F: компоновка всех страниц на четырёх ширинах и трёх языках, автосохранение профиля, шрифты, ручки «Фокуса»,
        // решение приватности для рабочего стола.
        RunPhase4FSelfTest(Check, notes);

        // 14. Phase 4H: управление линзы, «Фокуса» и панели при настоящем указателе (только пока впереди окно самопроверки).
        RunLiveHoverSelfTest(Check, notes);

        // Исходное состояние пользователя.
        _store.Update(_ => originalSettings);
        _prefs.Update(_ => originalPrefs);
        Loc.Set(originalPrefs.Language);
        UiTheme.Set(originalPrefs.Theme);
        notes.Add(problems.Count == 0 ? $"selftest-ui: ok, steps {steps}" : $"selftest-ui: FAILED {problems.Count} of {steps}: {string.Join("; ", problems.Take(10))}");
        ExitApplication();
    }

    /// <summary>
    /// Самопроверка рисования: каждый вариант панели открывается и закрывается; F2 зарегистрирована только в режиме; текст
    /// подтверждается; снимок делается настоящим захватом во временную папку и проверяется по двум пикселям — линия под панелью
    /// должна быть видна (панель на снимок не попала). Содержимое экрана не сохраняется: файл удаляется сразу после проверки,
    /// в отчёт попадают только размеры и признаки «да/нет».
    /// </summary>
    private void RunDrawingSelfTest(Action<bool, string> check, List<string> notes)
    {
        var p = _presentation!;
        string dir = Path.Combine(Path.GetTempPath(), "Backpack-selftest-" + Guid.NewGuid().ToString("N")[..8]);
        var red = DrawingWindow.Palette[0];
        var host = _drawHost!;
        _prefs!.Update(x => x with { ScreenshotFolder = dir });
        // Phase 4H: холст хоста открывается так же, как по запросу плагина «Рисование» (overlay.draw), владелец — самопроверка.
        var basic = new Backpack.Sdk.DrawRequest(Tool: "line", Color: red.ToString(), Thickness: 12, ShotAction: "save", AfterShot: "keep");
        var modes = new List<Backpack.Sdk.DrawRequest> { basic };
        modes.AddRange(new[] { "top", "bottom", "left", "right" }.Select(e => basic with { Toolbar = "autohide", AutoHideEdge = e }));
        modes.Add(basic with { Toolbar = "radial" });
        try
        {
            foreach (var mode in modes)
            {
                string name = mode.Toolbar == "autohide" ? $"auto-hide {mode.AutoHideEdge}" : mode.Toolbar;
                check(host.Open("selftest", "canvas", mode), $"draw host opens ({name})");
                check(!host.Open("other", "canvas", mode), $"second plugin is refused while the canvas is busy ({name})");
                PumpFor(250);
                var d = p.Drawing;
                check(d is { IsVisible: true }, $"drawing opens ({name})");
                if (d is null)
                {
                    continue;
                }

                if (mode.Toolbar == "docked")
                {
                    check(d.ToolbarShown, "docked toolbar visible");
                }

                if (mode.Toolbar == "radial")
                {
                    check(!d.ToolbarShown, "radial-only: no toolbar");
                    p.Execute(PresentationCommand.Radial);
                    check(p.State.Radial && d.RadialOpen, "radial-only: menu opens");
                    d.Radial!.Choose(RadialItem.Text);
                    d.BeginText(new Point(120, 200));
                    d.TypeForPreview("selftest");
                    d.CommitText();
                    check(d.TextCount == 1, "radial-only: text committed");
                    p.Execute(PresentationCommand.Radial);
                    d.Radial.Choose(RadialItem.Undo);
                    check(d.TextCount == 0, "radial-only: undo");
                    p.Execute(PresentationCommand.Radial);
                    d.Radial.Choose(RadialItem.Redo);
                    check(d.TextCount == 1, "radial-only: redo");
                }

                if (mode == basic)
                {
                    // Линия посередине и линия под верхним краем панели; на снимке обе должны быть красными.
                    var bar = d.ToolbarHost;
                    double barX = Canvas.GetLeft(bar) + (bar.ActualWidth / 2), barY = Canvas.GetTop(bar) + 4;
                    d.Draw(new Point(200, 300), new Point(500, 300));
                    d.Draw(new Point(barX - 60, barY), new Point(barX + 60, barY));
                    var task = p.TakeScreenshotAsync();
                    while (!task.IsCompleted)
                    {
                        PumpFor(30);
                    }

                    var result = task.Result;
                    check(result is { Ok: true, FilePath: not null }, "screenshot saved");
                    if (result?.FilePath is { } file && File.Exists(file))
                    {
                        var (width, height, mid, underBar) = SamplePng(file, d.Scale, new Point(350, 300), new Point(barX, barY));
                        File.Delete(file);
                        var b = d.Bounds;
                        check(width == b.Width && height == b.Height, "screenshot has the monitor's physical size");
                        check(IsNear(mid, red), "drawing is on the screenshot");
                        check(IsNear(underBar, red), "toolbar is not on the screenshot");
                        check(Path.GetFileName(file).StartsWith("Backpack_", StringComparison.Ordinal) && file.EndsWith(".png", StringComparison.Ordinal), "screenshot name");
                        notes.Add($"selftest-ui: screenshot {width}x{height} at scale {d.Scale:0.##}, drawing visible {IsNear(mid, red)}, toolbar excluded {IsNear(underBar, red)}");
                    }

                    check(d.ShapeCount == 2, "drawings kept after screenshot");
                    var again = p.TakeScreenshotAsync();
                    check(again.IsCompleted && again.Result is null, "repeated screenshot ignored");
                }

                p.Escape();
                PumpFor(250);
                check(p.Drawing is null && !p.State.Drawing, $"drawing closes by Esc ({name})");
                check(!p.AnyWindowCapturesMouse && !p.OpenWindows.Any(), $"no window captures the mouse ({name})");
                check(host.Owner is null, $"canvas is free after Esc ({name})");
            }
        }
        finally
        {
            if (p.State.Drawing)
            {
                p.Execute(PresentationCommand.ExitAll);
            }

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    /// <summary>
    /// Собственное окно с обычным полем, полем пароля и полем, которое меняет тип (TextBox ↔ PasswordBox). Фокус ставится
    /// программно; состояние приватности читается из фильтра. Это проверка цепочки «событие фокуса → UIA IsPassword → фильтр»
    /// для провайдера WPF, а не для произвольных приложений. Нажатия не вводятся, содержимое полей — служебные строки.
    /// </summary>
    private void RunPrivacySelfTest(Action<bool, string> check, List<string> notes)
    {
        var c = _controller!;
        var original = _prefs!.Current;
        _prefs.Update(x => x with { Privacy = new PrivacySettings(true, true, []) });
        var text = new TextBox { Text = "selftest", Width = 220 };
        var password = new PasswordBox { Width = 220 };
        password.Password = "selftest";
        var swap = new ContentControl { Content = new TextBox { Text = "selftest", Width = 220 } };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(text);
        panel.Children.Add(password);
        panel.Children.Add(swap);
        var w = new Window { Title = "Backpack privacy self-test", Content = panel, Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
        w.Show();
        w.Activate();
        bool Wait(Func<bool> condition, int ms = 1500)
        {
            for (int t = 0; t < ms; t += 30)
            {
                if (condition())
                {
                    return true;
                }

                PumpFor(30);
            }

            return condition();
        }

        // Phase 4H (п. 8): WPF Focus() меняет только фокус внутри окна. Если окно не стало окном переднего плана (пользователь
        // работает в другом приложении, блокировка переднего плана), Windows и UI Automation сообщают поле того приложения —
        // туда же уходят нажатия, — и фильтр приватности правильно оценивает именно его. Эксперимент Phase 4H воспроизвёл
        // провал Phase 4G так: WPF показывал PasswordBox с фокусом и IsActive=True, UIA — обычное поле другого процесса.
        // Поэтому перед каждым шагом проверяется системный передний план; без него шаг не засчитывается и не проваливается.
        nint hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        bool Front()
        {
            if (GetForegroundWindow() == hwnd)
            {
                return true;
            }

            w.Activate();
            return Wait(() => GetForegroundWindow() == hwnd, 1000);
        }

        string FrontPid() => GetWindowThreadProcessId(GetForegroundWindow(), out uint fp) != 0 ? fp.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
        try
        {
            check(c.FocusSource is not null, "privacy: focus source running");
            if (!Front())
            {
                notes.Add($"selftest-ui: privacy steps skipped — the test window is not the foreground window (foreground pid {FrontPid()}); environment, not a privacy result");
                return;
            }

            text.Focus();
            bool allowed = Wait(() => c.Privacy.State == Backpack.Core.Privacy.PrivacyState.Allowed);
            check(allowed, "privacy: ordinary field allows display");

            long closuresBefore = c.Privacy.Closures;
            if (!Front())
            {
                notes.Add($"selftest-ui: privacy password steps skipped — foreground moved to pid {FrontPid()} (environment)");
                return;
            }

            password.Focus();
            bool closedImmediately = !c.Privacy.Open || Wait(() => !c.Privacy.Open, 200);
            bool isProtected = Wait(() => c.Privacy.State == Backpack.Core.Privacy.PrivacyState.Protected);
            check(isProtected && c.Privacy.Reason == Backpack.Core.Privacy.PrivacyReason.PasswordField, $"privacy: password field is protected ({c.Privacy.State}/{c.Privacy.Reason}, foreground pid {FrontPid()}, history {string.Join(" ", c.Privacy.History)})");
            check(closedImmediately && c.Privacy.Closures > closuresBefore, "privacy: display closes on focus change");
            check(c.Display.Entries.All(e => Backpack.Core.Input.Slots.IsMouse(e.KeySlot)), "privacy: no key entries while protected");
            check(_overlays.All(o => o.Presenter.Visuals.All(v => Backpack.Core.Input.Slots.IsMouse(v.Item.MatchKey))), "privacy: no keycaps while protected");

            text.Focus();
            check(Wait(() => c.Privacy.State == Backpack.Core.Privacy.PrivacyState.Allowed), "privacy: leaving the protected field reopens");

            // Поле меняет тип на парольный: новый элемент получает фокус — проверка новой версии закрывает показ.
            ((TextBox)swap.Content).Focus();
            Wait(() => c.Privacy.State == Backpack.Core.Privacy.PrivacyState.Allowed);
            long serialBeforeSwap = c.FocusSource?.FocusSerial ?? -1;
            var swapped = new PasswordBox { Width = 220 };
            swap.Content = swapped;
            swap.UpdateLayout();
            Wait(() => swapped.IsLoaded, 500);
            if (!Front())
            {
                notes.Add($"selftest-ui: privacy dynamic-switch step skipped — foreground moved to pid {FrontPid()} (environment)");
                return;
            }

            swapped.Focus();
            bool swappedFocused = Wait(() => swapped.IsKeyboardFocused, 500);
            bool swappedProtected = Wait(() => c.Privacy.State == Backpack.Core.Privacy.PrivacyState.Protected);
            check(swappedFocused && swappedProtected, $"privacy: dynamic switch to password is protected (focused {swappedFocused}, loaded {swapped.IsLoaded}, state {c.Privacy.State}/{c.Privacy.Reason}, fg {(System.Windows.Input.Keyboard.FocusedElement?.GetType().Name ?? "none")}, foreground pid {FrontPid()}, focus events {(c.FocusSource?.FocusSerial ?? -1) - serialBeforeSwap}, history {string.Join(" ", c.Privacy.History)})");

            // Список запрета: имя собственного процесса — «скрыто для приложения».
            string self = System.IO.Path.GetFileName(Environment.ProcessPath ?? "Backpack.exe").ToLowerInvariant();
            _prefs.Update(x => x with { Privacy = new PrivacySettings(true, true, [self]) });
            text.Focus();
            check(Wait(() => c.Privacy.Reason == Backpack.Core.Privacy.PrivacyReason.BlockedApp), "privacy: blocked app");
            notes.Add($"selftest-ui: privacy probes {c.FocusSource?.Probes ?? 0}, no-data {c.FocusSource?.ProbeFailures ?? 0}, stale {c.Privacy.StaleResults}, timeouts {c.Privacy.Timeouts}");
        }
        finally
        {
            w.Close();
            _prefs.Update(_ => original);
        }
    }

    private void RunMonitorSelfTest(Action<bool, string> check, List<string> notes)
    {
        var original = _store!.Current;
        var monitors = _monitors;
        notes.Add($"selftest-ui: monitors {monitors.Count}: " + string.Join("; ", monitors.Select(m => $"{m.Bounds.Width}x{m.Bounds.Height} @{m.Scale * 100:0}%{(m.IsPrimary ? " primary" : "")}")));
        try
        {
            var all = new MonitorTarget(MonitorMode.Multiple, monitors.Select(m => new MonitorRef(m.Id, m.Name)).ToList());
            _store.Update(x => x with { Displays = x.DisplaysOrDefault with { Keycaps = all } });
            PumpFor(150);
            check(_overlays.Count == monitors.Count && _overlays.Select(o => o.Monitor.Id).Distinct().Count() == monitors.Count, "monitors: one overlay per selected monitor");
            var first = _overlays[0];

            var gone = new MonitorTarget(MonitorMode.Selected, [new MonitorRef("selftest-missing", "Missing")]);
            _store.Update(x => x with { Displays = x.DisplaysOrDefault with { Keycaps = gone } });
            PumpFor(100);
            check(_overlays.Count == 1 && _overlays[0].Monitor.IsPrimary && ReferenceEquals(first, _overlays[0]), "monitors: missing monitor falls back to primary, first window kept");
            check(MissingMonitors().Any(m => m.Id == "selftest-missing"), "monitors: missing monitor reported");

            _store.Update(x => x with { Displays = x.DisplaysOrDefault with { Keycaps = new MonitorTarget(MonitorMode.UnderCursor, []) } });
            PumpFor(300);
            var under = MonitorResolver.UnderCursor(monitors, Screens.Cursor());
            check(_overlays.Count == 1 && under is not null && _overlays[0].Monitor.Id == under.Id && ReferenceEquals(first, _overlays[0]), "monitors: under-cursor uses the pointer monitor without recreating");
        }
        finally
        {
            _store.Update(_ => original);
            PumpFor(100);
        }

        check(_overlays.Count == KeycapMonitors(_store.Current).Count, "monitors: restored");
    }

    private void RunToolsSelfTest(Action<bool, string> check, List<string> notes)
    {
        const string plugin = "backpack.selftest";
        var e = _tools!;
        var primary = _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors[0];
        var area = new Backpack.Visual.Presentation.PixelRect(primary.Work.Left + 100, primary.Work.Top + 100, (int)(360 * primary.Scale), (int)(220 * primary.Scale));
        try
        {
            e.Open(plugin, new PluginToolRequest("lens", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Region), null));
            var f = e.SessionsOf(plugin).Single().Focus!;
            check(f.State == FocusState.Selecting && f.CapturesMouse, "lens: selection window");
            f.Activate(area);
            PumpFor(400);
            check(f.State == FocusState.Active && f.Surface?.Status == Backpack.Visual.Presentation.LensStatus.Live && !f.CapturesMouse, "lens: live, view mode lets clicks through");
            check(!f.ControlsVisible || f.Grips.All(g => g.Opacity < 1), "lens: controls hidden until hover");
            e.Edit(plugin, "lens");
            PumpFor(60);
            check(f.ControlsVisible, "lens: edit command reveals controls without hover");
            notes.Add($"selftest-ui: lens frames {f.Surface?.Frames ?? 0}, zoom {f.Surface?.Zoom:0.##}");
            e.Escape();
            check(f.State == FocusState.Off && f.Surface is null && Backpack.Visual.Presentation.MagnifierSurface.OpenCount == 0 && e.Count == 0, "lens: Esc closes, magnifier released");

            e.Open(plugin, new PluginToolRequest("dim", null, new Backpack.Sdk.DimRequest()));
            f = e.SessionsOf(plugin).Single().Focus!;
            f.Activate(area);
            PumpFor(300);
            check(f.DimWindows.Count == 1 && f.DimWindows[0].Monitor.Id == primary.Id && !f.CapturesMouse, "dim: only on the area's monitor");
            check(f.DimWindows[0].StylesApplied, "dim: window is click-through");
            e.Close(plugin, "dim");
            check(f.DimWindows.Count == 0 && e.Count == 0, "dim: closed");

            e.Open(plugin, new PluginToolRequest("zoom", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Follow, HideMouseIcon: true), null));
            PumpFor(300);
            var z = e.SessionsOf(plugin).Single().Cursor!;
            check(z.Surface?.Status == Backpack.Visual.Presentation.LensStatus.Live, "zoom: follows the pointer");
            check(_mouse!.ZoomHidden, "zoom: mouse icon auto-hidden");
            e.Close(plugin, "zoom");
            check(!_mouse.ZoomHidden && _mouse.AvoidPixels == 0 && Backpack.Visual.Presentation.MagnifierSurface.OpenCount == 0, "zoom: close restores the mouse icon");
            e.Open(plugin, new PluginToolRequest("pin", new Backpack.Sdk.MagnifierRequest(Backpack.Sdk.MagnifierMode.Pinned), null));
            PumpFor(200);
            check(e.SessionsOf(plugin).Single().Cursor?.PinnedAt is not null, "zoom: pinned in place");
            e.CloseAll(plugin);
            check(e.Count == 0 && Backpack.Visual.Presentation.MagnifierSurface.OpenCount == 0, "zoom: plugin close-all releases everything");
        }
        finally
        {
            e.CloseAll(plugin);
        }
    }

    /// <summary>Полный путь плагина в настоящих окнах и процессах на собственном примере (пакет из tools/pack-plugin.ps1).</summary>
    private void RunPluginSelfTest(Action<bool, string> check, List<string> notes, string package)
    {
        var pm = _plugins!;
        bool Wait(Func<bool> c, int ms)
        {
            for (int t = 0; t < ms; t += 50)
            {
                if (c())
                {
                    return true;
                }

                PumpFor(50);
            }

            return c();
        }

        // Phase 4H: поставляемый «Таймер» уже в каталоге (нетронутый) — проверка начинается с чистого места и в конце его возвращает.
        string? bundledId = PluginStore.Inspect(package).Manifest?.Id;
        bool restore = bundledId is not null && pm.Store.Get(bundledId) is { } had && pm.Store.IsBundled(had) && !had.Trusted && !had.Enabled;
        if (restore)
        {
            pm.Remove(bundledId!);
        }

        var r = pm.Import(package);
        check(r.Outcome is Backpack.Visual.Plugins.ImportOutcome.Installed or Backpack.Visual.Plugins.ImportOutcome.Updated, "plugin: import " + string.Join(";", r.Errors));
        if (r.Manifest is not { } m)
        {
            return;
        }

        check(!pm.Running.ContainsKey(m.Id), "plugin: import does not start code");
        check(!pm.Enable(m.Id), "plugin: not enabled without trust");
        pm.Store.Trust(m.Id);
        check(pm.Enable(m.Id), "plugin: enabled after trust");
        bool running = Wait(() => pm.Running.TryGetValue(m.Id, out var p) && p.State == Backpack.Visual.Plugins.PluginRunState.Running, 8000);
        check(running, "plugin: process started and handshake done");
        string cmd = $"plugin.{m.Id}.toggle";
        check(Wait(() => _commands.Get(cmd) is not null, 2000), "plugin: command registered");
        PumpFor(500);
        check(!pm.Panels.ContainsKey(m.Id), "plugin: timer panel does not appear by itself after start");
        pm.SettingsChanged(m.Id, new System.Text.Json.Nodes.JsonObject { ["minutes"] = 2, ["seconds"] = 0, ["warnSeconds"] = 30 });
        check(_commands.Execute($"plugin.{m.Id}.reset"), "plugin: reset command runs through the registry");
        check(Wait(() => pm.Panels.TryGetValue(m.Id, out var w) && w.Window.Text == "02:00", 3000), "plugin: settings applied (02:00)");
        check(_commands.Execute(cmd), "plugin: command runs through the registry");
        check(Wait(() => pm.Panels.TryGetValue(m.Id, out var w) && w.Window.Text.StartsWith("01:5", StringComparison.Ordinal), 4000), "plugin: timer counts down after the command");
        check(pm.Panels.TryGetValue(m.Id, out var ph) && ph.ButtonsWindow.Buttons.Count == 3 && !ph.ControlsVisible, "plugin: panel buttons exist and are hidden until hover");
        int? pid = pm.Running[m.Id].ProcessId;
        OpenSettings(SettingsSection.Plugins);
        pm.Store.SetFavorite(m.Id, true);
        PumpFor(200);
        _settingsWindow!.UpdateLayout();
        var fav = _settingsWindow.FavoriteItems;
        check(fav.Count == 1, "plugin: favorite appears in the side menu after rebuild" );
        pm.Disable(m.Id);
        check(!pm.Running.ContainsKey(m.Id) && !pm.Panels.ContainsKey(m.Id) && _commands.Get(cmd) is null, "plugin: disable stops process, panel and commands");
        bool exited = pid is null || Wait(() => !System.Diagnostics.Process.GetProcesses().Any(x => x.Id == pid), 3000);
        check(exited, "plugin: no process left");
        check(pm.Remove(m.Id) && pm.Store.Get(m.Id) is null, "plugin: removed");
        if (restore)
        {
            RestoreBundled(m.Id, package);
        }

        notes.Add($"selftest-ui: plugin {m.Id} {m.Version} full path ok");
    }

    /// <summary>
    /// Phase 4G: каждый поставляемый плагин с overlay.magnifier или overlay.dim проходит полный путь в настоящих окнах: импорт не
    /// запускает код, без доверия не включается, команды открывают и закрывают ровно одну сессию хоста, повтор закрывает, Esc
    /// закрывает, отключение во время работы закрывает окна и убирает команды и процесс, удаление и повторная установка.
    /// Плагин, уже установленный пользователем, не трогается (проверка пропускается с отметкой).
    /// </summary>
    private void RunToolPluginsSelfTest(Action<bool, string> check, List<string> notes)
    {
        var pm = _plugins!;
        var tools = _tools!;
        bool Wait(Func<bool> c, int ms)
        {
            for (int t = 0; t < ms; t += 50)
            {
                if (c())
                {
                    return true;
                }

                PumpFor(50);
            }

            return c();
        }

        foreach (var package in PluginMigration.BundledPackages())
        {
            var (manifest, _) = PluginStore.Inspect(package);
            if (manifest is null || !(manifest.Has(Backpack.Sdk.Capabilities.OverlayMagnifier) || manifest.Has(Backpack.Sdk.Capabilities.OverlayDim)))
            {
                continue;
            }

            if (pm.Store.Get(manifest.Id) is { } installed && !(pm.Store.IsBundled(installed) && !installed.Trusted && !installed.Enabled))
            {
                notes.Add($"selftest-ui: {manifest.Id} already installed — tool path skipped");
                continue;
            }

            string id = manifest.Id;
            pm.Remove(id); // нетронутая поставляемая установка (Phase 4H ставит её при запуске) — проверяется заново и восстанавливается
            var r = pm.Import(package);
            check(r.Outcome == Backpack.Visual.Plugins.ImportOutcome.Installed, $"{id}: import");
            check(!pm.Running.ContainsKey(id), $"{id}: import does not start code");
            check(!pm.Enable(id), $"{id}: not enabled without trust");
            pm.Store.Trust(id);
            check(pm.Enable(id) && Wait(() => pm.Running.TryGetValue(id, out var p) && p.State == PluginRunState.Running, 8000), $"{id}: enabled and running");
            var toggles = manifest.Commands.Where(c => !c.Hold && c.Id != "edit").Select(c => c.Id).ToList();
            check(Wait(() => toggles.All(c => _commands.Get($"plugin.{id}.{c}") is not null), 2000), $"{id}: commands registered");
            foreach (var c in toggles)
            {
                check(_commands.Execute($"plugin.{id}.{c}") && Wait(() => tools.SessionsOf(id).Count == 1, 3000), $"{id}.{c}: opens one session");
                check(_commands.Execute($"plugin.{id}.{c}") && Wait(() => tools.SessionsOf(id).Count == 0, 3000), $"{id}.{c}: repeat closes it");
            }

            if (toggles.Count >= 2)
            {
                // Независимые команды одного плагина: вторая заменяет первую, сессий не становится две.
                _commands.Execute($"plugin.{id}.{toggles[0]}");
                Wait(() => tools.SessionsOf(id).Count == 1, 3000);
                _commands.Execute($"plugin.{id}.{toggles[1]}");
                PumpFor(400);
                check(tools.SessionsOf(id).Count == 1, $"{id}: {toggles[1]} replaces {toggles[0]} (no second window)");
                tools.CloseAll(id);
                Wait(() => tools.SessionsOf(id).Count == 0, 2000);
            }

            if (manifest.Commands.FirstOrDefault(c => c.Hold) is { } hold)
            {
                // Из реестра сочетание не удерживается: удержание открывается и сразу отпускается — без зависшего окна.
                check(_commands.Execute($"plugin.{id}.{hold.Id}"), $"{id}.{hold.Id}: runs");
                check(Wait(() => !pm.IsCommandHeld($"plugin.{id}.{hold.Id}") && tools.SessionsOf(id).Count == 0, 3000), $"{id}.{hold.Id}: release closes the session");
            }

            _commands.Execute($"plugin.{id}.{toggles[0]}");
            check(Wait(() => tools.SessionsOf(id).Count == 1, 3000) && tools.Escape() && Wait(() => tools.SessionsOf(id).Count == 0, 2000), $"{id}: Esc closes");
            _commands.Execute($"plugin.{id}.{toggles[0]}");
            Wait(() => tools.SessionsOf(id).Count == 1, 3000);
            int? pid = pm.Running.TryGetValue(id, out var running) ? running.ProcessId : null;
            pm.Disable(id);
            check(tools.SessionsOf(id).Count == 0 && !pm.Running.ContainsKey(id) && toggles.All(c => _commands.Get($"plugin.{id}.{c}") is null), $"{id}: disable while open closes windows and commands");
            check(pid is null || Wait(() => !System.Diagnostics.Process.GetProcesses().Any(x => x.Id == pid), 3000), $"{id}: no process left");
            check(pm.Remove(id) && pm.Store.Get(id) is null, $"{id}: removed");
            check(pm.Import(package).Outcome == Backpack.Visual.Plugins.ImportOutcome.Installed && !pm.Running.ContainsKey(id), $"{id}: reinstalled, still off until enabled");
            pm.Remove(id);
            RestoreBundled(id, package);
            notes.Add($"selftest-ui: tool plugin {id} {manifest.Version} ok");
        }

        RunDrawIndicatorPluginsSelfTest(check, notes, Wait);
    }

    /// <summary>Вернуть поставляемый плагин в исходное состояние каталога (выключен, с отметкой «От создателя»).</summary>
    private void RestoreBundled(string id, string package)
    {
        if (BundledIndex.Load().Contains(id))
        {
            _plugins!.Store.Undismiss(id);
            _plugins.Import(package, BundledIndex.Sha256Of(package));
        }
    }

    /// <summary>
    /// Phase 4H: «Рисование» и «Индикатор раскладки» — настоящие процессы плагинов через тот же контракт (SDK 1.2). Рисование:
    /// команда открывает холст хоста, сочетание снимка (F2, whileTool) регистрируется только на время холста, выбор инструмента
    /// доходит до холста, Esc закрывает и плагин это знает (повторная команда снова открывает), отключение во время рисования
    /// закрывает холст, команды и процесс. Индикатор: наблюдение за раскладкой идёт только пока плагин включён, пример показывает
    /// индикатор хоста, отключение скрывает его.
    /// </summary>
    private void RunDrawIndicatorPluginsSelfTest(Action<bool, string> check, List<string> notes, Func<Func<bool>, int, bool> wait)
    {
        var pm = _plugins!;
        var host = _drawHost!;
        foreach (var package in PluginMigration.BundledPackages())
        {
            var (m, _) = PluginStore.Inspect(package);
            bool draw = m?.Has(Backpack.Sdk.Capabilities.OverlayDraw) == true, indicator = m?.Has(Backpack.Sdk.Capabilities.OverlayIndicator) == true;
            if (m is null || !(draw || indicator))
            {
                continue;
            }

            string id = m.Id;
            if (pm.Store.Get(id) is { } installed && !(pm.Store.IsBundled(installed) && !installed.Trusted && !installed.Enabled))
            {
                notes.Add($"selftest-ui: {id} already installed — plugin path skipped");
                continue;
            }

            if (pm.Store.Get(id) is null)
            {
                pm.Import(package, BundledIndex.Sha256Of(package));
            }

            check(!pm.Running.ContainsKey(id) && !pm.Enable(id), $"{id}: in the catalog, off, not enabled without trust");
            pm.Store.Trust(id);
            check(pm.Enable(id) && wait(() => pm.Running.TryGetValue(id, out var p) && p.State == PluginRunState.Running, 8000), $"{id}: enabled and running");
            string Cmd(string c) => $"plugin.{id}.{c}";
            if (draw)
            {
                check(wait(() => _commands.Get(Cmd("toggle")) is not null, 2000), $"{id}: commands registered");
                check(_hotkeys!.StatusOf(Cmd("screenshot")) is HotkeyStatus.WaitingForMode, $"{id}: F2 not taken outside drawing");
                check(_commands.Execute(Cmd("toggle")) && wait(() => _presentation!.State.Drawing && host.Owner?.Plugin == id, 3000), $"{id}: command opens the host canvas");
                check(wait(() => _hotkeys.StatusOf(Cmd("screenshot")) is HotkeyStatus.Registered or HotkeyStatus.Busy, 2000), $"{id}: F2 active while drawing");
                _commands.Execute(Cmd("tool-arrow"));
                check(wait(() => _presentation!.Drawing?.Settings.Tool == DrawTool.Arrow, 2000), $"{id}: tool command reaches the canvas");
                _presentation!.Escape();
                check(wait(() => !_presentation.State.Drawing && host.Owner is null, 2000), $"{id}: Esc closes the canvas");
                check(wait(() => _hotkeys.StatusOf(Cmd("screenshot")) is HotkeyStatus.WaitingForMode, 2000), $"{id}: F2 released after drawing");
                check(_commands.Execute(Cmd("toggle")) && wait(() => _presentation.State.Drawing, 3000), $"{id}: plugin knows Esc closed it (next command opens)");
                int? pid = pm.Running.TryGetValue(id, out var running) ? running.ProcessId : null;
                pm.Disable(id);
                check(wait(() => !_presentation.State.Drawing, 2000) && !pm.Running.ContainsKey(id) && _commands.Get(Cmd("toggle")) is null, $"{id}: disable while drawing closes canvas and commands");
                check(pid is null || wait(() => !System.Diagnostics.Process.GetProcesses().Any(x => x.Id == pid), 3000), $"{id}: no process left");
            }
            else
            {
                check(wait(() => _indicator!.IsPolling, 3000), $"{id}: layout watching starts with the plugin");
                check(_commands.Execute(Cmd("example")) && wait(() => _indicator!.IsVisible, 3000), $"{id}: example shows the host indicator");
                pm.Disable(id);
                check(wait(() => !_indicator!.IsVisible && !_indicator.IsPolling, 3000), $"{id}: disable hides it and stops watching");
            }

            pm.Remove(id);
            RestoreBundled(id, package);
            check(pm.Store.Get(id) is { Enabled: false } back && pm.Store.IsBundled(back), $"{id}: added back, off, from the creator");
            notes.Add($"selftest-ui: plugin {id} {m.Version} ok");
        }
    }

    private static bool IsNear(Color c, ThemeColor t) => Math.Abs(c.R - t.R) < 40 && Math.Abs(c.G - t.G) < 40 && Math.Abs(c.B - t.B) < 40;

    /// <summary>Размер PNG и цвет двух точек (координаты окна в DIP). Изображение никуда не копируется.</summary>
    private static (int Width, int Height, Color A, Color B) SamplePng(string file, double scale, Point a, Point b)
    {
        BitmapSource frame;
        using (var fs = File.OpenRead(file))
        {
            frame = new FormatConvertedBitmap(new PngBitmapDecoder(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0], PixelFormats.Bgra32, null, 0);
        }

        Color At(Point pt)
        {
            int x = Math.Clamp((int)Math.Round(pt.X * scale), 0, frame.PixelWidth - 1), y = Math.Clamp((int)Math.Round(pt.Y * scale), 0, frame.PixelHeight - 1);
            var px = new byte[4];
            frame.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
            return Color.FromRgb(px[2], px[1], px[0]);
        }

        return (frame.PixelWidth, frame.PixelHeight, At(a), At(b));
    }

    private static void PumpFor(int ms)
    {
        var frame = new DispatcherFrame();
        var t = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            frame.Continue = false;
        };
        t.Start();
        Dispatcher.PushFrame(frame);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Без дампов и без записи содержимого: только факт и корректная остановка источников.
        _controller?.Report(FindingCode.UnhandledException, FindingLevel.Confirmed);
        _controller?.ShutdownNotes.Add($"unhandled: {e.Exception.GetType().Name} in {e.Exception.TargetSite?.DeclaringType?.Name}.{e.Exception.TargetSite?.Name}");
        e.Handled = true;
        _exiting = true;
        _tray?.Dispose();
        _tray = null;
        Shutdown(3);
    }
}

internal static partial class SelfCheck
{
    [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
    public static partial short KeyState(int vk);

    [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    public static partial nint ForegroundWindow();

    /// <summary>
    /// Снимок собственного окна через PrintWindow: только содержимое этого окна, без экрана и чужих окон.
    /// Для самопроверки (BACKPACK_SELFTEST_SHOTS=папка).
    /// </summary>
    public static void SaveWindow(Window w, string path)
    {
        nint hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        if (hwnd == 0 || !GetWindowRect(hwnd, out var r))
        {
            return;
        }

        int width = r.Right - r.Left, height = r.Bottom - r.Top;
        using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            nint hdc = g.GetHdc();
            try
            {
                PrintWindow(hwnd, hdc, 2); // PW_RENDERFULLCONTENT
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out Rect32 rect);

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool PrintWindow(nint hwnd, nint hdc, uint flags);
}
