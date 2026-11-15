using System.Globalization;
using SyncForge.Core.Models;
using SyncForge.Services;

namespace SyncForge.Helpers;

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Datenmenge lesbar, z. B. "1,4 GB" (Kultur der App-Sprache).</summary>
    public static string Bytes(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 ? "0" : value < 10 ? "0.0" : "0";
        return value.ToString(format, Culture) + " " + Units[unit];
    }

    public static string Duration(TimeSpan d) =>
        d.TotalHours >= 1 ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00} h"
        : d.TotalMinutes >= 1 ? $"{d.Minutes}:{d.Seconds:00} min"
        : $"{Math.Max(1, (int)Math.Round(d.TotalSeconds))} s";

    public static string DateTime(DateTimeOffset value) => value.LocalDateTime.ToString("g", Culture);

    public static string Number(int n) => n.ToString("N0", Culture);

    public static string Text(string key, params object[] args) =>
        string.Format(Culture, Localization.Get(key), args);

    public static string ModeName(SyncMode mode) => Localization.Get(mode switch
    {
        SyncMode.Versioned => "Mode_Versioned",
        SyncMode.TwoWay => "Mode_TwoWay",
        _ => "Mode_Mirror"
    });

    public static string StatusName(RunStatus status) => Localization.Get(status switch
    {
        RunStatus.Success => "Status_Success",
        RunStatus.Warning => "Status_Warning",
        RunStatus.Error => "Status_Error",
        RunStatus.Cancelled => "Status_Cancelled",
        _ => "Status_None"
    });

    /// <summary>Segoe-Fluent-Glyph für einen Laufstatus.</summary>
    public static string StatusGlyph(RunStatus status) => status switch
    {
        RunStatus.Success => "",   // Häkchen
        RunStatus.Warning => "",   // Warnung
        RunStatus.Error => "",     // Fehler
        RunStatus.Cancelled => "", // Abgebrochen
        _ => ""                    // Sync
    };

    /// <summary>Kultur passend zur UI-Sprache (für Zahlen- und Datumsformate).</summary>
    public static CultureInfo Culture
    {
        get
        {
            var lang = Localization.CurrentLanguage;
            try { return string.IsNullOrEmpty(lang) ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(lang); }
            catch (CultureNotFoundException) { return CultureInfo.CurrentUICulture; }
        }
    }
}
