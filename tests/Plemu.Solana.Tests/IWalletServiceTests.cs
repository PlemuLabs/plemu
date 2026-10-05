using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Xunit;

namespace Plemu.Solana.Tests;

public class IWalletServiceTests
{
    private class DummyWalletService : IWalletService
    {
        public WalletConnectionState State { get; private set; } = WalletConnectionState.Disconnected;
        public WalletIdentity? CurrentWallet { get; private set; }
        public event Action<WalletConnectionState, WalletIdentity?>? StateChanged;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            State = WalletConnectionState.Connected;
            CurrentWallet = new WalletIdentity("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");
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

    [Fact]
    public async Task IWalletService_StateAndEvents_CanBeObserved()
    {
        IWalletService service = new DummyWalletService();
        WalletConnectionState? observedState = null;
        WalletIdentity? observedIdentity = null;

        service.StateChanged += (state, identity) =>
        {
            observedState = state;
            observedIdentity = identity;
        };

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);

        bool connected = await service.ConnectAsync();

        Assert.True(connected);
        Assert.Equal(WalletConnectionState.Connected, service.State);
        Assert.NotNull(service.CurrentWallet);
        Assert.Equal(WalletConnectionState.Connected, observedState);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", observedIdentity?.PublicKey);

        await service.DisconnectAsync();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
        Assert.Equal(WalletConnectionState.Disconnected, observedState);
        Assert.Null(observedIdentity);
    }
}
