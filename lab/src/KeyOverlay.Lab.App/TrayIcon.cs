using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Profiles;
using KeyOverlay.Visual.Settings;
using KeyOverlay.Visual.Tray;
using Forms = System.Windows.Forms;

namespace KeyOverlay.Lab.App;

/// <summary>
/// Значок в области уведомлений. Щелчок левой кнопкой — показать окна; правой — быстрое меню (WPF, в теме интерфейса).
/// Меню строится при каждом открытии из текущего состояния. Положение значка (видимая часть или скрытые значки) выбирает Windows.
/// Перед открытием меню активируется невидимое вспомогательное окно — иначе меню не закрывается щелчком мимо него.
/// </summary>
internal sealed partial class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private System.Drawing.Icon _image;
    private bool _lightTaskbar;
    private readonly SettingsStore _store;
    private readonly ProfileSession? _profiles;
    private readonly TrayCommands _commands;
    private Window? _menuHost;
    private ContextMenu? _menu;
    private bool _disposed;

    public TrayIcon(SettingsStore store, ProfileSession? profiles, TrayCommands commands)
    {
        _store = store;
        _profiles = profiles;
        _commands = commands;
        // Контур логотипа без подложки — цветом, читаемым на текущей панели задач: тёмный на светлой, белый на тёмной.
        _lightTaskbar = UiTheme.TaskbarIsLight();
        _image = CreateImage(_lightTaskbar);
        _icon = new Forms.NotifyIcon { Icon = _image, Text = Loc.T("Tray.Tooltip"), Visible = true };
        _icon.MouseUp += OnMouseUp;
        Loc.Changed += UpdateText;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Панель задач светлая (значок нарисован тёмным контуром) — для самопроверки.</summary>
    public bool DrawnForLightTaskbar => _lightTaskbar;

    private static System.Drawing.Icon CreateImage(bool lightTaskbar)
    {
        var color = lightTaskbar ? System.Windows.Media.Color.FromRgb(0x1B, 0x1B, 0x1B) : System.Windows.Media.Colors.White;
        var bytes = KeyOverlay.Visual.Brand.LogoArt.Ico([16, 20, 24, 32, 40, 48], simplifiedOnly: true, color: color);
        using var ms = new System.IO.MemoryStream(bytes);
        return new System.Drawing.Icon(ms, Forms.SystemInformation.SmallIconSize);
    }

    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Color or Microsoft.Win32.UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            bool light = UiTheme.TaskbarIsLight();
            if (_disposed || light == _lightTaskbar)
            {
                return;
            }

            _lightTaskbar = light;
            var old = _image;
            _image = CreateImage(light);
            _icon.Icon = _image;
            old.Dispose();
        });
    }

    public bool Visible => !_disposed && _icon.Visible;

    /// <summary>Открытое сейчас меню (для самопроверки).</summary>
    public ContextMenu? OpenMenu => _menu is { IsOpen: true } ? _menu : null;

    public void UpdateText()
    {
        if (!_disposed)
        {
            _icon.Text = _commands.OverlayVisible() ? Loc.T("Tray.Tooltip") : Loc.T("Tray.TooltipHidden");
        }
    }

    /// <summary>Краткое уведомление у значка (например, о доступном обновлении). Без звука и без действий.</summary>
    public void Notify(string text)
    {
        if (!_disposed)
        {
            _icon.ShowBalloonTip(5000, "Backpack", text, Forms.ToolTipIcon.None);
        }
    }

    /// <summary>Построить меню без показа (самопроверка и тесты используют ту же сборку меню).</summary>
    public ContextMenu BuildMenu() => TrayMenu.Build(_store, _profiles, _commands);

    public void ShowMenu()
    {
        if (_disposed)
        {
            return;
        }

        CloseMenu();
        _menuHost ??= CreateHost();
        SetForegroundWindow(new WindowInteropHelper(_menuHost).Handle);
        _menu = BuildMenu();
        _menu.Placement = PlacementMode.MousePoint;
        _menu.PlacementTarget = _menuHost;
        _menu.IsOpen = true;
    }

    public void CloseMenu()
    {
        if (_menu is not null)
        {
            _menu.IsOpen = false;
            _menu = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loc.Changed -= UpdateText;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        CloseMenu();
        _icon.MouseUp -= OnMouseUp;
        _icon.Visible = false; // снимает значок сразу, без «призрака» до наведения мыши
        _icon.Dispose();
        _image.Dispose();
        _menuHost?.Close();
        _menuHost = null;
    }

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            _commands.ShowWindows();
        }
        else if (e.Button == Forms.MouseButtons.Right)
        {
            ShowMenu();
        }
    }

    private static Window CreateHost()
    {
        var w = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = null,
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            Title = "KeyOverlay tray menu host",
        };
        w.Show();
        return w;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);
}
