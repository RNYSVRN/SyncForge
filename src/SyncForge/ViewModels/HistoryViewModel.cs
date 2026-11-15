using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

public sealed record FilterOption(string Name, Guid? ProfileId = null, RunStatus? Status = null)
{
    public override string ToString() => Name;
}

/// <summary>Ein Lauf in der Historienliste.</summary>
public sealed class RunItemViewModel(RunSummary run)
{
    public long Id => run.Id;
    public string ProfileName => run.ProfileName;
    public string DateText => Format.DateTime(run.StartedAt);
    public string StatusGlyph => Format.StatusGlyph(run.Status);
    public Microsoft.UI.Xaml.Style? StatusStyle => Format.StatusIconStyle(run.Status);
    public string StatusText => Format.StatusName(run.Status);
    public string DurationText => Format.Duration(run.Duration);
    public string BytesText => Format.Bytes(run.Bytes);

    public override string ToString() => $"{ProfileName}, {StatusText}, {DateText}"; // Name für Screenreader

    public string SummaryText
    {
        get
        {
            var text = Format.Text("History_RunSummary", Format.Number(run.Copied), Format.Number(run.Updated), Format.Number(run.Deleted));
            if (run.Errors > 0) text += " · " + Format.Text("History_RunErrors", Format.Number(run.Errors));
            if (run.TargetsSkipped > 0) text += " · " + Format.Text("Run_SkippedTargets", Format.Number(run.TargetsSkipped));
            return text;
        }
    }
}

public sealed partial class HistoryViewModel : ObservableObject
{
    public const int StatsDays = 30;
    private const int PageSize = 100;

    private readonly HistoryStore _history;
    private bool _loading;

    public HistoryViewModel(HistoryStore history)
    {
        _history = history;
        StatusFilters =
        [
            new FilterOption(Localization.Get("History_AllStatus")),
            new FilterOption(Format.StatusName(RunStatus.Success), Status: RunStatus.Success),
            new FilterOption(Format.StatusName(RunStatus.Warning), Status: RunStatus.Warning),
            new FilterOption(Format.StatusName(RunStatus.Error), Status: RunStatus.Error),
            new FilterOption(Format.StatusName(RunStatus.Cancelled), Status: RunStatus.Cancelled)
        ];
    }

    public ObservableCollection<FilterOption> ProfileFilters { get; } = [];
    public IReadOnlyList<FilterOption> StatusFilters { get; }
    public ObservableCollection<RunItemViewModel> Runs { get; } = [];

    [ObservableProperty] public partial FilterOption? SelectedProfile { get; set; }
    [ObservableProperty] public partial FilterOption? SelectedStatus { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial bool HasRuns { get; set; }
    [ObservableProperty] public partial bool CanLoadMore { get; set; }

    [ObservableProperty] public partial string RunsTileText { get; set; } = "0";
    [ObservableProperty] public partial string SuccessTileText { get; set; } = "–";
    [ObservableProperty] public partial string BytesTileText { get; set; } = "0 B";
    [ObservableProperty] public partial string DurationTileText { get; set; } = "–";

    /// <summary>Tageswerte für das Diagramm (wird von der Seite gezeichnet).</summary>
    public IReadOnlyList<DailyStat> DailyStats { get; private set; } = [];
    public event EventHandler? StatsChanged;

    public void Load()
    {
        _loading = true;
        var previous = SelectedProfile?.ProfileId;
        ProfileFilters.Clear();
        ProfileFilters.Add(new FilterOption(Localization.Get("History_AllProfiles")));
        foreach (var p in _history.GetProfiles()) ProfileFilters.Add(new FilterOption(p.Name, p.Id));
        SelectedProfile = ProfileFilters.FirstOrDefault(f => f.ProfileId == previous) ?? ProfileFilters[0];
        SelectedStatus ??= StatusFilters[0];
        _loading = false;
        Refresh();
    }

    partial void OnSelectedProfileChanged(FilterOption? value) { if (!_loading) Refresh(); }
    partial void OnSelectedStatusChanged(FilterOption? value) { if (!_loading) Refresh(); }

    public void Refresh()
    {
        var profileId = SelectedProfile?.ProfileId;

        var overview = _history.GetOverview(StatsDays, profileId);
        RunsTileText = Format.Number(overview.Runs);
        SuccessTileText = overview.Runs == 0 ? "–" : overview.SuccessRate.ToString("0", Format.Culture) + " %";
        BytesTileText = Format.Bytes(overview.Bytes);
        DurationTileText = overview.Runs == 0 ? "–" : Format.Duration(overview.AverageDuration);
        DailyStats = _history.GetDailyStats(StatsDays, profileId);
        StatsChanged?.Invoke(this, EventArgs.Empty);

        Runs.Clear();
        LoadPage();
        IsEmpty = Runs.Count == 0 && profileId is null && SelectedStatus?.Status is null;
        HasRuns = !IsEmpty;
    }

    public void LoadMore() => LoadPage();

    /// <summary>Alle Läufe, die zum aktuellen Filter passen (nicht nur die geladene Seite), als CSV speichern.</summary>
    public Task ExportAsync(Microsoft.UI.Xaml.XamlRoot xamlRoot)
    {
        var query = new RunQuery { ProfileId = SelectedProfile?.ProfileId, Status = SelectedStatus?.Status, Limit = int.MaxValue };
        return Services.HistoryExport.ExportAsync(xamlRoot, Services.HistoryExport.FileName("Csv_FileNameRuns", DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            csv => Services.HistoryExport.WriteRuns(csv, _history.GetRuns(query)));
    }

    private void LoadPage()
    {
        var page = _history.GetRuns(new RunQuery
        {
            ProfileId = SelectedProfile?.ProfileId,
            Status = SelectedStatus?.Status,
            Limit = PageSize + 1,
            Offset = Runs.Count
        });
        foreach (var r in page.Take(PageSize)) Runs.Add(new RunItemViewModel(r));
        CanLoadMore = page.Count > PageSize;
    }
}
