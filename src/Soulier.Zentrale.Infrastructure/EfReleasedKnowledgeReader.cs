using System.Text;
using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;
using AppAiPolicy = Soulier.Zentrale.Application.AiPolicy;
using AppDataClassification = Soulier.Zentrale.Application.DataClassification;

namespace Soulier.Zentrale.Infrastructure;

/// <summary>
/// Reads only active, hash-bound and reviewed knowledge releases for one configured use case.
/// This first production adapter intentionally supports text-like content only; binary document
/// extraction remains a separate ingestion responsibility.
/// </summary>
public sealed class EfReleasedKnowledgeReader(
    SoulierDbContext dbContext,
    IContentStore contentStore,
    string useCaseKey) : IKnowledgeReader
{
    private const int MaxSearchResults = 50;
    private const int MaxReadChars = 100_000;
    private readonly string _useCaseKey = ValidateUseCaseKey(useCaseKey);

    public async Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(
        KnowledgeSearchRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateContext(context);
        ValidateScope(request.ResourceScope);
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 2_000)
            throw new ArgumentException("Knowledge query must contain 1 to 2000 characters.", nameof(request));

        var nowUtc = DateTimeOffset.UtcNow;
        var maxResults = Math.Clamp(request.MaxResults, 1, MaxSearchResults);
        var queryText = request.Query.Trim();

        var rows = await (
            from release in dbContext.KnowledgeReleases.AsNoTracking()
            join version in dbContext.DocumentVersions.AsNoTracking()
                on release.DocumentVersionId equals version.Id
            join document in dbContext.Documents.AsNoTracking()
                on version.DocumentId equals document.Id
            join source in dbContext.KnowledgeSources.AsNoTracking()
                on document.KnowledgeSourceId equals source.Id
            where release.ClientId == context.ClientId
                && release.ResourceScope == request.ResourceScope
                && release.UseCaseKey == _useCaseKey
                && release.Status == ReleaseStatus.Active
                && release.ValidFromUtc <= nowUtc
                && (release.ValidUntilUtc == null || release.ValidUntilUtc > nowUtc)
                && release.DocumentContentHash == version.ContentHash
                && version.TechnicalReviewStatus == ReviewStatus.Approved
                && version.SubjectReviewStatus == ReviewStatus.Approved
                && version.AiPolicy != Soulier.Zentrale.Domain.AiPolicy.AiForbidden
                && source.IsActive
                && EF.Functions.ILike(document.LogicalName, $"%{queryText}%")
            orderby version.CreatedAtUtc descending
            select new
            {
                document.Id,
                VersionId = version.Id,
                document.LogicalName,
                version.ContentHash,
                version.DataClassification,
                version.AiPolicy,
                version.CreatedAtUtc
            })
            .Distinct()
            .Take(maxResults)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new KnowledgeSearchHit(
                row.Id,
                row.VersionId,
                row.LogicalName,
                null,
                row.ContentHash,
                (AppDataClassification)(int)row.DataClassification,
                (AppAiPolicy)(int)row.AiPolicy,
                row.CreatedAtUtc))
            .ToArray();
    }

    public async Task<string?> ReadAsync(
        Guid documentVersionId,
        string resourceScope,
        int maxChars,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (documentVersionId == Guid.Empty)
            throw new ArgumentException("Document version id is required.", nameof(documentVersionId));
        ValidateContext(context);
        ValidateScope(resourceScope);
        if (maxChars is < 1 or > MaxReadChars)
            throw new ArgumentOutOfRangeException(nameof(maxChars));

        var nowUtc = DateTimeOffset.UtcNow;
        var row = await (
            from release in dbContext.KnowledgeReleases.AsNoTracking()
            join version in dbContext.DocumentVersions.AsNoTracking()
                on release.DocumentVersionId equals version.Id
            join document in dbContext.Documents.AsNoTracking()
                on version.DocumentId equals document.Id
            join source in dbContext.KnowledgeSources.AsNoTracking()
                on document.KnowledgeSourceId equals source.Id
            where release.ClientId == context.ClientId
                && release.ResourceScope == resourceScope
                && release.UseCaseKey == _useCaseKey
                && release.Status == ReleaseStatus.Active
                && release.ValidFromUtc <= nowUtc
                && (release.ValidUntilUtc == null || release.ValidUntilUtc > nowUtc)
                && version.Id == documentVersionId
                && release.DocumentContentHash == version.ContentHash
                && version.TechnicalReviewStatus == ReviewStatus.Approved
                && version.SubjectReviewStatus == ReviewStatus.Approved
                && version.AiPolicy != Soulier.Zentrale.Domain.AiPolicy.AiForbidden
                && source.IsActive
            select new
            {
                version.StorageProvider,
                version.StorageKey,
                version.ContentHash,
                version.MimeType
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        if (!string.Equals(row.StorageProvider, LocalFileContentStore.ProviderKey, StringComparison.Ordinal))
            throw new KnowledgeContentAccessException(
                "STORAGE_PROVIDER_UNSUPPORTED",
                "Released knowledge references a storage provider not supported by this reader.");

        if (!IsTextLike(row.MimeType))
            throw new KnowledgeContentAccessException(
                "CONTENT_TYPE_UNSUPPORTED",
                "Released knowledge content must be converted to a text-like artifact before this reader can expose it.");

        if (!await contentStore.VerifyAsync(row.StorageKey, row.ContentHash, cancellationToken))
            throw new KnowledgeContentAccessException(
                "CONTENT_INTEGRITY_FAILED",
                "Released knowledge content failed its hash verification.");

        await using var stream = await contentStore.OpenReadAsync(row.StorageKey, cancellationToken);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);

        var buffer = new char[maxChars];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(0, maxChars), cancellationToken);
        return new string(buffer, 0, read);
    }

    private static bool IsTextLike(string mimeType) =>
        mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mimeType, "application/json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mimeType, "application/xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mimeType, "application/markdown", StringComparison.OrdinalIgnoreCase);

    private static string ValidateUseCaseKey(string useCaseKey)
    {
        if (string.IsNullOrWhiteSpace(useCaseKey) || useCaseKey.Length > 200)
            throw new ArgumentException("Use case key must contain 1 to 200 characters.", nameof(useCaseKey));
        return useCaseKey.Trim();
    }

    private static void ValidateScope(string resourceScope)
    {
        if (string.IsNullOrWhiteSpace(resourceScope) || resourceScope.Length > 500)
            throw new ArgumentException("Resource scope must contain 1 to 500 characters.", nameof(resourceScope));
    }

    private static void ValidateContext(RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ClientId == Guid.Empty)
            throw new ArgumentException("Client id is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.CorrelationId))
            throw new ArgumentException("Correlation id is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.Environment))
            throw new ArgumentException("Environment is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.CapabilityKey))
            throw new ArgumentException("Capability key is required.", nameof(context));
    }
}

public sealed class KnowledgeContentAccessException : InvalidOperationException
{
    public string ReasonCode { get; }

    public KnowledgeContentAccessException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }
}
