using System.Windows;
using System.Windows.Input;
using KeyOverlay.Visual.Localization;

namespace KeyOverlay.Visual.Help;

/// <summary>Присоединённое свойство раздела справки: F1 ищет его от элемента с фокусом вверх по дереву.</summary>
public static class HelpTopic
{
    public static readonly DependencyProperty TopicProperty = DependencyProperty.RegisterAttached(
        "Topic", typeof(string), typeof(HelpTopic), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static void SetTopic(DependencyObject d, string? value) => d.SetValue(TopicProperty, value);

    public static string? GetTopic(DependencyObject d) => (string?)d.GetValue(TopicProperty);
}

/// <summary>
/// Один экземпляр окна справки на UI-поток. Повторный вызов активирует существующее окно (восстанавливает из свёрнутого) и
/// переходит к разделу. Язык справки — язык интерфейса; при его смене открытое окно показывает тот же раздел на новом языке.
/// F1 подключается к обычным окнам KeyOverlay через <see cref="ApplicationCommands.Help"/> — только пока окно активно;
/// глобальной регистрации клавиши нет.
/// </summary>
public static class HelpService
{
    [ThreadStatic] private static Dictionary<string, HelpLibrary>? t_libraries;
    [ThreadStatic] private static HelpWindow? t_window;
    [ThreadStatic] private static bool t_subscribed;

    public static HelpLibrary Library => LibraryFor(Loc.Code);

    public static HelpWindow? OpenWindow => t_window;

    public static int WindowsCreated { get; private set; }

    /// <summary>Только для тестов: не выводить окно на экран (логика единственного экземпляра та же).</summary>
    internal static bool ShowOnScreen { get; set; } = true;

    public static HelpLibrary LibraryFor(string language)
    {
        t_libraries ??= new();
        if (!t_libraries.TryGetValue(language, out var lib))
        {
            lib = HelpLibrary.FromResources(language);
            t_libraries[language] = lib;
        }

        return lib;
    }

    public static HelpWindow Show(string? topic = null)
    {
        topic ??= HelpLibrary.IndexFile;
        if (!t_subscribed)
        {
            Loc.Changed += OnLanguageChanged;
            t_subscribed = true;
        }

        if (t_window is null)
        {
            var w = new HelpWindow(Library);
            t_window = w;
            WindowsCreated++;
            w.Closed += (_, _) =>
            {
                if (ReferenceEquals(t_window, w))
                {
                    t_window = null;
                }
            };
            w.Navigate(topic);
            if (ShowOnScreen)
            {
                w.Show();
            }
        }
        else
        {
            if (t_window.WindowState == WindowState.Minimized)
            {
                t_window.WindowState = WindowState.Normal;
            }

            t_window.Navigate(topic);
            if (ShowOnScreen)
            {
                t_window.Activate();
            }
        }

        return t_window;
    }

    /// <summary>Подключить F1 к окну. Раздел — <see cref="HelpTopic.TopicProperty"/> ближайшего к фокусу элемента.</summary>
    public static void Attach(Window window, string defaultTopic)
    {
        if (HelpTopic.GetTopic(window) is null)
        {
            HelpTopic.SetTopic(window, defaultTopic);
        }

        window.CommandBindings.Add(new CommandBinding(ApplicationCommands.Help, (_, e) =>
        {
            Show(ResolveTopic(window));
            e.Handled = true;
        }));
    }

    public static string ResolveTopic(Window window) =>
        (Keyboard.FocusedElement is DependencyObject d && Window.GetWindow(d) == window ? HelpTopic.GetTopic(d) : null)
        ?? HelpTopic.GetTopic(window)
        ?? HelpLibrary.IndexFile;

    public static void CloseWindow() => t_window?.Close();

    /// <summary>Отписка от смены языка (выход из приложения).</summary>
    public static void Shutdown()
    {
        if (t_subscribed)
        {
            Loc.Changed -= OnLanguageChanged;
            t_subscribed = false;
        }

        CloseWindow();
    }

    private static void OnLanguageChanged() => t_window?.SetLibrary(Library);
}
