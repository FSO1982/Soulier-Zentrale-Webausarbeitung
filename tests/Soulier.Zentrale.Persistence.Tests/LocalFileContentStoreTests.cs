using System.Security.Cryptography;
using System.Text;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class LocalFileContentStoreTests
{
    [Fact]
    public async Task Store_read_and_verify_round_trip_uses_content_hash_key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTempRoot();

        try
        {
            var store = new LocalFileContentStore(root);
            var bytes = Encoding.UTF8.GetBytes("Soulier Gate-3 storage test");
            await using var input = new MemoryStream(bytes);

            var result = await store.StoreAsync(input, cancellationToken);
            var expectedHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

            Assert.Equal(LocalFileContentStore.ProviderKey, result.StorageProvider);
            Assert.Equal(expectedHash, result.ContentHash);
            Assert.Equal($"sha256/{expectedHash[..2]}/{expectedHash}", result.StorageKey);
            Assert.Equal(bytes.Length, result.SizeBytes);
            Assert.True(await store.VerifyAsync(result.StorageKey, expectedHash, cancellationToken));

            await using var stored = await store.OpenReadAsync(result.StorageKey, cancellationToken);
            using var reader = new StreamReader(stored, Encoding.UTF8);
            Assert.Equal("Soulier Gate-3 storage test", await reader.ReadToEndAsync(cancellationToken));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task Repeated_identical_content_is_deduplicated_without_overwrite_api()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTempRoot();

        try
        {
            var store = new LocalFileContentStore(root);
            var bytes = Encoding.UTF8.GetBytes("same immutable content");

            await using var firstInput = new MemoryStream(bytes);
            var first = await store.StoreAsync(firstInput, cancellationToken);
            await using var secondInput = new MemoryStream(bytes);
            var second = await store.StoreAsync(secondInput, cancellationToken);

            Assert.Equal(first.StorageKey, second.StorageKey);
            Assert.Equal(first.ContentHash, second.ContentHash);
            Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "sha256"), "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task Tampered_file_fails_hash_verification_and_is_not_silently_overwritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTempRoot();

        try
        {
            var store = new LocalFileContentStore(root);
            var originalBytes = Encoding.UTF8.GetBytes("original");
            await using var original = new MemoryStream(originalBytes);
            var result = await store.StoreAsync(original, cancellationToken);

            var physicalPath = Path.Combine(root, result.StorageKey.Replace('/', Path.DirectorySeparatorChar));
            await File.WriteAllTextAsync(physicalPath, "tampered", cancellationToken);

            Assert.False(await store.VerifyAsync(result.StorageKey, result.ContentHash, cancellationToken));

            await using var retry = new MemoryStream(originalBytes);
            await Assert.ThrowsAsync<IOException>(() => store.StoreAsync(retry, cancellationToken));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("sha256/aa/../../secret")]
    [InlineData("sha256/zz/zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task Arbitrary_or_traversal_storage_keys_are_rejected(string key)
    {
        var root = CreateTempRoot();

        try
        {
            var store = new LocalFileContentStore(root);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.OpenReadAsync(key, TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "soulier-zentrale-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
