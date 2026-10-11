using Plemu.Core.Models;

namespace Plemu.Core.Abstractions;

public interface IEntitlementService
{
    Task<bool> CanAccessPackAsync(WalletIdentity wallet, string packId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessPack(WalletIdentity wallet, string packId) =>
        CanAccessPackAsync(wallet, packId, CancellationToken.None);

    Task<bool> CanAccessEntryAsync(WalletIdentity wallet, string entrySha256, CancellationToken cancellationToken = default);

    Task<bool> CanAccessEntry(WalletIdentity wallet, string entrySha256) =>
        CanAccessEntryAsync(wallet, entrySha256, CancellationToken.None);
}
