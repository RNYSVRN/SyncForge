using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;

namespace SyncForge.Services;

/// <summary>Sprachwahl (Deutsch/Englisch) und Zugriff auf übersetzte Texte aus dem Code.</summary>
public static class Localization
{
    public static readonly string[] SupportedLanguages = ["de-DE", "en-US"];

    private static ResourceLoader? _loader;

    /// <summary>Gewählte Sprache ("de-DE"/"en-US") oder leer für die Systemsprache.</summary>
    public static string CurrentLanguage { get; private set; } = string.Empty;

    /// <summary>Muss vor dem Erzeugen des ersten Fensters aufgerufen werden.</summary>
    public static void ApplyLanguage(string language)
    {
        // Leerer String ist kein gültiger Wert; ohne Override gilt automatisch die Systemsprache.
        if (SupportedLanguages.Contains(language))
        {
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = language;
                CurrentLanguage = language;
            }
            catch
            {
                // Sprache nicht verfügbar: Systemsprache verwenden.
            }
        }
        _loader = null;
    }

    public static string Get(string key)
    {
        _loader ??= new ResourceLoader();
        var value = _loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}
