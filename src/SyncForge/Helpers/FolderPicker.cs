namespace SyncForge.Helpers;

public static class FolderPickerHelper
{
    /// <summary>Zeigt den Windows-Ordnerauswahldialog. Gibt null zurück, wenn abgebrochen.</summary>
    public static async Task<string?> PickFolderAsync()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(App.MainWindow.AppWindow.Id);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }
}
