using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
#if DEBUG
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => LogCrash(new Exception("FirstChance", e.Exception));
#endif
        Services = new ServiceCollection()
            .AddSingleton<SettingsService>()
            .AddSingleton<ThemeService>()
            .AddTransient<SettingsViewModel>()
            .BuildServiceProvider();

        // Sprache muss gesetzt sein, bevor XAML-Ressourcen geladen werden.
        var settings = Services.GetRequiredService<SettingsService>();
        settings.Load();
        Localization.ApplyLanguage(settings.Current.Language);

        InitializeComponent();

        UnhandledException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogCrash(e.Exception);
    }

    /// <summary>
    /// Einige WinUI-Steuerelemente (z. B. ToggleSwitch "Ein/Aus") folgen der Systemsprache statt der App-Sprache.
    /// Die Texte werden als Ressourcen bereitgestellt und direkt an den Steuerelementen gesetzt
    /// (z. B. OnContent="{StaticResource ToggleOnText}"), da Toolkit-Styles implizite Styles überdecken.
    /// </summary>
    private void ApplyLocalizedControlDefaults()
    {
        Resources["ToggleOnText"] = Localization.Get("Toggle_On");
        Resources["ToggleOffText"] = Localization.Get("Toggle_Off");
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            File.AppendAllText(Path.Combine(SettingsService.DataDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Protokollierung darf selbst nie abstürzen.
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            ApplyLocalizedControlDefaults();
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }
}
