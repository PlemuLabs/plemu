using System;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class DefaultReceiptPdaDeriverTests
{
    private const string ProgramId = "11111111111111111111111111111111";
    private const string WalletPubkey = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
    private const string SampleSha256Hex = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";
    private static readonly byte[] SampleSha256Bytes = Convert.FromHexString(SampleSha256Hex);

    [Fact]
    public void DeriveReceiptAddress_ValidInputs_ReturnsValidBase58Address()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;

        string receiptPda = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);

        Assert.False(string.IsNullOrWhiteSpace(receiptPda));
        Assert.InRange(receiptPda.Length, 32, 44);
        Assert.True(Base58.TryDecode(receiptPda, out var bytes));
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void DeriveReceiptAddress_WithStringSha256_MatchesBytesOverload()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;

        string receipt1 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);
        string receipt2 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Hex, WalletPubkey);

        Assert.Equal(receipt1, receipt2);
    }

    [Fact]
    public void DeriveReceiptAddress_Deterministic_ReturnsSameAddress()
    {
        var deriver = new DefaultReceiptPdaDeriver();

        string receipt1 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);
        string receipt2 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);

        Assert.Equal(receipt1, receipt2);
    }

    [Fact]
    public void DeriveReceiptAddress_DifferentWallets_ReturnDifferentAddresses()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;
        const string otherWallet = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";

        string receipt1 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);
        string receipt2 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, otherWallet);

        Assert.NotEqual(receipt1, receipt2);
    }

    [Fact]
    public void DeriveReceiptAddress_DifferentArtifactSha256_ReturnDifferentAddresses()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;
        byte[] otherSha = Convert.FromHexString("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        string receipt1 = deriver.DeriveReceiptAddress(ProgramId, SampleSha256Bytes, WalletPubkey);
        string receipt2 = deriver.DeriveReceiptAddress(ProgramId, otherSha, WalletPubkey);

        Assert.NotEqual(receipt1, receipt2);
    }

    [Theory]
    [InlineData(null, SampleSha256Hex, WalletPubkey)]
    [InlineData("", SampleSha256Hex, WalletPubkey)]
    [InlineData(ProgramId, null, WalletPubkey)]
    [InlineData(ProgramId, "", WalletPubkey)]
    [InlineData(ProgramId, "invalid-hex-sha256", WalletPubkey)]
    [InlineData(ProgramId, "1234", WalletPubkey)]
    [InlineData(ProgramId, SampleSha256Hex, null)]
    [InlineData(ProgramId, SampleSha256Hex, "")]
    [InlineData("invalid-b58-000", SampleSha256Hex, WalletPubkey)]
    [InlineData(ProgramId, SampleSha256Hex, "invalid-b58-000")]
    public void DeriveReceiptAddress_InvalidStringInputs_ThrowsArgumentException(string? programId, string? sha256Hex, string? walletPubkey)
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveReceiptAddress(programId!, sha256Hex!, walletPubkey!)));
    }

    [Fact]
    public void DeriveReceiptAddress_InvalidByteSha256_ThrowsArgumentException()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveReceiptAddress(ProgramId, (byte[])null!, WalletPubkey)));

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveReceiptAddress(ProgramId, new byte[31], WalletPubkey)));

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveReceiptAddress(ProgramId, new byte[33], WalletPubkey)));
    }

    [Fact]
    public void DeriveReceiptAddressInternal_WhenNoBumpFound_ThrowsInvalidOperationException()
    {
        var deriver = DefaultReceiptPdaDeriver.Instance;

        Assert.Throws<InvalidOperationException>((Action)(() =>
            deriver.DeriveReceiptAddressInternal(ProgramId, SampleSha256Bytes, WalletPubkey, 0, 1)));
    }
}
