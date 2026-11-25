using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SyncForge.Core.Engine;

/// <summary>
/// Niedrige E/A-Priorität für einzelne Dateizugriffe (Windows I/O Priority Hint): Andere Programme bekommen
/// beim Lesen und Schreiben Vorrang, SyncForge nutzt die freien Lücken. Gilt nur für das jeweilige Datei-Handle,
/// nicht für den ganzen Prozess (die Oberfläche bleibt also normal schnell).
/// </summary>
public static class IoPriority
{
    private const int FileIoPriorityHintInfo = 12;
    private const int IoPriorityHintLow = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_IO_PRIORITY_HINT_INFO
    {
        public int PriorityHint;
    }

    /// <summary>Setzt die Priorität des Handles herab. Fehler sind unkritisch (dann eben normale Priorität).</summary>
    public static bool TrySetLow(SafeFileHandle handle)
    {
        if (handle.IsInvalid || handle.IsClosed) return false;
        var info = new FILE_IO_PRIORITY_HINT_INFO { PriorityHint = IoPriorityHintLow };
        return SetFileInformationByHandle(handle, FileIoPriorityHintInfo, ref info, Marshal.SizeOf<FILE_IO_PRIORITY_HINT_INFO>());
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int infoClass, ref FILE_IO_PRIORITY_HINT_INFO info, int size);
}
