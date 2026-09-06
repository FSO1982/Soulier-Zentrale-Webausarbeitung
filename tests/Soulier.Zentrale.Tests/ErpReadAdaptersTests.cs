using Soulier.Zentrale.Application;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Tests;

public sealed class ErpReadAdaptersTests
{
    private static readonly RequestContext Context = new(
        "erp-test-correlation",
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "TEST",
        "erp.read");

    [Fact]
    public async Task Mock_reader_returns_seeded_customer_and_order_case_insensitively()
    {
        var reader = new MockErpReader(
            [new ErpCustomer("K-100", "Musterkunde")],
            [new ErpOrder("A-200", "Testauftrag", "K-100")]);

        var customer = await reader.GetCustomerAsync("k-100", Context, CancellationToken.None);
        var order = await reader.GetOrderAsync("a-200", Context, CancellationToken.None);

        Assert.Equal("Musterkunde", customer?.DisplayName);
        Assert.Equal("Testauftrag", order?.DisplayName);
        Assert.Equal("K-100", order?.CustomerReference);
    }

    [Fact]
    public async Task Mock_reader_returns_null_for_unknown_references()
    {
        var reader = new MockErpReader();

        Assert.Null(await reader.GetCustomerAsync("K-404", Context, CancellationToken.None));
        Assert.Null(await reader.GetOrderAsync("A-404", Context, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Mock_reader_rejects_blank_references(string reference)
    {
        var reader = new MockErpReader();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            reader.GetCustomerAsync(reference, Context, CancellationToken.None));
    }

    [Fact]
    public async Task Mock_reader_honors_cancellation_before_access()
    {
        var reader = new MockErpReader();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            reader.GetOrderAsync("A-200", Context, cts.Token));
    }

    [Fact]
    public async Task Inform_skeleton_fails_closed_without_live_configuration()
    {
        var reader = new InformReadOnlyAdapterSkeleton();

        var exception = await Assert.ThrowsAsync<ErpDependencyNotConfiguredException>(() =>
            reader.GetCustomerAsync("K-100", Context, CancellationToken.None));

        Assert.Equal("INFORM_NOT_CONFIGURED", exception.ReasonCode);
        Assert.Contains("intentionally not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
