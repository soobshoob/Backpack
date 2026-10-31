using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyOverlay.Visual.Localization;
using KeyOverlay.Visual.Settings;

namespace KeyOverlay.Visual.Presentation;

/// <summary>
/// Инструменты презентации: живая линза и временное рисование с панелью и радиальным меню. Переходы — <see cref="PresentationModes"/>;
/// контроллер создаёт и закрывает окна так, чтобы в обычном режиме не оставалось ни одного окна, принимающего мышь.
/// Esc приходит из двух мест (окно рисования, если оно активно, и наблюдение за вводом основного источника) — повтор
/// в пределах 200 мс считается тем же нажатием.
/// </summary>
public sealed class PresentationController : IDisposable
{
    private static readonly TimeSpan EscapeDebounce = TimeSpan.FromMilliseconds(200);

    /// <summary>Повтор снимка раньше этого интервала (удержание F2, двойной щелчок) игнорируется.</summary>
    public static readonly TimeSpan ScreenshotDebounce = TimeSpan.FromMilliseconds(800);

    private readonly Func<LensSettings> _lens;
    private readonly Func<DrawSettings> _draw;
    private readonly Func<(PixelRect Bounds, double Scale)> _monitor;
    private readonly Func<IEnumerable<nint>> _ownWindows;
    private readonly DispatcherTimer _lensTimer;
    private LensSelectionWindow? _selection;
    private LiveLens? _liveLens;
    private DrawingWindow? _drawing;
    private readonly Func<DrawingProfile> _profile;
    private readonly Func<string> _shotFolder;
    private readonly Action<DrawingProfile>? _saveProfile;
    private DateTime _lastEscape;
    private DateTime _lastShot;
    private bool _shotInProgress;
    private bool _disposed;

    /// <param name="monitor">Монитор для окон режима (по умолчанию — под указателем). Тесты передают область за экраном.</param>
    /// <param name="ownWindows">Прочие окна KeyOverlay, которые не нужно показывать в линзе (например, оверлей не исключается — по желанию).</param>
    /// <param name="profile">Поведение рисования из профиля (режим панели, снимок).</param>
    /// <param name="saveProfile">Сохранить изменённое поведение (положение перетащенной панели) в текущие настройки профиля.</param>
    /// <param name="shotFolder">Папка снимков (настройка компьютера).</param>
    public PresentationController(Func<LensSettings> lens, Func<DrawSettings> draw, Func<(PixelRect Bounds, double Scale)>? monitor = null, Func<IEnumerable<nint>>? ownWindows = null,
        Func<DrawingProfile>? profile = null, Action<DrawingProfile>? saveProfile = null, Func<string>? shotFolder = null)
    {
        _lens = lens;
        _draw = draw;
        _profile = profile ?? (() => DrawingProfile.Default);
        _saveProfile = saveProfile;
        _shotFolder = shotFolder ?? (() => AppPreferences.Default.ScreenshotFolderOrDefault);
        _monitor = monitor ?? (() =>
        {
            var (x, y) = Screens.Cursor();
            var m = Screens.MonitorAt(x, y);
            return (m.Bounds, m.Scale);
        });
        _ownWindows = ownWindows ?? (() => []);
        _lensTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _lensTimer.Tick += (_, _) =>
        {
            _lensTimer.Stop();
            Execute(PresentationCommand.LensExpired);
        };
    }

    public PresentationState State { get; private set; } = PresentationState.Normal;

    /// <summary>Запомнить последние значения панели рисования (инструмент, цвет, толщина…) — настройка компьютера.</summary>
    public Action<DrawSettings>? SaveDraw { get; set; }

    /// <summary>Категории общего реестра команд для радиального меню (Фокус, показ, плагины).</summary>
    public Func<IReadOnlyList<RadialNode>>? RadialCatalog { get; set; }

    public event Action<PresentationState>? StateChanged;

    public LensSelectionWindow? Selection => _selection;

    public LiveLens? Lens => _liveLens;

    public DrawingWindow? Drawing => _drawing;

    /// <summary>Окна режимов, которые сейчас существуют (для проверки: в обычном режиме — ни одного).</summary>
    public IEnumerable<Window> OpenWindows => new Window?[] { _selection, _drawing }.Where(w => w is not null).Cast<Window>();

    /// <summary>Есть ли окно, принимающее мышь (не click-through) — в обычном режиме должно быть false.</summary>
    public bool AnyWindowCapturesMouse => _selection is not null || _drawing is { Interactive: true };

    public long LensOpened { get; private set; }

    public long LensUnavailable { get; private set; }

    public long ScreenshotsTaken { get; private set; }

    public long ScreenshotsIgnored { get; private set; }

    /// <summary>Снимок готов (сохранён и/или скопирован) или не удался — для уведомления вне окна рисования.</summary>
    public event Action<ScreenshotResult>? ScreenshotDone;

    /// <summary>Подмена захвата и доставки (тесты): вместо экрана и диска.</summary>
    internal Func<Task<BitmapSource>>? CaptureOverride { get; set; }

    internal Func<BitmapSource, ScreenshotResult>? DeliverOverride { get; set; }

    /// <summary>Поведение рисования изменилось в настройках: открытое окно применяет режим панели сразу.</summary>
    public void ApplyDrawingProfile(DrawingProfile p)
    {
        if (_drawing is { } d && (d.Profile.ToolbarMode != p.ToolbarMode || d.Profile.AutoHideEdge != p.AutoHideEdge
            || d.Profile.ToolbarX != p.ToolbarX || d.Profile.ToolbarY != p.ToolbarY))
        {
            d.ApplyProfile(p);
        }
    }

    public void Escape()
    {
        var now = DateTime.UtcNow;
        if (now - _lastEscape < EscapeDebounce)
        {
            return;
        }

        _lastEscape = now;
        if (State.Radial && _drawing?.RadialBack() == true)
        {
            return; // Esc закрывает сначала внешнее кольцо радиального меню, затем само меню
        }

        if (_drawing is { IsEditingText: true } d && !State.Radial)
        {
            d.CancelText(); // Esc при вводе текста отменяет только текст, рисование остаётся
            return;
        }

        Execute(PresentationCommand.Escape);
    }

    /// <summary>
    /// Снимок экрана в режиме рисования (кнопка, радиальное меню, горячая клавиша). Вне режима ничего не делает. Повтор во время
    /// снимка или раньше <see cref="ScreenshotDebounce"/> игнорируется. После успеха — действие из профиля; при ошибке рисунки остаются.
    /// </summary>
    public async Task<ScreenshotResult?> TakeScreenshotAsync()
    {
        var now = DateTime.UtcNow;
        if (_drawing is null || _shotInProgress || now - _lastShot < ScreenshotDebounce)
        {
            ScreenshotsIgnored++;
            return null;
        }

        _shotInProgress = true;
        _lastShot = now;
        var window = _drawing;
        ScreenshotResult result;
        try
        {
            if (State.Radial)
            {
                var b = State;
                State = b with { Radial = false };
                window.HideRadial();
                StateChanged?.Invoke(State);
            }

            var image = await (CaptureOverride ?? window.CaptureAsync)();
            var profile = _profile();
            result = DeliverOverride?.Invoke(image) ?? ScreenshotSaver.Deliver(image, profile.ShotAction, _shotFolder(), DateTime.Now);
        }
        catch (InvalidOperationException ex)
        {
            result = new ScreenshotResult(false, null, false, ex.GetType().Name);
        }
        finally
        {
            _shotInProgress = false;
            _lastShot = DateTime.UtcNow;
        }

        if (result.Ok)
        {
            ScreenshotsTaken++;
        }

        string message = !result.Ok ? Loc.F("Draw.ShotFailed", result.Error ?? "?")
            : result.FilePath is { } path && result.Copied ? Loc.F("Draw.ShotSavedCopied", System.IO.Path.GetFileName(path))
            : result.FilePath is { } p ? Loc.F("Draw.ShotSaved", System.IO.Path.GetFileName(p))
            : Loc.T("Draw.ShotCopied");
        if (!ReferenceEquals(window, _drawing))
        {
            ScreenshotDone?.Invoke(result); // режим уже закрыт другим путём
            return result;
        }

        switch (result.Ok ? _profile().AfterShot : AfterScreenshot.KeepDrawings)
        {
            case AfterScreenshot.ClearDrawings:
                window.Clear();
                window.ShowToast(message);
                break;
            case AfterScreenshot.ExitDrawing:
                Execute(PresentationCommand.Draw);
                break;
            default:
                window.ShowToast(message);
                break;
        }

        ScreenshotDone?.Invoke(result);
        return result;
    }

    public void Execute(PresentationCommand command, PixelRect? selected = null)
    {
        if (_disposed)
        {
            return;
        }

        var before = State;
        var after = PresentationModes.Next(before, command);
        if (command == PresentationCommand.SelectionDone && after.Lens && selected is { IsEmpty: false } rect)
        {
            OpenLens(rect);
        }

        Apply(before, after);
        State = after;
        if (before != after)
        {
            StateChanged?.Invoke(after);
        }
    }

    private void Apply(PresentationState before, PresentationState after)
    {
        // Выбор области.
        if (after.Selecting && _selection is null)
        {
            var (bounds, scale) = _monitor();
            var l = _lens();
            _selection = new LensSelectionWindow(bounds, scale, new Size(l.DefaultWidth, l.DefaultHeight));
            _selection.Selected += r => Execute(PresentationCommand.SelectionDone, r);
            _selection.Cancelled += () => Execute(PresentationCommand.SelectionCancelled);
            _selection.Show();
            if (!InteractiveOverlayWindow.TestNoActivate)
            {
                _selection.Activate();
            }
        }
        else if (!after.Selecting && _selection is not null)
        {
            var s = _selection;
            _selection = null;
            s.Close();
        }

        // Линза: закрытие (открытие — по завершении выбора).
        if (!after.Lens && _liveLens is not null)
        {
            CloseLens();
        }

        // Рисование.
        if (after.Drawing && _drawing is null)
        {
            var (bounds, scale) = _monitor();
            _drawing = new DrawingWindow(bounds, scale, _draw(), _profile());
            _drawing.EscapeRequested += Escape;
            _drawing.ExitRequested += () => Execute(PresentationCommand.Draw);
            _drawing.ScreenshotRequested += () => _ = TakeScreenshotAsync();
            _drawing.RadialRequested += () =>
            {
                if (!State.Radial)
                {
                    Execute(PresentationCommand.Radial);
                }
            };
            _drawing.ToolbarMoved += (x, y) => _saveProfile?.Invoke(_profile() with { ToolbarX = x, ToolbarY = y });
            _drawing.CompactChanged += compact => _saveProfile?.Invoke(_profile() with { Compact = compact });
            _drawing.SettingsChanged += s => SaveDraw?.Invoke(s);
            _drawing.RadialCatalog = RadialCatalog;
            _drawing.RadialClosed += () =>
            {
                if (State.Radial)
                {
                    var b = State;
                    State = b with { Radial = false };
                    StateChanged?.Invoke(State);
                }
            };
            _drawing.Show();
            _liveLens?.UpdateExclusions();
        }
        else if (!after.Drawing && _drawing is not null)
        {
            var d = _drawing;
            _drawing = null;
            d.Close(); // рисунки не сохраняются
            _liveLens?.UpdateExclusions();
        }

        if (_drawing is not null)
        {
            _drawing.SetInteractive(after.DrawingInteractive);
            if (after.Radial && !before.Radial)
            {
                _drawing.ShowRadial(_drawing.CursorPoint());
            }
            else if (!after.Radial && _drawing.RadialOpen)
            {
                _drawing.HideRadial();
            }
        }

        if (_selection is not null && after.Selecting && !InteractiveOverlayWindow.TestNoActivate)
        {
            _selection.Activate();
        }
    }

    private void OpenLens(PixelRect target)
    {
        CloseLens();
        var settings = _lens();
        var (_, scale) = _monitor();
        _liveLens = new LiveLens(settings, target, scale, () => _ownWindows().Concat(_drawing?.Hwnd is > 0 and var h ? [h] : []));
        LensOpened++;
        _liveLens.Open();
        if (_liveLens.Status != LensStatus.Live)
        {
            LensUnavailable++;
        }

        if (settings.DurationSec > 0)
        {
            _lensTimer.Interval = TimeSpan.FromSeconds(settings.DurationSec);
            _lensTimer.Start();
        }
    }

    private void CloseLens()
    {
        _lensTimer.Stop();
        _liveLens?.Dispose();
        _liveLens = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Execute(PresentationCommand.ExitAll);
        _disposed = true;
        CloseLens();
        StateChanged = null;
    }
}
