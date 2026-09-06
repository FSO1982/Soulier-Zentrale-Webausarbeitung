using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class ModelRoutePolicyTests
{
    private static readonly Guid FrankId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 2, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Approved_local_route_allows_local_only_policy()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.LocalOnly,
            ModelExecutionTarget.Local,
            ProviderApprovalStatus.Approved,
            approvedBy: null,
            approvedAt: null,
            grantUseCase: false));

        Assert.True(decision.Allowed);
        Assert.Equal("ALLOW", decision.ReasonCode);
    }

    [Fact]
    public void External_provider_must_be_approved()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            ProviderApprovalStatus.Draft,
            approvedBy: null,
            approvedAt: null,
            grantUseCase: true));

        Assert.False(decision.Allowed);
        Assert.Equal("PROVIDER_NOT_APPROVED", decision.ReasonCode);
    }

    [Fact]
    public void Approved_external_provider_requires_approval_evidence()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            ProviderApprovalStatus.Approved,
            approvedBy: null,
            approvedAt: null,
            grantUseCase: true));

        Assert.False(decision.Allowed);
        Assert.Equal("PROVIDER_APPROVAL_EVIDENCE_MISSING", decision.ReasonCode);
    }

    [Fact]
    public void Approved_external_provider_requires_explicit_use_case_grant()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            ProviderApprovalStatus.Approved,
            FrankId,
            Now,
            grantUseCase: false));

        Assert.False(decision.Allowed);
        Assert.Equal("PROVIDER_USE_CASE_NOT_APPROVED", decision.ReasonCode);
    }

    [Fact]
    public void Local_only_policy_blocks_approved_external_route()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.LocalOnly,
            ModelExecutionTarget.External,
            ProviderApprovalStatus.Approved,
            FrankId,
            Now,
            grantUseCase: true));

        Assert.False(decision.Allowed);
        Assert.Equal("LOCAL_ONLY_EXTERNAL_ROUTE_DENIED", decision.ReasonCode);
    }

    [Fact]
    public void Ai_forbidden_blocks_even_approved_local_route()
    {
        var decision = ModelRoutePolicy.Evaluate(CreateRequest(
            AiPolicy.AiForbidden,
            ModelExecutionTarget.Local,
            ProviderApprovalStatus.Approved,
            null,
            null,
            grantUseCase: false));

        Assert.False(decision.Allowed);
        Assert.Equal("AI_FORBIDDEN", decision.ReasonCode);
    }

    [Fact]
    public void Disabled_route_is_denied()
    {
        var request = CreateRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.Local,
            ProviderApprovalStatus.Approved,
            null,
            null,
            grantUseCase: false) with
        {
            Route = new ModelRouteDefinition("route-local", "provider-local", "model-local", false)
        };

        var decision = ModelRoutePolicy.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Equal("MODEL_ROUTE_DISABLED", decision.ReasonCode);
    }

    [Fact]
    public void Route_provider_mismatch_is_denied()
    {
        var request = CreateRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.Local,
            ProviderApprovalStatus.Approved,
            null,
            null,
            grantUseCase: false) with
        {
            Route = new ModelRouteDefinition("route-local", "different-provider", "model-local", true)
        };

        var decision = ModelRoutePolicy.Evaluate(request);

        Assert.False(decision.Allowed);
        Assert.Equal("MODEL_ROUTE_PROVIDER_MISMATCH", decision.ReasonCode);
    }

    private static ModelRouteEvaluationRequest CreateRequest(
        AiPolicy aiPolicy,
        ModelExecutionTarget target,
        ProviderApprovalStatus providerStatus,
        Guid? approvedBy,
        DateTimeOffset? approvedAt,
        bool grantUseCase)
    {
        var providerKey = target == ModelExecutionTarget.Local ? "provider-local" : "provider-external";
        const string useCaseKey = "knowledge-assistant";
        return new ModelRouteEvaluationRequest(
            aiPolicy,
            useCaseKey,
            new ModelRouteDefinition("route-1", providerKey, "model-alias", true),
            new ProviderDefinition(providerKey, target, providerStatus, approvedBy, approvedAt),
            grantUseCase ? [new ProviderUseCaseGrant(providerKey, useCaseKey)] : []);
    }
}
