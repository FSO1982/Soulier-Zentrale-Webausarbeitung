using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class AutomationControlPolicyTests
{
    [Theory]
    [InlineData(ActionExecutionMode.ReadOnly)]
    [InlineData(ActionExecutionMode.PreapprovedBounded)]
    [InlineData(ActionExecutionMode.ApprovalPerExecution)]
    public void Allowed_action_modes_execute_when_all_required_guards_are_satisfied(ActionExecutionMode mode)
    {
        var decision = AutomationControlPolicy.Evaluate(CreateRequest(mode));

        Assert.Equal(ActionExecutionDisposition.Execute, decision.Disposition);
        Assert.Equal("ALLOW_EXECUTE", decision.ReasonCode);
    }

    [Fact]
    public void Forbidden_action_never_executes()
    {
        var decision = AutomationControlPolicy.Evaluate(CreateRequest(ActionExecutionMode.Forbidden));

        Assert.Equal(ActionExecutionDisposition.Deny, decision.Disposition);
        Assert.Equal("ACTION_FORBIDDEN", decision.ReasonCode);
    }

    [Theory]
    [InlineData(false, true, true, true, "CAPABILITY_DENIED")]
    [InlineData(true, false, true, true, "RESOURCE_SCOPE_DENIED")]
    [InlineData(true, true, false, true, "POLICY_DENIED")]
    [InlineData(true, true, true, false, "PARAMETER_BOUNDS_DENIED")]
    public void Guard_failure_denies_execution(
        bool capability,
        bool scope,
        bool policy,
        bool parameters,
        string expectedReason)
    {
        var request = CreateRequest(ActionExecutionMode.PreapprovedBounded) with
        {
            CapabilityAllowed = capability,
            ScopeAllowed = scope,
            PolicyAllowed = policy,
            ParametersWithinBounds = parameters
        };

        var decision = AutomationControlPolicy.Evaluate(request);

        Assert.Equal(ActionExecutionDisposition.Deny, decision.Disposition);
        Assert.Equal(expectedReason, decision.ReasonCode);
    }

    [Fact]
    public void Approval_per_execution_requires_concrete_approval()
    {
        var request = CreateRequest(ActionExecutionMode.ApprovalPerExecution) with
        {
            ApprovalSatisfied = false
        };

        var decision = AutomationControlPolicy.Evaluate(request);

        Assert.Equal(ActionExecutionDisposition.Deny, decision.Disposition);
        Assert.Equal("APPROVAL_REQUIRED", decision.ReasonCode);
    }

    [Theory]
    [InlineData(ActionExecutionMode.PreapprovedBounded)]
    [InlineData(ActionExecutionMode.ApprovalPerExecution)]
    public void Effectful_actions_require_idempotency_key(ActionExecutionMode mode)
    {
        var request = CreateRequest(mode) with { IdempotencyKey = null };

        var decision = AutomationControlPolicy.Evaluate(request);

        Assert.Equal(ActionExecutionDisposition.Deny, decision.Disposition);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", decision.ReasonCode);
    }

    [Theory]
    [InlineData(PriorExecutionState.InProgress)]
    [InlineData(PriorExecutionState.Succeeded)]
    [InlineData(PriorExecutionState.Failed)]
    public void Existing_effectful_execution_is_not_executed_again(PriorExecutionState priorState)
    {
        var request = CreateRequest(ActionExecutionMode.PreapprovedBounded) with
        {
            PriorExecutionState = priorState
        };

        var decision = AutomationControlPolicy.Evaluate(request);

        Assert.Equal(ActionExecutionDisposition.ReturnPriorResult, decision.Disposition);
        Assert.Equal("IDEMPOTENT_REPLAY", decision.ReasonCode);
    }

    [Fact]
    public void Read_only_action_does_not_require_idempotency_key()
    {
        var request = CreateRequest(ActionExecutionMode.ReadOnly) with { IdempotencyKey = null };

        var decision = AutomationControlPolicy.Evaluate(request);

        Assert.Equal(ActionExecutionDisposition.Execute, decision.Disposition);
    }

    private static ActionExecutionRequest CreateRequest(ActionExecutionMode mode) => new(
        new ActionDefinition("knowledge.refresh", mode, true, "policy-v1"),
        CapabilityAllowed: true,
        ScopeAllowed: true,
        PolicyAllowed: true,
        ParametersWithinBounds: true,
        ApprovalSatisfied: true,
        IdempotencyKey: "idem-test-001",
        PriorExecutionState.None);
}
