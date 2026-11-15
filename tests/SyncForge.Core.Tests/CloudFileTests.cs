using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>
/// Nur online verfügbare Dateien (Cloud-Platzhalter). Echte OneDrive-Platzhalter lassen sich im Test nicht
/// erzeugen; das Attribut "Offline" wird von SyncForge genauso behandelt und lässt sich setzen.
/// </summary>
public class CloudFileTests
{
    private static FilterSettings Filter(CloudFileMode mode) => new() { ExcludePatterns = [], ExcludeSystem = false, CloudFiles = mode };

    private static string MarkCloudOnly(string path)
    {
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Offline);
        return path;
    }

    private static async Task<(SyncPlan Plan, SyncResult Result)> Mirror(TestFolder f, CloudFileMode mode, CompareMode compare = CompareMode.SizeAndDate)
    {
        var ct = TestContext.Current.CancellationToken;
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, Filter(mode), compare, null, ct);
        var result = await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false }).ExecuteAsync(plan, null, ct);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        return (plan, result);
    }

    [Fact]
    public void Detection_RecognizesCloudAttributes()
    {
        Assert.True(CloudFiles.IsCloudOnly(FileAttributes.Offline));
        Assert.True(CloudFiles.IsCloudOnly((FileAttributes)0x00400000)); // RECALL_ON_DATA_ACCESS (OneDrive "nur online")
        Assert.True(CloudFiles.IsCloudOnly((FileAttributes)0x00040000)); // RECALL_ON_OPEN
        Assert.False(CloudFiles.IsCloudOnly(FileAttributes.Archive | FileAttributes.ReadOnly));
    }

    [Fact]
    public async Task Mirror_Skip_DoesNotCopy_AndKeepsExistingTargetCopy()
    {
        using var f = new TestFolder();
        MarkCloudOnly(f.Src("nur-online.docx", "neue Fassung in der Cloud", DateTime.UtcNow));
        f.Src("lokal.txt", "lokal");
        f.Dst("nur-online.docx", "alte Sicherung");

        var (plan, result) = await Mirror(f, CloudFileMode.Skip);

        Assert.Equal(1, plan.SkippedCloudFiles);
        Assert.Equal(1, result.SkippedCloudFiles);
        Assert.Equal(0, plan.DeleteFileCount); // wichtig: keine Löschung der vorhandenen Kopie
        Assert.Equal("alte Sicherung", File.ReadAllText(Path.Combine(f.Target, "nur-online.docx")));
        Assert.True(File.Exists(Path.Combine(f.Target, "lokal.txt")));
    }

    [Fact]
    public async Task Mirror_Skip_NewCloudFile_IsNotCopied()
    {
        using var f = new TestFolder();
        MarkCloudOnly(f.Src(@"ordner\neu.pdf", "noch nie gesichert"));

        var (plan, _) = await Mirror(f, CloudFileMode.Skip);

        Assert.Equal(1, plan.SkippedCloudFiles);
        Assert.Equal(0, plan.CopyNewCount);
        Assert.False(File.Exists(Path.Combine(f.Target, @"ordner\neu.pdf")));
    }

    [Fact]
    public async Task Mirror_Download_CopiesCloudFile()
    {
        using var f = new TestFolder();
        MarkCloudOnly(f.Src("nur-online.docx", "Inhalt"));

        var (plan, _) = await Mirror(f, CloudFileMode.Download);

        Assert.Equal(0, plan.SkippedCloudFiles);
        Assert.Equal("Inhalt", File.ReadAllText(Path.Combine(f.Target, "nur-online.docx")));
    }

    [Fact]
    public async Task ContentCompare_DoesNotReadCloudFiles()
    {
        using var f = new TestFolder();
        // Gleiche Größe und gleiches Datum, aber anderer Inhalt: Ein Inhaltsvergleich würde das finden,
        // bei Online-Dateien darf aber nicht gelesen werden, also gilt die Datei als unverändert.
        f.Src("a.txt", "AAAA");
        MarkCloudOnly(f.Dst("a.txt", "BBBB"));

        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, Filter(CloudFileMode.Download), CompareMode.Content,
            null, TestContext.Current.CancellationToken);

        Assert.Equal(0, plan.UpdateCount);
        Assert.Equal(1, plan.UnchangedFileCount);
    }

    [Fact]
    public async Task TwoWay_Skip_NeverCopiesOrDeletesCloudFiles()
    {
        using var f = new TestFolder();
        var state = Path.Combine(f.Root, "state.json");
        var ct = TestContext.Current.CancellationToken;
        f.Src("doc.txt", "Basis");
        f.Src("weg.txt", "x");

        async Task<SyncPlan> Run()
        {
            var plan = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, Filter(CloudFileMode.Skip), CompareMode.SizeAndDate,
                ConflictResolution.NewerWins, state, "Konflikt", null, ct);
            await new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false }).ExecuteAsync(plan, null, ct);
            TwoWayPlanner.UpdateState(plan);
            return plan;
        }

        await Run();
        // Quelle: doc.txt geändert und nur online; Ziel: weg.txt gelöscht, Quell-Kopie nur online.
        MarkCloudOnly(f.Src("doc.txt", "geändert in der Cloud", DateTime.UtcNow));
        MarkCloudOnly(Path.Combine(f.Source, "weg.txt"));
        File.Delete(Path.Combine(f.Target, "weg.txt"));

        var second = await Run();

        Assert.Equal(2, second.SkippedCloudFiles);
        Assert.False(second.HasChanges);
        Assert.Equal("Basis", File.ReadAllText(Path.Combine(f.Target, "doc.txt")));
        Assert.True(File.Exists(Path.Combine(f.Source, "weg.txt"))); // nicht gelöscht, obwohl im Ziel entfernt
    }

    [Fact]
    public void DefaultMode_IsSkip()
    {
        Assert.Equal(CloudFileMode.Skip, new FilterSettings().CloudFiles);
    }
}
