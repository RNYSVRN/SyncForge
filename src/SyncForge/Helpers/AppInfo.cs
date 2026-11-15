using System.Reflection;
using SyncForge.Core.Updates;

namespace SyncForge.Helpers;

/// <summary>Version und Herkunft der App an einer Stelle.</summary>
public static class AppInfo
{
    /// <summary>GitHub-Repository ("besitzer/name"): Quelle für Releases und die Update-Prüfung.</summary>
    public const string GitHubRepository = "RNYSVRN/SyncForge";

    public static string GitHubUrl => $"https://github.com/{GitHubRepository}";

    /// <summary>Version für die Anzeige, z. B. "0.4.0".</summary>
    public static string VersionText { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static Version Version { get; } = UpdateChecker.TryParseVersion(VersionText, out var v) ? v : new Version(0, 0, 0);
}
