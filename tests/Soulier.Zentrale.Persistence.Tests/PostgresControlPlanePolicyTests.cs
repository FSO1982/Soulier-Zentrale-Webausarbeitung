using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class PostgresControlPlanePolicyTests
{
    [Fact]
    public async Task Control_plane_tables_enforce_active_version_idempotency_and_retention_constraints()
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

        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('ai.use_case') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('ai.use_case_version') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('automation.action_definition') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('automation.action_execution') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('policy.retention_rule') is not null", cancellationToken));

        await ExecuteAsync(connection, """
            insert into ai.use_case
            ("Id", "Key", "Name", "IsActive", "CreatedAtUtc")
            values
            ('12121212-1212-1212-1212-121212121212', 'policy-test-use-case', 'Policy Test Use Case', true, '2026-09-06T02:00:00+00:00')
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into ai.use_case_version
            ("Id", "AiUseCaseId", "VersionNumber", "PromptTemplateHash", "ModelRouteKey", "Status", "TechnicalReviewStatus", "SubjectReviewStatus", "ContentLoggingMode", "CreatedAtUtc", "ActivatedAtUtc")
            values
            ('13131313-1313-1313-1313-131313131313', '12121212-1212-1212-1212-121212121212', 1, 'hash-v1', 'local-default', 1, 1, 1, 0, '2026-09-06T02:01:00+00:00', '2026-09-06T02:02:00+00:00')
            """, cancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into ai.use_case_version
                ("Id", "AiUseCaseId", "VersionNumber", "PromptTemplateHash", "ModelRouteKey", "Status", "TechnicalReviewStatus", "SubjectReviewStatus", "ContentLoggingMode", "CreatedAtUtc", "ActivatedAtUtc")
                values
                ('14141414-1414-1414-1414-141414141414', '12121212-1212-1212-1212-121212121212', 2, 'hash-v2', 'local-default', 1, 1, 1, 0, '2026-09-06T02:03:00+00:00', '2026-09-06T02:04:00+00:00')
                """, cancellationToken));

        await ExecuteAsync(connection, """
            insert into automation.action_definition
            ("Key", "Mode", "IsActive", "ParameterPolicyVersion")
            values ('policy-test-action', 1, true, 'v1')
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into automation.action_execution
            ("Id", "ActionKey", "IdempotencyKey", "ClientId", "ResourceScope", "CorrelationId", "State", "CreatedAtUtc", "CompletedAtUtc", "ResultReference")
            values
            ('15151515-1515-1515-1515-151515151515', 'policy-test-action', 'idem-policy-test', null, 'soulier:pilot', 'corr-policy-test', 2, '2026-09-06T02:05:00+00:00', '2026-09-06T02:06:00+00:00', 'result-1')
            """, cancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into automation.action_execution
                ("Id", "ActionKey", "IdempotencyKey", "ClientId", "ResourceScope", "CorrelationId", "State", "CreatedAtUtc", "CompletedAtUtc", "ResultReference")
                values
                ('16161616-1616-1616-1616-161616161616', 'policy-test-action', 'idem-policy-test', null, 'soulier:pilot', 'corr-policy-test-duplicate', 1, '2026-09-06T02:07:00+00:00', null, null)
                """, cancellationToken));

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into policy.retention_rule
                ("Category", "RetainFor", "DeletionEnabled", "LegalHoldSupported")
                values (4, null, true, true)
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
