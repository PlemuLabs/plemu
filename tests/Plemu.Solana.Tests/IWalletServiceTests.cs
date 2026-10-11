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
            return Task.FromResult(1_500_000_000UL);
        }

        public Task<ulong> GetLamportsBalanceAsync(string walletAddress, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(1_500_000_000UL);
        }

        public Task<decimal> GetTokenBalanceAsync(string tokenMintAddress, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenMintAddress);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(25.50m);
        }

        public Task<decimal> GetTokenBalanceAsync(string walletAddress, string tokenMintAddress, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(walletAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenMintAddress);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(25.50m);
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

    [Fact]
    public async Task IWalletService_SignAndBalanceMethods_CanBeInvoked()
    {
        IWalletService service = new DummyWalletService();
        await service.ConnectAsync();

        var message = new byte[] { 1, 2, 3 };
        var signature = await service.SignMessageAsync(message);
        Assert.NotNull(signature);
        Assert.Equal(64, signature!.Length);

        var tx = new byte[] { 4, 5, 6 };
        var txSignature = await service.SignTransactionAsync(tx);
        Assert.NotNull(txSignature);
        Assert.Equal(64, txSignature!.Length);

        ulong lamports = await service.GetLamportsBalanceAsync();
        Assert.Equal(1_500_000_000UL, lamports);

        ulong lamportsWithAddress = await service.GetLamportsBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V");
        Assert.Equal(1_500_000_000UL, lamportsWithAddress);

        decimal tokenBalance = await service.GetTokenBalanceAsync("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        Assert.Equal(25.50m, tokenBalance);

        decimal tokenBalanceWithAddress = await service.GetTokenBalanceAsync("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        Assert.Equal(25.50m, tokenBalanceWithAddress);
    }

    [Fact]
    public async Task MockWalletService_AllMethods_ExecuteSuccessfully()
    {
        var mock = new Plemu.Runtime.Mocks.MockWalletService();
        await mock.ConnectAsync();

        var msgSig = await mock.SignMessageAsync(new byte[] { 1, 2 });
        Assert.NotNull(msgSig);
        Assert.Equal(64, msgSig!.Length);

        var txSig = await mock.SignTransactionAsync(new byte[] { 3, 4 });
        Assert.NotNull(txSig);
        Assert.Equal(64, txSig!.Length);

        var balance = await mock.GetLamportsBalanceAsync();
        Assert.Equal(2_000_000_000UL, balance);

        var balanceWithAddr = await mock.GetLamportsBalanceAsync("4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL");
        Assert.Equal(2_000_000_000UL, balanceWithAddr);

        var tokenBal = await mock.GetTokenBalanceAsync("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        Assert.Equal(100.0m, tokenBal);

        var tokenBalWithAddr = await mock.GetTokenBalanceAsync("4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL", "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v");
        Assert.Equal(100.0m, tokenBalWithAddr);
    }
}
