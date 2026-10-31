using System.Diagnostics;
using System.Text.Json;
using KeyOverlay.Lab.Core.Diagnostics;
using KeyOverlay.Lab.Core.Input;
using KeyOverlay.Lab.Core.Model;
using static KeyOverlay.Lab.Core.Tests.Harness;

namespace KeyOverlay.Lab.Core.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Suspicion_NeverTriggersRecovery()
    {
        var p = new RecoveryPolicy(TimeSpan.FromSeconds(5), 3);
        var t = TimeSpan.FromSeconds(100);
        Assert.False(p.TryBegin(FindingCode.OverlayTopmostLost, FindingLevel.Suspicion, true, false, t));
        Assert.False(p.TryBegin(FindingCode.SourceSilentWhilePeerActive, FindingLevel.Suspicion, true, false, t));
        Assert.False(p.TryBegin(FindingCode.SystemInputWithoutSourceEvents, FindingLevel.Suspicion, true, false, t));
        Assert.Equal(0, p.Attempts);
    }

    [Fact]
    public void Sources_AreNeverRecoveredAutomatically_EvenWhenConfirmed()
    {
        var p = new RecoveryPolicy(TimeSpan.FromSeconds(5), 3);
        Assert.False(p.TryBegin(FindingCode.SourceThreadExited, FindingLevel.Confirmed, true, false, TimeSpan.Zero));
        Assert.False(p.TryBegin(FindingCode.RawInputRegistrationLost, FindingLevel.Confirmed, true, false, TimeSpan.Zero));
    }

    [Fact]
    public void ConfirmedWindowLoss_RespectsIntentShutdownCooldownAndCap()
    {
        var p = new RecoveryPolicy(TimeSpan.FromSeconds(5), 2);
        var code = FindingCode.OverlayTopmostLost;
        Assert.False(p.TryBegin(code, FindingLevel.Confirmed, overlayIntendedVisible: false, shuttingDown: false, TimeSpan.Zero));
        Assert.False(p.TryBegin(code, FindingLevel.Confirmed, overlayIntendedVisible: true, shuttingDown: true, TimeSpan.Zero));
        Assert.True(p.TryBegin(code, FindingLevel.Confirmed, true, false, TimeSpan.FromSeconds(0)));
        Assert.False(p.TryBegin(code, FindingLevel.Confirmed, true, false, TimeSpan.FromSeconds(1)));   // пауза
        Assert.True(p.TryBegin(code, FindingLevel.Confirmed, true, false, TimeSpan.FromSeconds(6)));
        Assert.False(p.TryBegin(code, FindingLevel.Confirmed, true, false, TimeSpan.FromSeconds(60)));  // лимит
        Assert.Equal(2, p.Attempts);
    }

    [Fact]
    public void Histogram_ReportsUpperBucketBounds()
    {
        var h = new LatencyHistogram();
        Assert.Null(h.PercentileUpperMicros(0.5));
        for (int i = 0; i < 90; i++)
        {
            h.RecordMicros(40);
        }

        for (int i = 0; i < 10; i++)
        {
            h.RecordMicros(3_000);
        }

        Assert.Equal(50, h.PercentileUpperMicros(0.5));
        Assert.Equal(5_000, h.PercentileUpperMicros(0.95));
        Assert.Equal(3_000, h.MaxMicros);
    }

    [Fact]
    public void Comparator_CountsMatchesAndOneSidedEdgesByClass()
    {
        var cmp = new EdgeComparator(TimeSpan.FromMilliseconds(200), 64);
        var t = TimeSpan.FromSeconds(1);
        var down = new InputEvent(1, 0, InputKind.Down, (ushort)A, 0, 0x41, InputFlags.None);
        cmp.Observe(SourceKind.RawInput, down, t);
        cmp.Observe(SourceKind.LowLevelHook, down, t + TimeSpan.FromMilliseconds(3));

        int fake = new PhysicalKey(ScanPrefix.Synthetic, 0x1D).Slot;
        cmp.Observe(SourceKind.LowLevelHook, new InputEvent(2, 0, InputKind.Down, (ushort)fake, 0, 0xA2, InputFlags.None), t);
        cmp.Flush(t + TimeSpan.FromSeconds(1));

        var stats = cmp.ToStats();
        Assert.Equal(1, stats.Classes.Single(c => c.Class == "Other").Matched);
        Assert.Equal(1, stats.Classes.Single(c => c.Class == "Synthetic").LowLevelOnly);
        Assert.Equal(0, stats.Classes.Single(c => c.Class == "Synthetic").RawOnly);
    }

    [Fact]
    public void ExportContainsOnlyAggregateFields_AndNoKeyIdentities()
    {
        // Распознаваемый ввод: «X», LShift, Numpad Enter, с повторами и потерей.
        var h = new Harness(ringCapacity: 4);
        h.Down(X); h.Down(X); h.Down(LShift); h.Down(NumEnter); h.Up(X); h.Up(LShift);
        h.Drain();
        h.Drain();
        var cmp = new EdgeComparator(TimeSpan.FromMilliseconds(200), 16);
        cmp.Observe(SourceKind.RawInput, new InputEvent(1, 0, InputKind.Down, (ushort)X, 0, 0x58, InputFlags.None), TimeSpan.Zero);
        cmp.Flush(TimeSpan.FromSeconds(5));

        var cb = new LatencyHistogram();
        cb.RecordMicros(12);
        var export = new DiagnosticsExport(
            SchemaVersion: 1,
            Mode: "compare",
            PrimarySource: "RawInput",
            UptimeSeconds: 12.5,
            Sources: [DiagnosticsBuilder.Source(h.Reader, "Running", null, cb, Stopwatch.GetTimestamp())],
            Reconcile: DiagnosticsBuilder.Reconcile(new ReconcileCounters()),
            ModelToUiApply: new LatencyHistogram().ToStats(LatencyDefinitions.ModelToUiApply),
            Comparator: cmp.ToStats(),
            Window: new WindowStats(true, true, true, false, true, 0, 0),
            SystemEvents: new SystemEventStats(0, 0, 0, 0, 0, 0),
            Findings: [new FindingStat(FindingCode.ResyncNotCompleted.ToString(), FindingLevel.Suspicion.ToString(), 1)],
            ShutdownNotes: ["RawInput: stopped"],
            Visual: new VisualStats("CombosOnly", "None", true,
                new LayoutIndicatorStats(true, true, "ShownInWindow", "ShownInWindow", 120, 1, 0, 4, 100, 2, 13, 3, 1, 1, 2, 1, 0, 3),
                new MouseCursorStats(true, true, 42)),
            Display: new DisplayPipelineStats("Ok", 1.5, 10, 9, 1, 0, 9, 9, 2, 0, new Dictionary<string, long> { ["WindowNotTopmost"] = 1 }, "Allowed/None", 1, 200, 40));

        using var doc = JsonDocument.Parse(export.ToJson());
        var names = new HashSet<string>();
        var strings = new List<string>();
        Walk(doc.RootElement, names, strings);

        var allowed = new HashSet<string>
        {
            "SchemaVersion", "Mode", "PrimarySource", "UptimeSeconds", "Sources", "Reconcile", "ModelToUiApply", "Comparator",
            "Window", "SystemEvents", "Findings", "ShutdownNotes",
            "Source", "Status", "StartError", "EventsTotal", "Repeats", "Ignored", "MouseMoves", "WheelNotches", "DevicesRemoved",
            "RingCapacity", "RingDepth", "DropEpoch", "SnapshotsCreated", "ResyncRequests", "SnapshotRerequests", "EventsApplied",
            "DuplicatesSkipped", "SnapshotsApplied", "SnapshotCorrections", "MarkedUnknown", "UnknownResolvedByEvent", "StaleNoRepeat",
            "ReconciledUp", "ReconciledDown", "DeviceRemovedReleases", "KeysDownNow", "KeysUnknownNow", "SecondsSinceLastEvent",
            "CallbackOrMessage", "CaptureToModel", "WheelEvents", "ModifierChords", "WinChords",
            "Definition", "Count", "P50UpperMicros", "P95UpperMicros", "P99UpperMicros", "MaxMicros",
            "Runs", "Untrusted", "NoPositiveControl", "AmbiguousVk", "ConfirmedDown",
            "WindowMs", "PendingOverflow", "Classes", "Class", "Matched", "RawOnly", "LowLevelOnly",
            "IntendedVisible", "Visible", "Topmost", "Minimized", "OnMonitor", "RecoveryAttempts", "RecoveryRefused",
            "SessionLock", "SessionUnlock", "DesktopSwitch", "Suspend", "Resume", "DisplayChange",
            "Code", "Level",
            "Visual", "KeyFilter", "Trail", "MouseInRow", "Layout", "MouseCursor",
            "Enabled", "Polling", "LastOutcome", "LastDecision", "Samples", "NoForeground", "Unavailable", "OwnWindow", "FocusThread",
            "ActiveThread", "OwnerThreadFallback", "ThreadsDiffer", "OwnerHklDiffers", "Baselines", "ShownInWindow", "ShownWindowSwitch",
            "SuppressedWindowSwitch", "Shown", "WindowVisible", "Moves",
            // Phase 4I: конвейер отображения — этап и счётчики (имена этапов — ключи словаря)
            "Display", "Stage", "StageSeconds", "KeyEvents", "Pending", "Hidden", "ViewsApplied", "Refreshes", "Visuals", "Recoveries",
            "Entered", "WindowNotTopmost", "Privacy", "OverlayWindows", "PrivateMegabytes", "Threads",
        };
        Assert.Empty(names.Except(allowed));

        // Ни одно строковое значение не совпадает с подписью или кодом нажатых клавиш.
        string[] forbidden = ["X", "LShift", "Num Enter", "2D", "E0 1C", "0x58", "88"];
        foreach (var s in strings)
        {
            Assert.DoesNotContain(s, forbidden);
        }
    }

    private static void Walk(JsonElement e, HashSet<string> names, List<string> strings)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    names.Add(p.Name);
                    Walk(p.Value, names, strings);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                {
                    Walk(item, names, strings);
                }

                break;
            case JsonValueKind.String:
                strings.Add(e.GetString()!);
                break;
        }
    }
}
