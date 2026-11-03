using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Backpack.Sdk;
using Backpack.Visual.Localization;
using Backpack.Visual.Plugins;
using Backpack.Visual.Windows;

namespace Backpack.Visual.Presentation;

/// <summary>
/// Окружение снимка: мониторы, указатель, наблюдение за кнопками мыши, активным окном и вводом текста, захват экрана, Space над
/// снимком. По умолчанию — Win32; тесты подменяют.
/// </summary>
public sealed class SnapshotEnvironment
{
    public Func<IReadOnlyList<MonitorDesc>> Monitors { get; init; } = DisplayMonitors.Enumerate;

    public Func<(int X, int Y)> Cursor { get; init; } = Screens.Cursor;

    public Func<bool> ButtonsDown { get; init; } = SnapshotNative.AnyMouseButtonDown;

    public Func<bool> Typing { get; init; } = SnapshotNative.IsTyping;

    public Func<nint> Foreground { get; init; } = SnapshotNative.Foreground;

    /// <summary>Захват области (физические пиксели) в память.</summary>
    public Func<PixelRect, BitmapSource> Capture { get; init; } = ScreenCapture.Capture;

    public Func<SnapshotHoverKey> HoverKey { get; init; } = () => new SnapshotHoverKey();

    public Func<bool> Animate { get; init; } = () => SystemParameters.ClientAreaAnimation;

    /// <summary>Пауза после закрытия окна выбора: кадр без него должен дойти до экрана (композиция DWM), как у снимка рисования.</summary>
    public TimeSpan CaptureDelay { get; init; } = TimeSpan.FromMilliseconds(90);

    /// <summary>Период наблюдения (наведение для Space, щелчок вне развёрнутого снимка). Таймер работает, только пока снимок виден.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(40);
}

/// <summary>
/// Снимок области экрана (SDK 1.3, overlay.snapshot): выделение, захват в память, плавающее окно поверх остальных, временное
/// развёртывание и свёртывание. Состояния — <see cref="SnapshotState"/>:
/// <list type="bullet">
/// <item><b>Selecting</b> — окно выбора области (как у «Линзы»); текущий снимок на это время скрыт и в новый не попадает. Esc или
///       правая кнопка — отмена: прежний снимок возвращается, без него снимок закрывается (cancelled).</item>
/// <item><b>Normal</b> — снимок в своём месте и размере (<see cref="NormalRect"/>): перетаскивание за изображение или ручку перемещает,
///       ручка угла меняет размер (единый механизм ручек «Фокуса» и «Линзы», видны при наведении); место сохраняется хостом.</item>
/// <item><b>Expanded</b> — временно почти на весь экран (<see cref="SnapshotGeometry.Expanded"/>). Обычный прямоугольник при этом не
///       меняется и не сохраняется заново: свёртывание (двойной щелчок, Space, команда, Esc, щелчок вне снимка, смена активного окна)
///       возвращает его точно.</item>
/// <item><b>Hidden</b> — окна скрыты, изображение в памяти.</item>
/// </list>
/// Изображение хранится только в памяти этого объекта и освобождается при удалении; на диск не пишется, плагину не передаётся.
/// Каждый экземпляр — один независимый снимок: несколько снимков — несколько сессий движка инструментов.
/// </summary>
public sealed class SnapshotController : IDisposable
{
    private readonly Func<SnapshotRequest> _request;
    private readonly SnapshotEnvironment _env;
    private readonly DispatcherTimer _poll;
    private SnapshotFrame? _frame;
    private SnapshotWindow? _window;
    private PanelButtonsWindow? _buttons;
    private FocusGrip? _move;
    private FocusGrip? _resize;
    private HoverReveal? _reveal;
    private LensSelectionWindow? _selection;
    private SnapshotHoverKey? _hoverKey;
    private PixelRect? _saved;
    private PixelRect _expanded;
    private PixelRect? _dragStart;
    private (bool Resize, int Dx, int Dy)? _pendingDrag;
    private SnapshotState _beforeCapture;
    private nint _foregroundAtExpand;
    private bool _buttonsBefore;
    private int _generation;
    private bool _disposed;

    /// <param name="request">Текущие параметры (обновляются плагином).</param>
    /// <param name="saved">Сохранённое место обычного вида (физические пиксели) или null.</param>
    public SnapshotController(Func<SnapshotRequest> request, SnapshotEnvironment? env = null, PixelRect? saved = null)
    {
        _request = request;
        _env = env ?? new SnapshotEnvironment();
        _saved = saved;
        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = _env.PollInterval };
        _poll.Tick += (_, _) => Tick();
    }

    public SnapshotState State { get; private set; } = SnapshotState.None;

    /// <summary>Изображение снимка (в памяти) или null.</summary>
    public BitmapSource? Image { get; private set; }

    /// <summary>Размер изображения (физические пиксели при захвате).</summary>
    public (int Width, int Height) ImageSize => Image is { } i ? (i.PixelWidth, i.PixelHeight) : (0, 0);

    /// <summary>Место и размер обычного вида (физические пиксели). Развёртывание его не меняет.</summary>
    public PixelRect NormalRect { get; private set; }

    /// <summary>Видимый прямоугольник: развёрнутый или обычный.</summary>
    public PixelRect Rect => State == SnapshotState.Expanded ? _expanded : NormalRect;

    /// <summary>Сохранённое место (последнее, что записал <see cref="PlaceChanged"/>).</summary>
    public PixelRect? SavedPlace => _saved;

    public SnapshotWindow? Window => _window;

    public SnapshotFrame? Frame => _frame;

    public PanelButtonsWindow? ButtonsWindow => _buttons;

    public LensSelectionWindow? Selection => _selection;

    public SnapshotHoverKey? HoverKey => _hoverKey;

    public bool ControlsVisible => _reveal?.Visible == true;

    public FocusGrip? GripOf(FocusGripKind kind) => kind == FocusGripKind.Resize ? _resize : kind == FocusGripKind.Move ? _move : null;

    /// <summary>Окна управления (ручки, кнопки) — исключаются из изображения «Линзы».</summary>
    public IEnumerable<nint> ControlHandles() =>
        new NoActivateWindow?[] { _move, _resize, _buttons }.Where(w => w is { Hwnd: not 0 }).Select(w => w!.Hwnd);

    /// <summary>Esc относится к снимку только во время выбора области и в развёрнутом виде.</summary>
    public bool WantsEscape => State is SnapshotState.Selecting or SnapshotState.Expanded;

    public event Action<SnapshotState>? StateChanged;

    /// <summary>Пользователь изменил место или размер обычного вида; null — место сброшено.</summary>
    public event Action<PixelRect?>? PlaceChanged;

    /// <summary>Снимок закрылся сам: cancelled (выбор отменён без снимка), cleared (кнопка «Удалить»), unavailable (захват не удался).</summary>
    public event Action<string>? Closed;

    private SnapshotAspect Aspect => _request().Aspect switch { "fit" => SnapshotAspect.Fit, "stretch" => SnapshotAspect.Stretch, _ => SnapshotAspect.Keep };

    private bool KeepAspect => Aspect == SnapshotAspect.Keep;

    // ── выбор и захват ─────────────────────────────────────────────────────

    /// <summary>Начать выбор области на мониторе под указателем. Текущий снимок скрывается до конца выбора.</summary>
    public void StartCapture()
    {
        if (_disposed || State == SnapshotState.Selecting)
        {
            return;
        }

        if (State == SnapshotState.Expanded)
        {
            Collapse();
        }

        _beforeCapture = Image is null ? SnapshotState.None : State == SnapshotState.Hidden ? SnapshotState.Hidden : SnapshotState.Normal;
        HideWindows();
        var monitor = MonitorResolver.UnderCursor(_env.Monitors(), _env.Cursor());
        if (monitor is null)
        {
            CancelCapture();
            return;
        }

        _selection = new LensSelectionWindow(monitor.Bounds, monitor.Scale, new Size(320, 200), Loc.T("Snapshot.SelectHint"), _request().SelectionDim);
        _selection.Selected += r => _ = CaptureAreaAsync(r);
        _selection.Cancelled += CancelCapture;
        _selection.Show();
        if (!InteractiveOverlayWindow.TestNoActivate)
        {
            _selection.Activate();
        }

        SetState(SnapshotState.Selecting);
    }

    /// <summary>Отмена выбора: прежний снимок возвращается; без него снимок закрывается (cancelled).</summary>
    public void CancelCapture()
    {
        if (State != SnapshotState.Selecting && _selection is null)
        {
            return;
        }

        _generation++;
        CloseSelection();
        if (Image is null || _beforeCapture == SnapshotState.None)
        {
            Close("cancelled");
            return;
        }

        SetState(_beforeCapture);
        Render();
    }

    /// <summary>
    /// Снять область <paramref name="area"/> (физические пиксели): окно выбора закрывается, после отрисовки кадра без него — захват
    /// в память и показ. Ошибка захвата оставляет прежний снимок; без него снимок закрывается (unavailable).
    /// </summary>
    public async Task<bool> CaptureAreaAsync(PixelRect area)
    {
        int gen = ++_generation;
        CloseSelection();
        if (area.IsEmpty || _disposed)
        {
            CancelCaptureAfterSelection();
            return false;
        }

        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (_env.CaptureDelay > TimeSpan.Zero)
        {
            await Task.Delay(_env.CaptureDelay);
        }

        if (gen != _generation || _disposed)
        {
            return false;
        }

        BitmapSource image;
        try
        {
            image = _env.Capture(area);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException or OutOfMemoryException or ExternalException)
        {
            if (Image is not null && _beforeCapture != SnapshotState.None)
            {
                SetState(_beforeCapture);
                Render();
            }
            else
            {
                Close("unavailable");
            }

            return false;
        }

        Present(image, area);
        return true;
    }

    private void CancelCaptureAfterSelection()
    {
        if (State == SnapshotState.Selecting)
        {
            CancelCapture();
        }
    }

    /// <summary>
    /// Показать снятое изображение (также для тестов без экрана): прежнее изображение освобождается, место — по настройке
    /// (на месте прежнего, в углу или поверх снятой области).
    /// </summary>
    public void Present(BitmapSource image, PixelRect area)
    {
        if (_disposed)
        {
            return;
        }

        _generation++;
        CloseSelection();
        bool replacing = Image is not null && !NormalRect.IsEmpty;
        _window?.ReleaseImage();
        Image = image;
        var req = _request();
        var place = req.Place switch { "corner" => SnapshotPlace.Corner, "capture" => SnapshotPlace.Capture, _ => SnapshotPlace.Last };
        PixelRect? last = replacing ? NormalRect : _saved;
        var anchor = place == SnapshotPlace.Last && last is { } l ? Center(l) : Center(area);
        var m = MonitorAt(anchor);
        var work = WorkOf(m, area);
        double scale = m?.Scale ?? 1;
        var limits = SnapshotLimits.For(work, scale, req.MinSize, req.MaxSize);
        NormalRect = SnapshotGeometry.Start(place, req.Corner, image.PixelWidth, image.PixelHeight, area, last, work, scale, req.StartSize, req.EdgeMargin, KeepAspect, limits);
        EnsureWindows();
        _window!.SetImage(image);
        SetState(SnapshotState.Normal);
        Render();
        SavePlace();
    }

    private void CloseSelection()
    {
        var s = _selection;
        _selection = null;
        s?.Close();
    }

    // ── видимость, развёртывание ───────────────────────────────────────────

    public void Show()
    {
        if (State == SnapshotState.Hidden && Image is not null)
        {
            SetState(SnapshotState.Normal);
            Render();
        }
    }

    public void Hide()
    {
        if (State is SnapshotState.Normal or SnapshotState.Expanded)
        {
            if (State == SnapshotState.Expanded)
            {
                SetState(SnapshotState.Normal);
            }

            HideWindows();
            SetState(SnapshotState.Hidden);
        }
    }

    public void ToggleVisible()
    {
        if (State == SnapshotState.Hidden)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>Развернуть: обычный прямоугольник запоминается (не меняется), развёрнутый — на мониторе по настройке.</summary>
    public void Expand()
    {
        if (State != SnapshotState.Normal || Image is null)
        {
            return;
        }

        FlushPendingDrag();
        _expanded = ComputeExpanded();
        _foregroundAtExpand = _env.Foreground();
        _buttonsBefore = _env.ButtonsDown(); // нажатие, которое развернуло снимок, не считается щелчком вне него
        SetState(SnapshotState.Expanded);
        Render();
    }

    /// <summary>Свернуть: снимок возвращается точно в обычное место и размер.</summary>
    public void Collapse()
    {
        if (State != SnapshotState.Expanded)
        {
            return;
        }

        SetState(SnapshotState.Normal);
        Render();
    }

    public void ToggleExpand()
    {
        if (State == SnapshotState.Expanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    /// <summary>Esc (наблюдение, ввод не подавляется): из выбора — отмена, из развёрнутого — свернуть. В обычном виде Esc снимок не трогает.</summary>
    public bool Escape()
    {
        switch (State)
        {
            case SnapshotState.Selecting:
                CancelCapture();
                return true;
            case SnapshotState.Expanded:
                Collapse();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Двойной щелчок по снимку (окно или тесты).</summary>
    public void DoubleClick()
    {
        if (_request().DoubleClick)
        {
            ToggleExpand();
        }
    }

    /// <summary>Блокировка, сон, смена рабочего стола: выбор отменяется, развёрнутый снимок сворачивается; снимок остаётся.</summary>
    public void Suspend()
    {
        if (State == SnapshotState.Selecting)
        {
            CancelCapture();
        }
        else if (State == SnapshotState.Expanded)
        {
            Collapse();
        }
    }

    /// <summary>Забыть сохранённое место: снимок — в угол по настройкам.</summary>
    public void ResetPlace()
    {
        _saved = null;
        PlaceChanged?.Invoke(null);
        if (Image is null || NormalRect.IsEmpty)
        {
            return;
        }

        var req = _request();
        var m = MonitorAt(Center(NormalRect));
        var work = WorkOf(m, NormalRect);
        double scale = m?.Scale ?? 1;
        NormalRect = SnapshotGeometry.Start(SnapshotPlace.Corner, req.Corner, ImageSize.Width, ImageSize.Height, NormalRect, null, work, scale, req.StartSize, req.EdgeMargin,
            true, SnapshotLimits.For(work, scale, req.MinSize, req.MaxSize));
        if (State == SnapshotState.Expanded)
        {
            _expanded = ComputeExpanded();
        }

        Render();
    }

    /// <summary>Новые параметры (оформление, пределы, пропорции) — на месте, без нового захвата.</summary>
    public void ApplySettings()
    {
        if (Image is null || NormalRect.IsEmpty || State is SnapshotState.None or SnapshotState.Selecting)
        {
            return;
        }

        var before = NormalRect;
        NormalRect = FitOnMonitor(NormalRect);
        if (State == SnapshotState.Expanded)
        {
            _expanded = ComputeExpanded();
        }

        if (!_request().SpaceHover)
        {
            DisposeHoverKey();
        }

        Render();
        if (NormalRect != before)
        {
            SavePlace();
        }
    }

    /// <summary>Клавиатурный вызов редактирования: ручки и кнопки видны без наведения.</summary>
    public void RevealControls() => _reveal?.Reveal();

    // ── перемещение и размер ───────────────────────────────────────────────

    /// <summary>Перетаскивание (ручка, изображение, тесты): смещение от начала, физические пиксели. Только в обычном виде.</summary>
    public void DragBy(bool resize, int dx, int dy)
    {
        if (State != SnapshotState.Normal || Image is null)
        {
            return;
        }

        _dragStart ??= NormalRect;
        if (_reveal is not null)
        {
            _reveal.Hold = true;
        }

        var start = _dragStart.Value;
        if (resize)
        {
            var m = MonitorAt(Center(start));
            var work = WorkOf(m, start);
            var req = _request();
            NormalRect = SnapshotGeometry.Resize(start, dx, dy, ImageSize.Width, ImageSize.Height, KeepAspect,
                SnapshotLimits.For(work, m?.Scale ?? 1, req.MinSize, req.MaxSize), m?.Bounds ?? work);
        }
        else
        {
            NormalRect = SnapshotGeometry.Move(start, dx, dy, VirtualBounds());
        }

        Render();
    }

    /// <summary>Конец перетаскивания: перенос на монитор центра (размер в DIP при другом масштабе), целиком в рабочей области, сохранить.</summary>
    public void EndDrag()
    {
        FlushPendingDrag();
        if (_reveal is not null)
        {
            _reveal.Hold = false;
        }

        if (_dragStart is not { } start)
        {
            return;
        }

        _dragStart = null;
        if (State != SnapshotState.Normal)
        {
            return;
        }

        var from = MonitorAt(Center(start));
        var to = MonitorAt(Center(NormalRect));
        if (from is not null && to is not null && from.Id != to.Id && _request().KeepDip && Math.Abs(to.Scale - from.Scale) > 0.001)
        {
            var req = _request();
            NormalRect = SnapshotGeometry.Rescale(NormalRect, to.Scale / from.Scale, ImageSize.Width, ImageSize.Height, KeepAspect,
                SnapshotLimits.For(to.Work, to.Scale, req.MinSize, req.MaxSize), to.Work);
        }

        NormalRect = FitOnMonitor(NormalRect);
        Render();
        SavePlace();
    }

    /// <summary>
    /// События мыши приходят чаще кадров: запоминается последнее смещение, применяется один раз перед отрисовкой кадра (как у ручек
    /// «Фокуса»). Смещение абсолютное от начала перетаскивания — пропуски ничего не теряют.
    /// </summary>
    private void QueueDrag(bool resize, int dx, int dy)
    {
        bool scheduled = _pendingDrag is not null;
        _pendingDrag = (resize, dx, dy);
        if (!scheduled)
        {
            CompositionTarget.Rendering += ApplyPendingDrag;
        }
    }

    private void ApplyPendingDrag(object? sender, EventArgs e) => FlushPendingDrag();

    private void FlushPendingDrag()
    {
        if (_pendingDrag is not { } p)
        {
            return;
        }

        CompositionTarget.Rendering -= ApplyPendingDrag;
        _pendingDrag = null;
        DragBy(p.Resize, p.Dx, p.Dy);
    }

    private void DropPendingDrag()
    {
        if (_pendingDrag is not null)
        {
            CompositionTarget.Rendering -= ApplyPendingDrag;
            _pendingDrag = null;
        }
    }

    // ── наблюдение: Space над снимком и автоматическое свёртывание ─────────

    /// <summary>
    /// Раз в <see cref="SnapshotEnvironment.PollInterval"/>, пока снимок виден: Space перехватывается, только пока указатель над
    /// снимком, кнопки мыши отпущены и в активном окне нет ввода текста; в развёрнутом виде новое нажатие кнопки мыши вне снимка
    /// и его управления или смена активного окна сворачивает снимок (если включено). Ввод не подавляется — щелчок вне снимка
    /// доходит до программы.
    /// </summary>
    public void Tick()
    {
        if (_disposed || State is not (SnapshotState.Normal or SnapshotState.Expanded))
        {
            DisposeHoverKey();
            return;
        }

        var req = _request();
        var p = _env.Cursor();
        bool inside = SnapshotGeometry.Contains(Rect, p);
        bool buttons = _env.ButtonsDown();
        bool arm = req.SpaceHover && inside && !buttons && !_env.Typing();
        if (arm && _hoverKey is null)
        {
            _hoverKey = _env.HoverKey();
            _hoverKey.Pressed += ToggleExpand;
        }

        _hoverKey?.SetArmed(arm);
        if (State == SnapshotState.Expanded && req.AutoCollapse)
        {
            bool pressed = buttons && !_buttonsBefore;
            if (pressed && !inside && !OverControls(p))
            {
                Collapse();
            }
            else if (_env.Foreground() != _foregroundAtExpand)
            {
                Collapse();
            }
        }

        _buttonsBefore = buttons;
    }

    private bool OverControls((int X, int Y) p) =>
        new NoActivateWindow?[] { _move, _resize, _buttons }.Any(w => w is { IsVisible: true } && PhysicalPlacement.TargetOf(w) is { } r && SnapshotGeometry.Contains(r, p));

    private void DisposeHoverKey()
    {
        var k = _hoverKey;
        _hoverKey = null;
        k?.Dispose();
    }

    // ── окна ───────────────────────────────────────────────────────────────

    private void EnsureWindows()
    {
        if (_window is not null)
        {
            return;
        }

        _frame = new SnapshotFrame();
        _window = new SnapshotWindow();
        _window.DragStarted += () =>
        {
            _dragStart = NormalRect;
            if (_reveal is not null)
            {
                _reveal.Hold = true;
            }
        };
        _window.Dragged += (dx, dy) => QueueDrag(false, dx, dy);
        _window.DragEnded += EndDrag;
        _window.DoubleClicked += DoubleClick;
        _buttons = new PanelButtonsWindow();
        _buttons.Pressed += OnButton;
    }

    private void OnButton(string id)
    {
        switch (id)
        {
            case "expand":
                ToggleExpand();
                break;
            case "replace":
                StartCapture();
                break;
            case "hide":
                Hide();
                break;
            case "clear":
                // Окно кнопок закрывается после обработки своего щелчка.
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    if (!_disposed && State != SnapshotState.None)
                    {
                        Clear();
                    }
                });
                break;
        }
    }

    /// <summary>Нарисовать текущее состояние: тень, снимок, кнопки и ручки (только в обычном виде); наблюдение — пока снимок виден.</summary>
    private void Render()
    {
        if (_disposed || Image is null || State is not (SnapshotState.Normal or SnapshotState.Expanded))
        {
            HideWindows();
            return;
        }

        EnsureWindows();
        var req = _request();
        var style = SnapshotStyle.From(req);
        var rect = Rect;
        var m = MonitorAt(Center(rect));
        double scale = m?.Scale ?? 1;
        double opacity = State == SnapshotState.Expanded ? 1 : req.Opacity; // развёрнутый — для чтения деталей, всегда непрозрачен
        _frame!.Set(style);
        if (_frame.HasEffect)
        {
            _frame.Place(rect, scale, opacity, req.Topmost);
            if (!_frame.IsVisible)
            {
                _frame.Show();
            }
        }
        else if (_frame.IsVisible)
        {
            _frame.Hide();
        }

        _window!.SetStyle(style, Aspect);
        _window.ClickThrough = req.ClickThrough;
        _window.Place(rect, scale, opacity, req.Topmost); // после тени: снимок над ней
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (m is not null)
        {
            PlaceControls(rect, m, req.Topmost);
        }

        if (!_poll.IsEnabled)
        {
            _poll.Start();
        }
    }

    /// <summary>
    /// Кнопки — над снимком справа (под ним или внутри, если места нет), ручки — как у «Линзы». Снимок только что поставлен наверх
    /// своего слоя, поэтому управление ставится заново над ним (ручка размера частично лежит на углу снимка) и в том же слое.
    /// </summary>
    private void PlaceControls(PixelRect rect, MonitorDesc m, bool topmost)
    {
        bool expanded = State == SnapshotState.Expanded;
        _buttons!.Set([
            new PanelButton("expand", expanded ? "minimize-2" : "maximize-2", Loc.T(expanded ? "Snapshot.Button.Collapse" : "Snapshot.Button.Expand")),
            new PanelButton("replace", "camera", Loc.T("Snapshot.Button.Replace")),
            new PanelButton("hide", "eye-off", Loc.T("Snapshot.Button.Hide")),
            new PanelButton("clear", "x", Loc.T("Snapshot.Button.Clear")),
        ]);
        var size = _buttons.Natural();
        int w = (int)Math.Ceiling(size.Width * m.Scale), h = (int)Math.Ceiling(size.Height * m.Scale);
        int y = rect.Top - h - (int)(2 * m.Scale);
        if (y < m.Bounds.Top)
        {
            y = rect.Bottom + (int)(2 * m.Scale);
        }

        if (y + h > m.Bounds.Bottom)
        {
            y = rect.Top + (int)(4 * m.Scale); // развёрнутый снимок у краёв: кнопки внутри, в правом верхнем углу
        }

        var r = new PixelRect(rect.Right - w, y, w, h).ClampInto(m.Bounds);
        _buttons.Width = r.Width / m.Scale;
        _buttons.Height = r.Height / m.Scale;
        PhysicalPlacement.Place(_buttons, r, topmost);
        if (!_buttons.IsVisible)
        {
            _buttons.Show();
            HoverReveal.SetVisible(_buttons, _reveal?.Visible == true, animate: false);
        }

        if (expanded)
        {
            CloseGrips(); // развёрнутый вид временный: перемещать и менять размер можно только обычный
        }
        else
        {
            _move ??= Grip(FocusGripKind.Move, Loc.T("Snapshot.Grip.Move"));
            _resize ??= Grip(FocusGripKind.Resize, Loc.T("Focus.Grip.Resize"));
            _move.Place(rect, m);
            _resize.Place(rect, m);
            PhysicalPlacement.Place(_move, _move.Rect, topmost);
            PhysicalPlacement.Place(_resize, _resize.Rect, topmost);
        }

        _reveal ??= new HoverReveal(() => State is SnapshotState.Normal or SnapshotState.Expanded ? Rect : null, Controls, _env.Animate, _env.Cursor);
        _reveal.Tick();
    }

    private IEnumerable<NoActivateWindow> Controls()
    {
        if (_buttons is { IsVisible: true })
        {
            yield return _buttons;
        }

        if (_move is not null)
        {
            yield return _move;
        }

        if (_resize is not null)
        {
            yield return _resize;
        }
    }

    private FocusGrip Grip(FocusGripKind kind, string label)
    {
        var g = new FocusGrip(kind, label);
        g.DragStarted += _ =>
        {
            _dragStart = NormalRect;
            if (_reveal is not null)
            {
                _reveal.Hold = true;
            }
        };
        g.Dragged += (grip, dx, dy) => QueueDrag(grip.Kind == FocusGripKind.Resize, dx, dy);
        g.DragEnded += _ => EndDrag();
        return g;
    }

    private void CloseGrips()
    {
        _move?.Close();
        _resize?.Close();
        _move = null;
        _resize = null;
    }

    private void HideWindows()
    {
        DropPendingDrag();
        _dragStart = null;
        _poll.Stop();
        DisposeHoverKey();
        CloseGrips();
        _reveal?.Dispose();
        _reveal = null;
        _frame?.Hide();
        _window?.Hide();
        _buttons?.Hide();
    }

    // ── мониторы ───────────────────────────────────────────────────────────

    private static (int X, int Y) Center(PixelRect r) => (r.Left + (r.Width / 2), r.Top + (r.Height / 2));

    private MonitorDesc? MonitorAt((int X, int Y) p) => MonitorResolver.UnderCursor(_env.Monitors(), p);

    private static PixelRect WorkOf(MonitorDesc? m, PixelRect fallback) => m is null ? fallback : m.Work.IsEmpty ? m.Bounds : m.Work;

    private PixelRect VirtualBounds()
    {
        var all = _env.Monitors();
        if (all.Count == 0)
        {
            return new PixelRect(0, 0, 1920, 1080);
        }

        int l = all.Min(m => m.Bounds.Left), t = all.Min(m => m.Bounds.Top);
        return new PixelRect(l, t, all.Max(m => m.Bounds.Right) - l, all.Max(m => m.Bounds.Bottom) - t);
    }

    /// <summary>Обычный прямоугольник — целиком на мониторе своего центра и в пределах размера (после перетаскивания, смены настроек).</summary>
    private PixelRect FitOnMonitor(PixelRect r)
    {
        var req = _request();
        var m = MonitorAt(Center(r));
        var work = WorkOf(m, r);
        return SnapshotGeometry.Fit(r, ImageSize.Width, ImageSize.Height, KeepAspect, SnapshotLimits.For(work, m?.Scale ?? 1, req.MinSize, req.MaxSize), work);
    }

    /// <summary>Монитор развёрнутого снимка: где снимок (по умолчанию), под указателем или основной.</summary>
    public MonitorDesc? ExpandMonitor()
    {
        var all = _env.Monitors();
        return _request().ExpandMonitor switch
        {
            "pointer" => MonitorResolver.UnderCursor(all, _env.Cursor()),
            "primary" => all.FirstOrDefault(m => m.IsPrimary) ?? MonitorAt(Center(NormalRect)),
            _ => MonitorAt(Center(NormalRect)),
        };
    }

    private PixelRect ComputeExpanded()
    {
        var req = _request();
        var m = ExpandMonitor();
        return SnapshotGeometry.Expanded(NormalRect, ImageSize.Width, ImageSize.Height, WorkOf(m, NormalRect), m?.Scale ?? 1, req.ExpandMargin, req.Upscale);
    }

    private void SavePlace()
    {
        if (!NormalRect.IsEmpty)
        {
            _saved = NormalRect;
            PlaceChanged?.Invoke(NormalRect);
        }
    }

    private void SetState(SnapshotState s)
    {
        if (State != s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }
    }

    /// <summary>Кнопка «Удалить снимок»: изображение освобождается, владелец получает причину cleared.</summary>
    public void Clear() => Close("cleared");

    /// <summary>Удалить снимок по действию пользователя: окна закрываются, изображение освобождается, владелец получает причину.</summary>
    private void Close(string reason)
    {
        Release();
        SetState(SnapshotState.None);
        Closed?.Invoke(reason);
    }

    private void Release()
    {
        _generation++;
        CloseSelection();
        HideWindows();
        _reveal?.Dispose();
        _reveal = null;
        _frame?.Close();
        _window?.ReleaseImage();
        _window?.Close();
        _buttons?.Close();
        _frame = null;
        _window = null;
        _buttons = null;
        Image = null; // буфер изображения больше нигде не хранится
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Release();
        _disposed = true;
        SetState(SnapshotState.None);
        StateChanged = null;
        PlaceChanged = null;
        Closed = null;
    }
}
