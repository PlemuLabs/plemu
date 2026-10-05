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
}
