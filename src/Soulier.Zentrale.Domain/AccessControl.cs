namespace Soulier.Zentrale.Domain;

public enum ClientStatus { Draft, Active, Paused, Revoked }
public enum GrantStatus { Active, Revoked }
public enum PolicyDecision { Allow, Deny, RequireApproval }

public sealed record Client(
    Guid Id,
    string Name,
    string Environment,
    ClientStatus Status);

public sealed record Capability(
    string Key,
    int MajorVersion,
    bool IsActive);

public sealed record Grant(
    Guid ClientId,
    string CapabilityKey,
    string ResourceScope,
    string Environment,
    GrantStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidUntilUtc);

public sealed record AuthorizationRequest(
    Client Client,
    Capability Capability,
    IReadOnlyCollection<Grant> Grants,
    string RequestedScope,
    PolicyDecision PolicyDecision,
    DateTimeOffset NowUtc);

public sealed record AuthorizationResult(bool Allowed, string ReasonCode)
{
    public static AuthorizationResult Allow() => new(true, "ALLOW");
    public static AuthorizationResult Deny(string reason) => new(false, reason);
}

/// <summary>
/// Pure domain authorization. Missing or ambiguous security information always fails closed.
/// </summary>
public static class CapabilityAuthorizer
{
    public static AuthorizationResult Authorize(AuthorizationRequest request)
    {
        if (request.Client.Status is ClientStatus.Revoked)
            return AuthorizationResult.Deny("CLIENT_REVOKED");

        if (request.Client.Status is not ClientStatus.Active)
            return AuthorizationResult.Deny("CLIENT_INACTIVE");

        if (!request.Capability.IsActive)
            return AuthorizationResult.Deny("CAPABILITY_DENIED");

        if (request.PolicyDecision is not PolicyDecision.Allow)
            return AuthorizationResult.Deny(request.PolicyDecision is PolicyDecision.RequireApproval
                ? "APPROVAL_REQUIRED"
                : "POLICY_DENIED");

        var matchingGrant = request.Grants.Any(g =>
            g.ClientId == request.Client.Id &&
            g.Status == GrantStatus.Active &&
            string.Equals(g.CapabilityKey, request.Capability.Key, StringComparison.Ordinal) &&
            string.Equals(g.Environment, request.Client.Environment, StringComparison.Ordinal) &&
            string.Equals(g.ResourceScope, request.RequestedScope, StringComparison.Ordinal) &&
            g.ValidFromUtc <= request.NowUtc &&
            (g.ValidUntilUtc is null || g.ValidUntilUtc > request.NowUtc));

        return matchingGrant
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny("RESOURCE_SCOPE_DENIED");
    }
}
