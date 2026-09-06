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
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        Assert.True(result.Allowed);
        Assert.Equal("ALLOW", result.ReasonCode);
    }

    [Fact]
    public void Revoked_client_is_denied_even_with_valid_grant()
    {
        var result = Authorize(ClientStatus.Revoked, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        AssertDenied(result, "CLIENT_REVOKED");
    }

    [Theory]
    [InlineData(ClientStatus.Draft)]
    [InlineData(ClientStatus.Paused)]
    public void Non_active_client_is_denied(ClientStatus status)
    {
        var result = Authorize(status, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "TEST")]);

        AssertDenied(result, "CLIENT_INACTIVE");
    }

    [Fact]
    public void Missing_grant_fails_closed()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow, []);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    [Fact]
    public void Revoked_grant_fails_closed()
    {
        var grant = Grant("knowledge.search", "soulier:test", "TEST") with { Status = GrantStatus.Revoked };
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow, [grant]);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    [Fact]
    public void Expired_grant_fails_closed()
    {
        var grant = Grant("knowledge.search", "soulier:test", "TEST") with { ValidUntilUtc = Now };
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow, [grant]);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    [Fact]
    public void Future_grant_fails_closed()
    {
        var grant = Grant("knowledge.search", "soulier:test", "TEST") with { ValidFromUtc = Now.AddSeconds(1) };
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow, [grant]);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    [Fact]
    public void Wrong_capability_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.read", "soulier:test", "TEST")]);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    [Fact]
    public void Foreign_scope_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:other", "TEST")]);
        AssertDenied(result, "RESOURCE_SCOPE_DENIED");
    }

    [Fact]
    public void Policy_deny_overrides_valid_grant()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Deny,
            [Grant("knowledge.search", "soulier:test", "TEST")]);
        AssertDenied(result, "POLICY_DENIED");
    }

    [Fact]
    public void Approval_required_is_not_silently_allowed()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.RequireApproval,
            [Grant("knowledge.search", "soulier:test", "TEST")]);
        AssertDenied(result, "APPROVAL_REQUIRED");
    }

    [Fact]
    public void Cross_environment_grant_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "PROD")]);
        AssertDenied(result, "ENVIRONMENT_DENIED");
    }

    [Fact]
    public void Disabled_capability_is_denied()
    {
        var result = Authorize(ClientStatus.Active, PolicyDecision.Allow,
            [Grant("knowledge.search", "soulier:test", "TEST")], capabilityActive: false);
        AssertDenied(result, "CAPABILITY_DENIED");
    }

    private static AuthorizationResult Authorize(
        ClientStatus status,
        PolicyDecision policy,
        IReadOnlyCollection<Grant> grants,
        string requestedScope = "soulier:test",
        bool capabilityActive = true)
    {
        return CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            new Client(ClientId, "codex-pilot", "TEST", status),
            new Capability("knowledge.search", 1, capabilityActive),
            grants,
            requestedScope,
            policy,
            Now));
    }

    private static Grant Grant(string capability, string scope, string environment) =>
        new(ClientId, capability, scope, environment, GrantStatus.Active, Now.AddMinutes(-1), Now.AddHours(1));

    private static void AssertDenied(AuthorizationResult result, string reasonCode)
    {
        Assert.False(result.Allowed);
        Assert.Equal(reasonCode, result.ReasonCode);
    }
}
