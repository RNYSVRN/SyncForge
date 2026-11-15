using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.Views;

public sealed partial class DashboardPage : Page
{
    private readonly ProfileStore _profiles = App.Services.GetRequiredService<ProfileStore>();
    private readonly AutomationService _automation = App.Services.GetRequiredService<AutomationService>();
    private readonly UpdateService _updates = App.Services.GetRequiredService<UpdateService>();

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _profiles.Changed += OnDataChanged;
            _automation.ScheduleChanged += OnDataChanged;
            _updates.Changed += OnDataChanged;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _profiles.Changed -= OnDataChanged;
            _automation.ScheduleChanged -= OnDataChanged;
            _updates.Changed -= OnDataChanged;
        };
    }

    // Ereignisse kommen ggf. von Hintergrund-Threads.
    private void OnDataChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
        if (_updates.ShowHint && _updates.Available is { } update)
        {
            UpdateBar.Title = Services.Localization.Get("Update_InfoTitle");
            UpdateBar.Message = Format.Text("Update_InfoMessage", update.Version.ToString(3), AppInfo.VersionText);
            UpdateBar.IsOpen = true;
        }
        else UpdateBar.IsOpen = false;

        var profiles = _profiles.Profiles;
        ProfileCountText.Text = Format.Number(profiles.Count);

        var last = profiles.Where(p => p.LastRunAt is not null).OrderByDescending(p => p.LastRunAt).FirstOrDefault();
        if (last is not null)
        {
            LastRunText.Text = Format.DateTime(last.LastRunAt!.Value);
            LastRunDetail.Text = $"{last.Name} · {Format.StatusName(last.LastRunStatus)}";
        }

        if (_automation.GetNextRun() is { } next)
        {
            NextRunText.Text = Format.DateTime(next.At);
            NextRunDetail.Text = next.Profile.Name;
        }
        else
        {
            NextRunText.Text = "–";
            NextRunDetail.Text = Services.Localization.Get("Dashboard_NoSchedule/Text");
        }
    }

    private void UpdateBar_CloseButtonClick(InfoBar sender, object args) => _updates.Dismiss();

    private void OpenUpdatePage_Click(object sender, RoutedEventArgs e)
    {
        if (_updates.Available is { } a) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(a.Url) { UseShellExecute = true });
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e) => App.MainWindow.Navigate(typeof(ProfileEditorPage));
}
