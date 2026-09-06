using System.Text;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class ReleasedKnowledgeReaderTests
{
    [Fact]
    public async Task Reader_exposes_only_matching_released_scope_and_verifies_content_hash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var root = Path.Combine(Path.GetTempPath(), $"soulier-released-knowledge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new LocalFileContentStore(root);
            const string content = "Dachsanierung Gate-3: freigegebener persistierter Wissensinhalt.";
            await using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var stored = await store.StoreAsync(contentStream, cancellationToken);

            var clientId = Guid.Parse("10101010-1010-1010-1010-101010101010");
            var sourceId = Guid.Parse("20202020-2020-2020-2020-202020202020");
            var documentId = Guid.Parse("30303030-3030-3030-3030-303030303030");
            var versionId = Guid.Parse("40404040-4040-4040-4040-404040404040");
            var releaseId = Guid.Parse("50505050-5050-5050-5050-505050505050");
            var now = DateTimeOffset.UtcNow;

            db.Clients.Add(new Client(clientId, "persisted-reader-test", "TEST", ClientStatus.Active));
            db.KnowledgeSources.Add(new KnowledgeSource(sourceId, "Testwissen", "text", true, now.AddMinutes(-10)));
            db.Documents.Add(new KnowledgeDocument(documentId, sourceId, "Dachsanierung-Testwissen.md", now.AddMinutes(-9)));
            db.DocumentVersions.Add(new DocumentVersion(
                versionId,
                documentId,
                1,
                stored.ContentHash,
                stored.StorageProvider,
                stored.StorageKey,
                "text/markdown",
                stored.SizeBytes,
                ReviewStatus.Approved,
                ReviewStatus.Approved,
                Soulier.Zentrale.Domain.DataClassification.Confidential,
                Soulier.Zentrale.Domain.AiPolicy.LocalOnly,
                now.AddMinutes(-8),
                null));
            db.KnowledgeReleases.Add(new KnowledgeRelease(
                releaseId,
                versionId,
                stored.ContentHash,
                clientId,
                "soulier:pilot",
                "codex-persisted-test",
                ReleaseStatus.Active,
                now.AddMinutes(-5),
                now.AddHours(1),
                now.AddMinutes(-5)));
            await db.SaveChangesAsync(cancellationToken);

            var reader = new EfReleasedKnowledgeReader(db, store, "codex-persisted-test");
            var searchContext = new RequestContext("corr-search", clientId, "TEST", "knowledge.search");
            var hits = await reader.SearchAsync(
                new KnowledgeSearchRequest("Dachsanierung", "soulier:pilot"),
                searchContext,
                cancellationToken);

            var hit = Assert.Single(hits);
            Assert.Equal(versionId, hit.DocumentVersionId);
            Assert.Equal(stored.ContentHash, hit.ContentHash);
            Assert.Equal(Soulier.Zentrale.Application.DataClassification.Confidential, hit.DataClassification);
            Assert.Equal(Soulier.Zentrale.Application.AiPolicy.LocalOnly, hit.AiPolicy);

            var readContext = new RequestContext("corr-read", clientId, "TEST", "knowledge.read");
            var read = await reader.ReadAsync(
                versionId,
                "soulier:pilot",
                8_000,
                readContext,
                cancellationToken);
            Assert.Equal(content, read);

            var foreignScope = await reader.ReadAsync(
                versionId,
                "soulier:other",
                8_000,
                readContext,
                cancellationToken);
            Assert.Null(foreignScope);

            var storedPath = Path.Combine(root, stored.StorageKey.Replace('/', Path.DirectorySeparatorChar));
            await File.WriteAllTextAsync(storedPath, "manipulated", cancellationToken);

            var integrityException = await Assert.ThrowsAsync<KnowledgeContentAccessException>(async () =>
                await reader.ReadAsync(
                    versionId,
                    "soulier:pilot",
                    8_000,
                    readContext,
                    cancellationToken));
            Assert.Equal("CONTENT_INTEGRITY_FAILED", integrityException.ReasonCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Ai_forbidden_release_is_not_searchable_or_readable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable("SOULIER_TEST_POSTGRES");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SoulierDbContext>()
            .UseNpgsql(connectionString!)
            .Options;
        await using var db = new SoulierDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var root = Path.Combine(Path.GetTempPath(), $"soulier-ai-forbidden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new LocalFileContentStore(root);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("blocked content"));
            var stored = await store.StoreAsync(stream, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var clientId = Guid.Parse("60606060-6060-6060-6060-606060606060");
            var sourceId = Guid.Parse("70707070-7070-7070-7070-707070707070");
            var documentId = Guid.Parse("80808080-8080-8080-8080-808080808080");
            var versionId = Guid.Parse("90909090-9090-9090-9090-909090909090");

            db.Clients.Add(new Client(clientId, "ai-forbidden-reader-test", "TEST", ClientStatus.Active));
            db.KnowledgeSources.Add(new KnowledgeSource(sourceId, "Gesperrtes Wissen", "text", true, now.AddMinutes(-10)));
            db.Documents.Add(new KnowledgeDocument(documentId, sourceId, "Geheim-Testwissen.md", now.AddMinutes(-9)));
            db.DocumentVersions.Add(new DocumentVersion(
                versionId,
                documentId,
                1,
                stored.ContentHash,
                stored.StorageProvider,
                stored.StorageKey,
                "text/plain",
                stored.SizeBytes,
                ReviewStatus.Approved,
                ReviewStatus.Approved,
                Soulier.Zentrale.Domain.DataClassification.Restricted,
                Soulier.Zentrale.Domain.AiPolicy.AiForbidden,
                now.AddMinutes(-8),
                null));
            db.KnowledgeReleases.Add(new KnowledgeRelease(
                Guid.Parse("a0a0a0a0-a0a0-a0a0-a0a0-a0a0a0a0a0a0"),
                versionId,
                stored.ContentHash,
                clientId,
                "soulier:restricted",
                "codex-persisted-test",
                ReleaseStatus.Active,
                now.AddMinutes(-5),
                null,
                now.AddMinutes(-5)));
            await db.SaveChangesAsync(cancellationToken);

            var reader = new EfReleasedKnowledgeReader(db, store, "codex-persisted-test");
            var searchContext = new RequestContext("corr-ai-forbidden-search", clientId, "TEST", "knowledge.search");
            var hits = await reader.SearchAsync(
                new KnowledgeSearchRequest("Geheim", "soulier:restricted"),
                searchContext,
                cancellationToken);
            Assert.Empty(hits);

            var readContext = new RequestContext("corr-ai-forbidden-read", clientId, "TEST", "knowledge.read");
            var content = await reader.ReadAsync(
                versionId,
                "soulier:restricted",
                1_000,
                readContext,
                cancellationToken);
            Assert.Null(content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
