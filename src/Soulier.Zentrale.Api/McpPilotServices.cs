using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Api;

public sealed class PilotMcpProfile
{
    public static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DocumentVersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public const string Environment = "TEST";
    public const string ResourceScope = "soulier:pilot";
    public const string ContentHash = "sha256:gate3-mcp-pilot";

    public Client Client { get; } = new(ClientId, "codex-pilot", Environment, ClientStatus.Active);

    public IReadOnlyCollection<Grant> Grants { get; } =
    [
        new(ClientId, "knowledge.search", ResourceScope, Environment, GrantStatus.Active, DateTimeOffset.UnixEpoch, null),
        new(ClientId, "knowledge.read", ResourceScope, Environment, GrantStatus.Active, DateTimeOffset.UnixEpoch, null)
    ];
}

public sealed class InMemoryPilotAuditWriter : IAuditEventWriter
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => [.. _events];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events.Enqueue(auditEvent);
        return Task.CompletedTask;
    }
}

public sealed class HealthyPilotKnowledgeDependencyStatusProvider : IKnowledgeDependencyStatusProvider
{
    public KnowledgeDependencyStatus GetStatus() => new(
        KnowledgeDependencyState.Healthy,
        DateTimeOffset.UtcNow,
        "Gate-3 pilot dependency is an in-process test source.");
}

public sealed class PilotKnowledgeReader : IKnowledgeReader
{
    private static readonly KnowledgeSearchHit PilotHit = new(
        PilotMcpProfile.DocumentId,
        PilotMcpProfile.DocumentVersionId,
        "Gate-3-Testwissen.md",
        "Freigegebener Testinhalt für den MCP-Transportnachweis.",
        PilotMcpProfile.ContentHash,
        Soulier.Zentrale.Application.DataClassification.Internal,
        Soulier.Zentrale.Application.AiPolicy.LocalOnly,
        new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero));

    public Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(
        KnowledgeSearchRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<KnowledgeSearchHit> result =
            string.Equals(request.ResourceScope, PilotMcpProfile.ResourceScope, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(request.Query)
                ? [PilotHit]
                : [];

        return Task.FromResult(result);
    }

    public Task<string?> ReadAsync(
        Guid documentVersionId,
        int maxChars,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string content = "Soulier-Zentrale MCP Pilot: Der kontrollierte Wissenszugriff ist erreichbar. Dieser Inhalt ist ausschließlich Testdatenbestand und keine produktive Wissensquelle.";
        string? result = documentVersionId == PilotMcpProfile.DocumentVersionId
            ? content[..Math.Min(content.Length, Math.Clamp(maxChars, 1, 8_000))]
            : null;

        return Task.FromResult(result);
    }
}

[McpServerToolType]
public sealed class SoulierKnowledgeTools(
    IKnowledgeReader knowledgeReader,
    AuditedCapabilityAuthorizer authorizer,
    AuditedKnowledgeDependencyGuard dependencyGuard,
    PilotMcpProfile profile)
{
    [McpServerTool(Name = "knowledge_search"), Description("Searches only the explicitly granted Soulier pilot knowledge scope. Internal capability: knowledge.search.")]
    public async Task<string> Search(
        [Description("Search query.")] string query,
        [Description("Granted Soulier resource scope. For the Gate-3 pilot use soulier:pilot.")] string resourceScope = PilotMcpProfile.ResourceScope)
    {
        const string capabilityKey = "knowledge.search";
        var nowUtc = DateTimeOffset.UtcNow;
        var correlationId = $"mcp-{Guid.NewGuid():N}";
        var requestContext = new RequestContext(correlationId, profile.Client.Id, profile.Client.Environment, capabilityKey);
        var auditContext = new AuthorizationAuditContext(
            correlationId,
            null,
            null,
            "knowledge_scope",
            resourceScope,
            null,
            null,
            "mcp-pilot-local-only",
            null,
            "mcp");

        var authorization = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(
                profile.Client,
                new Capability(capabilityKey, 1, true),
                profile.Grants,
                resourceScope,
                PolicyDecision.Allow,
                nowUtc),
            auditContext);

        if (!authorization.Allowed)
            throw new McpException($"Access denied: {authorization.ReasonCode}");

        var dependency = await dependencyGuard.EvaluateAsync(requestContext, auditContext, nowUtc);
        if (!dependency.Allowed)
            throw new McpException($"Access denied: {dependency.ReasonCode}");

        var hits = await knowledgeReader.SearchAsync(
            new KnowledgeSearchRequest(query, resourceScope),
            requestContext,
            CancellationToken.None);

        return JsonSerializer.Serialize(hits);
    }

    [McpServerTool(Name = "knowledge_read"), Description("Reads only a document version reachable through the granted Soulier pilot scope. Internal capability: knowledge.read.")]
    public async Task<string> Read(
        [Description("Exact released document-version id returned by knowledge_search.")] Guid documentVersionId,
        [Description("Maximum number of characters to return, capped at 8000.")] int maxChars = 4_000,
        [Description("Granted Soulier resource scope. For the Gate-3 pilot use soulier:pilot.")] string resourceScope = PilotMcpProfile.ResourceScope)
    {
        const string capabilityKey = "knowledge.read";
        var nowUtc = DateTimeOffset.UtcNow;
        var correlationId = $"mcp-{Guid.NewGuid():N}";
        var requestContext = new RequestContext(correlationId, profile.Client.Id, profile.Client.Environment, capabilityKey);
        var auditContext = new AuthorizationAuditContext(
            correlationId,
            null,
            null,
            "document_version",
            documentVersionId.ToString("D"),
            documentVersionId,
            documentVersionId == PilotMcpProfile.DocumentVersionId ? PilotMcpProfile.ContentHash : null,
            "mcp-pilot-local-only",
            null,
            "mcp");

        var authorization = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(
                profile.Client,
                new Capability(capabilityKey, 1, true),
                profile.Grants,
                resourceScope,
                PolicyDecision.Allow,
                nowUtc),
            auditContext);

        if (!authorization.Allowed)
            throw new McpException($"Access denied: {authorization.ReasonCode}");

        var dependency = await dependencyGuard.EvaluateAsync(requestContext, auditContext, nowUtc);
        if (!dependency.Allowed)
            throw new McpException($"Access denied: {dependency.ReasonCode}");

        var content = await knowledgeReader.ReadAsync(
            documentVersionId,
            Math.Clamp(maxChars, 1, 8_000),
            requestContext,
            CancellationToken.None);

        return content ?? throw new McpException("RESOURCE_NOT_FOUND");
    }
}
