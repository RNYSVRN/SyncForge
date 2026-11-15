using System.Text.Json;
using System.Text.Json.Serialization;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>Mehrere Quellen je Profil: jede Quelle in ihren eigenen Unterordner der Ziele.</summary>
public class MultiSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly FilterSettings NoFilter = new() { ExcludePatterns = [], ExcludeSystem = false };

    private static (SyncProfile Profile, string Root) Setup(TestFolder f, params string[] sourceNames)
    {
        var profile = new SyncProfile { Name = "Test", Filter = NoFilter, DeleteToRecycleBin = false };
        foreach (var name in sourceNames)
        {
            var path = Path.Combine(f.Root, name);
            Directory.CreateDirectory(path);
            profile.Sources.Add(new SyncSource
            {
                Path = path,
                TargetFolder = sourceNames.Length > 1 ? SyncProfile.SuggestTargetFolder(path, profile.Sources.Select(s => s.TargetFolder)) : string.Empty
            });
        }
        profile.Targets.Add(new SyncTarget { Path = f.Target });
        return (profile, f.Root);
    }

    private static async Task<ProfileRunResult> Run(SyncProfile profile, string? stateDir = null)
    {
        var analysis = await ProfileRunner.AnalyzeAsync(profile, null, Ct, stateDir);
        return await ProfileRunner.ExecuteAsync(analysis, new SyncExecutorOptions { DeleteToRecycleBin = false }, null, Ct);
    }

    [Fact]
    public async Task TwoSources_LandInOwnSubfolders()
    {
        using var f = new TestFolder();
        var (profile, root) = Setup(f, "Dokumente", "Bilder");
        File.WriteAllText(Path.Combine(root, "Dokumente", "brief.txt"), "Brief");
        File.WriteAllText(Path.Combine(root, "Bilder", "foto.jpg"), "Foto");

        var run = await Run(profile);

        Assert.Equal(RunStatus.Success, run.Status);
        Assert.Equal(2, run.Results.Count);
        Assert.Equal("Brief", File.ReadAllText(Path.Combine(f.Target, "Dokumente", "brief.txt")));
        Assert.Equal("Foto", File.ReadAllText(Path.Combine(f.Target, "Bilder", "foto.jpg")));
    }

    [Fact]
    public async Task Mirror_OfOneSource_NeverDeletesTheOther()
    {
        using var f = new TestFolder();
        var (profile, root) = Setup(f, "A", "B");
        File.WriteAllText(Path.Combine(root, "A", "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "B", "b.txt"), "b");
        await Run(profile);

        File.Delete(Path.Combine(root, "A", "a.txt"));
        var second = await Run(profile);

        Assert.False(File.Exists(Path.Combine(f.Target, "A", "a.txt"))); // gespiegelt
        Assert.True(File.Exists(Path.Combine(f.Target, "B", "b.txt")));  // unberührt
        Assert.Equal(1, second.Results.Sum(r => r.DeletedFiles));
    }

    [Fact]
    public async Task SingleSource_SyncsIntoTargetRoot_AsBefore()
    {
        using var f = new TestFolder();
        var (profile, root) = Setup(f, "Quelle");
        File.WriteAllText(Path.Combine(root, "Quelle", "x.txt"), "x");

        await Run(profile);

        Assert.True(File.Exists(Path.Combine(f.Target, "x.txt")));
    }

    [Fact]
    public async Task MissingSource_OthersStillRun_AndRunIsWarning()
    {
        using var f = new TestFolder();
        var (profile, root) = Setup(f, "Da", "Weg");
        File.WriteAllText(Path.Combine(root, "Da", "x.txt"), "x");
        Directory.Delete(Path.Combine(root, "Weg"));

        var analysis = await ProfileRunner.AnalyzeAsync(profile, null, Ct);
        Assert.False(analysis.SourceMissing);
        Assert.Single(analysis.MissingSources);
        var run = await ProfileRunner.ExecuteAsync(analysis, new SyncExecutorOptions { DeleteToRecycleBin = false }, null, Ct);

        Assert.True(File.Exists(Path.Combine(f.Target, "Da", "x.txt")));
        Assert.Single(run.Skipped, s => s.ValidationError == SyncValidationError.SourceMissing);
        Assert.Equal(RunStatus.Warning, run.Status);
    }

    [Fact]
    public async Task TwoWay_StatePerSource()
    {
        using var f = new TestFolder();
        var (profile, root) = Setup(f, "A", "B");
        profile.Mode = SyncMode.TwoWay;
        var stateDir = Path.Combine(f.Root, "state");
        File.WriteAllText(Path.Combine(root, "A", "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "B", "b.txt"), "b");
        await Run(profile, stateDir);
        foreach (var r in new[] { "A", "B" }) Assert.True(Directory.Exists(Path.Combine(f.Target, r)));

        // Löschen im Ziel von A wird zurück in die Quelle A übertragen, B bleibt unberührt.
        File.Delete(Path.Combine(f.Target, "A", "a.txt"));
        await Run(profile, stateDir);

        Assert.False(File.Exists(Path.Combine(root, "A", "a.txt")));
        Assert.True(File.Exists(Path.Combine(root, "B", "b.txt")));
        Assert.Equal(2, Directory.GetFiles(stateDir, "*.json").Length);
    }

    [Fact]
    public void OldProfileJson_WithOnlySourcePath_BecomesOneSource()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var profile = JsonSerializer.Deserialize<SyncProfile>("""{"Name":"Alt","SourcePath":"C:\\Daten","Targets":[]}""", options)!;

        var source = Assert.Single(profile.Sources);
        Assert.Equal(@"C:\Daten", source.Path);
        Assert.Equal(string.Empty, source.TargetFolder);
        Assert.Equal(@"C:\Daten", profile.SourcePath);

        // Rundreise: Quellenliste bleibt erhalten, SourcePath zeigt weiter auf die erste Quelle.
        profile.Sources.Add(new SyncSource { Path = @"D:\Fotos", TargetFolder = "Fotos" });
        var again = JsonSerializer.Deserialize<SyncProfile>(JsonSerializer.Serialize(profile, options), options)!;
        Assert.Equal(2, again.Sources.Count);
        Assert.Equal("Fotos", again.Sources[1].TargetFolder);
    }

    [Theory]
    [InlineData(@"C:\Users\ich\Dokumente", "Dokumente")]
    [InlineData(@"D:\", "Laufwerk D")]
    [InlineData(@"\\nas\fotos\", "fotos")]
    public void SuggestTargetFolder_UsesFolderName(string path, string expected) =>
        Assert.Equal(expected, SyncProfile.SuggestTargetFolder(path, []));

    [Fact]
    public void SuggestTargetFolder_AvoidsDuplicates() =>
        Assert.Equal("Dokumente (2)", SyncProfile.SuggestTargetFolder(@"E:\Dokumente", ["dokumente"]));
}
