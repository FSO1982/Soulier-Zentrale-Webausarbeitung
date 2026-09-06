using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class HumanAccessAdministrationTests
{
    [Fact]
    public async Task Admin_service_creates_disables_and_assigns_human_access_without_code_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var admin = new EfHumanAccessAdministration(db);
        var reader = new EfHumanAccessReader(db);
        var now = DateTimeOffset.UtcNow;
        var humanId = Guid.Parse("17171717-1717-1717-1717-171717171717");
        var roleId = Guid.Parse("18181818-1818-1818-1818-181818181818");

        var human = await admin.CreateHumanAsync(
            new CreateHumanPrincipalCommand(
                humanId,
                "future-user-test-sub",
                "Future Test User",
                now),
            cancellationToken);
        Assert.True(human.IsActive);

        var role = await admin.UpsertRoleAsync(
            new UpsertRoleCommand(roleId, "knowledge-user-test", "Knowledge User Test", true),
            cancellationToken);
        Assert.True(role.IsActive);

        await admin.ReplaceRoleCapabilitiesAsync(
            roleId,
            ["knowledge.search", "knowledge.read"],
            cancellationToken);

        await admin.AssignRoleAsync(
            new HumanRoleAssignment(
                Guid.Parse("19191919-1919-1919-1919-191919191919"),
                humanId,
                roleId,
                "soulier:future-user-test",
                "TEST",
                HumanRoleAssignmentStatus.Active,
                now.AddMinutes(-1),
                null),
            cancellationToken);

        var snapshot = await reader.GetAccessSnapshotAsync(humanId, cancellationToken);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.RoleCapabilities.Count);
        Assert.Single(snapshot.Assignments);

        var allowed = HumanAccessAuthorizer.Authorize(new HumanAuthorizationRequest(
            snapshot.Principal,
            snapshot.Roles,
            snapshot.RoleCapabilities,
            snapshot.Assignments,
            "knowledge.read",
            "soulier:future-user-test",
            "TEST",
            now));
        Assert.True(allowed.Allowed);

        Assert.True(await admin.SetHumanStatusAsync(
            humanId,
            HumanPrincipalStatus.Disabled,
            cancellationToken));

        var disabledSnapshot = await reader.GetAccessSnapshotAsync(humanId, cancellationToken);
        Assert.NotNull(disabledSnapshot);
        Assert.Equal(HumanPrincipalStatus.Disabled, disabledSnapshot.Principal.Status);

        var denied = HumanAccessAuthorizer.Authorize(new HumanAuthorizationRequest(
            disabledSnapshot.Principal,
            disabledSnapshot.Roles,
            disabledSnapshot.RoleCapabilities,
            disabledSnapshot.Assignments,
            "knowledge.read",
            "soulier:future-user-test",
            "TEST",
            now));
        Assert.False(denied.Allowed);
        Assert.Equal("HUMAN_DISABLED", denied.ReasonCode);
    }
}
