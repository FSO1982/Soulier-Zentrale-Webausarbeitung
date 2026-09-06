using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Tests;

public sealed class AuditedCapabilityAuthorizerTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Allow_is_not_returned_until_audit_event_is_written()
    {
        var writer = new RecordingAuditWriter();
        var service = new AuditedCapabilityAuthorizer(writer);

        var result = await service.AuthorizeAsync(
            Request(PolicyDecision.Allow),
            AuditContext(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Allowed);
        var auditEvent = Assert.Single(writer.Events);
        Assert.Equal("ALLOW", auditEvent.Result);
        Assert.Equal("ALLOW", auditEvent.ReasonCode);
        Assert.Equal(ClientId, auditEvent.ClientId);
        Assert.Equal("knowledge.read", auditEvent.CapabilityKey);
        Assert.Equal("corr-audit-001", auditEvent.CorrelationId);
        Assert.Equal("sha256:test", auditEvent.ContentHash);
    }

    [Fact]
    public async Task Denial_is_audited_with_reason_code()
    {
        var writer = new RecordingAuditWriter();
        var service = new AuditedCapabilityAuthorizer(writer);

        var result = await service.AuthorizeAsync(
            Request(PolicyDecision.Deny),
            AuditContext(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Allowed);
        var auditEvent = Assert.Single(writer.Events);
        Assert.Equal("DENY", auditEvent.Result);
        Assert.Equal("POLICY_DENIED", auditEvent.ReasonCode);
    }

    [Fact]
    public async Task Audit_writer_failure_propagates_instead_of_returning_unaudited_result()
    {
        var service = new AuditedCapabilityAuthorizer(new ThrowingAuditWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AuthorizeAsync(
            Request(PolicyDecision.Allow),
            AuditContext(),
            TestContext.Current.CancellationToken));
    }

    private static AuthorizationRequest Request(PolicyDecision policyDecision)
    {
        var client = new Client(ClientId, "codex-pilot", "TEST", ClientStatus.Active);
        var capability = new Capability("knowledge.read", 1, true);
        var grant = new Grant(
            ClientId,
            "knowledge.read",
            "soulier:test",
            "TEST",
            GrantStatus.Active,
            Now.AddMinutes(-1),
            Now.AddHours(1));

        return new AuthorizationRequest(
            client,
            capability,
            [grant],
            "soulier:test",
            policyDecision,
            Now);
    }

    private static AuthorizationAuditContext AuditContext() =>
        new(
            "corr-audit-001",
            null,
            null,
            "document_version",
            "document-version:33333333-3333-3333-3333-333333333333",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "sha256:test",
            "policy:test",
            null,
            "knowledge");

    private sealed class RecordingAuditWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("audit unavailable"));
    }
}
