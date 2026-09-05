using Soulier.Zentrale.Application;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class EnvironmentSecretResolverTests
{
    [Fact]
    public void Reference_rejects_path_or_shell_like_keys()
    {
        Assert.Throws<ArgumentException>(() => SecretReference.Create("../value"));
        Assert.Throws<ArgumentException>(() => SecretReference.Create("value$name"));
        Assert.Throws<ArgumentException>(() => SecretReference.Create("UpperCase"));
    }

    [Fact]
    public async Task Resolver_returns_only_allowlisted_value_and_string_representation_is_redacted()
    {
        var resolver = new EnvironmentSecretResolver(
            new Dictionary<string, string>
            {
                ["service.credential"] = "SOULIER_TEST_VALUE"
            },
            name => name == "SOULIER_TEST_VALUE" ? "placeholder-only" : null);

        using var resolved = await resolver.ResolveAsync(
            SecretReference.Create("service.credential"),
            TestContext.Current.CancellationToken);

        Assert.Equal("[REDACTED]", resolved.ToString());
        Assert.Equal("placeholder-only", new string(resolved.Memory.Span));
    }

    [Fact]
    public async Task Unknown_reference_is_denied_instead_of_becoming_arbitrary_environment_access()
    {
        var resolver = new EnvironmentSecretResolver(
            new Dictionary<string, string>
            {
                ["service.credential"] = "SOULIER_TEST_VALUE"
            },
            _ => "placeholder-only");

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            using var resolved = await resolver.ResolveAsync(
                SecretReference.Create("other.credential"),
                TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Missing_allowlisted_value_fails_closed()
    {
        var resolver = new EnvironmentSecretResolver(
            new Dictionary<string, string>
            {
                ["service.credential"] = "SOULIER_TEST_VALUE"
            },
            _ => null);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var resolved = await resolver.ResolveAsync(
                SecretReference.Create("service.credential"),
                TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Disposed_value_cannot_be_read_again()
    {
        var resolver = new EnvironmentSecretResolver(
            new Dictionary<string, string>
            {
                ["service.credential"] = "SOULIER_TEST_VALUE"
            },
            _ => "placeholder-only");

        var resolved = await resolver.ResolveAsync(
            SecretReference.Create("service.credential"),
            TestContext.Current.CancellationToken);
        resolved.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = resolved.Memory);
    }
}
