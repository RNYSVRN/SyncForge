using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.History;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class RunDetailPage : Page
{
    public RunDetailViewModel ViewModel { get; } = App.Services.GetRequiredService<RunDetailViewModel>();

    public RunDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is long id) ViewModel.Load(id);
    }

    /// <summary>Sichtbar, solange der Eintrag existiert.</summary>
    public Visibility ShowIf(bool notFound) => notFound ? Visibility.Collapsed : Visibility.Visible;

    private void KindSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag && Enum.TryParse<FileFilter>(tag, out var filter)) ViewModel.Filter = filter;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path && Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }

    private void ResolveConflicts_Click(object sender, RoutedEventArgs e) =>
        App.MainWindow.Navigate(typeof(ConflictsPage), ViewModel.ProfileId);

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.Get("Detail_DeleteTitle"),
            Content = Localization.Get("Detail_DeleteConfirm"),
            PrimaryButtonText = Localization.Get("Common_Delete"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        ViewModel.Delete();
        App.MainWindow.GoBack("History");
    }
}
