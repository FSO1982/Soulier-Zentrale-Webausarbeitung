using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using Soulier.Zentrale.Api;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var databaseConnectionString = SoulierRuntimeConfiguration.ResolveDatabaseConnectionString(
    builder.Configuration,
    builder.Environment);
var databaseConfigured = !string.IsNullOrWhiteSpace(databaseConnectionString);
if (databaseConfigured)
    builder.Services.AddSoulierPersistence(databaseConnectionString!);

var oidcOptions = SoulierAuthentication.ResolveOptions(builder.Configuration, builder.Environment);
if (oidcOptions is not null)
    builder.Services.AddSoulierOidcAuthentication(oidcOptions);

var isTesting = builder.Environment.IsEnvironment("Testing");
var developmentPilotEnabled =
    builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("Soulier:Mcp:PilotEnabled");
var mcpPilotEnabled = isTesting || developmentPilotEnabled;

string? mcpPilotToken = null;
if (mcpPilotEnabled)
{
    mcpPilotToken = isTesting
        ? "gate3-ci-test-token"
        : builder.Configuration["Soulier:Mcp:PilotToken"];

    if (string.IsNullOrWhiteSpace(mcpPilotToken))
        throw new InvalidOperationException("Soulier MCP pilot is enabled but no pilot bearer token is configured.");

    builder.Services.AddSingleton<PilotMcpProfile>();
    builder.Services.AddSingleton<IKnowledgeReader, PilotKnowledgeReader>();
    builder.Services.AddSingleton<IKnowledgeDependencyStatusProvider, HealthyPilotKnowledgeDependencyStatusProvider>();
    builder.Services.AddSingleton<InMemoryPilotAuditWriter>();
    builder.Services.AddSingleton<IAuditEventWriter>(sp => sp.GetRequiredService<InMemoryPilotAuditWriter>());
    builder.Services.AddSingleton<AuditedCapabilityAuthorizer>();
    builder.Services.AddSingleton<AuditedKnowledgeDependencyGuard>();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "soulier-zentrale-gate3-pilot",
                Version = "0.1.0"
            };
            options.ServerInstructions =
                "Read-only Soulier-Zentrale Gate-3 pilot. Use only the exposed knowledge tools. " +
                "Never infer broader access from a successful call; capability and resource scope are enforced server-side.";
        })
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<SoulierKnowledgeTools>();
}

var app = builder.Build();

if (oidcOptions is not null)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async Task<IResult> (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (!databaseConfigured)
        return Results.Json(
            new { status = "not_ready", reasonCode = "DATABASE_NOT_CONFIGURED" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SoulierDbContext>();

        if (!await db.Database.CanConnectAsync(cancellationToken))
            return Results.Json(
                new { status = "not_ready", reasonCode = "DATABASE_UNAVAILABLE" },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
            return Results.Json(
                new { status = "not_ready", reasonCode = "DATABASE_MIGRATIONS_PENDING" },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        return Results.Ok(new { status = "ready" });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch
    {
        return Results.Json(
            new { status = "not_ready", reasonCode = "DATABASE_CHECK_FAILED" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapPost("/internal/authorization/check", (AuthorizationProbe probe) =>
    {
        var client = new Client(probe.ClientId, "pilot-client", probe.Environment, probe.ClientStatus);
        var capability = new Capability(probe.CapabilityKey, 1, probe.CapabilityActive);
        var grants = probe.Grants.Select(g => new Grant(
            probe.ClientId,
            g.CapabilityKey,
            g.ResourceScope,
            g.Environment,
            g.Status,
            g.ValidFromUtc,
            g.ValidUntilUtc)).ToArray();

        var result = CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            client,
            capability,
            grants,
            probe.RequestedScope,
            probe.PolicyDecision,
            DateTimeOffset.UtcNow));

        return result.Allowed
            ? Results.Ok(new { allowed = true, reasonCode = result.ReasonCode })
            : Results.Json(new { allowed = false, reasonCode = result.ReasonCode }, statusCode: StatusCodes.Status403Forbidden);
    });

    if (oidcOptions is not null)
    {
        app.MapGet("/internal/identity/whoami", (System.Security.Claims.ClaimsPrincipal principal) =>
            Results.Ok(OidcIdentityDescriptor.FromPrincipal(principal)))
            .RequireAuthorization(SoulierAuthentication.HumanPolicy);
    }
}

if (mcpPilotEnabled)
{
    var expectedToken = mcpPilotToken!;

    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/mcp"),
        branch => branch.Use(async (context, next) =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!IsValidBearerToken(authorization, expectedToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        }));

    app.MapMcp("/mcp");
}

app.Run();

static bool IsValidBearerToken(string authorization, string expectedToken)
{
    const string prefix = "Bearer ";
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return false;

    var suppliedToken = authorization[prefix.Length..];
    var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
    var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
    return CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
}

public sealed record AuthorizationProbe(
    Guid ClientId,
    string Environment,
    ClientStatus ClientStatus,
    string CapabilityKey,
    bool CapabilityActive,
    string RequestedScope,
    PolicyDecision PolicyDecision,
    IReadOnlyList<GrantProbe> Grants);

public sealed record GrantProbe(
    string CapabilityKey,
    string ResourceScope,
    string Environment,
    GrantStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidUntilUtc);

public partial class Program;
