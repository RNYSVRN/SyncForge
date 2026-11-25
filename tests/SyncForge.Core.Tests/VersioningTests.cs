using SyncForge.Core.Engine;
using SyncForge.Core.Models;
using SyncForge.Core.Versioning;

namespace SyncForge.Core.Tests;

public class VersioningTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static SyncExecutorOptions Versioned(int keep = 10) => new() { DeleteToRecycleBin = false, KeepVersions = true, VersionsToKeep = keep, VersionMaxAgeDays = 0 };

    private static async Task Mirror(TestFolder f, SyncExecutorOptions opts)
    {
        var ct = TestContext.Current.CancellationToken;
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, ct);
        var result = await new SyncExecutor(opts).ExecuteAsync(plan, null, ct);
        Assert.True(result.Success, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task OverwrittenAndDeletedFiles_GoToVersions()
    {
        using var f = new TestFolder();
        f.Src(@"docs\bericht.txt", "neu, länger");
        f.Dst(@"docs\bericht.txt", "alt");
        f.Dst("weg.txt", "gelöscht");

        await Mirror(f, Versioned());

        Assert.Equal("neu, länger", File.ReadAllText(Path.Combine(f.Target, @"docs\bericht.txt")));
        Assert.False(File.Exists(Path.Combine(f.Target, "weg.txt")));

        var files = VersionStore.ListFiles(f.Target);
        Assert.Equal(2, files.Count);
        var bericht = Assert.Single(VersionStore.GetVersions(f.Target, @"docs\bericht.txt"));
        Assert.Equal("alt", File.ReadAllText(bericht.VersionPath));
        Assert.False(files.Single(x => x.RelativePath == "weg.txt").CurrentExists);
    }

    [Fact]
    public async Task VersionsFolder_IsIgnoredByMirror()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "Fassung 2");
        f.Dst("a.txt", "1");
        await Mirror(f, Versioned());

        // Zweiter Lauf: Versionsordner darf weder als "zu löschen" erkannt noch angefasst werden.
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, TestContext.Current.CancellationToken);
        Assert.False(plan.HasChanges);
        Assert.True(Directory.Exists(VersionStore.VersionsRoot(f.Target)));
        Assert.True(new DirectoryInfo(VersionStore.VersionsRoot(f.Target)).Attributes.HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public void Restore_CopiesVersionBack_AndKeepsIt()
    {
        using var f = new TestFolder();
        f.Dst("a.txt", "Fassung 1");
        VersionStore.MoveToVersions(f.Target, "a.txt", new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
        var v = Assert.Single(VersionStore.GetVersions(f.Target, "a.txt"));

        var restored = Path.Combine(f.Source, "wiederhergestellt", "a.txt");
        VersionStore.Restore(v, restored);

        Assert.Equal("Fassung 1", File.ReadAllText(restored));
        Assert.True(File.Exists(v.VersionPath));
    }

    [Fact]
    public void Versions_AreSortedNewestFirst_AndParsedCorrectly()
    {
        using var f = new TestFolder();
        for (var i = 1; i <= 3; i++)
        {
            f.Dst("archiv.tar.gz", $"v{i}");
            VersionStore.MoveToVersions(f.Target, "archiv.tar.gz", new DateTimeOffset(2026, 11, i, 12, 0, 0, TimeSpan.Zero));
        }
        f.Dst("README", "ohne Endung");
        VersionStore.MoveToVersions(f.Target, "README", DateTimeOffset.Now);

        var versions = VersionStore.GetVersions(f.Target, "archiv.tar.gz");
        Assert.Equal(3, versions.Count);
        Assert.Equal("v3", File.ReadAllText(versions[0].VersionPath));
        Assert.Single(VersionStore.GetVersions(f.Target, "README"));
        Assert.Equal(2, VersionStore.ListFiles(f.Target).Count);
        Assert.Single(VersionStore.ListFiles(f.Target, "readme"));
    }

    [Fact]
    public void SameSecond_DoesNotOverwriteVersion()
    {
        using var f = new TestFolder();
        var ts = DateTimeOffset.Now;
        f.Dst("a.txt", "1");
        VersionStore.MoveToVersions(f.Target, "a.txt", ts);
        f.Dst("a.txt", "2");
        VersionStore.MoveToVersions(f.Target, "a.txt", ts);
        Assert.Equal(2, VersionStore.GetVersions(f.Target, "a.txt").Count);
    }

    [Fact]
    public void Prune_KeepsNewestPerFile_AndRemovesOld()
    {
        using var f = new TestFolder();
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
        {
            f.Dst("a.txt", $"{i}");
            VersionStore.MoveToVersions(f.Target, "a.txt", now.AddDays(-i));
        }
        f.Dst(@"sub\alt.txt", "x");
        VersionStore.MoveToVersions(f.Target, @"sub\alt.txt", now.AddDays(-100));

        var removed = VersionStore.Prune(f.Target, keepPerFile: 3, maxAgeDays: 90, now: now);

        Assert.Equal(3, removed);
        Assert.Equal(3, VersionStore.GetVersions(f.Target, "a.txt").Count);
        Assert.Empty(VersionStore.GetVersions(f.Target, @"sub\alt.txt"));
        Assert.False(Directory.Exists(Path.Combine(VersionStore.VersionsRoot(f.Target), "sub"))); // leerer Ordner entfernt
    }

    [Fact]
    public async Task Executor_PrunesAfterRun()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 4; i++)
        {
            f.Src("a.txt", new string('x', i + 1), DateTime.UtcNow.AddMinutes(i));
            await Mirror(f, Versioned(keep: 2));
        }
        Assert.Equal(2, VersionStore.GetVersions(f.Target, "a.txt").Count);
    }
}
