using System.Security.AccessControl;
using System.Security.Principal;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>
/// Praxisfälle: gesperrte Dateien, lange Pfade, unlesbare Ordner und Laufwerke, die während eines Laufs
/// verschwinden. Hier geht es vor allem darum, dass nie etwas verloren geht.
/// </summary>
public class PracticeTests
{
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<SyncPlan> Plan(TestFolder f) =>
        SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate, null, Ct);

    private static Task<SyncResult> Execute(SyncPlan plan, bool recycle = false, IProgress<SyncProgress>? progress = null, int parallel = 4) =>
        new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = recycle, MaxParallelCopies = parallel }).ExecuteAsync(plan, progress, Ct);

    /// <summary>Verweigert dem aktuellen Benutzer das Auflisten eines Ordners (wie bei fehlenden Rechten oder einem Netzwerkfehler).</summary>
    private sealed class DenyListing : IDisposable
    {
        private readonly DirectoryInfo _dir;
        private readonly FileSystemAccessRule _rule;

        public DenyListing(string path)
        {
            _dir = new DirectoryInfo(path);
            _rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
            var acl = _dir.GetAccessControl();
            acl.AddAccessRule(_rule);
            _dir.SetAccessControl(acl);
        }

        public void Dispose()
        {
            var acl = _dir.GetAccessControl();
            acl.RemoveAccessRule(_rule);
            _dir.SetAccessControl(acl);
        }
    }

    /// <summary>Meldet synchron (Progress&lt;T&gt; würde über den Thread-Pool verzögern).</summary>
    private sealed class SyncProgressHandler(Action<SyncProgress> handler) : IProgress<SyncProgress>
    {
        public void Report(SyncProgress value) => handler(value);
    }

    // ---------- Unlesbare Ordner ----------

    [Fact]
    public async Task Mirror_UnreadableSourceFolder_DoesNotDeleteTargetCopies()
    {
        using var f = new TestFolder();
        f.Src(@"geheim\a.txt", "a");
        f.Src("frei.txt", "x");
        await Execute(await Plan(f));
        Assert.True(File.Exists(Path.Combine(f.Target, @"geheim\a.txt")));

        using (new DenyListing(Path.Combine(f.Source, "geheim")))
        {
            var plan = await Plan(f);
            Assert.Equal(0, plan.DeleteFileCount);
            Assert.Equal(0, plan.DeleteDirectoryCount);
            Assert.Single(plan.UnreadableFolders);
            var result = await Execute(plan);
            Assert.Equal(1, result.UnreadableFolders);
        }
        Assert.True(File.Exists(Path.Combine(f.Target, @"geheim\a.txt")));
    }

    [Fact]
    public async Task TwoWay_UnreadableFolder_DoesNotDeleteOnOtherSide()
    {
        using var f = new TestFolder();
        var state = Path.Combine(f.Root, "state.json");
        f.Src(@"ordner\a.txt", "a");

        async Task<SyncPlan> Run()
        {
            var plan = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, NoFilter, CompareMode.SizeAndDate,
                ConflictResolution.NewerWins, state, "Konflikt", null, Ct);
            await Execute(plan);
            TwoWayPlanner.UpdateState(plan);
            return plan;
        }

        await Run();
        using (new DenyListing(Path.Combine(f.Target, "ordner")))
        {
            var plan = await Run();
            Assert.Equal(0, plan.DeleteFileCount);
            Assert.NotEmpty(plan.UnreadableFolders);
        }
        Assert.True(File.Exists(Path.Combine(f.Source, @"ordner\a.txt")));

        // Nach dem Lauf mit unlesbarem Ordner darf der gespeicherte Stand nichts "vergessen" haben.
        var after = await Run();
        Assert.Equal(0, after.DeleteFileCount);
        Assert.True(File.Exists(Path.Combine(f.Source, @"ordner\a.txt")));
        Assert.True(File.Exists(Path.Combine(f.Target, @"ordner\a.txt")));
    }

    // ---------- Gesperrte Dateien ----------

    [Fact]
    public async Task LockedSourceFile_IsReported_OthersAreCopied_NoTempLeft()
    {
        using var f = new TestFolder();
        var locked = f.Src("gesperrt.pst", "Outlook");
        f.Src("frei.txt", "x");

        SyncResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await Execute(await Plan(f));
        }

        Assert.Single(result.Errors);
        Assert.Equal("gesperrt.pst", result.Errors[0].RelativePath);
        Assert.True(File.Exists(Path.Combine(f.Target, "frei.txt")));
        Assert.Empty(Directory.GetFiles(f.Target, ".~sf_*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BrieflyLockedFile_IsCopiedAfterRetry()
    {
        using var f = new TestFolder();
        var path = f.Src("kurz.txt", "Inhalt");
        var plan = await Plan(f);

        var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _ = Task.Run(async () => { await Task.Delay(400); await stream.DisposeAsync(); }, Ct);
        var result = await Execute(plan);

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal("Inhalt", File.ReadAllText(Path.Combine(f.Target, "kurz.txt")));
    }

    [Fact]
    public async Task LockedTargetFile_KeepsOriginal()
    {
        using var f = new TestFolder();
        f.Src("doc.txt", "neu", DateTime.UtcNow);
        var dst = f.Dst("doc.txt", "alt");

        SyncResult result;
        using (new FileStream(dst, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            result = await Execute(await Plan(f));
        }

        Assert.Single(result.Errors);
        Assert.Equal("alt", File.ReadAllText(dst));
        Assert.Empty(Directory.GetFiles(f.Target, ".~sf_*", SearchOption.AllDirectories));
    }

    // ---------- Lange Pfade (> 260 Zeichen) ----------

    private static string LongRelative()
    {
        var parts = Enumerable.Range(1, 8).Select(i => $"Ordner mit einem recht langen Namen Nummer {i:00}");
        return Path.Combine([.. parts, "Datei mit langem Namen.txt"]);
    }

    [Fact]
    public async Task LongPaths_CopyUpdateDelete()
    {
        using var f = new TestFolder();
        var rel = LongRelative();
        Assert.True(Path.Combine(f.Target, rel).Length > 300);
        f.Src(rel, "eins");

        var first = await Execute(await Plan(f));
        Assert.True(first.Success, string.Join("; ", first.Errors.Select(e => e.Message)));
        Assert.Equal("eins", File.ReadAllText(Path.Combine(f.Target, rel)));

        f.Src(rel, "zwei, länger", DateTime.UtcNow);
        var second = await Execute(await Plan(f));
        Assert.True(second.Success, string.Join("; ", second.Errors.Select(e => e.Message)));
        Assert.Equal("zwei, länger", File.ReadAllText(Path.Combine(f.Target, rel)));

        File.Delete(Path.Combine(f.Source, rel));
        var third = await Execute(await Plan(f));
        Assert.True(third.Success, string.Join("; ", third.Errors.Select(e => e.Message)));
        Assert.False(File.Exists(Path.Combine(f.Target, rel)));
    }

    [Fact]
    public async Task LongPaths_DeleteToRecycleBin()
    {
        using var f = new TestFolder();
        var rel = LongRelative();
        f.Dst(rel, "weg damit");

        var result = await Execute(await Plan(f), recycle: true);

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.False(File.Exists(Path.Combine(f.Target, rel)));
    }

    [Fact]
    public async Task LongPaths_Versioning()
    {
        using var f = new TestFolder();
        var rel = LongRelative();
        f.Src(rel, "neu", DateTime.UtcNow);
        f.Dst(rel, "alt");

        var plan = await Plan(f);
        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false, KeepVersions = true }).ExecuteAsync(plan, null, Ct);

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(f.Target, rel)));
    }

    // ---------- Laufwerk verschwindet während des Laufs ----------

    [Fact]
    public async Task TargetVanishesDuringRun_StopsWithOneError()
    {
        using var f = new TestFolder();
        for (var i = 0; i < 200; i++) f.Src($"d{i % 10}\\f{i:000}.txt", string.Empty);
        var plan = await Plan(f);
        var moved = f.Target + "_weg";
        var vanished = 0;

        var progress = new SyncProgressHandler(p =>
        {
            // Nach der ersten fertigen Datei "steckt jemand das Laufwerk ab".
            if (p.Phase == SyncPhase.Copying && p.ItemsDone >= 15 && Interlocked.Exchange(ref vanished, 1) == 0)
                Directory.Move(f.Target, moved);
        });
        var result = await Execute(plan, progress: progress, parallel: 1);

        Assert.Equal(1, vanished);
        Assert.NotNull(result.LostRoot);
        Assert.Single(result.Errors);
        Assert.False(Directory.Exists(f.Target)); // nicht neu angelegt
    }
}
