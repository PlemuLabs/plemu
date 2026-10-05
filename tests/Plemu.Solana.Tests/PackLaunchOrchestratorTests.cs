using System;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Runtime;
using Plemu.Runtime.Mocks;
using Xunit;

namespace Plemu.Solana.Tests;

public class PackLaunchOrchestratorTests
{
    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        var walletMock = new MockWalletService();
        var entitlementMock = new MockEntitlementService();

        Assert.Throws<ArgumentNullException>(() => new PackLaunchOrchestrator(null!, entitlementMock));
        Assert.Throws<ArgumentNullException>(() => new PackLaunchOrchestrator(walletMock, null!));
    }

    [Fact]
    public async Task CanLaunchAsync_EmptyPackId_ReturnsFalse()
    {
        var wallet = new MockWalletService();
        var entitlement = new MockEntitlementService();
        var orchestrator = new PackLaunchOrchestrator(wallet, entitlement);

        Assert.False(await orchestrator.CanLaunchAsync(""));
        Assert.False(await orchestrator.CanLaunchAsync("   "));
        Assert.False(await orchestrator.CanLaunchAsync(null!));
    }

    [Fact]
    public async Task CanLaunchAsync_WhenWalletDisconnected_ReturnsFalse()
    {
        var wallet = new MockWalletService();
        await wallet.DisconnectAsync();
        var entitlement = new MockEntitlementService();
        var orchestrator = new PackLaunchOrchestrator(wallet, entitlement);

        var canLaunch = await orchestrator.CanLaunchAsync("pack-123");
        Assert.False(canLaunch);
    }

    [Fact]
    public async Task CanLaunchAsync_WhenWalletConnectedAndEntitled_ReturnsTrue()
    {
        var wallet = new MockWalletService();
        await wallet.ConnectAsync();
        var entitlement = new MockEntitlementService();
        var orchestrator = new PackLaunchOrchestrator(wallet, entitlement);

        var canLaunch = await orchestrator.CanLaunchAsync("pack-123");
        Assert.True(canLaunch);
    }

    [Fact]
    public async Task MockWalletService_StateTransitions_WorkAsExpected()
    {
        var wallet = new MockWalletService();
        Assert.Equal(WalletConnectionState.Connected, wallet.State);
        Assert.NotNull(wallet.CurrentWallet);

        var eventFired = false;
        Action<WalletConnectionState, WalletIdentity?> disconnectHandler = (state, id) =>
        {
            eventFired = true;
            Assert.Equal(WalletConnectionState.Disconnected, state);
            Assert.Null(id);
        };

        wallet.StateChanged += disconnectHandler;
        await wallet.DisconnectAsync();
        wallet.StateChanged -= disconnectHandler;

        Assert.True(eventFired);
        Assert.Equal(WalletConnectionState.Disconnected, wallet.State);
        Assert.Null(wallet.CurrentWallet);

        eventFired = false;
        Action<WalletConnectionState, WalletIdentity?> connectHandler = (state, id) =>
        {
            eventFired = true;
            Assert.Equal(WalletConnectionState.Connected, state);
            Assert.NotNull(id);
        };

        wallet.StateChanged += connectHandler;
        var connected = await wallet.ConnectAsync();
        wallet.StateChanged -= connectHandler;

        Assert.True(connected);
        Assert.True(eventFired);
        Assert.Equal(WalletConnectionState.Connected, wallet.State);
    }

    [Fact]
    public async Task MockEntitlementService_AccessChecks_WorkAsExpected()
    {
        var service = new MockEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");

        Assert.True(await service.CanAccessPack(wallet, "valid-pack"));
        Assert.False(await service.CanAccessPack(wallet, ""));
        Assert.False(await service.CanAccessPack(wallet, "   "));
        Assert.False(await service.CanAccessPack(wallet, null!));
    }
}
