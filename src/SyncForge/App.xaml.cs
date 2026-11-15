using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SyncForge.Core.History;
using SyncForge.Core.Storage;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        Services = new ServiceCollection()
            .AddSingleton<SettingsService>()
            .AddSingleton<ThemeService>()
            .AddSingleton(_ => new ProfileStore(SettingsService.DataDirectory))
            .AddSingleton(_ => new HistoryStore(Path.Combine(SettingsService.DataDirectory, "history.db")))
            .AddTransient<HistoryViewModel>()
            .AddTransient<RunDetailViewModel>()
            .AddTransient<SettingsViewModel>()
            .AddTransient<ProfilesViewModel>()
            .AddTransient<ProfileEditorViewModel>()
            .AddTransient<SyncRunViewModel>()
            .BuildServiceProvider();

        // Sprache muss gesetzt sein, bevor XAML-Ressourcen geladen werden.
        var settings = Services.GetRequiredService<SettingsService>();
        settings.Load();
        Localization.ApplyLanguage(settings.Current.Language);
        Services.GetRequiredService<ProfileStore>().Load();
        InitializeHistory(settings);

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

    /// <summary>Legt die Historien-Datenbank an und entfernt Läufe außerhalb der Aufbewahrungsdauer.</summary>
    private static void InitializeHistory(SettingsService settings)
    {
        try
        {
            var history = Services.GetRequiredService<HistoryStore>();
            history.Initialize();
            history.DeleteOlderThan(DateTimeOffset.Now.AddDays(-Math.Max(1, settings.Current.HistoryRetentionDays)));
        }
        catch (Exception ex)
        {
            // Eine defekte Historie darf den Start nicht verhindern.
            LogCrash(ex);
        }
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
