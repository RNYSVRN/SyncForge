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

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

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
}
