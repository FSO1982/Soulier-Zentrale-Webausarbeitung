using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class AiUseCasePolicyTests
{
    private static readonly Guid UseCaseId = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reviewed_draft_can_replace_active_version_without_mutating_it_before_activation()
    {
        var useCase = new AiUseCase(UseCaseId, "knowledge-assistant", "Knowledge Assistant", true, Now.AddDays(-5));
        var active = Version(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 1, AiUseCaseVersionStatus.Active, ReviewStatus.Approved, ReviewStatus.Approved);
        var draft = Version(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 2, AiUseCaseVersionStatus.Draft, ReviewStatus.Approved, ReviewStatus.Approved);

        var decision = AiUseCaseVersionPolicy.PlanActivation(useCase, draft, [active, draft]);

        Assert.True(decision.Allowed);
        Assert.NotNull(decision.Plan);
        Assert.Equal(draft.Id, decision.Plan.CandidateVersionId);
        Assert.Contains(active.Id, decision.Plan.VersionsToRetire);
        Assert.Equal(AiUseCaseVersionStatus.Active, active.Status);
        Assert.Equal(AiUseCaseVersionStatus.Draft, draft.Status);
    }

    [Theory]
    [InlineData(ReviewStatus.Pending, ReviewStatus.Approved)]
    [InlineData(ReviewStatus.Approved, ReviewStatus.Pending)]
    [InlineData(ReviewStatus.Rejected, ReviewStatus.Approved)]
    public void Unreviewed_or_rejected_draft_cannot_be_activated(
        ReviewStatus technical,
        ReviewStatus subject)
    {
        var useCase = new AiUseCase(UseCaseId, "knowledge-assistant", "Knowledge Assistant", true, Now);
        var draft = Version(Guid.NewGuid(), 2, AiUseCaseVersionStatus.Draft, technical, subject);

        var decision = AiUseCaseVersionPolicy.PlanActivation(useCase, draft, [draft]);

        Assert.False(decision.Allowed);
        Assert.Equal("USE_CASE_REVIEW_REQUIRED", decision.ReasonCode);
    }

    [Fact]
    public void Duplicate_version_number_is_denied()
    {
        var useCase = new AiUseCase(UseCaseId, "knowledge-assistant", "Knowledge Assistant", true, Now);
        var existing = Version(Guid.NewGuid(), 2, AiUseCaseVersionStatus.Retired, ReviewStatus.Approved, ReviewStatus.Approved);
        var candidate = Version(Guid.NewGuid(), 2, AiUseCaseVersionStatus.Draft, ReviewStatus.Approved, ReviewStatus.Approved);

        var decision = AiUseCaseVersionPolicy.PlanActivation(useCase, candidate, [existing, candidate]);

        Assert.False(decision.Allowed);
        Assert.Equal("VERSION_CONFLICT", decision.ReasonCode);
    }

    [Fact]
    public void Full_content_logging_is_off_by_default()
    {
        var decision = ContentLoggingPolicy.Evaluate(new ContentLoggingRequest(
            ContentLoggingMode.FullContent,
            ExplicitlyEnabled: false,
            DataClassification.Internal,
            ContainsSecret: false,
            new HashSet<DataClassification> { DataClassification.Internal }));

        Assert.Equal(ContentLoggingMode.MetadataOnly, decision.EffectiveMode);
        Assert.Equal("METADATA_ONLY_DEFAULT", decision.ReasonCode);
    }

    [Fact]
    public void Secret_content_is_never_full_logged_even_when_explicitly_configured()
    {
        var decision = ContentLoggingPolicy.Evaluate(new ContentLoggingRequest(
            ContentLoggingMode.FullContent,
            ExplicitlyEnabled: true,
            DataClassification.Confidential,
            ContainsSecret: true,
            new HashSet<DataClassification> { DataClassification.Confidential }));

        Assert.Equal(ContentLoggingMode.MetadataOnly, decision.EffectiveMode);
        Assert.Equal("SECRET_CONTENT_BLOCKED", decision.ReasonCode);
    }

    [Fact]
    public void Full_content_requires_explicit_data_class_allowlist()
    {
        var denied = ContentLoggingPolicy.Evaluate(new ContentLoggingRequest(
            ContentLoggingMode.FullContent,
            ExplicitlyEnabled: true,
            DataClassification.Restricted,
            ContainsSecret: false,
            new HashSet<DataClassification> { DataClassification.Internal }));
        var allowed = ContentLoggingPolicy.Evaluate(new ContentLoggingRequest(
            ContentLoggingMode.FullContent,
            ExplicitlyEnabled: true,
            DataClassification.Internal,
            ContainsSecret: false,
            new HashSet<DataClassification> { DataClassification.Internal }));

        Assert.Equal(ContentLoggingMode.MetadataOnly, denied.EffectiveMode);
        Assert.Equal(ContentLoggingMode.FullContent, allowed.EffectiveMode);
    }

    private static AiUseCaseVersion Version(
        Guid id,
        int number,
        AiUseCaseVersionStatus status,
        ReviewStatus technical,
        ReviewStatus subject) => new(
            id,
            UseCaseId,
            number,
            $"sha256:prompt-{number}",
            "local-default",
            status,
            technical,
            subject,
            ContentLoggingMode.MetadataOnly,
            Now,
            status == AiUseCaseVersionStatus.Active ? Now : null);
}
