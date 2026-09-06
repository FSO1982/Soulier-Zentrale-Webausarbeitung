using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class ClientAccessAdministrationTests
{
    [Fact]
    public async Task Persistent_client_grant_can_be_authorized_then_revoked_without_code_change()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var access = new EfClientAccess(db);
        var now = DateTimeOffset.UtcNow;
        var clientId = Guid.Parse("25252525-2525-2525-2525-252525252525");

        await access.UpsertClientAsync(
            new Client(clientId, "persistent-client-test", "TEST", ClientStatus.Active),
            cancellationToken);
        await access.UpsertCapabilityAsync(
            new Capability("knowledge.read.persistent-test", 1, true),
            cancellationToken);
        await access.UpsertGrantAsync(
            new Grant(
                clientId,
                "knowledge.read.persistent-test",
                "soulier:persistent-client-test",
                "TEST",
                GrantStatus.Active,
                now.AddMinutes(-1),
                null,
                1),
            cancellationToken);

        var snapshot = await access.GetSnapshotAsync(
            clientId,
            "knowledge.read.persistent-test",
            1,
            cancellationToken);
        Assert.NotNull(snapshot);

        var allowed = CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            snapshot.Client,
            snapshot.Capability,
            snapshot.Grants,
            "soulier:persistent-client-test",
            PolicyDecision.Allow,
            now));
        Assert.True(allowed.Allowed);

        Assert.True(await access.SetClientStatusAsync(clientId, ClientStatus.Revoked, cancellationToken));

        var revokedSnapshot = await access.GetSnapshotAsync(
            clientId,
            "knowledge.read.persistent-test",
            1,
            cancellationToken);
        Assert.NotNull(revokedSnapshot);

        var denied = CapabilityAuthorizer.Authorize(new AuthorizationRequest(
            revokedSnapshot.Client,
            revokedSnapshot.Capability,
            revokedSnapshot.Grants,
            "soulier:persistent-client-test",
            PolicyDecision.Allow,
            now));
        Assert.False(denied.Allowed);
        Assert.Equal("CLIENT_REVOKED", denied.ReasonCode);
    }

    [Fact]
    public async Task Grant_for_missing_capability_version_is_rejected_by_database_fk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var access = new EfClientAccess(db);
        var clientId = Guid.Parse("26262626-2626-2626-2626-262626262626");
        await access.UpsertClientAsync(
            new Client(clientId, "missing-version-client-test", "TEST", ClientStatus.Active),
            cancellationToken);
        await access.UpsertCapabilityAsync(
            new Capability("versioned-capability-test", 1, true),
            cancellationToken);

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            await access.UpsertGrantAsync(
                new Grant(
                    clientId,
                    "versioned-capability-test",
                    "soulier:version-test",
                    "TEST",
                    GrantStatus.Active,
                    DateTimeOffset.UtcNow,
                    null,
                    CapabilityMajorVersion: 2),
                cancellationToken));
    }
}
