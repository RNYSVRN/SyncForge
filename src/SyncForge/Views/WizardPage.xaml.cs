using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Helpers;
using SyncForge.ViewModels;

namespace SyncForge.Views;

/// <summary>Assistent für neue Backup-Profile (Vorlage, Quellen, Ziel, Zeitpunkt, Zusammenfassung).</summary>
public sealed partial class WizardPage : Page
{
    public WizardViewModel ViewModel { get; } = App.Services.GetRequiredService<WizardViewModel>();

    public WizardPage()
    {
        InitializeComponent();
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not WizardTemplate template) return;
        // "Alles selbst festlegen": direkt in den vollständigen Editor (ersetzt den Assistenten im Verlauf).
        if (template.IsCustom)
        {
            App.MainWindow.Navigate(typeof(ProfileEditorPage));
            App.MainWindow.ForgetPage(typeof(WizardPage));
            return;
        }
        ViewModel.SelectTemplate(template);
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SourceItemViewModel source && await FolderPickerHelper.PickFolderAsync() is { } path)
            source.Path = path;
    }

    private async void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var path = await FolderPickerHelper.PickFolderAsync();
        if (path is null) return;
        var empty = ViewModel.Sources.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Path));
        if (empty is not null) empty.Path = path;
        else ViewModel.AddSource(path);
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SourceItemViewModel source) ViewModel.RemoveSource(source);
    }

    private async void SourceCredentials_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SourceItemViewModel source) await NetworkCredentialDialog.ShowAsync(this, source.Path);
    }

    private async void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (await FolderPickerHelper.PickFolderAsync() is { } path) ViewModel.TargetPath = path;
    }

    private async void TargetCredentials_Click(object sender, RoutedEventArgs e) =>
        await NetworkCredentialDialog.ShowAsync(this, ViewModel.TargetPath);

    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.Back();

    private void Next_Click(object sender, RoutedEventArgs e) => ViewModel.Next();

    private void Cancel_Click(object sender, RoutedEventArgs e) => App.MainWindow.GoBack("Dashboard");

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Create() is not null) App.MainWindow.GoBack("Dashboard");
    }

    private void CreatePreview_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Create() is not { } id) return;
        App.MainWindow.Navigate(typeof(SyncRunPage), new SyncRunRequest(id, PreviewOnly: true));
        App.MainWindow.ForgetPage(typeof(WizardPage));
    }

    private void CreateAndEdit_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Create() is not { } id) return;
        App.MainWindow.Navigate(typeof(ProfileEditorPage), id);
        App.MainWindow.ForgetPage(typeof(WizardPage));
    }
}
