using Microsoft.Data.Sqlite;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;

namespace SyncForge.Core.History;

/// <summary>
/// Speichert die Historie aller Läufe in einer SQLite-Datenbank (history.db).
/// Zeitpunkte werden als UTC-Millisekunden gespeichert, Tagesstatistiken in lokaler Zeit gebildet.
/// </summary>
public sealed class HistoryStore
{
    private const int SchemaVersion = 2;
    private readonly string _connectionString;

    public HistoryStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true
        }.ToString();
    }

    /// <summary>Wird nach jeder Änderung ausgelöst (neuer Lauf, Löschen).</summary>
    public event EventHandler? Changed;

    public void Initialize()
    {
        using var db = Open();
        Execute(db, "PRAGMA journal_mode=WAL;");
        var version = Convert.ToInt32(Scalar(db, "PRAGMA user_version;"));
        if (version >= SchemaVersion) return;
        if (version == 1)
        {
            // Migration 1 -> 2: Richtung je Datei (Zwei-Wege) und Konflikte je Lauf.
            Execute(db, "ALTER TABLE run_files ADD COLUMN side INTEGER NOT NULL DEFAULT 0;");
            Execute(db, "ALTER TABLE runs ADD COLUMN conflicts INTEGER NOT NULL DEFAULT 0;");
            Execute(db, $"PRAGMA user_version = {SchemaVersion};");
            return;
        }

        Execute(db, """
            CREATE TABLE IF NOT EXISTS runs (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id      TEXT    NOT NULL,
                profile_name    TEXT    NOT NULL,
                started_at      INTEGER NOT NULL,
                duration_ms     INTEGER NOT NULL,
                status          INTEGER NOT NULL,
                trigger         INTEGER NOT NULL,
                copied          INTEGER NOT NULL,
                updated         INTEGER NOT NULL,
                deleted         INTEGER NOT NULL,
                bytes           INTEGER NOT NULL,
                errors          INTEGER NOT NULL,
                targets_total   INTEGER NOT NULL,
                targets_skipped INTEGER NOT NULL,
                conflicts       INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_runs_started ON runs(started_at);
            CREATE INDEX IF NOT EXISTS ix_runs_profile ON runs(profile_id);

            CREATE TABLE IF NOT EXISTS run_targets (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id      INTEGER NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
                target_path TEXT    NOT NULL,
                status      INTEGER NOT NULL,
                copied      INTEGER NOT NULL,
                updated     INTEGER NOT NULL,
                deleted     INTEGER NOT NULL,
                bytes       INTEGER NOT NULL,
                errors      INTEGER NOT NULL,
                duration_ms INTEGER NOT NULL,
                message     TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_targets_run ON run_targets(run_id);

            CREATE TABLE IF NOT EXISTS run_files (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id    INTEGER NOT NULL REFERENCES runs(id) ON DELETE CASCADE,
                target_id INTEGER NOT NULL REFERENCES run_targets(id) ON DELETE CASCADE,
                kind      INTEGER NOT NULL,
                path      TEXT    NOT NULL,
                size      INTEGER NOT NULL,
                error     TEXT,
                side      INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_files_run ON run_files(run_id);
            """);
        Execute(db, $"PRAGMA user_version = {SchemaVersion};");
    }

    /// <summary>Speichert einen abgeschlossenen Lauf samt Ziel-Ergebnissen und Datei-Protokoll.</summary>
    public long AddRun(ProfileRunResult run, RunTrigger trigger = RunTrigger.Manual)
    {
        long runId;
        using (var db = Open())
        using (var tx = db.BeginTransaction())
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO runs (profile_id, profile_name, started_at, duration_ms, status, trigger,
                                      copied, updated, deleted, bytes, errors, targets_total, targets_skipped, conflicts)
                    VALUES ($pid, $pname, $start, $dur, $status, $trigger, $copied, $updated, $deleted, $bytes, $errors, $tt, $ts, $conflicts);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$pid", run.Profile.Id.ToString());
                cmd.Parameters.AddWithValue("$pname", run.Profile.Name);
                cmd.Parameters.AddWithValue("$start", run.StartedAt.ToUnixTimeMilliseconds());
                cmd.Parameters.AddWithValue("$dur", (long)run.Duration.TotalMilliseconds);
                cmd.Parameters.AddWithValue("$status", (int)run.Status);
                cmd.Parameters.AddWithValue("$trigger", (int)trigger);
                cmd.Parameters.AddWithValue("$copied", run.Results.Sum(r => r.Copied));
                cmd.Parameters.AddWithValue("$updated", run.Results.Sum(r => r.Updated));
                cmd.Parameters.AddWithValue("$deleted", run.Results.Sum(r => r.DeletedFiles));
                cmd.Parameters.AddWithValue("$bytes", run.Results.Sum(r => r.BytesCopied));
                cmd.Parameters.AddWithValue("$errors", run.Results.Sum(r => r.Errors.Count));
                cmd.Parameters.AddWithValue("$tt", run.Results.Count + run.Skipped.Count);
                cmd.Parameters.AddWithValue("$ts", run.Skipped.Count);
                cmd.Parameters.AddWithValue("$conflicts", run.Results.Sum(r => r.Conflicts));
                runId = (long)cmd.ExecuteScalar()!;
            }

            using var targetCmd = db.CreateCommand();
            targetCmd.Transaction = tx;
            targetCmd.CommandText = """
                INSERT INTO run_targets (run_id, target_path, status, copied, updated, deleted, bytes, errors, duration_ms, message)
                VALUES ($run, $path, $status, $copied, $updated, $deleted, $bytes, $errors, $dur, $msg);
                SELECT last_insert_rowid();
                """;
            var tRun = targetCmd.Parameters.Add("$run", SqliteType.Integer);
            var tPath = targetCmd.Parameters.Add("$path", SqliteType.Text);
            var tStatus = targetCmd.Parameters.Add("$status", SqliteType.Integer);
            var tCopied = targetCmd.Parameters.Add("$copied", SqliteType.Integer);
            var tUpdated = targetCmd.Parameters.Add("$updated", SqliteType.Integer);
            var tDeleted = targetCmd.Parameters.Add("$deleted", SqliteType.Integer);
            var tBytes = targetCmd.Parameters.Add("$bytes", SqliteType.Integer);
            var tErrors = targetCmd.Parameters.Add("$errors", SqliteType.Integer);
            var tDur = targetCmd.Parameters.Add("$dur", SqliteType.Integer);
            var tMsg = targetCmd.Parameters.Add("$msg", SqliteType.Text);

            using var fileCmd = db.CreateCommand();
            fileCmd.Transaction = tx;
            fileCmd.CommandText = "INSERT INTO run_files (run_id, target_id, kind, path, size, error, side) VALUES ($run, $target, $kind, $path, $size, $error, $side);";
            var fRun = fileCmd.Parameters.Add("$run", SqliteType.Integer);
            var fTarget = fileCmd.Parameters.Add("$target", SqliteType.Integer);
            var fKind = fileCmd.Parameters.Add("$kind", SqliteType.Integer);
            var fPath = fileCmd.Parameters.Add("$path", SqliteType.Text);
            var fSize = fileCmd.Parameters.Add("$size", SqliteType.Integer);
            var fError = fileCmd.Parameters.Add("$error", SqliteType.Text);
            var fSide = fileCmd.Parameters.Add("$side", SqliteType.Integer);
            fileCmd.Prepare();

            foreach (var r in run.Results)
            {
                tRun.Value = runId;
                tPath.Value = r.TargetRoot;
                tStatus.Value = (int)(r.Cancelled ? TargetRunStatus.Cancelled : r.Errors.Count > 0 ? TargetRunStatus.Error : TargetRunStatus.Success);
                tCopied.Value = r.Copied;
                tUpdated.Value = r.Updated;
                tDeleted.Value = r.DeletedFiles;
                tBytes.Value = r.BytesCopied;
                tErrors.Value = r.Errors.Count;
                tDur.Value = (long)r.Duration.TotalMilliseconds;
                tMsg.Value = DBNull.Value;
                var targetId = (long)targetCmd.ExecuteScalar()!;

                foreach (var f in r.Files)
                {
                    fRun.Value = runId;
                    fTarget.Value = targetId;
                    fKind.Value = (int)f.Kind;
                    fPath.Value = f.RelativePath;
                    fSize.Value = f.Size;
                    fError.Value = (object?)f.Error ?? DBNull.Value;
                    fSide.Value = (int)f.Side;
                    fileCmd.ExecuteNonQuery();
                }
            }

            foreach (var s in run.Skipped)
            {
                tRun.Value = runId;
                tPath.Value = s.Target.Path;
                tStatus.Value = (int)TargetRunStatus.Skipped;
                tCopied.Value = 0; tUpdated.Value = 0; tDeleted.Value = 0; tBytes.Value = 0; tErrors.Value = 0; tDur.Value = 0;
                tMsg.Value = (object?)s.ValidationError?.ToString() ?? s.ErrorMessage ?? (object)DBNull.Value;
                targetCmd.ExecuteScalar();
            }

            tx.Commit();
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return runId;
    }

    public IReadOnlyList<RunSummary> GetRuns(RunQuery query)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        var where = new List<string>();
        if (query.ProfileId is { } pid) { where.Add("profile_id = $pid"); cmd.Parameters.AddWithValue("$pid", pid.ToString()); }
        if (query.Status is { } st) { where.Add("status = $status"); cmd.Parameters.AddWithValue("$status", (int)st); }
        if (query.From is { } from) { where.Add("started_at >= $from"); cmd.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds()); }
        cmd.CommandText = $"""
            SELECT {RunColumns} FROM runs
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY started_at DESC, id DESC
            LIMIT $limit OFFSET $offset;
            """;
        cmd.Parameters.AddWithValue("$limit", query.Limit);
        cmd.Parameters.AddWithValue("$offset", query.Offset);
        using var reader = cmd.ExecuteReader();
        var list = new List<RunSummary>();
        while (reader.Read()) list.Add(ReadRun(reader));
        return list;
    }

    public RunDetail? GetRun(long id)
    {
        using var db = Open();
        RunSummary? run;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = $"SELECT {RunColumns} FROM runs WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            run = reader.Read() ? ReadRun(reader) : null;
        }
        if (run is null) return null;

        var targets = new List<TargetRunEntry>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, target_path, status, copied, updated, deleted, bytes, errors, duration_ms, message
                FROM run_targets WHERE run_id = $id ORDER BY id;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                targets.Add(new TargetRunEntry(reader.GetInt64(0), reader.GetString(1), (TargetRunStatus)reader.GetInt32(2),
                    reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt64(6), reader.GetInt32(7),
                    TimeSpan.FromMilliseconds(reader.GetInt64(8)), reader.IsDBNull(9) ? null : reader.GetString(9)));
            }
        }
        return new RunDetail(run, targets);
    }

    /// <summary>Datei-Details eines Laufs; Suche über den relativen Pfad (Teilstring, ohne Groß-/Kleinschreibung).</summary>
    public (IReadOnlyList<RunFileEntry> Items, int TotalCount) GetFiles(long runId, long? targetId = null,
        FileFilter filter = FileFilter.All, string? search = null, int limit = 1000)
    {
        using var db = Open();
        var where = new List<string> { "run_id = $run" };
        var parameters = new List<(string, object)> { ("$run", runId) };
        if (targetId is { } tid) { where.Add("target_id = $target"); parameters.Add(("$target", tid)); }
        switch (filter)
        {
            case FileFilter.New: where.Add($"kind = {(int)SyncActionKind.CopyNew} AND error IS NULL"); break;
            case FileFilter.Updated: where.Add($"kind = {(int)SyncActionKind.Update} AND error IS NULL"); break;
            case FileFilter.Deleted: where.Add($"kind IN ({(int)SyncActionKind.DeleteFile},{(int)SyncActionKind.DeleteDirectory}) AND error IS NULL"); break;
            case FileFilter.Errors: where.Add("error IS NOT NULL"); break;
            case FileFilter.Conflicts: where.Add($"kind IN ({(int)SyncActionKind.Conflict},{(int)SyncActionKind.RenameConflict})"); break;
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("path LIKE $search ESCAPE '\\'");
            var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            parameters.Add(("$search", "%" + escaped + "%"));
        }
        var whereSql = string.Join(" AND ", where);

        int total;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM run_files WHERE {whereSql};";
            foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        var items = new List<RunFileEntry>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = $"SELECT target_id, kind, path, size, error, side FROM run_files WHERE {whereSql} ORDER BY id LIMIT $limit;";
            foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new RunFileEntry(reader.GetInt64(0), (SyncActionKind)reader.GetInt32(1), reader.GetString(2),
                    reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4), (SyncSide)reader.GetInt32(5)));
            }
        }
        return (items, total);
    }

    /// <summary>Kennzahlen pro Tag (lokale Zeit) für die letzten <paramref name="days"/> Tage, lückenlos.</summary>
    public IReadOnlyList<DailyStat> GetDailyStats(int days, Guid? profileId = null, DateTimeOffset? now = null)
    {
        var today = DateOnly.FromDateTime((now ?? DateTimeOffset.Now).LocalDateTime);
        var firstDay = today.AddDays(-(days - 1));
        var from = new DateTimeOffset(firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));
        var runs = GetRuns(new RunQuery { ProfileId = profileId, From = from, Limit = int.MaxValue });

        var byDay = runs.GroupBy(r => DateOnly.FromDateTime(r.StartedAt.LocalDateTime)).ToDictionary(g => g.Key);
        var result = new List<DailyStat>(days);
        for (var d = firstDay; d <= today; d = d.AddDays(1))
        {
            if (byDay.TryGetValue(d, out var g))
            {
                result.Add(new DailyStat(d, g.Count(), g.Count(r => r.Status == RunStatus.Error), g.Sum(r => r.Bytes),
                    g.Sum(r => r.Copied + r.Updated + r.Deleted), TimeSpan.FromTicks(g.Sum(r => r.Duration.Ticks))));
            }
            else
            {
                result.Add(new DailyStat(d, 0, 0, 0, 0, TimeSpan.Zero));
            }
        }
        return result;
    }

    public HistoryOverview GetOverview(int days, Guid? profileId = null, DateTimeOffset? now = null)
    {
        var from = (now ?? DateTimeOffset.Now).AddDays(-days);
        var runs = GetRuns(new RunQuery { ProfileId = profileId, From = from, Limit = int.MaxValue });
        if (runs.Count == 0) return new HistoryOverview(0, 0, 0, 0, 0, TimeSpan.Zero);
        return new HistoryOverview(
            runs.Count,
            runs.Count(r => r.Status == RunStatus.Success),
            runs.Count(r => r.Status == RunStatus.Error),
            runs.Sum(r => r.Bytes),
            runs.Sum(r => r.Copied + r.Updated + r.Deleted),
            TimeSpan.FromTicks((long)runs.Average(r => r.Duration.Ticks)));
    }

    /// <summary>Profile, für die es Läufe gibt (für den Filter; auch bereits gelöschte Profile).</summary>
    public IReadOnlyList<ProfileRef> GetProfiles()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT profile_id, profile_name FROM runs r
            WHERE id = (SELECT MAX(id) FROM runs WHERE profile_id = r.profile_id)
            ORDER BY profile_name COLLATE NOCASE;
            """;
        using var reader = cmd.ExecuteReader();
        var list = new List<ProfileRef>();
        while (reader.Read()) list.Add(new ProfileRef(Guid.Parse(reader.GetString(0)), reader.GetString(1)));
        return list;
    }

    /// <summary>Löscht Läufe, die vor <paramref name="cutoff"/> gestartet wurden. Gibt die Anzahl zurück.</summary>
    public int DeleteOlderThan(DateTimeOffset cutoff)
    {
        int count;
        using (var db = Open())
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM runs WHERE started_at < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeMilliseconds());
            count = cmd.ExecuteNonQuery();
        }
        if (count > 0) Changed?.Invoke(this, EventArgs.Empty);
        return count;
    }

    public void DeleteRun(long id)
    {
        using (var db = Open())
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM runs WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        using (var db = Open())
        {
            Execute(db, "DELETE FROM runs;");
            Execute(db, "VACUUM;");
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private const string RunColumns =
        "id, profile_id, profile_name, started_at, duration_ms, status, trigger, copied, updated, deleted, bytes, errors, targets_total, targets_skipped, conflicts";

    private static RunSummary ReadRun(SqliteDataReader r) => new(
        r.GetInt64(0), Guid.Parse(r.GetString(1)), r.GetString(2),
        DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(3)).ToLocalTime(),
        TimeSpan.FromMilliseconds(r.GetInt64(4)), (RunStatus)r.GetInt32(5), (RunTrigger)r.GetInt32(6),
        r.GetInt32(7), r.GetInt32(8), r.GetInt32(9), r.GetInt64(10), r.GetInt32(11), r.GetInt32(12), r.GetInt32(13), r.GetInt32(14));

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connectionString);
        db.Open();
        return db;
    }

    private static void Execute(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
