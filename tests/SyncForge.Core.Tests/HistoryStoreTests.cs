using Microsoft.Data.Sqlite;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly HistoryStore _store;

    public HistoryStoreTests()
    {
        _store = new HistoryStore(Path.Combine(_folder.Root, "history.db"));
        _store.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _folder.Dispose();
    }

    private static ProfileRunResult FakeRun(string name, DateTimeOffset started, bool withError = false, bool skipped = false, Guid? id = null)
    {
        var profile = new SyncProfile { Id = id ?? Guid.NewGuid(), Name = name, SourcePath = @"C:\Quelle" };
        var run = new ProfileRunResult { Profile = profile, StartedAt = started, Duration = TimeSpan.FromSeconds(12) };
        var result = new SyncResult { TargetRoot = @"D:\Ziel", Copied = 2, Updated = 1, DeletedFiles = 1, BytesCopied = 3000, Duration = TimeSpan.FromSeconds(10) };
        result.Files.Add(new SyncFileRecord(SyncActionKind.CopyNew, @"ordner\neu1.txt", 1000));
        result.Files.Add(new SyncFileRecord(SyncActionKind.CopyNew, @"ordner\neu2.jpg", 1000));
        result.Files.Add(new SyncFileRecord(SyncActionKind.Update, "geaendert.txt", 1000));
        result.Files.Add(new SyncFileRecord(SyncActionKind.DeleteFile, "alt.txt", 0));
        if (withError)
        {
            result.Errors.Add(new SyncError("gesperrt.docx", "Zugriff verweigert"));
            result.Files.Add(new SyncFileRecord(SyncActionKind.Update, "gesperrt.docx", 500, "Zugriff verweigert"));
        }
        run.Results.Add(result);
        if (skipped)
        {
            run.Skipped.Add(new TargetAnalysis
            {
                Target = new SyncTarget { Path = @"E:\USB" }, Status = TargetStatus.Unavailable, ValidationError = SyncValidationError.TargetUnavailable
            });
        }
        return run;
    }

    [Fact]
    public void AddRun_IsReturnedWithTotals()
    {
        var id = _store.AddRun(FakeRun("Dokumente", DateTimeOffset.Now, skipped: true));

        var runs = _store.GetRuns(new RunQuery());
        var run = Assert.Single(runs);
        Assert.Equal(id, run.Id);
        Assert.Equal("Dokumente", run.ProfileName);
        Assert.Equal(2, run.Copied);
        Assert.Equal(1, run.Updated);
        Assert.Equal(1, run.Deleted);
        Assert.Equal(3000, run.Bytes);
        Assert.Equal(2, run.TargetsTotal);
        Assert.Equal(1, run.TargetsSkipped);
        Assert.Equal(RunStatus.Warning, run.Status);
        Assert.Equal(TimeSpan.FromSeconds(12), run.Duration);
    }

    [Fact]
    public void GetRun_ReturnsTargetsIncludingSkipped()
    {
        var id = _store.AddRun(FakeRun("Dokumente", DateTimeOffset.Now, skipped: true));
        var detail = _store.GetRun(id)!;
        Assert.Equal(2, detail.Targets.Count);
        Assert.Equal(TargetRunStatus.Success, detail.Targets[0].Status);
        Assert.Equal(TargetRunStatus.Skipped, detail.Targets[1].Status);
        Assert.Equal(@"E:\USB", detail.Targets[1].TargetPath);
        Assert.Equal("TargetUnavailable", detail.Targets[1].Message);
    }

    [Fact]
    public void GetFiles_FiltersAndSearches()
    {
        var id = _store.AddRun(FakeRun("Dokumente", DateTimeOffset.Now, withError: true));

        Assert.Equal(5, _store.GetFiles(id).TotalCount);
        Assert.Equal(2, _store.GetFiles(id, filter: FileFilter.New).TotalCount);
        Assert.Equal(1, _store.GetFiles(id, filter: FileFilter.Updated).TotalCount); // fehlgeschlagene zählen nicht
        Assert.Equal(1, _store.GetFiles(id, filter: FileFilter.Deleted).TotalCount);
        var errors = _store.GetFiles(id, filter: FileFilter.Errors);
        Assert.Equal("Zugriff verweigert", Assert.Single(errors.Items).Error);

        var search = _store.GetFiles(id, search: "ORDNER\\");
        Assert.Equal(2, search.TotalCount);
        Assert.Equal(0, _store.GetFiles(id, search: "%").TotalCount); // Platzhalter werden maskiert
        Assert.Equal(1, _store.GetFiles(id, search: "jpg").TotalCount);
    }

    [Fact]
    public void GetFiles_RespectsLimit_ButReportsTotal()
    {
        var id = _store.AddRun(FakeRun("Dokumente", DateTimeOffset.Now));
        var (items, total) = _store.GetFiles(id, limit: 2);
        Assert.Equal(2, items.Count);
        Assert.Equal(4, total);
    }

    [Fact]
    public void Query_ByProfileAndStatus()
    {
        var a = Guid.NewGuid();
        _store.AddRun(FakeRun("A", DateTimeOffset.Now.AddMinutes(-2), id: a));
        _store.AddRun(FakeRun("A", DateTimeOffset.Now.AddMinutes(-1), withError: true, id: a));
        _store.AddRun(FakeRun("B", DateTimeOffset.Now));

        Assert.Equal(2, _store.GetRuns(new RunQuery { ProfileId = a }).Count);
        Assert.Single(_store.GetRuns(new RunQuery { Status = RunStatus.Error }));
        Assert.Equal("B", _store.GetRuns(new RunQuery())[0].ProfileName); // neueste zuerst
        Assert.Equal(2, _store.GetProfiles().Count);
    }

    [Fact]
    public void DailyStats_AreContinuous_AndAggregated()
    {
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 15)));
        _store.AddRun(FakeRun("A", now));
        _store.AddRun(FakeRun("A", now.AddHours(-1), withError: true));
        _store.AddRun(FakeRun("A", now.AddDays(-3)));
        _store.AddRun(FakeRun("A", now.AddDays(-40))); // außerhalb

        var stats = _store.GetDailyStats(7, now: now);
        Assert.Equal(7, stats.Count);
        Assert.Equal(DateOnly.FromDateTime(now.LocalDateTime), stats[^1].Date);
        Assert.Equal(2, stats[^1].Runs);
        Assert.Equal(1, stats[^1].Failed);
        Assert.Equal(6000, stats[^1].Bytes);
        Assert.Equal(1, stats[^4].Runs);
        Assert.Equal(0, stats[0].Runs);

        var overview = _store.GetOverview(30, now: now);
        Assert.Equal(3, overview.Runs);
        Assert.Equal(1, overview.Failed);
        Assert.Equal(9000, overview.Bytes);
    }

    [Fact]
    public void Retention_DeletesOldRunsWithTheirFiles()
    {
        var oldId = _store.AddRun(FakeRun("A", DateTimeOffset.Now.AddDays(-100)));
        var newId = _store.AddRun(FakeRun("A", DateTimeOffset.Now));

        Assert.Equal(1, _store.DeleteOlderThan(DateTimeOffset.Now.AddDays(-90)));
        Assert.Null(_store.GetRun(oldId));
        Assert.Equal(0, _store.GetFiles(oldId).TotalCount); // kaskadiert
        Assert.NotNull(_store.GetRun(newId));
    }

    [Fact]
    public void Clear_RemovesEverything_AndRaisesChanged()
    {
        _store.AddRun(FakeRun("A", DateTimeOffset.Now));
        var raised = false;
        _store.Changed += (_, _) => raised = true;
        _store.Clear();
        Assert.Empty(_store.GetRuns(new RunQuery()));
        Assert.True(raised);
    }

    [Fact]
    public async Task RealRun_FileRecords_EndToEnd()
    {
        using var f = new TestFolder();
        f.Src("a.txt", "A");
        f.Src(@"sub\b.txt", "B");
        f.Dst("weg.txt", "x");
        var profile = new SyncProfile
        {
            Name = "E2E", SourcePath = f.Source, DeleteToRecycleBin = false,
            Filter = new FilterSettings { ExcludePatterns = [], ExcludeSystem = false },
            Targets = [new SyncTarget { Path = f.Target }]
        };
        var ct = TestContext.Current.CancellationToken;
        var analysis = await ProfileRunner.AnalyzeAsync(profile, null, ct);
        var run = await ProfileRunner.ExecuteAsync(analysis, new SyncExecutorOptions { DeleteToRecycleBin = false }, null, ct);

        var id = _store.AddRun(run);
        var (items, _) = _store.GetFiles(id);
        Assert.Contains(items, i => i.Kind == SyncActionKind.CopyNew && i.Path == "a.txt");
        Assert.Contains(items, i => i.Kind == SyncActionKind.CopyNew && i.Path == @"sub\b.txt");
        Assert.Contains(items, i => i.Kind == SyncActionKind.CreateDirectory && i.Path == "sub");
        Assert.Contains(items, i => i.Kind == SyncActionKind.DeleteFile && i.Path == "weg.txt");
    }
}
