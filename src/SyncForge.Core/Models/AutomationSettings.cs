namespace SyncForge.Core.Models;

public enum ScheduleKind
{
    None,
    /// <summary>Alle <see cref="AutomationSettings.IntervalMinutes"/> Minuten.</summary>
    Interval,
    /// <summary>Täglich um <see cref="AutomationSettings.TimeOfDay"/>.</summary>
    Daily,
    /// <summary>An <see cref="AutomationSettings.Days"/> um <see cref="AutomationSettings.TimeOfDay"/>.</summary>
    Weekly
}

/// <summary>Wann ein Profil automatisch synchronisiert wird.</summary>
public sealed class AutomationSettings
{
    public ScheduleKind Schedule { get; set; } = ScheduleKind.None;
    public int IntervalMinutes { get; set; } = 60;
    public TimeOnly TimeOfDay { get; set; } = new(18, 0);
    public List<DayOfWeek> Days { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>Termine, die verpasst wurden (PC aus, App beendet), beim nächsten Start nachholen.</summary>
    public bool CatchUpMissed { get; set; } = true;

    /// <summary>Bei Änderungen in der Quelle synchronisieren (Echtzeit-Überwachung).</summary>
    public bool WatchForChanges { get; set; }

    /// <summary>Wartezeit nach der letzten Änderung, bevor synchronisiert wird (bündelt viele Änderungen).</summary>
    public int WatchDelaySeconds { get; set; } = 30;

    /// <summary>Beim Start von SyncForge (z. B. bei der Windows-Anmeldung) synchronisieren.</summary>
    public bool RunAtStartup { get; set; }

    /// <summary>Synchronisieren, sobald ein Ziel-Laufwerk (z. B. USB) angeschlossen wird.</summary>
    public bool RunWhenTargetConnected { get; set; }

    /// <summary>Bezugspunkt des Zeitplans: letzter geplanter Lauf bzw. Zeitpunkt der Aktivierung.</summary>
    public DateTimeOffset? ScheduleAnchor { get; set; }

    public bool HasAnyTrigger => Schedule != ScheduleKind.None || WatchForChanges || RunAtStartup || RunWhenTargetConnected;
}
