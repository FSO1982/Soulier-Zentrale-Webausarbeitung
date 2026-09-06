namespace Soulier.Zentrale.Domain;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Revoked
}

public sealed record ExecutionApproval(
    Guid Id,
    string ActionKey,
    string IdempotencyKey,
    Guid HumanPrincipalId,
    ApprovalStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    DateTimeOffset? ValidUntilUtc);

public sealed record ApprovalEvaluationRequest(
    string ActionKey,
    string IdempotencyKey,
    DateTimeOffset NowUtc,
    ExecutionApproval? Approval);

public sealed record ApprovalDecision(bool Satisfied, string ReasonCode)
{
    public static ApprovalDecision Allow() => new(true, "APPROVAL_SATISFIED");
    public static ApprovalDecision Deny(string reasonCode) => new(false, reasonCode);
}

public static class ApprovalPolicy
{
    public static ApprovalDecision Evaluate(ApprovalEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Approval is null)
            return ApprovalDecision.Deny("APPROVAL_REQUIRED");

        var approval = request.Approval;
        if (!string.Equals(approval.ActionKey, request.ActionKey, StringComparison.Ordinal) ||
            !string.Equals(approval.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
            return ApprovalDecision.Deny("APPROVAL_TARGET_MISMATCH");

        if (approval.Status != ApprovalStatus.Approved)
            return ApprovalDecision.Deny("APPROVAL_NOT_ACTIVE");

        if (approval.HumanPrincipalId == Guid.Empty || approval.DecidedAtUtc is null)
            return ApprovalDecision.Deny("APPROVAL_EVIDENCE_MISSING");

        if (approval.ValidUntilUtc is not null && approval.ValidUntilUtc <= request.NowUtc)
            return ApprovalDecision.Deny("APPROVAL_EXPIRED");

        return ApprovalDecision.Allow();
    }
}
