using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class ProfilesPage : Page
{
    public ProfilesViewModel ViewModel { get; } = App.Services.GetRequiredService<ProfilesViewModel>();

    private readonly Core.Storage.ProfileStore _store = App.Services.GetRequiredService<Core.Storage.ProfileStore>();

    public ProfilesPage()
    {
        InitializeComponent();
        // Hintergrundläufe aktualisieren den Status in der Liste (Ereignis kommt ggf. von einem anderen Thread).
        Loaded += (_, _) => _store.Changed += OnStoreChanged;
        Unloaded += (_, _) => _store.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(ViewModel.Reload);

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Reload();
    }

    private static ProfileItemViewModel? ItemOf(object sender) => (sender as FrameworkElement)?.Tag as ProfileItemViewModel;

    private void Add_Click(object sender, RoutedEventArgs e) => App.MainWindow.Navigate(typeof(ProfileEditorPage));

    private void Profile_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProfileItemViewModel item) App.MainWindow.Navigate(typeof(ProfileEditorPage), item.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) App.MainWindow.Navigate(typeof(ProfileEditorPage), item.Id);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) App.MainWindow.Navigate(typeof(SyncRunPage), new SyncRunRequest(item.Id, PreviewOnly: true));
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) App.MainWindow.Navigate(typeof(SyncRunPage), new SyncRunRequest(item.Id, PreviewOnly: false));
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) ViewModel.Duplicate(item);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.Get("Profiles_DeleteTitle"),
            Content = Format.Text("Profiles_DeleteText", item.Name),
            PrimaryButtonText = Localization.Get("Common_Delete"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.Delete(item);
    }
}
