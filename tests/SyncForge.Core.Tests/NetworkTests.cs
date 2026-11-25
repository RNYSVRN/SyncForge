using SyncForge.Core.Network;

namespace SyncForge.Core.Tests;

public class NetworkTests
{
    [Theory]
    [InlineData(@"\\nas\backup", @"\\nas\backup")]
    [InlineData(@"\\nas\backup\Dokumente\2026", @"\\nas\backup")]
    [InlineData(@"\\192.168.1.10\share\", @"\\192.168.1.10\share")]
    [InlineData(@"//nas/backup/x", @"\\nas\backup")]
    [InlineData(@"\\?\UNC\nas\backup\x", @"\\nas\backup")]
    public void ShareRoot_FromNetworkPaths(string path, string expected) => Assert.Equal(expected, NetworkShares.GetShareRoot(path));

    [Theory]
    [InlineData(@"C:\Daten")]
    [InlineData(@"D:\")]
    [InlineData(@"\\nas")]
    [InlineData(@"\\?\C:\Daten")]
    [InlineData("")]
    [InlineData(null)]
    public void ShareRoot_NullForOtherPaths(string? path) => Assert.Null(NetworkShares.GetShareRoot(path));

    [Fact]
    public void LocalPath_NeedsNoConnection()
    {
        var outcome = NetworkShares.EnsureAccess(Path.GetTempPath());
        Assert.Equal(NetworkAccessResult.Available, outcome.Result);
        Assert.True(outcome.Success);
    }

    [Fact]
    public void Credentials_RoundTrip_AndDelete()
    {
        // Eigene Test-Freigabe, die es nicht gibt: berührt keine echten Einträge.
        var root = $@"\\syncforge-test-{Guid.NewGuid():N}\share";
        try
        {
            Assert.Null(NetworkCredentials.Load(root));
            NetworkCredentials.Save(root, @"NAS\ronny", "geheim äöü 123");
            var loaded = NetworkCredentials.Load(root.ToUpperInvariant() + @"\"); // Schreibweise egal
            Assert.NotNull(loaded);
            Assert.Equal(@"NAS\ronny", loaded.Value.UserName);
            Assert.Equal("geheim äöü 123", loaded.Value.Password);

            NetworkCredentials.Save(root, "anderer", "neu");
            Assert.Equal("anderer", NetworkCredentials.GetUserName(root));
        }
        finally
        {
            NetworkCredentials.Delete(root);
        }
        Assert.False(NetworkCredentials.Exists(root));
    }

    [Fact]
    public void UnreachableShare_WithoutCredentials_IsReported()
    {
        var outcome = NetworkShares.EnsureAccess($@"\\syncforge-test-{Guid.NewGuid():N}.invalid\share\ordner");
        Assert.Equal(NetworkAccessResult.NoCredentials, outcome.Result);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void UnknownServer_WithCredentials_IsNotFound()
    {
        var root = $@"\\syncforge-test-{Guid.NewGuid():N}.invalid\share";
        try
        {
            NetworkCredentials.Save(root, "user", "pw");
            var outcome = NetworkShares.EnsureAccess(root + @"\ordner");
            Assert.False(outcome.Success);
            Assert.Contains(outcome.Result, new[] { NetworkAccessResult.NotFound, NetworkAccessResult.Failed });
            Assert.NotEqual(0, outcome.ErrorCode);
        }
        finally
        {
            NetworkCredentials.Delete(root);
        }
    }
}
