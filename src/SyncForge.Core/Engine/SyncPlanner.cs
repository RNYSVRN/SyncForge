using SyncForge.Core.Filtering;
using SyncForge.Core.Models;

namespace SyncForge.Core.Engine;

/// <summary>
/// Vergleicht Quelle und Ziel und erstellt einen Plan (Modus: Spiegeln). Verändert nichts.
/// Ausgeschlossene Dateien werden auf beiden Seiten ignoriert – sie werden im Ziel also auch nie gelöscht.
/// </summary>
public static class SyncPlanner
{
    /// <summary>Toleranz für Zeitstempel (FAT/exFAT speichern nur auf 2 s genau).</summary>
    public static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

    public static async Task<SyncPlan> CreateMirrorPlanAsync(
        string sourceRoot,
        string targetRoot,
        FilterSettings filterSettings,
        CompareMode compareMode,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        targetRoot = Path.GetFullPath(targetRoot);
        ValidatePaths(sourceRoot, targetRoot);

        var filter = new PathFilter(filterSettings);
        var skipCloud = filterSettings.CloudFiles == CloudFileMode.Skip;

        progress?.Report(new SyncProgress(targetRoot, SyncPhase.Scanning, null, 0, 0, 0, 0));
        var sourceUnreadable = new List<string>();
        var targetUnreadable = new List<string>();
        var source = await Task.Run(() => FileTree.Scan(sourceRoot, filter, ct,
            n => progress?.Report(new SyncProgress(targetRoot, SyncPhase.Scanning, null, n, 0, 0, 0)), sourceUnreadable), ct);
        var targetMissing = !Directory.Exists(targetRoot);
        var target = targetMissing
            ? new Dictionary<string, FileTreeEntry>(StringComparer.OrdinalIgnoreCase)
            : await Task.Run(() => FileTree.Scan(targetRoot, filter, ct, null, targetUnreadable), ct);

        var plan = new SyncPlan { SourceRoot = sourceRoot, TargetRoot = targetRoot, TargetRootMissing = targetMissing };
        plan.UnreadableFolders.AddRange(sourceUnreadable.Select(r => Path.Combine(sourceRoot, r)));
        plan.UnreadableFolders.AddRange(targetUnreadable.Select(r => Path.Combine(targetRoot, r)));
        plan.ExistingTargetFileCount = target.Values.Count(e => !e.IsDirectory);
        plan.SourceFileCount = source.Values.Count(e => !e.IsDirectory);
        plan.SourceBytes = source.Values.Where(e => !e.IsDirectory).Sum(e => e.Size);

        var creates = new List<SyncAction>();
        var copies = new List<SyncAction>();
        var deleteFiles = new List<SyncAction>();
        var deleteDirs = new List<SyncAction>();

        // Quelle -> Ziel
        var compareCandidates = new List<(FileTreeEntry Src, FileTreeEntry Dst)>();
        foreach (var src in source.Values)
        {
            // Zielordner unlesbar: was dort liegt, ist unbekannt, also nichts hineinschreiben.
            if (targetUnreadable.Count > 0 && FileTree.IsInsideUnreadable(src.RelativePath, targetUnreadable)) continue;
            target.TryGetValue(src.RelativePath, out var dst);

            if (src.IsDirectory)
            {
                if (dst is null) creates.Add(new SyncAction(SyncActionKind.CreateDirectory, src.RelativePath, 0));
                else if (!dst.IsDirectory)
                {
                    // Typkonflikt: im Ziel liegt eine Datei, wo die Quelle einen Ordner hat.
                    deleteFiles.Add(new SyncAction(SyncActionKind.DeleteFile, dst.RelativePath, dst.Size, IsTypeConflict: true));
                    creates.Add(new SyncAction(SyncActionKind.CreateDirectory, src.RelativePath, 0));
                }
                continue;
            }

            // Nur online verfügbar: nicht herunterladen. Der Eintrag bleibt im Quellbaum, damit eine
            // vorhandene Kopie im Ziel nicht als "in der Quelle gelöscht" gilt.
            if (skipCloud && src.IsCloudOnly)
            {
                plan.SkippedCloudFiles++;
                plan.SkippedCloudBytes += src.Size;
                continue;
            }

            if (dst is null)
            {
                copies.Add(new SyncAction(SyncActionKind.CopyNew, src.RelativePath, src.Size));
            }
            else if (dst.IsDirectory)
            {
                // Typkonflikt: im Ziel liegt ein Ordner, wo die Quelle eine Datei hat.
                deleteDirs.Add(new SyncAction(SyncActionKind.DeleteDirectory, dst.RelativePath, 0, IsTypeConflict: true));
                copies.Add(new SyncAction(SyncActionKind.CopyNew, src.RelativePath, src.Size));
            }
            else if (src.Size != dst.Size)
            {
                copies.Add(new SyncAction(SyncActionKind.Update, src.RelativePath, src.Size));
            }
            else if (compareMode == CompareMode.Content && !src.IsCloudOnly && !dst.IsCloudOnly)
            {
                // Inhaltsvergleich nur bei lokal vorhandenen Dateien (sonst würde das Lesen einen Download auslösen).
                compareCandidates.Add((src, dst));
            }
            else if ((src.LastWriteUtc - dst.LastWriteUtc).Duration() > TimestampTolerance)
            {
                copies.Add(new SyncAction(SyncActionKind.Update, src.RelativePath, src.Size));
            }
            else
            {
                plan.UnchangedFileCount++;
            }
        }

        // Inhaltsvergleich (nur bei gleicher Größe nötig)
        if (compareCandidates.Count > 0)
        {
            var done = 0;
            var totalBytes = compareCandidates.Sum(c => c.Src.Size);
            long bytesDone = 0;
            var results = new System.Collections.Concurrent.ConcurrentBag<SyncAction>();
            await Parallel.ForEachAsync(compareCandidates, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct },
                async (pair, token) =>
                {
                    var equal = await FileHasher.AreEqualAsync(
                        Path.Combine(sourceRoot, pair.Src.RelativePath), Path.Combine(targetRoot, pair.Dst.RelativePath), token);
                    if (!equal) results.Add(new SyncAction(SyncActionKind.Update, pair.Src.RelativePath, pair.Src.Size));
                    var n = Interlocked.Increment(ref done);
                    var b = Interlocked.Add(ref bytesDone, pair.Src.Size);
                    progress?.Report(new SyncProgress(targetRoot, SyncPhase.Comparing, pair.Src.RelativePath, n, compareCandidates.Count, b, totalBytes));
                });
            copies.AddRange(results);
            plan.UnchangedFileCount += compareCandidates.Count - results.Count;
        }

        // Ziel -> Quelle: was es in der Quelle nicht mehr gibt, wird gelöscht
        foreach (var dst in target.Values)
        {
            if (source.ContainsKey(dst.RelativePath)) continue;
            // Quellordner unlesbar: Fehlt die Datei dort wirklich? Unbekannt, also nicht löschen.
            if (sourceUnreadable.Count > 0 && FileTree.IsInsideUnreadable(dst.RelativePath, sourceUnreadable)) continue;
            if (dst.IsDirectory) deleteDirs.Add(new SyncAction(SyncActionKind.DeleteDirectory, dst.RelativePath, 0));
            else deleteFiles.Add(new SyncAction(SyncActionKind.DeleteFile, dst.RelativePath, dst.Size));
        }

        // Ordner, die wegen eines Typkonflikts weichen, werden samt Inhalt in einem Schritt entfernt:
        // Einzelaktionen für ihren Inhalt entfallen.
        var conflictDirs = deleteDirs.Where(a => a.IsTypeConflict).Select(a => a.RelativePath + "\\").ToList();
        bool InsideConflictDir(SyncAction a) =>
            conflictDirs.Any(d => a.RelativePath.StartsWith(d, StringComparison.OrdinalIgnoreCase));
        deleteFiles.RemoveAll(a => !a.IsTypeConflict && InsideConflictDir(a));
        deleteDirs.RemoveAll(a => !a.IsTypeConflict && InsideConflictDir(a));

        var cmp = StringComparer.OrdinalIgnoreCase;
        // Reihenfolge: 1. Typkonflikte aus dem Weg räumen, 2. Ordner flach -> tief anlegen,
        // 3. Dateien kopieren, 4. Dateien löschen, 5. Ordner tief -> flach löschen.
        plan.Actions.AddRange(deleteFiles.Where(a => a.IsTypeConflict).Concat(deleteDirs.Where(a => a.IsTypeConflict)));
        plan.Actions.AddRange(creates.OrderBy(a => Depth(a.RelativePath)).ThenBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(copies.OrderBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(deleteFiles.Where(a => !a.IsTypeConflict).OrderBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(deleteDirs.Where(a => !a.IsTypeConflict).OrderByDescending(a => Depth(a.RelativePath)).ThenBy(a => a.RelativePath, cmp));
        return plan;
    }

    /// <summary>Verhindert gefährliche Kombinationen (Ziel in Quelle oder umgekehrt).</summary>
    public static void ValidatePaths(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
            throw new SyncValidationException(SyncValidationError.SourceMissing, sourceRoot);

        var s = WithSeparator(sourceRoot);
        var t = WithSeparator(targetRoot);
        if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase))
            throw new SyncValidationException(SyncValidationError.TargetEqualsSource, targetRoot);
        if (t.StartsWith(s, StringComparison.OrdinalIgnoreCase))
            throw new SyncValidationException(SyncValidationError.TargetInsideSource, targetRoot);
        if (s.StartsWith(t, StringComparison.OrdinalIgnoreCase))
            throw new SyncValidationException(SyncValidationError.SourceInsideTarget, targetRoot);
    }

    private static string WithSeparator(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

    private static int Depth(string relativePath) => relativePath.Count(c => c == '\\');
}

public enum SyncValidationError
{
    SourceMissing,
    TargetEqualsSource,
    TargetInsideSource,
    SourceInsideTarget,
    TargetUnavailable,
    /// <summary>Automatischer Lauf übersprungen, weil der Löschschutz angeschlagen hat.</summary>
    DeletionProtection,
    /// <summary>Netzwerkfreigabe: Benutzername oder Kennwort falsch.</summary>
    NetworkLogonFailed,
    /// <summary>Netzwerkfreigabe: Windows ist mit dem Server schon unter einem anderen Benutzer verbunden.</summary>
    NetworkCredentialConflict
}

public sealed class SyncValidationException(SyncValidationError error, string path)
    : Exception($"{error}: {path}")
{
    public SyncValidationError Error { get; } = error;
    public string Path { get; } = path;
}
