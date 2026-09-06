using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EfServiceAccess(SoulierDbContext dbContext)
    : IServiceAccessReader, IServiceAccessAdministration
{
    public async Task<ServiceAccessSnapshot?> GetSnapshotAsync(
        Guid serviceIdentityId,
        string capabilityKey,
        int capabilityMajorVersion,
        CancellationToken cancellationToken = default)
    {
        if (serviceIdentityId == Guid.Empty || string.IsNullOrWhiteSpace(capabilityKey) || capabilityMajorVersion < 1)
            return null;

        var service = await dbContext.ServiceIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == serviceIdentityId, cancellationToken);
        if (service is null)
            return null;

        var capability = await dbContext.Capabilities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Key == capabilityKey && x.MajorVersion == capabilityMajorVersion,
                cancellationToken);
        if (capability is null)
            return null;

        var grants = await dbContext.ServiceGrants
            .AsNoTracking()
            .Where(x =>
                x.ServiceIdentityId == serviceIdentityId &&
                x.CapabilityKey == capabilityKey &&
                x.CapabilityMajorVersion == capabilityMajorVersion)
            .ToArrayAsync(cancellationToken);

        return new ServiceAccessSnapshot(service, capability, grants);
    }

    public async Task<ServiceIdentity> UpsertServiceIdentityAsync(
        ServiceIdentity serviceIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceIdentity);
        ValidateService(serviceIdentity);
        var normalized = serviceIdentity with
        {
            Name = serviceIdentity.Name.Trim(),
            Environment = serviceIdentity.Environment.Trim()
        };

        var exists = await dbContext.ServiceIdentities
            .AsNoTracking()
            .AnyAsync(x => x.Id == normalized.Id, cancellationToken);
        if (!exists)
        {
            dbContext.ServiceIdentities.Add(normalized);
            await dbContext.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        await dbContext.ServiceIdentities
            .Where(x => x.Id == normalized.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Name, normalized.Name)
                    .SetProperty(x => x.Environment, normalized.Environment)
                    .SetProperty(x => x.Status, normalized.Status),
                cancellationToken);
        return normalized;
    }

    public async Task<bool> SetServiceStatusAsync(
        Guid serviceIdentityId,
        ServiceIdentityStatus status,
        CancellationToken cancellationToken = default)
    {
        if (serviceIdentityId == Guid.Empty)
            throw new ArgumentException("Service identity id is required.", nameof(serviceIdentityId));

        return await dbContext.ServiceIdentities
            .Where(x => x.Id == serviceIdentityId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status), cancellationToken) == 1;
    }

    public async Task<ServiceGrant> UpsertServiceGrantAsync(
        ServiceGrant grant,
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

        var exists = await dbContext.ServiceGrants.AsNoTracking().AnyAsync(x =>
            x.ServiceIdentityId == normalized.ServiceIdentityId &&
            x.CapabilityKey == normalized.CapabilityKey &&
            x.CapabilityMajorVersion == normalized.CapabilityMajorVersion &&
            x.ResourceScope == normalized.ResourceScope &&
            x.Environment == normalized.Environment,
            cancellationToken);

        if (!exists)
        {
            dbContext.ServiceGrants.Add(normalized);
            await dbContext.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        await dbContext.ServiceGrants
            .Where(x =>
                x.ServiceIdentityId == normalized.ServiceIdentityId &&
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

    private static void ValidateService(ServiceIdentity serviceIdentity)
    {
        if (serviceIdentity.Id == Guid.Empty)
            throw new ArgumentException("Service identity id is required.", nameof(serviceIdentity));
        if (string.IsNullOrWhiteSpace(serviceIdentity.Name) || serviceIdentity.Name.Length > 200)
            throw new ArgumentException("Service name must contain 1 to 200 characters.", nameof(serviceIdentity));
        if (string.IsNullOrWhiteSpace(serviceIdentity.Environment) || serviceIdentity.Environment.Length > 32)
            throw new ArgumentException("Service environment must contain 1 to 32 characters.", nameof(serviceIdentity));
    }

    private static void ValidateGrant(ServiceGrant grant)
    {
        if (grant.ServiceIdentityId == Guid.Empty)
            throw new ArgumentException("Service identity id is required.", nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.CapabilityKey) || grant.CapabilityKey.Length > 200)
            throw new ArgumentException("Capability key must contain 1 to 200 characters.", nameof(grant));
        if (grant.CapabilityMajorVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.ResourceScope) || grant.ResourceScope.Length > 500)
            throw new ArgumentException("Resource scope must contain 1 to 500 characters.", nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.Environment) || grant.Environment.Length > 32)
            throw new ArgumentException("Environment must contain 1 to 32 characters.", nameof(grant));
        if (grant.ValidUntilUtc is not null && grant.ValidUntilUtc <= grant.ValidFromUtc)
            throw new ArgumentException("Grant validity end must be after validity start.", nameof(grant));
    }
}
