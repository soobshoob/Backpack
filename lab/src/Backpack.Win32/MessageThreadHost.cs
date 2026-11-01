using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backpack.Win32.Interop;

namespace Backpack.Win32;

public enum HostStatus { NotStarted, Starting, Running, StartFailed, Stopping, Stopped, ExitedUnexpectedly }

public readonly record struct StartResult(bool Ok, int Win32Error, string Stage);

public readonly record struct StopResult(bool Clean, double Milliseconds, string Note);

/// <summary>
/// Собственный поток с message-only окном и циклом GetMessage. Наследник регистрирует обработчики
/// в <see cref="OnThreadStart"/> и снимает их в <see cref="OnThreadStop"/> — оба вызываются в этом же потоке.
/// Остановка и запрос снимка передаются окну через PostMessage, без ожидания UI.
/// </summary>
public abstract unsafe class MessageThreadHost
{
    protected const uint WmAppStop = Native.WM_APP + 1;
    protected const uint WmAppSnapshot = Native.WM_APP + 2;

    [ThreadStatic]
    private static MessageThreadHost? t_current;

    private readonly ManualResetEventSlim _ready = new(false);
    private readonly string _className = "BackpackLab." + Guid.NewGuid().ToString("N");
    private Thread? _thread;
    private volatile bool _stopRequested;
    private bool _stoppedInThread;
    private int _status = (int)HostStatus.NotStarted;

    protected MessageThreadHost(string name) => Name = name;

    public string Name { get; }

    public HostStatus Status => (HostStatus)Volatile.Read(ref _status);

    public StartResult LastStart { get; private set; }

    /// <summary>Ошибки, перехваченные в обработчиках потока (не должны выходить за границу native-вызова).</summary>
    public long HandlerExceptions => Interlocked.Read(ref _handlerExceptions);

    private long _handlerExceptions;

    public int StopError { get; protected set; }

    public nint Hwnd { get; private set; }

    public StartResult Start(TimeSpan timeout)
    {
        SetStatus(HostStatus.Starting);
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = Name };
        _thread.Start();
        if (!_ready.Wait(timeout))
        {
            LastStart = new StartResult(false, 0, "timeout");
            SetStatus(HostStatus.StartFailed);
            return LastStart;
        }

        return LastStart;
    }

    /// <summary>Попросить поток построить снимок (обрабатывается в потоке источника).</summary>
    public bool RequestSnapshot() => Hwnd != 0 && Status == HostStatus.Running && Native.PostMessage(Hwnd, WmAppSnapshot, 0, 0);

    public StopResult Stop(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        if (_thread is null || Status is HostStatus.NotStarted or HostStatus.StartFailed or HostStatus.Stopped)
        {
            return new StopResult(true, 0, "not running");
        }

        _stopRequested = true;
        SetStatus(HostStatus.Stopping);
        if (!Native.PostMessage(Hwnd, WmAppStop, 0, 0))
        {
            return new StopResult(false, sw.Elapsed.TotalMilliseconds, $"PostMessage(stop) failed: {Marshal.GetLastPInvokeError()}");
        }

        bool joined = _thread.Join(timeout);
        return joined
            ? new StopResult(StopError == 0, sw.Elapsed.TotalMilliseconds, StopError == 0 ? "stopped" : $"stopped, unregister error {StopError}")
            : new StopResult(false, sw.Elapsed.TotalMilliseconds, "join timeout");
    }

    /// <returns>0 — успех, иначе код ошибки Win32.</returns>
    protected abstract int OnThreadStart(nint hwnd, out string stage);

    protected abstract void OnThreadStop();

    /// <returns>true — сообщение обработано, <paramref name="result"/> возвращается системе.</returns>
    protected virtual bool OnMessage(nint hwnd, uint msg, nint wParam, nint lParam, out nint result)
    {
        result = 0;
        return false;
    }

    protected virtual void OnSnapshotRequested()
    {
    }

    protected void CountHandlerException() => Interlocked.Increment(ref _handlerExceptions);

    private void SetStatus(HostStatus s) => Volatile.Write(ref _status, (int)s);

    private void ThreadMain()
    {
        t_current = this;
        nint hInstance = Native.GetModuleHandle(null);
        fixed (char* cls = _className)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                lpszClassName = cls,
            };

            if (Native.RegisterClassEx(&wc) == 0)
            {
                Fail(Marshal.GetLastPInvokeError(), "RegisterClassEx");
                return;
            }

            nint hwnd = Native.CreateWindowEx(0, cls, null, 0, 0, 0, 0, 0, Native.HWND_MESSAGE, 0, hInstance, 0);
            if (hwnd == 0)
            {
                Fail(Marshal.GetLastPInvokeError(), "CreateWindowEx");
                Native.UnregisterClass(cls, hInstance);
                return;
            }

            Hwnd = hwnd;
            int error = OnThreadStart(hwnd, out string stage);
            if (error != 0)
            {
                Native.DestroyWindow(hwnd);
                Native.UnregisterClass(cls, hInstance);
                Fail(error, stage);
                return;
            }

            LastStart = new StartResult(true, 0, "running");
            SetStatus(HostStatus.Running);
            _ready.Set();

            MSG msg;
            int r;
            while ((r = Native.GetMessage(&msg, 0, 0, 0)) != 0)
            {
                if (r == -1)
                {
                    break; // ошибка цикла; ниже — аварийное снятие обработчиков
                }

                Native.DispatchMessage(&msg);
            }

            if (!_stoppedInThread)
            {
                SafeStop();
                Native.DestroyWindow(hwnd);
            }

            Native.UnregisterClass(cls, hInstance);
        }

        SetStatus(_stopRequested ? HostStatus.Stopped : HostStatus.ExitedUnexpectedly);
        t_current = null;
    }

    private void Fail(int error, string stage)
    {
        LastStart = new StartResult(false, error, stage);
        SetStatus(HostStatus.StartFailed);
        _ready.Set();
    }

    private void SafeStop()
    {
        try
        {
            OnThreadStop();
        }
        catch
        {
            CountHandlerException();
        }

        _stoppedInThread = true;
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        var self = t_current;
        if (self is null)
        {
            return Native.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        try
        {
            switch (msg)
            {
                case WmAppStop:
                    self.SafeStop();
                    Native.DestroyWindow(hwnd);
                    return 0;
                case Native.WM_DESTROY:
                    Native.PostQuitMessage(0);
                    return 0;
                case WmAppSnapshot:
                    self.OnSnapshotRequested();
                    return 0;
            }

            if (self.OnMessage(hwnd, msg, wParam, lParam, out nint result))
            {
                return result;
            }
        }
        catch
        {
            // Исключение не должно пересечь границу native-вызова.
            self.CountHandlerException();
        }

        return Native.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static long Now() => Stopwatch.GetTimestamp();
}
