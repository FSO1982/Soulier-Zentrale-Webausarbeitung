using System.Security.Cryptography;
using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

public sealed class LocalFileContentStore : IContentStore
{
    public const string ProviderKey = "local-file";

    private readonly string _rootPath;
    private readonly string _stagingPath;

    public LocalFileContentStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Storage root path is required.", nameof(rootPath));
        if (!Path.IsPathFullyQualified(rootPath))
            throw new ArgumentException("Storage root path must be absolute.", nameof(rootPath));

        _rootPath = Path.GetFullPath(rootPath);
        _stagingPath = Path.Combine(_rootPath, ".staging");
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(_stagingPath);
    }

    public async Task<ContentStoreWriteResult> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException("Content stream must be readable.", nameof(content));

        var stagingFile = Path.Combine(_stagingPath, $"{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var output = new FileStream(
                stagingFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            var sizeBytes = new FileInfo(stagingFile).Length;
            var contentHash = await ComputeHashAsync(stagingFile, cancellationToken);
            var storageKey = CreateStorageKey(contentHash);
            var destination = ResolvePath(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination))
            {
                if (!await FileMatchesHashAsync(destination, contentHash, cancellationToken))
                    throw new IOException("Content-addressed storage collision or corruption detected.");
            }
            else
            {
                try
                {
                    File.Move(stagingFile, destination);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    if (!await FileMatchesHashAsync(destination, contentHash, cancellationToken))
                        throw;
                }
            }

            return new ContentStoreWriteResult(ProviderKey, storageKey, contentHash, sizeBytes);
        }
        finally
        {
            if (File.Exists(stagingFile))
                File.Delete(stagingFile);
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public async Task<bool> VerifyAsync(
        string storageKey,
        string expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedContentHash))
            return false;

        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            return false;

        var actual = await ComputeHashAsync(path, cancellationToken);
        return string.Equals(actual, expectedContentHash, StringComparison.Ordinal);
    }

    private string ResolvePath(string storageKey)
    {
        if (!TryValidateStorageKey(storageKey, out var normalizedKey))
            throw new ArgumentException("Invalid local content storage key.", nameof(storageKey));

        var relativePath = normalizedKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage path escaped configured root.");

        return fullPath;
    }

    private static string CreateStorageKey(string contentHash) =>
        $"sha256/{contentHash[..2]}/{contentHash}";

    private static bool TryValidateStorageKey(string? storageKey, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(storageKey))
            return false;

        var candidate = storageKey.Replace('\\', '/');
        var parts = candidate.Split('/', StringSplitOptions.None);
        if (parts.Length != 3 || parts[0] != "sha256")
            return false;

        var prefix = parts[1];
        var hash = parts[2];
        if (prefix.Length != 2 || hash.Length != 64 || !hash.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        if (!prefix.All(IsLowerHex) || !hash.All(IsLowerHex))
            return false;

        normalized = $"sha256/{prefix}/{hash}";
        return true;
    }

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static async Task<bool> FileMatchesHashAsync(
        string path,
        string expectedContentHash,
        CancellationToken cancellationToken) =>
        string.Equals(
            await ComputeHashAsync(path, cancellationToken),
            expectedContentHash,
            StringComparison.Ordinal);

    private static async Task<string> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(input, cancellationToken);
        return Convert.ToHexStringLower(digest);
    }
}
