using System.Runtime.InteropServices;

namespace SyncForge.Core.Network;

/// <summary>
/// Anmeldedaten für Netzwerkfreigaben in der Windows-Anmeldeinformationsverwaltung (pro Benutzer, verschlüsselt
/// durch Windows). Schlüssel ist die Freigabe ("\\server\freigabe"), damit mehrere Profile dieselben Daten nutzen.
/// In Profilen, Export-Dateien und Protokollen tauchen Kennwörter nie auf.
/// </summary>
public static class NetworkCredentials
{
    internal const string Prefix = "SyncForge:";

    public static string TargetName(string shareRoot) => Prefix + shareRoot.TrimEnd('\\').ToLowerInvariant();

    public static void Save(string shareRoot, string userName, string password)
    {
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = TargetName(shareRoot),
                UserName = userName,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)(password.Length * 2),
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                Comment = "SyncForge"
            };
            if (!CredWrite(ref credential, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            // Kennwort im Speicher überschreiben, bevor er freigegeben wird.
            for (var i = 0; i < password.Length * 2; i++) Marshal.WriteByte(blob, i, 0);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public static (string UserName, string Password)? Load(string shareRoot)
    {
        if (!CredRead(TargetName(shareRoot), CRED_TYPE_GENERIC, 0, out var ptr)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            var password = credential.CredentialBlobSize == 0 || credential.CredentialBlob == 0
                ? string.Empty
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
            return (credential.UserName ?? string.Empty, password);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    /// <summary>Nur der Benutzername (für die Anzeige), ohne das Kennwort zu lesen.</summary>
    public static string? GetUserName(string shareRoot) => Load(shareRoot)?.UserName;

    public static bool Exists(string shareRoot) => Load(shareRoot) is not null;

    public static void Delete(string shareRoot) => CredDelete(TargetName(shareRoot), CRED_TYPE_GENERIC, 0);

    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out nint credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(nint buffer);
}
