using Plemu.Core.Models;
using Xunit;

namespace Plemu.Solana.Tests;

public class WalletIdentityTests
{
    [Fact]
    public void IsValid_WithValidSolanaPublicKey_ReturnsTrue()
    {
        // Typical 32-44 base58 characters
        var identity = new WalletIdentity("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", "PSG1 Primary");

        Assert.True(identity.IsValid);
        Assert.Equal("7nxQB5HhFhR4Xf9M1Wj7mP3W7T8c6Z5G4Y2W1Q9M8P7V", identity.PublicKey);
        Assert.Equal("PSG1 Primary", identity.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("shortkey123")] // < 32 chars
    [InlineData("toolongkey12345678901234567890123456789012345678901234567890")] // > 44 chars
    public void IsValid_WithInvalidPublicKey_ReturnsFalse(string invalidKey)
    {
        var identity = new WalletIdentity(invalidKey);

        Assert.False(identity.IsValid);
    }

    [Fact]
    public void IsValid_WithNullPublicKey_ReturnsFalse()
    {
        var identity = new WalletIdentity(null!);

        Assert.False(identity.IsValid);
    }

    [Fact]
    public void WalletConnectionState_ContainsAllRequiredStates()
    {
        var states = Enum.GetValues<WalletConnectionState>();

        Assert.Contains(WalletConnectionState.Disconnected, states);
        Assert.Contains(WalletConnectionState.Connecting, states);
        Assert.Contains(WalletConnectionState.Connected, states);
        Assert.Contains(WalletConnectionState.Disconnecting, states);
        Assert.Contains(WalletConnectionState.Error, states);
        Assert.Equal(5, states.Length);
    }
}
