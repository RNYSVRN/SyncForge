using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.ViewModels;

namespace SyncForge.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; } = App.Services.GetRequiredService<HistoryViewModel>();

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel.StatsChanged += (_, _) => Chart.SetData(ViewModel.DailyStats);
        var history = App.Services.GetRequiredService<Core.History.HistoryStore>();
        Loaded += (_, _) => history.Changed += OnHistoryChanged;
        Unloaded += (_, _) => history.Changed -= OnHistoryChanged;
    }

    // Neue Läufe (auch aus dem Hintergrund) sofort anzeigen.
    private void OnHistoryChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(ViewModel.Load);

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Load();
    }

    /// <summary>Hinweis "keine Treffer", wenn ein Filter aktiv ist und nichts übrig bleibt.</summary>
    public Visibility NoMatches(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Run_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RunItemViewModel run) App.MainWindow.Navigate(typeof(RunDetailPage), run.Id);
    }

    private void LoadMore_Click(object sender, RoutedEventArgs e) => ViewModel.LoadMore();

    private async void Export_Click(object sender, RoutedEventArgs e) => await ViewModel.ExportAsync(XamlRoot);
}
