using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SyncForge.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e) => App.MainWindow.NavigateTo("Profiles");
}
