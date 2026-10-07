using System;
using System.Reflection;
using Plemu.Solana.BondingCurve;
using Xunit;

namespace Plemu.Solana.Tests;

public class BondingCurveMathTests
{
    private const ulong DefaultVirtualSol = 30_000_000_000UL; // 30 SOL in lamports
    private const ulong DefaultVirtualTokens = 1_073_000_000_000_000UL; // ~1.073B tokens with 6 decimals
    private const ulong DefaultRealTokens = 793_100_000_000_000UL; // 793.1M tokens real reserves
    private const ulong DefaultRealSol = 10_000_000_000UL; // 10 SOL real reserves

    [Fact]
    public void CalculateBuyTokensOut_StandardReserves_ReturnsExpectedAmountWithoutPrecisionLoss()
    {
        ulong solAmountIn = 1_000_000_000UL; // 1 SOL

        // k = 30_000_000_000 * 1_073_000_000_000_000 = 32_190_000_000_000_000_000_000_000
        // newSol = 31_000_000_000
        // newToken = k / newSol = 1_038_387_096_774_193
        // tokensOut = 1_073_000_000_000_000 - 1_038_387_096_774_193 = 34_612_903_225_807
        ulong tokensOut = BondingCurveMath.CalculateBuyTokensOut(
            DefaultVirtualSol,
            DefaultVirtualTokens,
            DefaultRealTokens,
            solAmountIn);

        Assert.Equal(34_612_903_225_807UL, tokensOut);
    }

    [Fact]
    public void CalculateBuyTokensOut_ExceedingRealTokenReserves_CapsAtRealTokenReserves()
    {
        ulong smallRealTokens = 10_000_000UL;
        ulong solAmountIn = 500_000_000_000UL; // 500 SOL

        ulong tokensOut = BondingCurveMath.CalculateBuyTokensOut(
            DefaultVirtualSol,
            DefaultVirtualTokens,
            smallRealTokens,
            solAmountIn);

        Assert.Equal(smallRealTokens, tokensOut);
    }

    [Theory]
    [InlineData(0UL, 1000UL, 1000UL, 100UL)]
    [InlineData(1000UL, 0UL, 1000UL, 100UL)]
    [InlineData(1000UL, 1000UL, 0UL, 100UL)]
    [InlineData(1000UL, 1000UL, 1000UL, 0UL)]
    public void CalculateBuyTokensOut_ZeroInputs_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong virtualTokens, ulong realTokens, ulong solIn)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateBuyTokensOut(virtualSol, virtualTokens, realTokens, solIn));
    }

    [Fact]
    public void CalculateSellSolOut_StandardReserves_ReturnsExpectedAmount()
    {
        ulong virtualSol = 30_000_000_000UL;
        ulong virtualTokens = 1_000_000_000UL;
        ulong realSol = 10_000_000_000UL;
        ulong tokensIn = 100_000_000UL;

        // k = 30_000_000_000 * 1_000_000_000 = 30_000_000_000_000_000_000
        // newToken = 1_100_000_000
        // newSol = 30_000_000_000_000_000_000 / 1_100_000_000 = 27_272_727_272
        // solOut = 30_000_000_000 - 27_272_727_272 = 2_727_272_728
        ulong solOut = BondingCurveMath.CalculateSellSolOut(
            virtualSol,
            virtualTokens,
            realSol,
            tokensIn);

        Assert.Equal(2_727_272_728UL, solOut);
    }

    [Fact]
    public void CalculateSellSolOut_ExceedingRealSolReserves_CapsAtRealSolReserves()
    {
        ulong virtualSol = 30_000_000_000UL;
        ulong virtualTokens = 1_000_000_000UL;
        ulong smallRealSol = 500_000_000UL; // 0.5 SOL real available
        ulong tokensIn = 100_000_000UL; // would calculate 2.727 SOL out

        ulong solOut = BondingCurveMath.CalculateSellSolOut(
            virtualSol,
            virtualTokens,
            smallRealSol,
            tokensIn);

        Assert.Equal(smallRealSol, solOut);
    }

    [Theory]
    [InlineData(0UL, 1000UL, 1000UL, 100UL)]
    [InlineData(1000UL, 0UL, 1000UL, 100UL)]
    [InlineData(1000UL, 1000UL, 0UL, 100UL)]
    [InlineData(1000UL, 1000UL, 1000UL, 0UL)]
    public void CalculateSellSolOut_ZeroInputs_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong virtualTokens, ulong realSol, ulong tokenIn)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateSellSolOut(virtualSol, virtualTokens, realSol, tokenIn));
    }

    [Fact]
    public void CalculateRequiredSolForTokens_StandardDesired_ReturnsCeilingRequiredSol()
    {
        ulong virtualSol = 30_000_000_000UL;
        ulong virtualTokens = 1_000_000_000UL;
        ulong tokensDesired = 100_000_000UL;

        // newToken = 900_000_000
        // k = 30_000_000_000 * 1_000_000_000 = 30_000_000_000_000_000_000
        // ceil(k / 900_000_000) = ceil(33_333_333_333.333...) = 33_333_333_334
        // solRequired = 33_333_333_334 - 30_000_000_000 = 3_333_333_334
        ulong solRequired = BondingCurveMath.CalculateRequiredSolForTokens(
            virtualSol,
            virtualTokens,
            tokensDesired);

        Assert.Equal(3_333_333_334UL, solRequired);

        // Invariant: paying solRequired must yield at least tokensDesired
        ulong tokensReceived = BondingCurveMath.CalculateBuyTokensOut(
            virtualSol,
            virtualTokens,
            ulong.MaxValue,
            solRequired);

        Assert.True(tokensReceived >= tokensDesired);
    }

    [Theory]
    [InlineData(0UL, 1000UL, 100UL)]
    [InlineData(1000UL, 0UL, 100UL)]
    [InlineData(1000UL, 1000UL, 0UL)]
    public void CalculateRequiredSolForTokens_ZeroInputs_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong virtualTokens, ulong tokensDesired)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateRequiredSolForTokens(virtualSol, virtualTokens, tokensDesired));
    }

    [Theory]
    [InlineData(1000UL, 1000UL, 1000UL)] // tokensDesired == virtualTokens
    [InlineData(1000UL, 1000UL, 2000UL)] // tokensDesired > virtualTokens
    public void CalculateRequiredSolForTokens_DesiredTokensEqualOrGreaterReserves_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong virtualTokens, ulong tokensDesired)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateRequiredSolForTokens(virtualSol, virtualTokens, tokensDesired));
    }

    [Fact]
    public void CalculateSpotPriceLamports_StandardReserves_ReturnsSpotPriceForOneToken()
    {
        ulong virtualSol = 30_000_000_000UL; // 30 SOL
        ulong virtualTokens = 1_000_000_000_000_000UL; // 1B tokens with 6 decimals (1 token = 1_000_000 units)

        // P = (30_000_000_000 * 1_000_000) / 1_000_000_000_000_000 = 30 lamports
        ulong spotPrice = BondingCurveMath.CalculateSpotPriceLamports(virtualSol, virtualTokens);

        Assert.Equal(30UL, spotPrice);
    }

    [Theory]
    [InlineData(0UL, 1_000_000UL)]
    [InlineData(30_000_000_000UL, 0UL)]
    public void CalculateSpotPriceLamports_ZeroReserves_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong virtualTokens)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateSpotPriceLamports(virtualSol, virtualTokens));
    }

    [Fact]
    public void CalculateSpotPriceLamports_WithCustomDecimals_CalculatesCorrectly()
    {
        ulong virtualSol = 30_000_000_000UL;
        ulong virtualTokens = 1_000_000_000_000_000UL;

        // 9 decimals instead of 6: 1 token = 10^9 units
        // P = (30_000_000_000 * 1_000_000_000) / 1_000_000_000_000_000 = 30_000 lamports
        ulong spotPrice9 = BondingCurveMath.CalculateSpotPriceLamports(virtualSol, virtualTokens, decimals: 9);
        Assert.Equal(30_000UL, spotPrice9);

        // 0 decimals: 1 token = 1 unit
        ulong spotPrice0 = BondingCurveMath.CalculateSpotPriceLamports(virtualSol, virtualTokens, decimals: 0);
        Assert.Equal(0UL, spotPrice0); // integer truncation of (30B * 1) / 10^15
    }

    [Fact]
    public void CalculateSpotPriceLamports_DecimalsAbove18_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.CalculateSpotPriceLamports(1_000_000UL, 1_000_000UL, decimals: 19));
    }

    [Fact]
    public void InjectLiquidity_ValidFee_IncreasesReservesMonotonically()
    {
        ulong virtualSol = 30_000_000_000UL;
        ulong realSol = 0UL; // Genesis state before swaps
        ulong fee = 12_500_000UL; // 0.0125 SOL (e.g. 25% of 0.05 SOL game license)

        (ulong newVirtualSol, ulong newRealSol) = BondingCurveMath.InjectLiquidity(
            virtualSol,
            realSol,
            fee);

        Assert.Equal(30_012_500_000UL, newVirtualSol);
        Assert.Equal(12_500_000UL, newRealSol);

        ulong tokensSupply = 100_000_000_000UL;
        ulong priceBefore = BondingCurveMath.CalculateSpotPriceLamports(virtualSol, tokensSupply);
        ulong priceAfter = BondingCurveMath.CalculateSpotPriceLamports(newVirtualSol, tokensSupply);

        Assert.True(priceAfter >= priceBefore);
    }

    [Theory]
    [InlineData(0UL, 100UL, 50UL)]
    [InlineData(100UL, 100UL, 0UL)]
    public void InjectLiquidity_ZeroInputs_ThrowsArgumentOutOfRangeException(
        ulong virtualSol, ulong realSol, ulong fee)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BondingCurveMath.InjectLiquidity(virtualSol, realSol, fee));
    }

    [Fact]
    public void UInt128Arithmetic_LargeReserves_DoesNotOverflow()
    {
        // Near ulong.MaxValue: intermediate product will be around 10^38, well within UInt128.MaxValue (~3.4 * 10^38)
        ulong largeVirtualSol = 10_000_000_000_000_000_000UL;
        ulong largeVirtualTokens = 10_000_000_000_000_000_000UL;
        ulong largeRealTokens = 5_000_000_000_000_000_000UL;
        ulong solIn = 1_000_000_000_000_000_000UL;

        ulong tokensOut = BondingCurveMath.CalculateBuyTokensOut(
            largeVirtualSol,
            largeVirtualTokens,
            largeRealTokens,
            solIn);

        Assert.True(tokensOut > 0);
        Assert.True(tokensOut <= largeRealTokens);
    }

    [Fact]
    public void ArchitecturalInvariant_NoFloatingPointTypesInPublicApi()
    {
        var type = typeof(BondingCurveMath);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            Assert.False(
                method.ReturnType == typeof(float) ||
                method.ReturnType == typeof(double) ||
                method.ReturnType == typeof(decimal),
                $"Method {method.Name} returns floating-point type {method.ReturnType.Name}");

            foreach (var parameter in method.GetParameters())
            {
                Assert.False(
                    parameter.ParameterType == typeof(float) ||
                    parameter.ParameterType == typeof(double) ||
                    parameter.ParameterType == typeof(decimal),
                    $"Method {method.Name} has parameter {parameter.Name} of floating-point type {parameter.ParameterType.Name}");
            }
        }
    }
}
