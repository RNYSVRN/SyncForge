using System.Globalization;
using SyncForge.Core.Filtering;
using SyncForge.Core.Models;

namespace SyncForge.Core.Engine;

/// <summary>
/// Zwei-Wege-Abgleich: Änderungen auf beiden Seiten werden übernommen. Grundlage ist der gemeinsame Stand
/// nach dem letzten Lauf (<see cref="TwoWayState"/>):
/// <list type="bullet">
/// <item>nur auf einer Seite geändert/neu → auf die andere Seite kopieren</item>
/// <item>auf einer Seite gelöscht (und auf der anderen unverändert) → dort ebenfalls löschen</item>
/// <item>auf beiden Seiten geändert → Konflikt, gelöst nach <see cref="ConflictResolution"/></item>
/// <item>auf einer Seite gelöscht, auf der anderen geändert → die geänderte Fassung bleibt und wird zurückkopiert</item>
/// </list>
/// Beim ersten Lauf (kein Stand) wird nur zusammengeführt, nie gelöscht.
/// </summary>
public static class TwoWayPlanner
{
    public static async Task<SyncPlan> CreatePlanAsync(
        string sourceRoot,
        string targetRoot,
        FilterSettings filterSettings,
        CompareMode compareMode,
        ConflictResolution resolution,
        string? statePath,
        string conflictLabel = "Konflikt",
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        targetRoot = Path.GetFullPath(targetRoot);
        SyncPlanner.ValidatePaths(sourceRoot, targetRoot);

        var filter = new PathFilter(filterSettings);
        var skipCloud = filterSettings.CloudFiles == CloudFileMode.Skip;
        progress?.Report(new SyncProgress(targetRoot, SyncPhase.Scanning, null, 0, 0, 0, 0));
        var sourceUnreadable = new List<string>();
        var targetUnreadable = new List<string>();
        var source = await Task.Run(() => FileTree.Scan(sourceRoot, filter, ct, null, sourceUnreadable), ct);
        var targetMissing = !Directory.Exists(targetRoot);
        var target = targetMissing
            ? new Dictionary<string, FileTreeEntry>(StringComparer.OrdinalIgnoreCase)
            : await Task.Run(() => FileTree.Scan(targetRoot, filter, ct, null, targetUnreadable), ct);
        var state = TwoWayState.Load(statePath);

        var plan = new SyncPlan
        {
            SourceRoot = sourceRoot, TargetRoot = targetRoot, TargetRootMissing = targetMissing,
            Mode = SyncMode.TwoWay, StatePath = statePath, Filter = filterSettings
        };
        plan.UnreadableFolders.AddRange(sourceUnreadable.Select(r => Path.Combine(sourceRoot, r)));
        plan.UnreadableFolders.AddRange(targetUnreadable.Select(r => Path.Combine(targetRoot, r)));
        var unreadable = sourceUnreadable.Concat(targetUnreadable).ToList();

        // Ordner auf der einen, Datei auf der anderen Seite: bleibt ein Konflikt, bis der Benutzer ihn löst.
        // Was im Ordner liegt, wird bis dahin nicht angefasst (Kopieren würde ohnehin an der Datei scheitern).
        var typeConflicts = source.Values
            .Where(s => target.TryGetValue(s.RelativePath, out var t) && s.IsDirectory != t.IsDirectory)
            .Select(s => s.RelativePath)
            .ToList();
        plan.ExistingTargetFileCount = target.Values.Count(e => !e.IsDirectory);
        plan.SourceFileCount = source.Values.Count(e => !e.IsDirectory);
        plan.SourceBytes = source.Values.Where(e => !e.IsDirectory).Sum(e => e.Size);

        var renames = new List<SyncAction>();
        var creates = new List<SyncAction>();
        var copies = new List<SyncAction>();
        var deleteFiles = new List<SyncAction>();
        var deleteDirs = new List<SyncAction>();
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture);
        var allPaths = source.Keys.Union(target.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var path in allPaths)
        {
            ct.ThrowIfCancellationRequested();
            // In einem unlesbaren Ordner (auf einer der Seiten) ist der Zustand unbekannt: nichts tun.
            if (unreadable.Count > 0 && FileTree.IsInsideUnreadable(path, unreadable)) continue;
            if (typeConflicts.Count > 0 && FileTree.IsInsideUnreadable(path, typeConflicts)) continue;
            source.TryGetValue(path, out var s);
            target.TryGetValue(path, out var t);
            state.TryGetValue(path, out var b);

            // Ordner
            if (s?.IsDirectory == true || t?.IsDirectory == true)
            {
                if (s is not null && t is not null)
                {
                    if (s.IsDirectory != t.IsDirectory) copies.Add(new SyncAction(SyncActionKind.Conflict, path, 0));
                    continue;
                }
                var present = s ?? t!;
                var presentSide = s is not null ? SyncSide.Source : SyncSide.Target;
                if (b is { IsDirectory: true })
                    deleteDirs.Add(new SyncAction(SyncActionKind.DeleteDirectory, path, 0, Side: presentSide)); // auf der anderen Seite gelöscht
                else
                    creates.Add(new SyncAction(SyncActionKind.CreateDirectory, path, 0, Side: Other(presentSide)));
                _ = present;
                continue;
            }

            // Nur online verfügbar (auf einer der Seiten): nicht herunterladen und nichts ändern –
            // weder kopieren noch löschen. Der Stand bleibt, wie er ist.
            if (skipCloud && (s?.IsCloudOnly == true || t?.IsCloudOnly == true))
            {
                plan.SkippedCloudFiles++;
                plan.SkippedCloudBytes += (s ?? t)!.Size;
                continue;
            }

            // Dateien
            if (s is not null && t is not null)
            {
                var canHash = compareMode == CompareMode.Content && !s.IsCloudOnly && !t.IsCloudOnly;
                if (SameFile(s, t) && (!canHash || await ContentEqualAsync(sourceRoot, targetRoot, path, ct)))
                {
                    plan.UnchangedFileCount++;
                    continue;
                }
                var changedS = b is null || Changed(s, b);
                var changedT = b is null || Changed(t, b);
                if (changedS && !changedT) copies.Add(new SyncAction(SyncActionKind.Update, path, s.Size, Side: SyncSide.Target));
                else if (changedT && !changedS) copies.Add(new SyncAction(SyncActionKind.Update, path, t.Size, Side: SyncSide.Source));
                else ResolveConflict(path, s, t);
            }
            else if (s is not null)
            {
                if (b is not null && !Changed(s, b)) deleteFiles.Add(new SyncAction(SyncActionKind.DeleteFile, path, s.Size, Side: SyncSide.Source));
                else copies.Add(new SyncAction(SyncActionKind.CopyNew, path, s.Size, Side: SyncSide.Target)); // neu oder geändert+gelöscht
            }
            else if (t is not null)
            {
                if (b is not null && !Changed(t, b)) deleteFiles.Add(new SyncAction(SyncActionKind.DeleteFile, path, t.Size, Side: SyncSide.Target));
                else copies.Add(new SyncAction(SyncActionKind.CopyNew, path, t.Size, Side: SyncSide.Source));
            }
        }

        void ResolveConflict(string path, FileTreeEntry s, FileTreeEntry t)
        {
            switch (resolution)
            {
                case ConflictResolution.SourceWins:
                    copies.Add(new SyncAction(SyncActionKind.Update, path, s.Size, Side: SyncSide.Target));
                    break;
                case ConflictResolution.KeepBoth:
                    // Zielfassung umbenennen und auf beide Seiten bringen; Quellfassung ins Ziel kopieren.
                    var conflictPath = ConflictName(path, conflictLabel, stamp);
                    renames.Add(new SyncAction(SyncActionKind.RenameConflict, path, t.Size, Side: SyncSide.Target, NewRelativePath: conflictPath));
                    copies.Add(new SyncAction(SyncActionKind.CopyNew, conflictPath, t.Size, Side: SyncSide.Source));
                    copies.Add(new SyncAction(SyncActionKind.CopyNew, path, s.Size, Side: SyncSide.Target));
                    break;
                case ConflictResolution.Ask:
                    copies.Add(new SyncAction(SyncActionKind.Conflict, path, s.Size));
                    break;
                default: // NewerWins (bei gleichem Datum gewinnt die Quelle)
                    var targetNewer = t.LastWriteUtc > s.LastWriteUtc + SyncPlanner.TimestampTolerance;
                    copies.Add(targetNewer
                        ? new SyncAction(SyncActionKind.Update, path, t.Size, Side: SyncSide.Source)
                        : new SyncAction(SyncActionKind.Update, path, s.Size, Side: SyncSide.Target));
                    break;
            }
        }

        var cmp = StringComparer.OrdinalIgnoreCase;
        plan.Actions.AddRange(renames);
        plan.Actions.AddRange(copies.Where(a => a.Kind == SyncActionKind.Conflict));
        plan.Actions.AddRange(creates.OrderBy(a => Depth(a.RelativePath)).ThenBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(copies.Where(a => a.IsCopy).OrderBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(deleteFiles.OrderBy(a => a.RelativePath, cmp));
        plan.Actions.AddRange(deleteDirs.OrderByDescending(a => Depth(a.RelativePath)).ThenBy(a => a.RelativePath, cmp));
        return plan;
    }

    /// <summary>Speichert nach einem Lauf den neuen gemeinsamen Stand.</summary>
    public static void UpdateState(SyncPlan plan)
    {
        if (plan.StatePath is null || plan.Filter is null) return;
        var filter = new PathFilter(plan.Filter);
        var unreadable = new List<string>();
        var source = FileTree.Scan(plan.SourceRoot, filter, CancellationToken.None, null, unreadable);
        var target = FileTree.Scan(plan.TargetRoot, filter, CancellationToken.None, null, unreadable);
        var state = TwoWayState.Capture(source, target);

        // Unlesbare Bereiche: bisherigen Stand behalten, sonst gälten die Dateien dort beim nächsten Mal als neu.
        if (unreadable.Count > 0)
        {
            foreach (var (path, entry) in TwoWayState.Load(plan.StatePath))
            {
                if (FileTree.IsInsideUnreadable(path, unreadable)) state[path] = entry;
            }
        }
        TwoWayState.Save(plan.StatePath, state);
    }

    internal static bool SameFile(FileTreeEntry a, FileTreeEntry b) =>
        a.Size == b.Size && (a.LastWriteUtc - b.LastWriteUtc).Duration() <= SyncPlanner.TimestampTolerance;

    private static bool Changed(FileTreeEntry e, StateEntry b) =>
        b.IsDirectory || e.Size != b.Size || (e.LastWriteUtc - new DateTime(b.LastWriteUtcTicks, DateTimeKind.Utc)).Duration() > SyncPlanner.TimestampTolerance;

    private static Task<bool> ContentEqualAsync(string sourceRoot, string targetRoot, string path, CancellationToken ct) =>
        FileHasher.AreEqualAsync(Path.Combine(sourceRoot, path), Path.Combine(targetRoot, path), ct);

    /// <summary>"Bericht.docx" → "Bericht (Konflikt 2026-11-15 14-30).docx".</summary>
    internal static string ConflictName(string relativePath, string label, string stamp)
    {
        var dir = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var name = $"{Path.GetFileNameWithoutExtension(relativePath)} ({label} {stamp}){Path.GetExtension(relativePath)}";
        return dir.Length == 0 ? name : Path.Combine(dir, name);
    }

    private static SyncSide Other(SyncSide side) => side == SyncSide.Target ? SyncSide.Source : SyncSide.Target;
    private static int Depth(string relativePath) => relativePath.Count(c => c == '\\');
}
