using System.Text.Json;

namespace SyncForge.Core.Engine;

/// <summary>Stand einer Datei/eines Ordners nach dem letzten erfolgreichen Zwei-Wege-Abgleich.</summary>
public sealed record StateEntry(bool IsDirectory, long Size, long LastWriteUtcTicks);

/// <summary>
/// Gemeinsamer Stand beider Seiten nach dem letzten Lauf. Nur daran lässt sich erkennen, ob eine Datei,
/// die auf einer Seite fehlt, dort gelöscht oder auf der anderen Seite neu angelegt wurde.
/// </summary>
public static class TwoWayState
{
    public static Dictionary<string, StateEntry> Load(string? path)
    {
        var empty = new Dictionary<string, StateEntry>(StringComparer.OrdinalIgnoreCase);
        if (path is null || !File.Exists(path)) return empty;
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, StateEntry>>(File.ReadAllText(path));
            return data is null ? empty : new Dictionary<string, StateEntry>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return empty; // beschädigt: wie erster Lauf behandeln (löscht nichts)
        }
    }

    public static void Save(string path, Dictionary<string, StateEntry> state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Neuer Stand nach einem Lauf: alle Einträge, die auf beiden Seiten übereinstimmen.
    /// Abweichungen (Fehler, Konflikte "nachfragen") bleiben draußen und werden beim nächsten Lauf erneut geprüft.
    /// </summary>
    internal static Dictionary<string, StateEntry> Capture(Dictionary<string, FileTreeEntry> source, Dictionary<string, FileTreeEntry> target)
    {
        var state = new Dictionary<string, StateEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in source.Values)
        {
            if (!target.TryGetValue(s.RelativePath, out var t) || s.IsDirectory != t.IsDirectory) continue;
            if (!s.IsDirectory && !TwoWayPlanner.SameFile(s, t)) continue;
            state[s.RelativePath] = new StateEntry(s.IsDirectory, s.Size, s.LastWriteUtc.Ticks);
        }
        return state;
    }
}
