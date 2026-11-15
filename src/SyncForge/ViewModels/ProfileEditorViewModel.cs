using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Services;

namespace SyncForge.ViewModels;

public sealed partial class TargetItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    [ObservableProperty] public partial string Path { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Enabled { get; set; } = true;
}

public sealed partial class DayOptionViewModel : ObservableObject
{
    public required DayOfWeek Day { get; init; }
    public required string Name { get; init; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
}

public sealed partial class ProfileEditorViewModel : ObservableObject
{
    private const double Megabyte = 1024 * 1024;
    private readonly ProfileStore _store;
    private SyncProfile _profile = new();

    public ProfileEditorViewModel(ProfileStore store)
    {
        _store = store;
    }

    public ObservableCollection<TargetItemViewModel> Targets { get; } = [];

    [ObservableProperty] public partial bool IsNew { get; set; }
    [ObservableProperty] public partial string PageTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string SourcePath { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVersioned), nameof(IsTwoWay), nameof(HasModeOptions), nameof(ModeDescription))]
    public partial int ModeIndex { get; set; }
    [ObservableProperty] public partial int ConflictIndex { get; set; }
    [ObservableProperty] public partial double VersionsToKeep { get; set; }
    [ObservableProperty] public partial double VersionMaxAgeDays { get; set; }
    public bool IsVersioned => ModeIndex == (int)SyncMode.Versioned;
    public bool IsTwoWay => ModeIndex == (int)SyncMode.TwoWay;
    /// <summary>Modus hat Unteroptionen (Aufbewahrung bzw. Konfliktregel): Abschnitt aufklappen.</summary>
    public bool HasModeOptions => IsVersioned || IsTwoWay;
    public string ModeDescription => Localization.Get(ModeIndex switch
    {
        (int)SyncMode.Versioned => "Mode_VersionedDescription",
        (int)SyncMode.TwoWay => "Mode_TwoWayDescription",
        _ => "Mode_MirrorDescription"
    });
    [ObservableProperty] public partial int CompareIndex { get; set; }
    [ObservableProperty] public partial int MultiTargetIndex { get; set; }
    [ObservableProperty] public partial bool VerifyAfterCopy { get; set; }
    [ObservableProperty] public partial string ExcludePatterns { get; set; } = string.Empty;
    [ObservableProperty] public partial string IncludePatterns { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ExcludeHidden { get; set; }
    [ObservableProperty] public partial int CloudFilesIndex { get; set; }
    [ObservableProperty] public partial bool ExcludeSystem { get; set; }
    [ObservableProperty] public partial double MinSizeMb { get; set; }
    [ObservableProperty] public partial double MaxSizeMb { get; set; }
    [ObservableProperty] public partial bool DeleteToRecycleBin { get; set; }
    [ObservableProperty] public partial double DeletionProtectionPercent { get; set; }
    // Automatisierung
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInterval), nameof(IsTimeBased), nameof(IsWeekly), nameof(HasSchedule))]
    public partial int ScheduleIndex { get; set; }
    [ObservableProperty] public partial double IntervalMinutes { get; set; }
    [ObservableProperty] public partial TimeSpan TimeOfDay { get; set; }
    [ObservableProperty] public partial bool CatchUpMissed { get; set; }
    [ObservableProperty] public partial bool WatchForChanges { get; set; }
    [ObservableProperty] public partial double WatchDelaySeconds { get; set; }
    [ObservableProperty] public partial bool RunAtStartup { get; set; }
    [ObservableProperty] public partial bool RunWhenTargetConnected { get; set; }
    public ObservableCollection<DayOptionViewModel> Days { get; } = [];
    public bool IsInterval => ScheduleIndex == (int)ScheduleKind.Interval;
    public bool IsTimeBased => ScheduleIndex is (int)ScheduleKind.Daily or (int)ScheduleKind.Weekly;
    public bool IsWeekly => ScheduleIndex == (int)ScheduleKind.Weekly;
    public bool HasSchedule => ScheduleIndex != (int)ScheduleKind.None;

    [ObservableProperty] public partial string ValidationMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasValidationMessage { get; set; }

    public void Load(Guid? id)
    {
        var existing = id is { } guid ? _store.Get(guid) : null;
        IsNew = existing is null;
        _profile = existing?.Clone() ?? new SyncProfile();
        PageTitle = Localization.Get(IsNew ? "Editor_TitleNew" : "Editor_TitleEdit");

        Name = _profile.Name;
        SourcePath = _profile.SourcePath;
        ModeIndex = (int)_profile.Mode;
        ConflictIndex = (int)_profile.ConflictResolution;
        VersionsToKeep = _profile.VersionsToKeep;
        VersionMaxAgeDays = _profile.VersionMaxAgeDays;
        CompareIndex = (int)_profile.CompareMode;
        MultiTargetIndex = (int)_profile.MultiTargetMode;
        VerifyAfterCopy = _profile.VerifyAfterCopy;
        ExcludePatterns = string.Join(Environment.NewLine, _profile.Filter.ExcludePatterns);
        IncludePatterns = string.Join(Environment.NewLine, _profile.Filter.IncludePatterns);
        ExcludeHidden = _profile.Filter.ExcludeHidden;
        CloudFilesIndex = (int)_profile.Filter.CloudFiles;
        ExcludeSystem = _profile.Filter.ExcludeSystem;
        MinSizeMb = _profile.Filter.MinSizeBytes / Megabyte;
        MaxSizeMb = _profile.Filter.MaxSizeBytes / Megabyte;
        DeleteToRecycleBin = _profile.DeleteToRecycleBin;
        DeletionProtectionPercent = _profile.DeletionProtectionPercent;

        var a = _profile.Automation;
        ScheduleIndex = (int)a.Schedule;
        IntervalMinutes = a.IntervalMinutes;
        TimeOfDay = a.TimeOfDay.ToTimeSpan();
        CatchUpMissed = a.CatchUpMissed;
        WatchForChanges = a.WatchForChanges;
        WatchDelaySeconds = a.WatchDelaySeconds;
        RunAtStartup = a.RunAtStartup;
        RunWhenTargetConnected = a.RunWhenTargetConnected;
        Days.Clear();
        var culture = Helpers.Format.Culture.DateTimeFormat;
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)culture.FirstDayOfWeek + i) % 7);
            Days.Add(new DayOptionViewModel { Day = day, Name = culture.GetAbbreviatedDayName(day), IsSelected = a.Days.Contains(day) });
        }

        Targets.Clear();
        foreach (var t in _profile.Targets) Targets.Add(new TargetItemViewModel { Id = t.Id, Path = t.Path, Enabled = t.Enabled });
        if (Targets.Count == 0) Targets.Add(new TargetItemViewModel());
    }

    public void AddTarget(string path = "") => Targets.Add(new TargetItemViewModel { Path = path });

    public void RemoveTarget(TargetItemViewModel target)
    {
        Targets.Remove(target);
        if (Targets.Count == 0) Targets.Add(new TargetItemViewModel());
    }

    /// <summary>Prüft die Eingaben und speichert. Gibt die Profil-Id zurück oder null bei Fehlern.</summary>
    public Guid? Save()
    {
        var error = Validate();
        ValidationMessage = error ?? string.Empty;
        HasValidationMessage = error is not null;
        if (error is not null) return null;

        _profile.Name = Name.Trim();
        _profile.SourcePath = NormalizePath(SourcePath);
        _profile.Mode = (SyncMode)ModeIndex;
        _profile.ConflictResolution = (ConflictResolution)ConflictIndex;
        _profile.VersionsToKeep = double.IsNaN(VersionsToKeep) ? 10 : (int)Math.Clamp(VersionsToKeep, 0, 1000);
        _profile.VersionMaxAgeDays = double.IsNaN(VersionMaxAgeDays) ? 90 : (int)Math.Clamp(VersionMaxAgeDays, 0, 3650);
        _profile.CompareMode = (CompareMode)CompareIndex;
        _profile.MultiTargetMode = (MultiTargetMode)MultiTargetIndex;
        _profile.VerifyAfterCopy = VerifyAfterCopy;
        _profile.Filter = new FilterSettings
        {
            ExcludePatterns = SplitLines(ExcludePatterns),
            IncludePatterns = SplitLines(IncludePatterns),
            ExcludeHidden = ExcludeHidden,
            CloudFiles = (CloudFileMode)CloudFilesIndex,
            ExcludeSystem = ExcludeSystem,
            MinSizeBytes = ToBytes(MinSizeMb),
            MaxSizeBytes = ToBytes(MaxSizeMb)
        };
        _profile.DeleteToRecycleBin = DeleteToRecycleBin;
        _profile.DeletionProtectionPercent = double.IsNaN(DeletionProtectionPercent) ? 25 : (int)Math.Clamp(DeletionProtectionPercent, 0, 100);
        _profile.Targets = Targets
            .Where(t => !string.IsNullOrWhiteSpace(t.Path))
            .Select(t => CreateTarget(t))
            .ToList();

        var automation = new AutomationSettings
        {
            Schedule = (ScheduleKind)ScheduleIndex,
            IntervalMinutes = double.IsNaN(IntervalMinutes) ? 60 : (int)Math.Clamp(IntervalMinutes, 1, 7 * 24 * 60),
            TimeOfDay = TimeOnly.FromTimeSpan(TimeOfDay),
            Days = Days.Where(d => d.IsSelected).Select(d => d.Day).ToList(),
            CatchUpMissed = CatchUpMissed,
            WatchForChanges = WatchForChanges,
            WatchDelaySeconds = double.IsNaN(WatchDelaySeconds) ? 30 : (int)Math.Clamp(WatchDelaySeconds, 2, 3600),
            RunAtStartup = RunAtStartup,
            RunWhenTargetConnected = RunWhenTargetConnected
        };

        // Inzwischen geänderte Laufzeitdaten (Hintergrundläufe) nicht mit dem Stand beim Öffnen überschreiben.
        var current = _store.Get(_profile.Id);
        if (current is not null)
        {
            _profile.LastRunAt = current.LastRunAt;
            _profile.LastRunStatus = current.LastRunStatus;
            _profile.LastRunSummary = current.LastRunSummary;
            var old = current.Automation;
            var scheduleChanged = old.Schedule != automation.Schedule || old.IntervalMinutes != automation.IntervalMinutes
                || old.TimeOfDay != automation.TimeOfDay || !old.Days.Order().SequenceEqual(automation.Days.Order());
            // Geänderter Zeitplan zählt ab jetzt (der Planer setzt den Bezugspunkt neu).
            automation.ScheduleAnchor = scheduleChanged ? null : old.ScheduleAnchor;
        }
        _profile.Automation = automation;

        _store.Save(_profile);
        return _profile.Id;
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return Localization.Get("Editor_ErrorName");
        if (string.IsNullOrWhiteSpace(SourcePath)) return Localization.Get("Editor_ErrorSourceEmpty");
        if (!IsValidPath(SourcePath)) return Localization.Get("Editor_ErrorPathInvalid");
        if (!Directory.Exists(SourcePath)) return Localization.Get("Editor_ErrorSourceMissing");

        var targets = Targets.Where(t => !string.IsNullOrWhiteSpace(t.Path)).ToList();
        if (targets.Count == 0) return Localization.Get("Editor_ErrorNoTarget");

        foreach (var t in targets)
        {
            if (!IsValidPath(t.Path)) return Localization.Get("Editor_ErrorPathInvalid") + " " + t.Path;
            try
            {
                SyncPlanner.ValidatePaths(SourcePath, t.Path);
            }
            catch (SyncValidationException ex)
            {
                return Localization.Get("Validation_" + ex.Error) + " " + t.Path;
            }
        }

        var duplicates = targets.GroupBy(t => NormalizePath(t.Path), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicates is not null) return Localization.Get("Editor_ErrorDuplicateTarget") + " " + duplicates.Key;

        if (MaxSizeMb > 0 && MinSizeMb > MaxSizeMb) return Localization.Get("Editor_ErrorSizeRange");
        if (IsWeekly && !Days.Any(d => d.IsSelected)) return Localization.Get("Editor_ErrorNoDays");
        return null;
    }

    /// <summary>
    /// Ziel mit Volume-ID: Für lokale/USB-Laufwerke (außer dem Systemlaufwerk) wird die Seriennummer gespeichert,
    /// damit das Laufwerk auch unter anderem Buchstaben erkannt und nie ein fremdes beschrieben wird.
    /// </summary>
    private static SyncTarget CreateTarget(TargetItemViewModel t)
    {
        var target = new SyncTarget { Id = t.Id, Path = NormalizePath(t.Path), Enabled = t.Enabled };
        try
        {
            var root = System.IO.Path.GetPathRoot(target.Path);
            var systemRoot = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal)
                && !string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase)
                && new DriveInfo(root).DriveType is DriveType.Fixed or DriveType.Removable
                && VolumeResolver.GetVolume(root) is { } volume)
            {
                target.VolumeSerial = volume.Serial;
                target.VolumeLabel = volume.Label;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Laufwerk nicht bereit: ohne Volume-ID speichern.
        }
        return target;
    }

    private static bool IsValidPath(string path)
    {
        try
        {
            return System.IO.Path.IsPathFullyQualified(path.Trim()) && System.IO.Path.GetFullPath(path.Trim()).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizePath(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path.Trim()));

    private static List<string> SplitLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static long ToBytes(double mb) => double.IsNaN(mb) || mb <= 0 ? 0 : (long)(mb * Megabyte);
}
