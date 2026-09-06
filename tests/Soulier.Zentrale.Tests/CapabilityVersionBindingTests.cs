using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class CapabilityVersionBindingTests
{
    [Fact]
    public void Grant_for_major_version_one_does_not_authorize_major_version_two()
    {
        var clientId = Guid.Parse("24242424-2424-2424-2424-242424242424");
        var now = new DateTimeOffset(2026, 9, 6, 4, 30, 0, TimeSpan.Zero);
        var client = new Client(clientId, "version-test-client", "TEST", ClientStatus.Active);
        var requestedCapability = new Capability("knowledge.read", 2, true);
        var grants = new[]
        {
            new Grant(
                clientId,
                "knowledge.read",
                "soulier:pilot",
                "TEST",
                GrantStatus.Active,
                now.AddMinutes(-5),
                null,
                CapabilityMajorVersion: 1)
        };

        var result = CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            client,
            requestedCapability,
            grants,
            "soulier:pilot",
            PolicyDecision.Allow,
            now));

        Assert.False(result.Allowed);
        Assert.Equal("CAPABILITY_DENIED", result.ReasonCode);
    }
}
