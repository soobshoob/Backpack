using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyOverlay.Visual.Windows;

namespace KeyOverlay.Visual.Presentation;

/// <summary>
/// Единое поведение служебного управления оверлеев (Phase 4G): ручки, кнопки и подписи видны только при наведении на оверлей
/// или на само управление. Наведение определяется опросом положения указателя в процессе Backpack (раз в 40 мс) — сам оверлей
/// не принимает мышь ради этого и по-прежнему пропускает щелчки. Видимость:
/// <list type="bullet">
/// <item>указатель над целью (с полем <see cref="Margin"/>) или над любым окном управления — показать (переход с оверлея на
///       панель не скрывает её: поле и окна управления входят в зону);</item>
/// <item>указатель ушёл — скрыть через <see cref="HideDelay"/> (без мигания);</item>
/// <item><see cref="Hold"/> (перетаскивание, размер, открытое меню) — держать видимым;</item>
/// <item><see cref="Reveal"/> — клавиатурный вызов редактирования: видно без наведения <see cref="EditDuration"/>.</item>
/// </list>
/// Скрытые окна прозрачны для мыши (WS_EX_TRANSPARENT) и не перехватывают щелчки; окна не активируются. Появление — плавное
/// (150 мс), при «меньше движения» — сразу. Положение окон управления задаёт владелец; механизм меняет только видимость.
/// </summary>
public sealed class HoverReveal : IDisposable
{
    public static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(650);
    public static readonly TimeSpan EditDuration = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(150);

    private readonly Func<PixelRect?> _target;
    private readonly Func<IEnumerable<NoActivateWindow>> _controls;
    private readonly Func<bool> _animate;
    private readonly Func<(int X, int Y)> _cursor;
    private readonly DispatcherTimer _timer;
    private DateTime _lastInside = DateTime.MinValue;
    private DateTime _revealUntil = DateTime.MinValue;
    private bool _disposed;

    /// <param name="target">Прямоугольник оверлея (физические пиксели), null — нет цели.</param>
    /// <param name="controls">Окна управления (ручки, кнопки).</param>
    /// <param name="animate">Анимации разрешены (иначе — переход сразу).</param>
    public HoverReveal(Func<PixelRect?> target, Func<IEnumerable<NoActivateWindow>> controls, Func<bool>? animate = null, Func<(int X, int Y)>? cursor = null, Dispatcher? dispatcher = null)
    {
        _target = target;
        _controls = controls;
        _animate = animate ?? (() => SystemParameters.ClientAreaAnimation);
        _cursor = cursor ?? Screens.Cursor;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Background, (_, _) => Tick(), dispatcher ?? Dispatcher.CurrentDispatcher);
        _timer.Start();
        Apply(false, animate: false);
    }

    /// <summary>Поле вокруг цели (физические пиксели), в котором указатель ещё считается «над оверлеем».</summary>
    public int Margin { get; set; } = 28;

    public bool Visible { get; private set; }

    /// <summary>Удерживать видимым (перетаскивание, изменение размера, открытое меню, выбор цвета).</summary>
    public bool Hold { get; set; }

    /// <summary>Клавиатурный вызов: показать без наведения на <see cref="EditDuration"/>.</summary>
    public void Reveal()
    {
        _revealUntil = DateTime.UtcNow + EditDuration;
        Apply(true, _animate());
    }

    /// <summary>Решение видимости (чистая логика для тестов).</summary>
    public static bool ShouldShow(bool pointerInside, DateTime now, DateTime lastInside, DateTime revealUntil, bool hold) =>
        hold || pointerInside || now < revealUntil || now - lastInside < HideDelay;

    /// <summary>Проверка на сейчас (тесты и владелец после перемещения окон).</summary>
    public void Tick()
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var (x, y) = _cursor();
        bool inside = false;
        if (_target() is { } t)
        {
            inside = x >= t.Left - Margin && x < t.Right + Margin && y >= t.Top - Margin && y < t.Bottom + Margin;
        }

        if (!inside)
        {
            foreach (var w in _controls())
            {
                if (w.Hwnd != 0 && PhysicalPlacement.TargetOf(w) is { } r && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom)
                {
                    inside = true;
                    break;
                }
            }
        }

        if (inside)
        {
            _lastInside = now;
        }

        bool show = ShouldShow(inside, now, _lastInside, _revealUntil, Hold);
        if (show != Visible)
        {
            Apply(show, _animate());
        }
        else if (!show)
        {
            Apply(false, animate: false, onlyNew: true); // новые окна управления появляются скрытыми
        }
    }

    private void Apply(bool show, bool animate, bool onlyNew = false)
    {
        Visible = show;
        foreach (var w in _controls())
        {
            if (onlyNew && w.Opacity == 0)
            {
                continue;
            }

            SetVisible(w, show, animate);
        }
    }

    /// <summary>Видимость одного окна управления: прозрачность и пропуск щелчков, без активации.</summary>
    public static void SetVisible(NoActivateWindow w, bool show, bool animate)
    {
        if (w.Hwnd != 0)
        {
            long ex = Screens.ExStyle(w.Hwnd);
            Screens.SetExStyle(w.Hwnd, show ? ex & ~Screens.WsExTransparent : ex | Screens.WsExTransparent);
        }

        w.IsHitTestVisible = show;
        double to = show ? 1 : 0;
        if (animate)
        {
            w.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, FadeDuration));
        }
        else
        {
            w.BeginAnimation(UIElement.OpacityProperty, null);
            w.Opacity = to;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
