namespace SyncForge.Core.Tests;

/// <summary>Temporäre Quell-/Zielordner für einen Test; wird danach entfernt.</summary>
internal sealed class TestFolder : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SyncForgeTests", Guid.NewGuid().ToString("N"));
    public string Source => Path.Combine(Root, "source");
    public string Target => Path.Combine(Root, "target");

    public TestFolder()
    {
        Directory.CreateDirectory(Source);
    }

    public string Write(string root, string relative, string content, DateTime? lastWriteUtc = null)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteUtc ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        return path;
    }

    public string Src(string relative, string content, DateTime? t = null) => Write(Source, relative, content, t);
    public string Dst(string relative, string content, DateTime? t = null) => Write(Target, relative, content, t);

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Aufräumen ist best effort.
        }
    }
}
