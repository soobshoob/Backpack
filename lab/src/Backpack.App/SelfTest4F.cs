using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Backpack.Visual.Plugins;
using Backpack.Core.Privacy;
using Backpack.Visual.Localization;
using Backpack.Visual.Presentation;
using Backpack.Visual.Settings;

namespace Backpack.App;

/// <summary>
/// Самопроверка Phase 4F (шаг 13): адаптивная компоновка всех страниц на четырёх ширинах и трёх языках (ничего не выходит за
/// карточки, заголовок виден, превью закреплено вне прокрутки), автосохранение профиля, только «Стандартный» и «Spectral»,
/// ручки «Фокуса» и закрытие всех окон по Esc, решение приватности для рабочего стола Windows. Ввод не используется.
/// </summary>
public partial class App
{
    private void RunPhase4FSelfTest(Action<bool, string> check, List<string> notes)
    {
        RunLayoutSelfTest(check, notes);
        RunAutosaveSelfTest(check);
        RunFocusGripSelfTest(check);
        check(FocusClassifier.Classify(new FocusFacts(PasswordProperty.NotSupported, FocusClassifier.ControlTypeList, "Win32", "SysListView32", "SHELLDLL_DefView", "Progman", true, "explorer.exe")) == ProbeVerdict.NotPassword,
            "privacy: desktop icon list is an ordinary element");
        check(FocusClassifier.Classify(new FocusFacts(PasswordProperty.NotSupported, 50033, "XAML", "x", "Shell_TrayWnd", "Shell_TrayWnd", true, "explorer.exe")) == ProbeVerdict.NoData,
            "privacy: other Explorer windows stay no-data");
    }

    /// <summary>
    /// Каждая страница на ширинах 1400/1000/760/560 DIP: границы элементов, заголовок, превью. По умолчанию — украинский,
    /// английский и русский на всех ширинах плюс самые длинные строки (немецкий, финский) и греческое письмо на самой узкой;
    /// <c>--selftest-ui=languages</c> — все 19 языков на всех ширинах (в 6 раз дольше, не для проверки пакета).
    /// </summary>
    private void RunLayoutSelfTest(Action<bool, string> check, List<string> notes)
    {
        OpenSettings(SettingsSection.Home);
        var w = _settingsWindow;
        if (w is null)
        {
            check(false, "layout: settings window");
            return;
        }

        double width0 = w.Width, height0 = w.Height;
        var sections = Enum.GetValues<SettingsSection>().Where(s => s is not (SettingsSection.Help or SettingsSection.Lens or SettingsSection.CursorZoom or SettingsSection.Drawing)).ToArray();
        int pages = 0, overflow = 0;
        var first = new List<string>();
        double[] allWidths = [1400.0, 1000.0, 760.0, SettingsWindow.MinWindowWidth];
        var runs = _options?.SelfTestAllLanguages == true
            ? Loc.Supported.Select(s => (s.Language, Widths: allWidths)).ToList()
            : [(UiLanguage.Ukrainian, allWidths), (UiLanguage.English, allWidths), (UiLanguage.Russian, allWidths),
               (UiLanguage.German, [SettingsWindow.MinWindowWidth]), (UiLanguage.Finnish, [SettingsWindow.MinWindowWidth]), (UiLanguage.Greek, [SettingsWindow.MinWindowWidth])];
        foreach (var (lang, widths) in runs)
        {
            Loc.Set(lang);
            foreach (double width in widths)
            {
                w.Width = width;
                w.Height = 760;
                foreach (var s in sections)
                {
                    w.Show(s);
                    w.UpdateLayout();
                    pages++;
                    foreach (var problem in LayoutProblems(w))
                    {
                        overflow++;
                        if (first.Count < 6)
                        {
                            first.Add($"{lang}/{width}/{s}: {problem}");
                        }
                    }
                }
            }
        }

        check(overflow == 0, $"layout: {pages} page layouts without overflow ({string.Join("; ", first)})");
        notes.Add($"selftest-ui: layout pages {pages}, problems {overflow}");
        w.Width = width0;
        w.Height = height0;
        Loc.Set(_prefs!.Current.Language);
    }

    /// <summary>Заголовок виден целиком; превью не внутри прокрутки; элементы строк и кнопки не выходят за свои карточки.</summary>
    private static IEnumerable<string> LayoutProblems(SettingsWindow w)
    {
        var title = Descendants<TextBlock>(w).FirstOrDefault(t => t.Style == UiTheme.Resources.StyleOf("Ko.H1") && t.IsVisible);
        if (title is null || title.ActualHeight < 10 || title.ActualWidth < 10)
        {
            yield return "title not visible";
        }

        if (w.PreviewCard is { IsVisible: true } p && w.Cards.IsAncestorOf(p))
        {
            yield return "preview scrolls with cards";
        }

        foreach (var card in w.Cards.Children.OfType<Border>().Where(b => b.IsVisible && b.ActualWidth > 0 && !ReferenceEquals(b, w.PreviewCard)))
        {
            var bounds = new Rect(card.RenderSize);
            foreach (var e in Descendants<FrameworkElement>(card).Where(e => e.IsVisible && e.ActualWidth > 0 && e is Button or AdaptiveRow or TextBox or Slider or ComboBox))
            {
                var r = e.TransformToAncestor(card).TransformBounds(new Rect(e.RenderSize));
                if (r.Right > bounds.Right + 1 || r.Left < -1)
                {
                    yield return $"{e.GetType().Name} {(e as Button)?.Content as string ?? ""} at {r.Right:F0} > {bounds.Right:F0}";
                    break;
                }
            }
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t)
            {
                yield return t;
            }

            foreach (var d in Descendants<T>(c))
            {
                yield return d;
            }
        }
    }

    /// <summary>Изменение настройки записывается в текущий профиль; только «Стандартный» и «Spectral».</summary>
    private void RunAutosaveSelfTest(Action<bool, string> check)
    {
        if (_profiles?.Active is not { } active)
        {
            check(false, "autosave: no active profile");
            return;
        }

        int linger = _store!.Current.Keys.LingerMs == 1234 ? 1235 : 1234;
        _store.Update(x => x with { Keys = x.Keys with { LingerMs = linger } });
        bool flushed = _profiles.Flush();
        var saved = _profiles.Store.Load(_profiles.Active!.Id);
        check(flushed && saved.Ok && saved.Profile!.Settings.Keys.LingerMs == linger && saved.FileVersion == Visual.Profiles.ProfileSerializer.FormatVersion,
            $"autosave: change is written to profile {active.Name}");
        check(FontSettings.Choices.SequenceEqual([null, FontSettings.Spectral]) && FontSettings.NormalizeName("Segoe UI") is null, "fonts: only Standard and Spectral");
    }

    /// <summary>Ручки области затемнения (движок инструментов плагинов): двигают область, после Esc не остаётся ни одного окна.</summary>
    private void RunFocusGripSelfTest(Action<bool, string> check)
    {
        if (_tools is null || _monitors.Count == 0)
        {
            return;
        }

        const string plugin = "backpack.selftest";
        var m = _monitors.First(x => x.IsPrimary);
        var hole = new PixelRect(m.Bounds.Left + (m.Bounds.Width / 3), m.Bounds.Top + (m.Bounds.Height / 3), m.Bounds.Width / 4, m.Bounds.Height / 5);
        _tools.Open(plugin, new PluginToolRequest("dim", null, new Backpack.Sdk.DimRequest()));
        var f = _tools.SessionsOf(plugin).Single().Focus!;
        f.Activate(hole);
        PumpFor(150);
        f.RevealControls();
        PumpFor(60);
        bool grips = f.GripOf(FocusGripKind.Move) is { IsVisible: true } && f.GripOf(FocusGripKind.Resize) is { IsVisible: true } && !f.CapturesMouse && f.ControlsVisible;
        f.DragBy(FocusGripKind.Move, 40, 30);
        f.EndDrag();
        bool moved = f.Region?.Output.Left == hole.Left + 40;
        _tools.Escape();
        PumpFor(80);
        check(grips && moved && f.State == FocusState.Off && f.Grips.Count == 0 && f.DimWindows.Count == 0 && _tools.Count == 0,
            $"focus grips: visible {grips}, moved {moved}, closed {f.State == FocusState.Off && f.Grips.Count == 0}");
    }
}
