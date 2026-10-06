using Plemu.Core.Abstractions;
using Plemu.Core.Models;

namespace Plemu.Runtime.Mocks;

public class MockWalletService : IWalletService
{
    public WalletConnectionState State { get; private set; } = WalletConnectionState.Connected;
    public WalletIdentity? CurrentWallet { get; private set; } = new("11111111111111111111111111111111", "Mock Dev Wallet");

    public event Action<WalletConnectionState, WalletIdentity?>? StateChanged;

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        State = WalletConnectionState.Connected;
        CurrentWallet = new("11111111111111111111111111111111", "Mock Dev Wallet");
        StateChanged?.Invoke(State, CurrentWallet);
        return Task.FromResult(true);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = WalletConnectionState.Disconnected;
        CurrentWallet = null;
        StateChanged?.Invoke(State, null);
        return Task.CompletedTask;
    }

    public Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>(new byte[64]);
    }

    public Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>(new byte[64]);
    }

    public Task<ulong> GetLamportsBalanceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(2_000_000_000UL); // 2 SOL default mock
    }

    public Task<ulong> GetLamportsBalanceAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(2_000_000_000UL);
    }

    public Task<decimal> GetTokenBalanceAsync(string tokenMintAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenMintAddress);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(100.0m); // 100 USDC default mock
    }

    public Task<decimal> GetTokenBalanceAsync(string walletAddress, string tokenMintAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenMintAddress);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(100.0m);
    }
}
