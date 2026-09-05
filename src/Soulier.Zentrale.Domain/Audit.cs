namespace Soulier.Zentrale.Domain;

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid? HumanPrincipalId,
    Guid? ClientId,
    Guid? ServiceIdentityId,
    string? CapabilityKey,
    string? ResourceType,
    string? ResourceId,
    Guid? DocumentVersionId,
    string? ContentHash,
    string? PolicyVersion,
    Guid? ApprovalId,
    string Result,
    string ReasonCode,
    string? SourceAdapter,
    long? DurationMs)
{
    public static AuditEvent Create(
        DateTimeOffset occurredAtUtc,
        string correlationId,
        Guid? humanPrincipalId,
        Guid? clientId,
        Guid? serviceIdentityId,
        string? capabilityKey,
        string? resourceType,
        string? resourceId,
        Guid? documentVersionId,
        string? contentHash,
        string? policyVersion,
        Guid? approvalId,
        string result,
        string reasonCode,
        string? sourceAdapter,
        long? durationMs)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Result is required.", nameof(result));
        if (string.IsNullOrWhiteSpace(reasonCode)) throw new ArgumentException("Reason code is required.", nameof(reasonCode));
        if (durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs));

        return new AuditEvent(
            Guid.NewGuid(), occurredAtUtc, correlationId, humanPrincipalId, clientId, serviceIdentityId,
            capabilityKey, resourceType, resourceId, documentVersionId, contentHash, policyVersion,
            approvalId, result, reasonCode, sourceAdapter, durationMs);
    }
}
