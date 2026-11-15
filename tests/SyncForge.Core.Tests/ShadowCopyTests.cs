using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>
/// Schattenkopien. Ohne Administratorrechte (normale Testläufe, CI) muss alles wie bisher funktionieren;
/// der eigentliche VSS-Test läuft nur auf Wunsch in einer Konsole mit Administratorrechten:
/// dotnet test --project tests\SyncForge.Core.Tests\SyncForge.Core.Tests.csproj -- --filter-class *ShadowCopyTests --explicit on
/// </summary>
public class ShadowCopyTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SyncResult> CopyLocked(TestFolder f, bool useShadowCopy)
    {
        var locked = f.Src("outlook.pst", "Postfach");
        f.Src("frei.txt", "x");
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, Ct);
        using var stream = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        return await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false, UseShadowCopy = useShadowCopy })
            .ExecuteAsync(plan, null, Ct);
    }

    [Fact]
    public async Task WithoutAdminRights_BehavesAsBefore()
    {
        Assert.SkipWhen(ShadowCopySession.IsAvailable, "Läuft mit Administratorrechten: siehe VssCopiesLockedFile.");
        using var f = new TestFolder();

        var result = await CopyLocked(f, useShadowCopy: true);

        Assert.Single(result.Errors);
        Assert.Equal(0, result.ShadowCopiedFiles);
        Assert.True(File.Exists(Path.Combine(f.Target, "frei.txt")));
    }

    [Fact]
    public async Task Session_WithoutAdminRights_TranslatesNothing()
    {
        Assert.SkipWhen(ShadowCopySession.IsAvailable, "Läuft mit Administratorrechten.");
        using var session = new ShadowCopySession();
        Assert.Null(await session.TranslateAsync(Path.GetTempPath(), Ct));
        Assert.Equal(0, session.SnapshotCount);
    }

    [Fact(Explicit = true)]
    public async Task VssCopiesLockedFile()
    {
        Assert.SkipUnless(ShadowCopySession.IsAvailable, "Nur mit Administratorrechten.");
        using var f = new TestFolder();

        var result = await CopyLocked(f, useShadowCopy: true);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.ShadowCopiedFiles);
        Assert.Equal("Postfach", File.ReadAllText(Path.Combine(f.Target, "outlook.pst")));
    }
}
