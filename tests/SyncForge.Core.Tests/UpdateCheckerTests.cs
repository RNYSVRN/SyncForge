using System.Net;
using System.Text;
using SyncForge.Core.Updates;

namespace SyncForge.Core.Tests;

public class UpdateCheckerTests
{
    /// <summary>Antwortet wie GitHub, ohne Netzwerk.</summary>
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static async Task<(ReleaseInfo? Release, HttpRequestMessage Request)> Check(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        using var http = new HttpClient(handler);
        var release = await UpdateChecker.GetLatestReleaseAsync(http, "besitzer/SyncForge", new Version(0, 4, 0), TestContext.Current.CancellationToken);
        return (release, handler.Request!);
    }

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("V2.0.0-beta.1", 2, 0, 0)]
    [InlineData("v0.5.1+abc", 0, 5, 1)]
    [InlineData("3", 3, 0, 0)]
    public void TryParseVersion_Accepts(string tag, int major, int minor, int patch)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var v));
        Assert.Equal(new Version(major, minor, patch), v);
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("")]
    public void TryParseVersion_RejectsGarbage(string tag) => Assert.False(UpdateChecker.TryParseVersion(tag, out _));

    [Fact]
    public void IsNewer_IgnoresRevisionAndMissingParts()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(0, 5, 0), new Version(0, 4, 0)));
        Assert.True(UpdateChecker.IsNewer(new Version(0, 4, 1), new Version(0, 4)));
        Assert.False(UpdateChecker.IsNewer(new Version(0, 4, 0), new Version(0, 4, 0, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(0, 3, 9), new Version(0, 4, 0)));
    }

    [Fact]
    public async Task LatestRelease_IsParsed_AndRequestIsMinimal()
    {
        var (release, request) = await Check(HttpStatusCode.OK, """
            {"tag_name":"v0.5.0","html_url":"https://github.com/besitzer/SyncForge/releases/tag/v0.5.0",
             "draft":false,"prerelease":false,"published_at":"2026-12-01T10:00:00Z","body":"..."}
            """);

        Assert.NotNull(release);
        Assert.Equal(new Version(0, 5, 0), release.Version);
        Assert.Equal("https://github.com/besitzer/SyncForge/releases/tag/v0.5.0", release.PageUrl);
        Assert.Equal("https://api.github.com/repos/besitzer/SyncForge/releases/latest", request.RequestUri!.ToString());
        Assert.Equal("SyncForge/0.4.0", request.Headers.UserAgent.ToString());
        Assert.Null(request.Content); // nichts außer der Anfrage selbst
    }

    [Fact]
    public async Task NoReleaseYet_ReturnsNull()
    {
        var (release, _) = await Check(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        Assert.Null(release);
    }

    [Fact]
    public async Task Prerelease_AndForeignLinks_AreIgnored()
    {
        var (pre, _) = await Check(HttpStatusCode.OK, """{"tag_name":"v9.0.0","html_url":"https://github.com/x/y","prerelease":true}""");
        Assert.Null(pre);
        var (foreign, _) = await Check(HttpStatusCode.OK, """{"tag_name":"v9.0.0","html_url":"https://example.com/download"}""");
        Assert.Null(foreign);
    }

    [Fact]
    public async Task ServerError_Throws()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Check(HttpStatusCode.InternalServerError, "{}"));
    }
}
