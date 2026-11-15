namespace SyncForge.Core.Engine;

public enum SyncPhase
{
    Scanning,
    Comparing,
    Copying,
    Deleting,
    Finished
}

/// <summary>Fortschrittsmeldung eines Ziels (wird gedrosselt, ca. alle 100 ms).</summary>
public sealed record SyncProgress(
    string TargetRoot,
    SyncPhase Phase,
    string? CurrentFile,
    int ItemsDone,
    int ItemsTotal,
    long BytesDone,
    long BytesTotal)
{
    public double Percent => BytesTotal > 0
        ? Math.Min(100, BytesDone * 100.0 / BytesTotal)
        : ItemsTotal > 0 ? Math.Min(100, ItemsDone * 100.0 / ItemsTotal) : 0;
}

public sealed record SyncError(string RelativePath, string Message);

/// <summary>Protokolleintrag einer ausgeführten (oder fehlgeschlagenen) Dateiaktion.</summary>
public sealed record SyncFileRecord(SyncActionKind Kind, string RelativePath, long Size, string? Error = null, SyncSide Side = SyncSide.Target)
{
    public bool Failed => Error is not null;
}

/// <summary>Ergebnis der Ausführung eines Plans für ein Ziel.</summary>
public sealed class SyncResult
{
    public required string TargetRoot { get; init; }
    public int Copied { get; set; }
    public int Updated { get; set; }
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int CreatedDirectories { get; set; }
    public int Conflicts { get; set; }
    /// <summary>Übersprungene, nur online verfügbare Dateien (aus dem Plan übernommen).</summary>
    public int SkippedCloudFiles { get; set; }
    /// <summary>Unlesbare Ordner, die ausgelassen wurden (aus dem Plan übernommen).</summary>
    public int UnreadableFolders { get; set; }
    /// <summary>
    /// Quelle oder Ziel war während des Laufs plötzlich nicht mehr erreichbar (USB-Laufwerk abgezogen,
    /// Netzwerk getrennt). Der Lauf wurde dann geordnet beendet.
    /// </summary>
    public string? LostRoot { get; set; }
    /// <summary>Gesperrte Dateien, die aus einer Schattenkopie gesichert wurden.</summary>
    public int ShadowCopiedFiles { get; set; }
    public long BytesCopied { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Cancelled { get; set; }
    public List<SyncError> Errors { get; } = [];
    /// <summary>Alle ausgeführten bzw. fehlgeschlagenen Aktionen in Ausführungsreihenfolge.</summary>
    public List<SyncFileRecord> Files { get; } = [];
    public bool Success => !Cancelled && Errors.Count == 0;
}
