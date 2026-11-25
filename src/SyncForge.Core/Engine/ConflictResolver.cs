using System.Globalization;

namespace SyncForge.Core.Engine;

public enum ConflictChoice
{
    /// <summary>Nicht entscheiden: bleibt ein offener Konflikt.</summary>
    Skip,
    /// <summary>Fassung der Quelle auf beide Seiten bringen.</summary>
    KeepSource,
    /// <summary>Fassung des Ziels auf beide Seiten bringen.</summary>
    KeepTarget,
    /// <summary>Beide Fassungen behalten: die Zielfassung wird umbenannt und ebenfalls auf beide Seiten kopiert.</summary>
    KeepBoth
}

/// <summary>Eine Seite eines Konflikts (null = gibt es dort nicht).</summary>
public sealed record ConflictVersion(bool IsDirectory, long Size, DateTime LastWriteUtc);

/// <summary>Offener Zwei-Wege-Konflikt ("nur melden"): beide Seiten seit dem letzten Lauf geändert.</summary>
public sealed record ConflictInfo(string RelativePath, ConflictVersion? Source, ConflictVersion? Target)
{
    /// <summary>Ordner auf der einen, Datei auf der anderen Seite: lässt sich nicht automatisch lösen.</summary>
    public bool IsTypeConflict => Source is not null && Target is not null && Source.IsDirectory != Target.IsDirectory;
    public bool CanResolve => !IsTypeConflict && Source is { IsDirectory: false } && Target is { IsDirectory: false };

    /// <summary>Welche Seite ist neuer (null = gleich alt oder nicht vergleichbar).</summary>
    public SyncSide? Newer =>
        Source is null || Target is null || Source.IsDirectory || Target.IsDirectory
            || (Source.LastWriteUtc - Target.LastWriteUtc).Duration() <= SyncPlanner.TimestampTolerance
            ? null
            : Source.LastWriteUtc > Target.LastWriteUtc ? SyncSide.Source : SyncSide.Target;
}

/// <summary>Listet die offenen Konflikte eines Zwei-Wege-Plans und löst sie nach Wahl des Benutzers.</summary>
public static class ConflictResolver
{
    /// <summary>Offene Konflikte mit dem aktuellen Stand beider Fassungen.</summary>
    public static IReadOnlyList<ConflictInfo> List(SyncPlan plan) =>
        plan.Actions
            .Where(a => a.Kind == SyncActionKind.Conflict)
            .Select(a => new ConflictInfo(a.RelativePath,
                Describe(Path.Combine(plan.SourceRoot, a.RelativePath)),
                Describe(Path.Combine(plan.TargetRoot, a.RelativePath))))
            .OrderBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Plan zum Lösen der gewählten Konflikte. Er wird wie jeder Plan mit dem <see cref="SyncExecutor"/> ausgeführt
    /// (Temp-Datei, Versionen, Papierkorb); danach den gemeinsamen Stand mit <see cref="TwoWayPlanner.UpdateState"/>
    /// speichern, damit die gelösten Dateien nicht erneut als Konflikt erscheinen.
    /// </summary>
    public static SyncPlan BuildPlan(SyncPlan analyzed, IEnumerable<(ConflictInfo Conflict, ConflictChoice Choice)> choices, string conflictLabel)
    {
        var plan = new SyncPlan
        {
            SourceRoot = analyzed.SourceRoot, TargetRoot = analyzed.TargetRoot, Mode = analyzed.Mode,
            StatePath = analyzed.StatePath, Filter = analyzed.Filter
        };
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
        var renames = new List<SyncAction>();
        var copies = new List<SyncAction>();

        foreach (var (c, choice) in choices)
        {
            if (!c.CanResolve) continue;
            switch (choice)
            {
                case ConflictChoice.KeepSource:
                    copies.Add(new SyncAction(SyncActionKind.Update, c.RelativePath, c.Source!.Size, Side: SyncSide.Target));
                    break;
                case ConflictChoice.KeepTarget:
                    copies.Add(new SyncAction(SyncActionKind.Update, c.RelativePath, c.Target!.Size, Side: SyncSide.Source));
                    break;
                case ConflictChoice.KeepBoth:
                    var renamed = TwoWayPlanner.ConflictName(c.RelativePath, conflictLabel, stamp);
                    renames.Add(new SyncAction(SyncActionKind.RenameConflict, c.RelativePath, c.Target!.Size, Side: SyncSide.Target, NewRelativePath: renamed));
                    copies.Add(new SyncAction(SyncActionKind.CopyNew, renamed, c.Target.Size, Side: SyncSide.Source));
                    copies.Add(new SyncAction(SyncActionKind.CopyNew, c.RelativePath, c.Source!.Size, Side: SyncSide.Target));
                    break;
            }
        }

        plan.Actions.AddRange(renames);
        plan.Actions.AddRange(copies.OrderBy(a => a.RelativePath, StringComparer.OrdinalIgnoreCase));
        return plan;
    }

    private static ConflictVersion? Describe(string path)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            return new ConflictVersion(false, info.Length, info.LastWriteTimeUtc);
        }
        return Directory.Exists(path) ? new ConflictVersion(true, 0, Directory.GetLastWriteTimeUtc(path)) : null;
    }
}
