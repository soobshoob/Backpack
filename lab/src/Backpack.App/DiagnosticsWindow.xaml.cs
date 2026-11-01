using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Backpack.Core.Diagnostics;
using Backpack.Visual.Help;
using Backpack.Visual.Localization;
using Backpack.Visual.Settings;
using Microsoft.Win32;

namespace Backpack.App;

/// <summary>Действия главного окна, которые выполняет приложение (окна, трей, выход).</summary>
internal sealed record DiagnosticsActions(Action OpenSettings, Action HideToTray, Action Exit, Func<IEnumerable<string>> VisualLines);

public partial class DiagnosticsWindow : Window
{
    public const string HelpTopicName = "09-diagnostics-and-privacy.md";

    private readonly LabController _controller;
    private readonly DiagnosticsActions _actions;
    private readonly DispatcherTimer _refresh;
    private bool _checklistVisible = true;

    internal DiagnosticsWindow(LabController controller, DiagnosticsActions actions)
    {
        InitializeComponent();
        UiTheme.Attach(this);
        _controller = controller;
        _actions = actions;
        HelpService.Attach(this, HelpTopicName);
        _refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => RefreshStats(), Dispatcher);
        Loc.Changed += ApplyTexts;
        _controller.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
        Closed += (_, _) =>
        {
            _refresh.Stop();
            Loc.Changed -= ApplyTexts;
            _controller.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
        };
        ApplyTexts();
    }

    /// <summary>Тексты из ресурсов текущего языка; вызывается и при смене языка.</summary>
    private void ApplyTexts()
    {
        Title = Loc.T("Diag.WindowTitle");
        SettingsButton.Content = Loc.T("Diag.Settings");
        HelpButton.Content = Loc.T("Diag.Help");
        ExportButton.Content = Loc.T("Diag.Export");
        FreezeButton.Content = Loc.T("Diag.Freeze");
        FreezeButton.ToolTip = Loc.T("Diag.FreezeHint");
        ResetButton.Content = Loc.T("Diag.Reset");
        ResetButton.ToolTip = Loc.T("Diag.ResetHint");
        ToTrayButton.Content = Loc.T("Diag.ToTray");
        ToTrayButton.ToolTip = Loc.T("Settings.ToTrayHint");
        ExitButton.Content = Loc.T("Diag.Exit");
        ChecklistText.Text = Loc.T("Diag.Checklist");
        UpdateChecklist();
        OnOverlayVisibilityChanged(_controller.OverlayIntendedVisible);
        RefreshStats();
    }

    private void UpdateChecklist()
    {
        ChecklistText.Visibility = _checklistVisible ? Visibility.Visible : Visibility.Collapsed;
        ChecklistToggle.Content = (_checklistVisible ? "▾ " : "▸ ") + Loc.T("Diag.ChecklistTitle");
        ChecklistToggle.ToolTip = _checklistVisible ? Loc.T("Diag.ChecklistHide") : Loc.T("Diag.ChecklistShow");
    }

    private void OnOverlayVisibilityChanged(bool visible) =>
        ToggleOverlayButton.Content = visible ? Loc.T("Diag.HideOverlay") : Loc.T("Diag.ShowOverlay");

    private void UpdateModeText()
    {
        var o = _controller.Options;
        double linger = Math.Round(_controller.Display.Linger.TotalMilliseconds);
        ModeText.Text = o.LingerOverride is null
            ? Loc.F("Diag.ModeFromSettings", o.ModeName, o.Primary, linger)
            : Loc.F("Diag.ModeOverride", o.ModeName, o.Primary, linger);
    }

    public void RefreshStats()
    {
        if (!IsVisible && IsLoaded)
        {
            return; // скрыто в трей: без лишней работы
        }

        UpdateModeText();
        try
        {
            StatsText.Text = Format(_controller.BuildExport());
        }
        catch (Exception ex)
        {
            StatsText.Text = Loc.F("Diag.BuildError", ex.GetType().Name);
        }
    }

    private string Format(DiagnosticsExport x)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.F("Diag.Uptime", x.UptimeSeconds, x.Mode, x.PrimarySource));
        foreach (var note in _controller.StartNotes)
        {
            sb.AppendLine(Loc.F("Diag.StartNote", note));
        }

        sb.AppendLine();
        foreach (var s in x.Sources)
        {
            sb.AppendLine($"── {s.Source}  [{s.Status}]{(s.StartError is { } err ? $"  Win32 {err}" : "")}");
            sb.AppendLine(Loc.F("Diag.Src.Events", s.EventsTotal, s.Repeats, s.Ignored, s.MouseMoves, s.WheelNotches, s.DevicesRemoved));
            sb.AppendLine(Loc.F("Diag.Src.Ring", s.RingDepth, s.RingCapacity, s.DropEpoch, s.ResyncRequests, s.SnapshotRerequests, s.SnapshotsCreated, s.SnapshotsApplied, s.SnapshotCorrections));
            sb.AppendLine(Loc.F("Diag.Src.Applied", s.EventsApplied, s.DuplicatesSkipped, s.KeysDownNow, s.KeysUnknownNow));
            sb.AppendLine(Loc.F("Diag.Src.Unknown", s.MarkedUnknown, s.UnknownResolvedByEvent, s.StaleNoRepeat, s.ReconciledUp, s.ReconciledDown, s.DeviceRemovedReleases));
            sb.AppendLine(Loc.F("Diag.Src.SinceLast", s.SecondsSinceLastEvent is { } sec ? Loc.F("Diag.Seconds", sec) : "—"));
            AppendLatency(sb, s.CallbackOrMessage);
            AppendLatency(sb, s.CaptureToModel);
        }

        sb.AppendLine();
        AppendLatency(sb, x.ModelToUiApply);
        sb.AppendLine(Loc.T("Diag.NoPixel"));
        sb.AppendLine();
        var r = x.Reconcile;
        sb.AppendLine(Loc.F("Diag.Reconcile", r.Runs, r.Untrusted, r.NoPositiveControl, r.AmbiguousVk, r.ConfirmedDown));

        if (x.Comparator is { } c)
        {
            sb.AppendLine(Loc.F("Diag.Compare", c.WindowMs, c.PendingOverflow));
            foreach (var cl in c.Classes)
            {
                sb.AppendLine(Loc.F("Diag.CompareClass", cl.Class, cl.Matched, cl.RawOnly, cl.LowLevelOnly));
            }
        }

        var w = x.Window;
        sb.AppendLine();
        sb.AppendLine(Loc.F("Diag.Overlay", w.IntendedVisible, w.Visible, w.Topmost, w.Minimized, w.OnMonitor, w.RecoveryAttempts, w.RecoveryRefused));
        var e = x.SystemEvents;
        sb.AppendLine(Loc.F("Diag.System", e.SessionLock, e.SessionUnlock, e.DesktopSwitch, e.Suspend, e.Resume, e.DisplayChange));
        sb.AppendLine();
        foreach (var line in _actions.VisualLines())
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine(Loc.T("Diag.Findings"));
        if (x.Findings.Count == 0)
        {
            sb.AppendLine(Loc.T("Diag.FindingsNone"));
        }

        foreach (var f in x.Findings)
        {
            bool active = _controller.ActiveFindings.Any(a => a.ToString() == f.Code);
            sb.AppendLine(Loc.F(active ? "Diag.FindingActive" : "Diag.Finding", f.Level, f.Code, f.Count));
        }

        return sb.ToString();
    }

    private static void AppendLatency(StringBuilder sb, LatencyStats l)
    {
        static string F(long? us) => us switch
        {
            null => "—",
            long.MaxValue => Loc.T("Diag.Over1s"),
            { } v when v >= 1000 => Loc.F("Diag.UpToMs", v / 1000.0),
            { } v => Loc.F("Diag.UpToUs", v),
        };
        string def = l.Definition switch
        {
            LatencyDefinitions.LowLevelCallback => Loc.T("Diag.Def.LowLevelCallback"),
            LatencyDefinitions.RawInputMessage => Loc.T("Diag.Def.RawInputMessage"),
            LatencyDefinitions.CaptureToModel => Loc.T("Diag.Def.CaptureToModel"),
            LatencyDefinitions.ModelToUiApply => Loc.T("Diag.Def.ModelToUiApply"),
            _ => l.Definition,
        };
        sb.AppendLine("   " + def);
        sb.AppendLine(Loc.F("Diag.Latency", l.Count, F(l.P50UpperMicros), F(l.P95UpperMicros), F(l.P99UpperMicros), l.MaxMicros is { } m ? Loc.F("Diag.Us", m) : "—"));
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"backpack-lab-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Filter = "JSON|*.json",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, _controller.BuildExport().ToJson());
        }
    }

    private void OnToggleOverlay(object sender, RoutedEventArgs e) => _controller.SetOverlayVisible(!_controller.OverlayIntendedVisible);

    private void OnToggleChecklist(object sender, RoutedEventArgs e)
    {
        _checklistVisible = !_checklistVisible;
        UpdateChecklist();
    }

    private void OnReset(object sender, RoutedEventArgs e) => _controller.ManualReset();

    private void OnSettings(object sender, RoutedEventArgs e) => _actions.OpenSettings();

    private void OnHelp(object sender, RoutedEventArgs e) => HelpService.Show(HelpTopicName);

    private void OnToTray(object sender, RoutedEventArgs e) => _actions.HideToTray();

    private void OnExit(object sender, RoutedEventArgs e) => _actions.Exit();

    // X8: искусственная заморозка UI-потока стенда (не системного ввода). Потоки захвата не ждут UI,
    // поэтому события копятся в кольце; при переполнении — эпоха потерь и ресинхронизация по снимку.
    private void OnFreeze(object sender, RoutedEventArgs e) => Thread.Sleep(3000);
}
