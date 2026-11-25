using System.Text.Json;
using System.Text.Json.Serialization;
using SyncForge.Core.Models;

namespace SyncForge.Core.Storage;

/// <summary>Speichert die Backup-Profile als JSON-Datei (atomar über Temp-Datei).</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private List<SyncProfile> _profiles = [];

    public ProfileStore(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "profiles.json");
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SyncProfile> Profiles
    {
        get { lock (_lock) return _profiles.ToList(); }
    }

    public SyncProfile? Get(Guid id)
    {
        lock (_lock) return _profiles.FirstOrDefault(p => p.Id == id);
    }

    public void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                _profiles = [];
                return;
            }
            try
            {
                _profiles = JsonSerializer.Deserialize<List<SyncProfile>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                // Beschädigte Datei sichern statt überschreiben.
                File.Copy(_filePath, _filePath + $".defekt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
                _profiles = [];
            }
        }
    }

    /// <summary>Fügt ein Profil hinzu oder ersetzt das mit derselben Id.</summary>
    public void Save(SyncProfile profile)
    {
        lock (_lock)
        {
            var index = _profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0) _profiles[index] = profile;
            else _profiles.Add(profile);
            Persist();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _profiles.RemoveAll(p => p.Id == id);
            Persist();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_profiles, JsonOptions));
        File.Move(tmp, _filePath, overwrite: true);
    }
}
