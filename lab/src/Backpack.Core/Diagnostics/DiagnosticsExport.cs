using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backpack.Core.Diagnostics;

// Экспорт содержит только агрегаты: счётчики, перцентили, флаги и коды находок.
// Никаких кодов клавиш, подписей, символов, последовательностей и меток отдельных событий.
// Тест ExportContainsOnlyAggregateFields проверяет набор имён полей.

public sealed record LatencyStats(string Definition, long Count, long? P50UpperMicros, long? P95UpperMicros, long? P99UpperMicros, long? MaxMicros);

public sealed record SourceStats(
    string Source,
    string Status,
    int? StartError,
    long EventsTotal,
    long Repeats,
    long Ignored,
    long MouseMoves,
    long WheelNotches,
    long DevicesRemoved,
    long RingCapacity,
    long RingDepth,
    long DropEpoch,
    long SnapshotsCreated,
    long ResyncRequests,
    long SnapshotRerequests,
    long EventsApplied,
    long DuplicatesSkipped,
    long SnapshotsApplied,
    long SnapshotCorrections,
    long MarkedUnknown,
    long UnknownResolvedByEvent,
    long StaleNoRepeat,
    long ReconciledUp,
    long ReconciledDown,
    long DeviceRemovedReleases,
    int KeysDownNow,
    int KeysUnknownNow,
    long? SecondsSinceLastEvent,
    LatencyStats CallbackOrMessage,
    LatencyStats CaptureToModel,
    long WheelEvents = 0,
    long ModifierChords = 0,
    long WinChords = 0);

public sealed record ReconcileStats(long Runs, long Untrusted, long NoPositiveControl, long AmbiguousVk, long ConfirmedDown);

public sealed record ComparatorClassStats(string Class, long Matched, long RawOnly, long LowLevelOnly);

public sealed record ComparatorStats(double WindowMs, long PendingOverflow, IReadOnlyList<ComparatorClassStats> Classes);

public sealed record WindowStats(bool IntendedVisible, bool Visible, bool Topmost, bool Minimized, bool OnMonitor, int RecoveryAttempts, long RecoveryRefused);

public sealed record SystemEventStats(long SessionLock, long SessionUnlock, long DesktopSwitch, long Suspend, long Resume, long DisplayChange);

public sealed record FindingStat(string Code, string Level, long Count);

/// <summary>
/// Наблюдение за раскладкой: только итог последнего опроса (имя состояния) и счётчики. Без окон, заголовков,
/// процессов, HKL, клавиш и времени отдельных событий.
/// </summary>
public sealed record LayoutIndicatorStats(
    bool Enabled,
    bool Polling,
    string LastOutcome,
    string LastDecision,
    long Samples,
    long NoForeground,
    long Unavailable,
    long OwnWindow,
    long FocusThread,
    long ActiveThread,
    long OwnerThreadFallback,
    long ThreadsDiffer,
    long OwnerHklDiffers,
    long Baselines,
    long ShownInWindow,
    long ShownWindowSwitch,
    long SuppressedWindowSwitch,
    long Shown);

/// <summary>Мышь у курсора: включение, видимость окна и число перемещений окна (без координат).</summary>
public sealed record MouseCursorStats(bool Enabled, bool WindowVisible, long Moves);

/// <summary>Визуальный слой: режимы ряда, раскладка, мышь у курсора.</summary>
public sealed record VisualStats(string KeyFilter, string Trail, bool MouseInRow, LayoutIndicatorStats Layout, MouseCursorStats MouseCursor);

/// <summary>Конвейер отображения (Phase 4I): этап последнего показанного нажатия и счётчики — без клавиш и текста.</summary>
public sealed record DisplayPipelineStats(string Stage, double StageSeconds, long KeyEvents, long Shown, long Pending, long Hidden, long ViewsApplied,
    long Refreshes, int Visuals, long Recoveries, IReadOnlyDictionary<string, long> Entered, string Privacy, int OverlayWindows, long PrivateMegabytes, int Threads);

public sealed record DiagnosticsExport(
    int SchemaVersion,
    string Mode,
    string PrimarySource,
    double UptimeSeconds,
    IReadOnlyList<SourceStats> Sources,
    ReconcileStats Reconcile,
    LatencyStats ModelToUiApply,
    ComparatorStats? Comparator,
    WindowStats Window,
    SystemEventStats SystemEvents,
    IReadOnlyList<FindingStat> Findings,
    IReadOnlyList<string> ShutdownNotes,
    VisualStats? Visual = null,
    DisplayPipelineStats? Display = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
