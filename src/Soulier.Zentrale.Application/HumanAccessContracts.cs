using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Application;

public interface IHumanPrincipalRegistry
{
    Task<HumanPrincipal?> FindByOidcSubjectAsync(
        string oidcSubject,
        CancellationToken cancellationToken = default);
}

public interface IHumanAccessReader
{
    Task<HumanAccessSnapshot?> GetAccessSnapshotAsync(
        Guid humanPrincipalId,
        CancellationToken cancellationToken = default);
}

public sealed record HumanAccessSnapshot(
    HumanPrincipal Principal,
    IReadOnlyCollection<RoleDefinition> Roles,
    IReadOnlyCollection<RoleCapability> RoleCapabilities,
    IReadOnlyCollection<HumanRoleAssignment> Assignments);
