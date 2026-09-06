using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Application;

public sealed record ClientAccessSnapshot(
    Client Client,
    Capability Capability,
    IReadOnlyCollection<Grant> Grants);

public interface IClientAccessReader
{
    Task<ClientAccessSnapshot?> GetSnapshotAsync(
        Guid clientId,
        string capabilityKey,
        int capabilityMajorVersion,
        CancellationToken cancellationToken = default);
}

public interface IClientAccessAdministration
{
    Task<Client> UpsertClientAsync(
        Client client,
        CancellationToken cancellationToken = default);

    Task<bool> SetClientStatusAsync(
        Guid clientId,
        ClientStatus status,
        CancellationToken cancellationToken = default);

    Task<Capability> UpsertCapabilityAsync(
        Capability capability,
        CancellationToken cancellationToken = default);

    Task<Grant> UpsertGrantAsync(
        Grant grant,
        CancellationToken cancellationToken = default);
}
