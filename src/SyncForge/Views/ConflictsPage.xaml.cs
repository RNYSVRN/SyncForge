using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Engine;
using SyncForge.ViewModels;

namespace SyncForge.Views;

/// <summary>Offene Zwei-Wege-Konflikte eines Profils ("nur melden") anzeigen und lösen. Parameter: Profil-Id.</summary>
public sealed partial class ConflictsPage : Page
{
    public ConflictsViewModel ViewModel { get; } = App.Services.GetRequiredService<ConflictsViewModel>();

    public ConflictsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid profileId) await ViewModel.LoadAsync(profileId);
    }

    private void SetAll_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        ViewModel.SetAll(tag == "Newer" ? null : Enum.Parse<ConflictChoice>(tag ?? nameof(ConflictChoice.Skip)));
    }

    private async void Apply_Click(object sender, RoutedEventArgs e) => await ViewModel.ApplyAsync();

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow.GoBack();
}
