namespace Soulier.Zentrale.Domain;

public enum ModelExecutionTarget
{
    Local,
    External
}

public sealed record ModelExecutionRequest(
    AiPolicy AiPolicy,
    ModelExecutionTarget Target,
    string? ProviderKey,
    bool ProviderApproved);

public sealed record ModelExecutionDecision(bool Allowed, string ReasonCode)
{
    public static ModelExecutionDecision Allow() => new(true, "ALLOW");
    public static ModelExecutionDecision Deny(string reasonCode) => new(false, reasonCode);
}

public static class ModelExecutionPolicy
{
    public static ModelExecutionDecision Evaluate(ModelExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AiPolicy == AiPolicy.AiForbidden)
            return ModelExecutionDecision.Deny("AI_FORBIDDEN");

        if (request.AiPolicy == AiPolicy.LocalOnly && request.Target == ModelExecutionTarget.External)
            return ModelExecutionDecision.Deny("LOCAL_ONLY_EXTERNAL_ROUTE_DENIED");

        if (request.Target == ModelExecutionTarget.External)
        {
            if (string.IsNullOrWhiteSpace(request.ProviderKey))
                return ModelExecutionDecision.Deny("PROVIDER_REQUIRED");

            if (!request.ProviderApproved)
                return ModelExecutionDecision.Deny("PROVIDER_NOT_APPROVED");
        }

        return ModelExecutionDecision.Allow();
    }
}
