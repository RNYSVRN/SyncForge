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
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(App.MainWindow.AppWindow.Id)
        {
            SuggestedFileName = $"SyncForge-Sicherung {DateTime.Now:yyyy-MM-dd}"
        };
        picker.FileTypeChoices.Add(Localization.Get("ImportExport_FileType"), [".json"]);
        var result = await picker.PickSaveFileAsync();
        if (result?.Path is not { } path) return;
        try
        {
            await App.Services.GetRequiredService<ImportExportService>().ExportAsync(path);
            await ShowMessageAsync(Localization.Get("ImportExport_ExportedTitle"), Helpers.Format.Text("ImportExport_ExportedText", path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync(Localization.Get("Status_Error"), ex.Message);
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.MainWindow.AppWindow.Id);
        picker.FileTypeFilter.Add(".json");
        var result = await picker.PickSingleFileAsync();
        if (result?.Path is not { } path) return;

        var file = await ImportExportService.ReadAsync(path);
        if (file is null)
        {
            await ShowMessageAsync(Localization.Get("ImportExport_InvalidTitle"), Localization.Get("ImportExport_InvalidText"));
            return;
        }

        var includeSettings = new CheckBox { Content = Localization.Get("ImportExport_IncludeSettings"), IsChecked = file.Settings is not null, IsEnabled = file.Settings is not null };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = Helpers.Format.Text("ImportExport_ImportText", Helpers.Format.Number(file.Profiles.Count), Helpers.Format.DateTime(file.ExportedAt)),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(includeSettings);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localization.Get("ImportExport_ImportTitle"),
            Content = content,
            PrimaryButtonText = Localization.Get("ImportExport_ImportButton"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        App.Services.GetRequiredService<ImportExportService>().Import(file, includeSettings.IsChecked == true);
        if (includeSettings.IsChecked == true)
        {
            // Seite mit den übernommenen Werten neu laden; das Design gilt sofort, die Sprache nach Neustart.
            App.Services.GetRequiredService<ThemeService>().Apply(App.Services.GetRequiredService<SettingsService>().Current.Theme);
            Frame.Navigate(typeof(SettingsPage));
            Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
        }
    }

    private async Task ShowMessageAsync(string title, string text)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = Localization.Get("Common_Ok"),
            RequestedTheme = ActualTheme
        };
        await dialog.ShowAsync();
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
