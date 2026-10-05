using System;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class DefaultSeatPdaDeriverTests
{
    private const string ProgramId = "11111111111111111111111111111111";
    private const string PackPubkey = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
    private const string WalletPubkey = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";

    [Fact]
    public void DeriveSeatAddress_ValidInputs_ReturnsValidBase58Address()
    {
        var deriver = DefaultSeatPdaDeriver.Instance;

        string seatPda = deriver.DeriveSeatAddress(ProgramId, PackPubkey, WalletPubkey);

        Assert.False(string.IsNullOrWhiteSpace(seatPda));
        Assert.InRange(seatPda.Length, 32, 44);
        Assert.True(Base58.TryDecode(seatPda, out var bytes));
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void DeriveSeatAddress_Deterministic_ReturnsSameAddress()
    {
        var deriver = new DefaultSeatPdaDeriver();

        string seat1 = deriver.DeriveSeatAddress(ProgramId, PackPubkey, WalletPubkey);
        string seat2 = deriver.DeriveSeatAddress(ProgramId, PackPubkey, WalletPubkey);

        Assert.Equal(seat1, seat2);
    }

    [Fact]
    public void DeriveSeatAddress_DifferentWallets_ReturnDifferentAddresses()
    {
        var deriver = DefaultSeatPdaDeriver.Instance;
        const string otherWallet = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";

        string seat1 = deriver.DeriveSeatAddress(ProgramId, PackPubkey, WalletPubkey);
        string seat2 = deriver.DeriveSeatAddress(ProgramId, PackPubkey, otherWallet);

        Assert.NotEqual(seat1, seat2);
    }

    [Fact]
    public void DeriveSeatAddress_DifferentPacks_ReturnDifferentAddresses()
    {
        var deriver = DefaultSeatPdaDeriver.Instance;
        const string otherPack = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";

        string seat1 = deriver.DeriveSeatAddress(ProgramId, PackPubkey, WalletPubkey);
        string seat2 = deriver.DeriveSeatAddress(ProgramId, otherPack, WalletPubkey);

        Assert.NotEqual(seat1, seat2);
    }

    [Theory]
    [InlineData(null, PackPubkey, WalletPubkey)]
    [InlineData("", PackPubkey, WalletPubkey)]
    [InlineData(ProgramId, null, WalletPubkey)]
    [InlineData(ProgramId, "", WalletPubkey)]
    [InlineData(ProgramId, PackPubkey, null)]
    [InlineData(ProgramId, PackPubkey, "")]
    [InlineData("invalid-b58-000", PackPubkey, WalletPubkey)]
    [InlineData(ProgramId, "invalid-b58-000", WalletPubkey)]
    [InlineData(ProgramId, PackPubkey, "invalid-b58-000")]
    public void DeriveSeatAddress_InvalidInputs_ThrowsArgumentException(string? programId, string? packPubkey, string? walletPubkey)
    {
        var deriver = DefaultSeatPdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveSeatAddress(programId!, packPubkey!, walletPubkey!)));
    }

    [Fact]
    public void DeriveSeatAddressInternal_WhenNoBumpFound_ThrowsInvalidOperationException()
    {
        var deriver = DefaultSeatPdaDeriver.Instance;

        Assert.Throws<InvalidOperationException>(() =>
            deriver.DeriveSeatAddressInternal(ProgramId, PackPubkey, WalletPubkey, 0, 1)); // start < end, 0 iterations
    }

    [Fact]
    public void IsOnCurve_EdgeCases_ReturnsExpected()
    {
        // Invalid byte length
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(new byte[31]));
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(new byte[33]));

        // Point where y = 1 -> y^2 - 1 = 0 -> u = 0, w = 0 -> returns true (on curve)
        byte[] y1 = new byte[32];
        y1[0] = 1;
        Assert.True(DefaultSeatPdaDeriver.IsOnCurve(y1));

        // Point where y >= P: P = 2^255 - 19. All 0xFF with highest bit cleared is 2^255 - 1 > P
        byte[] yMax = new byte[32];
        Array.Fill(yMax, (byte)0xFF);
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(yMax));
    }
}
