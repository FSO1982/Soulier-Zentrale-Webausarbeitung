namespace Soulier.Zentrale.Domain;

public enum ProviderApprovalStatus
{
    Draft,
    Approved,
    Paused,
    Revoked
}

public sealed record ProviderDefinition(
    string Key,
    ModelExecutionTarget Target,
    ProviderApprovalStatus Status,
    Guid? ApprovedByHumanPrincipalId,
    DateTimeOffset? ApprovedAtUtc);

public sealed record ModelRouteDefinition(
    string Key,
    string ProviderKey,
    string ModelAlias,
    bool IsActive);

public sealed record ProviderUseCaseGrant(
    string ProviderKey,
    string UseCaseKey);

public sealed record ModelRouteEvaluationRequest(
    AiPolicy AiPolicy,
    string UseCaseKey,
    ModelRouteDefinition Route,
    ProviderDefinition Provider,
    IReadOnlyCollection<ProviderUseCaseGrant> UseCaseGrants);

public static class ModelRoutePolicy
{
    public static ModelExecutionDecision Evaluate(ModelRouteEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Route);
        ArgumentNullException.ThrowIfNull(request.Provider);
        ArgumentNullException.ThrowIfNull(request.UseCaseGrants);

        if (!request.Route.IsActive)
            return ModelExecutionDecision.Deny("MODEL_ROUTE_DISABLED");

        if (!string.Equals(request.Route.ProviderKey, request.Provider.Key, StringComparison.Ordinal))
            return ModelExecutionDecision.Deny("MODEL_ROUTE_PROVIDER_MISMATCH");

        if (request.Provider.Status != ProviderApprovalStatus.Approved)
            return ModelExecutionDecision.Deny("PROVIDER_NOT_APPROVED");

        if (request.Provider.Target == ModelExecutionTarget.External)
        {
            if (request.Provider.ApprovedByHumanPrincipalId is null || request.Provider.ApprovedAtUtc is null)
                return ModelExecutionDecision.Deny("PROVIDER_APPROVAL_EVIDENCE_MISSING");

            if (string.IsNullOrWhiteSpace(request.UseCaseKey) ||
                !request.UseCaseGrants.Any(x =>
                    string.Equals(x.ProviderKey, request.Provider.Key, StringComparison.Ordinal) &&
                    string.Equals(x.UseCaseKey, request.UseCaseKey, StringComparison.Ordinal)))
                return ModelExecutionDecision.Deny("PROVIDER_USE_CASE_NOT_APPROVED");
        }

        return ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            request.AiPolicy,
            request.Provider.Target,
            request.Provider.Key,
            ProviderApproved: true));
    }
}
