using System.Runtime.InteropServices;

namespace SyncForge.Core.Engine;

/// <summary>Verschiebt Dateien/Ordner in den Papierkorb (Shell-API). Nur für lokale Laufwerke sinnvoll.</summary>
internal static partial class RecycleBin
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>
    /// True, wenn der Pfad auf einem lokalen Festplatten-/Wechsellaufwerk liegt. Auf Netzlaufwerken
    /// würde die Shell sonst ohne Rückfrage endgültig löschen.
    /// </summary>
    public static bool IsSupported(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return false;
            var type = new DriveInfo(root).DriveType;
            return type is DriveType.Fixed or DriveType.Removable;
        }
        catch
        {
            return false;
        }
    }

    public static void Delete(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0' + '\0',
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI)
        };
        var result = SHFileOperation(ref op);
        if (result != 0 || op.fAnyOperationsAborted)
        {
            throw new IOException($"Papierkorb-Löschung fehlgeschlagen (Code 0x{result:X}).");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public nint hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public nint hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
