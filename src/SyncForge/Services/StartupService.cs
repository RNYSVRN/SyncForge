using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel;

namespace SyncForge.Services;

/// <summary>
/// "Mit Windows starten": unpaketiert über HKCU\...\Run (ohne Administratorrechte),
/// als MSIX-Paket über die im Manifest deklarierte StartupTask.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SyncForge";
    private const string StartupTaskId = "SyncForgeStartup";
    public const string BackgroundArgument = "--background";

    /// <summary>True, wenn SyncForge als MSIX-Paket läuft.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    public static void Apply(bool enabled)
    {
        try
        {
            if (IsPackaged)
            {
                _ = ApplyPackagedAsync(enabled);
                return;
            }
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SyncForge.exe");
                key.SetValue(ValueName, $"\"{exe}\" {BackgroundArgument}");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            if (IsPackaged)
            {
                var state = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult().State;
                return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Contains("SyncForge", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Gestartet durch Autostart (Kommandozeile bzw. StartupTask-Aktivierung): ohne Fenster.</summary>
    public static bool StartedInBackground =>
        Environment.GetCommandLineArgs().Contains(BackgroundArgument, StringComparer.OrdinalIgnoreCase)
        || Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;

    private static async Task ApplyPackagedAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (enabled) await task.RequestEnableAsync(); // Windows fragt ggf. selbst nach
            else task.Disable();
        }
        catch (Exception ex)
        {
            App.LogError(ex);
        }
    }

    private static bool DetectPackaged()
    {
        try
        {
            return Package.Current is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
