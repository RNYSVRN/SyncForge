using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

/// <summary>Ein offener Konflikt mit beiden Fassungen und der Wahl des Benutzers.</summary>
public sealed partial class ConflictItemViewModel : ObservableObject
{
    public required ConflictInfo Info { get; init; }
    public string Path => Info.RelativePath;
    public bool CanResolve => Info.CanResolve;
    public bool IsTypeConflict => Info.IsTypeConflict;
    public string SourceText => Describe("Conflict_SourceVersion", Info.Source, Info.Newer == SyncSide.Source);
    public string TargetText => Describe("Conflict_TargetVersion", Info.Target, Info.Newer == SyncSide.Target);

    /// <summary>0 = später entscheiden, 1 = Quelle, 2 = Ziel, 3 = beide behalten (Reihenfolge wie <see cref="ConflictChoice"/>).</summary>
    [ObservableProperty] public partial int ChoiceIndex { get; set; }

    public ConflictChoice Choice => (ConflictChoice)Math.Max(0, ChoiceIndex);

    private static string Describe(string key, ConflictVersion? v, bool newer)
    {
        var text = v is null ? Localization.Get("Conflict_Missing")
            : v.IsDirectory ? Localization.Get("Conflict_Folder")
            : $"{Format.Bytes(v.Size)} · {Format.DateTime(new DateTimeOffset(v.LastWriteUtc, TimeSpan.Zero).ToLocalTime())}";
        if (newer) text += " · " + Localization.Get("Conflict_Newer");
        return Format.Text(key, text);
    }

    public override string ToString() => $"{Path}, {SourceText}, {TargetText}"; // Name für Screenreader
}

/// <summary>Konflikte eines Ziels.</summary>
public sealed class ConflictGroupViewModel
{
    public required string TargetPath { get; init; }
    public required SyncPlan Plan { get; init; }
    public ObservableCollection<ConflictItemViewModel> Items { get; } = [];
}

/// <summary>"Konflikte lösen": offene Zwei-Wege-Konflikte eines Profils anzeigen und entscheiden.</summary>
public sealed partial class ConflictsViewModel : ObservableObject
{
    private readonly ProfileStore _profiles;
    private readonly SyncCoordinator _coordinator;
    private SyncProfile? _profile;

    public ConflictsViewModel(ProfileStore profiles, SyncCoordinator coordinator)
    {
        _profiles = profiles;
        _coordinator = coordinator;
    }

    public ObservableCollection<ConflictGroupViewModel> Groups { get; } = [];

    [ObservableProperty] public partial string Subtitle { get; set; } = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsIdle), nameof(HasConflicts), nameof(ShowEmpty))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string BusyText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ShowMessage { get; set; }
    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;
    [ObservableProperty] public partial Microsoft.UI.Xaml.Controls.InfoBarSeverity MessageSeverity { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasConflicts), nameof(ShowEmpty))]
    public partial int ConflictCount { get; set; }

    public bool IsIdle => !IsBusy;
    public bool HasConflicts => !IsBusy && ConflictCount > 0;
    public bool ShowEmpty => !IsBusy && ConflictCount == 0;

    public async Task LoadAsync(Guid profileId)
    {
        _profile = _profiles.Get(profileId);
        if (_profile is null) return;
        Subtitle = _profile.Name;
        await AnalyzeAsync();
    }

    private async Task AnalyzeAsync()
    {
        if (_profile is null) return;
        Groups.Clear();
        ConflictCount = 0;
        using var cts = new CancellationTokenSource();
        if (!_coordinator.TryBegin(_profile.Id, cts))
        {
            Show(Localization.Get("Conflicts_Busy"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
            return;
        }
        IsBusy = true;
        BusyText = Localization.Get("Conflicts_Analyzing");
        try
        {
            var analysis = await SyncCoordinator.AnalyzeAsync(_profile, null, cts.Token);
            foreach (var target in analysis.ReadyTargets)
            {
                var conflicts = ConflictResolver.List(target.Plan!);
                if (conflicts.Count == 0) continue;
                var group = new ConflictGroupViewModel { TargetPath = target.ResolvedPath ?? target.Target.Path, Plan = target.Plan! };
                foreach (var c in conflicts) group.Items.Add(new ConflictItemViewModel { Info = c });
                Groups.Add(group);
            }
            var unavailable = analysis.Targets.Count(t => t.Status != TargetStatus.Ready);
            if (analysis.SourceMissing) Show(Localization.Get("Validation_SourceMissing") + " " + _profile.SourcePath, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
            else if (unavailable > 0) Show(Format.Text("Conflicts_TargetsUnavailable", Format.Number(unavailable)), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Show(ex.Message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
        }
        finally
        {
            _coordinator.End(_profile.Id);
            ConflictCount = Groups.Sum(g => g.Items.Count);
            IsBusy = false;
        }
    }

    /// <summary>Gleiche Entscheidung für alle lösbaren Konflikte; null = jeweils die neuere Fassung (bei Gleichstand die Quelle).</summary>
    public void SetAll(ConflictChoice? choice)
    {
        foreach (var item in Groups.SelectMany(g => g.Items).Where(i => i.CanResolve))
        {
            item.ChoiceIndex = (int)(choice ?? (item.Info.Newer == SyncSide.Target ? ConflictChoice.KeepTarget : ConflictChoice.KeepSource));
        }
    }

    public async Task ApplyAsync()
    {
        if (_profile is null || IsBusy) return;
        var chosen = Groups.SelectMany(g => g.Items).Count(i => i.CanResolve && i.Choice != ConflictChoice.Skip);
        if (chosen == 0)
        {
            Show(Localization.Get("Conflicts_NothingChosen"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational);
            return;
        }

        using var cts = new CancellationTokenSource();
        if (!_coordinator.TryBegin(_profile.Id, cts))
        {
            Show(Localization.Get("Conflicts_Busy"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
            return;
        }
        IsBusy = true;
        BusyText = Localization.Get("Conflicts_Applying");
        var run = new ProfileRunResult { Profile = _profile, StartedAt = DateTimeOffset.Now };
        try
        {
            var executor = new SyncExecutor(_coordinator.BuildOptions(_profile));
            foreach (var group in Groups)
            {
                var plan = ConflictResolver.BuildPlan(group.Plan, group.Items.Select(i => (i.Info, i.Choice)), SyncCoordinator.ConflictLabel);
                if (plan.Actions.Count == 0) continue;
                var result = await executor.ExecuteAsync(plan, null, cts.Token);
                await Task.Run(() => TwoWayPlanner.UpdateState(plan));
                run.Results.Add(result);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            App.LogError(ex);
        }
        finally
        {
            run.Duration = DateTimeOffset.Now - run.StartedAt;
            _coordinator.End(_profile.Id);
            IsBusy = false;
        }

        if (run.Results.Count > 0) _coordinator.Record(run, RunTrigger.Manual);
        var errors = run.Results.Sum(r => r.Errors.Count);
        await AnalyzeAsync();
        Show(errors > 0 ? Format.Text("Conflicts_DoneErrors", Format.Number(errors)) : Format.Text("Conflicts_Done", Format.Number(chosen)),
            errors > 0 ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
    }

    private void Show(string text, Microsoft.UI.Xaml.Controls.InfoBarSeverity severity)
    {
        MessageText = text;
        MessageSeverity = severity;
        ShowMessage = true;
    }
}
