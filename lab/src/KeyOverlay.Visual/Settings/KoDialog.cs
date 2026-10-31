using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyOverlay.Visual.Localization;

namespace KeyOverlay.Visual.Settings;

/// <summary>Вариант ответа диалога: идентификатор, подпись, основная ли кнопка.</summary>
public sealed record DialogChoice(string Id, string Label, bool Primary = false);

/// <summary>
/// Собственные диалоги в общем оформлении (тема, язык) вместо MessageBox: вопрос с вариантами, необязательный флажок,
/// ввод текста с проверкой. Esc — отмена (последний вариант), Enter — основная кнопка.
/// </summary>
public sealed class KoDialog : Window
{
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBox? _input;
    private readonly TextBlock? _error;
    private readonly CheckBox? _check;
    private readonly Func<string, string?>? _validate;

    private KoDialog(string title, string message, IReadOnlyList<DialogChoice> choices, string? checkLabel, string? inputLabel, string? inputValue, Func<string, string?>? validate)
    {
        UiTheme.Attach(this);
        Title = title;
        SizeToContent = SizeToContent.Height;
        Width = 480;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _validate = validate;
        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        panel.Children.Add(new TextBlock { Text = title, Style = UiTheme.Resources.StyleOf("Ko.H2") });
        panel.Children.Add(new TextBlock { Text = message, Style = UiTheme.Resources.StyleOf("Ko.Label"), Margin = new Thickness(0, 0, 0, 12) });
        if (inputLabel is not null)
        {
            panel.Children.Add(new TextBlock { Text = inputLabel, Style = UiTheme.Resources.StyleOf("Ko.Hint") });
            _input = new TextBox { Text = inputValue ?? "", Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Automation.AutomationProperties.SetName(_input, inputLabel);
            _input.TextChanged += (_, _) => Validate();
            panel.Children.Add(_input);
            _error = new TextBlock { Style = UiTheme.Resources.StyleOf("Ko.Error"), Visibility = Visibility.Collapsed };
            panel.Children.Add(_error);
        }

        if (checkLabel is not null)
        {
            _check = new CheckBox { Content = checkLabel, Style = UiTheme.Resources.StyleOf("Ko.Toggle"), Margin = new Thickness(0, 4, 0, 12) };
            panel.Children.Add(_check);
        }

        foreach (var c in choices)
        {
            var b = new Button { Content = c.Label, Style = UiTheme.Resources.StyleOf(c.Primary ? "Ko.PrimaryButton" : "Ko.Button"), Margin = new Thickness(8, 8, 0, 0), IsDefault = c.Primary };
            b.Click += (_, _) => Finish(c.Id);
            _buttons.Children.Add(b);
        }

        panel.Children.Add(_buttons);
        Content = panel;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Result = choices[^1].Id;
                Close();
            }
        };
        Loaded += (_, _) =>
        {
            if (_input is not null)
            {
                _input.Focus();
                _input.SelectAll();
            }
            else
            {
                (_buttons.Children.OfType<Button>().FirstOrDefault(b => b.IsDefault) ?? _buttons.Children.OfType<Button>().FirstOrDefault())?.Focus();
            }
        };
        Result = choices[^1].Id;
    }

    public string Result { get; private set; }

    public bool Checked => _check?.IsChecked == true;

    public string? InputText => _input?.Text.Trim();

    /// <summary>Диалог-вопрос без показа (снимки для справки, тесты).</summary>
    public static KoDialog Create(string title, string message, IReadOnlyList<DialogChoice> choices) => new(title, message, choices, null, null, null, null);

    /// <summary>Вопрос с вариантами. Возвращает Id выбранного варианта (Esc и закрытие — последний вариант).</summary>
    public static (string Choice, bool Checked) Ask(Window? owner, string title, string message, IReadOnlyList<DialogChoice> choices, string? checkLabel = null)
    {
        var d = new KoDialog(title, message, choices, checkLabel, null, null, null);
        if (owner is { IsVisible: true })
        {
            d.Owner = owner;
        }
        else
        {
            d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        d.ShowDialog();
        return (d.Result, d.Checked);
    }

    /// <summary>Ввод текста. <paramref name="validate"/> возвращает текст ошибки или null. null — отмена.</summary>
    public static string? Prompt(Window? owner, string title, string label, string initial, Func<string, string?> validate)
    {
        var d = new KoDialog(title, "", [new("ok", Loc.T("Dialog.Ok"), true), new("cancel", Loc.T("Dialog.Cancel"))], null, label, initial, validate);
        if (owner is { IsVisible: true })
        {
            d.Owner = owner;
        }
        else
        {
            d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        d.ShowDialog();
        return d.Result == "ok" ? d.InputText : null;
    }

    private bool Validate()
    {
        if (_input is null || _validate is null)
        {
            return true;
        }

        string? error = _validate(_input.Text.Trim());
        _input.Tag = error is null ? null : "error";
        _error!.Text = error ?? "";
        _error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }

    private void Finish(string id)
    {
        if (id == "ok" && !Validate())
        {
            return;
        }

        Result = id;
        Close();
    }
}
