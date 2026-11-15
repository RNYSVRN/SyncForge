namespace SyncForge.Core.Cli;

public enum CommandKind
{
    /// <summary>Kein Befehl: normaler Start der App.</summary>
    None,
    Run,
    RunAll,
    List,
    Help,
    /// <summary>Fehlerhafte Angabe (Text in <see cref="CommandLineOptions.Error"/>).</summary>
    Invalid
}

/// <summary>
/// Befehle für Skripte und die Aufgabenplanung:
/// <c>--run "Profil"</c> (mehrfach möglich, Name oder Id), <c>--run-all</c>, <c>--list</c>, <c>--help</c>.
/// Andere Argumente (z. B. <c>--background</c> vom Autostart) bleiben unberührt.
/// </summary>
public sealed record CommandLineOptions(CommandKind Kind, IReadOnlyList<string> Profiles, string? Error = null)
{
    public static readonly CommandLineOptions None = new(CommandKind.None, []);
    public bool IsCommand => Kind != CommandKind.None;

    /// <summary>Exitcodes, damit Skripte das Ergebnis auswerten können.</summary>
    public static class ExitCode
    {
        public const int Success = 0;
        public const int Warning = 1;
        public const int Error = 2;
        public const int Cancelled = 3;
        public const int ProfileNotFound = 4;
        public const int InvalidArguments = 5;
        public const int AlreadyRunning = 6;
    }

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var profiles = new List<string>();
        var kind = CommandKind.None;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();
            switch (arg.ToLowerInvariant())
            {
                case "--run" or "-r" or "/run":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        return new(CommandKind.Invalid, [], "--run");
                    profiles.Add(args[++i].Trim());
                    if (kind is CommandKind.None) kind = CommandKind.Run;
                    break;
                case "--run-all" or "/run-all":
                    kind = CommandKind.RunAll;
                    break;
                case "--list" or "/list":
                    if (kind is CommandKind.None) kind = CommandKind.List;
                    break;
                case "--help" or "-h" or "-?" or "/?" or "/help":
                    return new(CommandKind.Help, []);
            }
        }
        return kind == CommandKind.RunAll ? new(CommandKind.RunAll, []) : new(kind, profiles);
    }

    /// <summary>
    /// Zerlegt eine Befehlszeile wie Windows (Anführungszeichen für Namen mit Leerzeichen),
    /// z. B. die Argumente einer weitergeleiteten Aktivierung.
    /// </summary>
    public static IReadOnlyList<string> Split(string? commandLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine)) return result;
        var current = new System.Text.StringBuilder();
        var quoted = false;
        var hasToken = false;
        foreach (var c in commandLine)
        {
            if (c == '"') { quoted = !quoted; hasToken = true; continue; }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (hasToken) { result.Add(current.ToString()); current.Clear(); hasToken = false; }
                continue;
            }
            current.Append(c);
            hasToken = true;
        }
        if (hasToken) result.Add(current.ToString());
        return result;
    }
}
