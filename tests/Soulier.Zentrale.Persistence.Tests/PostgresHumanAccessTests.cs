using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class PostgresHumanAccessTests
{
    [Fact]
    public async Task Human_access_schema_is_persistent_unique_scoped_and_readable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('identity.human_principal') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('identity.role') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('identity.role_capability') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('identity.human_role_assignment') is not null", cancellationToken));

        await ExecuteAsync(connection, """
            insert into identity.human_principal
            ("Id", "OidcSubject", "DisplayName", "Status", "CreatedAtUtc")
            values
            ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'frank-authentik-sub', 'Frank Soulier', 0, '2026-09-06T01:00:00+00:00')
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into identity.role
            ("Id", "Key", "Name", "IsActive")
            values
            ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'v1-admin', 'V1 Administrator', true)
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into identity.role_capability
            ("RoleId", "CapabilityKey")
            values
            ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'knowledge.read')
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into identity.human_role_assignment
            ("Id", "HumanPrincipalId", "RoleId", "ResourceScope", "Environment", "Status", "ValidFromUtc", "ValidUntilUtc")
            values
            ('cccccccc-cccc-cccc-cccc-cccccccccccc',
             'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
             'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
             'soulier:pilot', 'TEST', 0, '2026-09-06T01:01:00+00:00', null)
            """, cancellationToken);

        var reader = new EfHumanAccessReader(db);
        var principal = await reader.FindByOidcSubjectAsync(
            "frank-authentik-sub",
            cancellationToken);
        var snapshot = await reader.GetAccessSnapshotAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            cancellationToken);

        Assert.NotNull(principal);
        Assert.True(principal.IsActive);
        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Assignments);
        Assert.Single(snapshot.Roles);
        Assert.Single(snapshot.RoleCapabilities);

        var authorization = HumanAccessAuthorizer.Authorize(new HumanAuthorizationRequest(
            snapshot.Principal,
            snapshot.Roles,
            snapshot.RoleCapabilities,
            snapshot.Assignments,
            "knowledge.read",
            "soulier:pilot",
            "TEST",
            new DateTimeOffset(2026, 9, 6, 1, 5, 0, TimeSpan.Zero)));

        Assert.True(authorization.Allowed);

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into identity.human_principal
                ("Id", "OidcSubject", "DisplayName", "Status", "CreatedAtUtc")
                values
                ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'frank-authentik-sub', 'Duplicate Subject', 0, '2026-09-06T01:06:00+00:00')
                """, cancellationToken));

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into identity.human_role_assignment
                ("Id", "HumanPrincipalId", "RoleId", "ResourceScope", "Environment", "Status", "ValidFromUtc", "ValidUntilUtc")
                values
                ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
                 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
                 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
                 'soulier:pilot', 'TEST', 0, '2026-09-07T00:00:00+00:00', '2026-09-06T00:00:00+00:00')
                """, cancellationToken));
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ScalarBoolAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
}
