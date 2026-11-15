using System.Diagnostics;
using SyncForge.Core.Versioning;

namespace SyncForge.Core.Engine;

public sealed class SyncExecutorOptions
{
    public int MaxParallelCopies { get; init; } = 4;
    /// <summary>0 = unbegrenzt.</summary>
    public int BandwidthLimitKBps { get; init; }
    public bool VerifyAfterCopy { get; init; }
    public bool DeleteToRecycleBin { get; init; } = true;

    /// <summary>Überschriebene/gelöschte Dateien in den Versionsordner verschieben statt löschen.</summary>
    public bool KeepVersions { get; init; }
    public int VersionsToKeep { get; init; } = 10;
    public int VersionMaxAgeDays { get; init; } = 90;
}

/// <summary>
/// Führt einen <see cref="SyncPlan"/> aus. Kopien landen zuerst in einer Temp-Datei im Zielordner und
/// ersetzen das Original erst nach vollständigem (und optional geprüftem) Schreiben – ein Abbruch hinterlässt
/// nie halbe Dateien. Fehler einzelner Dateien brechen den Lauf nicht ab, sie werden gesammelt.
/// Jede Aktion verändert die Seite <see cref="SyncAction.Side"/>; Kopien kommen von der jeweils anderen Seite.
/// </summary>
public sealed class SyncExecutor
{
    internal const string TempPrefix = ".~sf_";
    private const int BufferSize = 1024 * 1024;

    private readonly SyncExecutorOptions _options;
    private readonly BandwidthThrottle? _throttle;
    private readonly DateTimeOffset _versionStamp = DateTimeOffset.Now;

    public SyncExecutor(SyncExecutorOptions options)
    {
        _options = options;
        _throttle = options.BandwidthLimitKBps > 0 ? new BandwidthThrottle(options.BandwidthLimitKBps * 1024L) : null;
    }

    public async Task<SyncResult> ExecuteAsync(SyncPlan plan, IProgress<SyncProgress>? progress = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new SyncResult { TargetRoot = plan.TargetRoot };
        var reporter = new ProgressReporter(plan, progress);
        var recycle = new Dictionary<SyncSide, bool>
        {
            [SyncSide.Target] = _options.DeleteToRecycleBin && RecycleBin.IsSupported(plan.TargetRoot),
            [SyncSide.Source] = _options.DeleteToRecycleBin && RecycleBin.IsSupported(plan.SourceRoot)
        };

        try
        {
            Directory.CreateDirectory(plan.TargetRoot);

            // 1. Konflikte/Typkonflikte auflösen und Ordner anlegen (sequenziell, Reihenfolge wichtig)
            foreach (var action in plan.Actions.Where(a =>
                         a.IsTypeConflict || a.Kind is SyncActionKind.CreateDirectory or SyncActionKind.RenameConflict or SyncActionKind.Conflict))
            {
                ct.ThrowIfCancellationRequested();
                Run(result, action, () =>
                {
                    var root = RootOf(plan, action.Side);
                    var path = Path.Combine(root, action.RelativePath);
                    switch (action.Kind)
                    {
                        case SyncActionKind.CreateDirectory:
                            Directory.CreateDirectory(path);
                            result.CreatedDirectories++;
                            return true;
                        case SyncActionKind.RenameConflict:
                            File.Move(path, Path.Combine(root, action.NewRelativePath!));
                            result.Conflicts++;
                            return true;
                        case SyncActionKind.Conflict:
                            result.Conflicts++;
                            return true; // nur protokollieren
                        case SyncActionKind.DeleteFile:
                            RemoveFile(root, action.RelativePath, recycle[action.Side]);
                            result.DeletedFiles++;
                            return true;
                        case SyncActionKind.DeleteDirectory:
                            if (recycle[action.Side]) RecycleBin.Delete(path);
                            else Directory.Delete(path, recursive: true);
                            result.DeletedDirectories++;
                            return true;
                    }
                    return false;
                });
                reporter.ItemDone(action.RelativePath, SyncPhase.Copying);
            }

            // 2. Dateien kopieren (parallel)
            var copies = plan.Actions.Where(a => a.IsCopy).ToList();
            await Parallel.ForEachAsync(copies,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(_options.MaxParallelCopies, 1, 16), CancellationToken = ct },
                async (action, token) =>
                {
                    try
                    {
                        await CopyFileAsync(plan, action, reporter, token);
                        lock (result)
                        {
                            if (action.Kind == SyncActionKind.CopyNew) result.Copied++;
                            else result.Updated++;
                            result.BytesCopied += action.Size;
                            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, Side: action.Side));
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lock (result)
                        {
                            result.Errors.Add(new SyncError(action.RelativePath, ex.Message));
                            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, ex.Message, action.Side));
                        }
                    }
                    reporter.ItemDone(action.RelativePath, SyncPhase.Copying);
                });

            // 3. Löschen (Dateien, dann Ordner tief -> flach)
            foreach (var action in plan.Actions.Where(a => a.IsDelete && !a.IsTypeConflict))
            {
                ct.ThrowIfCancellationRequested();
                Run(result, action, () =>
                {
                    var root = RootOf(plan, action.Side);
                    var path = Path.Combine(root, action.RelativePath);
                    if (action.Kind == SyncActionKind.DeleteFile)
                    {
                        if (!File.Exists(path)) return false;
                        RemoveFile(root, action.RelativePath, recycle[action.Side]);
                        result.DeletedFiles++;
                        return true;
                    }
                    // Nicht rekursiv: enthält der Ordner ausgeschlossene Dateien, bleibt er bestehen.
                    if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any()) return false;
                    if (recycle[action.Side]) RecycleBin.Delete(path);
                    else Directory.Delete(path);
                    result.DeletedDirectories++;
                    return true;
                });
                reporter.ItemDone(action.RelativePath, SyncPhase.Deleting);
            }

            if (_options.KeepVersions)
            {
                VersionStore.Prune(plan.TargetRoot, _options.VersionsToKeep, _options.VersionMaxAgeDays);
                if (plan.Actions.Any(a => a.Side == SyncSide.Source))
                    VersionStore.Prune(plan.SourceRoot, _options.VersionsToKeep, _options.VersionMaxAgeDays);
            }
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new SyncError(string.Empty, ex.Message));
        }

        result.Duration = sw.Elapsed;
        reporter.Finish();
        return result;
    }

    private static string RootOf(SyncPlan plan, SyncSide side) => side == SyncSide.Target ? plan.TargetRoot : plan.SourceRoot;
    private static string OtherRoot(SyncPlan plan, SyncSide side) => side == SyncSide.Target ? plan.SourceRoot : plan.TargetRoot;

    /// <summary>Führt eine sequenzielle Aktion aus und protokolliert sie (work gibt false zurück = nichts zu tun).</summary>
    private static void Run(SyncResult result, SyncAction action, Func<bool> work)
    {
        try
        {
            if (work()) result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, Side: action.Side));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors.Add(new SyncError(action.RelativePath, ex.Message));
            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, ex.Message, action.Side));
        }
    }

    private async Task CopyFileAsync(SyncPlan plan, SyncAction action, ProgressReporter reporter, CancellationToken ct)
    {
        var toRoot = RootOf(plan, action.Side);
        var source = Path.Combine(OtherRoot(plan, action.Side), action.RelativePath);
        var target = Path.Combine(toRoot, action.RelativePath);
        var targetDir = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(targetDir);
        var temp = Path.Combine(targetDir, $"{TempPrefix}{Guid.NewGuid():N}.tmp");

        try
        {
            var buffer = new byte[BufferSize];
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                             BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (input.Length > 0) output.SetLength(input.Length);
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    reporter.Bytes(read, action.RelativePath);
                    if (_throttle is not null) await _throttle.WaitAsync(read, ct);
                }
                output.SetLength(output.Position);
            }

            if (_options.VerifyAfterCopy && !await FileHasher.AreEqualAsync(source, temp, ct))
            {
                throw new IOException("Prüfsumme der Kopie stimmt nicht mit der Quelle überein.");
            }

            var info = new FileInfo(source);
            File.SetCreationTimeUtc(temp, info.CreationTimeUtc);
            File.SetLastWriteTimeUtc(temp, info.LastWriteTimeUtc);

            if (File.Exists(target))
            {
                if (_options.KeepVersions)
                {
                    VersionStore.MoveToVersions(toRoot, action.RelativePath, _versionStamp);
                }
                else
                {
                    var attrs = File.GetAttributes(target);
                    if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
                }
            }
            File.Move(temp, target, overwrite: true);
            File.SetAttributes(target, info.Attributes & ~(FileAttributes.Archive | FileAttributes.Normal) | FileAttributes.Archive);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>Löscht eine Datei – bzw. verschiebt sie in den Versionsordner oder Papierkorb.</summary>
    private void RemoveFile(string root, string relativePath, bool useRecycleBin)
    {
        if (_options.KeepVersions)
        {
            VersionStore.MoveToVersions(root, relativePath, _versionStamp);
            return;
        }
        var path = Path.Combine(root, relativePath);
        var attrs = File.GetAttributes(path);
        if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        if (useRecycleBin) RecycleBin.Delete(path);
        else File.Delete(path);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* Aufräumen ist best effort */ }
    }

    /// <summary>Begrenzt die Gesamtrate aller parallelen Kopien.</summary>
    private sealed class BandwidthThrottle(long bytesPerSecond)
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private long _bytes;

        public async Task WaitAsync(int bytes, CancellationToken ct)
        {
            var total = Interlocked.Add(ref _bytes, bytes);
            var expected = TimeSpan.FromSeconds((double)total / bytesPerSecond);
            var delay = expected - _sw.Elapsed;
            if (delay > TimeSpan.FromMilliseconds(10)) await Task.Delay(delay, ct);
        }
    }

    /// <summary>Sammelt Fortschritt threadsicher und meldet höchstens alle 100 ms.</summary>
    private sealed class ProgressReporter(SyncPlan plan, IProgress<SyncProgress>? progress)
    {
        private readonly int _itemsTotal = plan.Actions.Count;
        private readonly long _bytesTotal = plan.BytesToCopy;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private int _items;
        private long _bytes;
        private long _lastReportMs = -1000;

        public void Bytes(int count, string file)
        {
            Interlocked.Add(ref _bytes, count);
            Report(SyncPhase.Copying, file, force: false);
        }

        public void ItemDone(string file, SyncPhase phase)
        {
            Interlocked.Increment(ref _items);
            Report(phase, file, force: false);
        }

        public void Finish() => Report(SyncPhase.Finished, null, force: true);

        private void Report(SyncPhase phase, string? file, bool force)
        {
            if (progress is null) return;
            var now = _sw.ElapsedMilliseconds;
            var last = Interlocked.Read(ref _lastReportMs);
            if (!force && now - last < 100) return;
            if (!force && Interlocked.CompareExchange(ref _lastReportMs, now, last) != last) return;
            progress.Report(new SyncProgress(plan.TargetRoot, phase, file,
                Volatile.Read(ref _items), _itemsTotal, Interlocked.Read(ref _bytes), _bytesTotal));
        }
    }
}
