using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class PostgresProviderApprovalTests
{
    [Fact]
    public async Task Provider_and_approval_state_enforce_explicit_evidence_and_exact_execution_binding()
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

        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('ai.provider') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('ai.model_route') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('ai.provider_use_case_grant') is not null", cancellationToken));
        Assert.True(await ScalarBoolAsync(connection, "select to_regclass('approval.execution_approval') is not null", cancellationToken));

        const string humanId = "21212121-2121-2121-2121-212121212121";
        await ExecuteAsync(connection, $"""
            insert into identity.human_principal
            ("Id", "OidcSubject", "DisplayName", "Status", "CreatedAtUtc")
            values
            ('{humanId}', 'provider-approval-frank-test', 'Frank Provider Approval Test', 0, '2026-09-06T03:00:00+00:00')
            """, cancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into ai.provider
                ("Key", "Target", "Status", "ApprovedByHumanPrincipalId", "ApprovedAtUtc")
                values ('external-without-evidence-test', 1, 1, null, null)
                """, cancellationToken));

        await ExecuteAsync(connection, $"""
            insert into ai.provider
            ("Key", "Target", "Status", "ApprovedByHumanPrincipalId", "ApprovedAtUtc")
            values ('external-approved-test', 1, 1, '{humanId}', '2026-09-06T03:01:00+00:00')
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into ai.model_route
            ("Key", "ProviderKey", "ModelAlias", "IsActive")
            values ('route-external-approved-test', 'external-approved-test', 'approved-model-alias', true)
            """, cancellationToken);

        await ExecuteAsync(connection, """
            insert into ai.provider_use_case_grant
            ("ProviderKey", "UseCaseKey")
            values ('external-approved-test', 'provider-policy-test')
            """, cancellationToken);

        var routeDecision = ModelRoutePolicy.Evaluate(new ModelRouteEvaluationRequest(
            AiPolicy.ExternalAllowed,
            "provider-policy-test",
            new ModelRouteDefinition("route-external-approved-test", "external-approved-test", "approved-model-alias", true),
            new ProviderDefinition(
                "external-approved-test",
                ModelExecutionTarget.External,
                ProviderApprovalStatus.Approved,
                Guid.Parse(humanId),
                new DateTimeOffset(2026, 9, 6, 3, 1, 0, TimeSpan.Zero)),
            [new ProviderUseCaseGrant("external-approved-test", "provider-policy-test")]));
        Assert.True(routeDecision.Allowed);

        await ExecuteAsync(connection, """
            insert into automation.action_definition
            ("Key", "Mode", "IsActive", "ParameterPolicyVersion")
            values ('approval-bound-action-test', 2, true, 'v1')
            """, cancellationToken);

        await ExecuteAsync(connection, $"""
            insert into approval.execution_approval
            ("Id", "ActionKey", "IdempotencyKey", "HumanPrincipalId", "Status", "CreatedAtUtc", "DecidedAtUtc", "ValidUntilUtc")
            values
            ('22222222-2222-2222-2222-222222222222', 'approval-bound-action-test', 'idem-approval-test', '{humanId}', 1, '2026-09-06T03:02:00+00:00', '2026-09-06T03:03:00+00:00', '2026-09-06T04:00:00+00:00')
            """, cancellationToken);

        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await ExecuteAsync(connection, $"""
                insert into approval.execution_approval
                ("Id", "ActionKey", "IdempotencyKey", "HumanPrincipalId", "Status", "CreatedAtUtc", "DecidedAtUtc", "ValidUntilUtc")
                values
                ('23232323-2323-2323-2323-232323232323', 'approval-bound-action-test', 'idem-approval-test', '{humanId}', 1, '2026-09-06T03:04:00+00:00', '2026-09-06T03:05:00+00:00', '2026-09-06T04:00:00+00:00')
                """, cancellationToken));

        var approvalDecision = ApprovalPolicy.Evaluate(new ApprovalEvaluationRequest(
            "approval-bound-action-test",
            "idem-approval-test",
            new DateTimeOffset(2026, 9, 6, 3, 30, 0, TimeSpan.Zero),
            new ExecutionApproval(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "approval-bound-action-test",
                "idem-approval-test",
                Guid.Parse(humanId),
                ApprovalStatus.Approved,
                new DateTimeOffset(2026, 9, 6, 3, 2, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 6, 3, 3, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero))));
        Assert.True(approvalDecision.Satisfied);
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
