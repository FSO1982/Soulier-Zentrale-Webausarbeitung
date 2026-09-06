namespace Soulier.Zentrale.Domain;

public enum ServiceIdentityStatus { Draft, Active, Paused, Revoked }

public sealed record ServiceIdentity(
    Guid Id,
    string Name,
    string Environment,
    ServiceIdentityStatus Status);

public sealed record ServiceGrant(
    Guid ServiceIdentityId,
    string CapabilityKey,
    string ResourceScope,
    string Environment,
    GrantStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidUntilUtc,
    int CapabilityMajorVersion = 1);

public sealed record ServiceAuthorizationRequest(
    ServiceIdentity ServiceIdentity,
    Capability Capability,
    IReadOnlyCollection<ServiceGrant> Grants,
    string RequestedScope,
    PolicyDecision PolicyDecision,
    DateTimeOffset NowUtc);

public static class ServiceIdentityAuthorizer
{
    public static AuthorizationResult Authorize(ServiceAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ServiceIdentity.Status == ServiceIdentityStatus.Revoked)
            return AuthorizationResult.Deny("SERVICE_REVOKED");
        if (request.ServiceIdentity.Status != ServiceIdentityStatus.Active)
            return AuthorizationResult.Deny("SERVICE_INACTIVE");
        if (!request.Capability.IsActive || request.Capability.MajorVersion < 1)
            return AuthorizationResult.Deny("CAPABILITY_DENIED");
        if (request.PolicyDecision != PolicyDecision.Allow)
            return AuthorizationResult.Deny(request.PolicyDecision == PolicyDecision.RequireApproval
                ? "APPROVAL_REQUIRED"
                : "POLICY_DENIED");

        var applicable = request.Grants.Where(grant =>
            grant.ServiceIdentityId == request.ServiceIdentity.Id &&
            grant.Status == GrantStatus.Active &&
            string.Equals(grant.CapabilityKey, request.Capability.Key, StringComparison.Ordinal) &&
            grant.CapabilityMajorVersion == request.Capability.MajorVersion &&
            grant.ValidFromUtc <= request.NowUtc &&
            (grant.ValidUntilUtc is null || grant.ValidUntilUtc > request.NowUtc)).ToArray();

        if (applicable.Length == 0)
            return AuthorizationResult.Deny("CAPABILITY_DENIED");
        if (!applicable.Any(grant => string.Equals(grant.Environment, request.ServiceIdentity.Environment, StringComparison.Ordinal)))
            return AuthorizationResult.Deny("ENVIRONMENT_DENIED");

        return applicable.Any(grant =>
                string.Equals(grant.Environment, request.ServiceIdentity.Environment, StringComparison.Ordinal) &&
                string.Equals(grant.ResourceScope, request.RequestedScope, StringComparison.Ordinal))
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny("RESOURCE_SCOPE_DENIED");
    }
}
