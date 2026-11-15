using SyncForge.Core.Engine;
using SyncForge.Core.Models;
using SyncForge.Core.Network;

namespace SyncForge.Services;

/// <summary>Gemeinsame Prüfungen für Profil-Editor und Assistent (Pfade, Quellen, Ziele).</summary>
public static class ProfileValidation
{
    public static bool IsValidPath(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path.Trim()) && Path.GetFullPath(path.Trim()).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    /// <summary>
    /// Ziel mit Volume-ID: Für lokale/USB-Laufwerke (außer dem Systemlaufwerk) wird die Seriennummer gespeichert,
    /// damit das Laufwerk auch unter anderem Buchstaben erkannt und nie ein fremdes beschrieben wird.
    /// </summary>
    public static SyncTarget CreateTarget(Guid id, string path, bool enabled)
    {
        var target = new SyncTarget { Id = id, Path = NormalizePath(path), Enabled = enabled };
        try
        {
            var root = Path.GetPathRoot(target.Path);
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal)
                && !string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase)
                && new DriveInfo(root).DriveType is DriveType.Fixed or DriveType.Removable
                && VolumeResolver.GetVolume(root) is { } volume)
            {
                target.VolumeSerial = volume.Serial;
                target.VolumeLabel = volume.Label;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Laufwerk nicht bereit: ohne Volume-ID speichern.
        }
        return target;
    }

    /// <summary>
    /// Prüft Quellen (mit ihrem Unterordner im Ziel) und Ziele. Gibt eine lesbare Fehlermeldung zurück oder null.
    /// </summary>
    public static string? ValidatePaths(IReadOnlyList<(string Path, string TargetFolder)> sources, IReadOnlyList<string> targets)
    {
        var used = sources.Where(s => !string.IsNullOrWhiteSpace(s.Path)).ToList();
        if (used.Count == 0) return Localization.Get("Editor_ErrorSourceEmpty");
        foreach (var s in used)
        {
            if (!IsValidPath(s.Path)) return Localization.Get("Editor_ErrorPathInvalid") + " " + s.Path;
            NetworkShares.EnsureAccess(s.Path); // Netzwerkquelle mit gespeicherten Anmeldedaten
            if (!Directory.Exists(s.Path))
                return Localization.Get(NetworkShares.IsNetworkPath(s.Path) ? "Editor_ErrorSourceNetwork" : "Editor_ErrorSourceMissing") + " " + s.Path;
        }
        // Quellen dürfen sich nicht überschneiden (sonst würden Dateien doppelt gesichert).
        for (var i = 0; i < used.Count; i++)
        for (var j = i + 1; j < used.Count; j++)
        {
            try { SyncPlanner.ValidatePaths(used[i].Path, used[j].Path); }
            catch (SyncValidationException) { return Localization.Get("Editor_ErrorSourcesOverlap") + " " + used[j].Path; }
        }

        var usedTargets = targets.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (usedTargets.Count == 0) return Localization.Get("Editor_ErrorNoTarget");
        foreach (var t in usedTargets)
        {
            if (!IsValidPath(t)) return Localization.Get("Editor_ErrorPathInvalid") + " " + t;
            foreach (var s in used)
            {
                var root = string.IsNullOrEmpty(s.TargetFolder) ? t : Path.Combine(t, s.TargetFolder);
                try
                {
                    SyncPlanner.ValidatePaths(s.Path, t);
                    SyncPlanner.ValidatePaths(s.Path, root);
                }
                catch (SyncValidationException ex)
                {
                    return Localization.Get("Validation_" + ex.Error) + " " + t;
                }
            }
        }

        var duplicate = usedTargets.GroupBy(NormalizePath, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        return duplicate is null ? null : Localization.Get("Editor_ErrorDuplicateTarget") + " " + duplicate.Key;
    }

    /// <summary>Unterordner je Quelle: vorhandene bleiben, neue (bei mehreren Quellen) werden eindeutig vergeben.</summary>
    public static List<string> TargetFolders(IReadOnlyList<(string Path, string TargetFolder)> sources)
    {
        var usedCount = sources.Count(s => s.Path.Trim().Length > 0);
        if (usedCount <= 1) return sources.Select(s => s.TargetFolder).ToList();
        var taken = sources.Select(s => s.TargetFolder).Where(f => f.Length > 0).ToList();
        var result = new List<string>();
        foreach (var s in sources)
        {
            var folder = s.TargetFolder;
            if (folder.Length == 0 && s.Path.Trim().Length > 0)
            {
                folder = SyncProfile.SuggestTargetFolder(s.Path, taken, Localization.Get("Editor_DriveFolder"));
                taken.Add(folder);
            }
            result.Add(folder);
        }
        return result;
    }
}
