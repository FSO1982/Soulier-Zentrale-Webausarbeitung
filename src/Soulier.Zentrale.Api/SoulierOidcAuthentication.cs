using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Soulier.Zentrale.Api;

public sealed record SoulierOidcOptions(
    string Authority,
    string Audience,
    bool RequireHttpsMetadata = true);

public static class SoulierAuthentication
{
    public const string Scheme = "SoulierOidc";
    public const string HumanPolicy = "SoulierHuman";
    public const string TestingAuthority = "https://oidc.testing/application/o/soulier-zentrale/";
    public const string TestingAudience = "soulier-zentrale-test";

    public static SoulierOidcOptions? ResolveOptions(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
            return new SoulierOidcOptions(TestingAuthority, TestingAudience);

        if (!configuration.GetValue<bool>("Soulier:Identity:Oidc:Enabled"))
            return null;

        var authority = configuration["Soulier:Identity:Oidc:Authority"];
        var audience = configuration["Soulier:Identity:Oidc:Audience"];

        if (string.IsNullOrWhiteSpace(authority))
            throw new InvalidOperationException("OIDC is enabled but Soulier:Identity:Oidc:Authority is missing.");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("OIDC is enabled but Soulier:Identity:Oidc:Audience is missing.");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri))
            throw new InvalidOperationException("OIDC Authority must be an absolute URI.");
        if (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OIDC Authority must use HTTPS outside the isolated test environment.");

        return new SoulierOidcOptions(authorityUri.AbsoluteUri, audience.Trim());
    }

    public static IServiceCollection AddSoulierOidcAuthentication(
        this IServiceCollection services,
        SoulierOidcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = Scheme;
                authentication.DefaultChallengeScheme = Scheme;
            })
            .AddJwtBearer(Scheme, jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.MapInboundClaims = false;
                jwt.SaveToken = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub"
                };
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(HumanPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("sub");
            });

        return services;
    }
}

public sealed record OidcIdentityDescriptor(
    string Subject,
    string? Issuer,
    string? ClientId,
    string? PreferredUsername,
    string? Email)
{
    public static OidcIdentityDescriptor FromPrincipal(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("Authenticated OIDC principal is missing the sub claim.");

        return new OidcIdentityDescriptor(
            subject,
            principal.FindFirstValue("iss"),
            principal.FindFirstValue("client_id") ?? principal.FindFirstValue("azp"),
            principal.FindFirstValue("preferred_username"),
            principal.FindFirstValue("email"));
    }
}
