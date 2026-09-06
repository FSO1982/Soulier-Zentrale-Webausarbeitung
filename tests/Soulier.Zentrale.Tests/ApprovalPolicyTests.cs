using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class ApprovalPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 2, 45, 0, TimeSpan.Zero);
    private static readonly Guid FrankId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Missing_approval_is_denied()
    {
        var decision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            "customer.email.send",
            "idem-001",
            Now,
            null));

        Assert.False(decision.Satisfied);
        Assert.Equal("APPROVAL_REQUIRED", decision.ReasonCode);
    }

    [Fact]
    public void Approval_is_bound_to_exact_action_and_idempotency_key()
    {
        var approval = Approved("customer.email.send", "idem-001");

        var wrongAction = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            "customer.email.delete",
            "idem-001",
            Now,
            approval));
        var wrongExecution = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            "customer.email.send",
            "idem-002",
            Now,
            approval));

        Assert.Equal("APPROVAL_TARGET_MISMATCH", wrongAction.ReasonCode);
        Assert.Equal("APPROVAL_TARGET_MISMATCH", wrongExecution.ReasonCode);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending)]
    [InlineData(ApprovalStatus.Rejected)]
    [InlineData(ApprovalStatus.Revoked)]
    public void Non_approved_status_is_denied(ApprovalStatus status)
    {
        var approval = Approved("customer.email.send", "idem-001") with { Status = status };

        var decision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            approval.ActionKey,
            approval.IdempotencyKey,
            Now,
            approval));

        Assert.False(decision.Satisfied);
        Assert.Equal("APPROVAL_NOT_ACTIVE", decision.ReasonCode);
    }

    [Fact]
    public void Approved_record_requires_decision_evidence()
    {
        var approval = Approved("customer.email.send", "idem-001") with { DecidedAtUtc = null };

        var decision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            approval.ActionKey,
            approval.IdempotencyKey,
            Now,
            approval));

        Assert.False(decision.Satisfied);
        Assert.Equal("APPROVAL_EVIDENCE_MISSING", decision.ReasonCode);
    }

    [Fact]
    public void Expired_approval_is_denied()
    {
        var approval = Approved("customer.email.send", "idem-001") with { ValidUntilUtc = Now.AddMinutes(-1) };

        var decision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            approval.ActionKey,
            approval.IdempotencyKey,
            Now,
            approval));

        Assert.False(decision.Satisfied);
        Assert.Equal("APPROVAL_EXPIRED", decision.ReasonCode);
    }

    [Fact]
    public void Exact_current_approved_execution_is_satisfied()
    {
        var approval = Approved("customer.email.send", "idem-001");

        var decision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            approval.ActionKey,
            approval.IdempotencyKey,
            Now,
            approval));

        Assert.True(decision.Satisfied);
        Assert.Equal("APPROVAL_SATISFIED", decision.ReasonCode);
    }

    private static ExecutionApproval Approved(string actionKey, string idempotencyKey) => new(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        actionKey,
        idempotencyKey,
        FrankId,
        ApprovalStatus.Approved,
        Now.AddMinutes(-5),
        Now.AddMinutes(-4),
        Now.AddHours(1));
}
