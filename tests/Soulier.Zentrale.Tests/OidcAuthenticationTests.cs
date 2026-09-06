using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Soulier.Zentrale.Api;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class OidcAuthenticationTests
{
    private static readonly RSA SigningRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey SigningKey = new(SigningRsa) { KeyId = "gate3-oidc-test-key" };

    [Fact]
    public async Task Valid_signed_token_for_enrolled_active_human_returns_verified_identity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject: "frank-test", audience: SoulierAuthentication.TestingAudience));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("frank-test", body, StringComparison.Ordinal);
        Assert.Contains("codex-test", body, StringComparison.Ordinal);
        Assert.Contains(SoulierAuthentication.TestingAuthority, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_signed_token_for_unregistered_human_is_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject: "not-enrolled", audience: SoulierAuthentication.TestingAudience));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Valid_signed_token_for_disabled_human_is_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory(HumanPrincipalStatus.Disabled);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject: "frank-test", audience: SoulierAuthentication.TestingAudience));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_token_is_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_audience_is_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject: "frank-test", audience: "wrong-audience"));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(
                subject: "frank-test",
                audience: SoulierAuthentication.TestingAudience,
                expiresUtc: DateTime.UtcNow.AddMinutes(-5)));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_without_subject_is_forbidden_by_human_policy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new OidcFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(subject: null, audience: SoulierAuthentication.TestingAudience));

        using var response = await client.GetAsync("/internal/identity/whoami", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string CreateToken(
        string? subject,
        string audience,
        DateTime? expiresUtc = null)
    {
        var claims = new List<Claim>
        {
            new("client_id", "codex-test"),
            new("preferred_username", "frank"),
            new("email", "frank-test@example.invalid")
        };

        if (!string.IsNullOrWhiteSpace(subject))
            claims.Add(new Claim("sub", subject));

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = SoulierAuthentication.TestingAuthority,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expiresUtc ?? DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256)
        });
    }

    private sealed class OidcFactory(HumanPrincipalStatus frankStatus = HumanPrincipalStatus.Active)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHumanPrincipalRegistry>();
                services.AddSingleton<IHumanPrincipalRegistry>(new TestHumanPrincipalRegistry(frankStatus));

                services.PostConfigure<JwtBearerOptions>(SoulierAuthentication.Scheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = SoulierAuthentication.TestingAuthority
                    };
                    configuration.SigningKeys.Add(SigningKey);
                    options.Configuration = configuration;
                    options.TokenValidationParameters.ValidIssuer = SoulierAuthentication.TestingAuthority;
                    options.TokenValidationParameters.ValidAudience = SoulierAuthentication.TestingAudience;
                    options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                });
            });
        }
    }

    private sealed class TestHumanPrincipalRegistry(HumanPrincipalStatus status) : IHumanPrincipalRegistry
    {
        private readonly HumanPrincipal _frank = new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "frank-test",
            "Frank Test",
            status,
            new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero));

        public Task<HumanPrincipal?> FindByOidcSubjectAsync(
            string oidcSubject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<HumanPrincipal?>(
                string.Equals(oidcSubject, _frank.OidcSubject, StringComparison.Ordinal)
                    ? _frank
                    : null);
        }
    }
}
