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
    /// <summary>Tatsächlich verwendeter Pfad (bei Volume-ID ggf. mit anderem Laufwerksbuchstaben).</summary>
    public string? ResolvedPath { get; set; }
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
    public static async Task<ProfileAnalysis> AnalyzeAsync(SyncProfile profile, IProgress<SyncProgress>? progress = null, CancellationToken ct = default,
        string? stateDirectory = null, string conflictLabel = "Konflikt")
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

            // USB-/lokale Ziele über die Volume-ID finden (auch unter anderem Laufwerksbuchstaben).
            var path = VolumeResolver.Resolve(target.Path, target.VolumeSerial);
            if (path is null || !IsTargetReachable(path))
            {
                ta.Status = TargetStatus.Unavailable;
                ta.ValidationError = SyncValidationError.TargetUnavailable;
                continue;
            }
            ta.ResolvedPath = path;

            try
            {
                ta.Plan = profile.Mode == SyncMode.TwoWay
                    ? await TwoWayPlanner.CreatePlanAsync(profile.SourcePath, path, profile.Filter, profile.CompareMode, profile.ConflictResolution,
                        stateDirectory is null ? null : Path.Combine(stateDirectory, $"{profile.Id:N}_{target.Id:N}.json"), conflictLabel, progress, ct)
                    : await SyncPlanner.CreateMirrorPlanAsync(profile.SourcePath, path, profile.Filter, profile.CompareMode, progress, ct);
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

        async Task<SyncResult> RunOne(SyncPlan plan)
        {
            var result = await executor.ExecuteAsync(plan, progress, ct);
            if (plan.Mode == SyncMode.TwoWay && !result.Cancelled)
            {
                try { await Task.Run(() => TwoWayPlanner.UpdateState(plan), CancellationToken.None); }
                catch (Exception ex) { result.Errors.Add(new SyncError(string.Empty, ex.Message)); }
            }
            return result;
        }

        if (analysis.Profile.MultiTargetMode == MultiTargetMode.Parallel && ready.Count > 1)
        {
            run.Results.AddRange(await Task.WhenAll(ready.Select(t => RunOne(t.Plan!))));
        }
        else
        {
            foreach (var t in ready)
            {
                if (ct.IsCancellationRequested) break;
                run.Results.Add(await RunOne(t.Plan!));
            }
        }

        run.Cancelled = ct.IsCancellationRequested || run.Results.Any(r => r.Cancelled);
        run.Duration = DateTimeOffset.Now - started;
        return run;
    }

    /// <summary>
    /// Ziel erreichbar, wenn das Laufwerk bzw. die Netzwerkfreigabe existiert. Der Zielordner selbst
    /// darf fehlen – er wird angelegt. So wird ein ausgestecktes USB-Laufwerk übersprungen statt
    /// versehentlich auf ein anderes Laufwerk geschrieben (siehe auch <see cref="VolumeResolver"/>).
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
