using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class VersionsPage : Page
{
    public VersionsViewModel ViewModel { get; } = App.Services.GetRequiredService<VersionsViewModel>();

    public VersionsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id) ViewModel.Load(id);
    }

    public Visibility NoRoots(bool hasRoots) => hasRoots ? Visibility.Collapsed : Visibility.Visible;

    private static FileVersionItem? ItemOf(object sender) => (sender as FrameworkElement)?.Tag as FileVersionItem;

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        var destination = ViewModel.RestoreDestination(item);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.Get("Versions_RestoreTitle"),
            Content = Format.Text(File.Exists(destination) ? "Versions_RestoreOverwrite" : "Versions_RestoreText", destination, item.DateText),
            PrimaryButtonText = Localization.Get("Versions_RestoreButton"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.Restore(item, destination);
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        var name = Path.GetFileName(item.Version.RelativePath);
        var extension = Path.GetExtension(name);
        var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(App.MainWindow.AppWindow.Id)
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(name) + " (" + item.Version.Timestamp.ToString("yyyy-MM-dd HH-mm") + ")"
        };
        picker.FileTypeChoices.Add(string.IsNullOrEmpty(extension) ? Localization.Get("Versions_AnyFile") : extension, [string.IsNullOrEmpty(extension) ? "." : extension]);
        var result = await picker.PickSaveFileAsync();
        if (result?.Path is { } path) ViewModel.Restore(item, path);
    }

    private void Show_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item && File.Exists(item.Version.VersionPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Version.VersionPath}\"") { UseShellExecute = true });
        }
    }
}
