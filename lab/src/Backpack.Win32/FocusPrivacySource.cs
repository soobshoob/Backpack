using System.Runtime.InteropServices;
using Backpack.Core.Privacy;
using Backpack.Win32.Interop;

namespace Backpack.Win32;

/// <summary>
/// Источник сведений для динамической приватности. Две части:
/// <list type="bullet">
/// <item>собственный поток с WinEvent-хуками EVENT_OBJECT_FOCUS и EVENT_SYSTEM_FOREGROUND (out-of-context): каждое событие только
///       увеличивает атомарный <see cref="FocusSerial"/> и будит обработчик — без UIA, без ожидания;</item>
/// <item>один рабочий поток (MTA) для UI Automation: по запросу версии читает у сфокусированного элемента только
///       <c>UIA_IsPasswordPropertyId</c> через <c>GetCurrentPropertyValueEx(…, ignoreDefaultValue: TRUE)</c> — так отсутствие
///       свойства у провайдера отличается от false. Value, TextPattern, Name, буфер обмена и содержимое поля не читаются.</item>
/// </list>
/// Проверки — только по событиям (<see cref="FocusProbePolicy"/>): после смены фокуса не больше одной проверки с подтверждением и
/// трёх перепроверок, затем ожидание следующего события; периодического опроса программы нет. Повторные уведомления о фокусе на
/// том же элементе в пределах секунды отсеиваются (<see cref="FocusNoiseFilter"/>). Ввод этот источник не перехватывает и не
/// подавляет: WinEvent-хуки вне контекста только наблюдают, UIA только читает IsPassword и метаданные.
/// Запросы не копятся: рабочий поток берёт только последнюю запрошенную версию. Если UIA зависла (целевое приложение не отвечает),
/// поток остаётся занятым одной проверкой до внутреннего таймаута UIA, новые запросы ждут её окончания, а таймаут ожидания
/// результата в <see cref="PrivacyGate"/> закрывает показ («нет данных»). Других потоков не создаётся.
/// </summary>
public sealed unsafe class FocusPrivacySource : MessageThreadHost
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectFocus = 0x8005;
    private const int UiaIsPasswordPropertyId = 30019;
    private const int UiaControlTypePropertyId = 30003;
    private const int UiaFrameworkIdPropertyId = 30024;

    [ThreadStatic]
    private static FocusPrivacySource? t_self;

    private static readonly Guid ClsidCuiAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    private static readonly Guid IidIuiAutomation = new("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE");

    /// <summary>Пауза перед подтверждающей пробой разрешающего ответа, мс (только рабочий поток UIA).</summary>
    public const int ConfirmDelayMs = FocusProbePolicy.ConfirmDelayMs;

    private static readonly System.Diagnostics.Stopwatch s_clock = System.Diagnostics.Stopwatch.StartNew();

    private readonly Action<long, ProbeVerdict> _result;
    private readonly Action _focusChanged;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private nint[] _hooks = [];
    private long _focusSerial;
    private long _requested;
    private long _requestedSerial;
    private volatile bool _stopping;
    private volatile string[] _blockedApps = [];
    private int _busy;

    /// <param name="result">Результат проверки версии (вызывается в рабочем потоке; передать на UI-поток — забота вызывающего).</param>
    /// <param name="focusChanged">Сигнал о смене фокуса (в потоке хуков; только постановка в очередь, без ожидания).</param>
    public FocusPrivacySource(Action<long, ProbeVerdict> result, Action focusChanged)
        : base("Backpack.Privacy.Focus")
    {
        _result = result;
        _focusChanged = focusChanged;
        _worker = new Thread(WorkerMain) { IsBackground = true, Name = "Backpack.Privacy.Uia" };
    }

    /// <summary>Отсев повторных уведомлений о фокусе на том же элементе (для диагностики — число пропущенных).</summary>
    public FocusNoiseFilter Noise { get; } = new();

    /// <summary>Растёт при каждом событии фокуса или смены активного окна. Читается синхронно перед применением ввода.</summary>
    public long FocusSerial => Interlocked.Read(ref _focusSerial);

    /// <summary>Рабочий поток UIA сейчас выполняет проверку.</summary>
    public bool Busy => Volatile.Read(ref _busy) != 0;

    public long Probes => Interlocked.Read(ref _probes);

    public long ProbeFailures => Interlocked.Read(ref _probeFailures);

    private long _probes;
    private long _probeFailures;

    /// <summary>Имена исполняемых файлов (без пути, без учёта регистра), для которых показ всегда закрыт.</summary>
    public void SetBlockedApps(IEnumerable<string> names) =>
        _blockedApps = names.Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length > 0).Distinct().ToArray();

    /// <summary>Отметить смену контекста, замеченную вызывающим (например, сменилось окно с фокусом клавиатуры).</summary>
    public void BumpSerial() => Bump();

    /// <summary>Смена фокуса из другого источника (событие фокуса UI Automation): как событие WinEvent — счётчик и сигнал.</summary>
    public void SignalFocusChanged() => SignalFocusChanged(null);

    /// <summary>
    /// Событие фокуса UI Automation на элементе с ключом <paramref name="elementKey"/> (<see cref="FocusNoiseFilter.RuntimeIdKey"/>;
    /// null — неизвестен). Повтор того же элемента в пределах окна шума не начинает новую проверку.
    /// </summary>
    public void SignalFocusChanged(string? elementKey)
    {
        if (!Noise.Accept(FocusSource.UiAutomation, elementKey, s_clock.Elapsed))
        {
            return;
        }

        Bump();
        _focusChanged();
    }

    /// <summary>Время (Stopwatch) первой смены фокуса после прошлого чтения; 0 — смен не было.</summary>
    private long _firstBumpStamp;

    private void Bump()
    {
        Interlocked.CompareExchange(ref _firstBumpStamp, System.Diagnostics.Stopwatch.GetTimestamp(), 0);
        Interlocked.Increment(ref _focusSerial);
    }

    /// <summary>
    /// Забрать время первой смены фокуса с прошлого вызова (0 — не было). Ввод, захваченный раньше этого времени, относится к
    /// прежнему контексту: уведомление приходит после самой смены, поэтому граница не может оказаться раньше настоящей.
    /// </summary>
    public long TakeFirstChangeStamp() => Interlocked.Exchange(ref _firstBumpStamp, 0);

    /// <summary>Попросить проверить версию <paramref name="version"/>. Предыдущий незавершённый запрос заменяется.</summary>
    public void Request(long version)
    {
        Interlocked.Exchange(ref _requestedSerial, FocusSerial);
        Interlocked.Exchange(ref _requested, version);
        _wake.Set();
    }

    /// <summary>Окно с фокусом клавиатуры в потоке переднего плана (0 — неизвестно). Быстрый синхронный вызов без UIA.</summary>
    public static nint FocusWindow()
    {
        var info = new GuiThreadInfo { Size = sizeof(GuiThreadInfo) };
        return GetGUIThreadInfo(0, ref info) ? (info.Focus != 0 ? info.Focus : info.Active) : 0;
    }

    protected override int OnThreadStart(nint hwnd, out string stage)
    {
        stage = "SetWinEventHook";
        t_self = this;
        _hooks =
        [
            Native.SetWinEventHook(EventObjectFocus, EventObjectFocus, 0, &OnWinEvent, 0, 0, 0),
            Native.SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, &OnWinEvent, 0, 0, 0),
        ];
        if (_hooks.All(h => h == 0))
        {
            return Marshal.GetLastPInvokeError() is var e and not 0 ? e : -1;
        }

        _worker.Start();
        return 0;
    }

    protected override void OnThreadStop()
    {
        foreach (var h in _hooks)
        {
            if (h != 0)
            {
                Native.UnhookWinEvent(h);
            }
        }

        _hooks = [];
        _stopping = true;
        _wake.Set();
        t_self = null;
    }

    /// <summary>Дождаться рабочего потока после <see cref="MessageThreadHost.Stop"/> (не дольше <paramref name="timeout"/>).</summary>
    public bool JoinWorker(TimeSpan timeout) => !_worker.IsAlive || _worker.Join(timeout);

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            var self = t_self;
            if (self is null)
            {
                return;
            }

            if (evt == EventSystemForeground)
            {
                self.Noise.OnForegroundChanged();
            }
            else if (!self.Noise.Accept(FocusSource.WinEvent, FocusNoiseFilter.WinEventKey(hwnd, idObject, idChild), s_clock.Elapsed))
            {
                return; // повтор фокуса на том же элементе — контекст не менялся, проверять нечего
            }

            self.Bump();
            self._focusChanged();
        }
        catch
        {
            // не пропускать исключение через native-границу
        }
    }

    private void WorkerMain()
    {
        bool com = CoInitializeEx(0, 0 /* COINIT_MULTITHREADED */) >= 0;
        nint automation = 0;
        try
        {
            if (com)
            {
                fixed (Guid* clsid = &ClsidCuiAutomation, iid = &IidIuiAutomation)
                {
                    if (CoCreateInstance(clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, iid, &automation) < 0)
                    {
                        automation = 0;
                    }
                }
            }

            long lastVersion = -1;
            int retries = 0;
            int maxRetries = FocusProbePolicy.RetryDelaysMs.Count;
            while (!_stopping)
            {
                // Запрещающий ответ перепроверяется той же версией несколько раз (FocusProbePolicy): UIA может ещё отдавать прежний
                // элемент в момент события фокуса. После этого — только следующее событие: «нет данных» (например, 3D-вид CAD без
                // IsPassword) больше не опрашивается периодически, чтобы не обращаться к UI-потоку программы, пока в ней работают.
                // Перепроверка может только открыть показ позже; скрытое раньше не возвращается.
                int wait = FocusProbePolicy.NextWaitMs(retries);
                bool woke = _wake.WaitOne(wait);
                if (_stopping)
                {
                    break;
                }

                long version = Interlocked.Read(ref _requested);
                if (!woke && version != lastVersion)
                {
                    retries = maxRetries;
                    continue;
                }

                retries = woke && version != lastVersion ? 0 : Math.Min(retries + 1, maxRetries);
                lastVersion = version;
                long serialAtRequest = Interlocked.Read(ref _requestedSerial);
                Volatile.Write(ref _busy, 1);
                ProbeVerdict verdict;
                try
                {
                    verdict = Probe(automation);
                    if (verdict == ProbeVerdict.NotPassword)
                    {
                        // Разрешающий ответ подтверждается второй пробой: в момент события UIA может ещё отдавать
                        // прежний (не парольный) элемент. Показ закрыт всё это время; новые запросы не теряются
                        // (событие ожидания остаётся установленным).
                        Thread.Sleep(ConfirmDelayMs);
                        verdict = Probe(automation);
                    }
                }
                catch
                {
                    verdict = ProbeVerdict.NoData;
                }
                finally
                {
                    Volatile.Write(ref _busy, 0);
                }

                Interlocked.Increment(ref _probes);
                if (verdict == ProbeVerdict.NoData)
                {
                    Interlocked.Increment(ref _probeFailures);
                }

                // Фокус успел смениться во время проверки — результат относится неизвестно к какому элементу:
                // разрешающий ответ не отправляется (придёт новая версия), запрещающий безопасен.
                if (verdict == ProbeVerdict.NotPassword && FocusSerial != serialAtRequest)
                {
                    continue;
                }

                if (verdict == ProbeVerdict.NotPassword)
                {
                    retries = maxRetries; // открыто — перепроверять нечего
                }

                try
                {
                    _result(version, verdict);
                }
                catch
                {
                    // ошибка получателя не останавливает рабочий поток
                }
            }
        }
        finally
        {
            if (automation != 0)
            {
                Release(automation);
            }

            if (com)
            {
                CoUninitialize();
            }
        }
    }

    private ProbeVerdict Probe(nint automation)
    {
        if (IsBlockedForeground())
        {
            return ProbeVerdict.BlockedApp;
        }

        return automation == 0 ? ProbeVerdict.NoData : FocusClassifier.Classify(ReadFacts(automation));
    }

    /// <summary>
    /// IsPassword сфокусированного элемента; если провайдер его не поддерживает — ещё тип элемента, фреймворк и классы окна с
    /// фокусом (для единственного узкого исключения — списка значков рабочего стола, см. <see cref="FocusClassifier"/>).
    /// </summary>
    private static FocusFacts ReadFacts(nint automation)
    {
        nint element = 0;
        // IUIAutomation::GetFocusedElement — слот 8 vtable.
        var getFocused = (delegate* unmanaged[Stdcall]<nint, nint*, int>)(*(nint**)automation)[8];
        if (getFocused(automation, &element) < 0 || element == 0)
        {
            return new FocusFacts(PasswordProperty.Error);
        }

        try
        {
            var password = ReadProperty(element, UiaIsPasswordPropertyId, out var v) switch
            {
                VtBool => v.BoolVal != 0 ? PasswordProperty.True : PasswordProperty.False,
                VtUnknown or VtEmpty => PasswordProperty.NotSupported, // ReservedNotSupportedValue — не false
                _ => PasswordProperty.Error,
            };
            if (password != PasswordProperty.NotSupported)
            {
                return new FocusFacts(password);
            }

            int controlType = ReadProperty(element, UiaControlTypePropertyId, out var ct) == VtI4 ? ct.IntVal : 0;
            string? framework = ReadProperty(element, UiaFrameworkIdPropertyId, out var fw) == VtBstr ? fw.Text : null;
            nint focus = FocusWindow(), root = focus != 0 ? GetAncestor(focus, 2 /* GA_ROOT */) : 0;
            return new FocusFacts(password, controlType, framework, ClassOf(focus), ClassOf(focus != 0 ? GetAncestor(focus, 1 /* GA_PARENT */) : 0),
                ClassOf(root), root != 0 && root == GetForegroundWindow(), root != 0 ? ProcessName(root) : null);
        }
        finally
        {
            Release(element);
        }
    }

    private const ushort VtEmpty = 0, VtI4 = 3, VtBstr = 8, VtBool = 11, VtUnknown = 13;

    /// <summary>IUIAutomationElement::GetCurrentPropertyValueEx(id, ignoreDefaultValue: TRUE) — слот 11. Возвращает тип VARIANT (или 0xFFFF при ошибке).</summary>
    private static ushort ReadProperty(nint element, int propertyId, out PropertyValue value)
    {
        var v = default(Variant);
        var getProp = (delegate* unmanaged[Stdcall]<nint, int, int, Variant*, int>)(*(nint**)element)[11];
        int hr = getProp(element, propertyId, 1, &v);
        try
        {
            value = new PropertyValue(v.BoolVal, v.IntVal, v.Vt == VtBstr && v.PtrVal != 0 ? Marshal.PtrToStringBSTR(v.PtrVal) : null);
            return hr < 0 ? (ushort)0xFFFF : v.Vt;
        }
        finally
        {
            VariantClear(&v);
        }
    }

    private readonly record struct PropertyValue(short BoolVal, int IntVal, string? Text);

    private static string? ClassOf(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        char* buffer = stackalloc char[128];
        int n = GetClassNameW(hwnd, buffer, 128);
        return n > 0 ? new string(buffer, 0, n) : null;
    }

    /// <summary>Имя исполняемого файла владельца окна переднего плана — только имя файла, без заголовков окон.</summary>
    private bool IsBlockedForeground()
    {
        var blocked = _blockedApps;
        if (blocked.Length == 0)
        {
            return false;
        }

        string? name = ForegroundProcessName();
        return name is not null && blocked.Contains(name); // имя неизвестно — решает UIA (обычно «нет данных»)
    }

    public static string? ForegroundProcessName() => ProcessName(GetForegroundWindow());

    /// <summary>Имя исполняемого файла процесса — владельца окна (только имя файла).</summary>
    private static string? ProcessName(nint hwnd)
    {
        if (hwnd == 0 || GetWindowThreadProcessId(hwnd, out uint pid) == 0 || pid == 0)
        {
            return null;
        }

        nint process = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, 0, pid);
        if (process == 0)
        {
            return null;
        }

        try
        {
            char* buffer = stackalloc char[1024];
            int size = 1024;
            if (QueryFullProcessImageNameW(process, 0, buffer, ref size) == 0)
            {
                return null;
            }

            return Path.GetFileName(new string(buffer, 0, size)).ToLowerInvariant();
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static void Release(nint unknown)
    {
        var release = (delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)unknown)[2];
        release(unknown);
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct Variant
    {
        [FieldOffset(0)] public ushort Vt;
        [FieldOffset(8)] public short BoolVal;
        [FieldOffset(8)] public int IntVal;
        [FieldOffset(8)] public nint PtrVal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public int CaretLeft, CaretTop, CaretRight, CaretBottom;
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(Variant* v);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hwnd, char* buffer, int max);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [DllImport("kernel32.dll")]
    private static extern nint OpenProcess(uint access, int inherit, uint pid);

    [DllImport("kernel32.dll")]
    private static extern int CloseHandle(nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int QueryFullProcessImageNameW(nint process, uint flags, char* buffer, ref int size);
}
