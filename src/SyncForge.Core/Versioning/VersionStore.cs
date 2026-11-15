using System.Globalization;
using System.Text.RegularExpressions;

namespace SyncForge.Core.Versioning;

/// <summary>Eine gespeicherte frühere Fassung einer Datei.</summary>
public sealed record FileVersion(string RelativePath, string VersionPath, DateTimeOffset Timestamp, long Size);

/// <summary>Eine Datei, von der es frühere Fassungen gibt.</summary>
public sealed record VersionedFile(string RelativePath, int VersionCount, DateTimeOffset Latest, bool CurrentExists);

/// <summary>
/// Versionsordner im Wurzelverzeichnis einer Seite: Überschriebene oder gelöschte Dateien werden nach
/// <c>.syncforge_versions\Unterordner\Name~JJJJMMTT-HHMMSS.ext</c> verschoben statt gelöscht.
/// </summary>
public static partial class VersionStore
{
    public const string FolderName = ".syncforge_versions";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    public static string VersionsRoot(string root) => Path.Combine(root, FolderName);

    /// <summary>Verschiebt <paramref name="relativePath"/> in den Versionsordner. Gibt den neuen Pfad zurück.</summary>
    public static string MoveToVersions(string root, string relativePath, DateTimeOffset timestamp)
    {
        var source = Path.Combine(root, relativePath);
        var dir = Path.Combine(VersionsRoot(root), Path.GetDirectoryName(relativePath) ?? string.Empty);
        Directory.CreateDirectory(dir);
        HideVersionsRoot(root);

        var name = Path.GetFileNameWithoutExtension(relativePath);
        var ext = Path.GetExtension(relativePath);
        var stamp = timestamp.ToLocalTime().ToString(StampFormat, CultureInfo.InvariantCulture);
        var target = Path.Combine(dir, $"{name}~{stamp}{ext}");
        for (var i = 1; File.Exists(target); i++) target = Path.Combine(dir, $"{name}~{stamp}-{i}{ext}");

        var attrs = File.GetAttributes(source);
        if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(source, attrs & ~FileAttributes.ReadOnly);
        File.Move(source, target);
        return target;
    }

    /// <summary>Alle Dateien mit Versionen; optional gefiltert (Teilstring im Pfad).</summary>
    public static IReadOnlyList<VersionedFile> ListFiles(string root, string? search = null)
    {
        var versionsRoot = VersionsRoot(root);
        if (!Directory.Exists(versionsRoot)) return [];
        return Enumerate(versionsRoot)
            .Where(v => string.IsNullOrWhiteSpace(search) || v.RelativePath.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(v => v.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => new VersionedFile(g.Key, g.Count(), g.Max(v => v.Timestamp), File.Exists(Path.Combine(root, g.Key))))
            .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Fassungen einer Datei, neueste zuerst.</summary>
    public static IReadOnlyList<FileVersion> GetVersions(string root, string relativePath)
    {
        var dir = Path.Combine(VersionsRoot(root), Path.GetDirectoryName(relativePath) ?? string.Empty);
        if (!Directory.Exists(dir)) return [];
        var baseDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
        return Directory.EnumerateFiles(dir)
            .Select(p => Parse(p, baseDir))
            .Where(v => v is not null && string.Equals(v.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            .Select(v => v!)
            .OrderByDescending(v => v.Timestamp)
            .ToList();
    }

    /// <summary>Kopiert eine Fassung an <paramref name="destination"/> (die Fassung bleibt erhalten).</summary>
    public static void Restore(FileVersion version, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            var attrs = File.GetAttributes(destination);
            if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(destination, attrs & ~FileAttributes.ReadOnly);
        }
        File.Copy(version.VersionPath, destination, overwrite: true);
    }

    /// <summary>
    /// Räumt auf: behält je Datei höchstens <paramref name="keepPerFile"/> Fassungen und entfernt Fassungen,
    /// die älter als <paramref name="maxAgeDays"/> Tage sind (0 = keine Altersgrenze). Gibt die Anzahl zurück.
    /// </summary>
    public static int Prune(string root, int keepPerFile, int maxAgeDays, DateTimeOffset? now = null)
    {
        var versionsRoot = VersionsRoot(root);
        if (!Directory.Exists(versionsRoot)) return 0;
        var cutoff = maxAgeDays > 0 ? (now ?? DateTimeOffset.Now).AddDays(-maxAgeDays) : DateTimeOffset.MinValue;
        var removed = 0;
        foreach (var group in Enumerate(versionsRoot).GroupBy(v => v.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderByDescending(v => v.Timestamp).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if ((keepPerFile > 0 && i >= keepPerFile) || ordered[i].Timestamp < cutoff)
                {
                    try { File.Delete(ordered[i].VersionPath); removed++; } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
        RemoveEmptyDirectories(versionsRoot);
        return removed;
    }

    private static IEnumerable<FileVersion> Enumerate(string versionsRoot) =>
        Directory.EnumerateFiles(versionsRoot, "*", SearchOption.AllDirectories)
            .Select(p => Parse(p, Path.GetRelativePath(versionsRoot, Path.GetDirectoryName(p)!)))
            .Where(v => v is not null)
            .Select(v => v!);

    private static FileVersion? Parse(string versionPath, string relativeDir)
    {
        var match = VersionName().Match(Path.GetFileName(versionPath));
        if (!match.Success) return null;
        if (!DateTime.TryParseExact(match.Groups["ts"].Value, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ts)) return null;
        var original = match.Groups["name"].Value + match.Groups["ext"].Value;
        var rel = relativeDir is "" or "." ? original : Path.Combine(relativeDir, original);
        return new FileVersion(rel, versionPath, new DateTimeOffset(ts), new FileInfo(versionPath).Length);
    }

    private static void HideVersionsRoot(string root)
    {
        try
        {
            var info = new DirectoryInfo(VersionsRoot(root));
            if (!info.Attributes.HasFlag(FileAttributes.Hidden)) info.Attributes |= FileAttributes.Hidden;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void RemoveEmptyDirectories(string dir)
    {
        foreach (var sub in Directory.EnumerateDirectories(dir)) RemoveEmptyDirectories(sub);
        try
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any() && !string.Equals(Path.GetFileName(dir), FolderName, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(dir);
        }
        catch (IOException) { }
    }

    // Name~20261115-031200(-1).ext – der letzte "~" trennt den Zeitstempel ab.
    [GeneratedRegex(@"^(?<name>.+)~(?<ts>\d{8}-\d{6})(?:-\d+)?(?<ext>\.[^.]*)?$")]
    private static partial Regex VersionName();
}
