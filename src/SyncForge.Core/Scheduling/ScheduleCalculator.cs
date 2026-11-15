using SyncForge.Core.Models;

namespace SyncForge.Core.Scheduling;

/// <summary>Berechnet Termine eines Zeitplans. Uhrzeiten gelten in lokaler Zeit (inkl. Sommer-/Winterzeit).</summary>
public static class ScheduleCalculator
{
    /// <summary>Spielraum, innerhalb dessen ein Termin auch ohne "Nachholen" noch ausgeführt wird.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    /// <summary>Erster Termin nach <paramref name="anchor"/>; null, wenn kein Zeitplan aktiv ist.</summary>
    public static DateTimeOffset? NextAfter(AutomationSettings s, DateTimeOffset anchor)
    {
        switch (s.Schedule)
        {
            case ScheduleKind.Interval:
                return anchor.AddMinutes(Math.Max(1, s.IntervalMinutes));

            case ScheduleKind.Daily:
                return NextAtTime(anchor, s.TimeOfDay, _ => true);

            case ScheduleKind.Weekly:
                if (s.Days.Count == 0) return null;
                var days = s.Days.ToHashSet();
                return NextAtTime(anchor, s.TimeOfDay, d => days.Contains(d.DayOfWeek));

            default:
                return null;
        }
    }

    /// <summary>
    /// Entscheidet, ob jetzt ein Lauf fällig ist, und liefert den nächsten Termin.
    /// Verpasste Termine werden – je nach <see cref="AutomationSettings.CatchUpMissed"/> – einmalig nachgeholt
    /// oder übersprungen (nie mehrfach hintereinander).
    /// </summary>
    public static (bool Due, DateTimeOffset? Next) Evaluate(AutomationSettings s, DateTimeOffset anchor, DateTimeOffset now)
    {
        var next = NextAfter(s, anchor);
        if (next is null) return (false, null);
        if (next > now) return (false, next);

        var missedLongAgo = now - next.Value > Grace;
        var due = !missedLongAgo || s.CatchUpMissed;
        // Folgetermin immer ab "jetzt", damit nach langer Pause nicht mehrere Läufe nachgeholt werden.
        return (due, NextAfter(s, now));
    }

    private static DateTimeOffset NextAtTime(DateTimeOffset anchor, TimeOnly time, Func<DateTime, bool> dayAllowed)
    {
        var local = anchor.ToLocalTime().DateTime;
        for (var i = 0; i <= 8; i++)
        {
            var day = local.Date.AddDays(i);
            if (!dayAllowed(day)) continue;
            var candidate = DateTime.SpecifyKind(day + time.ToTimeSpan(), DateTimeKind.Local);
            if (candidate > local) return new DateTimeOffset(candidate);
        }
        // Nicht erreichbar, solange mindestens ein Tag erlaubt ist.
        return anchor.AddDays(7);
    }
}
