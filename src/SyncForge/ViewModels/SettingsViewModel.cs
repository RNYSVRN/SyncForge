using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.AppLifecycle;
using SyncForge.Models;
using SyncForge.Services;

namespace SyncForge.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] LanguageCodes = [string.Empty, "de-DE", "en-US"];

    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly UpdateService _updates;
    private readonly string _startupLanguage;
    private readonly bool _loading;

    public SettingsViewModel(SettingsService settings, ThemeService theme, UpdateService updates)
    {
        _settings = settings;
        _theme = theme;
        _updates = updates;
        _startupLanguage = settings.Current.Language;

        _loading = true;
        var s = settings.Current;
        ThemeIndex = (int)s.Theme;
        LanguageIndex = Math.Max(0, Array.IndexOf(LanguageCodes, s.Language));
        // Tatsächlichen Autostart-Eintrag anzeigen (könnte außerhalb der App entfernt worden sein).
        StartWithWindows = StartupService.IsEnabled();
        MinimizeToTray = s.MinimizeToTray;
        NotifyOnSuccess = s.NotifyOnSuccess;
        NotifyOnError = s.NotifyOnError;
        NotifyOnConflict = s.NotifyOnConflict;
        MaxParallelCopies = s.MaxParallelCopies;
        BandwidthLimitKBps = s.BandwidthLimitKBps;
        PauseOnBattery = s.PauseOnBattery;
        PauseOnMeteredConnection = s.PauseOnMeteredConnection;
        HistoryRetentionDays = s.HistoryRetentionDays;
        CheckForUpdates = s.CheckForUpdates;
        _loading = false;
        RefreshUpdateState();
    }

    public string Version => Helpers.AppInfo.VersionText;

    public string DataDirectory => SettingsService.DataDirectory;

    [ObservableProperty] public partial int ThemeIndex { get; set; }
    [ObservableProperty] public partial int LanguageIndex { get; set; }
    [ObservableProperty] public partial bool IsRestartRequired { get; set; }
    [ObservableProperty] public partial bool StartWithWindows { get; set; }
    [ObservableProperty] public partial bool MinimizeToTray { get; set; }
    [ObservableProperty] public partial bool NotifyOnSuccess { get; set; }
    [ObservableProperty] public partial bool NotifyOnError { get; set; }
    [ObservableProperty] public partial bool NotifyOnConflict { get; set; }
    [ObservableProperty] public partial double MaxParallelCopies { get; set; }
    [ObservableProperty] public partial double BandwidthLimitKBps { get; set; }
    [ObservableProperty] public partial bool PauseOnBattery { get; set; }
    [ObservableProperty] public partial bool PauseOnMeteredConnection { get; set; }
    [ObservableProperty] public partial double HistoryRetentionDays { get; set; }

    partial void OnThemeIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.Current.Theme = (AppTheme)value;
        _theme.Apply(_settings.Current.Theme);
        _settings.Save();
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.Current.Language = LanguageCodes[value];
        IsRestartRequired = _settings.Current.Language != _startupLanguage;
        _settings.Save();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        StartupService.Apply(value);
        Update(s => s.StartWithWindows = value);
    }
    partial void OnMinimizeToTrayChanged(bool value) => Update(s => s.MinimizeToTray = value);
    partial void OnNotifyOnSuccessChanged(bool value) => Update(s => s.NotifyOnSuccess = value);
    partial void OnNotifyOnErrorChanged(bool value) => Update(s => s.NotifyOnError = value);
    partial void OnNotifyOnConflictChanged(bool value) => Update(s => s.NotifyOnConflict = value);
    partial void OnMaxParallelCopiesChanged(double value) => Update(s => s.MaxParallelCopies = Clamp(value, 1, 16, 4));
    partial void OnBandwidthLimitKBpsChanged(double value) => Update(s => s.BandwidthLimitKBps = Clamp(value, 0, 10_000_000, 0));
    partial void OnPauseOnBatteryChanged(bool value) => Update(s => s.PauseOnBattery = value);
    partial void OnPauseOnMeteredConnectionChanged(bool value) => Update(s => s.PauseOnMeteredConnection = value);
    partial void OnHistoryRetentionDaysChanged(double value) => Update(s => s.HistoryRetentionDays = Clamp(value, 1, 3650, 90));

    // ---------- Updates ----------

    [ObservableProperty] public partial bool CheckForUpdates { get; set; }
    [ObservableProperty] public partial string UpdateStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }
    [ObservableProperty] public partial bool CanCheckUpdates { get; set; } = true;

    partial void OnCheckForUpdatesChanged(bool value)
    {
        if (_loading) return;
        _updates.SetEnabled(value);
    }

    /// <summary>Von der Seite beim Anzeigen/Verlassen aufgerufen (der Dienst lebt länger als das ViewModel).</summary>
    public void Attach() { _updates.Changed += OnUpdatesChanged; RefreshUpdateState(); }
    public void Detach() => _updates.Changed -= OnUpdatesChanged;

    private void OnUpdatesChanged(object? sender, EventArgs e) => RefreshUpdateState();

    private void RefreshUpdateState()
    {
        var available = _updates.Available;
        IsUpdateAvailable = available is not null;
        CanCheckUpdates = !_updates.IsChecking;
        var last = _settings.Current.LastUpdateCheck;
        UpdateStatus = _updates.IsChecking ? Localization.Get("Update_StatusChecking")
            : _updates.LastError is { } error ? Helpers.Format.Text("Update_StatusError", error)
            : available is { } a ? Helpers.Format.Text("Update_StatusAvailable", a.Version.ToString(3))
            : last is { } at ? Helpers.Format.Text("Update_StatusLatest", Helpers.Format.DateTime(at))
            : Localization.Get("Update_StatusNever");
    }

    [RelayCommand]
    private Task CheckUpdatesNow() => _updates.CheckNowAsync();

    [RelayCommand]
    private void OpenUpdatePage()
    {
        if (_updates.Available is { } a) Process.Start(new ProcessStartInfo(a.Url) { UseShellExecute = true });
    }

    [RelayCommand]
    private static void Restart() => AppInstance.Restart(string.Empty);

    [RelayCommand]
    private void OpenDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DataDirectory}\"") { UseShellExecute = true });
    }

    private void Update(Action<AppSettings> apply)
    {
        if (_loading) return;
        apply(_settings.Current);
        _settings.Save();
    }

    private static int Clamp(double value, int min, int max, int fallback) =>
        double.IsNaN(value) ? fallback : (int)Math.Clamp(Math.Round(value), min, max);
}
