using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Soulier.Zentrale.Api;

namespace Soulier.Zentrale.Tests;

public sealed class RuntimeConfigurationTests
{
    [Fact]
    public void Production_requires_database_connection_string()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateEnvironment(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SoulierRuntimeConfiguration.ResolveDatabaseConnectionString(configuration, environment));

        Assert.Contains("ConnectionStrings:Soulier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_may_start_without_database_for_isolated_engineering_checks()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateEnvironment(Environments.Development);

        var connectionString = SoulierRuntimeConfiguration.ResolveDatabaseConnectionString(
            configuration,
            environment);

        Assert.Null(connectionString);
    }

    [Fact]
    public void Production_requires_oidc_authentication()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateEnvironment(Environments.Production);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SoulierAuthentication.ResolveOptions(configuration, environment));

        Assert.Contains("mandatory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_test_oidc_authority_must_use_https()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Soulier:Identity:Oidc:Enabled"] = "true",
                ["Soulier:Identity:Oidc:Authority"] = "http://identity.internal/",
                ["Soulier:Identity:Oidc:Audience"] = "soulier-zentrale"
            })
            .Build();
        var environment = CreateEnvironment(Environments.Development);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SoulierAuthentication.ResolveOptions(configuration, environment));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_is_fail_closed_when_database_is_not_configured()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("DATABASE_NOT_CONFIGURED", body, StringComparison.Ordinal);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName) =>
        new TestHostEnvironment
        {
            EnvironmentName = environmentName,
            ApplicationName = "Soulier.Zentrale.Tests",
            ContentRootPath = Directory.GetCurrentDirectory(),
            ContentRootFileProvider = new NullFileProvider()
        };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Soulier.Zentrale.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
