using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EfAuditEventWriter(SoulierDbContext dbContext) : IAuditEventWriter
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        dbContext.AuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
