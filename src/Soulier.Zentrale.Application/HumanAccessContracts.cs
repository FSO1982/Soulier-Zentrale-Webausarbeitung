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

public sealed record CreateHumanPrincipalCommand(
    Guid Id,
    string OidcSubject,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

public sealed record UpsertRoleCommand(
    Guid Id,
    string Key,
    string Name,
    bool IsActive);

public interface IHumanAccessAdministration
{
    Task<HumanPrincipal> CreateHumanAsync(
        CreateHumanPrincipalCommand command,
        CancellationToken cancellationToken = default);

    Task<bool> SetHumanStatusAsync(
        Guid humanPrincipalId,
        HumanPrincipalStatus status,
        CancellationToken cancellationToken = default);

    Task<RoleDefinition> UpsertRoleAsync(
        UpsertRoleCommand command,
        CancellationToken cancellationToken = default);

    Task ReplaceRoleCapabilitiesAsync(
        Guid roleId,
        IReadOnlyCollection<string> capabilityKeys,
        CancellationToken cancellationToken = default);

    Task<HumanRoleAssignment> AssignRoleAsync(
        HumanRoleAssignment assignment,
        CancellationToken cancellationToken = default);
}
