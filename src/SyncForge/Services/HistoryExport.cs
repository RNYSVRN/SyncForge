using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Helpers;

namespace SyncForge.Services;

/// <summary>Export der Historie als CSV (Läufe bzw. Dateiliste eines Laufs) für Excel und Co.</summary>
public static class HistoryExport
{
    /// <summary>Speicherort wählen, schreiben und das Ergebnis melden (mit "Öffnen", z. B. in Excel).</summary>
    public static async Task ExportAsync(XamlRoot xamlRoot, string suggestedName, Action<CsvWriter> write)
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(App.MainWindow.AppWindow.Id)
        {
            SuggestedFileName = suggestedName
        };
        picker.FileTypeChoices.Add(Localization.Get("Csv_FileType"), [".csv"]);
        var result = await picker.PickSaveFileAsync();
        if (result?.Path is not { } path) return;

        try
        {
            var separator = CsvWriter.SeparatorFor(CultureInfo.CurrentCulture);
            await Task.Run(() =>
            {
                using var stream = new StreamWriter(path, append: false, CsvWriter.Encoding);
                write(new CsvWriter(stream, separator));
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = Localization.Get("Status_Error"),
                Content = ex.Message,
                CloseButtonText = Localization.Get("Common_Ok")
            }.ShowAsync();
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Localization.Get("Csv_ExportedTitle"),
            Content = new TextBlock { Text = Format.Text("Csv_ExportedText", path), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
            PrimaryButtonText = Localization.Get("Csv_Open"),
            CloseButtonText = Localization.Get("Common_Ok"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Kein Programm für .csv verknüpft: nichts tun.
            }
        }
    }

    public static void WriteRuns(CsvWriter csv, IEnumerable<RunSummary> runs)
    {
        csv.WriteRow(Headers("Start", "Profile", "Trigger", "Status", "Copied", "Updated", "Deleted", "Bytes", "Errors", "Conflicts", "Targets", "Skipped", "Duration"));
        foreach (var r in runs)
        {
            csv.WriteRow([
                CsvWriter.Date(r.StartedAt), r.ProfileName, Localization.Get("Trigger_" + r.Trigger), Format.StatusName(r.Status),
                CsvWriter.Number(r.Copied), CsvWriter.Number(r.Updated), CsvWriter.Number(r.Deleted), CsvWriter.Number(r.Bytes),
                CsvWriter.Number(r.Errors), CsvWriter.Number(r.Conflicts), CsvWriter.Number(r.TargetsTotal), CsvWriter.Number(r.TargetsSkipped),
                CsvWriter.Number(Math.Max(1, (long)Math.Round(r.Duration.TotalSeconds))) // wie in der App: mindestens 1 s
            ]);
        }
    }

    public static void WriteFiles(CsvWriter csv, RunDetail detail, IEnumerable<RunFileEntry> files)
    {
        var targets = detail.Targets.ToDictionary(t => t.Id, t => t.TargetPath);
        csv.WriteRow(Headers("Target", "Action", "Path", "Size", "Side", "Error"));
        foreach (var f in files)
        {
            csv.WriteRow([
                targets.GetValueOrDefault(f.TargetId, string.Empty), KindName(f.Kind), f.Path, CsvWriter.Number(f.Size),
                Localization.Get(f.Side == SyncSide.Source ? "Csv_SideSource" : "Csv_SideTarget"), f.Error
            ]);
        }
    }

    private static IEnumerable<string> Headers(params string[] keys) => keys.Select(k => Localization.Get("Csv_Col_" + k));

    private static string KindName(SyncActionKind kind) => Localization.Get("Csv_Kind_" + kind);

    /// <summary>Vorschlag wie "SyncForge Historie 2026-11-15", ohne Zeichen, die in Dateinamen nicht erlaubt sind.</summary>
    public static string FileName(string key, params object[] args)
    {
        var name = Format.Text(key, args);
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    public static string FileDate(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
}
