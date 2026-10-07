using System;
using Plemu.Solana.BondingCurve;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class BondingCurvePdaDeriverTests
{
    private const string ValidMint1 = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    private const string ValidMint2 = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
    private const string CustomProgramId = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";

    [Fact]
    public void SingletonInstance_ImplementsInterface()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        Assert.NotNull(deriver);
        Assert.IsAssignableFrom<IBondingCurvePdaDeriver>(deriver);
    }

    [Fact]
    public void DeriveBondingCurveAddress_ValidMint_ReturnsValidOffCurveAddressAndBump()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (address, bump) = deriver.DeriveBondingCurveAddress(ValidMint1);

        Assert.False(string.IsNullOrWhiteSpace(address));
        Assert.InRange(address.Length, 32, 44);
        Assert.True(Base58.TryDecode(address, out var bytes));
        Assert.Equal(32, bytes.Length);
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(bytes));
        Assert.InRange(bump, (byte)0, (byte)255);
    }

    [Fact]
    public void DeriveCurveSolVaultAddress_ValidMint_ReturnsValidOffCurveAddressAndBump()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (address, bump) = deriver.DeriveCurveSolVaultAddress(ValidMint1);

        Assert.False(string.IsNullOrWhiteSpace(address));
        Assert.InRange(address.Length, 32, 44);
        Assert.True(Base58.TryDecode(address, out var bytes));
        Assert.Equal(32, bytes.Length);
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(bytes));
        Assert.InRange(bump, (byte)0, (byte)255);
    }

    [Fact]
    public void DeriveCurveTokenVaultAddress_ValidMint_ReturnsValidOffCurveAddressAndBump()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (address, bump) = deriver.DeriveCurveTokenVaultAddress(ValidMint1);

        Assert.False(string.IsNullOrWhiteSpace(address));
        Assert.InRange(address.Length, 32, 44);
        Assert.True(Base58.TryDecode(address, out var bytes));
        Assert.Equal(32, bytes.Length);
        Assert.False(DefaultSeatPdaDeriver.IsOnCurve(bytes));
        Assert.InRange(bump, (byte)0, (byte)255);
    }

    [Fact]
    public void Derivations_AreDeterministic_ReturnSameAddressAndBump()
    {
        var deriver = new DefaultBondingCurvePdaDeriver();

        var (curve1, bump1) = deriver.DeriveBondingCurveAddress(ValidMint1);
        var (curve2, bump2) = deriver.DeriveBondingCurveAddress(ValidMint1);
        Assert.Equal(curve1, curve2);
        Assert.Equal(bump1, bump2);

        var (solVault1, solBump1) = deriver.DeriveCurveSolVaultAddress(ValidMint1);
        var (solVault2, solBump2) = deriver.DeriveCurveSolVaultAddress(ValidMint1);
        Assert.Equal(solVault1, solVault2);
        Assert.Equal(solBump1, solBump2);

        var (tokenVault1, tokenBump1) = deriver.DeriveCurveTokenVaultAddress(ValidMint1);
        var (tokenVault2, tokenBump2) = deriver.DeriveCurveTokenVaultAddress(ValidMint1);
        Assert.Equal(tokenVault1, tokenVault2);
        Assert.Equal(tokenBump1, tokenBump2);
    }

    [Fact]
    public void Derivations_ForSameMint_ProduceDistinctAddresses()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (curve, _) = deriver.DeriveBondingCurveAddress(ValidMint1);
        var (solVault, _) = deriver.DeriveCurveSolVaultAddress(ValidMint1);
        var (tokenVault, _) = deriver.DeriveCurveTokenVaultAddress(ValidMint1);

        Assert.NotEqual(curve, solVault);
        Assert.NotEqual(curve, tokenVault);
        Assert.NotEqual(solVault, tokenVault);
    }

    [Fact]
    public void Derivations_ForDifferentMints_ProduceDistinctAddresses()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (curve1, _) = deriver.DeriveBondingCurveAddress(ValidMint1);
        var (curve2, _) = deriver.DeriveBondingCurveAddress(ValidMint2);
        Assert.NotEqual(curve1, curve2);

        var (solVault1, _) = deriver.DeriveCurveSolVaultAddress(ValidMint1);
        var (solVault2, _) = deriver.DeriveCurveSolVaultAddress(ValidMint2);
        Assert.NotEqual(solVault1, solVault2);

        var (tokenVault1, _) = deriver.DeriveCurveTokenVaultAddress(ValidMint1);
        var (tokenVault2, _) = deriver.DeriveCurveTokenVaultAddress(ValidMint2);
        Assert.NotEqual(tokenVault1, tokenVault2);
    }

    [Fact]
    public void Derivations_WithNullMarketProgramId_MatchesDefaultMarketProgramId()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (curveWithNull, bumpWithNull) = deriver.DeriveBondingCurveAddress(ValidMint1, null);
        var (curveExplicit, bumpExplicit) = deriver.DeriveBondingCurveAddress(ValidMint1, PlemuMarketTransactionBuilder.DefaultMarketProgramId);

        Assert.Equal(curveExplicit, curveWithNull);
        Assert.Equal(bumpExplicit, bumpWithNull);

        var (solWithNull, solBumpWithNull) = deriver.DeriveCurveSolVaultAddress(ValidMint1, null);
        var (solExplicit, solBumpExplicit) = deriver.DeriveCurveSolVaultAddress(ValidMint1, PlemuMarketTransactionBuilder.DefaultMarketProgramId);

        Assert.Equal(solExplicit, solWithNull);
        Assert.Equal(solBumpExplicit, solBumpWithNull);

        var (tokenWithNull, tokenBumpWithNull) = deriver.DeriveCurveTokenVaultAddress(ValidMint1, null);
        var (tokenExplicit, tokenBumpExplicit) = deriver.DeriveCurveTokenVaultAddress(ValidMint1, PlemuMarketTransactionBuilder.DefaultMarketProgramId);

        Assert.Equal(tokenExplicit, tokenWithNull);
        Assert.Equal(tokenBumpExplicit, tokenBumpWithNull);
    }

    [Fact]
    public void Derivations_WithCustomMarketProgramId_ReturnsDifferentAddressThanDefault()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        var (curveDefault, _) = deriver.DeriveBondingCurveAddress(ValidMint1, null);
        var (curveCustom, _) = deriver.DeriveBondingCurveAddress(ValidMint1, CustomProgramId);

        Assert.NotEqual(curveDefault, curveCustom);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")] // Too short to be 32 bytes
    public void DeriveBondingCurveAddress_InvalidMint_ThrowsArgumentException(string? invalidMint)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveBondingCurveAddress(invalidMint!)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")]
    public void DeriveCurveSolVaultAddress_InvalidMint_ThrowsArgumentException(string? invalidMint)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveCurveSolVaultAddress(invalidMint!)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")]
    public void DeriveCurveTokenVaultAddress_InvalidMint_ThrowsArgumentException(string? invalidMint)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveCurveTokenVaultAddress(invalidMint!)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")]
    public void DeriveBondingCurveAddress_InvalidProgramId_ThrowsArgumentException(string invalidProgramId)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveBondingCurveAddress(ValidMint1, invalidProgramId)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")]
    public void DeriveCurveSolVaultAddress_InvalidProgramId_ThrowsArgumentException(string invalidProgramId)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveCurveSolVaultAddress(ValidMint1, invalidProgramId)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-b58-000")]
    [InlineData("111")]
    public void DeriveCurveTokenVaultAddress_InvalidProgramId_ThrowsArgumentException(string invalidProgramId)
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;

        Assert.ThrowsAny<ArgumentException>((Action)(() =>
            deriver.DeriveCurveTokenVaultAddress(ValidMint1, invalidProgramId)));
    }

    [Fact]
    public void DerivePdaInternal_WhenNoBumpFound_ThrowsInvalidOperationException()
    {
        var deriver = new DefaultBondingCurvePdaDeriver();
        byte[] mintBytes = Base58.Decode(ValidMint1);
        byte[] programIdBytes = Base58.Decode(CustomProgramId);
        byte[] prefix = System.Text.Encoding.ASCII.GetBytes("test");

        Assert.Throws<InvalidOperationException>((Action)(() =>
            deriver.DerivePdaInternal(prefix, mintBytes, programIdBytes, 0, 1)));
    }

    [Fact]
    public void BondingCurveState_RecordProperties_RetainedCorrectly()
    {
        var state = new BondingCurveState(
            Mint: ValidMint1,
            VirtualSolReserves: 30_000_000_000UL,
            VirtualTokenReserves: 1_073_000_000_000_000UL,
            RealSolReserves: 10_000_000_000UL,
            RealTokenReserves: 793_100_000_000_000UL,
            TotalSupply: 1_000_000_000_000_000UL,
            IsComplete: false,
            Bump: 254);

        Assert.Equal(ValidMint1, state.Mint);
        Assert.Equal(30_000_000_000UL, state.VirtualSolReserves);
        Assert.Equal(1_073_000_000_000_000UL, state.VirtualTokenReserves);
        Assert.Equal(10_000_000_000UL, state.RealSolReserves);
        Assert.Equal(793_100_000_000_000UL, state.RealTokenReserves);
        Assert.Equal(1_000_000_000_000_000UL, state.TotalSupply);
        Assert.False(state.IsComplete);
        Assert.Equal(254, state.Bump);

        var copy = state with { IsComplete = true };
        Assert.True(copy.IsComplete);
        Assert.Equal(state.Mint, copy.Mint);
    }

    [Fact]
    public void GameTokenQuote_RecordProperties_RetainedCorrectly()
    {
        var quote = new GameTokenQuote(
            Mint: ValidMint1,
            IsBuy: true,
            InAmount: 1_000_000_000UL,
            OutAmount: 34_612_903_225_807UL,
            MinOutAmount: 34_266_774_193_548UL,
            SlippageBps: 100,
            SpotPriceBefore: 27_958UL,
            SpotPriceAfter: 29_853UL,
            PriceImpactBps: 677);

        Assert.Equal(ValidMint1, quote.Mint);
        Assert.True(quote.IsBuy);
        Assert.Equal(1_000_000_000UL, quote.InAmount);
        Assert.Equal(34_612_903_225_807UL, quote.OutAmount);
        Assert.Equal(34_266_774_193_548UL, quote.MinOutAmount);
        Assert.Equal(100UL, quote.SlippageBps);
        Assert.Equal(27_958UL, quote.SpotPriceBefore);
        Assert.Equal(29_853UL, quote.SpotPriceAfter);
        Assert.Equal(677UL, quote.PriceImpactBps);
    }

    [Fact]
    public void GameTokenMarketData_RecordProperties_RetainedCorrectly()
    {
        var data = new GameTokenMarketData(
            Mint: ValidMint1,
            Symbol: "PLAY",
            SpotPriceLamports: 27_958UL,
            MarketCapLamports: 27_958_000_000_000UL,
            VirtualSolReserves: 30_000_000_000UL,
            VirtualTokenReserves: 1_073_000_000_000_000UL,
            RealSolReserves: 10_000_000_000UL,
            RealTokenReserves: 793_100_000_000_000UL,
            IsComplete: false);

        Assert.Equal(ValidMint1, data.Mint);
        Assert.Equal("PLAY", data.Symbol);
        Assert.Equal(27_958UL, data.SpotPriceLamports);
        Assert.Equal(27_958_000_000_000UL, data.MarketCapLamports);
        Assert.Equal(30_000_000_000UL, data.VirtualSolReserves);
        Assert.Equal(1_073_000_000_000_000UL, data.VirtualTokenReserves);
        Assert.Equal(10_000_000_000UL, data.RealSolReserves);
        Assert.Equal(793_100_000_000_000UL, data.RealTokenReserves);
        Assert.False(data.IsComplete);
    }
}
