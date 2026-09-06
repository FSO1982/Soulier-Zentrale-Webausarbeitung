using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class ModelExecutionPolicyTests
{
    [Theory]
    [InlineData(ModelExecutionTarget.Local)]
    [InlineData(ModelExecutionTarget.External)]
    public void Ai_forbidden_is_denied_for_every_target(ModelExecutionTarget target)
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.AiForbidden,
            target,
            target == ModelExecutionTarget.External ? "external:test" : "local:test",
            ProviderApproved: true));

        Assert.False(result.Allowed);
        Assert.Equal("AI_FORBIDDEN", result.ReasonCode);
    }

    [Fact]
    public void Local_only_is_allowed_on_local_target()
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.LocalOnly,
            ModelExecutionTarget.Local,
            "local:test",
            ProviderApproved: false));

        Assert.True(result.Allowed);
        Assert.Equal("ALLOW", result.ReasonCode);
    }

    [Fact]
    public void Local_only_cannot_fall_back_to_external_provider_even_when_provider_is_approved()
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.LocalOnly,
            ModelExecutionTarget.External,
            "external:approved",
            ProviderApproved: true));

        Assert.False(result.Allowed);
        Assert.Equal("LOCAL_ONLY_EXTERNAL_ROUTE_DENIED", result.ReasonCode);
    }

    [Fact]
    public void External_target_requires_named_provider()
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            ProviderKey: null,
            ProviderApproved: true));

        Assert.False(result.Allowed);
        Assert.Equal("PROVIDER_REQUIRED", result.ReasonCode);
    }

    [Fact]
    public void Unapproved_external_provider_is_denied()
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            "external:not-approved",
            ProviderApproved: false));

        Assert.False(result.Allowed);
        Assert.Equal("PROVIDER_NOT_APPROVED", result.ReasonCode);
    }

    [Fact]
    public void Approved_external_provider_is_allowed_only_for_external_allowed_data()
    {
        var result = ModelExecutionPolicy.Evaluate(new ModelExecutionRequest(
            AiPolicy.ExternalAllowed,
            ModelExecutionTarget.External,
            "external:approved",
            ProviderApproved: true));

        Assert.True(result.Allowed);
        Assert.Equal("ALLOW", result.ReasonCode);
    }
}
