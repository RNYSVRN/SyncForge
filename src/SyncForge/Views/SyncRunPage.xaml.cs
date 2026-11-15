using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class SyncRunPage : Page
{
    public SyncRunViewModel ViewModel { get; } = App.Services.GetRequiredService<SyncRunViewModel>();

    public SyncRunPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SyncRunRequest request) await ViewModel.StartAsync(request);
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.ExecuteAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.Cancel();

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow.GoBack();
}
