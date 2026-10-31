using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyOverlay.Visual.Commands;
using KeyOverlay.Visual.Hotkeys;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Plugins;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Settings;

/// <summary>Phase 4: страницы «Фокус», «Плагины», «Команды и сочетания» и карточки мониторов, приватности, темы, увеличения у курсора.</summary>
public sealed partial class SettingsWindow
{
    private string? _recordingCommand;

    private void SetPrefs4(Func<AppPreferences, AppPreferences> f) => _prefs?.Update(f);

    private void SetTools(Func<ToolToggles, ToolToggles> f) => SetPrefs4(p => p with { Tools = f(p.ToolsOrDefault) });

    /// <summary>Переключатель «Инструмент включён» в начале страницы инструмента.</summary>
    private Border ToolCard(bool on, Action<bool> set, string? hint = null) =>
        _ui.Card(Loc.T("Tool.Enabled.Title"), hint ?? Loc.T("Tool.Enabled.Hint"), _ui.Row(Loc.T("Tool.Enabled"), null, _ui.Toggle(on, set)));

    private Button LinkButton(string text, Action click)
    {
        var b = _ui.Button(text, click);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        return b;
    }

    /// <summary>Карточки Phase 4 на существующих страницах.</summary>
    private void AddPhase4Cards(LabSettings s)
    {
        var p = _prefs?.Current;
        switch (Section)
        {
            case SettingsSection.Keys when p is not null:
                _cards.Children.Insert(0, ToolCard(p.ToolsOrDefault.Keycaps, v => SetTools(t => t with { Keycaps = v })));
                var links = new ButtonBar { Margin = new Thickness(0, 4, 0, 0) };
                links.Children.Add(_ui.Button(Loc.T("Keys.OpenStyle"), () => Show(SettingsSection.Appearance)));
                links.Children.Add(_ui.Button(Loc.T("Keys.OpenPosition"), () => Show(SettingsSection.Position)));
                _cards.Children.Insert(1, _ui.Card(Loc.T("Keys.More.Title"), null, links));
                break;
            case SettingsSection.Position:
                _cards.Children.Insert(0, MonitorCard(Loc.T("Monitors.Keycaps.Title"), s.DisplaysOrDefault.Keycaps, t => U(x => x with { Displays = x.DisplaysOrDefault with { Keycaps = t } })));
                break;
            case SettingsSection.Layout:
                var d = s.DisplaysOrDefault;
                var inherit = _ui.Row(Loc.T("Monitors.Indicator.Inherit"), Loc.T("Monitors.Indicator.InheritHint"),
                    _ui.Toggle(d.IndicatorInherits, v => U(x => x with { Displays = x.DisplaysOrDefault with { IndicatorInherits = v } })));
                _cards.Children.Add(_ui.Card(Loc.T("Monitors.Indicator.Title"), null, inherit));
                if (!d.IndicatorInherits)
                {
                    _cards.Children.Add(MonitorCard(Loc.T("Monitors.Indicator.Own"), d.Indicator, t => U(x => x with { Displays = x.DisplaysOrDefault with { Indicator = t } })));
                }

                break;
            case SettingsSection.Appearance:
                _cards.Children.Insert(Math.Min(3, _cards.Children.Count), KeycapFontCard(s));
                _cards.Children.Insert(Math.Min(4, _cards.Children.Count), SecondaryLabelsCard(s));
                break;
            case SettingsSection.Application when p is not null:
                // Подстраницы группы — в меню слева; здесь те же переходы кнопками (меню синхронизируется).
                var more = new ButtonBar { Margin = new Thickness(0, 4, 0, 0) };
                foreach (var (sec, icon) in new[] { (SettingsSection.Interface, "sparkles"), (SettingsSection.Privacy, "shield-check"), (SettingsSection.Hotkeys, "command") })
                {
                    var target = sec;
                    more.Children.Add(_ui.Button(icon, Loc.T(Sections.First(x => x.Section == target).TitleKey), () => Show(target)));
                }

                _cards.Children.Insert(0, _ui.Card(Loc.T("App.More.Title"), Loc.T("App.More.Hint"), more));
                break;
        }
    }

    // ── мониторы ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Выбор мониторов: основной, выбранный, несколько выбранных, «активный» — монитор под указателем мыши (не монитор
    /// активного окна). Список — текущие мониторы; сохранённый, но отключённый монитор показывается с пометкой.
    /// </summary>
    private Border MonitorCard(string title, MonitorTarget target, Action<MonitorTarget> set)
    {
        var monitors = _actions.Monitors?.Invoke() ?? [];
        var modes = new[] { MonitorMode.Primary, MonitorMode.Selected, MonitorMode.Multiple, MonitorMode.UnderCursor };
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Monitors.Mode"), Loc.T("Monitors.ModeHint"), _ui.Segmented(Array.IndexOf(modes, target.Mode),
                [Loc.T("Monitors.Primary"), Loc.T("Monitors.Selected"), Loc.T("Monitors.Multiple"), Loc.T("Monitors.UnderCursor")],
                i => set(new MonitorTarget(modes[i], modes[i] is MonitorMode.Selected or MonitorMode.Multiple && target.Monitors.Count == 0 && monitors.Count > 0
                    ? [new MonitorRef(monitors[0].Id, monitors[0].Name)] : target.Monitors)))),
        };
        if (target.Mode is MonitorMode.Selected or MonitorMode.Multiple)
        {
            foreach (var m in monitors)
            {
                bool on = target.Monitors.Any(r => r.Id == m.Id);
                var mon = m;
                rows.Add(_ui.Row(m.Describe(Loc.T("Monitors.PrimaryWord")), null, _ui.Toggle(on, v =>
                {
                    var list = target.Mode == MonitorMode.Selected ? new List<MonitorRef>() : target.Monitors.Where(r => r.Id != mon.Id).ToList();
                    if (v)
                    {
                        list.Add(new MonitorRef(mon.Id, mon.Name));
                    }

                    set(target with { Monitors = list });
                })));
            }

            foreach (var gone in target.Monitors.Where(r => !monitors.Any(m => m.Id == r.Id)))
            {
                rows.Add(_ui.Note(Loc.F("Monitors.Missing", gone.Name)));
            }
        }

        rows.Add(_ui.Note(Loc.T("Monitors.Note")));
        return _ui.Card(title, Loc.T("Monitors.Hint"), rows.ToArray());
    }

    // ── приватность ──────────────────────────────────────────────────────────

    /// <summary>Приватность — три логические карточки: включение и состояние, список приложений, ограничения.</summary>
    private IEnumerable<Border> PrivacyCards(PrivacySettings pr)
    {
        void Set(Func<PrivacySettings, PrivacySettings> f) => SetPrefs4(x => x with { Privacy = f(x.PrivacyOrDefault) });
        var rows = new List<UIElement>
        {
            _ui.Row(Loc.T("Privacy.Enable"), Loc.T("Privacy.EnableHint"), _ui.Toggle(pr.Enabled, v => Set(x => x with { Enabled = v }))),
            _ui.Row(Loc.T("Privacy.Badge"), Loc.T("Privacy.BadgeHint"), _ui.Toggle(pr.ShowBadge, v => Set(x => x with { ShowBadge = v }))),
        };
        if (_actions.PrivacyStatus?.Invoke() is { } status)
        {
            rows.Add(_ui.Row(Loc.T("Privacy.Status"), null, new TextBlock { Text = status, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 280 }));
        }

        yield return _ui.Card(Loc.T("Privacy.Title"), Loc.T("Privacy.Hint"), rows.ToArray());
        rows = new List<UIElement>();
        foreach (var app in pr.Apps)
        {
            string a = app;
            rows.Add(_ui.Row(a, null, _ui.Action(ActionIcons.Remove, Loc.T("Privacy.RemoveApp") + ": " + a, () => Set(x => x with { BlockedApps = x.Apps.Where(y => y != a).ToList() }))));
        }

        var input = new TextBox { Width = 220, Style = UiTheme.Resources.StyleOf("Ko.TextBox") };
        System.Windows.Automation.AutomationProperties.SetName(input, Loc.T("Privacy.AddApp"));
        var add = new WrapPanel { Orientation = Orientation.Horizontal };
        input.Margin = new Thickness(0, 0, 8, 4);
        add.Children.Add(input);
        var addButton = _ui.Button(Loc.T("Privacy.AddAppButton"), () =>
        {
            if (PrivacySettings.NormalizeApp(input.Text) is { } name && !pr.Apps.Contains(name))
            {
                Set(x => x with { BlockedApps = x.Apps.Append(name).ToList() });
            }
        });
        addButton.Margin = new Thickness(0, 0, 0, 4);
        add.Children.Add(addButton);
        rows.Add(_ui.Row(Loc.T("Privacy.AddApp"), Loc.T("Privacy.AddAppHint"), add));
        yield return _ui.Card(Loc.T("Privacy.Apps.Title"), null, rows.ToArray());
        yield return _ui.Card(Loc.T("Privacy.Limits.Title"), null, _ui.Note(Loc.T("Privacy.Limits")), _ui.Note(Loc.T("Privacy.ManualHide")));
    }

    // ── команды и сочетания ──────────────────────────────────────────────────

    /// <summary>
    /// Все команды реестра (встроенные и плагинов) по категориям. Глобальные — Ctrl/Alt/Win обязательны; «только в
    /// инструменте» — действуют, пока инструмент активен (допустимы F2–F24); локальные (F1) не переназначаются.
    /// Сочетания по умолчанию у новых команд нет.
    /// </summary>
    private void BuildCommands()
    {
        if (_actions.Commands is not { } reg || _prefs is null)
        {
            return;
        }

        if (_prefs.LegacyHotkeysMigrated)
        {
            _cards.Children.Add(_ui.Card(Loc.T("Commands.Migrated.Title"), null, _ui.Note(Loc.T("Commands.Migrated"))));
        }

        foreach (var group in reg.All.GroupBy(d => d.CategoryKey))
        {
            var rows = new List<UIElement>();
            foreach (var d in group)
            {
                string scope = Loc.T("Commands.Scope." + d.Scope);
                string status = d.Assignable ? StatusText(d.Id) : Loc.T("Commands.LocalOnly");
                rows.Add(_ui.Row(d.Title, scope + " · " + status, d.Assignable ? CommandEditor(d) : new TextBlock { Text = d.Id == "app.help" ? "F1" : "", VerticalAlignment = VerticalAlignment.Center }));
            }

            _cards.Children.Add(_ui.Card(Loc.T(group.Key), null, rows.ToArray()));
        }

        _cards.Children.Add(_ui.Card(Loc.T("Commands.Rules.Title"), null, _ui.Note(Loc.T("Commands.Rules"))));
    }

    private string StatusText(string id)
    {
        if (_recordingCommand == id)
        {
            return Loc.T("Hotkeys.Recording");
        }

        var st = _hotkeys?.StatusOf(id) ?? HotkeyStatus.NotSet;
        return Loc.T("Hotkeys.Status." + st);
    }

    private FrameworkElement CommandEditor(CommandDef d)
    {
        var reg = _actions.Commands!;
        var current = reg.HotkeyOf(d.Id, _prefs!.Current.BindingsOrEmpty);
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        var box = new TextBox
        {
            Text = _recordingCommand == d.Id ? Loc.T("Hotkeys.PressKeys") : current?.ToString() ?? Loc.T("Hotkeys.NotSet"),
            IsReadOnly = true,
            Width = 190,
            Style = UiTheme.Resources.StyleOf("Ko.TextBox"),
            Tag = d.Id,
        };
        System.Windows.Automation.AutomationProperties.SetName(box, d.Title);
        box.GotKeyboardFocus += (_, _) =>
        {
            _recordingCommand = d.Id;
            _hotkeys?.Suspend();
            box.Text = Loc.T("Hotkeys.PressKeys");
        };
        box.LostKeyboardFocus += (_, _) =>
        {
            if (_recordingCommand == d.Id)
            {
                EndCommandRecording();
                Dispatcher.BeginInvoke(BuildSection);
            }
        };
        box.PreviewKeyDown += (_, e) => OnCommandRecorderKey(d, e);
        panel.Children.Add(box);
        box.Margin = new Thickness(0, 0, 8, 4);
        var clear = _ui.Action(ActionIcons.Clear, Loc.T("Hotkeys.Clear"), () => SetBinding(d.Id, ""));
        clear.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(clear);
        return panel;
    }

    private void EndCommandRecording()
    {
        if (_recordingCommand is null)
        {
            return;
        }

        _recordingCommand = null;
        if (_hotkeys is { IsSuspended: true })
        {
            _hotkeys.Resume();
        }
    }

    /// <summary>Запись сочетания команды (локально в окне). Esc — отмена, Backspace — снять.</summary>
    internal void OnCommandRecorderKey(CommandDef d, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            EndCommandRecording();
            Keyboard.ClearFocus();
            BuildSection();
            return;
        }

        if (key == Key.Back && Keyboard.Modifiers == ModifierKeys.None)
        {
            EndCommandRecording();
            SetBinding(d.Id, "");
            return;
        }

        if (Hotkey.FromKey(key, Keyboard.Modifiers) is not { } h)
        {
            return;
        }

        bool toolKey = d.Scope == CommandScope.Tool && h.Vk is >= 0x71 and <= 0x87 && (h.Modifiers & HotkeyModifiers.Win) == 0;
        var reg = _actions.Commands!;
        var binds = _prefs!.Current.BindingsOrEmpty;
        bool conflict = reg.All.Any(o => o.Id != d.Id && reg.HotkeyOf(o.Id, binds) == h);
        string? problem = !(HotkeyRules.IsAcceptable(h) || toolKey) ? Loc.T("Hotkeys.Status.Invalid")
            : HotkeyRules.IsReserved(h) ? Loc.T("Hotkeys.Status.Reserved")
            : conflict ? Loc.T("Hotkeys.Status.Conflict")
            : null;
        if (problem is not null)
        {
            if (e.OriginalSource is TextBox box)
            {
                box.Text = h + " — " + problem;
            }

            return;
        }

        EndCommandRecording();
        SetBinding(d.Id, h.ToString());
    }

    private void SetBinding(string id, string hotkey)
    {
        SetPrefs4(p => p with { Bindings = p.BindingsOrEmpty.With(id, hotkey) });
        Keyboard.ClearFocus();
        BuildSection();
    }

    // ── плагины ──────────────────────────────────────────────────────────────

    private void BuildPlugins()
    {
        if (_actions.Plugins is not { } pm)
        {
            return;
        }

        if (_pluginPage is { } id && pm.Store.Get(id) is { } focused)
        {
            BuildPluginDetail(pm, focused);
            return;
        }

        _pluginPage = null;
        var p = _prefs?.Current;
        var builtIn = new List<UIElement>();
        foreach (var (section, on) in new[]
        {
            (SettingsSection.Keys, p?.ToolsOrDefault.Keycaps ?? true), (SettingsSection.Mouse, _store.Current.MouseCursor.Enabled),
        })
        {
            var info = Sections.First(x => x.Section == section);
            var sec = section;
            builtIn.Add(_ui.Row(Loc.T(info.TitleKey), on ? Loc.T("Plugins.On") : Loc.T("Plugins.Off"), _ui.Button(Loc.T("Common.Open"), () => Show(sec))));
        }

        _cards.Children.Add(_ui.Card(Loc.T("Plugins.BuiltIn.Title"), Loc.T("Plugins.BuiltIn.Hint"), builtIn.ToArray()));
        AddBundledCard(pm);

        // Установленные плагины: карточки (по умолчанию) или список; выбор запоминается (app.json, pluginsView).
        bool asList = _prefs?.Current.PluginsAsList ?? false;
        var head = new List<UIElement> { _ui.Note(Loc.T("Plugins.TrustNote")) };
        if (pm.SafeMode)
        {
            head.Add(_ui.Note(Loc.T("Plugins.SafeMode")));
        }

        var bar = new ButtonBar { Margin = new Thickness(0, 8, 0, 4) };
        bar.Children.Add(_ui.Button(ActionIcons.Import, Loc.T("Plugins.Import"), () => ImportPlugin(pm), primary: true));
        head.Add(bar);
        if (_prefs is not null)
        {
            head.Add(_ui.Row(Loc.T("Plugins.View"), null, _ui.Segmented(asList ? 1 : 0, [Loc.T("Plugins.View.Cards"), Loc.T("Plugins.View.List")], i =>
            {
                _prefs.Update(x => x with { PluginsAsList = i == 1 });
                Dispatcher.BeginInvoke(BuildSection);
            })));
        }

        _cards.Children.Add(_ui.Card(Loc.T("Plugins.External.Title"), Loc.T("Plugins.External.Hint"), head.ToArray()));
        var installed = pm.Store.List();
        if (installed.Count == 0)
        {
            _cards.Children.Add(_ui.Card(Loc.T("Plugins.None.Title"), null, _ui.Note(Loc.T("Plugins.None"))));
            return;
        }

        if (asList)
        {
            var rows = new List<UIElement>();
            foreach (var pl in installed)
            {
                rows.Add(_ui.Row($"{pl.Manifest.Name.For(Loc.Code)} {pl.Manifest.Version}", $"{pl.Manifest.Author} · {PluginStateText(pm, pl)}", PluginActions(pm, pl)));
            }

            _cards.Children.Add(_ui.Card(Loc.T("Plugins.Installed.Title"), null, rows.ToArray()));
            return;
        }

        var grid = new AdaptiveGrid { MinItemWidth = 280, MaxColumns = 3, Gap = 12, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var pl in installed)
        {
            grid.Children.Add(PluginCard(pm, pl));
        }

        _cards.Children.Add(grid);
    }

    /// <summary>Состояние плагина для карточки и строки списка: включён, выключен или ошибка (отключён после сбоев, процесс упал).</summary>
    private static (string Text, bool Error) PluginState(PluginManager pm, InstalledPlugin pl) =>
        pl.DisabledReason == "crashes" ? (Loc.T("Plugins.State.Crashed"), true)
        : pl.Enabled && pm.Running.TryGetValue(pl.Id, out var proc) && proc.State == PluginRunState.Failed ? (Loc.T("Plugins.State.Failed"), true)
        : pl.Enabled ? (Loc.T("Plugins.On"), false)
        : (Loc.T("Plugins.Off"), false);

    private static string PluginStateText(PluginManager pm, InstalledPlugin pl) => PluginState(pm, pl).Text;

    /// <summary>Основные действия плагина (карточка и список): в меню, подробнее, включить/выключить, удалить.</summary>
    private ButtonBar PluginActions(PluginManager pm, InstalledPlugin pl, bool compact = false)
    {
        var actions = new ButtonBar { HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right, MaxWidth = 560 };
        var star = _ui.Toggle(pl.Favorite, v =>
        {
            pm.Store.SetFavorite(pl.Id, v);
            Rebuild();
        });
        star.ToolTip = Loc.T("Plugins.FavoriteHint");
        System.Windows.Automation.AutomationProperties.SetName(star, Loc.T("Plugins.Favorite") + ": " + pl.Manifest.Name.For(Loc.Code));
        actions.Children.Add(WithText(Loc.T("Plugins.Favorite"), star));
        // Phase 4H: подробнее, выключение и удаление — значки с подписями в подсказке; удаление отделено. Включение остаётся
        // текстовой кнопкой: оно ведёт к подтверждению доверия, и последствие должно быть понятно без наведения.
        var icons = _ui.Toolbar(PluginActionGroups(pm, pl, compact));
        icons.Margin = new Thickness(0, 4, 0, 0);
        actions.Children.Add(icons);
        return actions;
    }

    /// <summary>Группы действий плагина: [включить текстом | выключить значком, подробнее], [обновить], [удалить].</summary>
    private IReadOnlyList<UIElement>[] PluginActionGroups(PluginManager pm, InstalledPlugin pl, bool compact, bool details = true, bool update = false)
    {
        var main = new List<UIElement>();
        if (pl.Enabled)
        {
            main.Add(_ui.Action(ActionIcons.Disable, Loc.T("Plugins.Disable"), () => { pm.Disable(pl.Id); BuildSection(); }));
        }
        else
        {
            var enable = _ui.Button(Loc.T("Plugins.Enable"), () => EnablePlugin(pm, pl), primary: !compact);
            enable.Margin = new Thickness(0, 0, 8, 4);
            main.Add(enable);
        }

        if (details)
        {
            main.Add(_ui.Action(ActionIcons.Details, Loc.T("Plugins.Details"), () => ShowPlugin(pl.Id)));
        }

        var upd = update ? new List<UIElement> { _ui.Action(ActionIcons.Update, Loc.T("Plugins.Update"), () => ImportPlugin(pm)) } : [];
        return [main, upd, [_ui.Action(ActionIcons.Delete, Loc.T("Plugins.Remove"), () => RemovePlugin(pm, pl), danger: true)]];
    }

    /// <summary>
    /// Карточка плагина: иконка (или аккуратная замена — первая буква на плитке), название, версия и автор, краткое описание
    /// (до четырёх строк), состояние и действия. Просмотр карточки читает только манифест и картинку: код плагина не запускается;
    /// первое включение проходит то же подтверждение доверия (<see cref="EnablePlugin"/>).
    /// </summary>
    private Border PluginCard(PluginManager pm, InstalledPlugin pl)
    {
        var m = pl.Manifest;
        string name = m.Name.For(Loc.Code);
        var p = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var icon = PluginIcon(pl, 44);
        icon.Margin = new Thickness(0, 0, 12, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(icon, Dock.Left);
        head.Children.Add(icon);
        var titles = new StackPanel();
        titles.Children.Add(new TextBlock { Text = name, Style = UiTheme.Resources.StyleOf("Ko.H2"), Margin = new Thickness(0), TextWrapping = TextWrapping.Wrap, MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name });
        titles.Children.Add(new TextBlock { Text = Loc.F("Plugins.VersionAuthor", m.Version, m.Author), Style = UiTheme.Resources.StyleOf("Ko.Hint"), TextWrapping = TextWrapping.Wrap });
        if (pm.Store.IsBundled(pl))
        {
            titles.Children.Add(CreatorMark());
        }

        head.Children.Add(titles);
        p.Children.Add(head);
        if (m.Description is { } desc)
        {
            string d = desc.For(Loc.Code);
            p.Children.Add(new TextBlock { Text = d, Style = UiTheme.Resources.StyleOf("Ko.Hint"), TextWrapping = TextWrapping.Wrap, MaxHeight = 76, TextTrimming = TextTrimming.WordEllipsis, ToolTip = d, Margin = new Thickness(0, 0, 0, 8) });
        }

        var (state, error) = PluginState(pm, pl);
        var chip = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 3), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
        chip.SetResourceReference(Border.BackgroundProperty, error ? "Ko.HoverBrush" : pl.Enabled ? "Ko.SelectedBrush" : "Ko.SegmentBackBrush");
        var chipText = new TextBlock { Text = state.Length > 0 ? char.ToUpper(state[0], Loc.Culture) + state[1..] : state, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        chipText.SetResourceReference(TextBlock.ForegroundProperty, error ? "Ko.ErrorBrush" : "Ko.TextBrush");
        chip.Child = chipText;
        System.Windows.Automation.AutomationProperties.SetName(chip, state);
        p.Children.Add(chip);
        p.Children.Add(PluginActions(pm, pl, compact: true));
        var card = new Border { Style = UiTheme.Resources.StyleOf("Ko.Card"), Child = p, Margin = new Thickness(0) };
        System.Windows.Automation.AutomationProperties.SetName(card, name);
        return card;
    }

    /// <summary>
    /// Отметка «От создателя» (Phase 4H): установленная версия поставлена вместе с программой — пакет совпал по SHA-256 с
    /// индексом поставки. Ставится по метаданным поставки, а не по имени автора в manifest; это не подпись издателя (подсказка).
    /// </summary>
    private FrameworkElement CreatorMark()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0), ToolTip = Loc.T("Plugins.Creator.Hint") };
        var icon = Icons.Lucide.Icon("badge-check", 15, "Ko.LogoBrush");
        icon.Margin = new Thickness(0, 0, 5, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);
        var text = new TextBlock { Text = Loc.T("Plugins.Creator"), FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Ko.LogoBrush");
        row.Children.Add(text);
        System.Windows.Automation.AutomationProperties.SetName(row, Loc.T("Plugins.Creator") + ". " + Loc.T("Plugins.Creator.Hint"));
        ToolTipService.SetShowsToolTipOnKeyboardFocus(row, true);
        return row;
    }

    /// <summary>
    /// Значок плагина на карточке (Phase 4I): тот же, что в меню и шапке страницы, — выбранный пользователем или значок автора
    /// (manifest «symbol») на плитке. Картинка PNG из пакета (Phase 4F, не больше 256 КБ) показывается только у плагинов без
    /// значка; без того и другого — «puzzle».
    /// </summary>
    internal static FrameworkElement PluginIcon(InstalledPlugin pl, double size)
    {
        bool hasSymbol = PluginSymbols.IsChoosable(pl.SymbolOverride) || PluginSymbols.IsChoosable(pl.Manifest.Symbol);
        if (!hasSymbol && pl.Manifest.Icon is { } rel)
        {
            try
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(pl.Directory, rel));
                string root = System.IO.Path.GetFullPath(pl.Directory).TrimEnd('\\') + "\\";
                var info = new System.IO.FileInfo(path);
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && info.Exists && info.Length is > 8 and <= 256 * 1024)
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = (int)Math.Ceiling(size * 2);
                        bmp.StreamSource = new System.IO.MemoryStream(bytes);
                        bmp.EndInit();
                        bmp.Freeze();
                        return new Border
                        {
                            Width = size,
                            Height = size,
                            CornerRadius = new CornerRadius(10),
                            Background = new System.Windows.Media.ImageBrush(bmp) { Stretch = System.Windows.Media.Stretch.Uniform },
                            Tag = "icon",
                        };
                    }
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException or System.IO.FileFormatException)
            {
                // повреждённая или недоступная картинка — замена ниже
            }
        }

        var glyph = Icons.Lucide.Icon(pl.Symbol, Math.Round(size * 0.5), "Ko.LogoBrush");
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var tile = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(10), Child = glyph, Tag = hasSymbol ? "symbol" : "fallback" };
        tile.SetResourceReference(Border.BackgroundProperty, "Ko.SelectedBrush");
        return tile;
    }

    private void BuildPluginDetail(PluginManager pm, InstalledPlugin pl)
    {
        var m = pl.Manifest;
        var back = LinkButton("← " + Loc.T("Settings.Nav.Plugins"), () =>
        {
            _pluginPage = null;
            Show(SettingsSection.Plugins);
        });
        var info = new List<UIElement>
        {
            back,
            _ui.Row(Loc.T("Plugins.Id"), null, Value(m.Id)),
            _ui.Row(Loc.T("Plugins.Author"), null, pm.Store.IsBundled(pl) ? WithText(m.Author, CreatorMark()) : Value(m.Author)),
            _ui.Row(Loc.T("Plugins.Version"), null, Value($"{m.Version} · SDK {m.Sdk}")),
            _ui.Row(Loc.T("Plugins.License"), null, Value(m.License)),
            _ui.Row(Loc.T("Plugins.Capabilities"), null, Value(string.Join(", ", m.Capabilities.Select(c => Loc.T("Plugins.Cap." + c))))),
        };
        if (m.Description is { } desc)
        {
            info.Insert(1, _ui.Note(desc.For(Loc.Code)));
        }

        info.Add(_ui.Toolbar(PluginActionGroups(pm, pl, compact: false, details: false, update: true)));
        info.Add(_ui.Note(Loc.T("Plugins.TrustNote")));
        _cards.Children.Add(_ui.Card(m.Name.For(Loc.Code), null, info.ToArray()));

        AddPluginDisplay(pm, pl);
        AddPluginSettings(pm, m);
        AddPluginCommands(pm, m);
    }

    /// <summary>
    /// Отображение плагина (Phase 4I): значок в меню, на карточке и в шапке. Выбор — из каталога <see cref="PluginSymbols"/> с
    /// поиском; плитки показывают значок так же, как в меню. Выбор сохраняется по ID плагина и сразу применяется везде;
    /// «Значок автора» возвращает значок из manifest. Код плагина для показа значка не запускается.
    /// </summary>
    private void AddPluginDisplay(PluginManager pm, InstalledPlugin pl)
    {
        string current = pl.Symbol;
        var preview = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (double size in new[] { 16.0, 20.0, 32.0 })
        {
            var g = Icons.Lucide.Icon(current, size, "Ko.TextBrush");
            g.Margin = new Thickness(0, 0, 14, 0);
            g.VerticalAlignment = VerticalAlignment.Center;
            preview.Children.Add(g);
        }

        var picker = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0), Tag = "symbol-picker" };
        var search = new TextBox { Style = UiTheme.Resources.StyleOf("Ko.TextBox"), MaxLength = 40, Margin = new Thickness(0, 0, 0, 8) };
        System.Windows.Automation.AutomationProperties.SetName(search, Loc.T("Plugins.Symbol.Search"));
        var grid = new WrapPanel { Tag = "symbol-grid" };
        var empty = new TextBlock { Text = Loc.T("Plugins.Symbol.NoMatch"), Style = UiTheme.Resources.StyleOf("Ko.Hint"), Visibility = Visibility.Collapsed };
        var scroll = new ScrollViewer { Content = grid, MaxHeight = 236, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };

        void Choose(string name)
        {
            pm.Store.SetSymbol(pl.Id, name == pl.Manifest.Symbol ? null : name);
            Rebuild(); // меню, карточка и шапка — одним перестроением
        }

        void Fill(string query)
        {
            grid.Children.Clear();
            foreach (var name in PluginSymbols.Search(query))
            {
                var tile = new RadioButton
                {
                    GroupName = "plugin-symbol-" + pl.Id,
                    IsChecked = name == current,
                    Width = 44,
                    Height = 44,
                    Margin = new Thickness(0, 0, 6, 6),
                    Content = Icons.Lucide.Icon(name, 20, "Ko.TextBrush"),
                    ToolTip = name,
                    Style = UiTheme.Resources.StyleOf("Ko.IconTile"),
                    Tag = name,
                };
                System.Windows.Automation.AutomationProperties.SetName(tile, name);
                tile.Checked += (_, _) =>
                {
                    if (name != current)
                    {
                        Choose(name);
                    }
                };
                grid.Children.Add(tile);
            }

            empty.Visibility = grid.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        search.TextChanged += (_, _) => Fill(search.Text);
        picker.Children.Add(search);
        picker.Children.Add(scroll);
        picker.Children.Add(empty);

        var change = _ui.Action("search", Loc.T("Plugins.Symbol.Change"), () =>
        {
            bool open = picker.Visibility != Visibility.Visible;
            picker.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            if (open)
            {
                Fill(search.Text);
                search.Focus();
            }
        });
        var reset = _ui.Action(ActionIcons.ResetSection, Loc.T("Plugins.Symbol.Reset"), () => Choose(pl.Manifest.Symbol ?? PluginSymbols.Fallback),
            enabled: pl.SymbolOverride is not null, disabledReason: Loc.T("Plugins.Symbol.IsAuthor"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        change.Margin = new Thickness(0, 0, 6, 0);
        buttons.Children.Add(change);
        buttons.Children.Add(reset);
        var row = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(preview);
        _cards.Children.Add(_ui.Card(Loc.T("Plugins.Display.Title"), null,
            _ui.Row(Loc.T("Plugins.Symbol"), Loc.T(pl.SymbolOverride is null ? "Plugins.Symbol.HintAuthor" : "Plugins.Symbol.HintUser"), row),
            picker));
    }

    private TextBox PluginText(string value, Action<string> set)
    {
        var box = new TextBox { Text = value, Width = 240, MaxLength = 200, Style = UiTheme.Resources.StyleOf("Ko.TextBox") };
        box.LostKeyboardFocus += (_, _) => set(box.Text);
        return box;
    }

    /// <summary>Включение: перед первым включением — явное предупреждение о доверии (плагин — программа с правами пользователя).</summary>
    private void EnablePlugin(PluginManager pm, InstalledPlugin pl)
    {
        if (!pl.Trusted)
        {
            var (choice, _) = KoDialog.Ask(this, Loc.T("Plugins.Trust.Title"), Loc.F("Plugins.Trust.Message", pl.Manifest.Name.For(Loc.Code), pl.Manifest.Author),
                [new("trust", Loc.T("Plugins.Trust.Yes")), new("cancel", Loc.T("Dialog.Cancel"), true)]);
            if (choice != "trust")
            {
                return;
            }

            pm.Store.Trust(pl.Id);
        }

        pm.Enable(pl.Id);
        BuildSection();
    }

    private void RemovePlugin(PluginManager pm, InstalledPlugin pl)
    {
        var (choice, _) = KoDialog.Ask(this, Loc.T("Plugins.Remove.Title"), Loc.F("Plugins.Remove.Message", pl.Manifest.Name.For(Loc.Code)),
            [new("remove", Loc.T("Plugins.Remove")), new("cancel", Loc.T("Dialog.Cancel"), true)]);
        if (choice == "remove")
        {
            pm.Remove(pl.Id);
            _pluginPage = null;
            Rebuild();
        }
    }

    private void ImportPlugin(PluginManager pm)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Loc.T("Plugins.FileFilter") + "|*" + PluginStore.PackageExtension, Title = Loc.T("Plugins.Import") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var r = pm.Import(dialog.FileName);
        string msg = r.Outcome switch
        {
            ImportOutcome.Installed => Loc.F("Plugins.Imported", r.Manifest!.Name.For(Loc.Code)),
            ImportOutcome.Updated => Loc.F("Plugins.Updated", r.Manifest!.Name.For(Loc.Code), r.PreviousVersion ?? "?", r.Manifest.Version),
            _ => Loc.T("Plugins.ImportFailed") + "\n\n" + string.Join("\n", r.Errors.Take(8)),
        };
        KoDialog.Ask(this, Loc.T("Plugins.Import"), msg, [new("ok", Loc.T("Dialog.Ok"), true)]);
        BuildSection();
    }
}
