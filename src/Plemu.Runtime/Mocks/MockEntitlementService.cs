using Plemu.Core.Abstractions;
using Plemu.Core.Models;

namespace Plemu.Runtime.Mocks;

public class MockEntitlementService : IEntitlementService
{
    public Task<bool> CanAccessPackAsync(WalletIdentity wallet, string packId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(packId));
    }

    public Task<bool> CanAccessPack(WalletIdentity wallet, string packId) =>
        CanAccessPackAsync(wallet, packId, CancellationToken.None);
}
