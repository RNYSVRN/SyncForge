using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

public sealed record SyncRunRequest(Guid ProfileId, bool PreviewOnly);

public sealed record ActionItemViewModel(string Glyph, string Path, string Detail)
{
    public override string ToString() => $"{Path}, {Detail}"; // Name für Screenreader
}

public sealed partial class TargetRunViewModel : ObservableObject
{
    public required string TargetPath { get; init; }
    public string? PlanRoot { get; init; }

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusGlyph { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    public partial string SummaryText { get; set; } = string.Empty;
    public bool HasSummary => !string.IsNullOrEmpty(SummaryText);
    [ObservableProperty] public partial double ProgressPercent { get; set; }
    [ObservableProperty] public partial bool IsProgressIndeterminate { get; set; }
    [ObservableProperty] public partial bool ShowProgress { get; set; }
    [ObservableProperty] public partial string ProgressText { get; set; } = string.Empty;
    [ObservableProperty] public partial string MoreActionsText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasActions { get; set; }
    [ObservableProperty] public partial bool HasErrors { get; set; }

    public ObservableCollection<ActionItemViewModel> Actions { get; } = [];
    public ObservableCollection<ActionItemViewModel> Errors { get; } = [];
}

public enum RunState
{
    Analyzing,
    Ready,
    Running,
    Done
}

/// <summary>Steuert Vorschau (nur Analyse) und Lauf (Analyse + Ausführung) eines Profils.</summary>
public sealed partial class SyncRunViewModel : ObservableObject
{
    private const int MaxListedActions = 500;

    private readonly ProfileStore _store;
    private readonly SettingsService _settings;
    private ProfileAnalysis? _analysis;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, TargetRunViewModel> _byRoot = new(StringComparer.OrdinalIgnoreCase);

    private readonly SyncCoordinator _coordinator;

    public SyncRunViewModel(ProfileStore store, SettingsService settings, SyncCoordinator coordinator)
    {
        _store = store;
        _settings = settings;
        _coordinator = coordinator;
    }

    /// <summary>Id des gespeicherten Historien-Eintrags (für "Details anzeigen").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistoryEntry))]
    public partial long? HistoryRunId { get; set; }
    public bool HasHistoryEntry => HistoryRunId is not null && State == RunState.Done;

    public ObservableCollection<TargetRunViewModel> Targets { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string Subtitle { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalyzing), nameof(IsReady), nameof(IsRunning), nameof(IsDone), nameof(CanStart), nameof(IsBusy), nameof(IsIdle), nameof(HasHistoryEntry))]
    public partial RunState State { get; set; }

    [ObservableProperty] public partial bool ShowDeletionWarning { get; set; }
    [ObservableProperty] public partial string DeletionWarningText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ShowMessage { get; set; }
    [ObservableProperty] public partial string MessageTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;
    [ObservableProperty] public partial InfoBarSeverity MessageSeverity { get; set; }
    [ObservableProperty] public partial string AnalyzeText { get; set; } = string.Empty;

    public bool IsAnalyzing => State == RunState.Analyzing;
    public bool IsReady => State == RunState.Ready;
    public bool IsRunning => State == RunState.Running;
    public bool IsDone => State == RunState.Done;
    public bool IsBusy => State is RunState.Analyzing or RunState.Running;
    public bool IsIdle => !IsBusy;
    public bool CanStart => State == RunState.Ready && _analysis is not null && _analysis.ReadyTargets.Any(t => t.Plan!.HasChanges);

    public async Task StartAsync(SyncRunRequest request)
    {
        var profile = _store.Get(request.ProfileId);
        if (profile is null)
        {
            Fail(Localization.Get("Run_ProfileMissing"));
            return;
        }

        Title = profile.Name;
        Subtitle = profile.SourcePath;
        State = RunState.Analyzing;
        AnalyzeText = Localization.Get("Run_Analyzing");
        _cts = new CancellationTokenSource();

        var progress = new Progress<SyncProgress>(p =>
        {
            AnalyzeText = p.Phase == SyncPhase.Comparing
                ? Format.Text("Run_Comparing", Format.Number(p.ItemsDone), Format.Number(p.ItemsTotal))
                : Format.Text("Run_Scanning", Format.Number(p.ItemsDone));
        });

        try
        {
            _analysis = await ProfileRunner.AnalyzeAsync(profile, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            State = RunState.Done;
            SetMessage(InfoBarSeverity.Informational, Localization.Get("Status_Cancelled"), string.Empty);
            return;
        }

        if (_analysis.SourceMissing)
        {
            Fail(Localization.Get("Validation_SourceMissing") + " " + profile.SourcePath);
            return;
        }

        BuildTargetList(_analysis);
        State = RunState.Ready;

        var warnings = _analysis.DeletionWarnings.ToList();
        if (warnings.Count > 0)
        {
            ShowDeletionWarning = true;
            DeletionWarningText = string.Join(Environment.NewLine, warnings.Select(w =>
                Format.Text("Run_DeletionWarning", Format.Number(w.Plan!.DeleteFileCount), w.Plan.DeletePercent.ToString("0", Format.Culture), w.Target.Path)));
        }

        if (!_analysis.ReadyTargets.Any())
        {
            SetMessage(InfoBarSeverity.Error, Localization.Get("Run_NoTargetReady"), string.Empty);
            State = RunState.Done;
            return;
        }

        if (!CanStart)
        {
            SetMessage(InfoBarSeverity.Success, Localization.Get("Run_UpToDate"), string.Empty);
            if (!request.PreviewOnly && _coordinator.TryBegin(profile.Id, _cts))
            {
                // Auch ein Lauf ohne Änderungen gehört in die Historie (und prüft dabei die Ziele).
                try
                {
                    var run = await ProfileRunner.ExecuteAsync(_analysis, _coordinator.BuildOptions(profile), null, CancellationToken.None);
                    HistoryRunId = _coordinator.Record(run, RunTrigger.Manual).HistoryId;
                }
                finally
                {
                    _coordinator.End(profile.Id);
                }
            }
            State = RunState.Done;
            return;
        }

        // Direkt starten, außer bei Vorschau oder wenn der Löschschutz anschlägt.
        if (!request.PreviewOnly && warnings.Count == 0) await ExecuteAsync();
    }

    public async Task ExecuteAsync()
    {
        if (_analysis is null || !CanStart) return;
        _cts = new CancellationTokenSource();
        var profileId = _analysis.Profile.Id;
        if (!_coordinator.TryBegin(profileId, _cts))
        {
            SetMessage(InfoBarSeverity.Informational, Localization.Get("Run_AlreadyRunning"), string.Empty);
            return;
        }
        ShowDeletionWarning = false;
        ShowMessage = false;
        State = RunState.Running;

        foreach (var t in Targets.Where(t => t.PlanRoot is not null))
        {
            t.ShowProgress = true;
            t.IsProgressIndeterminate = false;
            t.ProgressPercent = 0;
            t.StatusText = Localization.Get("Run_Waiting");
        }

        var progress = new Progress<SyncProgress>(OnProgress);
        try
        {
            var run = await ProfileRunner.ExecuteAsync(_analysis, _coordinator.BuildOptions(_analysis.Profile), progress, _cts.Token);
            ShowResults(run);
            HistoryRunId = _coordinator.Record(run, RunTrigger.Manual).HistoryId;
        }
        finally
        {
            _coordinator.End(profileId);
        }
        State = RunState.Done;
    }

    public void Cancel() => _cts?.Cancel();

    private void OnProgress(SyncProgress p)
    {
        // Progress<T> stellt Meldungen asynchron zu: nach Laufende eintreffende verwerfen,
        // sonst überschreiben sie das Ergebnis.
        if (State != RunState.Running || !_byRoot.TryGetValue(p.TargetRoot, out var t)) return;
        t.ProgressPercent = p.Percent;
        t.StatusText = p.Phase switch
        {
            SyncPhase.Deleting => Localization.Get("Run_Deleting"),
            SyncPhase.Finished => Localization.Get("Run_Finishing"),
            _ => Localization.Get("Run_Copying")
        };
        t.ProgressText = Format.Text("Run_ProgressDetail",
            Format.Number(p.ItemsDone), Format.Number(p.ItemsTotal), Format.Bytes(p.BytesDone), Format.Bytes(p.BytesTotal),
            p.CurrentFile ?? string.Empty);
    }

    private void BuildTargetList(ProfileAnalysis analysis)
    {
        Targets.Clear();
        _byRoot.Clear();
        foreach (var ta in analysis.Targets)
        {
            var vm = new TargetRunViewModel { TargetPath = ta.Target.Path, PlanRoot = ta.Plan?.TargetRoot };
            switch (ta.Status)
            {
                case TargetStatus.Unavailable:
                    vm.StatusGlyph = "";
                    vm.StatusText = Localization.Get("Run_TargetUnavailable");
                    break;
                case TargetStatus.Error:
                    vm.StatusGlyph = "";
                    vm.StatusText = ta.ValidationError is { } v ? Localization.Get("Validation_" + v) : ta.ErrorMessage ?? string.Empty;
                    break;
                default:
                    var plan = ta.Plan!;
                    vm.StatusGlyph = plan.HasChanges ? "" : "";
                    vm.StatusText = plan.HasChanges ? Localization.Get("Run_TargetReady") : Localization.Get("Run_UpToDate");
                    vm.SummaryText = Format.Text("Run_PlanSummary",
                        Format.Number(plan.CopyNewCount), Format.Number(plan.UpdateCount), Format.Number(plan.DeleteFileCount),
                        Format.Number(plan.UnchangedFileCount), Format.Bytes(plan.BytesToCopy));
                    foreach (var a in plan.Actions.Take(MaxListedActions)) vm.Actions.Add(ToItem(a));
                    vm.HasActions = vm.Actions.Count > 0;
                    if (plan.Actions.Count > MaxListedActions)
                        vm.MoreActionsText = Format.Text("Run_MoreActions", Format.Number(plan.Actions.Count - MaxListedActions));
                    _byRoot[plan.TargetRoot] = vm;
                    break;
            }
            Targets.Add(vm);
        }
    }

    private void ShowResults(ProfileRunResult run)
    {
        int copied = 0, updated = 0, deleted = 0, errors = 0;
        long bytes = 0;
        foreach (var r in run.Results)
        {
            copied += r.Copied; updated += r.Updated; deleted += r.DeletedFiles; errors += r.Errors.Count; bytes += r.BytesCopied;
            if (!_byRoot.TryGetValue(r.TargetRoot, out var t)) continue;
            t.ShowProgress = false;
            t.ProgressPercent = 100;
            t.StatusGlyph = r.Cancelled ? "" : r.Errors.Count > 0 ? "" : "";
            t.StatusText = r.Cancelled ? Localization.Get("Status_Cancelled")
                : r.Errors.Count > 0 ? Format.Text(r.Errors.Count == 1 ? "Run_TargetDoneOneError" : "Run_TargetDoneErrors", Format.Number(r.Errors.Count))
                : Localization.Get("Run_TargetDone");
            t.SummaryText = Format.Text("Run_ResultSummary", Format.Number(r.Copied), Format.Number(r.Updated),
                Format.Number(r.DeletedFiles), Format.Bytes(r.BytesCopied), Format.Duration(r.Duration));
            t.Errors.Clear();
            foreach (var e in r.Errors.Take(MaxListedActions)) t.Errors.Add(new ActionItemViewModel("", e.RelativePath, e.Message));
            t.HasErrors = t.Errors.Count > 0;
        }

        var severity = run.Status switch
        {
            RunStatus.Success => InfoBarSeverity.Success,
            RunStatus.Warning => InfoBarSeverity.Warning,
            RunStatus.Cancelled => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Error
        };
        SetMessage(severity, Format.StatusName(run.Status), SyncCoordinator.BuildSummary(run));
    }

    private void Fail(string message)
    {
        State = RunState.Done;
        SetMessage(InfoBarSeverity.Error, Localization.Get("Status_Error"), message);
    }

    private void SetMessage(InfoBarSeverity severity, string title, string text)
    {
        MessageSeverity = severity;
        MessageTitle = title;
        MessageText = text;
        ShowMessage = true;
    }

    private static ActionItemViewModel ToItem(SyncAction a) => a.Kind switch
    {
        SyncActionKind.CopyNew => new("", a.RelativePath, Localization.Get("Action_CopyNew") + " · " + Format.Bytes(a.Size)),
        SyncActionKind.Update => new("", a.RelativePath, Localization.Get("Action_Update") + " · " + Format.Bytes(a.Size)),
        SyncActionKind.DeleteFile => new("", a.RelativePath, Localization.Get("Action_DeleteFile")),
        SyncActionKind.DeleteDirectory => new("", a.RelativePath + "\\", Localization.Get("Action_DeleteDirectory")),
        _ => new("", a.RelativePath + "\\", Localization.Get("Action_CreateDirectory"))
    };
}
