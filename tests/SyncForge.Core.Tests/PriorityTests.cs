using System.Diagnostics;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>Niedrige E/A-Priorität und Bandbreitenlimit.</summary>
public class PriorityTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void LowPriority_CanBeSetOnFileHandle()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            Assert.True(IoPriority.TrySetLow(stream.SafeFileHandle));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LowPriority_CopiesNormally()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 20; i++) f.Src($"f{i}.txt", new string('x', 10_000));
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, Ct);

        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false, LowPriorityIo = true }).ExecuteAsync(plan, null, Ct);

        Assert.True(result.Success);
        Assert.Equal(20, Directory.GetFiles(f.Target).Length);
    }

    [Fact]
    public async Task BandwidthLimit_SlowsDownCopy()
    {
        using var f = new TestFolder();
        var data = new byte[600 * 1024];
        new Random(1).NextBytes(data);
        File.WriteAllBytes(Path.Combine(f.Source, "gross.bin"), data);
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, Ct);

        var sw = Stopwatch.StartNew();
        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false, BandwidthLimitKBps = 1024 }).ExecuteAsync(plan, null, Ct);

        Assert.True(result.Success);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(400), $"Zu schnell für 1 MB/s: {sw.Elapsed}"); // 600 KB bei 1024 KB/s ≈ 0,6 s
    }
}
