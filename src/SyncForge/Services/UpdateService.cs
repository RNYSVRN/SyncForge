using Microsoft.UI.Dispatching;
using SyncForge.Core.Updates;
using SyncForge.Helpers;

namespace SyncForge.Services;

/// <summary>
/// Optionale Update-Prüfung: höchstens einmal am Tag eine Anfrage an GitHub, nur wenn eingeschaltet.
/// Gefundene Versionen werden gemerkt (Hinweis in der Übersicht) und einmal per Benachrichtigung gemeldet.
/// Heruntergeladen wird nichts.
/// </summary>
public sealed class UpdateService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private DispatcherQueueTimer? _timer;

    public UpdateService(SettingsService settings, NotificationService notifications)
    {
        _settings = settings;
        _notifications = notifications;
    }

    /// <summary>Status hat sich geändert (immer auf dem UI-Thread).</summary>
    public event EventHandler? Changed;

    public bool IsChecking { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Neuere Version als die laufende (aus der letzten erfolgreichen Prüfung).</summary>
    public (string Tag, Version Version, string Url)? Available
    {
        get
        {
            var s = _settings.Current;
            if (s.AvailableUpdateTag is not { } tag || s.AvailableUpdateUrl is not { } url) return null;
            if (!UpdateChecker.TryParseVersion(tag, out var version) || !UpdateChecker.IsNewer(version, AppInfo.Version)) return null;
            return (tag, version, url);
        }
    }

    /// <summary>Hinweis in der Übersicht zeigen (eingeschaltet, neuere Version, nicht weggeklickt).</summary>
    public bool ShowHint => _settings.Current.CheckForUpdates && Available is { } a && a.Tag != _settings.Current.DismissedUpdateTag;

    public void Start(DispatcherQueue queue)
    {
        // Stündlich prüfen, ob die letzte Abfrage einen Tag her ist (übersteht auch Standby und lange Laufzeiten).
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromHours(1);
        _timer.Tick += (_, _) => _ = CheckIfDueAsync();
        _timer.Start();
        // Kurz nach dem Start, damit der Programmstart nicht auf das Netzwerk wartet.
        queue.TryEnqueue(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            await CheckIfDueAsync();
        });
    }

    private Task CheckIfDueAsync()
    {
        var s = _settings.Current;
        var due = s.CheckForUpdates && (s.LastUpdateCheck is not { } last || DateTimeOffset.Now - last >= Interval);
        return due ? CheckAsync(background: true) : Task.CompletedTask;
    }

    /// <summary>"Jetzt suchen" in den Einstellungen.</summary>
    public Task CheckNowAsync() => CheckAsync(background: false);

    private async Task CheckAsync(bool background)
    {
        if (IsChecking) return;
        IsChecking = true;
        LastError = null;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            var release = await UpdateChecker.GetLatestReleaseAsync(Http, AppInfo.GitHubRepository, AppInfo.Version);
            var s = _settings.Current;
            s.LastUpdateCheck = DateTimeOffset.Now;
            var newer = release is not null && UpdateChecker.IsNewer(release.Version, AppInfo.Version);
            s.AvailableUpdateTag = newer ? release!.Tag : null;
            s.AvailableUpdateUrl = newer ? release!.PageUrl : null;

            // Im Hintergrund gefunden: einmal je Version benachrichtigen.
            if (newer && background && s.NotifiedUpdateTag != release!.Tag)
            {
                s.NotifiedUpdateTag = release.Tag;
                _notifications.ShowUpdateAvailable(release.Version.ToString(3), release.PageUrl);
            }
            _settings.Save();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            LastError = ex is TaskCanceledException ? Localization.Get("Update_Timeout") : ex.Message;
        }
        finally
        {
            IsChecking = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Ein-/Ausschalten. Beim Einschalten wird gleich gesucht.</summary>
    public void SetEnabled(bool enabled)
    {
        var s = _settings.Current;
        if (s.CheckForUpdates == enabled) return;
        s.CheckForUpdates = enabled;
        if (!enabled) { s.AvailableUpdateTag = null; s.AvailableUpdateUrl = null; }
        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
        if (enabled) _ = CheckAsync(background: false);
    }

    /// <summary>Hinweis in der Übersicht für diese Version schließen.</summary>
    public void Dismiss()
    {
        if (Available is not { } a) return;
        _settings.Current.DismissedUpdateTag = a.Tag;
        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
