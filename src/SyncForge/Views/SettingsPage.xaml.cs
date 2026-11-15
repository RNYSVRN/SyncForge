using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.History;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.Get("Settings_ClearHistoryTitle"),
            Content = Localization.Get("Settings_ClearHistoryText"),
            PrimaryButtonText = Localization.Get("Common_Delete"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Services.GetRequiredService<HistoryStore>().Clear();
        }
    }
}
