using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Xunit;

namespace Plemu.Solana.Tests;

public class IEntitlementServiceTests
{
    private class FakeEntitlementService : IEntitlementService
    {
        public bool AccessGranted { get; set; } = true;
        public WalletIdentity? LastWallet { get; private set; }
        public string? LastPackId { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<bool> CanAccessPackAsync(WalletIdentity wallet, string packId, CancellationToken cancellationToken = default)
        {
            LastWallet = wallet;
            LastPackId = packId;
            LastToken = cancellationToken;
            return Task.FromResult(AccessGranted);
        }
    }

    [Fact]
    public async Task CanAccessPackAsync_ReturnsExpectedResult()
    {
        var service = new FakeEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");
        var packId = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";

        var result = await service.CanAccessPackAsync(wallet, packId);

        Assert.True(result);
        Assert.Equal(wallet, service.LastWallet);
        Assert.Equal(packId, service.LastPackId);
    }

    [Fact]
    public async Task CanAccessPack_DefaultMethod_DelegatesToAsync()
    {
        IEntitlementService service = new FakeEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");
        var packId = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";

        var result = await service.CanAccessPack(wallet, packId);

        Assert.True(result);
        var fake = (FakeEntitlementService)service;
        Assert.Equal(wallet, fake.LastWallet);
        Assert.Equal(packId, fake.LastPackId);
    }
}
