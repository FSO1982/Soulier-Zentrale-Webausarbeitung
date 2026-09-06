using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Application;

public enum KnowledgeDependencyState
{
    Healthy,
    Stale,
    Degraded
}

public sealed record KnowledgeDependencyStatus(
    KnowledgeDependencyState State,
    DateTimeOffset CheckedAtUtc,
    string? Detail = null);

public interface IKnowledgeDependencyStatusProvider
{
    KnowledgeDependencyStatus GetStatus();
}

public static class KnowledgeDependencyPolicy
{
    public static AuthorizationResult Evaluate(KnowledgeDependencyStatus status) => status.State switch
    {
        KnowledgeDependencyState.Healthy => AuthorizationResult.Allow(),
        KnowledgeDependencyState.Stale => AuthorizationResult.Deny("RESOURCE_STALE"),
        KnowledgeDependencyState.Degraded => AuthorizationResult.Deny("DEPENDENCY_DEGRADED"),
        _ => AuthorizationResult.Deny("DEPENDENCY_DEGRADED")
    };
}

public sealed class AuditedKnowledgeDependencyGuard(
    IKnowledgeDependencyStatusProvider statusProvider,
    IAuditEventWriter auditWriter)
{
    public async Task<AuthorizationResult> EvaluateAsync(
        RequestContext requestContext,
        AuthorizationAuditContext auditContext,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(auditContext);

        var result = KnowledgeDependencyPolicy.Evaluate(statusProvider.GetStatus());
        if (result.Allowed)
            return result;

        var auditEvent = AuditEvent.Create(
            nowUtc,
            requestContext.CorrelationId,
            auditContext.HumanPrincipalId,
            requestContext.ClientId,
            auditContext.ServiceIdentityId,
            requestContext.CapabilityKey,
            auditContext.ResourceType,
            auditContext.ResourceId,
            auditContext.DocumentVersionId,
            auditContext.ContentHash,
            auditContext.PolicyVersion,
            auditContext.ApprovalId,
            "DENY",
            result.ReasonCode,
            auditContext.SourceAdapter,
            null);

        await auditWriter.WriteAsync(auditEvent, cancellationToken);
        return result;
    }
}
