using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Solana;
using Xunit;

namespace Plemu.Solana.Tests;

public class PlaySolanaWalletServiceTests
{
    private class FakeWalletDriver : IPlaySolanaWalletDriver
    {
        public string? PublicKeyToReturn { get; set; } = "7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V";
        public Exception? ExceptionToThrowOnConnect { get; set; }
        public Exception? ExceptionToThrowOnDisconnect { get; set; }
        public int ConnectCallCount { get; private set; }
        public int DisconnectCallCount { get; private set; }

        public Task<string?> RequestPublicKeyAsync(CancellationToken cancellationToken = default)
        {
            ConnectCallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrowOnConnect != null)
            {
                throw ExceptionToThrowOnConnect;
            }

            return Task.FromResult(PublicKeyToReturn);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrowOnDisconnect != null)
            {
                throw ExceptionToThrowOnDisconnect;
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_InitializesInDisconnectedStateWithNullWallet()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
    }

    [Fact]
    public async Task ConnectAsync_TransitionsThroughConnectingToConnectedAndFiresEvents()
    {
        var driver = new FakeWalletDriver
        {
            PublicKeyToReturn = "7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V"
        };
        var service = new PlaySolanaWalletService(driver);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        bool result = await service.ConnectAsync();

        Assert.True(result);
        Assert.Equal(WalletConnectionState.Connected, service.State);
        Assert.NotNull(service.CurrentWallet);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", service.CurrentWallet.PublicKey);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Connecting, transitions[0].State);
        Assert.Null(transitions[0].Identity);

        Assert.Equal(WalletConnectionState.Connected, transitions[1].State);
        Assert.NotNull(transitions[1].Identity);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", transitions[1].Identity!.PublicKey);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_IsIdempotentAndEmitsNoEvents()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);

        bool initialConnect = await service.ConnectAsync();
        Assert.True(initialConnect);
        Assert.Equal(1, driver.ConnectCallCount);

        int eventCount = 0;
        service.StateChanged += (_, _) => eventCount++;

        bool secondConnect = await service.ConnectAsync();

        Assert.True(secondConnect);
        Assert.Equal(WalletConnectionState.Connected, service.State);
        Assert.Equal(1, driver.ConnectCallCount);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task DisconnectAsync_TransitionsThroughDisconnectingToDisconnectedAndClearsWallet()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);

        await service.ConnectAsync();
        Assert.Equal(WalletConnectionState.Connected, service.State);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        await service.DisconnectAsync();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
        Assert.Equal(1, driver.DisconnectCallCount);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Disconnecting, transitions[0].State);
        Assert.NotNull(transitions[0].Identity);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", transitions[0].Identity!.PublicKey);

        Assert.Equal(WalletConnectionState.Disconnected, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }

    [Fact]
    public async Task DisconnectAsync_WhenAlreadyDisconnected_IsIdempotentAndEmitsNoEvents()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);

        int eventCount = 0;
        service.StateChanged += (_, _) => eventCount++;

        await service.DisconnectAsync();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
        Assert.Equal(0, driver.DisconnectCallCount);
        Assert.Equal(0, eventCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConnectAsync_WhenDriverReturnsNullOrWhitespace_TransitionsToErrorAndReturnsFalse(string? key)
    {
        var driver = new FakeWalletDriver
        {
            PublicKeyToReturn = key
        };
        var service = new PlaySolanaWalletService(driver);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        bool result = await service.ConnectAsync();

        Assert.False(result);
        Assert.Equal(WalletConnectionState.Error, service.State);
        Assert.Null(service.CurrentWallet);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Connecting, transitions[0].State);
        Assert.Equal(WalletConnectionState.Error, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }

    [Theory]
    [InlineData("TooShortKey")]
    [InlineData("ThisPublicKeyIsWayTooLongToBeAValidSolanaAddressOnChainBecauseItExceedsFortyFourCharacters")]
    public async Task ConnectAsync_WhenDriverReturnsInvalidKeyLength_TransitionsToErrorAndReturnsFalse(string invalidKey)
    {
        var driver = new FakeWalletDriver
        {
            PublicKeyToReturn = invalidKey
        };
        var service = new PlaySolanaWalletService(driver);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        bool result = await service.ConnectAsync();

        Assert.False(result);
        Assert.Equal(WalletConnectionState.Error, service.State);
        Assert.Null(service.CurrentWallet);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Connecting, transitions[0].State);
        Assert.Equal(WalletConnectionState.Error, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }

    [Fact]
    public async Task ConnectAsync_WhenDriverThrowsException_CatchesGracefullyTransitionsToErrorAndReturnsFalse()
    {
        var driver = new FakeWalletDriver
        {
            ExceptionToThrowOnConnect = new InvalidOperationException("Hardware driver communication failed or user rejected")
        };
        var service = new PlaySolanaWalletService(driver);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        bool result = await service.ConnectAsync();

        Assert.False(result);
        Assert.Equal(WalletConnectionState.Error, service.State);
        Assert.Null(service.CurrentWallet);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Connecting, transitions[0].State);
        Assert.Equal(WalletConnectionState.Error, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelled_TransitionsToDisconnectedAndReturnsFalse()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        bool result = await service.ConnectAsync(cts.Token);

        Assert.False(result);
        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Connecting, transitions[0].State);
        Assert.Equal(WalletConnectionState.Disconnected, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }

    [Fact]
    public async Task DisconnectAsync_WhenDriverThrowsException_CatchesGracefullyAndTransitionsToDisconnected()
    {
        var driver = new FakeWalletDriver
        {
            ExceptionToThrowOnDisconnect = new InvalidOperationException("Hardware disconnect communication timeout")
        };
        var service = new PlaySolanaWalletService(driver);

        await service.ConnectAsync();
        Assert.Equal(WalletConnectionState.Connected, service.State);

        var transitions = new List<(WalletConnectionState State, WalletIdentity? Identity)>();
        service.StateChanged += (state, identity) => transitions.Add((state, identity));

        await service.DisconnectAsync();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(WalletConnectionState.Disconnecting, transitions[0].State);
        Assert.Equal(WalletConnectionState.Disconnected, transitions[1].State);
        Assert.Null(transitions[1].Identity);
    }
}
