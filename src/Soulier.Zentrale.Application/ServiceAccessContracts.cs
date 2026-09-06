using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Application;

public sealed record ServiceAccessSnapshot(
    ServiceIdentity ServiceIdentity,
    Capability Capability,
    IReadOnlyCollection<ServiceGrant> Grants);

public interface IServiceAccessReader
{
    Task<ServiceAccessSnapshot?> GetSnapshotAsync(
        Guid serviceIdentityId,
        string capabilityKey,
        int capabilityMajorVersion,
        CancellationToken cancellationToken = default);
}

public interface IServiceAccessAdministration
{
    Task<ServiceIdentity> UpsertServiceIdentityAsync(
        ServiceIdentity serviceIdentity,
        CancellationToken cancellationToken = default);

    Task<bool> SetServiceStatusAsync(
        Guid serviceIdentityId,
        ServiceIdentityStatus status,
        CancellationToken cancellationToken = default);

    Task<ServiceGrant> UpsertServiceGrantAsync(
        ServiceGrant grant,
        CancellationToken cancellationToken = default);
}
