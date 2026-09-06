using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class HumanAccessAuthorizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 1, 0, 0, TimeSpan.Zero);
    private static readonly Guid HumanId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Active_human_with_active_role_capability_scope_and_environment_is_allowed()
    {
        var result = HumanAccessAuthorizer.Authorize(CreateRequest());

        Assert.True(result.Allowed);
        Assert.Equal("ALLOW", result.ReasonCode);
    }

    [Fact]
    public void Disabled_human_is_denied()
    {
        var request = CreateRequest() with
        {
            Principal = CreatePrincipal(HumanPrincipalStatus.Disabled)
        };

        var result = HumanAccessAuthorizer.Authorize(request);

        Assert.False(result.Allowed);
        Assert.Equal("HUMAN_DISABLED", result.ReasonCode);
    }

    [Fact]
    public void Missing_capability_on_role_is_denied()
    {
        var request = CreateRequest() with
        {
            RoleCapabilities = [new RoleCapability(RoleId, "knowledge.search")]
        };

        var result = HumanAccessAuthorizer.Authorize(request);

        Assert.False(result.Allowed);
        Assert.Equal("CAPABILITY_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Foreign_scope_is_denied()
    {
        var request = CreateRequest() with { RequestedScope = "soulier:other" };

        var result = HumanAccessAuthorizer.Authorize(request);

        Assert.False(result.Allowed);
        Assert.Equal("RESOURCE_SCOPE_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Foreign_environment_is_denied()
    {
        var request = CreateRequest() with { Environment = "PROD" };

        var result = HumanAccessAuthorizer.Authorize(request);

        Assert.False(result.Allowed);
        Assert.Equal("ENVIRONMENT_DENIED", result.ReasonCode);
    }

    [Theory]
    [InlineData(HumanRoleAssignmentStatus.Revoked, -10, null)]
    [InlineData(HumanRoleAssignmentStatus.Active, 10, null)]
    [InlineData(HumanRoleAssignmentStatus.Active, -20, -10)]
    public void Revoked_future_or_expired_assignment_is_denied(
        HumanRoleAssignmentStatus status,
        int validFromMinutes,
        int? validUntilMinutes)
    {
        var assignment = new HumanRoleAssignment(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            HumanId,
            RoleId,
            "soulier:pilot",
            "TEST",
            status,
            Now.AddMinutes(validFromMinutes),
            validUntilMinutes is null ? null : Now.AddMinutes(validUntilMinutes.Value));

        var request = CreateRequest() with { Assignments = [assignment] };

        var result = HumanAccessAuthorizer.Authorize(request);

        Assert.False(result.Allowed);
        Assert.Equal("ROLE_DENIED", result.ReasonCode);
    }

    private static HumanAuthorizationRequest CreateRequest() => new(
        CreatePrincipal(HumanPrincipalStatus.Active),
        [new RoleDefinition(RoleId, "admin", "Administrator", true)],
        [new RoleCapability(RoleId, "knowledge.read")],
        [new HumanRoleAssignment(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            HumanId,
            RoleId,
            "soulier:pilot",
            "TEST",
            HumanRoleAssignmentStatus.Active,
            Now.AddHours(-1),
            null)],
        "knowledge.read",
        "soulier:pilot",
        "TEST",
        Now);

    private static HumanPrincipal CreatePrincipal(HumanPrincipalStatus status) => new(
        HumanId,
        "frank-test",
        "Frank Test",
        status,
        Now.AddDays(-1));
}
