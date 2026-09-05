using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class PostgresPersistenceTests
{
    [Fact]
    public async Task Initial_migration_applies_and_enforces_core_constraints()
    {
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync();

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('knowledge.document_version') is not null"));
        Assert.True(await ScalarBoolAsync(connection,
            "select to_regclass('audit.event') is not null"));
        Assert.True(await ScalarBoolAsync(connection,
            "select exists (select 1 from pg_indexes where schemaname = 'knowledge' and tablename = 'release' and indexname = 'IX_release_DocumentVersionId')"));

        await ExecuteAsync(connection, """
            insert into clients.client ("Id", "Name", "Environment", "Status")
            values ('11111111-1111-1111-1111-111111111111', 'codex-pilot', 'TEST', 1)
            """);

        await ExecuteAsync(connection, """
            insert into knowledge.source ("Id", "Name", "SourceType", "IsActive", "CreatedAtUtc")
            values ('22222222-2222-2222-2222-222222222222', 'Gate-3-Testwissen', 'test', true, '2026-09-06T00:00:00+00:00')
            """);

        await ExecuteAsync(connection, """
            insert into knowledge.document ("Id", "KnowledgeSourceId", "LogicalName", "CreatedAtUtc")
            values ('33333333-3333-3333-3333-333333333333', '22222222-2222-2222-2222-222222222222', 'test.txt', '2026-09-06T00:01:00+00:00')
            """);

        await ExecuteAsync(connection, """
            insert into knowledge.document_version
            ("Id", "DocumentId", "VersionNumber", "ContentHash", "StorageProvider", "StorageKey", "MimeType", "SizeBytes",
             "TechnicalReviewStatus", "SubjectReviewStatus", "DataClassification", "AiPolicy", "CreatedAtUtc", "CreatedByHumanPrincipalId")
            values
            ('44444444-4444-4444-4444-444444444444', '33333333-3333-3333-3333-333333333333', 1, 'sha256:test', 'test-storage',
             'knowledge/test.txt', 'text/plain', 42, 1, 1, 1, 1, '2026-09-06T00:02:00+00:00', null)
            """);

        await ExecuteAsync(connection, """
            insert into knowledge.release
            ("Id", "DocumentVersionId", "ClientId", "ResourceScope", "UseCaseKey", "Status", "ValidFromUtc", "ValidUntilUtc", "CreatedAtUtc")
            values
            ('55555555-5555-5555-5555-555555555555', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111',
             'soulier:test', 'codex-pilot', 1, '2026-09-06T00:03:00+00:00', '2026-09-07T00:03:00+00:00', '2026-09-06T00:03:00+00:00')
            """);

        await ExecuteAsync(connection, """
            insert into audit.event
            ("Id", "OccurredAtUtc", "CorrelationId", "HumanPrincipalId", "ClientId", "ServiceIdentityId", "CapabilityKey",
             "ResourceType", "ResourceId", "DocumentVersionId", "ContentHash", "PolicyVersion", "ApprovalId", "Result", "ReasonCode",
             "SourceAdapter", "DurationMs")
            values
            ('66666666-6666-6666-6666-666666666666', '2026-09-06T00:04:00+00:00', 'corr-gate3-001', null,
             '11111111-1111-1111-1111-111111111111', null, 'knowledge.read', 'document_version',
             '44444444-4444-4444-4444-444444444444', '44444444-4444-4444-4444-444444444444', 'sha256:test', 'policy:test', null,
             'ALLOW', 'ALLOW', 'knowledge', 12)
            """);

        await Assert.ThrowsAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into knowledge.document_version
                ("Id", "DocumentId", "VersionNumber", "ContentHash", "StorageProvider", "StorageKey", "MimeType", "SizeBytes",
                 "TechnicalReviewStatus", "SubjectReviewStatus", "DataClassification", "AiPolicy", "CreatedAtUtc", "CreatedByHumanPrincipalId")
                values
                ('77777777-7777-7777-7777-777777777777', '33333333-3333-3333-3333-333333333333', 0, 'sha256:invalid', 'test-storage',
                 'knowledge/invalid.txt', 'text/plain', 1, 1, 1, 1, 1, '2026-09-06T00:05:00+00:00', null)
                """));

        await Assert.ThrowsAsync<DbException>(async () =>
            await ExecuteAsync(connection, """
                insert into knowledge.release
                ("Id", "DocumentVersionId", "ClientId", "ResourceScope", "UseCaseKey", "Status", "ValidFromUtc", "ValidUntilUtc", "CreatedAtUtc")
                values
                ('88888888-8888-8888-8888-888888888888', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111',
                 'soulier:test', 'codex-pilot-invalid', 1, '2026-09-07T00:00:00+00:00', '2026-09-06T00:00:00+00:00', '2026-09-06T00:06:00+00:00')
                """));
    }

    private static async Task<int> ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ScalarBoolAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }
}
