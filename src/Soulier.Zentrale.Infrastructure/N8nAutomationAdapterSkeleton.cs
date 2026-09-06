using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

public sealed class AutomationDependencyNotConfiguredException : InvalidOperationException
{
    public string ReasonCode { get; }

    public AutomationDependencyNotConfiguredException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }
}

/// <summary>
/// V1 architecture boundary for n8n. It intentionally contains no database access and no
/// guessed n8n workflow URL. Productive activation requires a configured HTTP contract,
/// authentication, allowlisted workflow mapping and explicit infrastructure test.
/// </summary>
public sealed class N8nAutomationAdapterSkeleton : IAutomationOrchestrator
{
    public Task<AutomationStartResult> StartAsync(
        AutomationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStartRequest(request);
        return NotConfigured<AutomationStartResult>();
    }

    public Task<AutomationRunStatus> GetStatusAsync(
        string runReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(runReference) || runReference.Length > 200)
            throw new ArgumentException("Automation run reference must contain 1 to 200 characters.", nameof(runReference));

        return NotConfigured<AutomationRunStatus>();
    }

    private static void ValidateStartRequest(AutomationStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Parameters);

        if (string.IsNullOrWhiteSpace(request.ActionKey) || request.ActionKey.Length > 200)
            throw new ArgumentException("Action key must contain 1 to 200 characters.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            throw new ArgumentException("Idempotency key must contain 1 to 200 characters.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ResourceScope) || request.ResourceScope.Length > 500)
            throw new ArgumentException("Resource scope must contain 1 to 500 characters.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 128)
            throw new ArgumentException("Correlation id must contain 1 to 128 characters.", nameof(request));
        if (request.Parameters.Count > 100)
            throw new ArgumentException("Automation parameter count exceeds the allowed contract size.", nameof(request));
    }

    private static Task<T> NotConfigured<T>() =>
        Task.FromException<T>(new AutomationDependencyNotConfiguredException(
            "N8N_NOT_CONFIGURED",
            "n8n live orchestration is intentionally not configured in the Gate-3 skeleton. The Soulier API remains the fachliche boundary and n8n receives no direct database access."));
}
