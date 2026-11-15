using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SyncForge.Services;

public sealed record HelpTopic(string Id, string Title, string Glyph, string Markdown)
{
    /// <summary>Reiner Text für die Suche.</summary>
    public string SearchText { get; } = Controls.MarkdownView.StripInline(Markdown);
    public override string ToString() => Title;
}

public sealed class ThirdPartyComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Authors { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public List<string> Packages { get; set; } = [];
}

/// <summary>
/// Liefert die mitgelieferten Inhalte: Hilfe (Assets\Help\&lt;Sprache&gt;\*.md), Rechtstexte (Assets\Legal) und
/// Drittanbieter-Lizenzen (Assets\Licenses, erzeugt von tools\Collect-Licenses.ps1).
/// </summary>
public static partial class ContentService
{
    private static string AssetsRoot => Path.Combine(AppContext.BaseDirectory, "Assets");

    /// <summary>Inhaltsordner: "Deutsch" bei deutscher App-/Systemsprache, sonst "English".
    /// (Keine Sprachkürzel wie "de-DE" als Ordnernamen – die würde der Ressourcen-Indexer als Sprachvariante deuten.)</summary>
    public static string ContentLanguage =>
        (string.IsNullOrEmpty(Localization.CurrentLanguage) ? System.Globalization.CultureInfo.CurrentUICulture.Name : Localization.CurrentLanguage)
            .StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "Deutsch" : "English";

    public static IReadOnlyList<HelpTopic> LoadHelpTopics()
    {
        var dir = Path.Combine(AssetsRoot, "Help", ContentLanguage);
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "*.md")
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var text = File.ReadAllText(path);
                var title = Title().Match(text) is { Success: true } t ? t.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(path);
                var glyph = GlyphComment().Match(text) is { Success: true } g ? ((char)Convert.ToInt32(g.Groups[1].Value, 16)).ToString() : "";
                return new HelpTopic(Path.GetFileNameWithoutExtension(path), title, glyph, text);
            })
            .ToList();
    }

    /// <summary>Rechtstext ("eula" oder "privacy") in der passenden Sprache.</summary>
    public static string LoadLegal(string name)
    {
        var path = Path.Combine(AssetsRoot, "Legal", ContentLanguage, name + ".md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public static IReadOnlyList<ThirdPartyComponent> LoadThirdPartyComponents()
    {
        var path = Path.Combine(AssetsRoot, "Licenses", "licenses.json");
        if (!File.Exists(path)) return [];
        List<ThirdPartyComponent> list;
        try
        {
            list = JsonSerializer.Deserialize<List<ThirdPartyComponent>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException ex)
        {
            App.LogError(ex);
            return [];
        }
        foreach (var c in list.Where(c => c.Version == "runtime"))
        {
            c.Version = c.Id switch
            {
                "dotnet" => Environment.Version.ToString(),
                "sqlite" => SqliteVersion(),
                _ => null
            };
        }
        return list;
    }

    public static string LicenseFilePath(ThirdPartyComponent component) => Path.Combine(AssetsRoot, "Licenses", component.File);

    private static string? SqliteVersion()
    {
        try
        {
            using var db = new SqliteConnection("Data Source=:memory:");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT sqlite_version();";
            return cmd.ExecuteScalar() as string;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Title();

    [GeneratedRegex(@"<!--\s*glyph:([0-9A-Fa-f]{4})\s*-->")]
    private static partial Regex GlyphComment();
}
