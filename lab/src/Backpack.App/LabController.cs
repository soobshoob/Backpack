using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Backpack.Core.Diagnostics;
using Backpack.Core.Input;
using Backpack.Core.Model;
using Backpack.Core.Privacy;
using Backpack.Core.Transport;
using Backpack.Win32;

namespace Backpack.App;

/// <summary>Один источник: собственный поток, своя таблица, своё кольцо, своя модель.</summary>
public sealed class SourceSlot
{
    public SourceSlot(SourceKind kind, MessageThreadHost host, CaptureState capture, SourceReader reader, LatencyHistogram callback)
    {
        Kind = kind;
        Host = host;
        Capture = capture;
        Reader = reader;
        Callback = callback;
    }

    public SourceKind Kind { get; }
    public MessageThreadHost Host { get; }
    public CaptureState Capture { get; }
    public SourceReader Reader { get; }
    public LatencyHistogram Callback { get; }
    public string Status { get; set; } = "NotStarted";
    public int? StartError { get; set; }
    public long ActivityMark { get; set; }
    public long ActivityDelta { get; set; }
}

/// <summary>
/// UI-поток: доставка событий из колец в модели, отображение, сверка, проверки здоровья и экспорт.
///
/// Доставка: поток захвата при первом событии после вычитки вызывает Dispatcher.BeginInvoke(Input) — без ожидания;
/// резервный таймер 100 мс подбирает потерянные сигналы, рост эпохи потерь и опубликованные снимки.
/// Обновление оверлея планируется отдельно (DispatcherPriority.Render), с замером «модель → свойства UI».
/// </summary>
public sealed class LabController
{
    private static readonly long QuietTicks = Stopwatch.Frequency / 10;

    private readonly LabOptions _options;
    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly List<KeyTransition> _transitions = new();
    private readonly Action _drainAction;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _reconcileTimer;
    private readonly DispatcherTimer _healthTimer;
    private readonly DispatcherTimer _expiryTimer;
    private readonly HashSet<FindingCode> _activeFindings = new();
    private readonly long _repeatWindowTicks;
    private nint[] _winEventHooks = [];
    private OverlayWindow? _overlay;
    private bool _viewScheduled;
    private long _displayChangedAt;
    private TimeSpan _lastHealthTick;
    private TimeSpan? _resyncPendingSince;
    private long _primaryActivityMark = -1;
    private TimeSpan _primaryActivityAt;

    private OverlayWindowState? _lastWindowState;
    private long _sessionLock, _sessionUnlock, _desktopSwitch, _suspend, _resume, _displayChange;
    private FocusPrivacySource? _focus;
    private UiaFocusEvents? _uiaFocus;
    private long _seenFocusSerial = -1;
    private nint _seenFocusWindow = -1;
    private bool _hiddenSinceOpen;

    public LabController(LabOptions options, Dispatcher dispatcher, TimeSpan linger)
    {
        _options = options;
        _dispatcher = dispatcher;
        _drainAction = DrainAll;
        Display = new DisplayModel(options.LingerOverride ?? linger);
        Probe = new KeyStateProbe(() => SessionLocked);
        Comparator = options.Mode == CaptureMode.Compare ? new EdgeComparator(TimeSpan.FromMilliseconds(200), 256) : null;
        Privacy = new PrivacyGate(TimeSpan.FromMilliseconds(400));
        Privacy.ClearRequested += OnPrivacyClear;

        // Эвристика устаревшего Down: задержка автоповтора Windows + запас. Не гарантия.
        int delayIndex = SystemParameters.KeyboardDelay; // 0..3 → 250..1000 мс
        _repeatWindowTicks = (long)((((delayIndex + 1) * 250) + 750) / 1000.0 * Stopwatch.Frequency);

        _pollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, (_, _) =>
        {
            DrainAll();
            Privacy.Tick(_uptime.Elapsed);
        }, dispatcher);
        _reconcileTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => ReconcileTick(), dispatcher);
        _healthTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => HealthTick(), dispatcher);
        _expiryTimer = new DispatcherTimer(DispatcherPriority.Input, dispatcher);
        _expiryTimer.Tick += (_, _) => ExpiryTick();
        _pollTimer.Stop();
        _reconcileTimer.Stop();
        _healthTimer.Stop();
    }

    public event Action? DisplayChanged;

    /// <summary>Импульс колеса основного источника (только в памяти; для стрелки на значке мыши).</summary>
    public event Action<WheelPulse>? Wheel;

    /// <summary>
    /// Esc нажата (основной источник, достоверный Down). Только наблюдение — ввод не подавляется и не изменяется;
    /// используется для выхода из линзы, рисования и радиального меню, которые не получают фокус клавиатуры.
    /// </summary>
    public event Action? EscapePressed;

    private const int EscSlot = 0x01;

    /// <summary>Блокировка/разблокировка сессии, смена рабочего стола, выход из сна (для индикатора раскладки: новая база).</summary>
    public event Action? ContextReset;

    public LabOptions Options => _options;

    /// <summary>
    /// Динамическая приватность: фильтр между физической моделью и моделью отображения. Всё, что получает ввод (оверлей,
    /// история, расширения), получает его только после этого фильтра.
    /// </summary>
    public PrivacyGate Privacy { get; }

    /// <summary>Источник сведений о фокусе (null — приватность выключена или источник не запустился).</summary>
    public FocusPrivacySource? FocusSource => _focus;

    /// <summary>С последнего открытия показа было скрыто хотя бы одно нажатие (для значка замка; без количества).</summary>
    public bool HiddenInputSinceOpen => _hiddenSinceOpen;

    /// <summary>Показ закрыт приватностью: очистить кейкапы немедленно, без анимации исчезновения.</summary>
    public event Action? PrivacyCleared;

    /// <summary>Изменилось состояние приватности или скрыто первое нажатие (для статуса).</summary>
    public event Action? PrivacyChanged;

    /// <summary>
    /// Ввод после фильтра приватности (те же переходы, что получает модель отображения). Для расширений и истории:
    /// скрытое приватностью сюда не попадает.
    /// </summary>
    public event Action<KeyTransition>? VisibleTransition;
    public DisplayModel Display { get; }
    public KeyStateProbe Probe { get; }
    public EdgeComparator? Comparator { get; }
    public List<SourceSlot> Sources { get; } = new();
    public SourceSlot? Primary { get; private set; }
    public ReconcileCounters ReconcileCounters { get; } = new();
    public HealthLog Health { get; } = new();
    public RecoveryPolicy Recovery { get; } = new(TimeSpan.FromSeconds(5), 5);

    /// <summary>Конвейер отображения (Phase 4I): счётчики и этап остановки последнего показанного нажатия, без клавиш.</summary>
    public DisplayWatch Watch { get; } = new();

    /// <summary>Все окна оверлея (по одному на монитор); задаёт App. Проверяются все, а не только первое.</summary>
    public Func<IReadOnlyList<OverlayWindow>>? OverlaysProvider { get; set; }

    private IReadOnlyList<OverlayWindow> AllOverlays => OverlaysProvider?.Invoke() ?? (_overlay is null ? [] : [_overlay]);

    /// <summary>Окно оверлея перестроило кейкапы (для этапа «не обновлено»).</summary>
    public void NoteRefresh(int visuals) => Watch.Refreshed(visuals, _uptime.Elapsed);
    public LatencyHistogram ModelToUi { get; } = new();
    /// <summary>Сообщения запуска для окна диагностики; переводятся при показе (смена языка не оставляет старый текст).</summary>
    public List<Backpack.Visual.Localization.LocText> StartNotes { get; } = new();
    public List<string> ShutdownNotes { get; } = new();
    public bool SessionLocked { get; private set; }
    public bool OverlayIntendedVisible { get; private set; } = true;
    public bool ShuttingDown { get; private set; }
    public TimeSpan Uptime => _uptime.Elapsed;

    public void AttachOverlay(OverlayWindow overlay) => _overlay = overlay;

    /// <summary>Поставщик агрегированных данных визуального слоя для экспорта (задаёт App).</summary>
    public Func<VisualStats?>? VisualStatsProvider { get; set; }

    /// <summary>
    /// Кнопки мыши по существующему состоянию основного источника (агрегат по устройствам): Down — нажата, Unknown — неизвестно.
    /// Только чтение модели; источники и сверка не затрагиваются.
    /// </summary>
    public (int Pressed, int Unknown) MouseButtonsState()
    {
        if (Primary is null)
        {
            return (0, 0);
        }

        int pressed = 0, unknown = 0;
        var model = Primary.Reader.Model;
        foreach (var b in (ReadOnlySpan<MouseButton>)[MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1, MouseButton.X2])
        {
            int bit = b switch { MouseButton.Left => 1, MouseButton.Right => 2, MouseButton.Middle => 4, MouseButton.X1 => 8, _ => 16 };
            switch (model.AggregateOf(Slots.Mouse(b)))
            {
                case KeyState.Down: pressed |= bit; break;
                case KeyState.Unknown: unknown |= bit; break;
            }
        }

        return (pressed, unknown);
    }

    /// <summary>
    /// Новая выдержка из настроек — только модель отображения: источники и сверка не затрагиваются.
    /// Параметр запуска --linger-ms имеет приоритет на время этого запуска.
    /// </summary>
    public void ApplyLinger(TimeSpan linger)
    {
        if (_options.LingerOverride is not null || ShuttingDown)
        {
            return;
        }

        Display.SetLinger(linger);
        if (Display.Tick(_uptime.Elapsed))
        {
            OnDisplayModelChanged();
        }
        else
        {
            ScheduleExpiry();
        }
    }

    // ── запуск и остановка ────────────────────────────────────────────────────

    public void Start()
    {
        if (_options.Mode is CaptureMode.Raw or CaptureMode.Compare)
        {
            var capture = new CaptureState(SourceKind.RawInput, _options.RingCapacity, ScheduleDrain);
            var host = new RawInputSource(capture);
            AddSource(SourceKind.RawInput, host, capture, host.MessageTime);
        }

        if (_options.Mode is CaptureMode.LowLevel or CaptureMode.Compare)
        {
            var capture = new CaptureState(SourceKind.LowLevelHook, _options.RingCapacity, ScheduleDrain);
            var host = new LowLevelHookSource(capture);
            AddSource(SourceKind.LowLevelHook, host, capture, host.CallbackTime);
        }

        Primary = Sources.Single(s => s.Kind == _options.Primary);
        Primary.Reader.Model.WheelObserved = p => Wheel?.Invoke(p);

        foreach (var s in Sources)
        {
            var r = s.Host.Start(TimeSpan.FromSeconds(3));
            s.Status = r.Ok ? "Running" : "StartFailed";
            s.StartError = r.Ok ? null : r.Win32Error;
            StartNotes.Add(r.Ok ? new("Note.SourceReady", s.Kind) : new("Note.SourceFailed", s.Kind, r.Stage, r.Win32Error));
            if (!r.Ok)
            {
                Report(FindingCode.SourceStartFailed, FindingLevel.Confirmed);
            }
        }

        _winEventHooks = SystemNotifications.HookWinEvents(OnWinEvent);
        Privacy.Changed += OnPrivacyChanged;
        if (Privacy.Enabled)
        {
            StartPrivacySource();
        }

        _pollTimer.Start();
        _reconcileTimer.Start();
        _healthTimer.Start();
    }

    public void Stop()
    {
        if (ShuttingDown)
        {
            return;
        }

        _lastWindowState = OverlayState ?? _lastWindowState;
        ShuttingDown = true;
        _pollTimer.Stop();
        _reconcileTimer.Stop();
        _healthTimer.Stop();
        _expiryTimer.Stop();
        SystemNotifications.UnhookWinEvents(_winEventHooks);
        _winEventHooks = [];
        Privacy.Changed -= OnPrivacyChanged;
        StopPrivacySource();

        foreach (var s in Sources)
        {
            var r = s.Host.Stop(TimeSpan.FromSeconds(2));
            s.Status = s.Host.Status.ToString();
            ShutdownNotes.Add($"{s.Kind}: {r.Note}, {r.Milliseconds:F0} мс, исключений в обработчиках: {s.Host.HandlerExceptions}");
            if (!r.Clean)
            {
                Report(FindingCode.SourceStopTimeout, FindingLevel.Confirmed);
            }
        }
    }

    /// <summary>Потокобезопасная аварийная остановка: каждый источник снимает свои обработчики в своём потоке.</summary>
    public void EmergencyStopSources()
    {
        ShuttingDown = true;
        foreach (var s in Sources.ToArray())
        {
            try
            {
                s.Host.Stop(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // процесс и так завершается; ОС освободит хуки при выходе
            }
        }
    }

    private void AddSource(SourceKind kind, MessageThreadHost host, CaptureState capture, LatencyHistogram callback)
    {
        var model = new SourceModel(kind);
        var reader = new SourceReader(capture, model, () =>
        {
            if (!host.RequestSnapshot())
            {
                // Поток не принял запрос; SourceReader повторит не чаще раза в секунду.
            }
        });
        Sources.Add(new SourceSlot(kind, host, capture, reader, callback));
    }

    // ── динамическая приватность ──────────────────────────────────────────────

    /// <summary>Включить или выключить динамическую приватность; источник фокуса работает только пока она включена.</summary>
    public void SetPrivacy(bool enabled, IReadOnlyCollection<string> blockedApps)
    {
        _blockedApps = blockedApps;
        if (Privacy.Enabled != enabled)
        {
            Privacy.Enabled = enabled;
            if (!ShuttingDown && Primary is not null)
            {
                if (enabled)
                {
                    StartPrivacySource();
                }
                else
                {
                    StopPrivacySource();
                }
            }
        }

        _focus?.SetBlockedApps(blockedApps);
        ForcePrivacyRecheck();
    }

    private IReadOnlyCollection<string> _blockedApps = [];

    /// <summary>Проверить текущий контекст заново (например, изменился список запрета).</summary>
    public void ForcePrivacyRecheck()
    {
        _focus?.BumpSerial();
        if (!ShuttingDown)
        {
            ScheduleDrain();
        }
    }

    private void StartPrivacySource()
    {
        if (_focus is not null)
        {
            return;
        }

        var f = new FocusPrivacySource(
            (version, verdict) =>
            {
                // Пока подписка UIA не готова, смена фокуса внутри окна может пройти незамеченной: «обычное поле» не
                // подтверждается (проверка остаётся незавершённой; по таймауту — «нет данных»). Запрещающий ответ принимается.
                if (verdict == ProbeVerdict.NotPassword && _uiaFocus is { Ready: false })
                {
                    return;
                }

                try
                {
                    _dispatcher.BeginInvoke(DispatcherPriority.Input, () => OnProbeResult(version, verdict));
                }
                catch
                {
                    // Dispatcher завершён
                }
            },
            ScheduleDrain);
        f.SetBlockedApps(_blockedApps);
        var r = f.Start(TimeSpan.FromSeconds(3));
        if (!r.Ok)
        {
            // Без источника фокуса состояние — «нет данных»: показ закрыт, а не открыт.
            StartNotes.Add(new("Note.PrivacyFailed", r.Stage, r.Win32Error));
            Privacy.OnFocusChanged(_uptime.Elapsed);
            Privacy.Tick(_uptime.Elapsed + Privacy.ProbeTimeout);
            return;
        }

        _focus = f;
        _uiaFocus = new UiaFocusEvents(f.SignalFocusChanged);
        _uiaFocus.ReadyChanged += f.SignalFocusChanged; // готова — проверить текущий элемент заново (новая версия)
        _uiaFocus.Start();
        _seenFocusSerial = -1;
        _seenFocusWindow = -1;
        SyncPrivacyContext(_uptime.Elapsed);
    }

    private void StopPrivacySource()
    {
        var f = _focus;
        _focus = null;
        _uiaFocus?.Dispose();
        _uiaFocus = null;
        if (f is null)
        {
            return;
        }

        f.Stop(TimeSpan.FromSeconds(1));
        f.JoinWorker(TimeSpan.FromMilliseconds(500)); // зависшая проверка UIA не задерживает выход дольше этого
    }

    /// <summary>
    /// Перед применением ввода: если с прошлой порции сменился фокус (событие WinEvent или окно с фокусом клавиатуры),
    /// закрыть показ новой версией контекста и запросить проверку. Выполняется синхронно в UI-потоке перед кадром,
    /// поэтому ввод, применяемый после замеченной смены фокуса, не попадёт на экран до решения.
    /// </summary>
    private void SyncPrivacyContext(TimeSpan now)
    {
        var f = _focus;
        if (f is null || !Privacy.Enabled)
        {
            return;
        }

        long serial = f.FocusSerial;
        nint window = FocusPrivacySource.FocusWindow();
        long previousSync = _lastSyncStamp;
        _lastSyncStamp = Stopwatch.GetTimestamp();
        if (serial == _seenFocusSerial && window == _seenFocusWindow)
        {
            return;
        }

        // Граница нового контекста: время первого уведомления о смене фокуса. Если сменилось окно с фокусом клавиатуры, а
        // уведомления не было, — время прошлой проверки: всё, что захвачено после неё, считается новым контекстом.
        long firstChange = f.TakeFirstChangeStamp();
        long start = serial != _seenFocusSerial && firstChange != 0 ? firstChange : previousSync;
        if (window != _seenFocusWindow && _seenFocusWindow != -1)
        {
            start = Math.Min(start, previousSync);
        }

        _seenFocusSerial = serial;
        _seenFocusWindow = window;
        long version = Privacy.OnFocusChanged(now, start);
        f.Request(version);
    }

    private long _lastSyncStamp;

    /// <summary>Результат проверки на UI-потоке: подтверждённое обычное поле передаёт ожидавшие переходы своей версии.</summary>
    private void OnProbeResult(long version, ProbeVerdict verdict)
    {
        if (!Privacy.OnProbeResult(version, verdict))
        {
            return;
        }

        var released = Privacy.TakeReleased();
        if (released.Count == 0)
        {
            return;
        }

        var now = _uptime.Elapsed;
        bool changed = false;
        foreach (var t in released)
        {
            Watch.Transition(true, false, t.State == KeyState.Down, now);
            changed |= Display.OnTransition(t, now);
            VisibleTransition?.Invoke(t);
        }

        if (changed)
        {
            OnDisplayModelChanged();
        }
    }

    // ── журнал ввода (--input-trace): диагностика мультимедиа и Fn ─────────────

    /// <summary>Журнал ввода пишется только первые две минуты после запуска (короткая проверка, а не постоянная запись).</summary>
    private bool TraceActive => _uptime.Elapsed < TimeSpan.FromMinutes(2);

    /// <summary>
    /// Событие источника (до фильтра приватности): время, источник, вид, scan code, VK, флаги (injected). Пока приватность
    /// закрыта, клавиша не пишется — только «hidden». Fn в Windows не приходит как клавиша; журнал показывает, что приходит.
    /// </summary>
    private void TraceInput(SourceKind source, InputEvent e)
    {
        string what = e.Kind is InputKind.Down or InputKind.Up && !Privacy.Open ? "hidden"
            : e.Kind is InputKind.Down or InputKind.Up ? $"{KeyInfo.Label(e.Slot)}\tslot={e.Slot}\tvk=0x{e.Vk:X2}\tflags={e.Flags}"
            : e.Kind.ToString();
        AppendTrace($"{_uptime.Elapsed.TotalMilliseconds:F1}\tevent\t{source}\t{e.Kind}\t{what}");
    }

    // ── короткая проверка Fn (--fn-trace, Phase 4H п. 8) ─────────────────────

    /// <summary>Сеанс проверки Fn: 45 секунд с запуска или до первого Esc — без лишнего ожидания и постоянной записи.</summary>
    private bool FnTraceActive => _options.FnTrace is not null && !_fnTraceDone && _uptime.Elapsed < FnTraceLength;

    private static readonly TimeSpan FnTraceLength = TimeSpan.FromSeconds(45);
    private bool _fnTraceDone;
    private bool _fnTraceStarted;
    private int _fnTraceEvents;

    /// <summary>
    /// Обычные символьные клавиши (буквы, цифры, знаки, пробел, Enter, Tab, Backspace, цифры дополнительной клавиатуры) — в
    /// журнал Fn не пишутся никогда: проверке нужны только служебные клавиши и то, что приходит по Fn-сочетаниям.
    /// </summary>
    internal static bool IsOrdinaryVk(int vk) => vk is 0x08 or 0x09 or 0x0D or 0x20 or (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A)
        or (>= 0x60 and <= 0x6F) or (>= 0xBA and <= 0xC0) or (>= 0xDB and <= 0xDF) or 0xE2;

    /// <summary>
    /// Запись проверки Fn: время, источник, нажатие или отпускание, VK, подпись Backpack, слот (scan code) и флаги (extended,
    /// injected). Аппаратная Fn обычно обрабатывается прошивкой и сама в Windows не приходит — журнал показывает, что приходит
    /// вместо неё (другой VK, клавиша с флагом extended, мультимедийная клавиша) или что не приходит ничего. Пока закрыта
    /// приватность, ничего не пишется.
    /// </summary>
    private void TraceFn(SourceKind source, InputEvent e)
    {
        if (!FnTraceActive || e.Kind is not (InputKind.Down or InputKind.Up) || !Privacy.Open || IsOrdinaryVk(e.Vk))
        {
            return;
        }

        if (!_fnTraceStarted)
        {
            _fnTraceStarted = true;
            AppendFn("# Backpack Fn check: service keys only (no letters, digits or punctuation). Stops after 45 s or at Esc.");
            AppendFn("# ms\tsource\tkind\tvk\tlabel\tslot\tflags");
        }

        _fnTraceEvents++;
        AppendFn($"{_uptime.Elapsed.TotalMilliseconds:F1}\t{source}\t{e.Kind}\t0x{e.Vk:X2}\t{KeyInfo.Label(e.Slot)}\tslot={e.Slot}\t{e.Flags}");
        if (e.Vk == 0x1B && e.Kind == InputKind.Up)
        {
            _fnTraceDone = true;
            AppendFn($"# end: Esc, events {_fnTraceEvents}");
        }
    }

    private void AppendFn(string line)
    {
        try
        {
            System.IO.File.AppendAllText(_options.FnTrace!, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }

    private void TraceTransition(KeyTransition t, bool shown)
    {
        if (_options.InputTrace is null || !TraceActive)
        {
            return;
        }

        AppendTrace($"{_uptime.Elapsed.TotalMilliseconds:F1}\tmodel\t{t.State}\t{(shown ? KeyInfo.Label(t.KeySlot) : "hidden")}\tshown={shown}\tdisplay={Display.Entries.Count}");
    }

    private void AppendTrace(string line)
    {
        try
        {
            System.IO.File.AppendAllText(_options.InputTrace!, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Журнал стенда (--privacy-trace): время, версия, состояние, причина, «скрыт ввод». Клавиши не пишутся.</summary>
    private void TracePrivacy()
    {
        if (_options.PrivacyTrace is not { } path)
        {
            return;
        }

        try
        {
            System.IO.File.AppendAllText(path, $"{_uptime.Elapsed.TotalMilliseconds:F0}	v{Privacy.Version}	{Privacy.State}	{Privacy.Reason}	open={Privacy.Open}	hidden={_hiddenSinceOpen}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }

    private void OnPrivacyClear()
    {
        Display.Clear();
        PrivacyCleared?.Invoke();
        OnDisplayModelChanged();
    }

    private void OnPrivacyChanged()
    {
        if (Privacy.Open)
        {
            _hiddenSinceOpen = false;
        }

        TracePrivacy();

        PrivacyChanged?.Invoke();
    }

    // ── доставка ──────────────────────────────────────────────────────────────

    /// <summary>Вызывается из потока захвата (в том числе из LL-callback): только постановка в очередь, без ожидания.</summary>
    private void ScheduleDrain()
    {
        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Input, _drainAction);
        }
        catch
        {
            // Dispatcher завершён — резервный опрос уже не нужен.
        }
    }

    private void DrainAll()
    {
        if (ShuttingDown)
        {
            return;
        }

        var now = _uptime.Elapsed;
        SyncPrivacyContext(now);
        bool changed = false, more = false;
        foreach (var s in Sources)
        {
            _transitions.Clear();
            var cmp = Comparator;
            var kind = s.Kind;
            Action<InputEvent>? observe = cmp is null ? null : e => cmp.Observe(kind, e, _uptime.Elapsed);
            if (_options.InputTrace is not null && TraceActive)
            {
                var inner = observe;
                observe = e =>
                {
                    inner?.Invoke(e);
                    TraceInput(kind, e);
                };
            }

            if (FnTraceActive)
            {
                var inner = observe;
                observe = e =>
                {
                    inner?.Invoke(e);
                    TraceFn(kind, e);
                };
            }

            if (ReferenceEquals(s, Primary))
            {
                var inner = observe;
                observe = e =>
                {
                    inner?.Invoke(e);
                    if (e.Kind is InputKind.Down or InputKind.Up && !Slots.IsMouse(e.Slot))
                    {
                        Watch.KeyEvent(now);
                    }
                };
            }

            more |= s.Reader.Drain(512, _transitions, observe);
            if (ReferenceEquals(s, Primary))
            {
                changed |= ApplyToDisplay(now);
            }
        }

        if (more)
        {
            ScheduleDrain();
        }

        if (changed)
        {
            OnDisplayModelChanged();
        }
    }

    private bool ApplyToDisplay(TimeSpan now)
    {
        bool changed = false, escape = false;
        foreach (var t in _transitions)
        {
            // Esc наблюдается всегда (выход из инструмента), но при закрытом показе не отображается.
            escape |= t.KeySlot == EscSlot && t.State == KeyState.Down;
            long pendingBefore = Privacy.PendingCount;
            bool shown = Privacy.Filter(t);
            if (!Slots.IsMouse(t.KeySlot))
            {
                Watch.Transition(shown, !shown && Privacy.PendingCount > pendingBefore, t.State == KeyState.Down, now);
            }

            TraceTransition(t, shown);
            if (!shown)
            {
                if (t.State == KeyState.Down && !_hiddenSinceOpen)
                {
                    _hiddenSinceOpen = true;
                    TracePrivacy();
                    PrivacyChanged?.Invoke();
                }

                continue;
            }

            changed |= Display.OnTransition(t, now);
            VisibleTransition?.Invoke(t);
        }

        _transitions.Clear();
        if (escape)
        {
            EscapePressed?.Invoke();
        }

        return changed;
    }

    /// <summary>Возврат скрытого отображения: отпущенные записи убираются, удерживаемые остаются.</summary>
    public void DropReleasedEntries()
    {
        if (Display.DropReleased())
        {
            OnDisplayModelChanged();
        }
    }

    private void OnDisplayModelChanged()
    {
        if (!_viewScheduled)
        {
            _viewScheduled = true;
            _displayChangedAt = Stopwatch.GetTimestamp();
            _dispatcher.BeginInvoke(DispatcherPriority.Render, ApplyView);
        }
    }

    private void ApplyView()
    {
        _viewScheduled = false;
        if (_options.PrivacyTrace is { } trace)
        {
            try
            {
                System.IO.File.AppendAllText(trace, $"{_uptime.Elapsed.TotalMilliseconds:F0}	display	keycaps={Display.Entries.Count}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
            }
        }

        Watch.ViewApplied(_uptime.Elapsed);
        DisplayChanged?.Invoke();
        ModelToUi.RecordTicks(Stopwatch.GetTimestamp() - _displayChangedAt);
        ScheduleExpiry();
    }

    private void ScheduleExpiry()
    {
        _expiryTimer.Stop();
        if (Display.NextDeadline is { } deadline)
        {
            var wait = deadline - _uptime.Elapsed;
            _expiryTimer.Interval = wait > TimeSpan.FromMilliseconds(1) ? wait : TimeSpan.FromMilliseconds(1);
            _expiryTimer.Start();
        }
    }

    private void ExpiryTick()
    {
        _expiryTimer.Stop();
        if (Display.Tick(_uptime.Elapsed))
        {
            OnDisplayModelChanged();
        }
        else
        {
            ScheduleExpiry();
        }
    }

    // ── сверка и потеря контекста (только основной источник; второй в compare — «чистое» наблюдение) ──

    private void ReconcileTick()
    {
        if (Primary is null || ShuttingDown)
        {
            return;
        }

        DrainAll(); // сверка — только после применения всех уже наблюдавшихся событий
        var model = Primary.Reader.Model;
        if (!model.HasNonUpKeys())
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        _transitions.Clear();
        Reconciliation.Run(model, Probe, now, QuietTicks, ReconcileCounters, _transitions);
        model.CheckStaleRepeat(now, _repeatWindowTicks, _transitions);
        if (ApplyToDisplay(_uptime.Elapsed))
        {
            OnDisplayModelChanged();
        }
    }

    private void MarkPrimaryUnknown(StateReason reason)
    {
        DrainAll(); // сначала применить всё, что источник уже наблюдал
        if (Primary is null)
        {
            return;
        }

        _transitions.Clear();
        Primary.Reader.Model.MarkAllUnknown(reason, _transitions);
        if (ApplyToDisplay(_uptime.Elapsed))
        {
            OnDisplayModelChanged();
        }
    }

    public void ManualReset()
    {
        if (Primary is null)
        {
            return;
        }

        _transitions.Clear();
        Primary.Reader.Model.ResetAll(_transitions);
        ApplyToDisplay(_uptime.Elapsed);
        Display.Clear();
        OnDisplayModelChanged();
    }

    public void OnSessionChange(int code)
    {
        if (code == 7)
        {
            _sessionLock++;
            SessionLocked = true;
            MarkPrimaryUnknown(StateReason.SessionReset);
            ContextReset?.Invoke();
        }
        else if (code == 8)
        {
            _sessionUnlock++;
            SessionLocked = false;
            MarkPrimaryUnknown(StateReason.SessionReset);
            ContextReset?.Invoke();
        }
    }

    public void OnPower(int code)
    {
        if (code == 4)
        {
            _suspend++;
        }
        else if (code == 0x12)
        {
            _resume++;
            MarkPrimaryUnknown(StateReason.PowerResume);
            ContextReset?.Invoke();
        }
    }

    /// <summary>Изменились мониторы, разрешение, основной экран или рабочая область: окна размещаются заново.</summary>
    public event Action? DisplaysChanged;

    public void OnDisplayChange()
    {
        _displayChange++;
        DisplaysChanged?.Invoke();
    }

    private void OnWinEvent(uint evt)
    {
        if (evt == SystemNotifications.EventSystemDesktopSwitch)
        {
            _desktopSwitch++;
            MarkPrimaryUnknown(StateReason.DesktopSwitch);
            ContextReset?.Invoke();
        }
        else if (evt == SystemNotifications.EventSystemForeground)
        {
            CheckOverlayWindow();
        }
    }

    // ── оверлей: намеренное скрытие и подтверждённое восстановление ──────────

    /// <summary>
    /// Показ или скрытие окна оверлея. Захват и модель клавиш не останавливаются: нажатия и отпускания продолжают
    /// отслеживаться, и после показа оверлей отображает актуальное состояние, без залипших клавиш.
    /// </summary>
    public void SetOverlayVisible(bool visible)
    {
        if (OverlayIntendedVisible == visible)
        {
            return;
        }

        OverlayIntendedVisible = visible;
        if (visible)
        {
            _overlay?.ShowOverlay();
        }
        else
        {
            _overlay?.Hide();
        }

        OverlayVisibilityChanged?.Invoke(visible);
    }

    /// <summary>Видимость оверлея изменена (кнопка диагностики, меню трея).</summary>
    public event Action<bool>? OverlayVisibilityChanged;

    public OverlayWindowState? OverlayState => _overlay?.Hwnd is { } h && h != 0 ? OverlayWindowTools.Check(h) : null;

    /// <summary>Проверить все окна оверлея (по одному на монитор), а не только первое.</summary>
    private void CheckOverlayWindow()
    {
        if (ShuttingDown || !OverlayIntendedVisible)
        {
            return;
        }

        var now = _uptime.Elapsed;
        foreach (var o in AllOverlays)
        {
            if (o.Hwnd == 0)
            {
                continue;
            }

            var st = OverlayWindowTools.Check(o.Hwnd);
            nint h = o.Hwnd;
            if (!st.Visible)
            {
                Recover(FindingCode.OverlayHiddenUnexpectedly, now, () => OverlayWindowTools.ShowNoActivate(h));
            }
            else if (st.Minimized)
            {
                Recover(FindingCode.OverlayMinimized, now, () => OverlayWindowTools.ShowNoActivate(h));
            }
            else if (!st.Topmost)
            {
                Recover(FindingCode.OverlayTopmostLost, now, () => OverlayWindowTools.RestoreTopmost(h));
            }
            else if (!st.OnMonitor)
            {
                Recover(FindingCode.OverlayOffMonitor, now, () => DisplaysChanged?.Invoke());
            }
        }
    }

    private void Recover(FindingCode code, TimeSpan now, Action action)
    {
        Health.Report(code, FindingLevel.Confirmed, now);
        if (Recovery.TryBegin(code, FindingLevel.Confirmed, OverlayIntendedVisible, ShuttingDown, now))
        {
            Watch.Recovered();
            action();
        }
    }

    // ── конвейер отображения (Phase 4I) ──────────────────────────────────────

    private TimeSpan _lastWatchTrace;

    /// <summary>Факты об окнах оверлея для <see cref="DisplayWatch"/>: видимость, topmost и перекрытие над группой кейкапов.</summary>
    private DisplayWindowFacts WatchFacts()
    {
        bool visible = true, topmost = true, covered = false;
        var overlays = AllOverlays;
        foreach (var o in overlays)
        {
            if (o.Hwnd == 0)
            {
                visible = false;
                continue;
            }

            var st = OverlayWindowTools.Check(o.Hwnd);
            visible &= st.Visible && !st.Minimized;
            topmost &= st.Topmost;
            if (o.GroupScreenPixels() is { } r)
            {
                var (c, own) = OverlayWindowTools.CoveredAbove(o.Hwnd, r.Left, r.Top, r.Right, r.Bottom);
                if (c && !own)
                {
                    covered = true;
                }
            }
        }

        bool silent = Primary is not null && (Primary.Host.Status == HostStatus.ExitedUnexpectedly
            || (Primary.Host is RawInputSource ri && ri.IsRegistrationIntact() == false));
        return new DisplayWindowFacts(OverlayIntendedVisible, overlays.Count > 0 && visible, topmost, covered, Privacy.Open, silent);
    }

    private void WatchTick(TimeSpan now)
    {
        if (Watch.Evaluate(now, WatchFacts()))
        {
            if (DisplayWatch.IsFault(Watch.Stage))
            {
                Report(Watch.Stage == DisplayStage.WindowCovered ? FindingCode.OverlayCovered : FindingCode.DisplayStalled,
                    Watch.Stage == DisplayStage.WindowCovered ? FindingLevel.Suspicion : FindingLevel.Confirmed);
            }

            TraceDisplay(now, "stage");
        }
        else if (now - _lastWatchTrace >= TimeSpan.FromSeconds(60))
        {
            TraceDisplay(now, "tick");
        }
    }

    /// <summary>
    /// Журнал конвейера (--display-trace): этап, счётчики, приватность (состояние и причина), окна, память и потоки процесса.
    /// Клавиши, текст, заголовки окон и буфер обмена не пишутся.
    /// </summary>
    private void TraceDisplay(TimeSpan now, string why)
    {
        _lastWatchTrace = now;
        if (_options.DisplayTrace is not { } path)
        {
            return;
        }

        using var p = Process.GetCurrentProcess();
        try
        {
            System.IO.File.AppendAllText(path, $"{now.TotalSeconds:F1}\t{why}\t{Watch.Line(now)}\tprivacy={Privacy.State}/{Privacy.Reason} v{Privacy.Version} " +
                $"timeouts={Privacy.Timeouts} closures={Privacy.Closures}\toverlays={AllOverlays.Count} intended={OverlayIntendedVisible} " +
                $"recovery={Recovery.Attempts}/{Recovery.Refused}\tmem={p.PrivateMemorySize64 / (1024 * 1024)}MB threads={p.Threads.Count} handles={p.HandleCount}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }

    public DisplayPipelineStats DisplayStats()
    {
        var now = _uptime.Elapsed;
        using var p = Process.GetCurrentProcess();
        return new DisplayPipelineStats(Watch.Stage.ToString(), Math.Round((now - Watch.StageSince).TotalSeconds, 1), Watch.KeyEvents, Watch.Shown,
            Watch.Pending, Watch.Hidden, Watch.ViewsApplied, Watch.Refreshes, Watch.LastVisuals, Watch.Recoveries,
            Watch.Entered.ToDictionary(e => e.Key.ToString(), e => e.Value), $"{Privacy.State}/{Privacy.Reason}", AllOverlays.Count,
            p.PrivateMemorySize64 / (1024 * 1024), p.Threads.Count);
    }

    // ── здоровье ──────────────────────────────────────────────────────────────

    private void HealthTick()
    {
        var now = _uptime.Elapsed;
        if (_lastHealthTick != TimeSpan.Zero && now - _lastHealthTick > TimeSpan.FromSeconds(1.5))
        {
            Report(FindingCode.UiDispatcherSlow, FindingLevel.Suspicion);
        }

        _lastHealthTick = now;

        foreach (var s in Sources)
        {
            s.Status = s.Host.Status.ToString();
            SetFinding(FindingCode.SourceThreadExited, FindingLevel.Confirmed, s.Host.Status == HostStatus.ExitedUnexpectedly);
            if (s.Host is RawInputSource ri)
            {
                SetFinding(FindingCode.RawInputRegistrationLost, FindingLevel.Confirmed, ri.IsRegistrationIntact() == false);
            }

            long mark = s.Capture.EventsTotal;
            s.ActivityDelta = mark - s.ActivityMark;
            s.ActivityMark = mark;
        }

        // Наблюдение, а не доказательство: источники видят разные потоки событий.
        if (Sources.Count == 2)
        {
            var (a, b) = (Sources[0], Sources[1]);
            bool silent = (a.ActivityDelta >= 5 && b.ActivityDelta == 0) || (b.ActivityDelta >= 5 && a.ActivityDelta == 0);
            SetFinding(FindingCode.SourceSilentWhilePeerActive, FindingLevel.Suspicion, silent);
        }

        if (Primary is not null)
        {
            bool pending = Primary.Reader.ResyncPending;
            _resyncPendingSince = pending ? _resyncPendingSince ?? now : null;
            SetFinding(FindingCode.ResyncNotCompleted, FindingLevel.Suspicion, _resyncPendingSince is { } since && now - since > TimeSpan.FromSeconds(2));

            // GetLastInputInfo — общий для сессии, не монотонный: только подозрение.
            long activity = Primary.Capture.EventsTotal + Primary.Capture.MouseMoves + Primary.Capture.WheelNotches;
            if (activity != _primaryActivityMark)
            {
                _primaryActivityMark = activity;
                _primaryActivityAt = now;
            }

            bool systemActive = KeyStateProbe.IdleMilliseconds() is < 1000;
            SetFinding(FindingCode.SystemInputWithoutSourceEvents, FindingLevel.Suspicion, systemActive && now - _primaryActivityAt > TimeSpan.FromSeconds(5));
        }

        _lastWindowState = OverlayState ?? _lastWindowState;
        CheckOverlayWindow();
        WatchTick(now);
    }

    private void SetFinding(FindingCode code, FindingLevel level, bool active)
    {
        if (active && _activeFindings.Add(code))
        {
            Health.Report(code, level, _uptime.Elapsed);
        }
        else if (!active)
        {
            _activeFindings.Remove(code);
        }
    }

    public void Report(FindingCode code, FindingLevel level) => Health.Report(code, level, _uptime.Elapsed);

    public IReadOnlyCollection<FindingCode> ActiveFindings => _activeFindings;

    // ── экспорт ───────────────────────────────────────────────────────────────

    public DiagnosticsExport BuildExport()
    {
        long now = Stopwatch.GetTimestamp();
        // После закрытия оверлея при завершении — последнее проверенное состояние.
        var st = ShuttingDown ? _lastWindowState : OverlayState ?? _lastWindowState;
        return new DiagnosticsExport(
            SchemaVersion: 2,
            Mode: _options.ModeName,
            PrimarySource: _options.Primary.ToString(),
            UptimeSeconds: Math.Round(_uptime.Elapsed.TotalSeconds, 1),
            Sources: Sources.Select(s => DiagnosticsBuilder.Source(s.Reader, s.Status, s.StartError, s.Callback, now)).ToList(),
            Reconcile: DiagnosticsBuilder.Reconcile(ReconcileCounters),
            ModelToUiApply: ModelToUi.ToStats(LatencyDefinitions.ModelToUiApply),
            Comparator: Comparator?.ToStats(),
            Window: new WindowStats(OverlayIntendedVisible, st?.Visible ?? false, st?.Topmost ?? false, st?.Minimized ?? false, st?.OnMonitor ?? false, Recovery.Attempts, Recovery.Refused),
            SystemEvents: new SystemEventStats(_sessionLock, _sessionUnlock, _desktopSwitch, _suspend, _resume, _displayChange),
            Findings: Health.Snapshot().ToList(),
            ShutdownNotes: ShutdownNotes.ToList(),
            Visual: SafeVisualStats(),
            Display: ShuttingDown ? null : DisplayStats());
    }

    private VisualStats? SafeVisualStats()
    {
        try
        {
            return VisualStatsProvider?.Invoke();
        }
        catch (InvalidOperationException)
        {
            return null; // окна уже закрыты при завершении
        }
    }
}
