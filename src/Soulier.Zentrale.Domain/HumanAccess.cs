namespace Soulier.Zentrale.Domain;

public enum HumanPrincipalStatus { Active, Disabled }
public enum HumanRoleAssignmentStatus { Active, Revoked }

public sealed record HumanPrincipal(
    Guid Id,
    string OidcSubject,
    string DisplayName,
    HumanPrincipalStatus Status,
    DateTimeOffset CreatedAtUtc)
{
    public static HumanPrincipal Create(
        Guid id,
        string oidcSubject,
        string displayName,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Human principal id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(oidcSubject)) throw new ArgumentException("OIDC subject is required.", nameof(oidcSubject));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

        return new HumanPrincipal(
            id,
            oidcSubject.Trim(),
            displayName.Trim(),
            HumanPrincipalStatus.Active,
            createdAtUtc);
    }

    public bool IsActive => Status == HumanPrincipalStatus.Active;
}

public sealed record RoleDefinition(
    Guid Id,
    string Key,
    string Name,
    bool IsActive);

public sealed record RoleCapability(
    Guid RoleId,
    string CapabilityKey);

public sealed record HumanRoleAssignment(
    Guid Id,
    Guid HumanPrincipalId,
    Guid RoleId,
    string ResourceScope,
    string Environment,
    HumanRoleAssignmentStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidUntilUtc)
{
    public bool IsActiveAt(DateTimeOffset nowUtc) =>
        Status == HumanRoleAssignmentStatus.Active &&
        ValidFromUtc <= nowUtc &&
        (ValidUntilUtc is null || ValidUntilUtc > nowUtc);
}

public sealed record HumanAuthorizationRequest(
    HumanPrincipal Principal,
    IReadOnlyCollection<RoleDefinition> Roles,
    IReadOnlyCollection<RoleCapability> RoleCapabilities,
    IReadOnlyCollection<HumanRoleAssignment> Assignments,
    string CapabilityKey,
    string RequestedScope,
    string Environment,
    DateTimeOffset NowUtc);

public static class HumanAccessAuthorizer
{
    public static AuthorizationResult Authorize(HumanAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Principal.IsActive)
            return AuthorizationResult.Deny("HUMAN_DISABLED");

        if (string.IsNullOrWhiteSpace(request.CapabilityKey))
            return AuthorizationResult.Deny("CAPABILITY_DENIED");

        if (string.IsNullOrWhiteSpace(request.RequestedScope))
            return AuthorizationResult.Deny("RESOURCE_SCOPE_DENIED");

        if (string.IsNullOrWhiteSpace(request.Environment))
            return AuthorizationResult.Deny("ENVIRONMENT_DENIED");

        var validAssignments = request.Assignments
            .Where(x =>
                x.HumanPrincipalId == request.Principal.Id &&
                x.IsActiveAt(request.NowUtc))
            .ToArray();

        if (validAssignments.Length == 0)
            return AuthorizationResult.Deny("ROLE_DENIED");

        var roleIdsForCapability = request.Roles
            .Where(role => role.IsActive)
            .Join(
                request.RoleCapabilities.Where(x =>
                    string.Equals(x.CapabilityKey, request.CapabilityKey, StringComparison.Ordinal)),
                role => role.Id,
                capability => capability.RoleId,
                (role, _) => role.Id)
            .ToHashSet();

        if (roleIdsForCapability.Count == 0)
            return AuthorizationResult.Deny("CAPABILITY_DENIED");

        var capabilityAssignments = validAssignments
            .Where(x => roleIdsForCapability.Contains(x.RoleId))
            .ToArray();

        if (capabilityAssignments.Length == 0)
            return AuthorizationResult.Deny("CAPABILITY_DENIED");

        if (!capabilityAssignments.Any(x =>
                string.Equals(x.Environment, request.Environment, StringComparison.Ordinal)))
            return AuthorizationResult.Deny("ENVIRONMENT_DENIED");

        return capabilityAssignments.Any(x =>
                string.Equals(x.Environment, request.Environment, StringComparison.Ordinal) &&
                string.Equals(x.ResourceScope, request.RequestedScope, StringComparison.Ordinal))
            ? AuthorizationResult.Allow()
            : AuthorizationResult.Deny("RESOURCE_SCOPE_DENIED");
    }
}
