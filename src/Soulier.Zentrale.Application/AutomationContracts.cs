namespace Soulier.Zentrale.Application;

public enum AutomationRunState
{
    Accepted,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed record AutomationStartRequest(
    string ActionKey,
    string IdempotencyKey,
    string ResourceScope,
    string CorrelationId,
    IReadOnlyDictionary<string, string?> Parameters);

public sealed record AutomationStartResult(
    string RunReference,
    AutomationRunState State,
    string ReasonCode);

public sealed record AutomationRunStatus(
    string RunReference,
    AutomationRunState State,
    string? ReasonCode,
    DateTimeOffset ObservedAtUtc);

public interface IAutomationOrchestrator
{
    Task<AutomationStartResult> StartAsync(
        AutomationStartRequest request,
        CancellationToken cancellationToken = default);

    Task<AutomationRunStatus> GetStatusAsync(
        string runReference,
        CancellationToken cancellationToken = default);
}
