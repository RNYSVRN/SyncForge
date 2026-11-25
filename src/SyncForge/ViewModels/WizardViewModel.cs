using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Models;
using SyncForge.Core.Network;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

/// <summary>Vorlage für den Assistenten: sinnvolle Voreinstellungen für einen typischen Zweck.</summary>
public sealed record WizardTemplate(
    string Id, string Glyph, SyncMode Mode, Environment.SpecialFolder[] Folders,
    ScheduleKind Schedule, bool Watch, bool DriveConnect)
{
    public string Title => Localization.Get($"Wizard_Template_{Id}_Title");
    public string Text => Localization.Get($"Wizard_Template_{Id}_Text");
    public bool IsCustom => Id == "custom";
    public override string ToString() => $"{Title}, {Text}"; // Name für Screenreader
}

/// <summary>
/// Assistent für neue Profile: Vorlage, Quellen, Ziel, Zeitpunkt, Zusammenfassung.
/// Die Prüfungen sind dieselben wie im Profil-Editor (<see cref="ProfileValidation"/>).
/// </summary>
public sealed partial class WizardViewModel : ObservableObject
{
    public const int StepCount = 5;
    private readonly ProfileStore _store;

    public WizardViewModel(ProfileStore store)
    {
        _store = store;
    }

    public IReadOnlyList<WizardTemplate> Templates { get; } =
    [
        new("documents", "", SyncMode.Mirror, [Environment.SpecialFolder.MyDocuments], ScheduleKind.Daily, false, true),
        new("photos", "", SyncMode.Mirror, [Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos], ScheduleKind.Daily, false, true),
        new("usb", "", SyncMode.Mirror, [], ScheduleKind.None, false, true),
        new("nas", "", SyncMode.Versioned, [Environment.SpecialFolder.MyDocuments], ScheduleKind.Daily, false, false),
        new("twoway", "", SyncMode.TwoWay, [], ScheduleKind.None, true, false),
        new("custom", "", SyncMode.Mirror, [], ScheduleKind.None, false, false),
    ];

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTemplateStep), nameof(IsSourcesStep), nameof(IsTargetStep), nameof(IsWhenStep),
        nameof(IsSummaryStep), nameof(CanGoBack), nameof(ShowNext), nameof(StepText), nameof(StepProgress))]
    public partial int Step { get; set; }

    public bool IsTemplateStep => Step == 0;
    public bool IsSourcesStep => Step == 1;
    public bool IsTargetStep => Step == 2;
    public bool IsWhenStep => Step == 3;
    public bool IsSummaryStep => Step == 4;
    public bool CanGoBack => Step > 0;
    public bool ShowNext => Step is > 0 and < StepCount - 1;
    public string StepText => Format.Text("Wizard_StepOf", Step + 1, StepCount);
    public double StepProgress => (Step + 1) * 100.0 / StepCount;

    [ObservableProperty] public partial WizardTemplate? Template { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetIsNetwork))]
    public partial string TargetPath { get; set; } = string.Empty;
    public bool TargetIsNetwork => NetworkShares.IsNetworkPath(TargetPath);
    [ObservableProperty] public partial string TargetInfo { get; set; } = string.Empty;

    /// <summary>0 = nur manuell, 1 = täglich um <see cref="TimeOfDay"/>, 2 = stündlich.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDaily))]
    public partial int ScheduleIndex { get; set; }
    public bool IsDaily => ScheduleIndex == 1;
    [ObservableProperty] public partial TimeSpan TimeOfDay { get; set; } = new(18, 0, 0);
    [ObservableProperty] public partial bool Watch { get; set; }
    [ObservableProperty] public partial bool DriveConnect { get; set; }

    [ObservableProperty] public partial string Error { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasError { get; set; }

    [ObservableProperty] public partial string SummaryMode { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummarySources { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummaryTarget { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummaryWhen { get; set; } = string.Empty;

    /// <summary>Vorlage übernehmen und zu den Quellen weitergehen.</summary>
    public void SelectTemplate(WizardTemplate template)
    {
        Template = template;
        Sources.Clear();
        foreach (var folder in template.Folders)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) AddSource(path);
        }
        if (Sources.Count == 0) AddSource();
        ScheduleIndex = template.Schedule switch { ScheduleKind.Daily => 1, ScheduleKind.Interval => 2, _ => 0 };
        Watch = template.Watch;
        DriveConnect = template.DriveConnect;
        Name = string.Empty;
        ClearError();
        Step = 1;
    }

    public SourceItemViewModel AddSource(string path = "")
    {
        var item = new SourceItemViewModel { Path = path };
        Sources.Add(item);
        return item;
    }

    public void RemoveSource(SourceItemViewModel source)
    {
        Sources.Remove(source);
        if (Sources.Count == 0) AddSource();
    }

    // Gleicher Hinweis wie im Editor (Laufwerk, freier Platz, Warnungen wie "gleiches Laufwerk").
    partial void OnTargetPathChanged(string value) => TargetInfo = PathHints.ForTarget(value, Sources.Select(s => s.Path).ToList()).Text;

    public void Back()
    {
        ClearError();
        if (Step > 0) Step--;
    }

    /// <summary>Prüft den aktuellen Schritt und geht weiter (false = Fehler wird angezeigt).</summary>
    public bool Next()
    {
        var error = Step switch
        {
            1 => ValidateSources(),
            2 => ValidateTarget(),
            _ => null
        };
        if (error is not null)
        {
            Error = error;
            HasError = true;
            return false;
        }
        ClearError();
        if (Step == 3) PrepareSummary();
        if (Step < StepCount - 1) Step++;
        return true;
    }

    private string? ValidateSources()
    {
        var used = Sources.Where(s => !string.IsNullOrWhiteSpace(s.Path)).Select(s => (s.Path, string.Empty)).ToList();
        if (used.Count == 0) return Localization.Get("Editor_ErrorSourceEmpty");
        // Ziel ist noch nicht bekannt: nur die Quellen selbst prüfen (das Ziel wird im nächsten Schritt geprüft).
        foreach (var (path, _) in used)
        {
            if (!ProfileValidation.IsValidPath(path)) return Localization.Get("Editor_ErrorPathInvalid") + " " + path;
            NetworkShares.EnsureAccess(path);
            if (!Directory.Exists(path))
                return Localization.Get(NetworkShares.IsNetworkPath(path) ? "Editor_ErrorSourceNetwork" : "Editor_ErrorSourceMissing") + " " + path;
        }
        return null;
    }

    private List<(string Path, string TargetFolder)> SourceList() =>
        Sources.Where(s => !string.IsNullOrWhiteSpace(s.Path)).Select(s => (s.Path.Trim(), string.Empty)).ToList();

    private string? ValidateTarget()
    {
        if (string.IsNullOrWhiteSpace(TargetPath)) return Localization.Get("Editor_ErrorNoTarget");
        var sources = SourceList();
        var folders = ProfileValidation.TargetFolders(sources);
        return ProfileValidation.ValidatePaths(sources.Select((s, i) => (s.Path, folders[i])).ToList(), [TargetPath]);
    }

    private void PrepareSummary()
    {
        var template = Template ?? Templates[0];
        if (string.IsNullOrWhiteSpace(Name))
        {
            var sources = SourceList();
            Name = sources.Count == 1 ? $"{DisplayName(sources[0].Path)} → {DisplayName(TargetPath)}" : template.Title;
        }
        SummaryMode = Format.ModeName(template.Mode);
        SummarySources = string.Join(Environment.NewLine, SourceList().Select(s => s.Path));
        SummaryTarget = TargetPath + (string.IsNullOrEmpty(TargetInfo) ? string.Empty : Environment.NewLine + TargetInfo);
        var when = new List<string>();
        if (ScheduleIndex == 1) when.Add(Format.Text("Wizard_WhenDailyAt", DateTime.Today.Add(TimeOfDay).ToString("t", Format.Culture)));
        if (ScheduleIndex == 2) when.Add(Localization.Get("Wizard_WhenHourly"));
        if (Watch) when.Add(Localization.Get("Dashboard_OnChanges"));
        if (DriveConnect) when.Add(Localization.Get("Dashboard_OnDrive"));
        SummaryWhen = when.Count > 0 ? string.Join(" · ", when) : Localization.Get("Dashboard_ManualOnly");
    }

    /// <summary>Legt das Profil an und gibt seine Id zurück (null = Fehler wird angezeigt).</summary>
    public Guid? Create()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = Localization.Get("Editor_ErrorName");
            HasError = true;
            return null;
        }
        var template = Template ?? Templates[0];
        var sources = SourceList();
        var folders = ProfileValidation.TargetFolders(sources);
        var profile = new SyncProfile
        {
            Name = Name.Trim(),
            Mode = template.Mode,
            // Zwei-Wege: Bei Konflikten nichts verlieren (beide Fassungen behalten).
            ConflictResolution = template.Mode == SyncMode.TwoWay ? ConflictResolution.KeepBoth : ConflictResolution.NewerWins,
            Sources = sources.Select((s, i) => new SyncSource { Path = ProfileValidation.NormalizePath(s.Path), TargetFolder = folders[i] }).ToList(),
            Targets = [ProfileValidation.CreateTarget(Guid.NewGuid(), TargetPath, true)],
            Automation = new AutomationSettings
            {
                Schedule = ScheduleIndex switch { 1 => ScheduleKind.Daily, 2 => ScheduleKind.Interval, _ => ScheduleKind.None },
                IntervalMinutes = 60,
                TimeOfDay = TimeOnly.FromTimeSpan(TimeOfDay),
                WatchForChanges = Watch,
                RunWhenTargetConnected = DriveConnect
            }
        };
        _store.Save(profile);
        return profile.Id;
    }

    private void ClearError()
    {
        Error = string.Empty;
        HasError = false;
    }

    private static string DisplayName(string path)
    {
        var trimmed = path.Trim().TrimEnd('\\', '/');
        return trimmed.Split('\\', '/').LastOrDefault(s => s.Length > 0) ?? path;
    }

    /// <summary>"USB-Laufwerk „Backup“ · 812 GB frei" (leer, wenn nichts bekannt ist).</summary>
    public static string DescribeTarget(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !ProfileValidation.IsValidPath(path)) return string.Empty;
            if (NetworkShares.IsNetworkPath(path)) return Localization.Get("Wizard_TargetNetwork");
            var root = Path.GetPathRoot(Path.GetFullPath(path.Trim()));
            if (string.IsNullOrEmpty(root)) return string.Empty;
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return Localization.Get("Wizard_TargetNotReady");
            var kind = Localization.Get(drive.DriveType switch
            {
                DriveType.Removable => "Wizard_DriveRemovable",
                DriveType.Network => "Wizard_DriveNetwork",
                _ => "Wizard_DriveFixed"
            });
            var label = string.IsNullOrEmpty(drive.VolumeLabel) ? root.TrimEnd('\\') : $"{drive.VolumeLabel} ({root.TrimEnd('\\')})";
            return Format.Text("Wizard_TargetInfo", kind, label, Format.Bytes(drive.AvailableFreeSpace));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Empty;
        }
    }
}
