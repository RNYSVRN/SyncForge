using Microsoft.Win32;

namespace SyncForge.Services;

/// <summary>
/// "Mit Windows starten" für die unpaketierte App über HKCU\...\Run (benötigt keine Administratorrechte).
/// Im MSIX-Paket übernimmt das später eine StartupTask.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SyncForge";
    public const string BackgroundArgument = "--background";

    public static void Apply(bool enabled)
    {
        try
        {
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
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value.Contains("SyncForge", StringComparison.OrdinalIgnoreCase);
    }

    public static bool StartedInBackground => Environment.GetCommandLineArgs().Contains(BackgroundArgument, StringComparer.OrdinalIgnoreCase);
}
