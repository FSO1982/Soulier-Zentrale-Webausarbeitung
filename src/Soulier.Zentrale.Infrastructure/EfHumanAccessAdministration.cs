using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EfHumanAccessAdministration(SoulierDbContext dbContext)
    : IHumanAccessAdministration
{
    public async Task<HumanPrincipal> CreateHumanAsync(
        CreateHumanPrincipalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var principal = HumanPrincipal.Create(
            command.Id,
            command.OidcSubject,
            command.DisplayName,
            command.CreatedAtUtc);

        dbContext.HumanPrincipals.Add(principal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return principal;
    }

    public async Task<bool> SetHumanStatusAsync(
        Guid humanPrincipalId,
        HumanPrincipalStatus status,
        CancellationToken cancellationToken = default)
    {
        if (humanPrincipalId == Guid.Empty)
            throw new ArgumentException("Human principal id is required.", nameof(humanPrincipalId));

        var affected = await dbContext.HumanPrincipals
            .Where(x => x.Id == humanPrincipalId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, status),
                cancellationToken);

        return affected == 1;
    }

    public async Task<RoleDefinition> UpsertRoleAsync(
        UpsertRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Id == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Key) || command.Key.Length > 100)
            throw new ArgumentException("Role key must contain 1 to 100 characters.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 200)
            throw new ArgumentException("Role name must contain 1 to 200 characters.", nameof(command));

        var normalized = new RoleDefinition(
            command.Id,
            command.Key.Trim(),
            command.Name.Trim(),
            command.IsActive);

        var exists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.Id, cancellationToken);

        if (!exists)
        {
            dbContext.Roles.Add(normalized);
            await dbContext.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        await dbContext.Roles
            .Where(x => x.Id == command.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Key, normalized.Key)
                    .SetProperty(x => x.Name, normalized.Name)
                    .SetProperty(x => x.IsActive, normalized.IsActive),
                cancellationToken);

        return normalized;
    }

    public async Task ReplaceRoleCapabilitiesAsync(
        Guid roleId,
        IReadOnlyCollection<string> capabilityKeys,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(roleId));
        ArgumentNullException.ThrowIfNull(capabilityKeys);

        var normalized = capabilityKeys
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Any(x => x.Length > 200))
            throw new ArgumentException("Capability keys may contain at most 200 characters.", nameof(capabilityKeys));

        if (!await dbContext.Roles.AsNoTracking().AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new KeyNotFoundException("Role does not exist.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.RoleCapabilities
            .Where(x => x.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);

        dbContext.RoleCapabilities.AddRange(normalized.Select(x => new RoleCapability(roleId, x)));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HumanRoleAssignment> AssignRoleAsync(
        HumanRoleAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.Id == Guid.Empty)
            throw new ArgumentException("Assignment id is required.", nameof(assignment));
        if (assignment.HumanPrincipalId == Guid.Empty)
            throw new ArgumentException("Human principal id is required.", nameof(assignment));
        if (assignment.RoleId == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(assignment));
        if (string.IsNullOrWhiteSpace(assignment.ResourceScope) || assignment.ResourceScope.Length > 500)
            throw new ArgumentException("Resource scope must contain 1 to 500 characters.", nameof(assignment));
        if (string.IsNullOrWhiteSpace(assignment.Environment) || assignment.Environment.Length > 32)
            throw new ArgumentException("Environment must contain 1 to 32 characters.", nameof(assignment));
        if (assignment.ValidUntilUtc is not null && assignment.ValidUntilUtc <= assignment.ValidFromUtc)
            throw new ArgumentException("Assignment validity end must be after validity start.", nameof(assignment));

        dbContext.HumanRoleAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return assignment;
    }
}
