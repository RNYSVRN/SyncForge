using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Storage;
using SyncForge.Helpers;

namespace SyncForge.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var profiles = App.Services.GetRequiredService<ProfileStore>().Profiles;
        ProfileCountText.Text = Format.Number(profiles.Count);

        var last = profiles.Where(p => p.LastRunAt is not null).OrderByDescending(p => p.LastRunAt).FirstOrDefault();
        if (last is not null)
        {
            LastRunText.Text = Format.DateTime(last.LastRunAt!.Value);
            LastRunDetail.Text = $"{last.Name} · {Format.StatusName(last.LastRunStatus)}";
        }
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e) => App.MainWindow.Navigate(typeof(ProfileEditorPage));
}
