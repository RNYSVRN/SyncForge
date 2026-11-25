using System.Runtime.InteropServices;
using System.Text;

namespace SyncForge.Core.Engine;

/// <summary>
/// Erkennt Laufwerke an ihrer Seriennummer (Volume-ID) statt am Buchstaben. Bekommt eine USB-Festplatte
/// beim nächsten Anschließen einen anderen Buchstaben, wird das Ziel trotzdem gefunden – und ein fremdes
/// Laufwerk mit dem alten Buchstaben wird nie beschrieben.
/// </summary>
public static class VolumeResolver
{
    public sealed record VolumeId(uint Serial, string Label);

    /// <summary>Seriennummer und Bezeichnung des Laufwerks, auf dem <paramref name="path"/> liegt (null bei Netzwerkpfaden).</summary>
    public static VolumeId? GetVolume(string path)
    {
        try
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;
            var label = new StringBuilder(261);
            var fs = new StringBuilder(261);
            return GetVolumeInformation(root, label, label.Capacity, out var serial, out _, out _, fs, fs.Capacity)
                ? new VolumeId(serial, label.ToString())
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Liefert den aktuellen Pfad eines Ziels: unverändert, wenn keine Volume-ID gespeichert ist oder das
    /// Laufwerk passt; mit neuem Buchstaben, wenn das Laufwerk woanders eingebunden ist; null, wenn es fehlt.
    /// </summary>
    public static string? Resolve(string path, uint? serial)
    {
        if (serial is null || path.StartsWith(@"\\", StringComparison.Ordinal)) return path;
        if (GetVolume(path)?.Serial == serial) return path;

        var relative = Path.GetRelativePath(Path.GetPathRoot(path)!, path);
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady) continue;
            if (GetVolume(drive.RootDirectory.FullName)?.Serial == serial)
                return relative == "." ? drive.RootDirectory.FullName : Path.Combine(drive.RootDirectory.FullName, relative);
        }
        return null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(string rootPathName, StringBuilder volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags, StringBuilder fileSystemNameBuffer, int nFileSystemNameSize);
}
