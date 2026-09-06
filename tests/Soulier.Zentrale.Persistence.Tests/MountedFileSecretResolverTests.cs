using Soulier.Zentrale.Application;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class MountedFileSecretResolverTests
{
    [Fact]
    public async Task Allowlisted_secret_file_is_resolved_without_exposing_value_in_ToString()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "database.password"),
                "test-secret-value",
                cancellationToken);
            var resolver = new MountedFileSecretResolver(root, ["database.password"]);

            using var secret = await resolver.ResolveAsync(
                SecretReference.Create("database.password"),
                cancellationToken);

            Assert.Equal("test-secret-value", new string(secret.Memory.Span));
            Assert.Equal("[REDACTED]", secret.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Non_allowlisted_secret_is_denied_even_if_file_exists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "other.secret"),
                "must-not-be-readable",
                cancellationToken);
            var resolver = new MountedFileSecretResolver(root, ["database.password"]);

            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                using var _ = await resolver.ResolveAsync(
                    SecretReference.Create("other.secret"),
                    cancellationToken);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_allowlisted_secret_fails_closed()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var resolver = new MountedFileSecretResolver(root, ["database.password"]);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var _ = await resolver.ResolveAsync(
                    SecretReference.Create("database.password"),
                    TestContext.Current.CancellationToken);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Oversized_secret_file_is_rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllBytesAsync(
                Path.Combine(root, "database.password"),
                new byte[(64 * 1024) + 1],
                cancellationToken);
            var resolver = new MountedFileSecretResolver(root, ["database.password"]);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var _ = await resolver.ResolveAsync(
                    SecretReference.Create("database.password"),
                    cancellationToken);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soulier-secret-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
