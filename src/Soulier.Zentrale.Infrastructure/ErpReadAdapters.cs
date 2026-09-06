using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

public sealed class MockErpReader : IErpReader
{
    private readonly IReadOnlyDictionary<string, ErpCustomer> _customers;
    private readonly IReadOnlyDictionary<string, ErpOrder> _orders;

    public MockErpReader(
        IEnumerable<ErpCustomer>? customers = null,
        IEnumerable<ErpOrder>? orders = null)
    {
        _customers = (customers ?? [])
            .ToDictionary(x => x.Reference, StringComparer.OrdinalIgnoreCase);
        _orders = (orders ?? [])
            .ToDictionary(x => x.Reference, StringComparer.OrdinalIgnoreCase);
    }

    public Task<ErpCustomer?> GetCustomerAsync(
        string customerRef,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReference(customerRef, nameof(customerRef));
        _customers.TryGetValue(customerRef, out var customer);
        return Task.FromResult(customer);
    }

    public Task<ErpOrder?> GetOrderAsync(
        string orderRef,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReference(orderRef, nameof(orderRef));
        _orders.TryGetValue(orderRef, out var order);
        return Task.FromResult(order);
    }

    private static void ValidateReference(string reference, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("ERP reference is required.", parameterName);
    }
}

public sealed class ErpDependencyNotConfiguredException : InvalidOperationException
{
    public string ReasonCode { get; }

    public ErpDependencyNotConfiguredException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }
}

public sealed class InformReadOnlyAdapterSkeleton : IErpReader
{
    public Task<ErpCustomer?> GetCustomerAsync(
        string customerRef,
        RequestContext context,
        CancellationToken cancellationToken) =>
        NotConfigured<ErpCustomer?>();

    public Task<ErpOrder?> GetOrderAsync(
        string orderRef,
        RequestContext context,
        CancellationToken cancellationToken) =>
        NotConfigured<ErpOrder?>();

    private static Task<T> NotConfigured<T>() =>
        Task.FromException<T>(new ErpDependencyNotConfiguredException(
            "INFORM_NOT_CONFIGURED",
            "IN-FORM live access is intentionally not configured in V1. The adapter remains a read-only skeleton until manufacturer/interface clarification and explicit activation."));
}
