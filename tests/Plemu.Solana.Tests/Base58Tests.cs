using System;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class Base58Tests
{
    [Fact]
    public void EncodeAndDecode_Zeros_Produces32CharactersOfOnes()
    {
        byte[] zeros = new byte[32];
        string encoded = Base58.Encode(zeros);

        Assert.Equal("11111111111111111111111111111111", encoded);

        byte[] decoded = Base58.Decode(encoded);
        Assert.Equal(zeros, decoded);
    }

    [Fact]
    public void EncodeAndDecode_SolanaAddress_RoundtripsSuccessfully()
    {
        const string address = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";

        byte[] decoded = Base58.Decode(address);
        Assert.Equal(32, decoded.Length);

        string reEncoded = Base58.Encode(decoded);
        Assert.Equal(address, reEncoded);
    }

    [Theory]
    [InlineData("0OIl")] // 0, O, I, l are illegal in Base58
    [InlineData("FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm!")]
    [InlineData("FmRd9U€")] // Non-ASCII >= 128
    public void Decode_InvalidCharacters_ThrowsFormatException(string invalidInput)
    {
        Assert.Throws<FormatException>(() => Base58.Decode(invalidInput));
    }

    [Fact]
    public void Decode_NullOrEmpty_ReturnsEmptyArray()
    {
        Assert.Empty(Base58.Decode(string.Empty));
        Assert.Empty(Base58.Decode(null!));
    }

    [Fact]
    public void Encode_EmptySpan_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, Base58.Encode(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void EncodeAndDecode_LeadingAndOnlyZeros_PreservedAccurately()
    {
        byte[] threeZeros = new byte[] { 0, 0, 0 };
        string encoded = Base58.Encode(threeZeros);
        Assert.Equal("111", encoded);

        byte[] decoded = Base58.Decode("111");
        Assert.Equal(threeZeros, decoded);
    }

    [Fact]
    public void EncodeAndDecode_LeadingZerosFollowedByData_PreservedAccurately()
    {
        byte[] data = new byte[] { 0, 0, 5, 20 };
        string encoded = Base58.Encode(data);
        byte[] decoded = Base58.Decode(encoded);
        Assert.Equal(data, decoded);
    }

    [Fact]
    public void TryDecode_ValidAndInvalid_ReturnsExpected()
    {
        Assert.True(Base58.TryDecode("11111111111111111111111111111111", out var decoded));
        Assert.Equal(32, decoded.Length);

        Assert.False(Base58.TryDecode("0OIl", out var invalidDecoded));
        Assert.Empty(invalidDecoded);
    }
}
