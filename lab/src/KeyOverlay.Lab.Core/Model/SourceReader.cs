using System.Diagnostics;
using KeyOverlay.Lab.Core.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Transport;

namespace KeyOverlay.Lab.Core.Model;

/// <summary>
/// Потребитель одного источника (UI-поток): вычитывает кольцо в модель, обнаруживает потери и
/// выполняет ресинхронизацию по снимку. Снимок строится в потоке захвата по запросу
/// (<paramref name="requestSnapshot"/>), поэтому он согласован с событиями без блокировок.
/// </summary>
public sealed class SourceReader
{
    private readonly CaptureState _capture;
    private readonly Action _requestSnapshot;
    private long _resyncedEpoch;
    private bool _snapshotRequested;
    private long _snapshotRequestedAt;

    public SourceReader(CaptureState capture, SourceModel model, Action requestSnapshot)
    {
        _capture = capture;
        Model = model;
        _requestSnapshot = requestSnapshot;
    }

    public SourceKind Kind => _capture.Kind;

    public CaptureState Capture => _capture;

    public SourceModel Model { get; }

    /// <summary>Задержка «событие получено потоком захвата → применено к модели».</summary>
    public LatencyHistogram CaptureToModel { get; } = new();

    public long ResyncRequests { get; private set; }

    public long SnapshotRerequests { get; private set; }

    public bool ResyncPending => _snapshotRequested;

    /// <param name="onEvent">Вызывается для каждого применённого события (сравнение источников).</param>
    /// <returns>true, если в кольце остались события сверх <paramref name="maxBatch"/>.</returns>
    public bool Drain(int maxBatch, List<KeyTransition> output, Action<InputEvent>? onEvent = null)
    {
        _capture.AcknowledgeSignal();

        // 1. Снимок применяется раньше кольца: в кольце после него остаются только события с большими номерами
        //    (меньшие отбрасываются по LastAppliedSeq).
        var snapshot = _capture.TakePublishedSnapshot();
        if (snapshot is not null)
        {
            Model.ApplySnapshot(snapshot, output);
            if (snapshot.DropEpoch > _resyncedEpoch)
            {
                _resyncedEpoch = snapshot.DropEpoch;
            }

            _snapshotRequested = false;
        }

        // 2. События.
        int processed = 0;
        while (processed < maxBatch && _capture.Ring.TryRead(out var e))
        {
            processed++;
            long now = Stopwatch.GetTimestamp();
            CaptureToModel.RecordTicks(now - e.CaptureTimestamp);
            Model.ApplyEvent(e, now, output);
            onEvent?.Invoke(e);
        }

        // 3. Потери. Проверяется при каждом опросе, а не только при новых событиях:
        //    если потеряно последнее событие, его покажет рост эпохи при следующем вызове (резервный таймер).
        CheckForLoss();

        return _capture.Ring.Count > 0;
    }

    private void CheckForLoss()
    {
        long epoch = _capture.DropEpoch;
        long now = Stopwatch.GetTimestamp();
        if (epoch > _resyncedEpoch && !_snapshotRequested)
        {
            _snapshotRequested = true;
            _snapshotRequestedAt = now;
            ResyncRequests++;
            _requestSnapshot();
        }
        else if (_snapshotRequested && now - _snapshotRequestedAt > Stopwatch.Frequency)
        {
            // Запрос мог не дойти (например, поток захвата занят или завершён) — повторить не чаще раза в секунду.
            _snapshotRequestedAt = now;
            SnapshotRerequests++;
            _requestSnapshot();
        }
    }
}
