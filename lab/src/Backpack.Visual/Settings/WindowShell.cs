using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Backpack.Visual.Localization;

namespace Backpack.Visual.Settings;

/// <summary>
/// Оболочка окна Backpack (шаблон <c>Ko.Window</c>): полоса заголовка — часть окна, кнопки окна нарисованы приложением.
/// Штатное поведение Windows сохраняется: перетаскивание, двойной щелчок, системное меню (Alt+Space, правый щелчок по
/// заголовку) и изменение размера — через <see cref="System.Windows.Shell.WindowChrome"/>; кнопка «Развернуть» отвечает на
/// WM_NCHITTEST как HTMAXBUTTON, поэтому в Windows 11 работает меню Snap Layouts. Развёрнутое окно получает отступ рамки,
/// чтобы содержимое не выходило за монитор. Материал окна (Mica/Acrylic) задаётся атрибутами DWM, см. <see cref="UiTheme"/>.
/// </summary>
public static partial class WindowShell
{
    private const int WmNcHitTest = 0x0084, WmNcMouseMove = 0x00A0, WmNcLButtonDown = 0x00A1, WmNcLButtonUp = 0x00A2, WmNcRButtonUp = 0x00A5, WmNcMouseLeave = 0x02A2;
    private const int WmStyleChanging = 0x007C;
    private const int HtCaption = 2, HtMaxButton = 9;
    private const long WsSysMenu = 0x00080000;

    /// <summary>
    /// Встроенный заголовок (Phase 4H, главное окно): отдельной полосы с маленьким значком и названием нет — название окна
    /// остаётся системным (Alt+Tab, доступность), а кнопки окна стоят справа в верхней строке интерфейса. Верхние
    /// <see cref="CaptionHeight"/> DIP окна по-прежнему — область заголовка (перетаскивание, двойной щелчок, системное меню);
    /// элементы управления в ней помечаются <see cref="System.Windows.Shell.WindowChrome.IsHitTestVisibleInChrome"/>.
    /// </summary>
    public static readonly DependencyProperty IntegratedTitleProperty = DependencyProperty.RegisterAttached(
        "IntegratedTitle", typeof(bool), typeof(WindowShell), new FrameworkPropertyMetadata(false));

    public static void SetIntegratedTitle(Window w, bool value) => w.SetValue(IntegratedTitleProperty, value);

    public static bool GetIntegratedTitle(Window w) => (bool)w.GetValue(IntegratedTitleProperty);

    /// <summary>Высота области заголовка (WindowChrome.CaptionHeight в стиле Ko.Window).</summary>
    public const double CaptionHeight = 44;

    /// <summary>Ширина трёх кнопок окна (46 DIP каждая) с отступом: место, которое страница оставляет справа сверху.</summary>
    public const double CaptionButtonsWidth = 146;

    /// <summary>Нижний край кнопок окна (отступ 4 + высота 32), DIP от верха окна.</summary>
    public const double CaptionButtonsBottom = 36;

    /// <summary>Элемент в области заголовка принимает щелчки (кнопка, пункт меню), а не перетаскивает окно.</summary>
    public static T Interactive<T>(T element)
        where T : DependencyObject
    {
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome((IInputElement)element, true);
        return element;
    }

    /// <summary>Подключить поведение к окну со стилем <c>Ko.Window</c> (вызывает <see cref="UiTheme.Attach"/>).</summary>
    internal static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                source.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) => WndProc(window, msg, w, l, ref handled));
                HideSystemCaptionButtons(source.Handle);
            }

            ApplyMaterial(window);
        };
        // Без WS_SYSMENU системное меню и Alt+F4 обслуживаются здесь (поведение Windows сохраняется).
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.System && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Alt)
            {
                if (e.SystemKey == System.Windows.Input.Key.Space)
                {
                    e.Handled = true;
                    ShowSystemMenuAtCaption(window);
                }
                else if (e.SystemKey == System.Windows.Input.Key.F4)
                {
                    e.Handled = true;
                    window.Close();
                }
            }
        };
        window.StateChanged += (_, _) => FitMaximized(window);
        window.DpiChanged += (_, _) => FitMaximized(window);
        window.Loaded += (_, _) =>
        {
            Wire(window);
            FitMaximized(window);
        };
    }

    private static T? Part<T>(Window w, string name)
        where T : class => w.Template?.FindName(name, w) as T;

    private static void Wire(Window w)
    {
        w.ApplyTemplate();
        if (Part<Button>(w, "PART_Minimize") is { } min && min.Tag is not "wired")
        {
            min.Tag = "wired";
            min.Click += (_, _) => w.WindowState = WindowState.Minimized;
            min.ToolTip = Loc.T("Window.Minimize");
            System.Windows.Automation.AutomationProperties.SetName(min, Loc.T("Window.Minimize"));
        }

        if (Part<Button>(w, "PART_Maximize") is { } max && max.Uid != "wired")
        {
            max.Uid = "wired";
            max.Click += (_, _) => ToggleMaximize(w);
            System.Windows.Automation.AutomationProperties.SetName(max, Loc.T("Window.Maximize"));
        }

        if (Part<Button>(w, "PART_Close") is { } close && close.Tag is not "wired")
        {
            close.Tag = "wired";
            close.Click += (_, _) => w.Close();
            close.ToolTip = Loc.T("Window.Close");
            System.Windows.Automation.AutomationProperties.SetName(close, Loc.T("Window.Close"));
        }

        if (Part<Border>(w, "PART_Logo") is { } logo && logo.Child is null)
        {
            logo.Child = Brand.LogoArt.Themed(18);
        }
    }

    /// <summary>
    /// DWM рисует собственные кнопки заголовка в расширенной рамке (она нужна для Mica/Acrylic) только у окон со стилем
    /// WS_SYSMENU — иначе они просвечивали бы под кнопками Backpack. Стиль снимается; системное меню остаётся: правый щелчок по
    /// заголовку и Alt+Space открывают его через <see cref="SystemCommands.ShowSystemMenu"/>, Alt+F4 закрывает окно.
    /// </summary>
    private static void HideSystemCaptionButtons(nint hwnd)
    {
        long style = (long)GetWindowLongPtr(hwnd, -16 /* GWL_STYLE */);
        if ((style & WsSysMenu) != 0)
        {
            _ = SetWindowLongPtr(hwnd, -16, (nint)(style & ~WsSysMenu));
        }
    }

    private static void ShowSystemMenuAtCaption(Window w)
    {
        var screen = w.PointToScreen(new Point(8, 36)); // под левым краем полосы заголовка
        SystemCommands.ShowSystemMenu(w, new Point(screen.X / DpiScale(w), screen.Y / DpiScale(w)));
    }

    private static double DpiScale(Window w) => VisualTreeHelper.GetDpi(w).DpiScaleX;

    private static void ToggleMaximize(Window w) => w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>Развёрнутое окно Windows располагает за краем монитора на толщину рамки — содержимое сдвигается внутрь.</summary>
    private static void FitMaximized(Window w)
    {
        if (Part<Border>(w, "PART_Root") is not { } root)
        {
            return;
        }

        if (w.WindowState != WindowState.Maximized)
        {
            root.Padding = new Thickness(0);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(w);
        uint d = (uint)Math.Round(96 * dpi.DpiScaleX);
        int frame = GetSystemMetricsForDpi(32 /* SM_CXSIZEFRAME */, d) + GetSystemMetricsForDpi(92 /* SM_CXPADDEDBORDER */, d);
        double t = frame / dpi.DpiScaleX;
        root.Padding = new Thickness(t);
    }

    /// <summary>
    /// Кнопка «Развернуть» как HTMAXBUTTON (Snap Layouts). Пока указатель над ней, Windows присылает не-клиентские сообщения:
    /// состояние наведения и нажатия кнопки отражается вручную, щелчок выполняется по отпусканию над кнопкой.
    /// </summary>
    private static IntPtr WndProc(Window w, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmStyleChanging when wParam == -16:
                // WindowChrome и смена состояния окна могут вернуть WS_SYSMENU — снова снимаем.
                unsafe
                {
                    var st = (StyleStruct*)lParam;
                    st->NewStyle &= ~(uint)WsSysMenu;
                }

                break;
            case WmNcRButtonUp when wParam == HtCaption:
                handled = true;
                int sx = unchecked((short)(long)lParam), sy = unchecked((short)((long)lParam >> 16));
                SystemCommands.ShowSystemMenu(w, new Point(sx / DpiScale(w), sy / DpiScale(w)));
                break;
            case WmNcHitTest when OverMaximize(w, lParam):
                handled = true;
                SetMaxVisual(w, "hover");
                return HtMaxButton;
            case WmNcHitTest:
                SetMaxVisual(w, null);
                break;
            case WmNcMouseMove when wParam == HtMaxButton:
                SetMaxVisual(w, "hover");
                break;
            case WmNcLButtonDown when wParam == HtMaxButton:
                handled = true;
                SetMaxVisual(w, "pressed");
                break;
            case WmNcLButtonUp when wParam == HtMaxButton:
                handled = true;
                SetMaxVisual(w, null);
                ToggleMaximize(w);
                break;
            case WmNcMouseLeave:
                SetMaxVisual(w, null);
                break;
        }

        return IntPtr.Zero;
    }

    private static bool OverMaximize(Window w, IntPtr lParam)
    {
        if (Part<Button>(w, "PART_Maximize") is not { IsVisible: true } max)
        {
            return false;
        }

        int x = unchecked((short)(long)lParam), y = unchecked((short)((long)lParam >> 16));
        try
        {
            var p = max.PointFromScreen(new Point(x, y));
            return p.X >= 0 && p.Y >= 0 && p.X < max.ActualWidth && p.Y < max.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void SetMaxVisual(Window w, string? state)
    {
        if (Part<Button>(w, "PART_Maximize") is { } max && !Equals(max.Tag, state))
        {
            max.Tag = state;
        }
    }

    // ── материал ─────────────────────────────────────────────────────────────

    /// <summary>Применить атрибуты DWM текущего материала и темы (тёмный режим заголовка, фон-подложка, скругление углов).</summary>
    public static void ApplyMaterial(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
        {
            return;
        }

        int dark = UiTheme.IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref dark, sizeof(int));
        int corner = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref corner, sizeof(int));
        // Neon: тонкая рамка окна цвета свечения (Windows 11; на Windows 10 атрибут не поддерживается и игнорируется).
        // Остальные стили — системный цвет рамки (DWMWA_COLOR_DEFAULT).
        int edge = UiTheme.WindowEdge is { } c ? c.R | (c.G << 8) | (c.B << 16) : unchecked((int)0xFFFFFFFF);
        _ = DwmSetWindowAttribute(hwnd, 34 /* DWMWA_BORDER_COLOR */, ref edge, sizeof(int));
        var material = UiTheme.EffectiveMaterial;
        if (UiTheme.SystemBackdropSupported)
        {
            int backdrop = material switch { WindowMaterial.Mica => 2, WindowMaterial.Acrylic => 3, _ => 1 };
            _ = DwmSetWindowAttribute(hwnd, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref backdrop, sizeof(int));
        }

        if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
        {
            // Прозрачная очистка кадра — подложка DWM видна сквозь полупрозрачные поверхности; при непрозрачном материале фон
            // окна сплошной, поэтому прозрачная очистка ничего не меняет.
            target.BackgroundColor = material == WindowMaterial.Opaque ? Colors.Black : Colors.Transparent;
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct StyleStruct
    {
        public uint OldStyle;
        public uint NewStyle;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);
}
