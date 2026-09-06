namespace Soulier.Zentrale.Domain;

public enum ActionExecutionMode
{
    ReadOnly,
    PreapprovedBounded,
    ApprovalPerExecution,
    Forbidden
}

public enum PriorExecutionState
{
    None,
    InProgress,
    Succeeded,
    Failed
}

public enum ActionExecutionDisposition
{
    Execute,
    ReturnPriorResult,
    Deny
}

public sealed record ActionDefinition(
    string Key,
    ActionExecutionMode Mode,
    bool IsActive,
    string? ParameterPolicyVersion);

public sealed record ActionExecutionRecord(
    Guid Id,
    string ActionKey,
    string IdempotencyKey,
    Guid? ClientId,
    string ResourceScope,
    string CorrelationId,
    PriorExecutionState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? ResultReference);

public sealed record ActionExecutionRequest(
    ActionDefinition Action,
    bool CapabilityAllowed,
    bool ScopeAllowed,
    bool PolicyAllowed,
    bool ParametersWithinBounds,
    bool ApprovalSatisfied,
    string? IdempotencyKey,
    PriorExecutionState PriorExecutionState);

public sealed record ActionExecutionDecision(
    ActionExecutionDisposition Disposition,
    string ReasonCode)
{
    public static ActionExecutionDecision Execute() => new(ActionExecutionDisposition.Execute, "ALLOW_EXECUTE");
    public static ActionExecutionDecision ReturnPrior() => new(ActionExecutionDisposition.ReturnPriorResult, "IDEMPOTENT_REPLAY");
    public static ActionExecutionDecision Deny(string reasonCode) => new(ActionExecutionDisposition.Deny, reasonCode);
}

public static class AutomationControlPolicy
{
    public static ActionExecutionDecision Evaluate(ActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Action);

        if (!request.Action.IsActive)
            return ActionExecutionDecision.Deny("ACTION_DISABLED");

        if (request.Action.Mode == ActionExecutionMode.Forbidden)
            return ActionExecutionDecision.Deny("ACTION_FORBIDDEN");

        if (!request.CapabilityAllowed)
            return ActionExecutionDecision.Deny("CAPABILITY_DENIED");

        if (!request.ScopeAllowed)
            return ActionExecutionDecision.Deny("RESOURCE_SCOPE_DENIED");

        if (!request.PolicyAllowed)
            return ActionExecutionDecision.Deny("POLICY_DENIED");

        if (!request.ParametersWithinBounds)
            return ActionExecutionDecision.Deny("PARAMETER_BOUNDS_DENIED");

        if (request.Action.Mode == ActionExecutionMode.ApprovalPerExecution && !request.ApprovalSatisfied)
            return ActionExecutionDecision.Deny("APPROVAL_REQUIRED");

        if (request.Action.Mode == ActionExecutionMode.ReadOnly)
            return ActionExecutionDecision.Execute();

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            return ActionExecutionDecision.Deny("IDEMPOTENCY_KEY_REQUIRED");

        if (request.PriorExecutionState != PriorExecutionState.None)
            return ActionExecutionDecision.ReturnPrior();

        return ActionExecutionDecision.Execute();
    }
}
