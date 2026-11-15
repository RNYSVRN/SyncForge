using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

public class TwoWayTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string StatePath(TestFolder f) => Path.Combine(f.Root, "state.json");

    private static async Task<(SyncPlan Plan, SyncResult Result)> Sync(TestFolder f, ConflictResolution resolution = ConflictResolution.NewerWins)
    {
        var ct = TestContext.Current.CancellationToken;
        var plan = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, resolution, StatePath(f), "Konflikt", null, ct);
        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false }).ExecuteAsync(plan, null, ct);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        TwoWayPlanner.UpdateState(plan);
        return (plan, result);
    }

    private static string Read(string root, string rel) => File.ReadAllText(Path.Combine(root, rel));
    private static bool Exists(string root, string rel) => File.Exists(Path.Combine(root, rel));

    [Fact]
    public async Task FirstRun_MergesBothSides_WithoutDeleting()
    {
        using var f = new TestFolder();
        f.Src("nur-quelle.txt", "Q");
        f.Dst("nur-ziel.txt", "Z");
        f.Src(@"ordner\a.txt", "A");

        var (plan, _) = await Sync(f);

        Assert.Equal(0, plan.DeleteFileCount);
        Assert.Equal("Z", Read(f.Source, "nur-ziel.txt"));
        Assert.Equal("Q", Read(f.Target, "nur-quelle.txt"));
        Assert.Equal("A", Read(f.Target, @"ordner\a.txt"));
    }

    [Fact]
    public async Task SecondRun_WithoutChanges_DoesNothing()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        f.Dst("b.txt", "B");
        await Sync(f);
        var (plan, _) = await Sync(f);
        Assert.False(plan.HasChanges);
    }

    [Fact]
    public async Task ChangesOnEitherSide_AreCopiedToTheOther()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A1", T0);
        f.Src("b.txt", "B1", T0);
        await Sync(f);

        f.Src("a.txt", "A2 Quelle", T0.AddHours(1));
        f.Dst("b.txt", "B2 Ziel", T0.AddHours(1));
        var (plan, _) = await Sync(f);

        Assert.Equal(1, plan.ToSourceCount);
        Assert.Equal("A2 Quelle", Read(f.Target, "a.txt"));
        Assert.Equal("B2 Ziel", Read(f.Source, "b.txt"));
    }

    [Fact]
    public async Task DeletionsOnEitherSide_ArePropagated()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        f.Src(@"ordner\b.txt", "B");
        await Sync(f);

        File.Delete(Path.Combine(f.Source, "a.txt"));
        Directory.Delete(Path.Combine(f.Target, "ordner"), recursive: true);
        await Sync(f);

        Assert.False(Exists(f.Target, "a.txt"));
        Assert.False(Exists(f.Source, @"ordner\b.txt"));
        Assert.False(Directory.Exists(Path.Combine(f.Source, "ordner")));
    }

    [Fact]
    public async Task DeletedOnOneSide_ChangedOnOther_KeepsChangedVersion()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A1", T0);
        await Sync(f);

        File.Delete(Path.Combine(f.Target, "a.txt"));
        f.Src("a.txt", "A2 geändert", T0.AddHours(1));
        await Sync(f);

        Assert.Equal("A2 geändert", Read(f.Target, "a.txt"));
        Assert.Equal("A2 geändert", Read(f.Source, "a.txt"));
    }

    private static async Task<TestFolder> ConflictSetup()
    {
        var f = new TestFolder();
        f.Src("doc.txt", "Basis", T0);
        await Sync(f);
        f.Src("doc.txt", "Quelle neu", T0.AddHours(1));
        f.Dst("doc.txt", "Ziel neuer!", T0.AddHours(2));
        return f;
    }

    [Fact]
    public async Task Conflict_NewerWins()
    {
        using var f = await ConflictSetup();
        await Sync(f, ConflictResolution.NewerWins);
        Assert.Equal("Ziel neuer!", Read(f.Source, "doc.txt"));
        Assert.Equal("Ziel neuer!", Read(f.Target, "doc.txt"));
    }

    [Fact]
    public async Task Conflict_SourceWins()
    {
        using var f = await ConflictSetup();
        await Sync(f, ConflictResolution.SourceWins);
        Assert.Equal("Quelle neu", Read(f.Target, "doc.txt"));
    }

    [Fact]
    public async Task Conflict_KeepBoth_CreatesConflictCopyOnBothSides()
    {
        using var f = await ConflictSetup();
        var (plan, result) = await Sync(f, ConflictResolution.KeepBoth);

        Assert.Equal(1, result.Conflicts);
        Assert.Equal("Quelle neu", Read(f.Target, "doc.txt"));
        var copyName = Assert.Single(Directory.GetFiles(f.Target, "doc (Konflikt *).txt"));
        Assert.Equal("Ziel neuer!", File.ReadAllText(copyName));
        Assert.True(Exists(f.Source, Path.GetFileName(copyName)));

        var (next, _) = await Sync(f, ConflictResolution.KeepBoth);
        Assert.False(next.HasChanges);
    }

    [Fact]
    public async Task Conflict_Ask_ChangesNothing_AndIsReportedAgain()
    {
        using var f = await ConflictSetup();
        var (_, result) = await Sync(f, ConflictResolution.Ask);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal("Quelle neu", Read(f.Source, "doc.txt"));
        Assert.Equal("Ziel neuer!", Read(f.Target, "doc.txt"));

        var (again, _) = await Sync(f, ConflictResolution.Ask);
        Assert.Equal(1, again.ConflictCount);
        Assert.False(again.HasChanges); // nur der offene Konflikt
    }

    [Fact]
    public async Task DeletionThreshold_CountsSourceSideToo()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 4; i++) f.Src($"d{i}.txt", "x");
        await Sync(f);
        foreach (var file in Directory.GetFiles(f.Target)) File.Delete(file);

        var plan = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, ConflictResolution.NewerWins,
            StatePath(f), "Konflikt", null, TestContext.Current.CancellationToken);
        Assert.Equal(4, plan.DeleteFileCount);
        Assert.True(plan.ExceedsDeletionThreshold(25)); // 100 % der Quelle würden gelöscht
    }

    [Fact]
    public async Task ProfileRunner_TwoWay_PersistsState()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        var stateDir = Path.Combine(f.Root, "state");
        var profile = new SyncProfile
        {
            Name = "2W", SourcePath = f.Source, Mode = SyncMode.TwoWay, Filter = NoFilter, DeleteToRecycleBin = false,
            Targets = [new SyncTarget { Path = f.Target }]
        };
        var ct = TestContext.Current.CancellationToken;
        var analysis = await ProfileRunner.AnalyzeAsync(profile, null, ct, stateDir);
        await ProfileRunner.ExecuteAsync(analysis, new SyncExecutorOptions { DeleteToRecycleBin = false }, null, ct);
        Assert.Single(Directory.GetFiles(stateDir));

        File.Delete(Path.Combine(f.Target, "a.txt"));
        analysis = await ProfileRunner.AnalyzeAsync(profile, null, ct, stateDir);
        await ProfileRunner.ExecuteAsync(analysis, new SyncExecutorOptions { DeleteToRecycleBin = false }, null, ct);
        Assert.False(File.Exists(Path.Combine(f.Source, "a.txt"))); // Löschung im Ziel wurde übernommen
    }

    [Fact]
    public void ConflictName_KeepsFolderAndExtension()
    {
        Assert.Equal(@"ordner\Bericht (Konflikt 2026-11-15 14-30).docx",
            TwoWayPlanner.ConflictName(@"ordner\Bericht.docx", "Konflikt", "2026-11-15 14-30"));
    }

    [Fact]
    public void VolumeResolver_KeepsPathOfMatchingVolume_AndRejectsUnknown()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory)!;
        var volume = VolumeResolver.GetVolume(system);
        Assert.NotNull(volume);
        var path = Path.Combine(system, "SyncForgeTest");
        Assert.Equal(path, VolumeResolver.Resolve(path, volume.Serial));
        Assert.Equal(path, VolumeResolver.Resolve(path, null));
        Assert.Null(VolumeResolver.Resolve(path, unchecked(volume.Serial + 12345)));
        Assert.Equal(@"\\server\share", VolumeResolver.Resolve(@"\\server\share", 42));
    }
}
