using System.Text.Json;
using System.Text.Json.Serialization;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Models;

namespace SyncForge.Services;

/// <summary>Sicherung aller Profile und Einstellungen in eine Datei – z. B. für den Umzug auf einen anderen PC.</summary>
public sealed class ImportExportService
{
    private const string FormatId = "SyncForge.Export";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ProfileStore _profiles;
    private readonly SettingsService _settings;

    public ImportExportService(ProfileStore profiles, SettingsService settings)
    {
        _profiles = profiles;
        _settings = settings;
    }

    public sealed class ExportFile
    {
        public string Format { get; set; } = FormatId;
        public int Version { get; set; } = FormatVersion;
        public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
        public AppSettings? Settings { get; set; }
        public List<SyncProfile> Profiles { get; set; } = [];
    }

    public async Task ExportAsync(string path)
    {
        var file = new ExportFile { Settings = _settings.Current, Profiles = _profiles.Profiles.ToList() };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    /// <summary>Liest eine Exportdatei; null, wenn es keine gültige SyncForge-Sicherung ist.</summary>
    public static async Task<ExportFile?> ReadAsync(string path)
    {
        try
        {
            var file = JsonSerializer.Deserialize<ExportFile>(await File.ReadAllTextAsync(path), JsonOptions);
            return file is { Format: FormatId } && file.Version <= FormatVersion ? file : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Übernimmt Profile (gleiche Id = ersetzen, sonst hinzufügen) und optional die Einstellungen.
    /// Zeitpläne beginnen neu; Laufzeitdaten des anderen PCs (letzter Lauf) werden nicht übernommen.
    /// </summary>
    public int Import(ExportFile file, bool includeSettings)
    {
        foreach (var p in file.Profiles)
        {
            p.LastRunAt = null;
            p.LastRunStatus = RunStatus.None;
            p.LastRunSummary = null;
            p.Automation.ScheduleAnchor = null;
            _profiles.Save(p);
        }

        if (includeSettings && file.Settings is { } imported)
        {
            imported.WindowWidth = _settings.Current.WindowWidth;
            imported.WindowHeight = _settings.Current.WindowHeight;
            imported.StartWithWindows = StartupService.IsEnabled(); // Autostart ist gerätespezifisch
            _settings.Replace(imported);
        }
        return file.Profiles.Count;
    }
}
