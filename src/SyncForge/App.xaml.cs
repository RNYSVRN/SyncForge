using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using SyncForge.Core.History;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;
using SyncForge.ViewModels;
using SyncForge.Views;

namespace SyncForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;
    public static DispatcherQueue UiQueue { get; private set; } = null!;

    /// <summary>True, sobald SyncForge wirklich beendet wird (nicht nur in den Infobereich).</summary>
    public static bool IsExiting { get; private set; }

    private static App? _current;
    private TrayIcon? _tray;

    public App()
    {
        _current = this;
        Services = new ServiceCollection()
            .AddSingleton<SettingsService>()
            .AddSingleton<ThemeService>()
            .AddSingleton(_ => new ProfileStore(SettingsService.DataDirectory))
            .AddSingleton(_ => new HistoryStore(Path.Combine(SettingsService.DataDirectory, "history.db")))
            .AddSingleton<SyncCoordinator>()
            .AddSingleton<AutomationService>()
            .AddSingleton<NotificationService>()
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

        // Das Beenden steuert SyncForge selbst (Fenster schließen = in den Infobereich).
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        UnhandledException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogError(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            UiQueue = DispatcherQueue.GetForCurrentThread();
            ApplyLocalizedControlDefaults();
            var settings = Services.GetRequiredService<SettingsService>();

            // Benachrichtigungen vor dem ersten Fenster registrieren (Klicks können den Start auslösen).
            var notifications = Services.GetRequiredService<NotificationService>();
            notifications.Invoked += (_, runId) => UiQueue.TryEnqueue(() => ShowMainWindow(runId));
            notifications.Register();

            MainWindow = new MainWindow();
            CreateTrayIcon(settings);

            var coordinator = Services.GetRequiredService<SyncCoordinator>();
            coordinator.RunStarted += (_, _) => UiQueue.TryEnqueue(UpdateTrayToolTip);
            coordinator.RunCompleted += (_, e) => UiQueue.TryEnqueue(() =>
            {
                UpdateTrayToolTip();
                // Manuelle Läufe sieht man im Fenster; Benachrichtigung nur, wenn es verborgen ist.
                if (e.Trigger != RunTrigger.Manual || !MainWindow.AppWindow.IsVisible) notifications.OnRunCompleted(e);
            });
            Services.GetRequiredService<AutomationService>().Start();

            // Start durch Klick auf eine Benachrichtigung?
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.AppNotification && activation.Data is AppNotificationActivatedEventArgs n)
            {
                ShowMainWindow(NotificationService.ParseRunId(n.Arguments));
            }
            else if (!StartupService.StartedInBackground)
            {
                MainWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            throw;
        }
    }

    /// <summary>Weitergeleiteter Start einer zweiten Instanz (läuft auf einem Hintergrund-Thread).</summary>
    public static void OnRedirectedActivation(AppActivationArguments args)
    {
        long? runId = null;
        if (args.Kind == ExtendedActivationKind.AppNotification && args.Data is AppNotificationActivatedEventArgs n)
            runId = NotificationService.ParseRunId(n.Arguments);
        UiQueue?.TryEnqueue(() => ShowMainWindow(runId));
    }

    /// <summary>Zeigt das Hauptfenster (aus dem Infobereich) und öffnet optional einen Lauf.</summary>
    public static void ShowMainWindow(long? runId = null)
    {
        var window = MainWindow;
        window.AppWindow.Show();
        if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p) p.Restore();
        window.Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        if (runId is { } id) window.Navigate(typeof(RunDetailPage), id);
    }

    /// <summary>Beendet SyncForge vollständig (Infobereich-Menü oder Schließen ohne Infobereich).</summary>
    public static void ExitApp()
    {
        if (IsExiting) return;
        IsExiting = true;
        Services.GetRequiredService<SyncCoordinator>().CancelAll();
        Services.GetRequiredService<AutomationService>().Dispose();
        Services.GetRequiredService<NotificationService>().Unregister();
        _current?._tray?.Dispose();
        MainWindow.Close();
        Current.Exit();
    }

    private void CreateTrayIcon(SettingsService settings)
    {
        TrayIcon.ApplyMenuTheme(settings.Current.Theme);
        _tray = new TrayIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SyncForge.ico"))
        {
            MenuText = c => Localization.Get(c switch
            {
                TrayIcon.MenuCommand.Open => "Tray_Open",
                TrayIcon.MenuCommand.SyncAll => "Tray_SyncAll",
                _ => "Tray_Exit"
            })
        };
        _tray.Command += (_, cmd) =>
        {
            switch (cmd)
            {
                case TrayIcon.MenuCommand.Open: ShowMainWindow(); break;
                case TrayIcon.MenuCommand.SyncAll: Services.GetRequiredService<AutomationService>().RunAll(); break;
                case TrayIcon.MenuCommand.Exit: ExitApp(); break;
            }
        };
        _tray.DriveArrived += (_, letter) => Services.GetRequiredService<AutomationService>().OnDriveArrived(letter);
        UpdateTrayToolTip();
    }

    private void UpdateTrayToolTip()
    {
        var running = Services.GetRequiredService<SyncCoordinator>().RunningCount;
        _tray?.SetToolTip(running > 0 ? Format.Text("Tray_Running", Format.Number(running)) : "SyncForge");
    }

    /// <summary>Menü-Design des Infobereichs an das App-Design anpassen.</summary>
    public static void OnThemeChanged(Models.AppTheme theme) => TrayIcon.ApplyMenuTheme(theme);

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
            LogError(ex);
        }
    }

    public static void LogError(Exception? ex)
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);
}
