using Plemu.Core.Models;

namespace Plemu.Core.Abstractions;

public interface IWalletService
{
    WalletConnectionState State { get; }
    WalletIdentity? CurrentWallet { get; }
    event Action<WalletConnectionState, WalletIdentity?>? StateChanged;
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
