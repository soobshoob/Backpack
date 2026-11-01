using System.Windows;
using System.Windows.Controls;
using Backpack.Visual.Localization;
using Backpack.Visual.Profiles;
using Backpack.Visual.Settings;

namespace Backpack.Visual.Tray;

/// <summary>Действия приложения, которые меню трея вызывает напрямую (окна, видимость оверлея, выход).</summary>
public sealed record TrayCommands(
    Action ShowWindows,
    Action OpenSettings,
    Func<bool> OverlayVisible,
    Action<bool> SetOverlayVisible,
    Action OpenHelp,
    Action Exit,
    Commands.CommandRegistry? Registry = null);

/// <summary>
/// Быстрое меню значка в области уведомлений. Строится заново при каждом открытии, поэтому отметки отражают текущее состояние.
/// Переключатели меняют настройки через <see cref="SettingsStore.Update"/> — открытое окно настроек получает то же изменение
/// и перестраивает раздел. Скрытие оверлея не останавливает захват: модель клавиш продолжает получать нажатия и отпускания.
/// </summary>
public static class TrayMenu
{
    public static ContextMenu Build(SettingsStore store, ProfileSession? profiles, TrayCommands commands)
    {
        var menu = new ContextMenu();
        menu.Resources.MergedDictionaries.Add(UiTheme.Resources);
        var s = store.Current;
        if (commands.Registry is { } reg)
        {
            // Пункты меню — команды общего реестра: та же реализация, что у кнопок и горячих клавиш.
            // «Открыть Backpack» объединяет прежние «Открыть» и «Показать окна» (у них больше нет разного смысла).
            menu.Items.Add(Cmd(reg, "app.open", "tray.open-settings", bold: true));
            menu.Items.Add(new Separator());
            foreach (var (id, name) in new[]
            {
                ("display.toggle", "tray.overlay"), ("keys.combosOnly", "tray.combos"), ("keys.mouseInRow", "tray.mouse-in-row"),
                ("mouse.icon", "tray.mouse-icon"), ("mouse.circle", "tray.mouse-circle"), ("mouse.highlight", "tray.mouse-highlight"),
                ("layout.indicator", "tray.layout"),
            })
            {
                menu.Items.Add(Cmd(reg, id, name));
            }

            menu.Items.Add(new Separator());

            // Команды включённых плагинов (рисование, индикатор раскладки, лупа, линза, фокус, таймер и т. д.) — подменю «Плагины», та же реализация, что у сочетаний.
            var pluginCommands = reg.All.Where(d => d.Source is not null && d.Id.StartsWith("plugin.", StringComparison.Ordinal)).ToList();
            if (pluginCommands.Count > 0)
            {
                var plugins = new MenuItem { Header = Loc.T("Commands.Category.Plugins"), Name = "tray_plugins" };
                foreach (var d in pluginCommands)
                {
                    plugins.Items.Add(Cmd(reg, d.Id, "tray-plugin-" + plugins.Items.Count));
                }

                menu.Items.Add(plugins);
            }

            AddProfiles(menu, profiles);
            menu.Items.Add(new Separator());
            menu.Items.Add(Cmd(reg, "app.help", "tray.help"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Cmd(reg, "app.exit", "tray.exit"));
            return menu;
        }

        menu.Items.Add(Item("tray.open-settings", Loc.T("Tray.OpenSettings"), commands.OpenSettings, bold: true));
        menu.Items.Add(Item("tray.show-windows", Loc.T("Tray.ShowWindows"), commands.ShowWindows));
        menu.Items.Add(new Separator());
        menu.Items.Add(Check("tray.overlay", Loc.T("Tray.ShowOverlay"), commands.OverlayVisible(), commands.SetOverlayVisible));
        menu.Items.Add(Check("tray.combos", Loc.T("Tray.CombosOnly"), s.Keys.Filter == KeyFilter.CombosOnly,
            v => store.Update(x => x with { Keys = x.Keys with { Filter = v ? KeyFilter.CombosOnly : KeyFilter.All } })));
        menu.Items.Add(Check("tray.mouse-in-row", Loc.T("Tray.MouseInRow"), s.Keys.MouseInRow,
            v => store.Update(x => x with { Keys = x.Keys with { MouseInRow = v } })));
        menu.Items.Add(Check("tray.mouse-icon", Loc.T("Tray.MouseIcon"), MouseIconOn(s), v => store.Update(x => SetMouseIcon(x, v))));
        menu.Items.Add(Check("tray.mouse-circle", Loc.T("Tray.MouseCircle"), MouseCircleOn(s), v => store.Update(x => SetMouseCircle(x, v))));
        // Индикатор раскладки и рисование — плагины (Phase 4H): их команды в подменю «Плагины».

        AddProfiles(menu, profiles);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("tray.help", Loc.T("Tray.Help"), commands.OpenHelp));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("tray.exit", Loc.T("Tray.Exit"), commands.Exit));
        return menu;
    }

    private static MenuItem Cmd(Commands.CommandRegistry reg, string id, string name, bool bold = false)
    {
        var d = reg.Get(id);
        var item = new MenuItem { Header = d?.Title ?? id, Name = name.Replace('.', '_').Replace('-', '_'), IsEnabled = d?.Available ?? false };
        if (bold)
        {
            item.FontWeight = FontWeights.SemiBold;
        }

        if (d?.IsChecked is { } check)
        {
            item.IsCheckable = true;
            item.IsChecked = check();
        }

        item.Click += (_, _) => reg.Execute(id);
        return item;
    }

    private static void AddProfiles(ContextMenu menu, ProfileSession? profiles)
    {
        if (profiles is not null)
        {
            menu.Items.Add(new Separator());
            var sub = new MenuItem { Header = Loc.F("Tray.Profile", profiles.ActiveName), Name = "tray_profiles" };
            foreach (var info in profiles.List())
            {
                string id = info.Id;
                var item = new MenuItem { Header = info.Name, IsCheckable = false, IsChecked = profiles.Active?.Id == id };
                item.Click += (_, _) =>
                {
                    if (profiles.Active?.Id != id)
                    {
                        var (_, errors) = profiles.SwitchTo(id);
                        if (errors.Count > 0)
                        {
                            KoDialog.Ask(null, Loc.T("Profiles.LoadFailed.Title"),
                                Loc.T("Profiles.LoadFailed.Message") + "\n\n" + string.Join("\n", errors.Take(8)), [new("ok", Loc.T("Dialog.Ok"), true)]);
                        }
                    }
                };
                sub.Items.Add(item);
            }

            menu.Items.Add(sub);
        }
    }

    public static bool MouseIconOn(LabSettings s) => s.MouseCursor.Enabled && s.MouseCursor.ShowIcon;

    public static bool MouseCircleOn(LabSettings s) => s.MouseCursor.Enabled && s.MouseCursor.ShowCircle;

    public static bool MouseHighlightOn(LabSettings s) => s.MouseCursor.Enabled && s.MouseCursor.ShowHighlight;

    /// <summary>Подсветка курсора: как у значка и круга — включение включает показ у курсора, выключение последней части выключает его.</summary>
    public static LabSettings SetMouseHighlight(LabSettings s, bool on)
    {
        var c = s.MouseCursor;
        c = on ? c with { Enabled = true, ShowHighlight = true } : c with { ShowHighlight = false, Enabled = c.Enabled && (MouseIconOn(s) || MouseCircleOn(s)) };
        return s with { MouseCursor = c };
    }

    /// <summary>Значок у курсора: включение включает и показ у курсора; выключение последней части выключает показ целиком.</summary>
    public static LabSettings SetMouseIcon(LabSettings s, bool on)
    {
        var c = s.MouseCursor;
        c = on ? c with { Enabled = true, ShowIcon = true } : c with { ShowIcon = false, Enabled = c.Enabled && (MouseCircleOn(s) || MouseHighlightOn(s)) };
        return s with { MouseCursor = c };
    }

    public static LabSettings SetMouseCircle(LabSettings s, bool on)
    {
        var c = s.MouseCursor;
        c = on ? c with { Enabled = true, ShowCircle = true } : c with { ShowCircle = false, Enabled = c.Enabled && (MouseIconOn(s) || MouseHighlightOn(s)) };
        return s with { MouseCursor = c };
    }

    /// <summary>Пункт по стабильному имени (для тестов и автоматизации): «tray.combos» → имя элемента «tray_combos».</summary>
    public static MenuItem? Find(ContextMenu menu, string id) =>
        menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == id.Replace('.', '_').Replace('-', '_'));

    private static MenuItem Item(string id, string header, Action click, bool bold = false)
    {
        var m = new MenuItem { Header = header, Name = id.Replace('.', '_').Replace('-', '_'), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        m.Click += (_, _) => click();
        return m;
    }

    private static MenuItem Check(string id, string header, bool value, Action<bool> set)
    {
        var m = new MenuItem { Header = header, Name = id.Replace('.', '_').Replace('-', '_'), IsCheckable = true, IsChecked = value, StaysOpenOnClick = false };
        m.Click += (_, _) => set(m.IsChecked);
        return m;
    }
}
