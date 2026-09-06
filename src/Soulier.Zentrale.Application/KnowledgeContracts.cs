namespace Soulier.Zentrale.Application;

public enum DataClassification { Public, Internal, Confidential, Restricted }
public enum AiPolicy { ExternalAllowed, LocalOnly, AiForbidden }

public sealed record RequestContext(
    string CorrelationId,
    Guid ClientId,
    string Environment,
    string CapabilityKey);

public sealed record KnowledgeSearchRequest(string Query, string ResourceScope, int MaxResults = 8);

public sealed record KnowledgeSearchHit(
    Guid DocumentId,
    Guid DocumentVersionId,
    string LogicalName,
    string? Snippet,
    string ContentHash,
    DataClassification DataClassification,
    AiPolicy AiPolicy,
    DateTimeOffset FreshnessTimestamp);

public interface IKnowledgeReader
{
    Task<IReadOnlyList<KnowledgeSearchHit>> SearchAsync(
        KnowledgeSearchRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<string?> ReadAsync(
        Guid documentVersionId,
        string resourceScope,
        int maxChars,
        RequestContext context,
        CancellationToken cancellationToken);
}

public interface IAuditWriter
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}

public sealed record AuditRecord(
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid? ClientId,
    string? CapabilityKey,
    string Result,
    string ReasonCode,
    long? DurationMs = null);

public interface IErpReader
{
    Task<ErpCustomer?> GetCustomerAsync(string customerRef, RequestContext context, CancellationToken cancellationToken);
    Task<ErpOrder?> GetOrderAsync(string orderRef, RequestContext context, CancellationToken cancellationToken);
}

public sealed record ErpCustomer(string Reference, string DisplayName);
public sealed record ErpOrder(string Reference, string DisplayName, string? CustomerReference);
