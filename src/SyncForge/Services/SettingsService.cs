using System.Text.Json;
using System.Text.Json.Serialization;
using SyncForge.Models;

namespace SyncForge.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SyncForge");

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new();
            }
        }
        catch
        {
            // Beschädigte Datei: mit Standardwerten weiterarbeiten.
            Current = new();
        }
    }

    /// <summary>Ersetzt alle Einstellungen (z. B. beim Import) und speichert.</summary>
    public void Replace(AppSettings settings)
    {
        Current = settings;
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(tmp, SettingsPath, overwrite: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
