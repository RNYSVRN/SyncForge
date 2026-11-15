using System.Runtime.InteropServices;
using SyncForge.Core.Cli;
using SyncForge.Core.History;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Helpers;

namespace SyncForge.Services;

/// <summary>
/// Befehlszeile ohne Fenster, z. B. für Skripte und die Windows-Aufgabenplanung:
/// <c>SyncForge.exe --run "Dokumente"</c>. Ergebnis steht in der Historie, Ausgabe in der aufrufenden Konsole,
/// der Exitcode sagt, wie es lief (<see cref="CommandLineOptions.ExitCode"/>).
/// </summary>
public static class CommandLineRunner
{
    public static int Run(CommandLineOptions options)
    {
        AttachToParentConsole();
        var settings = new SettingsService();
        settings.Load();
        Localization.ApplyLanguage(settings.Current.Language);

        switch (options.Kind)
        {
            case CommandKind.Help:
                PrintUsage();
                return CommandLineOptions.ExitCode.Success;
            case CommandKind.Invalid:
                Print(Format.Text("Cli_Invalid", options.Error ?? string.Empty));
                PrintUsage();
                return CommandLineOptions.ExitCode.InvalidArguments;
        }

        var profiles = new ProfileStore(SettingsService.DataDirectory);
        profiles.Load();

        if (options.Kind == CommandKind.List)
        {
            if (profiles.Profiles.Count == 0) Print(Localization.Get("Cli_NoProfiles"));
            foreach (var p in profiles.Profiles) Print($"{p.Name}  ({p.Id})");
            return CommandLineOptions.ExitCode.Success;
        }

        // Erst alle Namen auflösen, dann laufen lassen: ein Tippfehler startet nicht die Hälfte der Profile.
        var selected = new List<SyncProfile>();
        if (options.Kind == CommandKind.RunAll) selected.AddRange(profiles.Profiles);
        foreach (var name in options.Profiles)
        {
            if (Find(profiles, name) is { } p) { if (!selected.Contains(p)) selected.Add(p); }
            else
            {
                Print(Format.Text("Cli_NotFound", name));
                return CommandLineOptions.ExitCode.ProfileNotFound;
            }
        }
        if (selected.Count == 0)
        {
            Print(Localization.Get("Cli_NoProfiles"));
            return CommandLineOptions.ExitCode.Success;
        }

        var history = new HistoryStore(Path.Combine(SettingsService.DataDirectory, "history.db"));
        history.Initialize();
        var coordinator = new SyncCoordinator(profiles, history, settings);

        var worst = CommandLineOptions.ExitCode.Success;
        foreach (var profile in selected)
        {
            Print(Format.Text("Cli_Running", profile.Name));
            var result = Task.Run(() => coordinator.RunAsync(profile.Id, RunTrigger.CommandLine)).GetAwaiter().GetResult();
            int code;
            if (result is null)
            {
                Print(Format.Text("Cli_AlreadyRunning", profile.Name));
                code = CommandLineOptions.ExitCode.AlreadyRunning;
            }
            else
            {
                Print(Format.Text("Cli_Result", profile.Name, Format.StatusName(result.Run.Status), result.Summary));
                code = result.Run.Status switch
                {
                    RunStatus.Success => CommandLineOptions.ExitCode.Success,
                    RunStatus.Warning => CommandLineOptions.ExitCode.Warning,
                    RunStatus.Cancelled => CommandLineOptions.ExitCode.Cancelled,
                    _ => CommandLineOptions.ExitCode.Error
                };
            }
            worst = Math.Max(worst, code);
        }
        return worst;
    }

    /// <summary>Hinweis, wenn der Befehl an das bereits geöffnete SyncForge übergeben wurde.</summary>
    public static void ReportHandedOver()
    {
        AttachToParentConsole();
        var settings = new SettingsService();
        settings.Load();
        Localization.ApplyLanguage(settings.Current.Language);
        Print(Localization.Get("Cli_HandedOver"));
    }

    /// <summary>Profil per Id oder Name (ohne Groß-/Kleinschreibung).</summary>
    public static SyncProfile? Find(ProfileStore profiles, string nameOrId) =>
        Guid.TryParse(nameOrId, out var id)
            ? profiles.Get(id)
            : profiles.Profiles.FirstOrDefault(p => string.Equals(p.Name.Trim(), nameOrId.Trim(), StringComparison.OrdinalIgnoreCase));

    private static void PrintUsage()
    {
        Print(Localization.Get("Cli_UsageTitle"));
        Print($"  --run \"<Name>\"  {Localization.Get("Cli_UsageRun")}");
        Print($"  --run-all       {Localization.Get("Cli_UsageRunAll")}");
        Print($"  --list          {Localization.Get("Cli_UsageList")}");
        Print($"  --help          {Localization.Get("Cli_UsageHelp")}");
        Print(Localization.Get("Cli_UsageExit"));
    }

    private static void Print(string text)
    {
        try { Console.WriteLine(text); } catch { /* keine Konsole: nur Historie und Exitcode */ }
    }

    /// <summary>SyncForge ist eine Fenster-App: Für Ausgaben an die Konsole hängen, aus der gestartet wurde.</summary>
    private static void AttachToParentConsole()
    {
        if (AttachConsole(-1))
        {
            try
            {
                var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdout);
                Console.WriteLine();
            }
            catch { /* best effort */ }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
