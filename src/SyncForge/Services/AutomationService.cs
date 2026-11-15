using System.Collections.Concurrent;
using Microsoft.Windows.System.Power;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Core.Scheduling;
using SyncForge.Core.Storage;
using Windows.Networking.Connectivity;

namespace SyncForge.Services;

/// <summary>
/// Startet Läufe automatisch: Zeitpläne, Echtzeit-Überwachung der Quelle, App-Start/Anmeldung und
/// angeschlossene Ziel-Laufwerke. Läuft im Hintergrund, solange SyncForge aktiv ist (auch im Infobereich).
/// </summary>
public sealed class AutomationService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DriveDelay = TimeSpan.FromSeconds(5);

    private readonly ProfileStore _profiles;
    private readonly SyncCoordinator _coordinator;
    private readonly SettingsService _settings;
    private readonly ConcurrentDictionary<Guid, ProfileWatcher> _watchers = new();
    /// <summary>Profile mit zurückgestellten automatischen Läufen (Akku, Laufwerk belegt …) und ihrem Auslöser.</summary>
    private readonly ConcurrentDictionary<Guid, RunTrigger> _pending = new();
    private readonly Lock _scheduleLock = new();
    private Timer? _timer;
    private bool _disposed;

    public AutomationService(ProfileStore profiles, SyncCoordinator coordinator, SettingsService settings)
    {
        _profiles = profiles;
        _coordinator = coordinator;
        _settings = settings;
    }

    /// <summary>Nächster geplanter Lauf hat sich geändert (für Übersicht/Tooltip).</summary>
    public event EventHandler? ScheduleChanged;

    public void Start()
    {
        _profiles.Changed += (_, _) => RefreshWatchers();
        _coordinator.RunCompleted += OnRunCompleted;
        RefreshWatchers();
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TickInterval);

        // Läufe "beim Start/bei der Anmeldung" leicht verzögert, damit Netzlaufwerke verbunden sind.
        _ = Task.Delay(StartupDelay).ContinueWith(_ =>
        {
            foreach (var p in _profiles.Profiles.Where(p => p.Automation.RunAtStartup)) Trigger(p.Id, RunTrigger.Event);
        }, TaskScheduler.Default);
    }

    /// <summary>Frühester nächster Zeitplan-Termin über alle Profile.</summary>
    public (SyncProfile Profile, DateTimeOffset At)? GetNextRun()
    {
        (SyncProfile, DateTimeOffset)? best = null;
        foreach (var p in _profiles.Profiles)
        {
            if (p.Automation.Schedule == ScheduleKind.None) continue;
            var next = ScheduleCalculator.NextAfter(p.Automation, p.Automation.ScheduleAnchor ?? DateTimeOffset.Now);
            if (next is { } n && (best is null || n < best.Value.Item2)) best = (p, n);
        }
        return best;
    }

    /// <summary>Wird vom Infobereich-Fenster gemeldet, wenn ein Laufwerk angeschlossen wurde.</summary>
    public void OnDriveArrived(char letter)
    {
        var root = $"{char.ToUpperInvariant(letter)}:\\";
        var affected = _profiles.Profiles.Where(p =>
            p.Automation.RunWhenTargetConnected &&
            p.Targets.Any(t => t.Enabled && string.Equals(Path.GetPathRoot(t.Path), root, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (affected.Count == 0) return;
        _ = Task.Delay(DriveDelay).ContinueWith(_ =>
        {
            foreach (var p in affected) Trigger(p.Id, RunTrigger.Event);
        }, TaskScheduler.Default);
    }

    /// <summary>Alle Profile sofort synchronisieren (Infobereich-Menü).</summary>
    public void RunAll()
    {
        foreach (var p in _profiles.Profiles) _ = _coordinator.RunAsync(p.Id, RunTrigger.Manual);
    }

    private void Tick()
    {
        if (_disposed) return;
        try
        {
            var now = DateTimeOffset.Now;
            foreach (var p in _profiles.Profiles)
            {
                if (p.Automation.Schedule == ScheduleKind.None) continue;
                lock (_scheduleLock)
                {
                    var stored = _profiles.Get(p.Id);
                    if (stored is null) continue;
                    if (stored.Automation.ScheduleAnchor is null)
                    {
                        // Zeitplan neu aktiviert: ab jetzt zählen.
                        stored.Automation.ScheduleAnchor = now;
                        _profiles.Save(stored);
                        continue;
                    }
                    var (due, _) = ScheduleCalculator.Evaluate(stored.Automation, stored.Automation.ScheduleAnchor.Value, now);
                    if (!due) continue;
                    stored.Automation.ScheduleAnchor = now;
                    _profiles.Save(stored);
                }
                Trigger(p.Id, RunTrigger.Schedule);
            }

            // Zurückgestellte Läufe erneut versuchen.
            foreach (var (id, trigger) in _pending.ToArray())
            {
                if (_pending.TryRemove(id, out _)) Trigger(id, trigger);
            }
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
    }

    /// <summary>Startet einen automatischen Lauf oder stellt ihn zurück, wenn gerade nicht möglich.</summary>
    private void Trigger(Guid profileId, RunTrigger trigger)
    {
        if (_disposed) return;
        var profile = _profiles.Get(profileId);
        if (profile is null) return;

        if (_coordinator.IsRunning(profileId) || !CanRunNow(profile))
        {
            _pending[profileId] = trigger;
            return;
        }
        _ = _coordinator.RunAsync(profileId, trigger);
    }

    private bool CanRunNow(SyncProfile profile)
    {
        try
        {
            if (_settings.Current.PauseOnBattery && PowerManager.PowerSourceKind == PowerSourceKind.DC) return false;
            if (_settings.Current.PauseOnMeteredConnection && profile.Targets.Any(t => t.Enabled && IsNetworkPath(t.Path)))
            {
                var cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
                if (cost is not null && cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable) return false;
            }
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
        return true;
    }

    private static bool IsNetworkPath(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    private void OnRunCompleted(object? sender, RunCompletedEventArgs e)
    {
        // Änderungen während des Laufs: danach erneut synchronisieren.
        if (_watchers.TryGetValue(e.Profile.Id, out var watcher) && watcher.ChangedDuringRun)
        {
            watcher.ChangedDuringRun = false;
            watcher.Restart();
        }
        ScheduleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshWatchers()
    {
        if (_disposed) return;
        var wanted = _profiles.Profiles
            // Netzwerkquelle mit gespeicherten Anmeldedaten: erst verbinden, sonst gäbe es sie scheinbar nicht.
            .Where(p => p.Automation.WatchForChanges && Core.Network.NetworkShares.EnsureAccess(p.SourcePath).Success && Directory.Exists(p.SourcePath))
            .ToDictionary(p => p.Id);

        foreach (var (id, w) in _watchers.ToArray())
        {
            if (!wanted.TryGetValue(id, out var p) || !w.Matches(p))
            {
                w.Dispose();
                _watchers.TryRemove(id, out _);
            }
        }
        foreach (var p in wanted.Values)
        {
            if (_watchers.ContainsKey(p.Id)) continue;
            try
            {
                _watchers[p.Id] = new ProfileWatcher(p, () => OnWatcherFired(p.Id));
            }
            catch (Exception ex)
            {
                App.LogError(ex);
            }
        }
        ScheduleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatcherFired(Guid profileId)
    {
        if (_coordinator.IsRunning(profileId))
        {
            if (_watchers.TryGetValue(profileId, out var w)) w.ChangedDuringRun = true;
            return;
        }
        Trigger(profileId, RunTrigger.Watcher);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        foreach (var w in _watchers.Values) w.Dispose();
        _watchers.Clear();
    }

    /// <summary>Überwacht einen Quellordner und meldet sich erst, wenn eine Weile Ruhe ist (Entprellung).</summary>
    private sealed class ProfileWatcher : IDisposable
    {
        private readonly FileSystemWatcher _fsw;
        private readonly Timer _debounce;
        private readonly TimeSpan _delay;
        private readonly string _signature;

        public bool ChangedDuringRun { get; set; }

        public ProfileWatcher(SyncProfile profile, Action fired)
        {
            _signature = Signature(profile);
            _delay = TimeSpan.FromSeconds(Math.Clamp(profile.Automation.WatchDelaySeconds, 2, 3600));
            _debounce = new Timer(_ => fired(), null, Timeout.Infinite, Timeout.Infinite);
            _fsw = new FileSystemWatcher(profile.SourcePath)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _fsw.Changed += (_, _) => Restart();
            _fsw.Created += (_, _) => Restart();
            _fsw.Deleted += (_, _) => Restart();
            _fsw.Renamed += (_, _) => Restart();
            _fsw.Error += (_, _) => Restart(); // Pufferüberlauf: sicherheitshalber synchronisieren
            _fsw.EnableRaisingEvents = true;
        }

        public bool Matches(SyncProfile p) => Signature(p) == _signature;

        public void Restart() => _debounce.Change(_delay, Timeout.InfiniteTimeSpan);

        private static string Signature(SyncProfile p) => $"{p.SourcePath}|{p.Automation.WatchDelaySeconds}";

        public void Dispose()
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
            _debounce.Dispose();
        }
    }
}
