using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SyncForge.Core.Models;
using SyncForge.Helpers;

namespace SyncForge.Services;

/// <summary>Windows-Benachrichtigungen nach Läufen (je nach Einstellung) inkl. "Details anzeigen".</summary>
public sealed class NotificationService
{
    private readonly SettingsService _settings;
    private bool _registered;

    public NotificationService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>Klick auf eine Benachrichtigung: Lauf-Id (oder null = nur App öffnen).</summary>
    public event EventHandler<long?>? Invoked;

    public void Register()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, e) => Invoked?.Invoke(this, ParseRunId(e.Arguments));
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
    }

    public void Unregister()
    {
        if (!_registered) return;
        try { AppNotificationManager.Default.Unregister(); } catch { /* beim Beenden egal */ }
    }

    public static long? ParseRunId(IDictionary<string, string> arguments) =>
        arguments.TryGetValue("runId", out var id) && long.TryParse(id, out var runId) ? runId : null;

    /// <summary>Zeigt eine Benachrichtigung, sofern für diesen Status eingeschaltet.</summary>
    public void OnRunCompleted(RunCompletedEventArgs e)
    {
        if (!_registered) return;
        var status = e.Run.Status;
        var wanted = status switch
        {
            RunStatus.Success => _settings.Current.NotifyOnSuccess,
            RunStatus.Warning or RunStatus.Error => _settings.Current.NotifyOnError,
            _ => false
        };
        if (!wanted) return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "openRun")
                .AddText(Format.Text("Notify_Title", e.Profile.Name, Format.StatusName(status)))
                .AddText(e.Summary);
            if (e.HistoryId is { } id)
            {
                builder.AddArgument("runId", id.ToString());
                builder.AddButton(new AppNotificationButton(Localization.Get("Notify_ShowDetails"))
                    .AddArgument("action", "openRun")
                    .AddArgument("runId", id.ToString()));
            }
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
    }
}
