using System.Management;
using System.Security.Principal;

namespace SyncForge.Core.Engine;

/// <summary>
/// Volumeschattenkopien (VSS) für gesperrte Dateien: Erst wenn eine Datei trotz Wiederholungen gesperrt bleibt,
/// wird eine Momentaufnahme ihres Laufwerks erstellt und die Datei daraus gelesen. Die Momentaufnahmen werden
/// am Ende wieder gelöscht. Windows erlaubt das nur mit Administratorrechten; ohne sie ist die Sitzung inaktiv
/// und alles bleibt wie bisher (die Datei wird als Fehler gemeldet).
/// </summary>
public sealed class ShadowCopySession : IDisposable
{
    private readonly Dictionary<string, (string Id, string Device)> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedVolumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Läuft der Prozess mit Administratorrechten (Voraussetzung für Schattenkopien)?</summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Anzahl der in dieser Sitzung erstellten Momentaufnahmen.</summary>
    public int SnapshotCount => _snapshots.Count;

    /// <summary>
    /// Pfad derselben Datei in einer Momentaufnahme ihres Laufwerks (wird bei Bedarf erstellt).
    /// null, wenn keine Schattenkopie möglich ist (keine Rechte, Netzwerkpfad, Laufwerk ohne VSS).
    /// </summary>
    public async Task<string?> TranslateAsync(string path, CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal) || root.Length != 3) return null;
        try
        {
            var drive = new DriveInfo(root);
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)
                || drive.DriveFormat is not ("NTFS" or "ReFS")) return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        (string Id, string Device) snapshot;
        await _gate.WaitAsync(ct);
        try
        {
            if (_failedVolumes.Contains(root)) return null;
            if (!_snapshots.TryGetValue(root, out snapshot))
            {
                try
                {
                    snapshot = await Task.Run(() => Create(root), ct);
                    _snapshots[root] = snapshot;
                }
                catch (Exception ex) when (ex is ManagementException or IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    _failedVolumes.Add(root); // nicht bei jeder gesperrten Datei erneut versuchen
                    return null;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
        // "C:\Users\x\a.pst" → "\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy7\Users\x\a.pst"
        return snapshot.Device + full[(root.Length - 1)..];
    }

    private static (string Id, string Device) Create(string volume)
    {
        using var shadowClass = new ManagementClass(@"\\.\root\cimv2", "Win32_ShadowCopy", null);
        using var input = shadowClass.GetMethodParameters("Create");
        input["Volume"] = volume;
        input["Context"] = "ClientAccessible";
        using var output = shadowClass.InvokeMethod("Create", input, null);
        var code = Convert.ToUInt32(output["ReturnValue"]);
        if (code != 0) throw new IOException($"Win32_ShadowCopy.Create: {code}");
        var id = (string)output["ShadowID"];

        using var searcher = new ManagementObjectSearcher(@"\\.\root\cimv2", $"SELECT DeviceObject FROM Win32_ShadowCopy WHERE ID='{id}'");
        foreach (ManagementObject item in searcher.Get())
        {
            using (item) return (id, (string)item["DeviceObject"]);
        }
        throw new InvalidOperationException("Schattenkopie nicht gefunden.");
    }

    /// <summary>Löscht alle Momentaufnahmen dieser Sitzung (sie belegen sonst Speicherplatz auf dem Laufwerk).</summary>
    public void Dispose()
    {
        foreach (var (id, _) in _snapshots.Values)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"\\.\root\cimv2", $"SELECT * FROM Win32_ShadowCopy WHERE ID='{id}'");
                foreach (ManagementObject item in searcher.Get())
                {
                    using (item) item.Delete();
                }
            }
            catch
            {
                // best effort: Windows räumt alte Schattenkopien bei Platzmangel ohnehin auf
            }
        }
        _snapshots.Clear();
        _gate.Dispose();
    }
}
