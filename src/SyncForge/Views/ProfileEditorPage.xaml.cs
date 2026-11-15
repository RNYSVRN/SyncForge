using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class ProfileEditorPage : Page
{
    public ProfileEditorViewModel ViewModel { get; } = App.Services.GetRequiredService<ProfileEditorViewModel>();

    public ProfileEditorPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Load(e.Parameter as Guid?);
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        if (await FolderPickerHelper.PickFolderAsync() is { } path)
        {
            ViewModel.SourcePath = path;
            if (string.IsNullOrWhiteSpace(ViewModel.Name)) ViewModel.Name = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } n ? n : path;
        }
    }

    private async void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TargetItemViewModel target && await FolderPickerHelper.PickFolderAsync() is { } path)
        {
            target.Path = path;
        }
    }

    private async void AddTarget_Click(object sender, RoutedEventArgs e)
    {
        // Leere Zeile wiederverwenden, sonst neue anlegen – direkt mit Ordnerauswahl.
        var path = await FolderPickerHelper.PickFolderAsync();
        if (path is null) return;
        var empty = ViewModel.Targets.FirstOrDefault(t => string.IsNullOrWhiteSpace(t.Path));
        if (empty is not null) empty.Path = path;
        else ViewModel.AddTarget(path);
    }

    private async void SourceCredentials_Click(object sender, RoutedEventArgs e) =>
        await NetworkCredentialDialog.ShowAsync(this, ViewModel.SourcePath);

    private async void TargetCredentials_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ViewModels.TargetItemViewModel target)
            await NetworkCredentialDialog.ShowAsync(this, target.Path);
    }

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TargetItemViewModel target) ViewModel.RemoveTarget(target);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Save() is not null) App.MainWindow.GoBack();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => App.MainWindow.GoBack();
}
