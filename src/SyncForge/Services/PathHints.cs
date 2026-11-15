using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SyncForge.Core.Engine;
using SyncForge.Core.Network;
using SyncForge.Helpers;

namespace SyncForge.Services;

public enum HintKind { None, Ok, Info, Warning, Error }

/// <summary>Kurzer Hinweis unter einem Pfadfeld (Symbol, Farbe, Text).</summary>
public sealed record PathHint(HintKind Kind, string Text)
{
    public static readonly PathHint None = new(HintKind.None, string.Empty);

    public string Glyph => Kind switch
    {
        HintKind.Ok => "",       // Häkchen
        HintKind.Warning => "",  // Warnung
        HintKind.Error => "",    // Fehler
        _ => ""                  // Info
    };

    public Brush? Brush => Resource(Kind switch
    {
        HintKind.Ok => "SystemFillColorSuccessBrush",
        HintKind.Warning => "SystemFillColorCautionBrush",
        HintKind.Error => "SystemFillColorCriticalBrush",
        _ => "TextFillColorSecondaryBrush"
    });

    private static Brush? Resource(string key)
    {
        try { return Application.Current.Resources[key] as Brush; }
        catch { return null; }
    }
}

/// <summary>
/// Schnelle Prüfungen beim Tippen (nur lokale Dateisystem-Abfragen). Netzwerkpfade werden erst beim Speichern
/// geprüft, damit das Tippen nie auf einen nicht erreichbaren Server wartet.
/// </summary>
public static class PathHints
{
    /// <summary>Unter diesem freien Speicher wird gewarnt.</summary>
    private const long LowSpace = 2L * 1024 * 1024 * 1024;

    public static PathHint ForSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return PathHint.None;
        if (!ProfileValidation.IsValidPath(path)) return new(HintKind.Error, Localization.Get("Hint_InvalidPath"));
        if (NetworkShares.IsNetworkPath(path)) return new(HintKind.Info, Localization.Get("Hint_NetworkCheckedOnSave"));
        try
        {
            return Directory.Exists(path)
                ? new(HintKind.Ok, Localization.Get("Hint_SourceFound"))
                : new(HintKind.Error, Localization.Get("Hint_SourceMissing"));
        }
        catch
        {
            return PathHint.None;
        }
    }

    public static PathHint ForTarget(string path, IReadOnlyList<string> sources)
    {
        if (string.IsNullOrWhiteSpace(path)) return PathHint.None;
        if (!ProfileValidation.IsValidPath(path)) return new(HintKind.Error, Localization.Get("Hint_InvalidPath"));

        // Ziel in einer Quelle (oder umgekehrt): würde sich selbst sichern.
        foreach (var source in sources.Where(s => !string.IsNullOrWhiteSpace(s) && ProfileValidation.IsValidPath(s)))
        {
            try { SyncPlanner.ValidatePaths(source, path); }
            catch (SyncValidationException ex) when (ex.Error != SyncValidationError.SourceMissing)
            {
                return new(HintKind.Error, Localization.Get("Validation_" + ex.Error).TrimEnd(':'));
            }
        }

        if (NetworkShares.IsNetworkPath(path)) return new(HintKind.Info, Localization.Get("Hint_NetworkCheckedOnSave"));
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path.Trim()));
            if (string.IsNullOrEmpty(root)) return PathHint.None;
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return new(HintKind.Warning, Localization.Get("Hint_DriveNotReady"));

            var info = WizardViewModel_DescribeTarget(path);
            var sameDrive = sources.Any(s => !string.IsNullOrWhiteSpace(s) && !NetworkShares.IsNetworkPath(s) && ProfileValidation.IsValidPath(s)
                && string.Equals(Path.GetPathRoot(Path.GetFullPath(s.Trim())), root, StringComparison.OrdinalIgnoreCase));
            if (sameDrive) return new(HintKind.Warning, Format.Text("Hint_SameDrive", info));
            if (drive.AvailableFreeSpace < LowSpace) return new(HintKind.Warning, Format.Text("Hint_LowSpace", info));
            return new(HintKind.Ok, info);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return PathHint.None;
        }
    }

    private static string WizardViewModel_DescribeTarget(string path) => ViewModels.WizardViewModel.DescribeTarget(path);
}
