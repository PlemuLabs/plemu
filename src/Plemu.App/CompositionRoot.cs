using Microsoft.Extensions.DependencyInjection;
using Plemu.Core.Abstractions;
using Plemu.Runtime;
using Plemu.Solana;

namespace Plemu.App;

public static class CompositionRoot
{
    public static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registros do Dev 1 (Runtime & Orchestration):
        services.AddSingleton<PackLaunchOrchestrator>();

        // Registros do Dev 2:
        services.AddSingleton<IWalletService, PlaySolanaWalletService>();
        services.AddSingleton<IEntitlementService, SolanaEntitlementService>();

        // Registros do Dev 3 (Packs & Manifests):
        // services.AddSingleton<...>();

        return services;
    }
}
