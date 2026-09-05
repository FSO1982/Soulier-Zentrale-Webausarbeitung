using Soulier.Zentrale.Domain;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

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

app.Run();

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
