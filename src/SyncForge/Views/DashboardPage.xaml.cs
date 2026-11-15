using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Storage;
using SyncForge.Helpers;
using SyncForge.Services;
using SyncForge.ViewModels;

namespace SyncForge.Views;

/// <summary>Übersicht: Kennzahlen, eine Karte je Profil mit Status, nächstem Lauf, Aktionen und laufendem Fortschritt.</summary>
public sealed partial class DashboardPage : Page
{
    private readonly ProfileStore _profiles = App.Services.GetRequiredService<ProfileStore>();
    private readonly AutomationService _automation = App.Services.GetRequiredService<AutomationService>();
    private readonly UpdateService _updates = App.Services.GetRequiredService<UpdateService>();
    private readonly SyncCoordinator _coordinator = App.Services.GetRequiredService<SyncCoordinator>();
    private readonly ObservableCollection<DashboardProfileViewModel> _cards = [];

    public DashboardPage()
    {
        InitializeComponent();
        ProfileCards.ItemsSource = _cards;
        Loaded += (_, _) =>
        {
            _profiles.Changed += OnDataChanged;
            _automation.ScheduleChanged += OnDataChanged;
            _updates.Changed += OnDataChanged;
            _coordinator.ActivityChanged += OnActivityChanged;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _profiles.Changed -= OnDataChanged;
            _automation.ScheduleChanged -= OnDataChanged;
            _updates.Changed -= OnDataChanged;
            _coordinator.ActivityChanged -= OnActivityChanged;
        };
    }

    // Ereignisse kommen ggf. von Hintergrund-Threads.
    private void OnDataChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Refresh);
    private void OnActivityChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshActivity);

    private void Refresh()
    {
        if (_updates.ShowHint && _updates.Available is { } update)
        {
            UpdateBar.Title = Services.Localization.Get("Update_InfoTitle");
            UpdateBar.Message = Format.Text("Update_InfoMessage", update.Version.ToString(3), AppInfo.VersionText);
            UpdateBar.IsOpen = true;
        }
        else UpdateBar.IsOpen = false;

        var profiles = _profiles.Profiles;
        var hasProfiles = profiles.Count > 0;
        EmptyState.Visibility = hasProfiles ? Visibility.Collapsed : Visibility.Visible;
        foreach (var element in new FrameworkElement[] { StatsGrid, ProfilesHeader, ProfileCards, SyncAllButton, NewProfileButton })
            element.Visibility = hasProfiles ? Visibility.Visible : Visibility.Collapsed;

        ProfileCountText.Text = Format.Number(profiles.Count);
        var last = profiles.Where(p => p.LastRunAt is not null).OrderByDescending(p => p.LastRunAt).FirstOrDefault();
        if (last is not null)
        {
            LastRunText.Text = Format.DateTime(last.LastRunAt!.Value);
            LastRunDetail.Text = $"{last.Name} · {Format.StatusName(last.LastRunStatus)}";
        }
        if (_automation.GetNextRun() is { } next)
        {
            NextRunText.Text = Format.DateTime(next.At);
            NextRunDetail.Text = next.Profile.Name;
        }
        else
        {
            NextRunText.Text = "–";
            NextRunDetail.Text = Services.Localization.Get("Dashboard_NoSchedule/Text");
        }

        // Karten angleichen statt neu aufbauen (kein Flackern bei Hintergrundläufen).
        foreach (var gone in _cards.Where(c => profiles.All(p => p.Id != c.Id)).ToList()) _cards.Remove(gone);
        for (var i = 0; i < profiles.Count; i++)
        {
            var card = _cards.FirstOrDefault(c => c.Id == profiles[i].Id);
            if (card is null)
            {
                card = new DashboardProfileViewModel(profiles[i]);
                _cards.Insert(Math.Min(i, _cards.Count), card);
            }
            card.Update(profiles[i]);
        }

        var attention = _cards.Count(c => c.NeedsAttention);
        AttentionBar.IsOpen = attention > 0;
        AttentionBar.Title = Services.Localization.Get("Dashboard_AttentionTitle");
        AttentionBar.Message = Format.Text(attention == 1 ? "Dashboard_AttentionOne" : "Dashboard_AttentionMany", Format.Number(attention));
        RefreshActivity();
    }

    private void RefreshActivity()
    {
        var activities = _coordinator.Activities;
        foreach (var card in _cards) card.Update(activities.FirstOrDefault(a => a.ProfileId == card.Id));
    }

    private static Guid? IdOf(object sender) => ((sender as FrameworkElement)?.Tag as DashboardProfileViewModel)?.Id;

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (IdOf(sender) is { } id) App.MainWindow.Navigate(typeof(SyncRunPage), new SyncRunRequest(id, PreviewOnly: false));
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (IdOf(sender) is { } id) App.MainWindow.Navigate(typeof(SyncRunPage), new SyncRunRequest(id, PreviewOnly: true));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (IdOf(sender) is { } id) App.MainWindow.Navigate(typeof(ProfileEditorPage), id);
    }

    /// <summary>Alle Profile im Hintergrund synchronisieren (wie im Infobereich); der Fortschritt erscheint auf den Karten.</summary>
    private void SyncAll_Click(object sender, RoutedEventArgs e) => _automation.RunAll();

    private void UpdateBar_CloseButtonClick(InfoBar sender, object args) => _updates.Dismiss();

    private void OpenUpdatePage_Click(object sender, RoutedEventArgs e)
    {
        if (_updates.Available is { } a) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(a.Url) { UseShellExecute = true });
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e) => App.MainWindow.Navigate(typeof(WizardPage));
}
