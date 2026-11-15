namespace SyncForge.Core.Models;

public enum SyncMode
{
    /// <summary>Ziel = exakte Kopie der Quelle (inkl. Löschungen).</summary>
    Mirror,
    /// <summary>Wie Spiegeln, überschriebene/gelöschte Dateien landen im Versionsordner (Etappe 4).</summary>
    Versioned,
    /// <summary>Änderungen in beide Richtungen abgleichen (Etappe 4).</summary>
    TwoWay
}

public enum CompareMode
{
    /// <summary>Gleich, wenn Größe und Änderungsdatum (±2 s) übereinstimmen. Schnell.</summary>
    SizeAndDate,
    /// <summary>Gleich, wenn Größe und Inhalt (SHA-256) übereinstimmen. Gründlich, langsamer.</summary>
    Content
}

public enum MultiTargetMode
{
    Sequential,
    Parallel
}

public enum ConflictResolution
{
    NewerWins,
    SourceWins,
    KeepBoth,
    Ask
}

public enum RunStatus
{
    None,
    Success,
    Warning,
    Error,
    Cancelled
}

/// <summary>Ein Backup-Profil: eine Quelle wird in ein oder mehrere Ziele synchronisiert.</summary>
public sealed class SyncProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public List<SyncTarget> Targets { get; set; } = [];

    public SyncMode Mode { get; set; } = SyncMode.Mirror;
    public CompareMode CompareMode { get; set; } = CompareMode.SizeAndDate;
    public bool VerifyAfterCopy { get; set; }
    public MultiTargetMode MultiTargetMode { get; set; } = MultiTargetMode.Sequential;
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.NewerWins;

    public FilterSettings Filter { get; set; } = new();

    public AutomationSettings Automation { get; set; } = new();

    /// <summary>Modus "Versioniert": so viele Fassungen je Datei behalten (0 = unbegrenzt).</summary>
    public int VersionsToKeep { get; set; } = 10;

    /// <summary>Modus "Versioniert": Fassungen nach so vielen Tagen entfernen (0 = nie).</summary>
    public int VersionMaxAgeDays { get; set; } = 90;

    /// <summary>Gelöschte Dateien in den Papierkorb verschieben (nur lokale Laufwerke).</summary>
    public bool DeleteToRecycleBin { get; set; } = true;

    /// <summary>Warnen, wenn mehr als dieser Anteil (%) der Zieldateien gelöscht würde. 0 = aus.</summary>
    public int DeletionProtectionPercent { get; set; } = 25;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    // Letzter Lauf (bis die Historie in Etappe 3 kommt)
    public DateTimeOffset? LastRunAt { get; set; }
    public RunStatus LastRunStatus { get; set; }
    public string? LastRunSummary { get; set; }

    public SyncProfile Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<SyncProfile>(json)!;
    }
}

public sealed class SyncTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>Seriennummer des Laufwerks (lokale/USB-Ziele) – erkennt das Laufwerk auch unter anderem Buchstaben.</summary>
    public uint? VolumeSerial { get; set; }

    /// <summary>Bezeichnung des Laufwerks zur Anzeige (z. B. "Backup-HDD").</summary>
    public string? VolumeLabel { get; set; }
}

/// <summary>Umgang mit Dateien, die nur online verfügbar sind (Cloud-Platzhalter).</summary>
public enum CloudFileMode
{
    /// <summary>Nicht herunterladen und nicht synchronisieren; vorhandene Kopien im Ziel bleiben erhalten.</summary>
    Skip,
    /// <summary>Herunterladen (löst den Download durch den Cloud-Dienst aus) und synchronisieren.</summary>
    Download
}

public sealed class FilterSettings
{
    /// <summary>Nur online verfügbare Dateien überspringen (Standard) oder herunterladen.</summary>
    public CloudFileMode CloudFiles { get; set; } = CloudFileMode.Skip;

    /// <summary>Ausschlussmuster, je Zeile eines (z. B. *.tmp, node_modules\, Ordner\Unterordner\*.log).</summary>
    public List<string> ExcludePatterns { get; set; } = ["Thumbs.db", "desktop.ini", "~$*", "*.tmp"];

    /// <summary>Einschlussmuster für Dateien. Leer = alle Dateien.</summary>
    public List<string> IncludePatterns { get; set; } = [];

    public bool ExcludeHidden { get; set; }
    public bool ExcludeSystem { get; set; } = true;

    /// <summary>Minimale Dateigröße in Bytes (0 = keine Grenze).</summary>
    public long MinSizeBytes { get; set; }

    /// <summary>Maximale Dateigröße in Bytes (0 = keine Grenze).</summary>
    public long MaxSizeBytes { get; set; }
}
