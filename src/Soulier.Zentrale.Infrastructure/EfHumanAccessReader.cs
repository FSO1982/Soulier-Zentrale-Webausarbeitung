using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EfHumanAccessReader(SoulierDbContext dbContext)
    : IHumanPrincipalRegistry, IHumanAccessReader
{
    public Task<HumanPrincipal?> FindByOidcSubjectAsync(
        string oidcSubject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oidcSubject))
            return Task.FromResult<HumanPrincipal?>(null);

        return dbContext.HumanPrincipals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OidcSubject == oidcSubject,
                cancellationToken);
    }

    public async Task<HumanAccessSnapshot?> GetAccessSnapshotAsync(
        Guid humanPrincipalId,
        CancellationToken cancellationToken = default)
    {
        if (humanPrincipalId == Guid.Empty)
            return null;

        var principal = await dbContext.HumanPrincipals
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == humanPrincipalId, cancellationToken);

        if (principal is null)
            return null;

        var assignments = await dbContext.HumanRoleAssignments
            .AsNoTracking()
            .Where(x => x.HumanPrincipalId == humanPrincipalId)
            .ToArrayAsync(cancellationToken);

        var roleIds = assignments
            .Select(x => x.RoleId)
            .Distinct()
            .ToArray();

        var roles = roleIds.Length == 0
            ? []
            : await dbContext.Roles
                .AsNoTracking()
                .Where(x => roleIds.Contains(x.Id))
                .ToArrayAsync(cancellationToken);

        var roleCapabilities = roleIds.Length == 0
            ? []
            : await dbContext.RoleCapabilities
                .AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId))
                .ToArrayAsync(cancellationToken);

        return new HumanAccessSnapshot(principal, roles, roleCapabilities, assignments);
    }
}
