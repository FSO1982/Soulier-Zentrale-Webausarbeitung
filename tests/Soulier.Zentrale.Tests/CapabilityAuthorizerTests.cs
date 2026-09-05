using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class CapabilityAuthorizerTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Allows_only_matching_active_grant()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "TEST")],
            requestedScope: "soulier:test");

        Assert.True(result.Allowed);
        Assert.Equal("ALLOW", result.ReasonCode);
    }

    [Fact]
    public void Revoked_client_is_denied_even_with_valid_grant()
    {
        var result = Authorize(ClientStatus.Revoked, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        Assert.False(result.Allowed);
        Assert.Equal("CLIENT_REVOKED", result.ReasonCode);
    }

    [Fact]
    public void Missing_grant_fails_closed()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow, []);

        Assert.False(result.Allowed);
        Assert.Equal("RESOURCE_SCOPE_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Foreign_scope_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:other", "TEST")],
            requestedScope: "soulier:test");

        Assert.False(result.Allowed);
        Assert.Equal("RESOURCE_SCOPE_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Policy_deny_overrides_valid_grant()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Deny,
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        Assert.False(result.Allowed);
        Assert.Equal("POLICY_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Approval_required_is_not_silently_allowed()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.RequireApproval,
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        Assert.False(result.Allowed);
        Assert.Equal("APPROVAL_REQUIRED", result.ReasonCode);
    }

    [Fact]
    public void Cross_environment_grant_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "PROD")]);

        Assert.False(result.Allowed);
        Assert.Equal("RESOURCE_SCOPE_DENIED", result.ReasonCode);
    }

    private static AuthorizationResult Authorize(
        ClientStatus status,
        PolicyDecision policy,
        IReadOnlyCollection<Grant> grants,
        string requestedScope = "soulier:test")
    {
        return CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            new Client(ClientId, "codex-pilot", "TEST", status),
            new Capability("knowledge.search", 1, true),
            grants,
            requestedScope,
            policy,
            Now));
    }

    private static Grant Grant(string capability, string scope, string environment) =>
        new(ClientId, capability, scope, environment, GrantStatus.Active, Now.AddMinutes(-1), Now.AddHours(1));
}
