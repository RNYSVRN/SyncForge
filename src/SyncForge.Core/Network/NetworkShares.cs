using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SyncForge.Core.Network;

public enum NetworkAccessResult
{
    /// <summary>Kein Netzwerkpfad oder Freigabe ohnehin erreichbar.</summary>
    Available,
    /// <summary>Mit den gespeicherten Anmeldedaten verbunden.</summary>
    Connected,
    /// <summary>Nicht erreichbar und keine Anmeldedaten gespeichert.</summary>
    NoCredentials,
    /// <summary>Benutzername oder Kennwort falsch bzw. kein Zugriff.</summary>
    LogonFailure,
    /// <summary>Windows ist mit diesem Server schon unter einem anderen Benutzer verbunden.</summary>
    CredentialConflict,
    /// <summary>Server oder Freigabe nicht gefunden (ausgeschaltet, kein Netzwerk, Tippfehler).</summary>
    NotFound,
    Failed
}

public sealed record NetworkAccessOutcome(NetworkAccessResult Result, int ErrorCode = 0, string? Message = null)
{
    public bool Success => Result is NetworkAccessResult.Available or NetworkAccessResult.Connected;
}

/// <summary>
/// Netzwerkfreigaben (\\server\freigabe) mit eigenen Anmeldedaten: Verbindung ohne Laufwerksbuchstaben für die
/// aktuelle Windows-Sitzung. Die Anmeldedaten liegen in der Windows-Anmeldeinformationsverwaltung
/// (<see cref="NetworkCredentials"/>), nie in den Profilen.
/// </summary>
public static class NetworkShares
{
    /// <summary>"\\server\freigabe\ordner" → "\\server\freigabe"; kein Netzwerkpfad → null.</summary>
    public static string? GetShareRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Trim().Replace('/', '\\');
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) p = @"\\" + p[8..];
        else if (p.StartsWith(@"\\?\", StringComparison.Ordinal) || p.StartsWith(@"\\.\", StringComparison.Ordinal)) return null;
        if (!p.StartsWith(@"\\", StringComparison.Ordinal)) return null;
        var parts = p[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $@"\\{parts[0]}\{parts[1]}" : null;
    }

    public static bool IsNetworkPath(string? path) => GetShareRoot(path) is not null;

    /// <summary>
    /// Vor einem Zugriff: Ist die Freigabe nicht erreichbar und sind Anmeldedaten gespeichert, wird damit verbunden.
    /// Lokale Pfade und bereits erreichbare Freigaben bleiben unberührt.
    /// </summary>
    public static NetworkAccessOutcome EnsureAccess(string? path)
    {
        var root = GetShareRoot(path);
        if (root is null || Exists(root)) return new(NetworkAccessResult.Available);
        var credentials = NetworkCredentials.Load(root);
        if (credentials is null) return new(NetworkAccessResult.NoCredentials);
        var outcome = Connect(root, credentials.Value.UserName, credentials.Value.Password);
        return outcome.Result == NetworkAccessResult.Available ? outcome with { Result = NetworkAccessResult.Connected } : outcome;
    }

    /// <summary>Verbindet mit der Freigabe (ohne Laufwerksbuchstaben, nur für diese Windows-Sitzung).</summary>
    public static NetworkAccessOutcome Connect(string shareRoot, string userName, string password)
    {
        var resource = new NETRESOURCE { dwType = RESOURCETYPE_DISK, lpRemoteName = shareRoot };
        var code = WNetAddConnection2(ref resource, password, string.IsNullOrWhiteSpace(userName) ? null : userName, CONNECT_TEMPORARY);
        if (code == 0) return new(NetworkAccessResult.Available);
        var result = code switch
        {
            1326 or 86 or 2202 or 5 or 1909 or 1331 or 1330 => NetworkAccessResult.LogonFailure, // Anmeldung fehlgeschlagen, Konto gesperrt/abgelaufen …
            1219 => NetworkAccessResult.CredentialConflict,
            53 or 67 or 1203 or 1222 or 64 or 121 => NetworkAccessResult.NotFound,
            _ => NetworkAccessResult.Failed
        };
        return new(result, code, new Win32Exception(code).Message);
    }

    private static bool Exists(string root)
    {
        try { return Directory.Exists(root); }
        catch { return false; }
    }

    private const uint RESOURCETYPE_DISK = 1;
    private const uint CONNECT_TEMPORARY = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public uint dwScope;
        public uint dwType;
        public uint dwDisplayType;
        public uint dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, EntryPoint = "WNetAddConnection2W")]
    private static extern int WNetAddConnection2(ref NETRESOURCE netResource, string? password, string? userName, uint flags);
}
