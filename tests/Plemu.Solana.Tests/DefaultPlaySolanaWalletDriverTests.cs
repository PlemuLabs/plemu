using Plemu.Core.Models;
using Plemu.Solana;
using Xunit;

namespace Plemu.Solana.Tests;

public class DefaultPlaySolanaWalletDriverTests
{
    [Fact]
    public async Task RequestPublicKeyAsync_ReturnsValidDefaultPublicKey()
    {
        var driver = new DefaultPlaySolanaWalletDriver();

        var publicKey = await driver.RequestPublicKeyAsync();

        Assert.NotNull(publicKey);
        var identity = new WalletIdentity(publicKey!);
        Assert.True(identity.IsValid);
    }

    [Fact]
    public async Task RequestPublicKeyAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => driver.RequestPublicKeyAsync(cts.Token));
    }

    [Fact]
    public async Task DisconnectAsync_CompletesSuccessfully()
    {
        var driver = new DefaultPlaySolanaWalletDriver();

        await driver.DisconnectAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => driver.DisconnectAsync(cts.Token));
    }

    [Fact]
    public async Task SignMessageAsync_ReturnsValid64ByteSignature()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        var message = new byte[] { 1, 2, 3, 4 };

        var signature = await driver.SignMessageAsync(message);

        Assert.NotNull(signature);
        Assert.Equal(64, signature!.Length);
    }

    [Fact]
    public async Task SignMessageAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => driver.SignMessageAsync(new byte[] { 1 }, cts.Token));
    }

    [Fact]
    public async Task SignMessageAsync_WhenMessageIsNull_ThrowsArgumentNullException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();

        await Assert.ThrowsAsync<ArgumentNullException>(() => driver.SignMessageAsync(null!));
    }

    [Fact]
    public async Task SignTransactionAsync_ReturnsValid64ByteSignature()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        var tx = new byte[] { 10, 20, 30 };

        var signature = await driver.SignTransactionAsync(tx);

        Assert.NotNull(signature);
        Assert.Equal(64, signature!.Length);
    }

    [Fact]
    public async Task SignTransactionAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => driver.SignTransactionAsync(new byte[] { 1 }, cts.Token));
    }

    [Fact]
    public async Task SignTransactionAsync_WhenTransactionIsNull_ThrowsArgumentNullException()
    {
        var driver = new DefaultPlaySolanaWalletDriver();

        await Assert.ThrowsAsync<ArgumentNullException>(() => driver.SignTransactionAsync(null!));
    }

    [Fact]
    public async Task PlaySolanaWalletService_DefaultConstructor_UsesDefaultDriverSuccessfully()
    {
        var service = new PlaySolanaWalletService();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);

        bool connected = await service.ConnectAsync();

        Assert.True(connected);
        Assert.Equal(WalletConnectionState.Connected, service.State);
        Assert.NotNull(service.CurrentWallet);
        Assert.True(service.CurrentWallet!.IsValid);

        await service.DisconnectAsync();

        Assert.Equal(WalletConnectionState.Disconnected, service.State);
        Assert.Null(service.CurrentWallet);
    }
}
