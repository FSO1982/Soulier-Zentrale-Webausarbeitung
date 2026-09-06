using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class RetentionPolicyTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_retention_rule_never_triggers_automatic_deletion()
    {
        var decision = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.AuditEvent,
            Created,
            Created.AddYears(20),
            LegalHoldActive: false,
            []));

        Assert.False(decision.Delete);
        Assert.Equal("RETENTION_UNDEFINED", decision.ReasonCode);
    }

    [Fact]
    public void Explicit_disabled_deletion_never_deletes()
    {
        var rule = new RetentionRule(
            RetentionDataCategory.DocumentVersion,
            TimeSpan.FromDays(30),
            DeletionEnabled: false,
            LegalHoldSupported: true);

        var decision = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.DocumentVersion,
            Created,
            Created.AddYears(1),
            LegalHoldActive: false,
            [rule]));

        Assert.False(decision.Delete);
        Assert.Equal("RETENTION_DELETION_DISABLED", decision.ReasonCode);
    }

    [Fact]
    public void Configured_period_allows_deletion_only_after_expiry()
    {
        var rule = new RetentionRule(
            RetentionDataCategory.TechnicalLog,
            TimeSpan.FromDays(14),
            DeletionEnabled: true,
            LegalHoldSupported: false);

        var before = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.TechnicalLog,
            Created,
            Created.AddDays(13),
            LegalHoldActive: false,
            [rule]));
        var after = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.TechnicalLog,
            Created,
            Created.AddDays(15),
            LegalHoldActive: false,
            [rule]));

        Assert.False(before.Delete);
        Assert.Equal("RETENTION_ACTIVE", before.ReasonCode);
        Assert.True(after.Delete);
        Assert.Equal("RETENTION_EXPIRED", after.ReasonCode);
    }

    [Fact]
    public void Legal_hold_blocks_deletion_after_expiry()
    {
        var rule = new RetentionRule(
            RetentionDataCategory.AiContent,
            TimeSpan.FromDays(1),
            DeletionEnabled: true,
            LegalHoldSupported: true);

        var decision = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.AiContent,
            Created,
            Created.AddDays(10),
            LegalHoldActive: true,
            [rule]));

        Assert.False(decision.Delete);
        Assert.Equal("LEGAL_HOLD", decision.ReasonCode);
    }

    [Fact]
    public void Conflicting_duplicate_rules_fail_closed()
    {
        var rules = new[]
        {
            new RetentionRule(RetentionDataCategory.Backup, TimeSpan.FromDays(7), true, false),
            new RetentionRule(RetentionDataCategory.Backup, TimeSpan.FromDays(30), true, false)
        };

        var decision = RetentionPolicy.Evaluate(new RetentionEvaluationRequest(
            RetentionDataCategory.Backup,
            Created,
            Created.AddYears(1),
            LegalHoldActive: false,
            rules));

        Assert.False(decision.Delete);
        Assert.Equal("RETENTION_UNDEFINED", decision.ReasonCode);
    }
}
