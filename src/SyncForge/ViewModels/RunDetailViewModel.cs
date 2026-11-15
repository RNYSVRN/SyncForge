using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

public sealed record TargetEntryViewModel(long Id, string Path, string StatusGlyph, string StatusText, string SummaryText, bool CanOpen, RunStatus Status)
{
    public Microsoft.UI.Xaml.Style? StatusStyle => Format.StatusIconStyle(Status);

    public override string ToString() => $"{Path}, {StatusText}"; // Name für Screenreader
}

public sealed record FileItemViewModel(string Glyph, string Path, string Detail, bool IsError)
{
    public override string ToString() => $"{Path}, {Detail}";
}

public sealed record TargetFilterOption(string Name, long? TargetId)
{
    public override string ToString() => Name;
}

public sealed partial class RunDetailViewModel : ObservableObject
{
    private const int MaxFiles = 1000;
    private readonly HistoryStore _history;
    private readonly Core.Storage.ProfileStore _profiles;
    private long _runId;
    private bool _loading;

    public RunDetailViewModel(HistoryStore history, Core.Storage.ProfileStore profiles)
    {
        _history = history;
        _profiles = profiles;
    }

    public ObservableCollection<TargetEntryViewModel> Targets { get; } = [];
    public ObservableCollection<TargetFilterOption> TargetFilters { get; } = [];
    public ObservableCollection<FileItemViewModel> Files { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string Subtitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummaryText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusGlyph { get; set; } = string.Empty;
    [ObservableProperty] public partial Microsoft.UI.Xaml.Style? StatusStyle { get; set; }
    [ObservableProperty] public partial string FilesCountText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasNoFiles { get; set; }
    [ObservableProperty] public partial bool ShowTargetFilter { get; set; }
    [ObservableProperty] public partial bool NotFound { get; set; }
    [ObservableProperty] public partial TargetFilterOption? SelectedTarget { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial FileFilter Filter { get; set; } = FileFilter.All;

    public long RunId => _runId;

    /// <summary>Profil des Laufs (für "Konflikte lösen").</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>Lauf hatte offene Konflikte und das Profil ist (noch) ein Zwei-Wege-Profil mit "nur melden".</summary>
    [ObservableProperty] public partial bool CanResolveConflicts { get; set; }

    public void Load(long runId)
    {
        _runId = runId;
        var detail = _history.GetRun(runId);
        if (detail is null)
        {
            NotFound = true;
            Title = Localization.Get("Detail_NotFound");
            return;
        }

        var run = detail.Run;
        Title = run.ProfileName;
        StatusGlyph = Format.StatusGlyph(run.Status);
        StatusStyle = Format.StatusIconStyle(run.Status);
        Subtitle = $"{Format.DateTime(run.StartedAt)} · {Format.StatusName(run.Status)} · {Localization.Get("Trigger_" + run.Trigger)}";
        SummaryText = Format.Text("Run_TotalSummary", Format.Number(run.Copied), Format.Number(run.Updated), Format.Number(run.Deleted),
            Format.Bytes(run.Bytes), Format.Duration(run.Duration));
        if (run.Conflicts > 0) SummaryText += " " + Format.Text("Run_Conflicts", Format.Number(run.Conflicts)) + ".";
        ProfileId = run.ProfileId;
        CanResolveConflicts = run.Conflicts > 0 && _profiles.Get(run.ProfileId) is { Mode: Core.Models.SyncMode.TwoWay, ConflictResolution: Core.Models.ConflictResolution.Ask };

        _loading = true;
        Targets.Clear();
        TargetFilters.Clear();
        TargetFilters.Add(new TargetFilterOption(Localization.Get("Detail_AllTargets"), null));
        foreach (var t in detail.Targets)
        {
            var (glyph, status) = t.Status switch
            {
                TargetRunStatus.Success => ("", Localization.Get("Run_TargetDone")),
                TargetRunStatus.Error => ("", Format.Text(t.Errors == 1 ? "Run_TargetDoneOneError" : "Run_TargetDoneErrors", Format.Number(t.Errors))),
                TargetRunStatus.Cancelled => ("", Localization.Get("Status_Cancelled")),
                _ => ("", t.Message is { Length: > 0 } reason && !reason.Contains(' ')
                    ? Localization.Get("Validation_" + reason)   // z. B. DeletionProtection, SourceMissing
                    : t.Message ?? Localization.Get("Run_TargetUnavailable"))
            };
            var summary = t.Status == TargetRunStatus.Skipped
                ? string.Empty
                : Format.Text("Run_ResultSummary", Format.Number(t.Copied), Format.Number(t.Updated), Format.Number(t.Deleted),
                    Format.Bytes(t.Bytes), Format.Duration(t.Duration));
            var targetStatus = t.Status switch
            {
                TargetRunStatus.Success => RunStatus.Success,
                TargetRunStatus.Error => RunStatus.Error,
                TargetRunStatus.Skipped => RunStatus.Warning,
                _ => RunStatus.Cancelled
            };
            Targets.Add(new TargetEntryViewModel(t.Id, t.TargetPath, glyph, status, summary, Directory.Exists(t.TargetPath), targetStatus));
            if (t.Status != TargetRunStatus.Skipped) TargetFilters.Add(new TargetFilterOption(t.TargetPath, t.Id));
        }
        ShowTargetFilter = TargetFilters.Count > 2;
        SelectedTarget = TargetFilters[0];
        _loading = false;
        RefreshFiles();
    }

    partial void OnSelectedTargetChanged(TargetFilterOption? value) { if (!_loading) RefreshFiles(); }
    partial void OnSearchTextChanged(string value) { if (!_loading) RefreshFiles(); }
    partial void OnFilterChanged(FileFilter value) { if (!_loading) RefreshFiles(); }

    public void RefreshFiles()
    {
        if (NotFound) return;
        var (items, total) = _history.GetFiles(_runId, SelectedTarget?.TargetId, Filter, SearchText, MaxFiles);
        Files.Clear();
        foreach (var f in items) Files.Add(ToItem(f));
        HasNoFiles = total == 0;
        CanExport = total > 0;
        FilesCountText = total > items.Count
            ? Format.Text("Detail_FilesCountLimited", Format.Number(total), Format.Number(items.Count))
            : Format.Text(total == 1 ? "Detail_FilesCountOne" : "Detail_FilesCount", Format.Number(total));
    }

    public void Delete() => _history.DeleteRun(_runId);

    /// <summary>Es gibt (mit aktuellem Ziel, Filter und Suche) Dateien zum Exportieren.</summary>
    [ObservableProperty] public partial bool CanExport { get; set; }

    /// <summary>Dateiliste mit aktuellem Ziel, Filter und Suche als CSV speichern, ohne die Grenze der Anzeige.</summary>
    public Task ExportAsync(Microsoft.UI.Xaml.XamlRoot xamlRoot)
    {
        if (_history.GetRun(_runId) is not { } detail) return Task.CompletedTask;
        var (targetId, filter, search) = (SelectedTarget?.TargetId, Filter, SearchText);
        var name = Services.HistoryExport.FileName("Csv_FileNameFiles", detail.Run.ProfileName, Services.HistoryExport.FileDate(detail.Run.StartedAt));
        return Services.HistoryExport.ExportAsync(xamlRoot, name,
            csv => Services.HistoryExport.WriteFiles(csv, detail, _history.GetFiles(_runId, targetId, filter, search, int.MaxValue).Items));
    }

    private static FileItemViewModel ToItem(RunFileEntry f)
    {
        if (f.Error is not null) return new FileItemViewModel("\uEA39", f.Path, f.Error, true);
        var side = f.Side == SyncSide.Source ? " · " + Localization.Get("Action_InSource") : string.Empty;
        return f.Kind switch
        {
            SyncActionKind.CopyNew => new("\uE710", f.Path, Localization.Get("Action_CopyNew") + " · " + Format.Bytes(f.Size) + side, false),
            SyncActionKind.Update => new("\uE72C", f.Path, Localization.Get("Action_Update") + " · " + Format.Bytes(f.Size) + side, false),
            SyncActionKind.DeleteFile => new("\uE74D", f.Path, Localization.Get("Action_Deleted") + side, false),
            SyncActionKind.DeleteDirectory => new("\uE74D", f.Path + "\\", Localization.Get("Action_DirectoryDeleted") + side, false),
            SyncActionKind.RenameConflict => new("\uE7BA", f.Path, Localization.Get("Action_ConflictKeptBoth"), false),
            SyncActionKind.Conflict => new("\uE7BA", f.Path, Localization.Get("Action_ConflictOpen"), false),
            _ => new("\uE8F4", f.Path + "\\", Localization.Get("Action_DirectoryCreated") + side, false)
        };
    }
}
