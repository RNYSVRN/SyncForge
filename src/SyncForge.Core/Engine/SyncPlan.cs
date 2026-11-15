namespace SyncForge.Core.Engine;

public enum SyncActionKind
{
    /// <summary>Ordner im Ziel anlegen.</summary>
    CreateDirectory,
    /// <summary>Neue Datei ins Ziel kopieren.</summary>
    CopyNew,
    /// <summary>Geänderte Datei im Ziel überschreiben.</summary>
    Update,
    /// <summary>Datei im Ziel löschen (in der Quelle nicht mehr vorhanden).</summary>
    DeleteFile,
    /// <summary>Ordner im Ziel löschen (in der Quelle nicht mehr vorhanden).</summary>
    DeleteDirectory
}

/// <summary>Ein einzelner Schritt der Synchronisierung. Pfade sind relativ zum Quell- bzw. Zielordner.</summary>
public sealed record SyncAction(SyncActionKind Kind, string RelativePath, long Size, bool IsTypeConflict = false)
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
    public long BytesToCopy => Actions.Where(a => a.IsCopy).Sum(a => a.Size);
    public bool HasChanges => Actions.Count > 0 || TargetRootMissing;

    /// <summary>Anteil der Zieldateien, die gelöscht würden (0–100).</summary>
    public double DeletePercent =>
        ExistingTargetFileCount == 0 ? 0 : DeleteFileCount * 100.0 / ExistingTargetFileCount;

    /// <summary>True, wenn der Löschschutz anschlägt (Schwelle überschritten).</summary>
    public bool ExceedsDeletionThreshold(int thresholdPercent) =>
        thresholdPercent > 0 && DeleteFileCount > 0 && DeletePercent > thresholdPercent;
}
