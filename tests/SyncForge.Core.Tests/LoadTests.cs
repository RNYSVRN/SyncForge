using System.Diagnostics;
using SyncForge.Core.Engine;
using SyncForge.Core.History;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

/// <summary>
/// Lasttest mit vielen Dateien. Läuft nicht automatisch mit (dauert), Aufruf:
/// dotnet test --project tests\SyncForge.Core.Tests\SyncForge.Core.Tests.csproj -- --filter-class *LoadTests --explicit only
/// Anzahl über die Umgebungsvariable SYNCFORGE_LOADTEST_FILES (Standard 100000). Messwerte stehen in
/// %TEMP%\SyncForgeLoadTest.txt.
/// </summary>
public class LoadTests
{
    [Fact(Explicit = true)]
    public async Task ManyFiles_ScanCopyRescanHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        var count = int.TryParse(Environment.GetEnvironmentVariable("SYNCFORGE_LOADTEST_FILES"), out var n) ? n : 100_000;
        var log = new List<string> { $"SyncForge-Lasttest {DateTime.Now:yyyy-MM-dd HH:mm}, {count:N0} Dateien" };
        var sw = Stopwatch.StartNew();
        var logPath = Path.Combine(Path.GetTempPath(), "SyncForgeLoadTest.txt");

        void Step(string name)
        {
            Process.GetCurrentProcess().Refresh();
            log.Add($"{name,-38} {sw.Elapsed.TotalSeconds,7:0.0} s   Arbeitsspeicher {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024,5} MB (Spitze)");
            File.WriteAllLines(logPath, log);
            sw.Restart();
        }

        using var f = new TestFolder();
        var payload = new byte[512];
        for (var i = 0; i < count; i++)
        {
            var dir = Path.Combine(f.Source, $"Bereich {i / 10_000:00}", $"Ordner {i / 100 % 100:00}");
            if (i % 100 == 0) Directory.CreateDirectory(dir);
            payload[0] = (byte)i;
            File.WriteAllBytes(Path.Combine(dir, $"Datei {i:000000}.dat"), payload);
        }
        Step("Testdaten anlegen");

        var filter = new FilterSettings();
        var plan = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, filter, CompareMode.SizeAndDate, null, ct);
        Step("Analyse (erster Lauf)");
        Assert.Equal(count, plan.CopyNewCount);

        var executor = new SyncExecutor(new SyncExecutorOptions { DeleteToRecycleBin = false });
        var result = await executor.ExecuteAsync(plan, new Progress<SyncProgress>(_ => { }), ct);
        Step("Kopieren (erster Lauf)");
        Assert.True(result.Success, string.Join("; ", result.Errors.Take(5).Select(e => e.Message)));
        Assert.Equal(count, result.Copied);

        var history = new HistoryStore(Path.Combine(f.Root, "history.db"));
        history.Initialize();
        var run = new ProfileRunResult { Profile = new SyncProfile { Name = "Last" }, StartedAt = DateTimeOffset.Now };
        run.Results.Add(result);
        var runId = history.AddRun(run);
        Step("Historie speichern");
        Assert.Equal(count, history.GetFiles(runId, filter: FileFilter.New).TotalCount);
        Step("Historie lesen (Dateiliste)");

        var again = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, filter, CompareMode.SizeAndDate, null, ct);
        Step("Analyse ohne Änderungen");
        Assert.False(again.HasChanges);

        // 1 % ändern, 1 % löschen
        var files = Directory.GetFiles(f.Source, "*.dat", SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i += 100) File.AppendAllText(files[i], "x");
        for (var i = 50; i < files.Length; i += 100) File.Delete(files[i]);
        sw.Restart();
        var delta = await SyncPlanner.CreateMirrorPlanAsync(f.Source, f.Target, filter, CompareMode.SizeAndDate, null, ct);
        var deltaResult = await executor.ExecuteAsync(delta, null, ct);
        Step("Abgleich nach 1 % Änderungen");
        Assert.True(deltaResult.Success);
        Assert.Equal(count / 100, deltaResult.Updated);
        Assert.Equal(count / 100, deltaResult.DeletedFiles);

        // Zwei-Wege mit Stand-Datei
        var state = Path.Combine(f.Root, "state.json");
        var tw = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, filter, CompareMode.SizeAndDate, ConflictResolution.NewerWins, state, "Konflikt", null, ct);
        TwoWayPlanner.UpdateState(tw);
        Step("Zwei-Wege: Analyse + Stand speichern");
        var tw2 = await TwoWayPlanner.CreatePlanAsync(f.Source, f.Target, filter, CompareMode.SizeAndDate, ConflictResolution.NewerWins, state, "Konflikt", null, ct);
        Step("Zwei-Wege: Analyse ohne Änderungen");
        Assert.False(tw2.HasChanges);

        log.Add($"Stand-Datei: {new FileInfo(state).Length / 1024:N0} KB, Historie: {new FileInfo(Path.Combine(f.Root, "history.db")).Length / 1024:N0} KB");
        File.WriteAllLines(logPath, log);
        foreach (var line in log) TestContext.Current.TestOutputHelper?.WriteLine(line);
    }
}
