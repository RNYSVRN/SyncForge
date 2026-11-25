using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace SyncForge;

/// <summary>
/// Einstiegspunkt: Es läuft immer nur eine Instanz. Ein weiterer Start (Startmenü, Benachrichtigung)
/// wird an die laufende Instanz weitergeleitet, die dann ihr Fenster zeigt.
/// </summary>
public static class Program
{
    private const string InstanceKey = "SyncForge.Main";

    /// <summary>Vom Deinstallationsprogramm aufgerufen: Spuren in Windows entfernen und sofort beenden.</summary>
    public const string UninstallCleanupArgument = "--uninstall-cleanup";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (args.Contains(UninstallCleanupArgument, StringComparer.OrdinalIgnoreCase))
        {
            UninstallCleanup();
            return 0;
        }

        // Befehlszeile (--run, --run-all, --list, --help): ohne Fenster.
        var command = Core.Cli.CommandLineOptions.Parse(args);
        if (command.IsCommand)
        {
            // Läuft SyncForge schon, übernimmt das geöffnete Programm die Synchronisierung
            // (Fortschritt, Benachrichtigung, keine zwei Prozesse für dasselbe Profil).
            var running = AppInstance.GetInstances().FirstOrDefault(i => i.Key == InstanceKey);
            if (running is not null && command.Kind is Core.Cli.CommandKind.Run or Core.Cli.CommandKind.RunAll)
            {
                var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
                Task.Run(() => running.RedirectActivationToAsync(activation).AsTask()).Wait();
                Services.CommandLineRunner.ReportHandedOver();
                return 0;
            }
            return Services.CommandLineRunner.Run(command);
        }

        var current = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!current.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            // Weiterleiten auf einem eigenen Thread (blockierendes Warten im STA-Thread vermeiden).
            Task.Run(() => current.RedirectActivationToAsync(activation).AsTask()).Wait();
            return 0;
        }

        current.Activated += (_, e) => App.OnRedirectedActivation(e);

        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>Benachrichtigungs-Registrierung und Autostart-Eintrag entfernen (Daten bleiben unberührt).</summary>
    private static void UninstallCleanup()
    {
        try { Microsoft.Windows.AppNotifications.AppNotificationManager.Default.UnregisterAll(); } catch { /* best effort */ }
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue("SyncForge", throwOnMissingValue: false);
        }
        catch { /* best effort */ }
    }
}
