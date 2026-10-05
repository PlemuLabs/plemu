using Microsoft.Extensions.DependencyInjection;
using Plemu.App;
using Plemu.Core.Abstractions;
using Plemu.Solana;
using Xunit;

namespace Plemu.Solana.Tests;

public class CompositionRootTests
{
    [Fact]
    public void ConfigureServices_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CompositionRoot.ConfigureServices(null!));
    }

    [Fact]
    public void ConfigureServices_RegistersDev2Services_AsExpectedSingletons()
    {
        var services = new ServiceCollection();
        var returnedServices = CompositionRoot.ConfigureServices(services);

        Assert.Same(services, returnedServices);

        using var provider = services.BuildServiceProvider();

        var walletService = provider.GetService<IWalletService>();
        Assert.NotNull(walletService);
        Assert.IsType<PlaySolanaWalletService>(walletService);

        var entitlementService = provider.GetService<IEntitlementService>();
        Assert.NotNull(entitlementService);
        Assert.IsType<SolanaEntitlementService>(entitlementService);

        // Verify singleton lifetime (subsequent resolutions return identical references)
        var walletServiceSecondResolve = provider.GetRequiredService<IWalletService>();
        Assert.Same(walletService, walletServiceSecondResolve);

        var entitlementServiceSecondResolve = provider.GetRequiredService<IEntitlementService>();
        Assert.Same(entitlementService, entitlementServiceSecondResolve);

        // Verify PackLaunchOrchestrator resolves and injects the Dev 2 singletons seamlessly
        var orchestrator = provider.GetService<Plemu.Runtime.PackLaunchOrchestrator>();
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Program_Main_ExecutesSuccessfully()
    {
        var exception = Record.Exception(() => Program.Main(Array.Empty<string>()));
        Assert.Null(exception);
    }
}
