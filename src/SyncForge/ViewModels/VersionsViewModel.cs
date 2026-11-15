using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Core.Engine;
using SyncForge.Core.Models;
using SyncForge.Core.Storage;
using SyncForge.Core.Versioning;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.ViewModels;

/// <param name="SourcePath">Quelle, zu der dieser Versionsordner gehört (Ziel der Wiederherstellung).</param>
public sealed record VersionRootOption(string Name, string Root, string SourcePath)
{
    public override string ToString() => Name;
}

public sealed record VersionedFileItem(VersionedFile File)
{
    public string Path => File.RelativePath;
    public string Detail => Format.Text(File.VersionCount == 1 ? "Versions_CountOne" : "Versions_Count", Format.Number(File.VersionCount))
                            + " · " + Format.DateTime(File.Latest)
                            + (File.CurrentExists ? string.Empty : " · " + Localization.Get("Versions_Deleted"));
    public override string ToString() => $"{Path}, {Detail}";
}

public sealed record FileVersionItem(FileVersion Version)
{
    public string DateText => Format.DateTime(Version.Timestamp);
    public string SizeText => Format.Bytes(Version.Size);
    public override string ToString() => $"{DateText}, {SizeText}";
}

/// <summary>Frühere Fassungen eines Profils durchsuchen und wiederherstellen.</summary>
public sealed partial class VersionsViewModel : ObservableObject
{
    private readonly ProfileStore _store;
    private SyncProfile? _profile;
    private bool _loading;

    public VersionsViewModel(ProfileStore store)
    {
        _store = store;
    }

    public ObservableCollection<VersionRootOption> Roots { get; } = [];
    public ObservableCollection<VersionedFileItem> Files { get; } = [];
    public ObservableCollection<FileVersionItem> Versions { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial VersionRootOption? SelectedRoot { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial VersionedFileItem? SelectedFile { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial bool HasRoots { get; set; }
    [ObservableProperty] public partial bool ShowRootSelector { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasStatusMessage { get; set; }

    public bool HasSelection => SelectedFile is not null;
    public string SourcePath => SelectedRoot?.SourcePath ?? _profile?.SourcePath ?? string.Empty;

    public void Load(Guid profileId)
    {
        _profile = _store.Get(profileId);
        if (_profile is null) return;
        Title = Format.Text("Versions_Title", _profile.Name);

        _loading = true;
        Roots.Clear();
        // Versionsordner liegen je Quelle im jeweiligen Zielordner (bei mehreren Quellen im Unterordner der Quelle),
        // im Zwei-Wege-Modus zusätzlich in der Quelle selbst.
        foreach (var source in _profile.Sources)
        {
            foreach (var t in _profile.Targets)
            {
                var path = VolumeResolver.Resolve(t.Path, t.VolumeSerial);
                if (path is null) continue;
                var root = source.TargetRootFor(path);
                if (Directory.Exists(VersionStore.VersionsRoot(root)))
                    Roots.Add(new VersionRootOption(Format.Text("Versions_RootTarget", root), root, source.Path));
            }
            if (Directory.Exists(VersionStore.VersionsRoot(source.Path)))
                Roots.Add(new VersionRootOption(Format.Text("Versions_RootSource", source.Path), source.Path, source.Path));
        }
        HasRoots = Roots.Count > 0;
        ShowRootSelector = Roots.Count > 1;
        SelectedRoot = Roots.FirstOrDefault();
        _loading = false;
        RefreshFiles();
    }

    partial void OnSelectedRootChanged(VersionRootOption? value) { if (!_loading) RefreshFiles(); }
    partial void OnSearchTextChanged(string value) { if (!_loading) RefreshFiles(); }
    partial void OnSelectedFileChanged(VersionedFileItem? value) => RefreshVersions();

    public void RefreshFiles()
    {
        Files.Clear();
        if (SelectedRoot is not null)
        {
            foreach (var f in VersionStore.ListFiles(SelectedRoot.Root, SearchText)) Files.Add(new VersionedFileItem(f));
        }
        IsEmpty = Files.Count == 0;
        SelectedFile = Files.FirstOrDefault();
    }

    private void RefreshVersions()
    {
        Versions.Clear();
        if (SelectedRoot is null || SelectedFile is null) return;
        foreach (var v in VersionStore.GetVersions(SelectedRoot.Root, SelectedFile.Path)) Versions.Add(new FileVersionItem(v));
    }

    /// <summary>Zielpfad beim Wiederherstellen: ursprünglicher Ort in der Quelle (von dort wird beim nächsten Lauf synchronisiert).</summary>
    public string RestoreDestination(FileVersionItem item) => System.IO.Path.Combine(SourcePath, item.Version.RelativePath);

    public void Restore(FileVersionItem item, string destination)
    {
        try
        {
            VersionStore.Restore(item.Version, destination);
            ShowStatus(Format.Text("Versions_Restored", destination));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus(Format.Text("Versions_RestoreFailed", ex.Message));
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        HasStatusMessage = true;
    }
}
