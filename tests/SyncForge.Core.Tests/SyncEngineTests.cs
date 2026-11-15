using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

public class SyncEngineTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static readonly SyncExecutorOptions Opts = new() { DeleteToRecycleBin = false, MaxParallelCopies = 4 };

    private static Task<SyncPlan> Plan(TestFolder f, FilterSettings? filter = null, CompareMode mode = CompareMode.SizeAndDate) =>
        SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, filter ?? NoFilter, mode, null, TestContext.Current.CancellationToken);

    private static async Task<(SyncPlan Plan, SyncResult Result)> Mirror(TestFolder f, FilterSettings? filter = null, SyncExecutorOptions? opts = null)
    {
        var plan = await Plan(f, filter);
        var result = await new SyncExecutor(opts ?? Opts).ExecuteAsync(plan, null, TestContext.Current.CancellationToken);
        return (plan, result);
    }

    private static string[] Tree(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p) + (Directory.Exists(p) ? "\\" : "=" + File.ReadAllText(p)))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    [Fact]
    public async Task Mirror_IntoMissingTarget_CopiesEverything()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        f.Src(@"sub\b.txt", "B");
        f.Src(@"sub\deep\c.txt", "C");
        Directory.CreateDirectory(Path.Combine(f.Source, "leer"));

        var (plan, result) = await Mirror(f);

        Assert.True(plan.TargetRootMissing);
        Assert.Equal(3, plan.CopyNewCount);
        Assert.True(result.Success);
        Assert.Equal(Tree(f.Source), Tree(f.Target));
    }

    [Fact]
    public async Task Mirror_UpdatesChangedAndDeletesRemoved()
    {
        using var f = new TestFolder();
        f.Src("gleich.txt", "same");
        f.Dst("gleich.txt", "same");
        f.Src("geaendert.txt", "neu, laenger");
        f.Dst("geaendert.txt", "alt");
        f.Src("datum.txt", "abc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        f.Dst("datum.txt", "xyz", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        f.Dst("weg.txt", "x");
        f.Dst(@"alterOrdner\x.txt", "x");

        var (plan, result) = await Mirror(f);

        Assert.Equal(2, plan.UpdateCount);
        Assert.Equal(2, plan.DeleteFileCount);
        Assert.Equal(1, plan.DeleteDirectoryCount);
        Assert.Equal(1, plan.UnchangedFileCount);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(Tree(f.Source), Tree(f.Target));
    }

    [Fact]
    public async Task Mirror_SecondRun_HasNoChanges()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        f.Src(@"sub\b.txt", "B");
        await Mirror(f);

        var plan = await Plan(f);
        Assert.False(plan.HasChanges);
        Assert.Equal(2, plan.UnchangedFileCount);
    }

    [Fact]
    public async Task TimestampWithinTolerance_IsUnchanged()
    {
        using var f = new TestFolder();
        var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        f.Src("a.txt", "A", t);
        f.Dst("a.txt", "A", t.AddSeconds(1.5));
        Assert.False((await Plan(f)).HasChanges);
    }

    [Fact]
    public async Task ContentCompare_DetectsSameSizeDifferentContent()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "AAAA");
        f.Dst("a.txt", "BBBB"); // gleiche Größe, gleiches Datum
        Assert.False((await Plan(f, mode: CompareMode.SizeAndDate)).HasChanges);
        Assert.Equal(1, (await Plan(f, mode: CompareMode.Content)).UpdateCount);
    }

    [Fact]
    public async Task ExcludedFiles_AreNeitherCopiedNorDeleted()
    {
        using var f = new TestFolder();
        var filter = new FilterSettings { ExcludePatterns = ["*.tmp", @"cache\"], ExcludeSystem = false };
        f.Src("keep.txt", "k");
        f.Src("skip.tmp", "s");
        f.Src(@"cache\x.bin", "c");
        f.Dst("nurImZiel.tmp", "bleibt");
        f.Dst(@"cache\alt.bin", "bleibt");

        var (plan, result) = await Mirror(f, filter);

        Assert.Equal(1, plan.CopyNewCount);
        Assert.Equal(0, plan.DeleteFileCount);
        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(f.Target, "nurImZiel.tmp")));
        Assert.True(File.Exists(Path.Combine(f.Target, @"cache\alt.bin")));
        Assert.False(File.Exists(Path.Combine(f.Target, "skip.tmp")));
    }

    [Fact]
    public async Task DeletedDirectory_WithExcludedContent_IsKept()
    {
        using var f = new TestFolder();
        var filter = new FilterSettings { ExcludePatterns = ["*.tmp"], ExcludeSystem = false };
        f.Dst(@"ordner\a.txt", "weg");
        f.Dst(@"ordner\b.tmp", "bleibt");

        var (_, result) = await Mirror(f, filter);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.False(File.Exists(Path.Combine(f.Target, @"ordner\a.txt")));
        Assert.True(File.Exists(Path.Combine(f.Target, @"ordner\b.tmp")));
    }

    [Fact]
    public async Task DeletionThreshold_IsDetected()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 10; i++) f.Dst($"d{i}.txt", "x");
        f.Src("d0.txt", "x");
        f.Dst("d0.txt", "x");

        var plan = await Plan(f);
        Assert.Equal(9, plan.DeleteFileCount);
        Assert.Equal(90, plan.DeletePercent, 1);
        Assert.True(plan.ExceedsDeletionThreshold(25));
        Assert.False(plan.ExceedsDeletionThreshold(95));
        Assert.False(plan.ExceedsDeletionThreshold(0)); // 0 = aus
    }

    [Fact]
    public async Task TypeConflicts_AreResolved()
    {
        using var f = new TestFolder();
        f.Src(@"wirdOrdner\inhalt.txt", "1");
        f.Dst("wirdOrdner", "war Datei");
        f.Src("wirdDatei", "jetzt Datei");
        f.Dst(@"wirdDatei\alt\x.txt", "war Ordner");

        var (_, result) = await Mirror(f);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(Tree(f.Source), Tree(f.Target));
    }

    [Fact]
    public async Task ReadOnlyTargetFile_IsUpdated()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "neu!");
        var dst = f.Dst("a.txt", "alt");
        File.SetAttributes(dst, FileAttributes.ReadOnly);

        var (_, result) = await Mirror(f);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal("neu!", File.ReadAllText(dst));
    }

    [Fact]
    public async Task VerifyAfterCopy_Succeeds()
    {
        using var f = new TestFolder();
        f.Src("gross.bin", new string('x', 3_000_000));
        var (_, result) = await Mirror(f, opts: new SyncExecutorOptions { DeleteToRecycleBin = false, VerifyAfterCopy = true });
        Assert.True(result.Success);
        Assert.Equal(3_000_000, new FileInfo(Path.Combine(f.Target, "gross.bin")).Length);
    }

    [Fact]
    public async Task CopiedFiles_KeepTimestamp_AndNoTempFilesRemain()
    {
        using var f = new TestFolder();
        var t = new DateTime(2025, 6, 15, 8, 30, 0, DateTimeKind.Utc);
        f.Src("a.txt", "A", t);
        await Mirror(f);
        Assert.Equal(t, File.GetLastWriteTimeUtc(Path.Combine(f.Target, "a.txt")));
        Assert.Empty(Directory.GetFiles(f.Target, ".~sf_*"));
    }

    [Theory]
    [InlineData("target", SyncValidationError.TargetEqualsSource)]
    [InlineData(@"source\inner", SyncValidationError.TargetInsideSource)]
    public void InvalidTargetPaths_AreRejected(string targetKind, SyncValidationError expected)
    {
        using var f = new TestFolder();
        var target = targetKind == "target" ? f.Source : Path.Combine(f.Root, targetKind);
        var ex = Assert.Throws<SyncValidationException>(() => SyncPlanner.ValidatePaths(f.Source, target));
        Assert.Equal(expected, ex.Error);
    }

    [Fact]
    public void SourceInsideTarget_IsRejected()
    {
        using var f = new TestFolder();
        var ex = Assert.Throws<SyncValidationException>(() => SyncPlanner.ValidatePaths(f.Source, f.Root));
        Assert.Equal(SyncValidationError.SourceInsideTarget, ex.Error);
    }

    [Fact]
    public async Task ProfileRunner_SkipsUnavailableTarget_AndSyncsOthers()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        var profile = new SyncProfile
        {
            Name = "Test", SourcePath = f.Source, Filter = NoFilter, DeleteToRecycleBin = false,
            Targets =
            [
                new SyncTarget { Path = f.Target },
                new SyncTarget { Path = @"Q:\gibt-es-nicht\backup" }
            ]
        };

        var ct = TestContext.Current.CancellationToken;
        var analysis = await ProfileRunner.AnalyzeAsync(profile, null, ct);
        var run = await ProfileRunner.ExecuteAsync(analysis, Opts, null, ct);

        Assert.Single(run.Results);
        Assert.Single(run.Skipped);
        Assert.Equal(RunStatus.Warning, run.Status);
        Assert.True(File.Exists(Path.Combine(f.Target, "a.txt")));
    }

    [Fact]
    public async Task Cancellation_LeavesNoPartialFiles()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 20; i++) f.Src($"f{i}.bin", new string('x', 2_000_000));
        var plan = await Plan(f);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false, MaxParallelCopies = 1, BandwidthLimitKBps = 5000 })
            .ExecuteAsync(plan, null, cts.Token);

        Assert.True(result.Cancelled);
        Assert.Empty(Directory.GetFiles(f.Target, ".~sf_*"));
        foreach (var copied in Directory.GetFiles(f.Target))
            Assert.Equal(2_000_000, new FileInfo(copied).Length);
    }
}
