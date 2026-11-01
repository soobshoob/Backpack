using System.Diagnostics;
using Backpack.Core.Model;

namespace Backpack.Core.Diagnostics;

public static class LatencyDefinitions
{
    public const string LowLevelCallback = "LL: длительность callback хука (вход → перед CallNextHookEx)";
    public const string RawInputMessage = "Raw Input: обработка одного WM_INPUT (вход в обработчик → выход)";
    public const string CaptureToModel = "Поток захвата получил событие → событие применено к модели в UI-потоке (не время до пикселя)";
    public const string ModelToUiApply = "Модель изменена → свойства оверлея обновлены в UI-потоке (до композиции и вывода кадра, не время до пикселя)";
}

public static class DiagnosticsBuilder
{
    public static SourceStats Source(SourceReader reader, string status, int? startError, LatencyHistogram callback, long nowTimestamp)
    {
        var c = reader.Capture;
        var m = reader.Model.Counters;
        long last = c.LastEventTimestamp;
        return new SourceStats(
            Source: reader.Kind.ToString(),
            Status: status,
            StartError: startError,
            EventsTotal: c.EventsTotal,
            Repeats: c.Repeats,
            Ignored: c.Ignored,
            MouseMoves: c.MouseMoves,
            WheelNotches: c.WheelNotches,
            DevicesRemoved: c.DevicesRemoved,
            RingCapacity: c.Ring.Capacity,
            RingDepth: c.Ring.Count,
            DropEpoch: c.DropEpoch,
            SnapshotsCreated: c.SnapshotsCreated,
            ResyncRequests: reader.ResyncRequests,
            SnapshotRerequests: reader.SnapshotRerequests,
            EventsApplied: m.EventsApplied,
            DuplicatesSkipped: m.DuplicatesSkipped,
            SnapshotsApplied: m.SnapshotsApplied,
            SnapshotCorrections: m.SnapshotCorrections,
            MarkedUnknown: m.MarkedUnknown,
            UnknownResolvedByEvent: m.UnknownResolvedByEvent,
            StaleNoRepeat: m.StaleNoRepeat,
            ReconciledUp: m.ReconciledUp,
            ReconciledDown: m.ReconciledDown,
            DeviceRemovedReleases: m.DeviceRemovedReleases,
            KeysDownNow: reader.Model.CountInState(KeyState.Down),
            KeysUnknownNow: reader.Model.CountInState(KeyState.Unknown),
            SecondsSinceLastEvent: last == 0 ? null : (nowTimestamp - last) / Stopwatch.Frequency,
            CallbackOrMessage: callback.ToStats(reader.Kind == Input.SourceKind.LowLevelHook ? LatencyDefinitions.LowLevelCallback : LatencyDefinitions.RawInputMessage),
            CaptureToModel: reader.CaptureToModel.ToStats(LatencyDefinitions.CaptureToModel),
            WheelEvents: c.WheelEvents,
            ModifierChords: m.ModifierChords,
            WinChords: m.WinChords);
    }

    public static ReconcileStats Reconcile(ReconcileCounters c) => new(c.Runs, c.Untrusted, c.NoPositiveControl, c.AmbiguousVk, c.ConfirmedDown);
}
