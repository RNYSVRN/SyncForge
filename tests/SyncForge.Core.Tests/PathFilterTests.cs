using SyncForge.Core.Filtering;
using SyncForge.Core.Models;

namespace SyncForge.Core.Tests;

public class PathFilterTests
{
    private static PathFilter Filter(params string[] excludes) =>
        new(new FilterSettings { ExcludePatterns = [.. excludes], ExcludeSystem = false });

    [Theory]
    [InlineData("*.tmp", "a.tmp", true)]
    [InlineData("*.tmp", @"sub\deep\a.TMP", true)]
    [InlineData("*.tmp", "a.tmpx", false)]
    [InlineData("~$*", "~$Bericht.docx", true)]
    [InlineData("Thumbs.db", @"Bilder\Thumbs.db", true)]
    [InlineData(@"Projekte\*.log", @"Projekte\a.log", true)]
    [InlineData(@"Projekte\*.log", @"Projekte\sub\a.log", false)]
    [InlineData(@"Projekte\**\*.log", @"Projekte\sub\x\a.log", true)]
    [InlineData(@"Projekte\**\*.log", @"Projekte\a.log", true)]
    [InlineData("datei?.txt", "datei1.txt", true)]
    [InlineData("datei?.txt", "datei12.txt", false)]
    public void FileExcludePatterns(string pattern, string path, bool excluded) =>
        Assert.Equal(excluded, Filter(pattern).IsFileExcluded(path, FileAttributes.Normal, 10));

    [Fact]
    public void DirectoryPattern_MatchesDirectoriesOnly()
    {
        var f = Filter(@"node_modules\");
        Assert.True(f.IsDirectoryExcluded(@"web\node_modules", FileAttributes.Directory));
        Assert.False(f.IsFileExcluded("node_modules", FileAttributes.Normal, 1));
    }

    [Fact]
    public void IncludePatterns_RestrictFiles_ButNotDirectories()
    {
        var f = new PathFilter(new FilterSettings { ExcludePatterns = [], IncludePatterns = ["*.jpg", "*.png"], ExcludeSystem = false });
        Assert.False(f.IsFileExcluded(@"urlaub\bild.JPG", FileAttributes.Normal, 1));
        Assert.True(f.IsFileExcluded(@"urlaub\notiz.txt", FileAttributes.Normal, 1));
        Assert.False(f.IsDirectoryExcluded("urlaub", FileAttributes.Directory));
    }

    [Fact]
    public void SizeAndAttributeLimits()
    {
        var f = new PathFilter(new FilterSettings
        {
            ExcludePatterns = [], ExcludeHidden = true, ExcludeSystem = true, MinSizeBytes = 10, MaxSizeBytes = 100
        });
        Assert.True(f.IsFileExcluded("a", FileAttributes.Normal, 5));
        Assert.True(f.IsFileExcluded("a", FileAttributes.Normal, 500));
        Assert.False(f.IsFileExcluded("a", FileAttributes.Normal, 50));
        Assert.True(f.IsFileExcluded("a", FileAttributes.Hidden, 50));
        Assert.True(f.IsDirectoryExcluded("d", FileAttributes.Directory | FileAttributes.System));
    }

    [Fact]
    public void CommentsAndEmptyLinesAreIgnored()
    {
        var f = Filter("# Kommentar", "  ", "*.bak");
        Assert.False(f.IsFileExcluded("Kommentar", FileAttributes.Normal, 1));
        Assert.True(f.IsFileExcluded("x.bak", FileAttributes.Normal, 1));
    }
}
