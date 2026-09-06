using Soulier.Zentrale.Application;
using Soulier.Zentrale.Infrastructure;

namespace Soulier.Zentrale.Tests;

public sealed class N8nAutomationAdapterTests
{
    [Fact]
    public async Task Live_execution_is_fail_closed_until_n8n_contract_is_configured()
    {
        var adapter = new N8nAutomationAdapterSkeleton();
        var request = new AutomationStartRequest(
            "knowledge.refresh",
            "idem-001",
            "soulier:pilot",
            "corr-001",
            new Dictionary<string, string?> { ["source"] = "test" });

        var exception = await Assert.ThrowsAsync<AutomationDependencyNotConfiguredException>(async () =>
            await adapter.StartAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal("N8N_NOT_CONFIGURED", exception.ReasonCode);
    }

    [Fact]
    public async Task Invalid_start_contract_is_rejected_before_dependency_access()
    {
        var adapter = new N8nAutomationAdapterSkeleton();
        var request = new AutomationStartRequest(
            "knowledge.refresh",
            "",
            "soulier:pilot",
            "corr-001",
            new Dictionary<string, string?>());

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await adapter.StartAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Status_lookup_is_fail_closed_until_n8n_contract_is_configured()
    {
        var adapter = new N8nAutomationAdapterSkeleton();

        var exception = await Assert.ThrowsAsync<AutomationDependencyNotConfiguredException>(async () =>
            await adapter.GetStatusAsync("run-001", TestContext.Current.CancellationToken));

        Assert.Equal("N8N_NOT_CONFIGURED", exception.ReasonCode);
    }
}
