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

        public byte[]? SignatureToReturn { get; set; } = new byte[64];
        public Exception? ExceptionToThrowOnSignMessage { get; set; }
        public Exception? ExceptionToThrowOnSignTransaction { get; set; }

        public Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ExceptionToThrowOnSignMessage != null)
            {
                throw ExceptionToThrowOnSignMessage;
            }

            return Task.FromResult(SignatureToReturn);
        }

        public Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ExceptionToThrowOnSignTransaction != null)
            {
                throw ExceptionToThrowOnSignTransaction;
            }

            return Task.FromResult(SignatureToReturn);
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

    private class FakeRpcClient : ISolanaRpcClient, IDisposable
    {
        public ulong LamportsToReturn { get; set; } = 5_000_000UL;
        public decimal TokenBalanceToReturn { get; set; } = 25.5m;
        public Exception? ExceptionToThrow { get; set; }
        public string? LastRequestedPubkey { get; private set; }
        public string? LastRequestedTokenMint { get; private set; }
        public bool IsDisposed { get; private set; }

        public Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult<SolanaAccountInfo?>(null);
        }

        public Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult<IReadOnlyList<SolanaAccountInfo?>?>(null);
        }

        public Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedPubkey = pubkey;
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(LamportsToReturn);
        }

        public Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedPubkey = walletPubkey;
            LastRequestedTokenMint = tokenMint;
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            return Task.FromResult(TokenBalanceToReturn);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Fact]
    public async Task SignMessageAsync_WhenMessageIsNull_ThrowsArgumentNullException()
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SignMessageAsync(null!));
    }

    [Theory]
    [InlineData(WalletConnectionState.Disconnected)]
    public async Task SignMessageAsync_WhenDisconnected_ReturnsNull(WalletConnectionState expectedState)
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver());
        Assert.Equal(expectedState, service.State);

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 });
        Assert.Null(signature);
    }

    [Fact]
    public async Task SignMessageAsync_WhenConnectedAndDriverReturns64ByteSignature_ReturnsSignature()
    {
        var driver = new FakeWalletDriver();
        var expectedSig = new byte[64];
        expectedSig[0] = 0xAA;
        expectedSig[63] = 0xBB;
        driver.SignatureToReturn = expectedSig;

        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 });

        Assert.NotNull(signature);
        Assert.Equal(64, signature.Length);
        Assert.Equal(0xAA, signature[0]);
        Assert.Equal(0xBB, signature[63]);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(65)]
    public async Task SignMessageAsync_WhenDriverReturnsInvalidLength_ReturnsNullAndKeepsConnected(int length)
    {
        var driver = new FakeWalletDriver { SignatureToReturn = new byte[length] };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignMessageAsync_WhenDriverReturnsNull_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver { SignatureToReturn = null };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignMessageAsync_WhenCancelled_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 }, cts.Token);

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignMessageAsync_WhenDriverThrowsUnexpectedException_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver
        {
            ExceptionToThrowOnSignMessage = new InvalidOperationException("Hardware malfunction")
        };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignMessageAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenTransactionIsNull_ThrowsArgumentNullException()
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SignTransactionAsync(null!));
    }

    [Fact]
    public async Task SignTransactionAsync_WhenDisconnected_ReturnsNull()
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver());
        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 });
        Assert.Null(signature);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenConnectedAndDriverReturns64ByteSignature_ReturnsSignature()
    {
        var driver = new FakeWalletDriver();
        var expectedSig = new byte[64];
        expectedSig[0] = 0xDE;
        expectedSig[63] = 0xAD;
        driver.SignatureToReturn = expectedSig;

        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 });

        Assert.NotNull(signature);
        Assert.Equal(64, signature.Length);
        Assert.Equal(0xDE, signature[0]);
        Assert.Equal(0xAD, signature[63]);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(65)]
    public async Task SignTransactionAsync_WhenDriverReturnsInvalidLength_ReturnsNullAndKeepsConnected(int length)
    {
        var driver = new FakeWalletDriver { SignatureToReturn = new byte[length] };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenDriverReturnsNull_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver { SignatureToReturn = null };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenCancelled_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver();
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 }, cts.Token);

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenDriverThrowsUnexpectedException_ReturnsNullAndKeepsConnected()
    {
        var driver = new FakeWalletDriver
        {
            ExceptionToThrowOnSignTransaction = new InvalidOperationException("Hardware error")
        };
        var service = new PlaySolanaWalletService(driver);
        await service.ConnectAsync();

        var signature = await service.SignTransactionAsync(new byte[] { 1, 2, 3 });

        Assert.Null(signature);
        Assert.Equal(WalletConnectionState.Connected, service.State);
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithoutAddress_WhenDisconnected_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        ulong balance = await service.GetLamportsBalanceAsync();

        Assert.Equal(0UL, balance);
        Assert.Null(rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithoutAddress_WhenConnected_CallsRpcAndReturnsBalance()
    {
        var driver = new FakeWalletDriver { PublicKeyToReturn = "7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V" };
        var rpc = new FakeRpcClient { LamportsToReturn = 10_000_000UL };
        var service = new PlaySolanaWalletService(driver, rpc);

        await service.ConnectAsync();
        ulong balance = await service.GetLamportsBalanceAsync();

        Assert.Equal(10_000_000UL, balance);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", rpc.LastRequestedPubkey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetLamportsBalanceAsync_WithAddress_NullOrWhitespace_ThrowsArgumentException(string? address)
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), new FakeRpcClient());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLamportsBalanceAsync(address!));
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithAddress_InvalidBase58_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        ulong balance = await service.GetLamportsBalanceAsync("InvalidBase58AddressString");

        Assert.Equal(0UL, balance);
        Assert.Null(rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithAddress_ValidAddress_ReturnsLamports()
    {
        var rpc = new FakeRpcClient { LamportsToReturn = 5_000_000UL };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        ulong balance = await service.GetLamportsBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");

        Assert.Equal(5_000_000UL, balance);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithAddress_WhenRpcThrows_ReturnsZero()
    {
        var rpc = new FakeRpcClient { ExceptionToThrow = new InvalidOperationException("RPC connection lost") };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        ulong balance = await service.GetLamportsBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");

        Assert.Equal(0UL, balance);
    }

    [Fact]
    public async Task GetLamportsBalanceAsync_WithAddress_WhenCancelled_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        ulong balance = await service.GetLamportsBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", cts.Token);

        Assert.Equal(0UL, balance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTokenBalanceAsync_WithoutAddress_NullOrWhitespaceMint_ThrowsArgumentException(string? mint)
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), new FakeRpcClient());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTokenBalanceAsync(mint!));
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithoutAddress_WhenDisconnected_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        decimal balance = await service.GetTokenBalanceAsync("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        Assert.Equal(0.0m, balance);
        Assert.Null(rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithoutAddress_WhenConnected_CallsRpcAndReturnsDecimal()
    {
        var driver = new FakeWalletDriver { PublicKeyToReturn = "7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V" };
        var rpc = new FakeRpcClient { TokenBalanceToReturn = 125.75m };
        var service = new PlaySolanaWalletService(driver, rpc);

        await service.ConnectAsync();
        decimal balance = await service.GetTokenBalanceAsync("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        Assert.Equal(125.75m, balance);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", rpc.LastRequestedPubkey);
        Assert.Equal("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", rpc.LastRequestedTokenMint);
    }

    [Theory]
    [InlineData(null, "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")]
    [InlineData("", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")]
    [InlineData("   ", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v")]
    [InlineData("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", null)]
    [InlineData("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "")]
    [InlineData("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "   ")]
    public async Task GetTokenBalanceAsync_WithAddress_NullOrWhitespace_ThrowsArgumentException(string? wallet, string? mint)
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), new FakeRpcClient());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTokenBalanceAsync(wallet!, mint!));
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithAddress_InvalidWalletAddress_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        decimal balance = await service.GetTokenBalanceAsync("InvalidAddress", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        Assert.Equal(0.0m, balance);
        Assert.Null(rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithAddress_InvalidMintAddress_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        decimal balance = await service.GetTokenBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "InvalidMintAddress");

        Assert.Equal(0.0m, balance);
        Assert.Null(rpc.LastRequestedPubkey);
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithAddress_ValidAddresses_ReturnsDecimal()
    {
        var rpc = new FakeRpcClient { TokenBalanceToReturn = 50.0m };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        decimal balance = await service.GetTokenBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        Assert.Equal(50.0m, balance);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", rpc.LastRequestedPubkey);
        Assert.Equal("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", rpc.LastRequestedTokenMint);
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithAddress_WhenRpcThrows_ReturnsZero()
    {
        var rpc = new FakeRpcClient { ExceptionToThrow = new InvalidOperationException("Network failure") };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        decimal balance = await service.GetTokenBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");

        Assert.Equal(0.0m, balance);
    }

    [Fact]
    public async Task GetTokenBalanceAsync_WithAddress_WhenCancelled_ReturnsZero()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        decimal balance = await service.GetTokenBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v", cts.Token);

        Assert.Equal(0.0m, balance);
    }

    [Theory]
    [InlineData(0UL, false)]
    [InlineData(1_999_999UL, false)]
    [InlineData(2_000_000UL, true)]
    [InlineData(5_000_000UL, true)]
    public void HasMinimumSolForUsdcPayment_ValidatesTwoMillionLamportsThreshold(ulong lamports, bool expected)
    {
        Assert.Equal(2_000_000UL, PlaySolanaWalletService.MinimumSolBalanceForUsdcPaymentLamports);
        Assert.Equal(0.002m, PlaySolanaWalletService.MinimumSolBalanceForUsdcPayment);
        Assert.Equal(expected, PlaySolanaWalletService.HasMinimumSolForUsdcPayment(lamports));
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WhenDisconnected_ReturnsFalse()
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), new FakeRpcClient());
        bool isValid = await service.ValidateMinimumSolForUsdcAsync();
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WhenConnectedAndBelowThreshold_ReturnsFalse()
    {
        var driver = new FakeWalletDriver();
        var rpc = new FakeRpcClient { LamportsToReturn = 1_500_000UL };
        var service = new PlaySolanaWalletService(driver, rpc);

        await service.ConnectAsync();
        bool isValid = await service.ValidateMinimumSolForUsdcAsync();

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WhenConnectedAndAtOrAboveThreshold_ReturnsTrue()
    {
        var driver = new FakeWalletDriver();
        var rpc = new FakeRpcClient { LamportsToReturn = 2_000_000UL };
        var service = new PlaySolanaWalletService(driver, rpc);

        await service.ConnectAsync();
        bool isValid = await service.ValidateMinimumSolForUsdcAsync();

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WithExplicitAddress_WhenValidAndAboveThreshold_ReturnsTrue()
    {
        var rpc = new FakeRpcClient { LamportsToReturn = 5_000_000UL };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        bool isValid = await service.ValidateMinimumSolForUsdcAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WithExplicitAddress_WhenValidAndBelowThreshold_ReturnsFalse()
    {
        var rpc = new FakeRpcClient { LamportsToReturn = 1_999_999UL };
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        bool isValid = await service.ValidateMinimumSolForUsdcAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateMinimumSolForUsdcAsync_WithExplicitAddress_WhenInvalidAddress_ReturnsFalse()
    {
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), new FakeRpcClient());
        bool isValid = await service.ValidateMinimumSolForUsdcAsync("InvalidAddress");
        Assert.False(isValid);
    }

    [Fact]
    public void Constructor_DefaultParameters_InitializesCorrectly()
    {
        using var service = new PlaySolanaWalletService();
        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
    }

    [Fact]
    public void Dispose_DisposesOwnedRpcClient()
    {
        var rpc = new FakeRpcClient();
        var service = new PlaySolanaWalletService(new FakeWalletDriver(), rpc);

        // When RPC is injected externally, service should not dispose it
        service.Dispose();
        Assert.False(rpc.IsDisposed);

        // Default constructor creates owned RPC client, dispose should succeed without exception
        var defaultService = new PlaySolanaWalletService();
        defaultService.Dispose();
        defaultService.Dispose(); // Multiple dispose is safe
    }
}
