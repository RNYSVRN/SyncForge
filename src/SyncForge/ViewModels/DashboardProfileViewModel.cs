using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SyncForge.Core.Models;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

/// <summary>Profilkarte in der Übersicht: Status auf einen Blick, nächster Lauf, laufender Fortschritt.</summary>
public sealed partial class DashboardProfileViewModel(SyncProfile profile) : ObservableObject
{
    public Guid Id { get; } = profile.Id;
    public string Name { get; private set; } = profile.Name;

    [ObservableProperty] public partial string Route { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusGlyph { get; set; } = string.Empty;
    [ObservableProperty] public partial Style? StatusStyle { get; set; }
    [ObservableProperty] public partial string LastRunLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string NextRunLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string ModeText { get; set; } = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsRunning { get; set; }
    public bool IsIdle => !IsRunning;
    [ObservableProperty] public partial double Percent { get; set; }
    [ObservableProperty] public partial bool IsIndeterminate { get; set; }
    [ObservableProperty] public partial string ActivityText { get; set; } = string.Empty;

    /// <summary>Letzter Lauf mit Fehlern oder Warnungen.</summary>
    public bool NeedsAttention { get; private set; }

    public void Update(SyncProfile p)
    {
        Name = p.Name;
        OnPropertyChanged(nameof(Name));
        var targets = p.Targets.Count(t => t.Enabled);
        var sources = p.Sources.Count > 1 ? Format.Text("Profiles_SourcesMany", p.Sources.Count) : p.SourcePath;
        Route = Format.Text(targets == 1 ? "Profiles_RouteOne" : "Profiles_RouteMany", sources, targets);
        ModeText = Format.ModeName(p.Mode);

        var ran = p.LastRunAt is not null;
        StatusGlyph = ran ? Format.StatusGlyph(p.LastRunStatus) : ""; // Stoppuhr: noch nie gelaufen
        StatusStyle = Format.StatusIconStyle(ran ? p.LastRunStatus : RunStatus.None);
        NeedsAttention = ran && p.LastRunStatus is RunStatus.Warning or RunStatus.Error;
        LastRunLabel = p.LastRunAt is { } at
            ? Format.Text("Profiles_LastRun", Format.DateTime(at), Format.StatusName(p.LastRunStatus))
            : Localization.Get("Profiles_NeverRun");

        var next = AutomationService.NextRunFor(p);
        var events = new List<string>();
        if (p.Automation.WatchForChanges) events.Add(Localization.Get("Dashboard_OnChanges"));
        if (p.Automation.RunWhenTargetConnected) events.Add(Localization.Get("Dashboard_OnDrive"));
        NextRunLabel = next is { } n ? Format.Text("Dashboard_NextRun", Format.DateTime(n))
            : events.Count > 0 ? string.Join(" · ", events)
            : Localization.Get("Dashboard_ManualOnly");
    }

    /// <summary>Fortschritt eines laufenden Laufs (null = läuft gerade nicht).</summary>
    public void Update(RunActivity? activity)
    {
        IsRunning = activity is not null;
        if (activity is null) return;
        if (activity.IsCancelling)
        {
            IsIndeterminate = true;
            ActivityText = Localization.Get("Activity_Cancelling");
        }
        else if (activity.IsAnalyzing)
        {
            IsIndeterminate = true;
            ActivityText = activity.ScannedItems > 0
                ? Format.Text("Activity_Scanning", Format.Number(activity.ScannedItems))
                : Localization.Get("Activity_Analyzing");
        }
        else
        {
            IsIndeterminate = false;
            Percent = activity.Percent;
            ActivityText = Format.Text("Activity_Copying", activity.Percent.ToString("0", Format.Culture));
        }
    }

    /// <summary>Theme-Pinsel (der Indexer sucht auch in den Design-Wörterbüchern).</summary>
    public override string ToString() => $"{Name}, {LastRunLabel}, {NextRunLabel}"; // Name für Screenreader
}
