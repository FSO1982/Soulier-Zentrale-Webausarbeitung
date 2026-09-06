using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Application;

public interface IAuditEventWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed record AuthorizationAuditContext(
    string CorrelationId,
    Guid? HumanPrincipalId,
    Guid? ServiceIdentityId,
    string? ResourceType,
    string? ResourceId,
    Guid? DocumentVersionId,
    string? ContentHash,
    string? PolicyVersion,
    Guid? ApprovalId,
    string? SourceAdapter);

public sealed class AuditedCapabilityAuthorizer(IAuditEventWriter auditWriter)
{
    public async Task<AuthorizationResult> AuthorizeAsync(
        AuthorizationRequest request,
        AuthorizationAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auditContext);

        var result = CapabilityAuthorizer.Authorize(request);
        var auditEvent = AuditEvent.Create(
            request.NowUtc,
            auditContext.CorrelationId,
            auditContext.HumanPrincipalId,
            request.Client.Id,
            auditContext.ServiceIdentityId,
            request.Capability.Key,
            auditContext.ResourceType,
            auditContext.ResourceId,
            auditContext.DocumentVersionId,
            auditContext.ContentHash,
            auditContext.PolicyVersion,
            auditContext.ApprovalId,
            result.Allowed ? "ALLOW" : "DENY",
            result.ReasonCode,
            auditContext.SourceAdapter,
            null);

        // Security-relevant authorization results are not returned until the audit event is durably accepted.
        // A writer failure therefore propagates instead of silently producing an unaudited ALLOW/DENY result.
        await auditWriter.WriteAsync(auditEvent, cancellationToken);
        return result;
    }
}
