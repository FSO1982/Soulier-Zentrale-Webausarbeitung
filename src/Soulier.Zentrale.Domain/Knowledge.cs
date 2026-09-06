namespace Soulier.Zentrale.Domain;

public enum DataClassification { Public, Internal, Confidential, Restricted }
public enum AiPolicy { ExternalAllowed, LocalOnly, AiForbidden }
public enum ReviewStatus { Pending, Approved, Rejected }
public enum ReleaseStatus { Draft, Active, Revoked }

public sealed record KnowledgeSource(
    Guid Id,
    string Name,
    string SourceType,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record KnowledgeDocument(
    Guid Id,
    Guid KnowledgeSourceId,
    string LogicalName,
    DateTimeOffset CreatedAtUtc);

public sealed record DocumentVersion(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string ContentHash,
    string StorageProvider,
    string StorageKey,
    string MimeType,
    long SizeBytes,
    ReviewStatus TechnicalReviewStatus,
    ReviewStatus SubjectReviewStatus,
    DataClassification DataClassification,
    AiPolicy AiPolicy,
    DateTimeOffset CreatedAtUtc,
    Guid? CreatedByHumanPrincipalId)
{
    public static DocumentVersion Create(
        Guid id,
        Guid documentId,
        int versionNumber,
        string contentHash,
        string storageProvider,
        string storageKey,
        string mimeType,
        long sizeBytes,
        DataClassification dataClassification,
        AiPolicy aiPolicy,
        DateTimeOffset createdAtUtc,
        Guid? createdByHumanPrincipalId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Version id is required.", nameof(id));
        if (documentId == Guid.Empty) throw new ArgumentException("Document id is required.", nameof(documentId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (string.IsNullOrWhiteSpace(contentHash)) throw new ArgumentException("Content hash is required.", nameof(contentHash));
        if (string.IsNullOrWhiteSpace(storageProvider)) throw new ArgumentException("Storage provider is required.", nameof(storageProvider));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key is required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(mimeType)) throw new ArgumentException("MIME type is required.", nameof(mimeType));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        return new DocumentVersion(
            id,
            documentId,
            versionNumber,
            contentHash,
            storageProvider,
            storageKey,
            mimeType,
            sizeBytes,
            ReviewStatus.Pending,
            ReviewStatus.Pending,
            dataClassification,
            aiPolicy,
            createdAtUtc,
            createdByHumanPrincipalId);
    }

    public bool IsEligibleForRelease =>
        TechnicalReviewStatus == ReviewStatus.Approved &&
        SubjectReviewStatus == ReviewStatus.Approved;
}

public sealed record KnowledgeRelease(
    Guid Id,
    Guid DocumentVersionId,
    string DocumentContentHash,
    Guid ClientId,
    string ResourceScope,
    string UseCaseKey,
    ReleaseStatus Status,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset? ValidUntilUtc,
    DateTimeOffset CreatedAtUtc)
{
    public bool IsActiveAt(DateTimeOffset nowUtc) =>
        Status == ReleaseStatus.Active &&
        ValidFromUtc <= nowUtc &&
        (ValidUntilUtc is null || ValidUntilUtc > nowUtc);
}

public static class KnowledgeReleasePolicy
{
    public static AuthorizationResult CanRelease(DocumentVersion version, KnowledgeRelease release, DateTimeOffset nowUtc)
    {
        if (version.Id != release.DocumentVersionId)
            return AuthorizationResult.Deny("RELEASE_VERSION_MISMATCH");

        if (!string.Equals(version.ContentHash, release.DocumentContentHash, StringComparison.Ordinal))
            return AuthorizationResult.Deny("RELEASE_HASH_MISMATCH");

        if (!version.IsEligibleForRelease)
            return AuthorizationResult.Deny("DOCUMENT_REVIEW_REQUIRED");

        if (version.AiPolicy == AiPolicy.AiForbidden)
            return AuthorizationResult.Deny("POLICY_DENIED");

        if (!release.IsActiveAt(nowUtc))
            return AuthorizationResult.Deny("RELEASE_INACTIVE");

        return AuthorizationResult.Allow();
    }
}
