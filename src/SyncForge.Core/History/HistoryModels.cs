using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.History;

/// <summary>Wodurch ein Lauf ausgelöst wurde.</summary>
public enum RunTrigger
{
    Manual,
    Schedule,
    Watcher,
    Event,
    /// <summary>Über die Befehlszeile gestartet (z. B. Skript oder Aufgabenplanung).</summary>
    CommandLine
}

public enum TargetRunStatus
{
    Success,
    Error,
    Cancelled,
    Skipped
}

/// <summary>Filter für die Datei-Details eines Laufs.</summary>
public enum FileFilter
{
    All,
    New,
    Updated,
    Deleted,
    Errors,
    Conflicts
}

/// <summary>Ein Lauf in der Übersicht (eine Zeile der Historie).</summary>
public sealed record RunSummary(
    long Id,
    Guid ProfileId,
    string ProfileName,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    RunStatus Status,
    RunTrigger Trigger,
    int Copied,
    int Updated,
    int Deleted,
    long Bytes,
    int Errors,
    int TargetsTotal,
    int TargetsSkipped,
    int Conflicts = 0);

public sealed record TargetRunEntry(
    long Id,
    string TargetPath,
    TargetRunStatus Status,
    int Copied,
    int Updated,
    int Deleted,
    long Bytes,
    int Errors,
    TimeSpan Duration,
    string? Message);

public sealed record RunDetail(RunSummary Run, IReadOnlyList<TargetRunEntry> Targets);

public sealed record RunFileEntry(long TargetId, SyncActionKind Kind, string Path, long Size, string? Error, SyncSide Side = SyncSide.Target);

public sealed record RunQuery
{
    public Guid? ProfileId { get; init; }
    public RunStatus? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public int Limit { get; init; } = 200;
    public int Offset { get; init; }
}

/// <summary>Kennzahlen eines Tages (lokale Zeit).</summary>
public sealed record DailyStat(DateOnly Date, int Runs, int Failed, long Bytes, int Files, TimeSpan TotalDuration);

public sealed record HistoryOverview(int Runs, int Successful, int Failed, long Bytes, int Files, TimeSpan AverageDuration)
{
    public double SuccessRate => Runs == 0 ? 0 : Successful * 100.0 / Runs;
}

public sealed record ProfileRef(Guid Id, string Name);
