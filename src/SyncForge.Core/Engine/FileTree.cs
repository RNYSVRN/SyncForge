using SyncForge.Core.Filtering;

namespace SyncForge.Core.Engine;

internal sealed record FileTreeEntry(string RelativePath, bool IsDirectory, long Size, DateTime LastWriteUtc, FileAttributes Attributes);

/// <summary>Liest einen Verzeichnisbaum gefiltert ein (ausgeschlossene Ordner werden gar nicht betreten).</summary>
internal static class FileTree
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false
    };

    public static Dictionary<string, FileTreeEntry> Scan(string root, PathFilter filter, CancellationToken ct, Action<int>? onProgress = null)
    {
        var result = new Dictionary<string, FileTreeEntry>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return result;

        var rootInfo = new DirectoryInfo(root);
        var pending = new Stack<(DirectoryInfo Dir, string Rel)>();
        pending.Push((rootInfo, string.Empty));

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, rel) = pending.Pop();

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = dir.EnumerateFileSystemInfos("*", Options).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var entryRel = rel.Length == 0 ? entry.Name : rel + "\\" + entry.Name;
                if (entry is DirectoryInfo sub)
                {
                    if (filter.IsDirectoryExcluded(entryRel, sub.Attributes)) continue;
                    // Verknüpfungen (Symlinks/Junctions) nicht verfolgen: Endlosschleifen vermeiden.
                    if (sub.LinkTarget is not null) continue;
                    result[entryRel] = new FileTreeEntry(entryRel, true, 0, sub.LastWriteTimeUtc, sub.Attributes);
                    pending.Push((sub, entryRel));
                }
                else if (entry is FileInfo file)
                {
                    if (filter.IsFileExcluded(entryRel, file.Attributes, file.Length)) continue;
                    if (IsTempFile(file.Name)) continue;
                    result[entryRel] = new FileTreeEntry(entryRel, false, file.Length, file.LastWriteTimeUtc, file.Attributes);
                }
            }
            onProgress?.Invoke(result.Count);
        }
        return result;
    }

    /// <summary>Eigene Zwischendateien abgebrochener Kopien nie als Inhalt betrachten.</summary>
    internal static bool IsTempFile(string name) =>
        name.StartsWith(SyncExecutor.TempPrefix, StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
}
