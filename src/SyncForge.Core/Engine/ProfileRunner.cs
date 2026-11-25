using SyncForge.Core.Models;
using SyncForge.Core.Network;

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
    /// <summary>Quelle, die in dieses Ziel synchronisiert wird (bei mehreren Quellen gibt es je Quelle einen Eintrag).</summary>
    public SyncSource? Source { get; init; }
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
    /// <summary>Quellen, die nicht erreichbar waren (die übrigen werden trotzdem synchronisiert).</summary>
    public List<SyncSource> MissingSources { get; } = [];
    /// <summary>Keine einzige Quelle erreichbar.</summary>
    public bool SourceMissing => Profile.Sources.Count == 0 || MissingSources.Count >= Profile.Sources.Count;

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
        : Skipped.Count > 0 || Results.Any(r => r.UnreadableFolders > 0) ? RunStatus.Warning
        : RunStatus.Success;
}

/// <summary>Analysiert und synchronisiert ein Profil über alle aktivierten Ziele.</summary>
public static class ProfileRunner
{
    public static async Task<ProfileAnalysis> AnalyzeAsync(SyncProfile profile, IProgress<SyncProgress>? progress = null, CancellationToken ct = default,
        string? stateDirectory = null, string conflictLabel = "Konflikt")
    {
        var analysis = new ProfileAnalysis { Profile = profile };
        // Netzwerkfreigaben mit gespeicherten Anmeldedaten vorher verbinden (sonst wären sie "nicht vorhanden").
        var sources = new List<SyncSource>();
        foreach (var source in profile.Sources.Where(s => !string.IsNullOrWhiteSpace(s.Path)))
        {
            await Task.Run(() => NetworkShares.EnsureAccess(source.Path), ct);
            if (Directory.Exists(source.Path)) sources.Add(source);
            else analysis.MissingSources.Add(source);
        }
        var enabledTargets = profile.Targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Path)).ToList();
        foreach (var missing in analysis.MissingSources)
        foreach (var target in enabledTargets)
            analysis.Targets.Add(new TargetAnalysis { Target = target, Source = missing, Status = TargetStatus.Error, ValidationError = SyncValidationError.SourceMissing });
        if (sources.Count == 0) return analysis;

        // Mehrere Quellen brauchen getrennte Unterordner, sonst würde eine Quelle die andere im Ziel löschen.
        // Der Editor legt sie fest; fehlen sie (z. B. von Hand bearbeitete Datei), werden sie hier eindeutig vergeben.
        if (profile.Sources.Count > 1)
        {
            foreach (var s in profile.Sources.Where(s => string.IsNullOrEmpty(s.TargetFolder)))
                s.TargetFolder = SyncProfile.SuggestTargetFolder(s.Path, profile.Sources.Select(x => x.TargetFolder));
        }

        foreach (var source in sources)
        foreach (var target in enabledTargets)
        {
            ct.ThrowIfCancellationRequested();
            var ta = new TargetAnalysis { Target = target, Source = source };
            analysis.Targets.Add(ta);

            // USB-/lokale Ziele über die Volume-ID finden (auch unter anderem Laufwerksbuchstaben).
            var path = VolumeResolver.Resolve(target.Path, target.VolumeSerial);
            if (path is not null)
            {
                var access = await Task.Run(() => NetworkShares.EnsureAccess(path), ct);
                if (access.Result is NetworkAccessResult.LogonFailure or NetworkAccessResult.CredentialConflict)
                {
                    ta.Status = TargetStatus.Unavailable;
                    ta.ValidationError = access.Result == NetworkAccessResult.LogonFailure
                        ? SyncValidationError.NetworkLogonFailed
                        : SyncValidationError.NetworkCredentialConflict;
                    ta.ErrorMessage = access.Message;
                    continue;
                }
            }
            if (path is null || !IsTargetReachable(path))
            {
                ta.Status = TargetStatus.Unavailable;
                ta.ValidationError = SyncValidationError.TargetUnavailable;
                continue;
            }
            ta.ResolvedPath = path;
            var targetRoot = source.TargetRootFor(path);

            try
            {
                ta.Plan = profile.Mode == SyncMode.TwoWay
                    ? await TwoWayPlanner.CreatePlanAsync(source.Path, targetRoot, profile.Filter, profile.CompareMode, profile.ConflictResolution,
                        stateDirectory is null ? null : Path.Combine(stateDirectory, StateFileName(profile, source, target)), conflictLabel, progress, ct)
                    : await SyncPlanner.CreateMirrorPlanAsync(source.Path, targetRoot, profile.Filter, profile.CompareMode, progress, ct);
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

    /// <summary>
    /// Datei mit dem Zwei-Wege-Stand je Quelle und Ziel. Eine Quelle ohne Unterordner (das bisherige Einzel-Quellen-Profil)
    /// behält ihren alten Namen, damit vorhandene Stände weiter gelten.
    /// </summary>
    public static string StateFileName(SyncProfile profile, SyncSource source, SyncTarget target) =>
        string.IsNullOrEmpty(source.TargetFolder) ? $"{profile.Id:N}_{target.Id:N}.json" : $"{profile.Id:N}_{target.Id:N}_{source.Id:N}.json";

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
