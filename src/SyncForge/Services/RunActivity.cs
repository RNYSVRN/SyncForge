using System.Collections.Concurrent;
using SyncForge.Core.Engine;

namespace SyncForge.Services;

/// <summary>
/// Ein laufender Synchronisierungslauf (manuell oder automatisch) mit aktuellem Fortschritt über alle Ziele.
/// Wird threadsicher von den Fortschrittsmeldungen aktualisiert und von Navigation und Infobereich angezeigt.
/// </summary>
public sealed class RunActivity(Guid profileId, string profileName, CancellationTokenSource cts)
{
    private readonly ConcurrentDictionary<string, SyncProgress> _targets = new(StringComparer.OrdinalIgnoreCase);

    public Guid ProfileId { get; } = profileId;
    public string ProfileName { get; } = profileName;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public bool IsCancelling => cts.IsCancellationRequested;

    /// <summary>Noch keine Kopier-/Löschmeldung: Quelle und Ziele werden gelesen und verglichen.</summary>
    public bool IsAnalyzing => _targets.Values.All(p => p.Phase is SyncPhase.Scanning or SyncPhase.Comparing);

    /// <summary>Gefundene Dateien während der Analyse (größter Wert über alle Ziele).</summary>
    public int ScannedItems => _targets.Values.Where(p => p.Phase == SyncPhase.Scanning).Select(p => p.ItemsDone).DefaultIfEmpty().Max();

    /// <summary>Fortschritt 0–100 über alle Ziele (nach Datenmenge, sonst nach Anzahl der Aktionen).</summary>
    public double Percent
    {
        get
        {
            var working = _targets.Values.Where(p => p.Phase is not (SyncPhase.Scanning or SyncPhase.Comparing)).ToList();
            if (working.Count == 0) return 0;
            var bytesTotal = working.Sum(p => p.BytesTotal);
            if (bytesTotal > 0) return Math.Min(100, working.Sum(p => p.BytesDone) * 100.0 / bytesTotal);
            var itemsTotal = working.Sum(p => p.ItemsTotal);
            return itemsTotal > 0 ? Math.Min(100, working.Sum(p => p.ItemsDone) * 100.0 / itemsTotal) : 0;
        }
    }

    public string? CurrentFile { get; private set; }

    internal void Report(SyncProgress progress)
    {
        _targets[progress.TargetRoot] = progress;
        if (progress.CurrentFile is not null) CurrentFile = progress.CurrentFile;
    }

    public void Cancel() => cts.Cancel();
}

/// <summary>Leitet eine Fortschrittsmeldung an mehrere Empfänger weiter.</summary>
public sealed class CompositeProgress<T>(params IProgress<T>?[] targets) : IProgress<T>
{
    public void Report(T value)
    {
        foreach (var target in targets) target?.Report(value);
    }
}
