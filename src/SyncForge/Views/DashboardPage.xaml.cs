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

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _profiles.Changed += OnDataChanged;
            _automation.ScheduleChanged += OnDataChanged;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _profiles.Changed -= OnDataChanged;
            _automation.ScheduleChanged -= OnDataChanged;
        };
    }

    // Ereignisse kommen ggf. von Hintergrund-Threads.
    private void OnDataChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);

    private void Refresh()
    {
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

    private void NewProfile_Click(object sender, RoutedEventArgs e) => App.MainWindow.Navigate(typeof(ProfileEditorPage));
}
