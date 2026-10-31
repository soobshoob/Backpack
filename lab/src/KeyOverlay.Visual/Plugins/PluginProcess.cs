using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Backpack.Sdk;

namespace KeyOverlay.Visual.Plugins;

public enum PluginRunState { Starting, Running, Stopped, Failed }


/// <summary>
/// Запущенный плагин — отдельный процесс (<c>Backpack.exe --plugin-host &lt;папка&gt;</c>) в Job Object. Обмен — строки JSON
/// через stdin/stdout. Каждое входящее сообщение проверяется по возможностям manifest (без возможности — отклоняется и
/// считается нарушением); слишком длинные строки и поток сообщений выше лимита завершают процесс. Колбэки вызываются в
/// UI-потоке через <see cref="Dispatcher"/>. Сбой процесса — <see cref="Crashed"/>.
/// </summary>
public sealed class PluginProcess : IDisposable
{
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    public const int MaxMessagesPerSecond = 60;
    public const int MaxViolations = 5;

    private readonly PluginManifest _manifest;
    private readonly Dispatcher _dispatcher;
    /// <summary>
    /// Очередь сообщений процессу плагина (Phase 4I): запись в stdin — в отдельном потоке. Раньше UI-поток писал синхронно (в том
    /// числе каждую видимую клавишу), и плагин, переставший читать stdin, мог остановить интерфейс и показ клавиш. При
    /// переполнении новые сообщения отбрасываются (<see cref="DroppedMessages"/>), UI-поток не ждёт.
    /// </summary>
    private System.Collections.Concurrent.BlockingCollection<string>? _outbox;

    public const int OutboxCapacity = 512;

    /// <summary>Сколько сообщений отброшено из-за переполненной очереди (плагин не читает ввод).</summary>
    public long DroppedMessages => Interlocked.Read(ref _dropped);

    private long _dropped;
    private Process? _process;
    private nint _job;
    private Thread? _reader;
    private DispatcherTimer? _handshake;
    private bool _stopping;
    private int _violations;
    private long _windowStart;
    private int _windowCount;

    public PluginProcess(PluginManifest manifest, Dispatcher dispatcher)
    {
        _manifest = manifest;
        _dispatcher = dispatcher;
    }

    public PluginManifest Manifest => _manifest;

    public PluginRunState State { get; private set; } = PluginRunState.Stopped;

    public int Violations => _violations;

    public int? ProcessId => _process is { HasExited: false } p ? p.Id : null;

    /// <summary>Панель изменилась (null — скрыта).</summary>
    public event Action<PluginPanelSpec?>? PanelChanged;

    /// <summary>Запрос открыть или обновить инструмент (возможность проверена).</summary>
    public event Action<PluginToolRequest>? ToolRequested;

    public event Action<string>? ToolCloseRequested;

    public event Action<string>? ToolEditRequested;

    /// <summary>Действие на холсте рисования (SDK 1.2; проверено: известное действие, есть overlay.draw).</summary>
    public event Action<string, string>? ToolActionRequested;

    /// <summary>Показать индикатор (null — скрыть; SDK 1.2, overlay.indicator).</summary>
    public event Action<Backpack.Sdk.IndicatorRequest?>? IndicatorRequested;

    /// <summary>Плагин записывает свои настройки (SDK 1.2); значения проверяет хранилище по схеме manifest.</summary>
    public event Action<JsonObject>? SettingsUpdateRequested;

    /// <summary>Проверка иконки кнопки панели (по умолчанию — любая принимается; хост подставляет свой набор).</summary>
    public Func<string, bool> IconExists { get; set; } = _ => true;

    /// <summary>Процесс завершился не по команде остановки, не ответил на рукопожатие или нарушил протокол.</summary>
    public event Action<string>? Crashed;

    public event Action? Ready;

    /// <summary>Запустить хост плагина. <paramref name="hostExe"/> — Backpack.exe; <paramref name="pluginDir"/> — папка установленного пакета.</summary>
    public bool Start(string hostExe, string pluginDir, string locale, JsonObject settings)
    {
        if (_process is not null)
        {
            return true;
        }

        var psi = new ProcessStartInfo(hostExe)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            WorkingDirectory = pluginDir,
        };
        psi.ArgumentList.Add("--plugin-host");
        psi.ArgumentList.Add(pluginDir);
        try
        {
            _process = Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            State = PluginRunState.Failed;
            return false;
        }

        if (_process is null)
        {
            State = PluginRunState.Failed;
            return false;
        }

        _job = JobObject.CreateFor(_process);
        State = PluginRunState.Starting;
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => _dispatcher.BeginInvoke(OnExited);
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "Backpack.Plugin." + _manifest.Id };
        _reader.Start();
        StartWriter(_process.StandardInput);
        Send(new JsonObject { ["t"] = Protocol.Hello, ["sdk"] = SdkInfo.Version, ["locale"] = locale, ["settings"] = settings.DeepClone() });
        _handshake = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = HandshakeTimeout };
        _handshake.Tick += (_, _) =>
        {
            _handshake?.Stop();
            if (State == PluginRunState.Starting)
            {
                Fail("handshake timeout");
            }
        };
        _handshake.Start();
        return true;
    }

    /// <summary>Команда плагину (локальный ID из manifest). Без ожидания ответа.</summary>
    public void Command(string id)
    {
        if (State == PluginRunState.Running && _manifest.Commands.Any(c => c.Id == id))
        {
            Send(new JsonObject { ["t"] = Protocol.Command, ["id"] = id });
        }
    }

    /// <summary>Отпущено сочетание команды с «hold» (или превышено время удержания).</summary>
    public void CommandReleased(string id)
    {
        if (State == PluginRunState.Running && _manifest.Commands.Any(c => c.Id == id && c.Hold))
        {
            Send(new JsonObject { ["t"] = Protocol.Command, ["id"] = id, ["phase"] = "up" });
        }
    }

    /// <summary>Инструмент закрыт хостом (Esc, отмена, система).</summary>
    public void ToolClosed(string toolId, string reason)
    {
        if (State == PluginRunState.Running)
        {
            Send(new JsonObject { ["t"] = Protocol.ToolClosed, ["id"] = toolId, ["reason"] = reason });
        }
    }

    public void PanelButton(string buttonId)
    {
        if (State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayPanel))
        {
            Send(new JsonObject { ["t"] = Protocol.PanelButtonPressed, ["id"] = buttonId });
        }
    }

    public void SettingsChanged(JsonObject values)
    {
        if (State == PluginRunState.Running)
        {
            Send(new JsonObject { ["t"] = Protocol.SettingsChanged, ["values"] = values.DeepClone() });
        }
    }

    /// <summary>Смена раскладки — только плагинам с возможностью input.layout (SDK 1.2).</summary>
    public void LayoutChanged(string code, string? detail, string reason)
    {
        if (State == PluginRunState.Running && _manifest.Has(Capabilities.InputLayout))
        {
            Send(new JsonObject { ["t"] = Protocol.Layout, ["code"] = code, ["detail"] = detail, ["reason"] = reason });
        }
    }

    /// <summary>Пользователь изменил параметры инструмента на нём самом (SDK 1.2).</summary>
    public void ToolState(string toolId, JsonObject values)
    {
        if (State == PluginRunState.Running)
        {
            Send(new JsonObject { ["t"] = Protocol.ToolState, ["id"] = toolId, ["values"] = values.DeepClone() });
        }
    }

    /// <summary>Видимое нажатие — только плагинам с возможностью input.visible и только то, что прошло фильтр приватности.</summary>
    public void VisibleKey(string key, bool down)
    {
        if (State == PluginRunState.Running && _manifest.Has(Capabilities.InputVisible))
        {
            Send(new JsonObject { ["t"] = Protocol.Key, ["key"] = key, ["down"] = down });
        }
    }

    /// <summary>Остановка: сообщение stop, затем принудительное завершение через <see cref="StopTimeout"/>. Панель скрывается.</summary>
    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        _stopping = true;
        _handshake?.Stop();
        Send(new JsonObject { ["t"] = Protocol.Stop });
        try
        {
            if (!_process.WaitForExit(StopTimeout))
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch (InvalidOperationException)
        {
        }

        Cleanup();
        State = PluginRunState.Stopped;
        PanelChanged?.Invoke(null);
    }

    private void Fail(string reason)
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        try
        {
            _process?.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        Cleanup();
        State = PluginRunState.Failed;
        PanelChanged?.Invoke(null);
        Crashed?.Invoke(reason);
    }

    private void OnExited()
    {
        if (!_stopping)
        {
            Fail("process exited");
        }
    }

    private void Cleanup()
    {
        _outbox?.CompleteAdding();
        _outbox = null;
        _handshake?.Stop();
        _handshake = null;
        _process?.Dispose();
        _process = null;
        JobObject.Close(_job);
        _job = 0;
    }

    private void Send(JsonObject msg)
    {
        var outbox = _outbox;
        if (outbox is null || outbox.IsAddingCompleted)
        {
            return;
        }

        try
        {
            if (!outbox.TryAdd(Protocol.Write(msg)))
            {
                Interlocked.Increment(ref _dropped);
            }
        }
        catch (InvalidOperationException)
        {
            // очередь закрыта — процесс завершается
        }
    }

    private void StartWriter(StreamWriter input)
    {
        var outbox = new System.Collections.Concurrent.BlockingCollection<string>(OutboxCapacity);
        _outbox = outbox;
        new Thread(() =>
        {
            try
            {
                foreach (var line in outbox.GetConsumingEnumerable())
                {
                    input.WriteLine(line);
                    input.Flush();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
            {
                // процесс завершился — сбой будет сообщён через Exited
            }
        })
        { IsBackground = true, Name = "Backpack.Plugin.Out." + _manifest.Id }.Start();
    }

    private void ReadLoop()
    {
        var p = _process;
        if (p is null)
        {
            return;
        }

        try
        {
            var reader = p.StandardOutput;
            var buffer = new StringBuilder();
            int ch;
            while ((ch = reader.Read()) >= 0)
            {
                if (ch == '\n')
                {
                    string line = buffer.ToString().TrimEnd('\r');
                    buffer.Clear();
                    _dispatcher.BeginInvoke(() => Handle(line));
                    continue;
                }

                if (buffer.Length >= Protocol.MaxLine)
                {
                    _dispatcher.BeginInvoke(() => Fail("message too long"));
                    return;
                }

                buffer.Append((char)ch);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    /// <summary>Проверка сообщения на границе SDK (UI-поток).</summary>
    internal void Handle(string line)
    {
        if (State is PluginRunState.Stopped or PluginRunState.Failed)
        {
            return;
        }

        long now = Environment.TickCount64;
        if (now - _windowStart > 1000)
        {
            _windowStart = now;
            _windowCount = 0;
        }

        if (++_windowCount > MaxMessagesPerSecond)
        {
            Violation();
            return;
        }

        JsonObject? msg = null;
        try
        {
            msg = JsonNode.Parse(line) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
        }

        switch ((string?)msg?["t"])
        {
            case Protocol.Ready when State == PluginRunState.Starting:
                if (!SdkInfo.IsCompatible((string?)msg!["sdk"]))
                {
                    Fail("sdk mismatch");
                    return;
                }

                State = PluginRunState.Running;
                _handshake?.Stop();
                Ready?.Invoke();
                break;
            case Protocol.Panel when State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayPanel):
                PanelChanged?.Invoke(PluginToolParser.ParsePanel(msg!, IconExists));
                break;
            case Protocol.ToolOpen when State == PluginRunState.Running:
                var request = PluginToolParser.Parse(msg!, out string? kind);
                bool allowed = kind switch
                {
                    "magnifier" => _manifest.Has(Capabilities.OverlayMagnifier),
                    "dim" => _manifest.Has(Capabilities.OverlayDim),
                    "draw" => _manifest.Has(Capabilities.OverlayDraw),
                    _ => false,
                };
                if (request is null || !allowed)
                {
                    Violation(); // неизвестный вид, неверный ID или нет заявленной возможности
                    return;
                }

                ToolRequested?.Invoke(request);
                break;
            case Protocol.ToolClose or Protocol.ToolEdit when State == PluginRunState.Running && HasToolCapability():
                string? toolId = (string?)msg!["id"];
                if (!PluginToolParser.IsValidToolId(toolId))
                {
                    Violation();
                    return;
                }

                if ((string?)msg["t"] == Protocol.ToolClose)
                {
                    ToolCloseRequested?.Invoke(toolId!);
                }
                else
                {
                    ToolEditRequested?.Invoke(toolId!);
                }

                break;
            case Protocol.PanelHide when State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayPanel):
                PanelChanged?.Invoke(null);
                break;
            case Protocol.ToolAction when State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayDraw):
                string? actionTool = (string?)msg!["id"];
                if (!PluginToolParser.IsValidToolId(actionTool) || PluginToolParser.ParseDrawAction((string?)msg["action"]) is not { } action)
                {
                    Violation();
                    return;
                }

                ToolActionRequested?.Invoke(actionTool!, action);
                break;
            case Protocol.Indicator when State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayIndicator):
                if (PluginToolParser.ParseIndicator(msg!) is not { } indicator)
                {
                    Violation();
                    return;
                }

                IndicatorRequested?.Invoke(indicator);
                break;
            case Protocol.IndicatorHide when State == PluginRunState.Running && _manifest.Has(Capabilities.OverlayIndicator):
                IndicatorRequested?.Invoke(null);
                break;
            case Protocol.SettingsUpdate when State == PluginRunState.Running && _manifest.Has(Capabilities.Settings):
                if (msg!["values"] is not JsonObject values || values.Count > 64)
                {
                    Violation();
                    return;
                }

                SettingsUpdateRequested?.Invoke(values);
                break;
            case Protocol.Error:
                Fail("plugin error");
                break;
            default:
                Violation(); // неизвестный тип или запрос без заявленной возможности
                break;
        }
    }

    private bool HasToolCapability() => _manifest.Has(Capabilities.OverlayMagnifier) || _manifest.Has(Capabilities.OverlayDim) || _manifest.Has(Capabilities.OverlayPanel)
        || _manifest.Has(Capabilities.OverlayDraw);

    /// <summary>Тесты: состояние «ожидание рукопожатия» без запуска процесса.</summary>
    internal void BeginForTest() => State = PluginRunState.Starting;

    private void Violation()
    {
        if (++_violations >= MaxViolations)
        {
            Fail("protocol violations");
        }
    }

    public void Dispose()
    {
        Stop();
        PanelChanged = null;
        ToolRequested = null;
        ToolCloseRequested = null;
        ToolEditRequested = null;
        Crashed = null;
        Ready = null;
    }
}

/// <summary>Job Object: процесс плагина завершается вместе с Backpack и не может запускать дочерние процессы.</summary>
internal static class JobObject
{
    public static nint CreateFor(Process p)
    {
        nint job = CreateJobObjectW(0, null);
        if (job == 0)
        {
            return 0;
        }

        var info = new ExtendedLimit
        {
            Basic = new BasicLimit { LimitFlags = 0x2000 /* KILL_ON_JOB_CLOSE */ | 0x0008 /* ACTIVE_PROCESS */, ActiveProcessLimit = 1 },
        };
        int size = Marshal.SizeOf<ExtendedLimit>();
        if (!SetInformationJobObject(job, 9 /* JobObjectExtendedLimitInformation */, ref info, size) || !AssignProcessToJobObject(job, p.Handle))
        {
            CloseHandle(job);
            return 0;
        }

        return job;
    }

    public static void Close(nint job)
    {
        if (job != 0)
        {
            CloseHandle(job);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong A, B, C, D, E, F;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateJobObjectW(nint attributes, string? name);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(nint job, int infoClass, ref ExtendedLimit info, int size);

    [DllImport("kernel32.dll")]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
