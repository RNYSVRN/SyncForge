using System.Security.Cryptography;

namespace SyncForge.Core.Engine;

internal static class FileHasher
{
    public static async Task<byte[]> ComputeAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, ct);
    }

    public static async Task<bool> AreEqualAsync(string pathA, string pathB, CancellationToken ct)
    {
        var a = ComputeAsync(pathA, ct);
        var b = ComputeAsync(pathB, ct);
        await Task.WhenAll(a, b);
        return a.Result.AsSpan().SequenceEqual(b.Result);
    }
}
