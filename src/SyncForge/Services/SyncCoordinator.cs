using System.Collections.Concurrent;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Helpers;

namespace SyncForge.Services;

public sealed class RunCompletedEventArgs(SyncProfile profile, ProfileRunResult run, RunTrigger trigger, long? historyId, string summary) : EventArgs
{
    public SyncProfile Profile { get; } = profile;
    public ProfileRunResult Run { get; } = run;
    public RunTrigger Trigger { get; } = trigger;
    public long? HistoryId { get; } = historyId;
    public string Summary { get; } = summary;
}

/// <summary>
/// Zentrale Stelle für alle Läufe (manuell und automatisch): verhindert parallele Läufe desselben Profils,
/// speichert Historie und letzten Status und meldet abgeschlossene Läufe (z. B. für Benachrichtigungen).
/// Threadsicher; Ereignisse kommen ggf. von Hintergrund-Threads.
/// </summary>
public sealed class SyncCoordinator
{
    private readonly ProfileStore _profiles;
    private readonly HistoryStore _history;
    private readonly SettingsService _settings;
    private readonly ConcurrentDictionary<Guid, RunActivity> _running = new();
    /// <summary>Prozessübergreifende Sperre je Profil (Fenster und Befehlszeile laufen in getrennten Prozessen).</summary>
    private readonly ConcurrentDictionary<Guid, Semaphore> _locks = new();
    private long _lastActivityEventMs = -1000;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    public SyncCoordinator(ProfileStore profiles, HistoryStore history, SettingsService settings)
    {
        _profiles = profiles;
        _history = history;
        _settings = settings;
    }

    public event EventHandler<Guid>? RunStarted;
    public event EventHandler<RunCompletedEventArgs>? RunCompleted;

    /// <summary>Laufende Synchronisierungen oder ihr Fortschritt haben sich geändert (beliebiger Thread, gedrosselt).</summary>
    public event EventHandler? ActivityChanged;

    public bool IsRunning(Guid profileId) => _running.ContainsKey(profileId);
    public int RunningCount => _running.Count;

    /// <summary>Alle laufenden Synchronisierungen, älteste zuerst.</summary>
    public IReadOnlyList<RunActivity> Activities => _running.Values.OrderBy(a => a.StartedAt).ToList();

    /// <summary>Fortschritt eines laufenden Profils an die zentrale Anzeige melden (synchron, ohne UI-Thread).</summary>
    public IProgress<SyncProgress> ProgressFor(Guid profileId) => new ActivityProgress(this, profileId);

    public void Cancel(Guid profileId)
    {
        if (_running.TryGetValue(profileId, out var activity)) activity.Cancel();
        RaiseActivityChanged(force: true);
    }

    private void RaiseActivityChanged(bool force)
    {
        var now = _clock.ElapsedMilliseconds;
        var last = Interlocked.Read(ref _lastActivityEventMs);
        if (!force && (now - last < 250 || Interlocked.CompareExchange(ref _lastActivityEventMs, now, last) != last)) return;
        if (force) Interlocked.Exchange(ref _lastActivityEventMs, now);
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ActivityProgress(SyncCoordinator owner, Guid profileId) : IProgress<SyncProgress>
    {
        public void Report(SyncProgress value)
        {
            if (!owner._running.TryGetValue(profileId, out var activity)) return;
            activity.Report(value);
            owner.RaiseActivityChanged(force: false);
        }
    }

    /// <summary>Reserviert ein Profil für einen manuellen Lauf (UI). False, wenn es gerade läuft.</summary>
    public bool TryBegin(Guid profileId, CancellationTokenSource cts)
    {
        var name = _profiles.Get(profileId)?.Name ?? string.Empty;
        // Läuft das Profil schon in einem anderen SyncForge-Prozess (z. B. von der Befehlszeile)?
        var systemLock = new Semaphore(1, 1, $@"Local\SyncForge.Run.{profileId:N}");
        if (!systemLock.WaitOne(0))
        {
            systemLock.Dispose();
            return false;
        }
        if (!_running.TryAdd(profileId, new RunActivity(profileId, name, cts)))
        {
            systemLock.Release();
            systemLock.Dispose();
            return false;
        }
        _locks[profileId] = systemLock;
        RunStarted?.Invoke(this, profileId);
        RaiseActivityChanged(force: true);
        return true;
    }

    public void End(Guid profileId)
    {
        _running.TryRemove(profileId, out _);
        if (_locks.TryRemove(profileId, out var systemLock))
        {
            systemLock.Release();
            systemLock.Dispose();
        }
        RaiseActivityChanged(force: true);
    }

    public void CancelAll()
    {
        foreach (var activity in _running.Values) activity.Cancel();
    }

    /// <summary>Zwei-Wege-Stände (gemeinsamer Stand nach dem letzten Lauf je Profil und Ziel).</summary>
    public static string StateDirectory => Path.Combine(SettingsService.DataDirectory, "state");

    public static string ConflictLabel => Localization.Get("Conflict_Label");

    public SyncExecutorOptions BuildOptions(SyncProfile profile) => new()
    {
        MaxParallelCopies = _settings.Current.MaxParallelCopies,
        BandwidthLimitKBps = profile.BandwidthLimitKBps ?? _settings.Current.BandwidthLimitKBps,
        LowPriorityIo = profile.LowPriorityIo,
        VerifyAfterCopy = profile.VerifyAfterCopy,
        DeleteToRecycleBin = profile.DeleteToRecycleBin,
        UseShadowCopy = profile.UseShadowCopy,
        KeepVersions = profile.Mode == SyncMode.Versioned,
        VersionsToKeep = profile.VersionsToKeep,
        VersionMaxAgeDays = profile.VersionMaxAgeDays
    };

    /// <summary>Analyse mit den App-Vorgaben (Zwei-Wege-Stand, lokalisierte Konfliktbezeichnung).</summary>
    public static Task<ProfileAnalysis> AnalyzeAsync(SyncProfile profile, IProgress<SyncProgress>? progress, CancellationToken ct) =>
        ProfileRunner.AnalyzeAsync(profile, progress, ct, StateDirectory, ConflictLabel);

    /// <summary>
    /// Automatischer Lauf ohne Oberfläche. Schlägt der Löschschutz an, wird das betroffene Ziel
    /// nicht synchronisiert, sondern als übersprungen protokolliert. Gibt null zurück, wenn das Profil
    /// fehlt oder bereits läuft.
    /// </summary>
    public async Task<RunCompletedEventArgs?> RunAsync(Guid profileId, RunTrigger trigger)
    {
        var profile = _profiles.Get(profileId);
        if (profile is null) return null;
        var cts = new CancellationTokenSource();
        if (!TryBegin(profileId, cts)) return null;

        try
        {
            var progress = ProgressFor(profileId);
            var analysis = await AnalyzeAsync(profile, progress, cts.Token);
            // Fehlende Quellen stehen bereits als Einträge "Quelle fehlt" in der Analyse.
            // Überwachung ohne tatsächliche Änderung (z. B. das Echo eigener Schreibvorgänge im Zwei-Wege-Modus):
            // nichts tun und nichts protokollieren.
            if (trigger == RunTrigger.Watcher && !analysis.SourceMissing
                && analysis.Targets.All(t => t.Status == TargetStatus.Ready && t.Plan is { HasChanges: false } && t.Plan.ConflictCount == 0))
            {
                return null;
            }

            foreach (var warned in analysis.DeletionWarnings.ToList())
            {
                warned.Status = TargetStatus.Error;
                warned.ValidationError = SyncValidationError.DeletionProtection;
            }

            var run = await ProfileRunner.ExecuteAsync(analysis, BuildOptions(profile), progress, cts.Token);
            return Record(run, trigger);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return null;
        }
        finally
        {
            End(profileId);
        }
    }

    /// <summary>Speichert einen Lauf (Historie + letzter Status) und meldet ihn.</summary>
    public RunCompletedEventArgs Record(ProfileRunResult run, RunTrigger trigger)
    {
        var summary = BuildSummary(run);
        long? historyId = null;
        try
        {
            historyId = _history.AddRun(run, trigger);
            _history.DeleteOlderThan(DateTimeOffset.Now.AddDays(-Math.Max(1, _settings.Current.HistoryRetentionDays)));
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }

        var stored = _profiles.Get(run.Profile.Id);
        if (stored is not null)
        {
            stored.LastRunAt = DateTimeOffset.Now;
            stored.LastRunStatus = run.Status;
            stored.LastRunSummary = summary;
            _profiles.Save(stored);
        }

        var args = new RunCompletedEventArgs(stored ?? run.Profile, run, trigger, historyId, summary);
        RunCompleted?.Invoke(this, args);
        return args;
    }

    public static string BuildSummary(ProfileRunResult run)
    {
        var copied = run.Results.Sum(r => r.Copied);
        var updated = run.Results.Sum(r => r.Updated);
        var deleted = run.Results.Sum(r => r.DeletedFiles);
        var bytes = run.Results.Sum(r => r.BytesCopied);
        var summary = copied + updated + deleted == 0 && run.Status == RunStatus.Success
            ? Localization.Get("Run_UpToDate")
            : Format.Text("Run_TotalSummary", Format.Number(copied), Format.Number(updated), Format.Number(deleted), Format.Bytes(bytes), Format.Duration(run.Duration));
        var cloudSkipped = run.Results.Sum(r => r.SkippedCloudFiles);
        if (cloudSkipped > 0) summary += " " + Format.Text("Run_CloudSkipped", Format.Number(cloudSkipped));
        var shadowCopied = run.Results.Sum(r => r.ShadowCopiedFiles);
        if (shadowCopied > 0) summary += " " + Format.Text("Run_ShadowCopied", Format.Number(shadowCopied));
        var unreadable = run.Results.Sum(r => r.UnreadableFolders);
        if (unreadable > 0) summary += " " + Format.Text("Run_Unreadable", Format.Number(unreadable));
        foreach (var lost in run.Results.Select(r => r.LostRoot).OfType<string>()) summary += " " + Format.Text("Run_RootLost", lost) + ".";
        var conflicts = run.Results.Sum(r => r.Conflicts);
        if (conflicts > 0) summary += " " + Format.Text("Run_Conflicts", Format.Number(conflicts));
        var errors = run.Results.Sum(r => r.Errors.Count);
        if (errors > 0) summary += " " + Format.Text("History_RunErrors", Format.Number(errors)) + ".";
        if (run.Skipped.Count > 0)
        {
            var protectedCount = run.Skipped.Count(s => s.ValidationError == SyncValidationError.DeletionProtection);
            summary += " " + (protectedCount > 0
                ? Format.Text("Run_SkippedProtection", Format.Number(protectedCount))
                : Format.Text("Run_SkippedTargets", Format.Number(run.Skipped.Count)));
        }
        return summary;
    }
}
