namespace SyncForge.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

/// <summary>Persistente App-Einstellungen (JSON unter %LOCALAPPDATA%\SyncForge).</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Leer = Systemsprache, sonst z. B. "de-DE" oder "en-US".</summary>
    public string Language { get; set; } = string.Empty;

    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    public bool NotifyOnSuccess { get; set; }
    public bool NotifyOnError { get; set; } = true;
    public bool NotifyOnConflict { get; set; } = true;

    public int MaxParallelCopies { get; set; } = 4;
    public int BandwidthLimitKBps { get; set; }
    public bool PauseOnBattery { get; set; } = true;
    public bool PauseOnMeteredConnection { get; set; } = true;

    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>Einmal täglich bei GitHub nach einer neuen Version fragen (aus Datenschutzgründen standardmäßig aus).</summary>
    public bool CheckForUpdates { get; set; }
    public DateTimeOffset? LastUpdateCheck { get; set; }
    /// <summary>Zuletzt gefundene neuere Version (Tag und Seite), damit der Hinweis auch ohne neue Abfrage erscheint.</summary>
    public string? AvailableUpdateTag { get; set; }
    public string? AvailableUpdateUrl { get; set; }
    /// <summary>Für diese Version wurde schon eine Benachrichtigung gezeigt.</summary>
    public string? NotifiedUpdateTag { get; set; }
    /// <summary>Hinweis in der Übersicht für diese Version geschlossen.</summary>
    public string? DismissedUpdateTag { get; set; }

    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 800;
}
