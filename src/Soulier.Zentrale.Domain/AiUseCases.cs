namespace Soulier.Zentrale.Domain;

public enum AiUseCaseVersionStatus
{
    Draft,
    Active,
    Retired
}

public enum ContentLoggingMode
{
    MetadataOnly,
    FullContent
}

public sealed record AiUseCase(
    Guid Id,
    string Key,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record AiUseCaseVersion(
    Guid Id,
    Guid AiUseCaseId,
    int VersionNumber,
    string PromptTemplateHash,
    string ModelRouteKey,
    AiUseCaseVersionStatus Status,
    ReviewStatus TechnicalReviewStatus,
    ReviewStatus SubjectReviewStatus,
    ContentLoggingMode ContentLoggingMode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ActivatedAtUtc)
{
    public bool IsEligibleForActivation =>
        Status == AiUseCaseVersionStatus.Draft &&
        TechnicalReviewStatus == ReviewStatus.Approved &&
        SubjectReviewStatus == ReviewStatus.Approved;
}

public sealed record AiUseCaseActivationPlan(
    Guid CandidateVersionId,
    IReadOnlyCollection<Guid> VersionsToRetire);

public sealed record AiUseCaseActivationDecision(
    bool Allowed,
    string ReasonCode,
    AiUseCaseActivationPlan? Plan)
{
    public static AiUseCaseActivationDecision Deny(string reasonCode) => new(false, reasonCode, null);
    public static AiUseCaseActivationDecision Allow(AiUseCaseActivationPlan plan) => new(true, "ALLOW", plan);
}

public static class AiUseCaseVersionPolicy
{
    public static AiUseCaseActivationDecision PlanActivation(
        AiUseCase useCase,
        AiUseCaseVersion candidate,
        IReadOnlyCollection<AiUseCaseVersion> existingVersions)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existingVersions);

        if (!useCase.IsActive)
            return AiUseCaseActivationDecision.Deny("USE_CASE_DISABLED");

        if (candidate.AiUseCaseId != useCase.Id)
            return AiUseCaseActivationDecision.Deny("USE_CASE_VERSION_MISMATCH");

        if (candidate.VersionNumber < 1 || string.IsNullOrWhiteSpace(candidate.PromptTemplateHash))
            return AiUseCaseActivationDecision.Deny("VERSION_INVALID");

        if (string.IsNullOrWhiteSpace(candidate.ModelRouteKey))
            return AiUseCaseActivationDecision.Deny("MODEL_ROUTE_REQUIRED");

        if (!candidate.IsEligibleForActivation)
            return AiUseCaseActivationDecision.Deny("USE_CASE_REVIEW_REQUIRED");

        if (existingVersions.Any(x =>
                x.Id != candidate.Id &&
                x.AiUseCaseId == candidate.AiUseCaseId &&
                x.VersionNumber == candidate.VersionNumber))
            return AiUseCaseActivationDecision.Deny("VERSION_CONFLICT");

        var activeVersions = existingVersions
            .Where(x =>
                x.Id != candidate.Id &&
                x.AiUseCaseId == candidate.AiUseCaseId &&
                x.Status == AiUseCaseVersionStatus.Active)
            .Select(x => x.Id)
            .ToArray();

        return AiUseCaseActivationDecision.Allow(
            new AiUseCaseActivationPlan(candidate.Id, activeVersions));
    }
}

public sealed record ContentLoggingRequest(
    ContentLoggingMode ConfiguredMode,
    bool ExplicitlyEnabled,
    DataClassification DataClassification,
    bool ContainsSecret,
    IReadOnlySet<DataClassification> FullContentAllowedClassifications);

public sealed record ContentLoggingDecision(
    ContentLoggingMode EffectiveMode,
    string ReasonCode);

public static class ContentLoggingPolicy
{
    public static ContentLoggingDecision Evaluate(ContentLoggingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FullContentAllowedClassifications);

        if (request.ContainsSecret)
            return new ContentLoggingDecision(ContentLoggingMode.MetadataOnly, "SECRET_CONTENT_BLOCKED");

        if (!request.ExplicitlyEnabled || request.ConfiguredMode == ContentLoggingMode.MetadataOnly)
            return new ContentLoggingDecision(ContentLoggingMode.MetadataOnly, "METADATA_ONLY_DEFAULT");

        return request.FullContentAllowedClassifications.Contains(request.DataClassification)
            ? new ContentLoggingDecision(ContentLoggingMode.FullContent, "FULL_CONTENT_ALLOWED")
            : new ContentLoggingDecision(ContentLoggingMode.MetadataOnly, "DATA_CLASS_NOT_ENABLED_FOR_FULL_CONTENT");
    }
}
