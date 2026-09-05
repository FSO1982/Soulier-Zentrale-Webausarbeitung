using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Soulier.Zentrale.Api;

namespace Soulier.Zentrale.Tests;

public sealed class OidcAuthenticationTests
{
    private static readonly RSA SigningRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey SigningKey = new(SigningRsa) { KeyId = "gate3-oidc-test-key" };

    [Fact]
    public async Task Valid_signed_token_returns_verified_identity()
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

    private sealed class OidcFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
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
}
