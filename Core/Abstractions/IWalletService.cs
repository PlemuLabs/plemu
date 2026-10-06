using Plemu.Core.Models;

namespace Plemu.Core.Abstractions;

public interface IWalletService
{
    WalletConnectionState State { get; }
    WalletIdentity? CurrentWallet { get; }
    event Action<WalletConnectionState, WalletIdentity?>? StateChanged;
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default);
    Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);

    Task<ulong> GetLamportsBalanceAsync(CancellationToken cancellationToken = default);
    Task<ulong> GetLamportsBalanceAsync(string walletAddress, CancellationToken cancellationToken = default);
    Task<decimal> GetTokenBalanceAsync(string tokenMintAddress, CancellationToken cancellationToken = default);
    Task<decimal> GetTokenBalanceAsync(string walletAddress, string tokenMintAddress, CancellationToken cancellationToken = default);
}
