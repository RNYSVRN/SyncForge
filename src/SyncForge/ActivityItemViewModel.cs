using CommunityToolkit.Mvvm.ComponentModel;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge;

/// <summary>Anzeige eines laufenden Laufs in der Navigation (wird bei jeder Meldung aktualisiert, nicht neu erzeugt).</summary>
public sealed partial class ActivityItemViewModel(Guid profileId, string name) : ObservableObject
{
    public Guid ProfileId { get; } = profileId;
    public string Name { get; } = string.IsNullOrEmpty(name) ? "SyncForge" : name;

    [ObservableProperty] public partial double Percent { get; set; }
    [ObservableProperty] public partial bool IsIndeterminate { get; set; } = true;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string ToolTip { get; set; } = string.Empty;
    [ObservableProperty] public partial bool CanCancel { get; set; } = true;

    public void Update(RunActivity a)
    {
        CanCancel = !a.IsCancelling;
        if (a.IsCancelling)
        {
            IsIndeterminate = true;
            StatusText = Localization.Get("Activity_Cancelling");
        }
        else if (a.IsAnalyzing)
        {
            IsIndeterminate = true;
            StatusText = a.ScannedItems > 0
                ? Format.Text("Activity_Scanning", Format.Number(a.ScannedItems))
                : Localization.Get("Activity_Analyzing");
        }
        else
        {
            IsIndeterminate = false;
            Percent = a.Percent;
            StatusText = Format.Text("Activity_Copying", a.Percent.ToString("0", Format.Culture));
        }
        ToolTip = a.CurrentFile is { } file ? $"{Name}{Environment.NewLine}{StatusText}{Environment.NewLine}{file}" : $"{Name}{Environment.NewLine}{StatusText}";
    }

    public override string ToString() => $"{Name}, {StatusText}"; // Name für Screenreader
}
