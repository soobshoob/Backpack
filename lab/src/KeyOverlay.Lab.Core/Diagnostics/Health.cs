namespace KeyOverlay.Lab.Core.Diagnostics;

/// <summary>
/// Signal — сырой факт (счётчик/метка). Suspicion — эвристика: только индикатор, никаких действий.
/// Confirmed — прямое доказательство (ошибка API, проверенное состояние окна, завершившийся поток).
/// </summary>
public enum FindingLevel { Signal, Suspicion, Confirmed }

public enum FindingCode
{
    // источники
    SourceStartFailed,
    SourceThreadExited,
    RawInputRegistrationLost,
    SourceStopTimeout,

    // наблюдения (подозрения)
    SourceSilentWhilePeerActive,
    SystemInputWithoutSourceEvents,
    ResyncNotCompleted,
    UiDispatcherSlow,

    // окно оверлея
    OverlayTopmostLost,
    OverlayHiddenUnexpectedly,
    OverlayMinimized,
    OverlayOffMonitor,

    // прочее
    UnhandledException,

    // конвейер отображения (Phase 4I)
    DisplayStalled,
    OverlayCovered,
}

public sealed class HealthLog
{
    private readonly Dictionary<FindingCode, (FindingLevel Level, long Count, TimeSpan Last)> _findings = new();

    public void Report(FindingCode code, FindingLevel level, TimeSpan now)
    {
        _findings.TryGetValue(code, out var f);
        _findings[code] = (level, f.Count + 1, now);
    }

    public IEnumerable<FindingStat> Snapshot() =>
        _findings.OrderBy(kv => kv.Key).Select(kv => new FindingStat(kv.Key.ToString(), kv.Value.Level.ToString(), kv.Value.Count));
}

/// <summary>
/// Автоматическое восстановление стенда. Источники ввода не восстанавливаются автоматически никогда:
/// расхождение Raw Input и LL — наблюдение, а не доказательство снятия хука.
/// Восстанавливается только окно оверлея, только по подтверждённому отказу, если окно должно быть видимым
/// и приложение не завершается; с паузой между попытками и ограничением их числа.
/// </summary>
public sealed class RecoveryPolicy
{
    private readonly TimeSpan _cooldown;
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;
    private readonly Queue<TimeSpan> _recent = new();
    private TimeSpan? _lastAttempt;

    /// <param name="window">
    /// Окно ограничения: не больше <paramref name="maxAttempts"/> попыток за это время. Phase 4I: прежде лимит действовал на всё
    /// время работы процесса — после пятой потери topmost за день окно оверлея больше не восстанавливалось никогда, и
    /// кейкапы пропадали до выключения и включения показа.
    /// </param>
    public RecoveryPolicy(TimeSpan cooldown, int maxAttempts, TimeSpan? window = null)
    {
        _cooldown = cooldown;
        _maxAttempts = maxAttempts;
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    public int Attempts { get; private set; }

    public long Refused { get; private set; }

    public static bool IsRecoverable(FindingCode code) =>
        code is FindingCode.OverlayTopmostLost or FindingCode.OverlayHiddenUnexpectedly or FindingCode.OverlayMinimized or FindingCode.OverlayOffMonitor;

    public bool TryBegin(FindingCode code, FindingLevel level, bool overlayIntendedVisible, bool shuttingDown, TimeSpan now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= _window)
        {
            _recent.Dequeue();
        }

        bool allowed = level == FindingLevel.Confirmed
            && IsRecoverable(code)
            && overlayIntendedVisible
            && !shuttingDown
            && _recent.Count < _maxAttempts
            && (_lastAttempt is null || now - _lastAttempt.Value >= _cooldown);

        if (!allowed)
        {
            Refused++;
            return false;
        }

        Attempts++;
        _recent.Enqueue(now);
        _lastAttempt = now;
        return true;
    }
}
