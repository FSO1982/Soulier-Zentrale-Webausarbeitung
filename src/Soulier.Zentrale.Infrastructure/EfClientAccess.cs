using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EfClientAccess(SoulierDbContext dbContext)
    : IClientAccessReader, IClientAccessAdministration
{
    public async Task<ClientAccessSnapshot?> GetSnapshotAsync(
        Guid clientId,
        string capabilityKey,
        int capabilityMajorVersion,
        CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty || string.IsNullOrWhiteSpace(capabilityKey) || capabilityMajorVersion < 1)
            return null;

        var client = await dbContext.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        if (client is null)
            return null;

        var capability = await dbContext.Capabilities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Key == capabilityKey && x.MajorVersion == capabilityMajorVersion,
                cancellationToken);
        if (capability is null)
            return null;

        var grants = await dbContext.Grants
            .AsNoTracking()
            .Where(x =>
                x.ClientId == clientId &&
                x.CapabilityKey == capabilityKey &&
                x.CapabilityMajorVersion == capabilityMajorVersion)
            .ToArrayAsync(cancellationToken);

        return new ClientAccessSnapshot(client, capability, grants);
    }

    public async Task<Client> UpsertClientAsync(
        Client client,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ValidateClient(client);

        var exists = await dbContext.Clients
            .AsNoTracking()
            .AnyAsync(x => x.Id == client.Id, cancellationToken);

        if (!exists)
        {
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync(cancellationToken);
            return client;
        }

        await dbContext.Clients
            .Where(x => x.Id == client.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Name, client.Name.Trim())
                    .SetProperty(x => x.Environment, client.Environment.Trim())
                    .SetProperty(x => x.Status, client.Status),
                cancellationToken);
        return client with { Name = client.Name.Trim(), Environment = client.Environment.Trim() };
    }

    public async Task<bool> SetClientStatusAsync(
        Guid clientId,
        ClientStatus status,
        CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("Client id is required.", nameof(clientId));

        return await dbContext.Clients
            .Where(x => x.Id == clientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status), cancellationToken) == 1;
    }

    public async Task<Capability> UpsertCapabilityAsync(
        Capability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ValidateCapability(capability);

        var exists = await dbContext.Capabilities
            .AsNoTracking()
            .AnyAsync(
                x => x.Key == capability.Key && x.MajorVersion == capability.MajorVersion,
                cancellationToken);

        var normalized = capability with { Key = capability.Key.Trim() };
        if (!exists)
        {
            dbContext.Capabilities.Add(normalized);
            await dbContext.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        await dbContext.Capabilities
            .Where(x => x.Key == normalized.Key && x.MajorVersion == normalized.MajorVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, normalized.IsActive), cancellationToken);
        return normalized;
    }

    public async Task<Grant> UpsertGrantAsync(
        Grant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateGrant(grant);

        var normalized = grant with
        {
            CapabilityKey = grant.CapabilityKey.Trim(),
            ResourceScope = grant.ResourceScope.Trim(),
            Environment = grant.Environment.Trim()
        };

        var exists = await dbContext.Grants.AsNoTracking().AnyAsync(x =>
            x.ClientId == normalized.ClientId &&
            x.CapabilityKey == normalized.CapabilityKey &&
            x.CapabilityMajorVersion == normalized.CapabilityMajorVersion &&
            x.ResourceScope == normalized.ResourceScope &&
            x.Environment == normalized.Environment,
            cancellationToken);

        if (!exists)
        {
            dbContext.Grants.Add(normalized);
            await dbContext.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        await dbContext.Grants
            .Where(x =>
                x.ClientId == normalized.ClientId &&
                x.CapabilityKey == normalized.CapabilityKey &&
                x.CapabilityMajorVersion == normalized.CapabilityMajorVersion &&
                x.ResourceScope == normalized.ResourceScope &&
                x.Environment == normalized.Environment)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, normalized.Status)
                    .SetProperty(x => x.ValidFromUtc, normalized.ValidFromUtc)
                    .SetProperty(x => x.ValidUntilUtc, normalized.ValidUntilUtc),
                cancellationToken);
        return normalized;
    }

    private static void ValidateClient(Client client)
    {
        if (client.Id == Guid.Empty) throw new ArgumentException("Client id is required.", nameof(client));
        if (string.IsNullOrWhiteSpace(client.Name) || client.Name.Length > 200)
            throw new ArgumentException("Client name must contain 1 to 200 characters.", nameof(client));
        if (string.IsNullOrWhiteSpace(client.Environment) || client.Environment.Length > 32)
            throw new ArgumentException("Client environment must contain 1 to 32 characters.", nameof(client));
    }

    private static void ValidateCapability(Capability capability)
    {
        if (string.IsNullOrWhiteSpace(capability.Key) || capability.Key.Length > 200)
            throw new ArgumentException("Capability key must contain 1 to 200 characters.", nameof(capability));
        if (capability.MajorVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(capability));
    }

    private static void ValidateGrant(Grant grant)
    {
        if (grant.ClientId == Guid.Empty) throw new ArgumentException("Grant client id is required.", nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.CapabilityKey) || grant.CapabilityKey.Length > 200)
            throw new ArgumentException("Grant capability key must contain 1 to 200 characters.", nameof(grant));
        if (grant.CapabilityMajorVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.ResourceScope) || grant.ResourceScope.Length > 500)
            throw new ArgumentException("Grant resource scope must contain 1 to 500 characters.", nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.Environment) || grant.Environment.Length > 32)
            throw new ArgumentException("Grant environment must contain 1 to 32 characters.", nameof(grant));
        if (grant.ValidUntilUtc is not null && grant.ValidUntilUtc <= grant.ValidFromUtc)
            throw new ArgumentException("Grant validity end must be after validity start.", nameof(grant));
    }
}
