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
        public string? LastEntrySha256 { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<bool> CanAccessPackAsync(WalletIdentity wallet, string packId, CancellationToken cancellationToken = default)
        {
            LastWallet = wallet;
            LastPackId = packId;
            LastToken = cancellationToken;
            return Task.FromResult(AccessGranted);
        }

        public Task<bool> CanAccessEntryAsync(WalletIdentity wallet, string entrySha256, CancellationToken cancellationToken = default)
        {
            LastWallet = wallet;
            LastEntrySha256 = entrySha256;
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

    [Fact]
    public async Task CanAccessEntryAsync_ReturnsExpectedResult()
    {
        var service = new FakeEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");
        var sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        var result = await service.CanAccessEntryAsync(wallet, sha256);

        Assert.True(result);
        Assert.Equal(wallet, service.LastWallet);
        Assert.Equal(sha256, service.LastEntrySha256);
    }

    [Fact]
    public async Task CanAccessEntry_DefaultMethod_DelegatesToAsync()
    {
        IEntitlementService service = new FakeEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");
        var sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        var result = await service.CanAccessEntry(wallet, sha256);

        Assert.True(result);
        var fake = (FakeEntitlementService)service;
        Assert.Equal(wallet, fake.LastWallet);
        Assert.Equal(sha256, fake.LastEntrySha256);
    }

    [Fact]
    public async Task MockEntitlementService_CanAccessPackAndEntry_ReturnsExpectedResults()
    {
        var mock = new Plemu.Runtime.Mocks.MockEntitlementService();
        var wallet = new WalletIdentity("11111111111111111111111111111111");

        Assert.True(await mock.CanAccessPackAsync(wallet, "valid-pack"));
        Assert.False(await mock.CanAccessPackAsync(wallet, ""));
        Assert.True(await mock.CanAccessPack(wallet, "valid-pack"));

        Assert.True(await mock.CanAccessEntryAsync(wallet, "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd"));
        Assert.False(await mock.CanAccessEntryAsync(wallet, ""));
        Assert.True(await mock.CanAccessEntry(wallet, "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd"));
    }
}
