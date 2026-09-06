using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class ServiceAccessAdministrationTests
{
    [Fact]
    public async Task N8n_like_service_identity_uses_own_grant_and_revocation_is_immediate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var clientAccess = new EfClientAccess(db);
        var serviceAccess = new EfServiceAccess(db);
        var now = DateTimeOffset.UtcNow;
        var serviceId = Guid.Parse("27272727-2727-2727-2727-272727272727");

        await clientAccess.UpsertCapabilityAsync(
            new Capability("automation.execute.service-test", 1, true),
            cancellationToken);
        await serviceAccess.UpsertServiceIdentityAsync(
            new ServiceIdentity(serviceId, "n8n-service-test", "TEST", ServiceIdentityStatus.Active),
            cancellationToken);
        await serviceAccess.UpsertServiceGrantAsync(
            new ServiceGrant(
                serviceId,
                "automation.execute.service-test",
                "soulier:automation-test",
                "TEST",
                GrantStatus.Active,
                now.AddMinutes(-1),
                null,
                1),
            cancellationToken);

        var snapshot = await serviceAccess.GetSnapshotAsync(
            serviceId,
            "automation.execute.service-test",
            1,
            cancellationToken);
        Assert.NotNull(snapshot);

        var allowed = ServiceIdentityAuthorizer.Authorize(new ServiceAuthorizationRequest(
            snapshot.ServiceIdentity,
            snapshot.Capability,
            snapshot.Grants,
            "soulier:automation-test",
            PolicyDecision.Allow,
            now));
        Assert.True(allowed.Allowed);

        Assert.True(await serviceAccess.SetServiceStatusAsync(
            serviceId,
            ServiceIdentityStatus.Revoked,
            cancellationToken));

        var revoked = await serviceAccess.GetSnapshotAsync(
            serviceId,
            "automation.execute.service-test",
            1,
            cancellationToken);
        Assert.NotNull(revoked);

        var denied = ServiceIdentityAuthorizer.Authorize(new ServiceAuthorizationRequest(
            revoked.ServiceIdentity,
            revoked.Capability,
            revoked.Grants,
            "soulier:automation-test",
            PolicyDecision.Allow,
            now));
        Assert.False(denied.Allowed);
        Assert.Equal("SERVICE_REVOKED", denied.ReasonCode);
    }
}
