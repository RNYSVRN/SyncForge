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

/// <summary>
/// Eine Quelle eines Profils. Bei mehreren Quellen landet jede in einem eigenen Unterordner der Ziele
/// (<see cref="TargetFolder"/>); bei nur einer Quelle ist er leer und es wird direkt ins Ziel synchronisiert.
/// </summary>
public sealed class SyncSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;

    /// <summary>Unterordner in jedem Ziel ("" = Ziel selbst). Wird beim Hinzufügen festgelegt und bleibt danach stabil.</summary>
    public string TargetFolder { get; set; } = string.Empty;

    /// <summary>Ordner im Ziel, in den diese Quelle synchronisiert wird.</summary>
    public string TargetRootFor(string targetPath) =>
        string.IsNullOrEmpty(TargetFolder) ? targetPath : System.IO.Path.Combine(targetPath, TargetFolder);

    /// <summary>Anzeigename: Ordnername der Quelle (bei Laufwerken der Pfad selbst).</summary>
    public string DisplayName
    {
        get
        {
            var trimmed = Path.Trim().TrimEnd('\\', '/');
            if (trimmed.Length <= 2) return Path; // Laufwerk
            return trimmed.Split('\\', '/').LastOrDefault(s => s.Length > 0) ?? Path;
        }
    }
}

/// <summary>Ein Backup-Profil: eine oder mehrere Quellen werden in ein oder mehrere Ziele synchronisiert.</summary>
public sealed class SyncProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Erste Quelle. Bleibt für ältere Profile und Export-Dateien erhalten (dort gab es nur diese eine Quelle)
    /// und steht deshalb vor <see cref="Sources"/>: Beim Laden wird sie zuerst gelesen und von einer
    /// vorhandenen Quellenliste ersetzt.
    /// </summary>
    public string SourcePath
    {
        get => Sources.FirstOrDefault()?.Path ?? string.Empty;
        set
        {
            if (Sources.Count == 0) { if (!string.IsNullOrEmpty(value)) Sources.Add(new SyncSource { Path = value }); }
            else Sources[0].Path = value;
        }
    }

    public List<SyncSource> Sources { get; set; } = [];
    public List<SyncTarget> Targets { get; set; } = [];

    /// <summary>
    /// Eindeutiger Unterordnername im Ziel für eine neue Quelle: Ordnername der Quelle (bei Laufwerken
    /// "Laufwerk C"), bei Gleichstand mit Zähler.
    /// </summary>
    public static string SuggestTargetFolder(string sourcePath, IEnumerable<string> taken, string driveLabel = "Laufwerk")
    {
        var trimmed = sourcePath.Trim().TrimEnd('\\', '/');
        var name = trimmed.Length >= 2 && trimmed[1] == ':' && trimmed.Length <= 2
            ? $"{driveLabel} {char.ToUpperInvariant(trimmed[0])}" // Laufwerk, z. B. "D:\"
            : trimmed.Split('\\', '/').LastOrDefault(s => s.Length > 0) ?? "Quelle"; // auch \\server\freigabe
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var candidate = name;
        for (var i = 2; used.Contains(candidate); i++) candidate = $"{name} ({i})";
        return candidate;
    }

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

    /// <summary>Dauerhaft gesperrte Dateien (z. B. Outlook) aus einer Volumeschattenkopie lesen. Wirkt nur mit Administratorrechten.</summary>
    public bool UseShadowCopy { get; set; }

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
