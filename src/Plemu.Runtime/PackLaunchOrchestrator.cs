using Plemu.Core.Abstractions;
using Plemu.Core.Models;

namespace Plemu.Runtime;

public class PackLaunchOrchestrator
{
    private readonly IWalletService _walletService;
    private readonly IEntitlementService _entitlementService;

    public PackLaunchOrchestrator(IWalletService walletService, IEntitlementService entitlementService)
    {
        _walletService = walletService ?? throw new ArgumentNullException(nameof(walletService));
        _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
    }

    public async Task<bool> CanLaunchAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packId))
        {
            return false;
        }

        if (_walletService.State != WalletConnectionState.Connected || _walletService.CurrentWallet == null)
        {
            return false;
        }

        return await _entitlementService.CanAccessPack(_walletService.CurrentWallet, packId);
    }
}
