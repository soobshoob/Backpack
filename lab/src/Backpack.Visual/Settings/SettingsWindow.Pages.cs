using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Backpack.Visual.Brand;
using Backpack.Visual.Help;
using Backpack.Visual.Hotkeys;
using Backpack.Visual.Layout;
using Backpack.Visual.Localization;
using Backpack.Visual.Mouse;
using Backpack.Visual.Rendering;
using Backpack.Visual.Styles;
using Backpack.Visual.Updates;

namespace Backpack.Visual.Settings;

/// <summary>Страницы главного окна Phase 2E: главная, каталоги стилей, горячие клавиши, справка, о программе.</summary>
public sealed partial class SettingsWindow
{
    private UpdateCheckResult? _lastUpdate;
    private bool _checking;

    /// <summary>Команда, сочетание которой сейчас записывается (для тестов).</summary>
    public string? RecordingCommand => _recordingCommand;

    /// <summary>Текст действующего сочетания команды (или «не назначено»).</summary>
    private string CommandHotkeyText(string id) =>
        _actions.Commands is { } reg && _prefs is not null && reg.HotkeyOf(id, _prefs.Current.BindingsOrEmpty) is { } h ? h.ToString() : Loc.T("Hotkeys.NotSet");

    // ── главная ──────────────────────────────────────────────────────────────

    private HomeDemo? _homeDemo;

    /// <summary>
    /// «Главная»: приветствие, живая демонстрация из настоящих элементов (кейкапы, мышь, рисование, линза) с паузой, карточки
    /// основных возможностей с быстрыми действиями, состояние отображения и профиля. Технических счётчиков здесь нет.
    /// </summary>
    private void BuildHome(LabSettings s)
    {
        bool animate = (_prefs?.Current.HomeAnimation ?? true) && !(_prefs?.Current.ReducedMotion ?? false) && SystemParameters.ClientAreaAnimation;
        _homeDemo = new HomeDemo(s, animate);
        // Приветствие и демонстрация — рядом в широком окне, одно под другим в узком.
        var hero = new AdaptiveGrid { MinItemWidth = 300, MaxColumns = 2, Gap = 20 };
        var intro = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var heroLogo = LogoArt.Themed(40);
        heroLogo.Margin = new Thickness(0, 0, 12, 0);
        title.Children.Add(heroLogo);
        var appName = Wordmark.Create(30, FontWeights.Bold); // фирменный знак «Backpack.» с точкой
        title.Children.Add(appName);
        intro.Children.Add(title);
        intro.Children.Add(new TextBlock { Text = Loc.T("Home.Welcome"), TextWrapping = TextWrapping.Wrap, Style = UiTheme.Resources.StyleOf("Ko.Label") });
        intro.Children.Add(new TextBlock { Text = Loc.T("Home.WelcomeHint"), TextWrapping = TextWrapping.Wrap, Style = UiTheme.Resources.StyleOf("Ko.Hint"), Margin = new Thickness(0, 6, 0, 10) });
        // Phase 4H: пауза и запуск демонстрации — значок; подсказка и доступное имя называют действие и меняются вместе с ним.
        var play = _ui.Action(animate ? ActionIcons.Pause : ActionIcons.Play, animate ? Loc.T("Home.Pause") : Loc.T("Home.Play"), () => { });
        play.Click += (_, _) =>
        {
            bool now = !_homeDemo!.IsPlaying;
            _homeDemo.SetPlaying(now);
            string text = now ? Loc.T("Home.Pause") : Loc.T("Home.Play");
            play.Content = Icons.Lucide.Icon(now ? ActionIcons.Pause : ActionIcons.Play, 18, "Ko.TextBrush");
            play.ToolTip = text;
            System.Windows.Automation.AutomationProperties.SetName(play, text);
        };
        play.HorizontalAlignment = HorizontalAlignment.Left;
        intro.Children.Add(play);
        hero.Children.Add(intro);
        hero.Children.Add(_homeDemo);
        var heroCard = new Border { Style = UiTheme.Resources.StyleOf("Ko.Card"), Child = hero };
        _cards.Children.Add(heroCard);

        // Карточки возможностей с быстрыми действиями.
        var features = new AdaptiveGrid { MinItemWidth = 260, MaxColumns = 2, Gap = 12, Margin = new Thickness(0, 0, 0, 12) };
        features.Children.Add(Feature("keyboard", Loc.T("Home.F.Keys"), Loc.T("Home.F.KeysHint"),
            (_ui.Button(Loc.T("Home.F.Style"), () => Show(SettingsSection.Appearance)), _ui.Button(Loc.T("Home.F.Settings"), () => Show(SettingsSection.Keys)))));
        features.Children.Add(Feature("mouse", Loc.T("Home.F.Mouse"), Loc.T("Home.F.MouseHint"),
            (_ui.Button(s.MouseCursor.Enabled ? Loc.T("Home.F.MouseOff") : Loc.T("Home.F.MouseOn"), () => { Mc(x => x with { Enabled = !x.Enabled }); BuildSection(); }), _ui.Button(Loc.T("Home.F.Settings"), () => Show(SettingsSection.Mouse)))));
        // Рисование, индикатор раскладки, лупа, линза, фокус и таймер — плагины (Phase 4G/4H): с «Главной» — переход к ним.
        features.Children.Add(Feature("puzzle", Loc.T("Home.F.Tools"), Loc.T("Home.F.ToolsHint"),
            (_ui.Button(Loc.T("Home.F.Open"), () => Show(SettingsSection.Plugins), primary: true), null)));
        _cards.Children.Add(features);

        var state = new List<UIElement>();
        if (_actions.DisplayVisible is { } visible && _actions.SetDisplayVisible is { } setVisible)
        {
            string hk = CommandHotkeyText("display.toggle");
            state.Add(_ui.Row(Loc.T("Home.Display"), Loc.F("Home.DisplayHint", hk), _ui.Toggle(visible(), v =>
            {
                setVisible(v);
                BuildSection();
            })));
        }

        if (_profiles is not null)
        {
            var go = _ui.Button(Loc.T("Home.OpenProfiles"), () => Show(SettingsSection.Profiles));
            state.Add(_ui.Row(Loc.T("Profiles.Active"), Loc.T("Profiles.AutosaveHint"), WithText(_profiles.ActiveName, go)));
        }

        state.Add(_ui.Row(Loc.T("Keys.Filter.Combos"), Loc.T("Home.CombosHint"), _ui.Toggle(s.Keys.Filter == KeyFilter.CombosOnly, v => Ks(x => x with { Filter = v ? KeyFilter.CombosOnly : KeyFilter.All }))));
        _cards.Children.Add(_ui.Card(Loc.T("Home.State.Title"), Loc.T("Home.State.Hint"), state.ToArray()));
    }

    /// <summary>Карточка инструмента на «Главной»: иконка Lucide, понятное описание, действие и переход к настройкам.</summary>
    private Border Feature(string glyph, string title, string hint, (Button? Primary, Button? Secondary) actions)
    {
        var p = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var icon = Icons.Lucide.Icon(glyph, 22, "Ko.GlowBrush");
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(icon);
        head.Children.Add(new TextBlock { Text = title, Style = UiTheme.Resources.StyleOf("Ko.H2"), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(head);
        p.Children.Add(new TextBlock { Text = hint, Style = UiTheme.Resources.StyleOf("Ko.Hint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        var bar = new ButtonBar();
        foreach (var b in new[] { actions.Primary, actions.Secondary })
        {
            if (b is not null)
            {
                bar.Children.Add(b);
            }
        }

        p.Children.Add(bar);
        return new Border { Style = UiTheme.Resources.StyleOf("Ko.Card"), Child = p, Margin = new Thickness(0) };
    }

    private static FrameworkElement WithText(string text, FrameworkElement control)
    {
        var p = new WrapPanel { Orientation = Orientation.Horizontal };
        p.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 4), MaxWidth = 260, TextWrapping = TextWrapping.Wrap });
        p.Children.Add(control);
        return p;
    }

    // ── каталог стилей ───────────────────────────────────────────────────────

    /// <summary>
    /// Карточка каталога: миниатюры встроенных пресетов и пользовательских стилей, выбранное состояние и отметка «изменён»,
    /// операции. Встроенные неизменяемы: «Сохранить» для них создаёт пользовательскую копию.
    /// </summary>
    private Border StyleCatalogCard(StyleKind kind, LabSettings s, string titleKey, string hintKey)
    {
        var rows = new List<UIElement>();
        var catalog = _styles?.Catalog(kind) ?? BuiltInStyles.All.Where(e => e.Kind == kind).ToList();
        string? selectedId = StyleOps.SelectedId(s, kind);
        var selected = catalog.FirstOrDefault(e => e.Id == selectedId);
        bool modified = selected is not null && !StyleOps.Matches(s, selected);

        // Колонок столько, сколько помещается (ширина карточки — от 172 DIP и не больше двух минимумов): названия переносятся, а не
        // обрезаются, одна карточка не растягивается на всю строку, у карточек одной строки — общая высота.
        var tiles = new AdaptiveGrid { MinItemWidth = TileMinWidth, MaxColumns = 12, Gap = 10, Margin = new Thickness(0, 4, 0, 8) };
        string group = "styles-" + kind + GetHashCode();
        foreach (var e in catalog)
        {
            string name = BuiltInStyles.LocalizedName(e);
            string sub = e.BuiltIn ? Loc.T("Styles.BuiltIn") : Loc.T("Styles.Mine");
            if (e.Kind == StyleKind.Keycap)
            {
                sub += " · " + Loc.T("Renderer." + e.Keycap!.Renderer);
            }
            else if (e.Kind == StyleKind.Mouse)
            {
                sub += " · " + Loc.T("MouseRenderer." + e.Mouse!.Renderer);
            }

            bool isSelected = e.Id == selectedId;
            var entry = e;
            tiles.Children.Add(Tile(group, name, sub, Thumb(entry, s), isSelected, () => U(x => StyleOps.Apply(x, entry))));
        }

        rows.Add(tiles);

        string status = selected is null
            ? selectedId is null ? Loc.T("Styles.Status.Custom") : Loc.T("Styles.Status.Missing")
            : modified ? Loc.F("Styles.Status.Modified", BuiltInStyles.LocalizedName(selected)) : Loc.F("Styles.Status.Selected", BuiltInStyles.LocalizedName(selected));
        rows.Add(_ui.Note(status));

        if (_styles is not null)
        {
            // Phase 4H: девять действий — одна компактная панель значков (было три строки текстовых кнопок). Группы: сохранение,
            // правка, обмен файлами; удаление — отдельной последней группой. Текст — в подсказке и доступном имени; недоступное
            // действие объясняет причину. Состояние («изменён», выбранный стиль) показывает строка над панелью.
            bool canSave = modified || selected is null || selectedId is null;
            bool builtIn = selected is { BuiltIn: true };
            string? noSelection = selected is null ? Loc.T("Styles.Why.NoSelection") : null;
            var save = _ui.Action(ActionIcons.Save, Loc.T("Styles.Save"), () => SaveStyle(kind, selected), canSave, Loc.T("Styles.Why.NotModified"), primary: canSave);
            var saveAs = _ui.Action(ActionIcons.SaveAsNew, Loc.T("Styles.SaveAsNew"), () => SaveStyleAs(kind, selected));
            var dup = _ui.Action(ActionIcons.Duplicate, Loc.T("Styles.Duplicate"), () =>
            {
                var copy = _styles.Duplicate(selected!, n => Loc.F("Profiles.CopyName", n));
                U(x => StyleOps.Apply(x, copy));
                BuildSection();
            }, selected is not null, noSelection);
            var rename = _ui.Action(ActionIcons.Rename, Loc.T("Profiles.Rename"), () => RenameStyle(selected!), selected is { BuiltIn: false },
                builtIn ? Loc.T("Styles.Why.BuiltIn") : noSelection);
            var restore = _ui.Action(ActionIcons.Restore, Loc.T("Styles.Restore"), () =>
            {
                U(x => StyleOps.Apply(x, selected!));
                BuildSection();
            }, selected is not null && modified, noSelection ?? Loc.T("Styles.Why.NotModified"));
            var import = _ui.Action(ActionIcons.Import, Loc.T("Profiles.Import"), () => ImportStyle(kind));
            var export = _ui.Action(ActionIcons.Export, Loc.T("Profiles.Export"), () => ExportStyle(kind, selected));
            var folder = _ui.Action(ActionIcons.OpenFolder, Loc.T("Styles.OpenFolder"), OpenStylesFolder);
            var delete = _ui.Action(ActionIcons.Delete, Loc.T("Profiles.Delete"), () => DeleteStyle(selected!), selected is { BuiltIn: false },
                builtIn ? Loc.T("Styles.Why.BuiltIn") : noSelection, danger: true);
            // Обмен файлами — редкие действия: при нехватке ширины уходят в меню «Ещё» с текстовыми названиями.
            var bar = _ui.Toolbar([[save, saveAs], [dup, rename, restore]], [import, export, folder], [delete]);
            bar.Tag = "style-toolbar";
            rows.Add(bar);
            if (_styles.SkippedFiles > 0)
            {
                rows.Add(_ui.Note(Loc.F("Styles.Skipped", _styles.SkippedFiles)));
            }
        }

        var card = _ui.Card(Loc.T(titleKey), Loc.T(hintKey), rows.ToArray());
        card.Tag = CatalogTag;
        return card;
    }

    internal const double TileMinWidth = 172;

    private RadioButton Tile(string group, string name, string subtitle, FrameworkElement thumb, bool isSelected, Action select)
    {
        var content = new StackPanel();
        // Миниатюра — постоянной высоты, рисунок внутри вписывается без искажения (Viewbox Uniform) при любой ширине карточки.
        var stage = new ClipBorder { Height = 70, CornerRadius = new CornerRadius(6), Child = StageBackground(thumb) };
        content.Children.Add(stage);
        // Название — до трёх строк (длинные uk/ru переносятся), подзаголовок — до двух; обрезка только в крайнем случае.
        content.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 6, 2, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 62, TextTrimming = TextTrimming.WordEllipsis });
        content.Children.Add(new TextBlock { Text = subtitle, Style = UiTheme.Resources.StyleOf("Ko.Hint"), Margin = new Thickness(2, 1, 2, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 36, TextTrimming = TextTrimming.WordEllipsis });
        var rb = new RadioButton { Content = content, GroupName = group, IsChecked = isSelected, Style = UiTheme.Resources.StyleOf("Ko.Tile"), ToolTip = name, Margin = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Top };
        System.Windows.Automation.AutomationProperties.SetName(rb, name);
        rb.Checked += (_, _) =>
        {
            if (!_switching)
            {
                select();
                Dispatcher.BeginInvoke(BuildSection);
            }
        };
        return rb;
    }

    /// <summary>Фон миниатюры — сцена в цветах темы окна, как у превью (стиль кейкапов свои цвета сохраняет).</summary>
    private static Grid StageBackground(FrameworkElement thumb) =>
        Stage.Wrap(new Viewbox { Child = thumb, Margin = new Thickness(6), StretchDirection = StretchDirection.Both }, grid: false);

    /// <summary>Миниатюра — настоящий рендер приложения (не картинка-макет).</summary>
    private FrameworkElement Thumb(StyleEntry e, LabSettings s) => e.Kind switch
    {
        StyleKind.Keycap => KeycapThumb(StyleOps.ApplyKeycap(s.Appearance, e.Keycap!)),
        StyleKind.Indicator => IndicatorThumb(s, e.Indicator),
        _ => MouseThumb(e.Mouse! with { SideButtons = s.Appearance.MouseLook.SideButtons, SideButtonsSide = s.Appearance.MouseLook.SideButtonsSide }),
    };

    public static FrameworkElement KeycapThumb(AppearanceSettings a)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var m = KeycapMetrics.From(a);
        var items = new[] { new KeycapItem(1, 1, "Ctrl", true, false, KeySide.Left), new KeycapItem(2, 2, "K", false, true) };
        for (int i = 0; i < items.Length; i++)
        {
            var k = KeycapFactory.Create(a.Renderer);
            k.Apply(a, items[i], showBadge: false);
            k.PressOffset = items[i].IsPressed ? m.PressTravel : 0;
            k.Margin = new Thickness(i == 0 ? 0 : m.KeyGap, 0, 0, 0);
            row.Children.Add(k);
        }

        var (fill, outline, width) = a.BackgroundEnabled ? BackdropMaterials.Paint(a.Backdrop, a.BackgroundColor) : (null, null, 0);
        return new Border
        {
            Background = fill,
            BorderBrush = outline,
            BorderThickness = new Thickness(width),
            Padding = new Thickness(a.BackgroundEnabled ? Math.Max(0, m.GroupPadding - width) : 0),
            CornerRadius = new CornerRadius(m.GroupCornerRadius),
            Child = row,
        };
    }

    private static FrameworkElement IndicatorThumb(LabSettings s, IndicatorVisual? own)
    {
        var v = new LayoutIndicatorVisual();
        var look = own is null
            ? IndicatorLook.Resolve(s.Appearance, s.LayoutIndicator with { CustomStyle = false })
            : IndicatorLook.Resolve(s.Appearance with { Indicator = own }, s.LayoutIndicator with { CustomStyle = true });
        v.Set(new LayoutLabel("EN", null), look);
        return v;
    }

    public static FrameworkElement MouseThumb(MouseVisual look)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var st in new[] { new MouseIconState(MouseButtons.Left, MouseButtons.None, 0, 0), new MouseIconState(MouseButtons.None, MouseButtons.None, 1, 0) })
        {
            var (fill, outline, width) = BackdropMaterials.Paint(look.Plate, look.IconBackground);
            row.Children.Add(new Border
            {
                Background = fill,
                BorderBrush = outline,
                BorderThickness = new Thickness(width),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(6),
                Margin = new Thickness(3),
                Child = MouseIcon.Create(40, look, st),
            });
        }

        return row;
    }

    private void SaveStyle(StyleKind kind, StyleEntry? selected)
    {
        if (_styles is null)
        {
            return;
        }

        if (selected is null || selected.BuiltIn)
        {
            SaveStyleAs(kind, selected);
            return;
        }

        var updated = StyleOps.Capture(_store.Current, kind, selected.Id, selected.Name, selected.BasedOn);
        _styles.Save(updated);
        U(x => StyleOps.Apply(x, updated));
        BuildSection();
    }

    private void SaveStyleAs(StyleKind kind, StyleEntry? basis)
    {
        if (_styles is null)
        {
            return;
        }

        string suggested = basis is null ? Loc.T("Styles.NewName") : Loc.F("Profiles.CopyName", BuiltInStyles.LocalizedName(basis));
        var name = KoDialog.Prompt(this, Loc.T("Styles.SaveAsNew"), Loc.T("Profiles.NameLabel"), suggested, ValidateName);
        if (name is null)
        {
            return;
        }

        var captured = StyleOps.Capture(_store.Current, kind, StyleIds.NewId(), name, basis?.BuiltIn == true ? basis.Id : basis?.BasedOn);
        var saved = _styles.SaveAsNew(captured, name);
        U(x => StyleOps.Apply(x, saved));
        BuildSection();
    }

    private void RenameStyle(StyleEntry e)
    {
        var name = KoDialog.Prompt(this, Loc.T("Profiles.Rename.Title"), Loc.T("Profiles.NameLabel"), e.Name, ValidateName);
        if (name is not null)
        {
            _styles!.Rename(e.Id, name);
            BuildSection();
        }
    }

    private void DeleteStyle(StyleEntry e)
    {
        var (choice, _) = KoDialog.Ask(this, Loc.T("Styles.Delete.Title"), Loc.F("Styles.Delete.Message", e.Name),
            [new("delete", Loc.T("Profiles.Delete"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
        if (choice == "delete")
        {
            _styles!.Delete(e.Id);
            // Параметры остаются в настройках (и в профиле): оформление не пропадает, ссылка показывается как «нет в каталоге».
            BuildSection();
        }
    }

    private void ImportStyle(StyleKind kind)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Loc.T("Styles.FileFilter") + "|*.json", Title = Loc.T("Profiles.Import") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var r = _styles!.Import(dialog.FileName);
        if (!r.Ok)
        {
            KoDialog.Ask(this, Loc.T("Styles.ImportFailed.Title"), Loc.T("Profiles.ImportFailed.Message") + "\n\n" + string.Join("\n", r.Errors.Take(8)), [new("ok", Loc.T("Dialog.Ok"), true)]);
            return;
        }

        if (r.Style!.Kind != kind)
        {
            KoDialog.Ask(this, Loc.T("Styles.Imported.Title"), Loc.F("Styles.Imported.OtherKind", r.Style.Name), [new("ok", Loc.T("Dialog.Ok"), true)]);
            BuildSection();
            return;
        }

        string message = r.Outcome switch
        {
            ImportOutcome.AlreadyPresent => Loc.F("Styles.Imported.Present", r.Style.Name),
            ImportOutcome.Renamed => Loc.F("Styles.Imported.Renamed", r.Style.Name),
            _ => Loc.F("Styles.Imported.Added", r.Style.Name),
        };
        var (choice, _) = KoDialog.Ask(this, Loc.T("Styles.Imported.Title"), message, [new("apply", Loc.T("Styles.Apply"), true), new("later", Loc.T("Profiles.Imported.Later"))]);
        if (choice == "apply")
        {
            U(x => StyleOps.Apply(x, r.Style));
        }

        BuildSection();
    }

    private void ExportStyle(StyleKind kind, StyleEntry? selected)
    {
        var entry = selected is not null && StyleOps.Matches(_store.Current, selected)
            ? selected
            : StyleOps.Capture(_store.Current, kind, StyleIds.NewId(), selected is null ? Loc.T("Styles.NewName") : BuiltInStyles.LocalizedName(selected), selected?.Id);
        if (entry.BuiltIn)
        {
            entry = entry with { Id = StyleIds.NewId(), Name = BuiltInStyles.LocalizedName(entry), BuiltIn = false, BasedOn = entry.Id };
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("Styles.FileFilter") + "|*.json", FileName = entry.Name + ".backpack-style.json" };
        if (dialog.ShowDialog(this) == true)
        {
            _styles!.Export(entry, dialog.FileName);
        }
    }

    private void OpenStylesFolder()
    {
        try
        {
            Directory.CreateDirectory(_styles!.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _styles.Directory + "\"") { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            KoDialog.Ask(this, Loc.T("Styles.OpenFolder"), Loc.F("Styles.FolderFailed", _styles!.Directory), [new("ok", Loc.T("Dialog.Ok"), true)]);
        }
    }

    // ── горячие клавиши ──────────────────────────────────────────────────────

    private void ChooseShotFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.T("Draw.Shot.Folder"), InitialDirectory = _prefs!.Current.ScreenshotFolderOrDefault };
        if (dialog.ShowDialog(this) == true && AppPreferencesStore.IsSafeFolder(dialog.FolderName))
        {
            _prefs.Update(p => p with { ScreenshotFolder = dialog.FolderName });
            BuildSection();
        }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            KoDialog.Ask(this, Loc.T("Styles.OpenFolder"), Loc.F("Styles.FolderFailed", folder), [new("ok", Loc.T("Dialog.Ok"), true)]);
        }
    }

    /// <summary>Превью линзы: та же рамка, скругление, тень и увеличение (VisualBrush части ряда кейкапов примера).</summary>
    private FrameworkElement LensPreview()
    {
        var lens = _prefs?.Current.LensOrDefault ?? LensSettings.Default;
        var scene = new Grid { Width = 520, Height = 150 };
        var row = KeycapThumb(_store.Current.Appearance);
        row.HorizontalAlignment = HorizontalAlignment.Left;
        row.VerticalAlignment = VerticalAlignment.Center;
        row.Margin = new Thickness(20, 0, 0, 0);
        scene.Children.Add(row);
        double w = 200, h = 110;
        var frame = new Border
        {
            Width = w,
            Height = h,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 30, 0),
            CornerRadius = new CornerRadius(lens.Radius),
            BorderThickness = new Thickness(lens.BorderWidth),
            BorderBrush = lens.BorderColor.ToFrozenBrush(),
            Background = new VisualBrush(row) { Stretch = Stretch.Uniform, Viewbox = new Rect(0.15, 0.05, 1 / lens.Zoom, 1 / lens.Zoom) },
        };
        if (lens.Shadow)
        {
            frame.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = 0.45 };
        }

        scene.Children.Add(frame);
        return new Viewbox { Child = scene, Margin = new Thickness(12), StretchDirection = StretchDirection.DownOnly };
    }

    /// <summary>Размер «экрана» превью рисования: рабочая область основного монитора (DIP) в разумных пределах.</summary>
    internal static Size DrawingPreviewScreen()
    {
        var wa = SystemParameters.WorkArea;
        double w = Math.Clamp(wa.Width, 1024, 2560), h = Math.Clamp(wa.Height, 576, 1600);
        return new Size(w, h);
    }

    /// <summary>Базовый размер виртуального монитора превью рисования и пределы масштаба (панель не мельче 55 % настоящей).</summary>
    internal static readonly Size DrawingPreviewBase = new(960, 600);

    internal const double DrawingPreviewMinScale = 0.55, DrawingPreviewMaxScale = 0.9;

    /// <summary>
    /// Превью рисования (Phase 4G): область превью — виртуальный монитор (<see cref="VirtualMonitor"/>): вертикальное превью —
    /// вертикальный монитор. Настоящая панель в реальном размере стоит там же, что в режиме рисования
    /// (<see cref="Presentation.DrawingWindow.ToolbarOrigin"/>): постоянная, у выбранного края (видна, как при подведении указателя;
    /// край подсвечен) или только радиальное меню. Если полная панель не помещается вдоль своего края (узкий вертикальный монитор,
    /// низкий — для боковой панели), показывается её компактный вид, как сделало бы окно рисования; вся панель не уменьшается.
    /// </summary>
    internal FrameworkElement DrawingPreview(DrawSettings draw, DrawingProfile profile) =>
        new VirtualMonitor(area => DrawingScene(draw, profile, area), DrawingPreviewBase, DrawingPreviewMinScale, DrawingPreviewMaxScale);

    /// <summary>Логический размер монитора последней построенной сцены превью рисования.</summary>
    internal Size DrawingPreviewArea { get; private set; }

    /// <summary>Панель не помещается вдоль своего края — нужен компактный вид.</summary>
    internal static bool ToolbarNeedsCompact(DrawingProfile d, Size area, Size bar) =>
        d.ToolbarMode == DrawToolbarMode.AutoHide && d.AutoHideEdge is ScreenEdge.Left or ScreenEdge.Right
            ? bar.Height > area.Height - (2 * Presentation.DrawingWindow.EdgeMargin)
            : bar.Width > area.Width - (2 * Presentation.DrawingWindow.EdgeMargin);

    private FrameworkElement DrawingScene(DrawSettings draw, DrawingProfile d, Size area)
    {
        DrawingPreviewBar = null;
        DrawingPreviewArea = area;
        double w = area.Width, h = area.Height, k = 1;
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        var screen = new Border
        {
            Width = w,
            Height = h,
            Background = new LinearGradientBrush(Color.FromRgb(0x2B, 0x2E, 0x3A), Color.FromRgb(0x46, 0x4B, 0x5E), 20),
            Child = canvas,
        };

        // Пример рисунка: стрелка, подпись и нумерованная отметка — ниже панели, у центра экрана.
        var arrow = new System.Windows.Shapes.Path
        {
            Data = Presentation.DrawingWindow.ArrowGeometry(new Point(w * 0.26, h * 0.72), new Point(w * 0.46, h * 0.52), 6 * k),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)),
            StrokeThickness = 6 * k,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        canvas.Children.Add(arrow);
        var text = new TextBlock { Text = Loc.T("Draw.PreviewText"), Foreground = Brushes.White, FontSize = 44 * k, FontFamily = new FontFamily("Segoe UI") };
        Canvas.SetLeft(text, w * 0.48);
        Canvas.SetTop(text, h * 0.44);
        canvas.Children.Add(text);
        var marker = new Border { Width = 52 * k, Height = 52 * k, CornerRadius = new CornerRadius(26 * k), Background = new SolidColorBrush(Color.FromRgb(0x31, 0xC9, 0x9A)), Child = new TextBlock { Text = "1", Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 28 * k, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Canvas.SetLeft(marker, w * 0.2);
        Canvas.SetTop(marker, h * 0.72);
        canvas.Children.Add(marker);

        if (d.ToolbarMode == DrawToolbarMode.RadialOnly)
        {
            var radial = new Presentation.RadialMenu();
            radial.Place(new Point(w * 0.76, h * 0.5)); // настоящий размер меню относительно экрана
            canvas.Children.Add(radial);
        }
        else
        {
            // Естественный размер полной панели (без переноса в несколько строк): не помещается вдоль края — компактный вид.
            var natural = Presentation.DrawingWindow.PreviewToolbar(draw, d, new Size(10000, 10000));
            natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (!d.Compact && ToolbarNeedsCompact(d, area, natural.DesiredSize))
            {
                d = d with { Compact = true };
            }

            var bar = Presentation.DrawingWindow.PreviewToolbar(draw, d, area);
            bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var origin = Presentation.DrawingWindow.ToolbarOrigin(d, area, bar.DesiredSize);
            Canvas.SetLeft(bar, origin.X);
            Canvas.SetTop(bar, origin.Y);
            if (d.ToolbarMode == DrawToolbarMode.AutoHide)
            {
                // Край, у которого панель появляется: тонкая подсветка во всю длину края.
                bool vertical = d.AutoHideEdge is ScreenEdge.Left or ScreenEdge.Right;
                var edge = new Border { Width = vertical ? 4 * k : w, Height = vertical ? h : 4 * k, Opacity = 0.8 };
                edge.SetResourceReference(Border.BackgroundProperty, "Ko.AccentBrush");
                Canvas.SetLeft(edge, d.AutoHideEdge == ScreenEdge.Right ? w - edge.Width : 0);
                Canvas.SetTop(edge, d.AutoHideEdge == ScreenEdge.Bottom ? h - edge.Height : 0);
                canvas.Children.Add(edge);
            }

            canvas.Children.Add(bar);
            DrawingPreviewBar = bar;
        }

        return screen;
    }

    /// <summary>Панель в последнем построенном превью рисования (для проверок раскладки).</summary>
    internal FrameworkElement? DrawingPreviewBar { get; private set; }

    internal void SetParts(DisplayParts part, bool on) => _prefs!.Update(p => p with { HideParts = on ? p.HideParts | part : p.HideParts & ~part });

    // ── справка ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Проверки и диагностика (прежде — страница «Справка», Phase 4F перенёс их на «Настройки приложения»): пример оверлея и
    /// индикатора, окно диагностики, файл настроек, сообщения о чтении и записи, полный сброс.
    /// </summary>
    private void AddDiagnosticsCards()
    {
        var diag = new List<UIElement>
        {
            _ui.Row(Loc.T("HelpSection.OverlayExample"), Loc.T("HelpSection.OverlayExampleHint"), _ui.Button(Loc.T("Common.Show"), () => _actions.ShowOverlayExample())),
            _ui.Row(Loc.T("HelpSection.LayoutIndicator"), _actions.LayoutStatus(), _ui.Button(Loc.T("Layout.ShowExample"), () => _actions.ShowLayoutIndicatorExample())),
        };
        if (_actions.OpenDiagnostics is { } open)
        {
            diag.Add(_ui.Row(Loc.T("HelpSection.Diagnostics"), Loc.T("HelpSection.DiagnosticsHint"), _ui.Button(Loc.T("Common.Open"), open)));
        }

        _cards.Children.Add(_ui.Card(Loc.T("HelpSection.Diag.Title"), null, diag.ToArray()));
        var info = new List<UIElement> { _ui.Note(Loc.F("HelpSection.SettingsFile", _store.FilePath)) };
        if (_store.MigratedFrom is { } v)
        {
            info.Add(_ui.Note(Loc.F("HelpSection.Migrated", v)));
        }

        info.AddRange(_store.LoadProblems.Select(p => _ui.Note(Loc.F("HelpSection.LoadProblem", p))));
        if (_store.LastSaveError is { } e)
        {
            info.Add(_ui.Note(e.ToString()));
        }

        info.Add(_ui.Row(Loc.T("HelpSection.ResetAll"), Loc.T("HelpSection.ResetAllHint"), _ui.Button(Loc.T("HelpSection.ResetAllButton"), ResetAll)));
        _cards.Children.Add(_ui.Card(Loc.T("HelpSection.Settings.Title"), null, info.ToArray()));
    }

    // ── о программе ──────────────────────────────────────────────────────────

    /// <summary>
    /// «Вироблено в Україні» с флагом. Флаг нарисован (синий над жёлтым), а не эмодзи 🇺🇦: Windows не рисует эмодзи флагов и
    /// показывает вместо них буквы «UA». Phase 4I: надпись на языке интерфейса (About.MadeIn), смысл — «Сделано в Украине».
    /// </summary>
    private static FrameworkElement MadeInUkraine()
    {
        string text = Loc.T("About.MadeIn");
        var flag = new Border { Width = 22, Height = 15, CornerRadius = new CornerRadius(2.5), ClipToBounds = true, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var stripes = new Grid();
        stripes.RowDefinitions.Add(new RowDefinition());
        stripes.RowDefinitions.Add(new RowDefinition());
        var blue = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x57, 0xB7)), CornerRadius = new CornerRadius(2.5, 2.5, 0, 0) };
        var yellow = new Border { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)), CornerRadius = new CornerRadius(0, 0, 2.5, 2.5) };
        Grid.SetRow(yellow, 1);
        stripes.Children.Add(blue);
        stripes.Children.Add(yellow);
        flag.Child = stripes;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(flag);
        row.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        System.Windows.Automation.AutomationProperties.SetName(row, text);
        row.Tag = "made-in-ukraine";
        return row;
    }

    private void BuildAbout()
    {
        var info = _about ?? AppInfo.FromAssembly(typeof(SettingsWindow).Assembly);
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var logo = LogoArt.Themed(72);
        logo.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(logo, Dock.Left);
        head.Children.Add(logo);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var mark = Wordmark.Create(30, FontWeights.SemiBold);
        System.Windows.Automation.AutomationProperties.SetName(mark, info.Name);
        title.Children.Add(mark);
        title.Children.Add(new TextBlock { Text = Loc.F("About.VersionLine", info.Version, info.Channel), Style = UiTheme.Resources.StyleOf("Ko.Hint") });
        title.Children.Add(MadeInUkraine());
        head.Children.Add(title);

        _cards.Children.Add(_ui.Card(Loc.T("About.Title"), null,
            head,
            _ui.Row(Loc.T("About.Version"), null, Value(info.Version)),
            _ui.Row(Loc.T("About.Channel"), null, Value(info.Channel)),
            _ui.Row(Loc.T("About.ReleaseDate"), info.ReleaseDate is null ? Loc.T("About.ReleaseDateNone") : null, Value(info.ReleaseDate ?? "—")),
            _ui.Row(Loc.T("About.Author"), null, Link(info.Author, info.AuthorUrl)),
            _ui.Row(Loc.T("About.License"), info.License is null ? Loc.T("About.LicenseNone") : Loc.T("About.LicenseHint"),
                info.License is null ? Value(Loc.T("About.LicenseNotChosen"))
                : info.LicenseUrl is { } licenseUrl ? Link(info.License, licenseUrl) : Value(info.License))));

        var third = info.ThirdParty.Select(t => (UIElement)_ui.Row(t.Name, Loc.F("About.ThirdPartyLine", t.Version, t.Use), Value(t.License))).ToList();
        third.Add(_ui.Note(Loc.T("About.ThirdPartyNote")));
        _cards.Children.Add(_ui.Card(Loc.T("About.ThirdParty"), null, third.ToArray()));

        var up = new List<UIElement>();
        var source = _updates?.Source;
        up.Add(_ui.Row(Loc.T("About.UpdateSource"), source is null ? Loc.T("About.UpdateSourceNone") : null, Value(source?.ToString() ?? Loc.T("About.NotConfigured"))));
        if (_prefs is not null)
        {
            var u = _prefs.Current.UpdatesOrDefault;
            up.Add(_ui.Row(Loc.T("About.Channel"), Loc.T("About.ChannelHint"), _ui.Segmented((int)u.Channel, ["Stable", "Preview"], i => _prefs.Update(p => p with { Updates = p.UpdatesOrDefault with { Channel = (UpdateChannel)i } }))));
            up.Add(_ui.Row(Loc.T("About.AutoCheck"), Loc.T("About.AutoCheckHint"), _ui.Toggle(u.AutoCheck, v => _prefs.Update(p => p with { Updates = p.UpdatesOrDefault with { AutoCheck = v } }))));
        }

        var check = _ui.Button(_checking ? Loc.T("About.Checking") : Loc.T("About.Check"), () => _ = CheckUpdatesAsync(info), primary: true);
        check.IsEnabled = !_checking;
        up.Add(_ui.Row(Loc.T("About.CheckNow"), UpdateText(_lastUpdate), check));
        if (_lastUpdate is { Release: { } rel } && source is not null && source.IsOwnPage(rel.PageUrl))
        {
            up.Add(_ui.Row(Loc.F("About.ReleaseTitle", rel.Version, rel.Published?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "—"),
                rel.Notes.Length > 0 ? rel.Notes : null, _ui.Button(Loc.T("About.OpenDownload"), () => OpenExternal(rel.PageUrl))));
        }

        up.Add(_ui.Note(Loc.T("About.UpdateNote")));
        _cards.Children.Add(_ui.Card(Loc.T("About.Updates"), null, up.ToArray()));
    }

    private static string? UpdateText(UpdateCheckResult? r) => r is null ? null : r.State switch
    {
        UpdateState.Available => Loc.F("Update.Available", r.Release!.Version),
        UpdateState.NoSuitablePackage => Loc.F("Update.NoPackage", r.Release!.Version),
        _ => Loc.T("Update." + r.State),
    };

    private async Task CheckUpdatesAsync(AppInfo info)
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        BuildSection();
        try
        {
            var checker = _updates ?? new UpdateChecker(null);
            var channel = _prefs?.Current.UpdatesOrDefault.Channel ?? UpdateChannel.Stable;
            _lastUpdate = await checker.CheckAsync(info.SemVer ?? new SemVersion(0, 0, 0, null), channel);
        }
        finally
        {
            _checking = false;
            if (!_released && Section == SettingsSection.About)
            {
                BuildSection();
            }
        }
    }

    /// <summary>Внешняя ссылка — только после подтверждения (как в справке), в браузере по умолчанию.</summary>
    private void OpenExternal(string url)
    {
        var (choice, _) = KoDialog.Ask(this, Loc.T("About.OpenLink.Title"), Loc.F("About.OpenLink.Message", url), [new("open", Loc.T("Common.Open"), true), new("cancel", Loc.T("Dialog.Cancel"))]);
        if (choice == "open")
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // браузер не настроен — ссылка видна в диалоге
            }
        }
    }

    private static TextBlock Value(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 360, VerticalAlignment = VerticalAlignment.Center };

    private Button Link(string text, string url)
    {
        var b = _ui.Button(text, () => OpenExternal(url));
        b.ToolTip = url;
        return b;
    }
}
