using SyncForge.Core.Models;

namespace SyncForge.Core.Engine;

public enum TargetStatus
{
    Ready,
    Unavailable,
    Error
}

/// <summary>Analyse-Ergebnis eines Ziels.</summary>
public sealed class TargetAnalysis
{
    public required SyncTarget Target { get; init; }
    public TargetStatus Status { get; set; }
    public SyncPlan? Plan { get; set; }
    public SyncValidationError? ValidationError { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ProfileAnalysis
{
    public required SyncProfile Profile { get; init; }
    public List<TargetAnalysis> Targets { get; } = [];
    public bool SourceMissing { get; set; }

    public IEnumerable<TargetAnalysis> ReadyTargets => Targets.Where(t => t.Status == TargetStatus.Ready && t.Plan is not null);

    /// <summary>Ziele, bei denen der Löschschutz anschlägt.</summary>
    public IEnumerable<TargetAnalysis> DeletionWarnings =>
        ReadyTargets.Where(t => t.Plan!.ExceedsDeletionThreshold(Profile.DeletionProtectionPercent));
}

public sealed class ProfileRunResult
{
    public required SyncProfile Profile { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public TimeSpan Duration { get; set; }
    public List<SyncResult> Results { get; } = [];
    public List<TargetAnalysis> Skipped { get; } = [];
    public bool Cancelled { get; set; }

    public RunStatus Status =>
        Cancelled ? RunStatus.Cancelled
        : Results.Any(r => r.Errors.Count > 0) || (Results.Count == 0 && Skipped.Count > 0) ? RunStatus.Error
        : Skipped.Count > 0 ? RunStatus.Warning
        : RunStatus.Success;
}

/// <summary>Analysiert und synchronisiert ein Profil über alle aktivierten Ziele.</summary>
public static class ProfileRunner
{
    public static async Task<ProfileAnalysis> AnalyzeAsync(SyncProfile profile, IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var analysis = new ProfileAnalysis { Profile = profile };
        if (!Directory.Exists(profile.SourcePath))
        {
            analysis.SourceMissing = true;
            return analysis;
        }

        foreach (var target in profile.Targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Path)))
        {
            ct.ThrowIfCancellationRequested();
            var ta = new TargetAnalysis { Target = target };
            analysis.Targets.Add(ta);

            if (!IsTargetReachable(target.Path))
            {
                ta.Status = TargetStatus.Unavailable;
                ta.ValidationError = SyncValidationError.TargetUnavailable;
                continue;
            }

            try
            {
                ta.Plan = await SyncPlanner.CreateMirrorPlanAsync(profile.SourcePath, target.Path, profile.Filter, profile.CompareMode, progress, ct);
                ta.Status = TargetStatus.Ready;
            }
            catch (SyncValidationException ex)
            {
                ta.Status = TargetStatus.Error;
                ta.ValidationError = ex.Error;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ta.Status = TargetStatus.Error;
                ta.ErrorMessage = ex.Message;
            }
        }
        return analysis;
    }

    public static async Task<ProfileRunResult> ExecuteAsync(ProfileAnalysis analysis, SyncExecutorOptions options,
        IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        var run = new ProfileRunResult { Profile = analysis.Profile, StartedAt = started };
        run.Skipped.AddRange(analysis.Targets.Where(t => t.Status != TargetStatus.Ready));

        var executor = new SyncExecutor(options);
        var ready = analysis.ReadyTargets.ToList();

        if (analysis.Profile.MultiTargetMode == MultiTargetMode.Parallel && ready.Count > 1)
        {
            var results = await Task.WhenAll(ready.Select(t => executor.ExecuteAsync(t.Plan!, progress, ct)));
            run.Results.AddRange(results);
        }
        else
        {
            foreach (var t in ready)
            {
                if (ct.IsCancellationRequested) break;
                run.Results.Add(await executor.ExecuteAsync(t.Plan!, progress, ct));
            }
        }

        run.Cancelled = ct.IsCancellationRequested || run.Results.Any(r => r.Cancelled);
        run.Duration = DateTimeOffset.Now - started;
        return run;
    }

    /// <summary>
    /// Ziel erreichbar, wenn das Laufwerk bzw. die Netzwerkfreigabe existiert. Der Zielordner selbst
    /// darf fehlen – er wird angelegt. So wird ein ausgestecktes USB-Laufwerk übersprungen statt
    /// versehentlich auf ein anderes Laufwerk mit gleichem Buchstaben geschrieben … (Volume-ID folgt in Etappe 4).
    /// </summary>
    public static bool IsTargetReachable(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return !string.IsNullOrEmpty(root) && Directory.Exists(root);
        }
        catch
        {
            return false;
        }
    }
}
