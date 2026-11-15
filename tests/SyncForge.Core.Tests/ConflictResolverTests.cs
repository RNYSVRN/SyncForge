using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

public class ConflictResolverTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<SyncPlan> Analyze(TestFolder f, string state) =>
        TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, ConflictResolution.Ask, state, "Konflikt", null, Ct);

    private static async Task Run(SyncPlan plan)
    {
        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false }).ExecuteAsync(plan, null, Ct);
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        TwoWayPlanner.UpdateState(plan);
    }

    /// <summary>Gemeinsamer Stand, danach "bericht.txt" auf beiden Seiten unterschiedlich geändert.</summary>
    private static async Task<(TestFolder F, string State, SyncPlan Plan)> WithConflict()
    {
        var f = new TestFolder();
        var state = Path.Combine(f.Root, "state.json");
        f.Src("bericht.txt", "Basis");
        f.Src("anderes.txt", "x");
        await Run(await Analyze(f, state));

        f.Src("bericht.txt", "Quelle, neu", new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        f.Dst("bericht.txt", "Ziel, neuer!", new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));
        return (f, state, await Analyze(f, state));
    }

    [Fact]
    public async Task List_ShowsBothVersions_AndWhichIsNewer()
    {
        var (f, _, plan) = await WithConflict();
        using var _ = f;

        var c = Assert.Single(ConflictResolver.List(plan));
        Assert.Equal("bericht.txt", c.RelativePath);
        Assert.True(c.CanResolve);
        Assert.Equal("Quelle, neu".Length, c.Source!.Size);
        Assert.Equal("Ziel, neuer!".Length, c.Target!.Size);
        Assert.Equal(SyncSide.Target, c.Newer);
    }

    [Theory]
    [InlineData(ConflictChoice.KeepSource, "Quelle, neu")]
    [InlineData(ConflictChoice.KeepTarget, "Ziel, neuer!")]
    public async Task KeepOneSide_BothSidesEqual_AndConflictIsGone(ConflictChoice choice, string expected)
    {
        var (f, state, plan) = await WithConflict();
        using var _ = f;

        await Run(ConflictResolver.BuildPlan(plan, ConflictResolver.List(plan).Select(c => (c, choice)), "Konflikt"));

        Assert.Equal(expected, File.ReadAllText(Path.Combine(f.Source, "bericht.txt")));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(f.Target, "bericht.txt")));
        var again = await Analyze(f, state);
        Assert.Equal(0, again.ConflictCount);
        Assert.False(again.HasChanges);
    }

    [Fact]
    public async Task KeepBoth_RenamesTargetVersion_OnBothSides()
    {
        var (f, state, plan) = await WithConflict();
        using var _ = f;

        await Run(ConflictResolver.BuildPlan(plan, ConflictResolver.List(plan).Select(c => (c, ConflictChoice.KeepBoth)), "Konflikt"));

        Assert.Equal("Quelle, neu", File.ReadAllText(Path.Combine(f.Source, "bericht.txt")));
        Assert.Equal("Quelle, neu", File.ReadAllText(Path.Combine(f.Target, "bericht.txt")));
        var copySource = Assert.Single(Directory.GetFiles(f.Source, "bericht (Konflikt *).txt"));
        var copyTarget = Assert.Single(Directory.GetFiles(f.Target, "bericht (Konflikt *).txt"));
        Assert.Equal("Ziel, neuer!", File.ReadAllText(copySource));
        Assert.Equal("Ziel, neuer!", File.ReadAllText(copyTarget));
        Assert.Equal(0, (await Analyze(f, state)).ConflictCount);
    }

    [Fact]
    public async Task Skip_LeavesConflictOpen()
    {
        var (f, state, plan) = await WithConflict();
        using var _ = f;

        var resolve = ConflictResolver.BuildPlan(plan, ConflictResolver.List(plan).Select(c => (c, ConflictChoice.Skip)), "Konflikt");
        Assert.Empty(resolve.Actions);
        await Run(resolve);

        Assert.Equal(1, (await Analyze(f, state)).ConflictCount);
    }

    [Fact]
    public async Task TypeConflict_ContentsAreLeftAlone_NoErrors()
    {
        using var f = new TestFolder();
        var state = Path.Combine(f.Root, "state.json");
        f.Src(@"ding\a.txt", "Ordner in der Quelle");
        f.Src(@"ding\unter\b.txt", "tiefer");
        f.Dst("ding", "Datei im Ziel");
        f.Src("normal.txt", "wird kopiert");

        var plan = await Analyze(f, state);

        // Nur der Konflikt selbst, nichts aus dem Ordner (das würde bei jedem Lauf scheitern).
        Assert.DoesNotContain(plan.Actions, a => a.RelativePath.StartsWith(@"ding\", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Actions, a => a.Kind == SyncActionKind.Conflict && a.RelativePath == "ding");
        await Run(plan); // ohne Fehler
        Assert.Equal("wird kopiert", File.ReadAllText(Path.Combine(f.Target, "normal.txt")));
        Assert.Equal("Datei im Ziel", File.ReadAllText(Path.Combine(f.Target, "ding")));
    }

    [Fact]
    public async Task TypeConflict_IsListed_ButNotResolvable()
    {
        using var f = new TestFolder();
        var state = Path.Combine(f.Root, "state.json");
        f.Src(@"ding\a.txt", "Ordner in der Quelle");
        f.Dst("ding", "Datei im Ziel");

        var plan = await Analyze(f, state);
        var c = Assert.Single(ConflictResolver.List(plan));
        Assert.True(c.IsTypeConflict);
        Assert.False(c.CanResolve);
        Assert.Empty(ConflictResolver.BuildPlan(plan, [(c, ConflictChoice.KeepSource)], "Konflikt").Actions);
    }
}
