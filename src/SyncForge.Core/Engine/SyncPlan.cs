using SyncForge.Core.Models;

namespace SyncForge.Core.Engine;

public enum SyncActionKind
{
    /// <summary>Ordner anlegen.</summary>
    CreateDirectory,
    /// <summary>Neue Datei kopieren.</summary>
    CopyNew,
    /// <summary>Geänderte Datei überschreiben.</summary>
    Update,
    /// <summary>Datei löschen (auf der anderen Seite nicht mehr vorhanden).</summary>
    DeleteFile,
    /// <summary>Ordner löschen (auf der anderen Seite nicht mehr vorhanden).</summary>
    DeleteDirectory,
    /// <summary>Konflikt: Datei auf dieser Seite umbenennen, damit beide Fassungen erhalten bleiben.</summary>
    RenameConflict,
    /// <summary>Konflikt, der nicht automatisch gelöst wird ("nachfragen"): nichts ändern, nur melden.</summary>
    Conflict
}

/// <summary>Welche Seite eine Aktion verändert. Kopien laufen immer von der jeweils anderen Seite hierher.</summary>
public enum SyncSide
{
    Target,
    Source
}

/// <summary>
/// Ein einzelner Schritt der Synchronisierung. Pfade sind relativ zu den Wurzelordnern.
/// <see cref="Side"/> ist die veränderte Seite (Standard: Ziel). Bei <see cref="SyncActionKind.RenameConflict"/>
/// enthält <see cref="NewRelativePath"/> den neuen Namen.
/// </summary>
public sealed record SyncAction(
    SyncActionKind Kind,
    string RelativePath,
    long Size,
    bool IsTypeConflict = false,
    SyncSide Side = SyncSide.Target,
    string? NewRelativePath = null)
{
    public bool IsDelete => Kind is SyncActionKind.DeleteFile or SyncActionKind.DeleteDirectory;
    public bool IsCopy => Kind is SyncActionKind.CopyNew or SyncActionKind.Update;
}

/// <summary>Ergebnis der Analyse eines Ziels: was zu tun ist, bevor etwas verändert wird.</summary>
public sealed class SyncPlan
{
    public required string SourceRoot { get; init; }
    public required string TargetRoot { get; init; }
    public bool TargetRootMissing { get; init; }
    public SyncMode Mode { get; init; } = SyncMode.Mirror;

    /// <summary>Zwei-Wege: Datei mit dem Stand nach dem letzten Lauf (wird nach der Ausführung aktualisiert).</summary>
    public string? StatePath { get; init; }
    public FilterSettings? Filter { get; init; }

    public List<SyncAction> Actions { get; } = [];

    /// <summary>Anzahl der (gefilterten) Dateien, die vorher im Ziel lagen.</summary>
    public int ExistingTargetFileCount { get; set; }
    public int UnchangedFileCount { get; set; }
    public int SourceFileCount { get; set; }
    public long SourceBytes { get; set; }

    public int CopyNewCount => Actions.Count(a => a.Kind == SyncActionKind.CopyNew);
    public int UpdateCount => Actions.Count(a => a.Kind == SyncActionKind.Update);
    public int DeleteFileCount => Actions.Count(a => a.Kind == SyncActionKind.DeleteFile);
    public int DeleteDirectoryCount => Actions.Count(a => a.Kind == SyncActionKind.DeleteDirectory);
    public int CreateDirectoryCount => Actions.Count(a => a.Kind == SyncActionKind.CreateDirectory);
    public int ConflictCount => Actions.Count(a => a.Kind is SyncActionKind.Conflict or SyncActionKind.RenameConflict);
    public int ToSourceCount => Actions.Count(a => a.Side == SyncSide.Source && a.Kind != SyncActionKind.Conflict);
    public long BytesToCopy => Actions.Where(a => a.IsCopy).Sum(a => a.Size);
    public bool HasChanges => Actions.Any(a => a.Kind != SyncActionKind.Conflict) || TargetRootMissing;

    /// <summary>Anteil der Dateien, die auf einer Seite gelöscht würden (0–100, die stärker betroffene Seite zählt).</summary>
    public double DeletePercent
    {
        get
        {
            var target = Percent(Actions.Count(a => a.Kind == SyncActionKind.DeleteFile && a.Side == SyncSide.Target), ExistingTargetFileCount);
            var source = Percent(Actions.Count(a => a.Kind == SyncActionKind.DeleteFile && a.Side == SyncSide.Source), SourceFileCount);
            return Math.Max(target, source);
        }
    }

    /// <summary>True, wenn der Löschschutz anschlägt (Schwelle überschritten).</summary>
    public bool ExceedsDeletionThreshold(int thresholdPercent) =>
        thresholdPercent > 0 && DeleteFileCount > 0 && DeletePercent > thresholdPercent;

    private static double Percent(int part, int total) => total == 0 ? 0 : part * 100.0 / total;
}
