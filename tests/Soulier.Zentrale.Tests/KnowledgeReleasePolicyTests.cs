using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class KnowledgeReleasePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ContentHash = "sha256:test";

    [Fact]
    public void New_version_starts_without_review_approval()
    {
        var version = CreateVersion(AiPolicy.LocalOnly);

        Assert.Equal(ReviewStatus.Pending, version.TechnicalReviewStatus);
        Assert.Equal(ReviewStatus.Pending, version.SubjectReviewStatus);
        Assert.False(version.IsEligibleForRelease);
    }

    [Fact]
    public void Ai_forbidden_version_cannot_be_released()
    {
        var version = ApprovedVersion(AiPolicy.AiForbidden);
        var result = KnowledgeReleasePolicy.CanRelease(version, ActiveRelease(), Now);

        Assert.False(result.Allowed);
        Assert.Equal("POLICY_DENIED", result.ReasonCode);
    }

    [Fact]
    public void Unreviewed_version_cannot_be_released()
    {
        var version = CreateVersion(AiPolicy.LocalOnly);
        var result = KnowledgeReleasePolicy.CanRelease(version, ActiveRelease(), Now);

        Assert.False(result.Allowed);
        Assert.Equal("DOCUMENT_REVIEW_REQUIRED", result.ReasonCode);
    }

    [Fact]
    public void Release_is_bound_to_exact_document_version()
    {
        var version = ApprovedVersion(AiPolicy.LocalOnly);
        var release = ActiveRelease() with { DocumentVersionId = Guid.NewGuid() };
        var result = KnowledgeReleasePolicy.CanRelease(version, release, Now);

        Assert.False(result.Allowed);
        Assert.Equal("RELEASE_VERSION_MISMATCH", result.ReasonCode);
    }

    [Fact]
    public void Release_is_bound_to_exact_content_hash()
    {
        var version = ApprovedVersion(AiPolicy.LocalOnly);
        var release = ActiveRelease() with { DocumentContentHash = "sha256:changed" };
        var result = KnowledgeReleasePolicy.CanRelease(version, release, Now);

        Assert.False(result.Allowed);
        Assert.Equal("RELEASE_HASH_MISMATCH", result.ReasonCode);
    }

    [Fact]
    public void Expired_release_is_denied()
    {
        var version = ApprovedVersion(AiPolicy.LocalOnly);
        var release = ActiveRelease() with { ValidUntilUtc = Now };
        var result = KnowledgeReleasePolicy.CanRelease(version, release, Now);

        Assert.False(result.Allowed);
        Assert.Equal("RELEASE_INACTIVE", result.ReasonCode);
    }

    [Fact]
    public void Reviewed_local_only_version_with_active_release_is_allowed()
    {
        var version = ApprovedVersion(AiPolicy.LocalOnly);
        var result = KnowledgeReleasePolicy.CanRelease(version, ActiveRelease(), Now);

        Assert.True(result.Allowed);
    }

    private static DocumentVersion CreateVersion(AiPolicy aiPolicy) =>
        DocumentVersion.Create(
            VersionId,
            DocumentId,
            1,
            ContentHash,
            "test-storage",
            "knowledge/test.txt",
            "text/plain",
            42,
            DataClassification.Internal,
            aiPolicy,
            Now.AddMinutes(-5),
            null);

    private static DocumentVersion ApprovedVersion(AiPolicy aiPolicy) =>
        CreateVersion(aiPolicy) with
        {
            TechnicalReviewStatus = ReviewStatus.Approved,
            SubjectReviewStatus = ReviewStatus.Approved
        };

    private static KnowledgeRelease ActiveRelease() =>
        new(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            VersionId,
            ContentHash,
            ClientId,
            "soulier:test",
            "codex-pilot",
            ReleaseStatus.Active,
            Now.AddMinutes(-1),
            Now.AddHours(1),
            Now.AddMinutes(-2));
}
