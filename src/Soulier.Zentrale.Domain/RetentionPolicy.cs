namespace Soulier.Zentrale.Domain;

public enum RetentionDataCategory
{
    DocumentVersion,
    Approval,
    AuditEvent,
    TechnicalLog,
    AiContent,
    Backup
}

public sealed record RetentionRule(
    RetentionDataCategory Category,
    TimeSpan? RetainFor,
    bool DeletionEnabled,
    bool LegalHoldSupported);

public sealed record RetentionEvaluationRequest(
    RetentionDataCategory Category,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset NowUtc,
    bool LegalHoldActive,
    IReadOnlyCollection<RetentionRule> Rules);

public sealed record RetentionDecision(
    bool Delete,
    string ReasonCode,
    DateTimeOffset? EligibleAtUtc);

public static class RetentionPolicy
{
    public static RetentionDecision Evaluate(RetentionEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rules);

        var matchingRules = request.Rules
            .Where(x => x.Category == request.Category)
            .ToArray();

        if (matchingRules.Length != 1)
            return new RetentionDecision(false, "RETENTION_UNDEFINED", null);

        var rule = matchingRules[0];
        if (!rule.DeletionEnabled || rule.RetainFor is null)
            return new RetentionDecision(false, "RETENTION_DELETION_DISABLED", null);

        if (rule.RetainFor <= TimeSpan.Zero)
            return new RetentionDecision(false, "RETENTION_INVALID", null);

        if (request.LegalHoldActive)
            return new RetentionDecision(false, "LEGAL_HOLD", null);

        var eligibleAt = request.CreatedAtUtc.Add(rule.RetainFor.Value);
        return request.NowUtc >= eligibleAt
            ? new RetentionDecision(true, "RETENTION_EXPIRED", eligibleAt)
            : new RetentionDecision(false, "RETENTION_ACTIVE", eligibleAt);
    }
}
