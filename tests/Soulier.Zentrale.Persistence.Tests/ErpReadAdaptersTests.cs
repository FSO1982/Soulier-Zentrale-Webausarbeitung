using Soulier.Zentrale.Application;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Persistence.Tests;

public sealed class ErpReadAdaptersTests
{
    private static readonly RequestContext Context = new(
        "corr-erp-test",
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "TEST",
        "inform.order.read");

    [Fact]
    public async Task Mock_reader_returns_only_seeded_read_models()
    {
        var reader = new MockErpReader(
            customers:
            [
                new ErpCustomer("K-100", "Testkunde")
            ],
            orders:
            [
                new ErpOrder("A-200", "Testauftrag", "K-100")
            ]);

        var customer = await reader.GetCustomerAsync(
            "K-100",
            Context,
            TestContext.Current.CancellationToken);
        var order = await reader.GetOrderAsync(
            "A-200",
            Context,
            TestContext.Current.CancellationToken);
        var missing = await reader.GetOrderAsync(
            "A-999",
            Context,
            TestContext.Current.CancellationToken);

        Assert.Equal("Testkunde", customer?.DisplayName);
        Assert.Equal("K-100", order?.CustomerReference);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Inform_skeleton_never_fabricates_live_erp_data()
    {
        var reader = new InformReadOnlyAdapterSkeleton();

        var exception = await Assert.ThrowsAsync<ErpDependencyNotConfiguredException>(() =>
            reader.GetOrderAsync(
                "A-200",
                Context,
                TestContext.Current.CancellationToken));

        Assert.Equal("INFORM_NOT_CONFIGURED", exception.ReasonCode);
    }
}
