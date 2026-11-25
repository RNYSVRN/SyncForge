using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Helpers;

namespace SyncForge.ViewModels;

/// <summary>Anzeige eines Profils in der Liste.</summary>
public sealed class ProfileItemViewModel(SyncProfile profile)
{
    public SyncProfile Profile { get; } = profile;
    public Guid Id => Profile.Id;
    public string Name => Profile.Name;

    public string Route
    {
        get
        {
            var targets = Profile.Targets.Count(t => t.Enabled);
            var sources = Profile.Sources.Count > 1 ? Format.Text("Profiles_SourcesMany", Profile.Sources.Count) : Profile.SourcePath;
            return Format.Text(targets == 1 ? "Profiles_RouteOne" : "Profiles_RouteMany", sources, targets);
        }
    }

    public string ModeText => Format.ModeName(Profile.Mode);

    public string LastRunText => Profile.LastRunAt is { } at
        ? Format.Text("Profiles_LastRun", Format.DateTime(at), Format.StatusName(Profile.LastRunStatus))
        : Format.Text("Profiles_NeverRun");

    /// <summary>Segoe-Fluent-Glyph für den Status des letzten Laufs.</summary>
    public string StatusGlyph => Format.StatusGlyph(Profile.LastRunStatus);

    public Microsoft.UI.Xaml.Style? StatusStyle => Format.StatusIconStyle(Profile.LastRunAt is null ? RunStatus.None : Profile.LastRunStatus);

    public override string ToString() => $"{Name}, {LastRunText}"; // Name für Screenreader
}

public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileStore _store;

    public ProfilesViewModel(ProfileStore store)
    {
        _store = store;
        Reload();
    }

    public ObservableCollection<ProfileItemViewModel> Items { get; } = [];

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public void Reload()
    {
        Items.Clear();
        foreach (var p in _store.Profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(new ProfileItemViewModel(p));
        }
        IsEmpty = Items.Count == 0;
    }

    public void Duplicate(ProfileItemViewModel item)
    {
        var copy = item.Profile.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name = Format.Text("Profiles_CopyName", item.Profile.Name);
        copy.CreatedAt = DateTimeOffset.Now;
        copy.LastRunAt = null;
        copy.LastRunStatus = RunStatus.None;
        copy.LastRunSummary = null;
        foreach (var t in copy.Targets) t.Id = Guid.NewGuid();
        _store.Save(copy);
        Reload();
    }

    public void Delete(ProfileItemViewModel item)
    {
        _store.Delete(item.Id);
        Reload();
    }
}
