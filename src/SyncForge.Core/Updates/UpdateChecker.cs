using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SyncForge.Core.Updates;

/// <summary>Neueste veröffentlichte Version auf GitHub.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, DateTimeOffset? PublishedAt);

/// <summary>
/// Fragt bei GitHub die neueste veröffentlichte Version ab (ohne Vorabversionen und Entwürfe).
/// Es wird nichts heruntergeladen und außer der Anfrage selbst (IP-Adresse, App-Version im User-Agent)
/// nichts übertragen.
/// </summary>
public static class UpdateChecker
{
    /// <returns>Die neueste Version oder null, wenn es noch kein Release gibt.</returns>
    public static async Task<ReleaseInfo?> GetLatestReleaseAsync(HttpClient http, string repository, Version currentVersion, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SyncForge", Normalize(currentVersion).ToString(3)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // noch kein Release
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return Parse(json.RootElement);
    }

    internal static ReleaseInfo? Parse(JsonElement release)
    {
        if (Bool(release, "draft") || Bool(release, "prerelease")) return null;
        var tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        var url = release.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        if (tag is null || url is null || !TryParseVersion(tag, out var version)) return null;
        // Nur Links auf GitHub öffnen, egal was in der Antwort steht.
        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) return null;
        DateTimeOffset? published = release.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(p.GetString(), out var d) ? d : null;
        return new ReleaseInfo(version, tag, url, published);
    }

    /// <summary>"v1.2.3", "1.2", "V2.0.0-beta+abc" → Version (Zusätze nach - oder + werden ignoriert).</summary>
    public static bool TryParseVersion(string text, out Version version)
    {
        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        var cut = s.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0) s = s[..cut];
        if (!s.Contains('.')) s += ".0";
        if (Version.TryParse(s, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    /// <summary>True, wenn <paramref name="latest"/> neuer ist (Haupt-, Neben-, Patch-Version).</summary>
    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    private static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
