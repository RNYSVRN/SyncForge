using System.Diagnostics;

namespace SyncForge.Core.Engine;

public sealed class SyncExecutorOptions
{
    public int MaxParallelCopies { get; init; } = 4;
    /// <summary>0 = unbegrenzt.</summary>
    public int BandwidthLimitKBps { get; init; }
    public bool VerifyAfterCopy { get; init; }
    public bool DeleteToRecycleBin { get; init; } = true;
}

/// <summary>
/// Führt einen <see cref="SyncPlan"/> aus. Kopien landen zuerst in einer Temp-Datei im Zielordner und
/// ersetzen das Original erst nach vollständigem (und optional geprüftem) Schreiben – ein Abbruch hinterlässt
/// nie halbe Dateien. Fehler einzelner Dateien brechen den Lauf nicht ab, sie werden gesammelt.
/// </summary>
public sealed class SyncExecutor
{
    internal const string TempPrefix = ".~sf_";
    private const int BufferSize = 1024 * 1024;

    private readonly SyncExecutorOptions _options;
    private readonly BandwidthThrottle? _throttle;

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
        var useRecycleBin = _options.DeleteToRecycleBin && RecycleBin.IsSupported(plan.TargetRoot);

        try
        {
            Directory.CreateDirectory(plan.TargetRoot);

            // 1. Typkonflikte + Ordner anlegen (sequenziell, Reihenfolge wichtig)
            foreach (var action in plan.Actions.Where(a => a.IsTypeConflict || a.Kind == SyncActionKind.CreateDirectory))
            {
                ct.ThrowIfCancellationRequested();
                Run(result, action, () =>
                {
                    var path = Combine(plan.TargetRoot, action.RelativePath);
                    switch (action.Kind)
                    {
                        case SyncActionKind.CreateDirectory:
                            Directory.CreateDirectory(path);
                            result.CreatedDirectories++;
                            break;
                        case SyncActionKind.DeleteFile:
                            DeleteFile(path, useRecycleBin);
                            result.DeletedFiles++;
                            break;
                        case SyncActionKind.DeleteDirectory:
                            DeleteDirectory(path, recursive: true, useRecycleBin);
                            result.DeletedDirectories++;
                            break;
                    }
                    return true;
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
                            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size));
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
                            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, ex.Message));
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
                    var path = Combine(plan.TargetRoot, action.RelativePath);
                    if (action.Kind == SyncActionKind.DeleteFile)
                    {
                        if (!File.Exists(path)) return false;
                        DeleteFile(path, useRecycleBin);
                        result.DeletedFiles++;
                        return true;
                    }
                    // Nicht rekursiv: enthält der Ordner ausgeschlossene Dateien, bleibt er bestehen.
                    if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any()) return false;
                    DeleteDirectory(path, recursive: false, useRecycleBin);
                    result.DeletedDirectories++;
                    return true;
                });
                reporter.ItemDone(action.RelativePath, SyncPhase.Deleting);
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

    /// <summary>Führt eine sequenzielle Aktion aus und protokolliert sie (work gibt false zurück = nichts zu tun).</summary>
    private static void Run(SyncResult result, SyncAction action, Func<bool> work)
    {
        try
        {
            if (work()) result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Errors.Add(new SyncError(action.RelativePath, ex.Message));
            result.Files.Add(new SyncFileRecord(action.Kind, action.RelativePath, action.Size, ex.Message));
        }
    }

    private async Task CopyFileAsync(SyncPlan plan, SyncAction action, ProgressReporter reporter, CancellationToken ct)
    {
        var source = Combine(plan.SourceRoot, action.RelativePath);
        var target = Combine(plan.TargetRoot, action.RelativePath);
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
                var attrs = File.GetAttributes(target);
                if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(target, attrs & ~FileAttributes.ReadOnly);
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

    private static void DeleteFile(string path, bool useRecycleBin)
    {
        var attrs = File.GetAttributes(path);
        if (attrs.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        if (useRecycleBin) RecycleBin.Delete(path);
        else File.Delete(path);
    }

    private static void DeleteDirectory(string path, bool recursive, bool useRecycleBin)
    {
        if (useRecycleBin) RecycleBin.Delete(path);
        else Directory.Delete(path, recursive);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* Aufräumen ist best effort */ }
    }

    private static string Combine(string root, string relative) => Path.Combine(root, relative);

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
