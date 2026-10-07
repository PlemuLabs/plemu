using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Solana.BondingCurve;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class BondingCurveServiceTests
{
    private const string ValidMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    private const string ValidBuyer = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
    private const string ValidTokenAccount = "95EuhxN4dM1EecYhJdGg5sZkR1pGg5sZkR1pGg5sZkR1";

    private class MockSolanaRpcClient : ISolanaRpcClient
    {
        public Func<string, Task<SolanaAccountInfo?>>? GetAccountInfoHandler { get; set; }

        public Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (GetAccountInfoHandler != null)
            {
                return GetAccountInfoHandler(pubkey);
            }
            return Task.FromResult<SolanaAccountInfo?>(null);
        }

        public Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task GetStateAsync_WhenRpcClientIsNull_ReturnsDeterministicFallbackState()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        var service = new BondingCurveService(deriver, rpcClient: null);

        var state = await service.GetStateAsync(ValidMint);

        Assert.NotNull(state);
        Assert.Equal(ValidMint, state.Mint);
        Assert.Equal(30_000_000_000UL, state.VirtualSolReserves);
        Assert.Equal(1_073_000_000_000_000UL, state.VirtualTokenReserves);
        Assert.Equal(0UL, state.RealSolReserves);
        Assert.Equal(793_100_000_000_000UL, state.RealTokenReserves);
        Assert.Equal(1_000_000_000_000_000UL, state.TotalSupply);
        Assert.False(state.IsComplete);
        var (_, expectedBump) = deriver.DeriveBondingCurveAddress(ValidMint);
        Assert.Equal(expectedBump, state.Bump);
    }

    [Fact]
    public async Task GetStateAsync_WhenRpcAccountExists_DeserializesOnChainData()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        var mockRpc = new MockSolanaRpcClient();

        var expectedState = new BondingCurveState(
            Mint: ValidMint,
            VirtualSolReserves: 35_000_000_000UL,
            VirtualTokenReserves: 900_000_000_000_000UL,
            RealSolReserves: 5_000_000_000UL,
            RealTokenReserves: 700_000_000_000_000UL,
            TotalSupply: 1_000_000_000_000_000UL,
            IsComplete: false,
            Bump: 254);

        byte[] serializedData = BondingCurveService.SerializeAccountData(expectedState);

        mockRpc.GetAccountInfoHandler = pubkey =>
        {
            var (expectedPda, _) = deriver.DeriveBondingCurveAddress(ValidMint);
            if (pubkey == expectedPda)
            {
                return Task.FromResult<SolanaAccountInfo?>(new SolanaAccountInfo(
                    Owner: PlemuMarketTransactionBuilder.DefaultMarketProgramId,
                    Data: serializedData,
                    Lamports: 1_000_000_000UL));
            }
            return Task.FromResult<SolanaAccountInfo?>(null);
        };

        var service = new BondingCurveService(deriver, mockRpc);
        var state = await service.GetStateAsync(ValidMint);

        Assert.Equal(expectedState.VirtualSolReserves, state.VirtualSolReserves);
        Assert.Equal(expectedState.VirtualTokenReserves, state.VirtualTokenReserves);
        Assert.Equal(expectedState.RealSolReserves, state.RealSolReserves);
        Assert.Equal(expectedState.RealTokenReserves, state.RealTokenReserves);
        Assert.Equal(expectedState.TotalSupply, state.TotalSupply);
        Assert.Equal(expectedState.IsComplete, state.IsComplete);
        Assert.Equal(expectedState.Bump, state.Bump);
    }

    [Fact]
    public async Task GetBuyQuoteAsync_StandardFallbackReserves_CalculatesExpectedQuoteAndSlippage()
    {
        var service = new BondingCurveService(DefaultBondingCurvePdaDeriver.Instance, rpcClient: null);
        ulong solIn = 1_000_000_000UL; // 1 SOL

        var quote = await service.GetBuyQuoteAsync(ValidMint, solIn, slippageBasisPoints: 100);

        Assert.Equal(ValidMint, quote.Mint);
        Assert.True(quote.IsBuy);
        Assert.Equal(solIn, quote.InAmount);
        Assert.True(quote.OutAmount > 0);
        Assert.Equal(100UL, quote.SlippageBps);

        // minTokensOut must be exactly 99% of outAmount
        ulong expectedMinOut = (ulong)(((UInt128)quote.OutAmount * 9900) / 10000);
        Assert.Equal(expectedMinOut, quote.MinOutAmount);

        Assert.True(quote.SpotPriceAfter >= quote.SpotPriceBefore);
        Assert.True(quote.PriceImpactBps > 0);
    }

    [Fact]
    public async Task GetSellQuoteAsync_WithRealSolReserves_CalculatesExpectedQuoteAndSlippage()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        var mockRpc = new MockSolanaRpcClient();

        var stateWithSol = new BondingCurveState(
            Mint: ValidMint,
            VirtualSolReserves: 35_000_000_000UL,
            VirtualTokenReserves: 900_000_000_000_000UL,
            RealSolReserves: 5_000_000_000UL, // 5 SOL
            RealTokenReserves: 700_000_000_000_000UL,
            TotalSupply: 1_000_000_000_000_000UL,
            IsComplete: false,
            Bump: 254);

        mockRpc.GetAccountInfoHandler = _ => Task.FromResult<SolanaAccountInfo?>(new SolanaAccountInfo(
            Owner: PlemuMarketTransactionBuilder.DefaultMarketProgramId,
            Data: BondingCurveService.SerializeAccountData(stateWithSol),
            Lamports: 1_000_000_000UL));

        var service = new BondingCurveService(deriver, mockRpc);
        ulong tokensIn = 10_000_000_000UL; // 10,000 tokens

        var quote = await service.GetSellQuoteAsync(ValidMint, tokensIn, slippageBasisPoints: 200);

        Assert.Equal(ValidMint, quote.Mint);
        Assert.False(quote.IsBuy);
        Assert.Equal(tokensIn, quote.InAmount);
        Assert.True(quote.OutAmount > 0);
        Assert.Equal(200UL, quote.SlippageBps);

        // minSolOut must be 98% of outAmount
        ulong expectedMinOut = (ulong)(((UInt128)quote.OutAmount * 9800) / 10000);
        Assert.Equal(expectedMinOut, quote.MinOutAmount);
        Assert.True(quote.SpotPriceBefore >= quote.SpotPriceAfter);
    }

    [Fact]
    public async Task GetMarketDataAsync_ReturnsAccurateMetrics()
    {
        var service = new BondingCurveService(DefaultBondingCurvePdaDeriver.Instance, rpcClient: null);

        var data = await service.GetMarketDataAsync(ValidMint);

        Assert.Equal(ValidMint, data.Mint);
        Assert.True(data.SpotPriceLamports > 0);
        Assert.True(data.MarketCapLamports > 0);
        Assert.Equal(30_000_000_000UL, data.VirtualSolReserves);
        Assert.Equal(1_073_000_000_000_000UL, data.VirtualTokenReserves);
        Assert.False(data.IsComplete);
    }

    [Fact]
    public void BuildBuySwapInstruction_DerivesPdasAndBuildsInstruction()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        var service = new BondingCurveService(deriver);

        ulong solAmount = 1_000_000_000UL;
        ulong minTokens = 30_000_000UL;

        var ix = service.BuildBuySwapInstruction(
            buyer: ValidBuyer,
            mint: ValidMint,
            buyerTokenAccount: ValidTokenAccount,
            solAmountIn: solAmount,
            minTokensOut: minTokens);

        var (expectedCurve, _) = deriver.DeriveBondingCurveAddress(ValidMint);
        var (expectedSolVault, _) = deriver.DeriveCurveSolVaultAddress(ValidMint);
        var (expectedTokenVault, _) = deriver.DeriveCurveTokenVaultAddress(ValidMint);

        Assert.Equal(PlemuMarketTransactionBuilder.DefaultMarketProgramId, ix.ProgramId);
        Assert.Equal(8, ix.Accounts.Count);
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.Equal(ValidMint, ix.Accounts[1].PublicKey);
        Assert.Equal(expectedCurve, ix.Accounts[2].PublicKey);
        Assert.Equal(expectedSolVault, ix.Accounts[3].PublicKey);
        Assert.Equal(expectedTokenVault, ix.Accounts[4].PublicKey);
        Assert.Equal(ValidTokenAccount, ix.Accounts[5].PublicKey);

        Assert.Equal(PlemuMarketTransactionBuilder.SwapBuyDiscriminator, ix.Data.AsSpan(0, 8).ToArray());
        Assert.Equal(solAmount, BitConverter.ToUInt64(ix.Data, 8));
        Assert.Equal(minTokens, BitConverter.ToUInt64(ix.Data, 16));
    }

    [Fact]
    public void BuildSellSwapInstruction_DerivesPdasAndBuildsInstruction()
    {
        var deriver = DefaultBondingCurvePdaDeriver.Instance;
        var service = new BondingCurveService(deriver);

        ulong tokenAmount = 50_000_000UL;
        ulong minSol = 1_500_000UL;

        var ix = service.BuildSellSwapInstruction(
            seller: ValidBuyer,
            mint: ValidMint,
            sellerTokenAccount: ValidTokenAccount,
            tokenAmountIn: tokenAmount,
            minSolOut: minSol);

        var (expectedCurve, _) = deriver.DeriveBondingCurveAddress(ValidMint);

        Assert.Equal(PlemuMarketTransactionBuilder.DefaultMarketProgramId, ix.ProgramId);
        Assert.Equal(8, ix.Accounts.Count);
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.Equal(ValidMint, ix.Accounts[1].PublicKey);
        Assert.Equal(expectedCurve, ix.Accounts[2].PublicKey);

        Assert.Equal(PlemuMarketTransactionBuilder.SwapSellDiscriminator, ix.Data.AsSpan(0, 8).ToArray());
        Assert.Equal(tokenAmount, BitConverter.ToUInt64(ix.Data, 8));
        Assert.Equal(minSol, BitConverter.ToUInt64(ix.Data, 16));
    }

    [Fact]
    public void SimulateLicensePurchaseFeeInjection_IncreasesSpotPriceMonotonically()
    {
        var service = new BondingCurveService(DefaultBondingCurvePdaDeriver.Instance);

        var initialState = new BondingCurveState(
            Mint: ValidMint,
            VirtualSolReserves: 30_000_000_000UL,
            VirtualTokenReserves: 1_000_000_000_000_000UL,
            RealSolReserves: 0UL,
            RealTokenReserves: 800_000_000_000_000UL,
            TotalSupply: 1_000_000_000_000_000UL,
            IsComplete: false,
            Bump: 255);

        ulong licensePriceSol = 50_000_000UL; // 0.05 SOL

        var updatedState = service.SimulateLicensePurchaseFeeInjection(initialState, licensePriceSol, feeBps: 2500);

        // 25% of 50M lamports = 12.5M lamports
        ulong expectedFee = 12_500_000UL;
        Assert.Equal(initialState.VirtualSolReserves + expectedFee, updatedState.VirtualSolReserves);
        Assert.Equal(initialState.RealSolReserves + expectedFee, updatedState.RealSolReserves);
        Assert.Equal(initialState.VirtualTokenReserves, updatedState.VirtualTokenReserves);

        ulong priceBefore = BondingCurveMath.CalculateSpotPriceLamports(initialState.VirtualSolReserves, initialState.VirtualTokenReserves);
        ulong priceAfter = BondingCurveMath.CalculateSpotPriceLamports(updatedState.VirtualSolReserves, initialState.VirtualTokenReserves);
        Assert.True(priceAfter >= priceBefore);

        // With significant fee injection (e.g. 5 SOL license), price strictly increases
        ulong largeLicensePrice = 5_000_000_000UL;
        var stateAfterLargeFee = service.SimulateLicensePurchaseFeeInjection(initialState, largeLicensePrice, feeBps: 2500);
        ulong priceAfterLargeFee = BondingCurveMath.CalculateSpotPriceLamports(stateAfterLargeFee.VirtualSolReserves, stateAfterLargeFee.VirtualTokenReserves);
        Assert.True(priceAfterLargeFee > priceBefore);
    }

    [Theory]
    [InlineData(0UL, 100UL)]
    [InlineData(1_000_000UL, 10_001UL)] // slippage > 100%
    public async Task GetBuyQuoteAsync_InvalidInputs_ThrowsArgumentOutOfRangeException(ulong solIn, ulong slippage)
    {
        var service = new BondingCurveService(DefaultBondingCurvePdaDeriver.Instance);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetBuyQuoteAsync(ValidMint, solIn, slippage));
    }

    [Theory]
    [InlineData(0UL, 100UL)]
    [InlineData(1_000_000UL, 10_001UL)]
    public async Task GetSellQuoteAsync_InvalidInputs_ThrowsArgumentOutOfRangeException(ulong tokensIn, ulong slippage)
    {
        var service = new BondingCurveService(DefaultBondingCurvePdaDeriver.Instance);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetSellQuoteAsync(ValidMint, tokensIn, slippage));
    }

    [Fact]
    public void SerializationRoundtrip_PreservesAllFields()
    {
        var state = new BondingCurveState(
            Mint: ValidMint,
            VirtualSolReserves: 32_000_000_000UL,
            VirtualTokenReserves: 980_000_000_000_000UL,
            RealSolReserves: 2_000_000_000UL,
            RealTokenReserves: 720_000_000_000_000UL,
            TotalSupply: 1_000_000_000_000_000UL,
            IsComplete: true,
            Bump: 253);

        byte[] serialized = BondingCurveService.SerializeAccountData(state);
        Assert.Equal(50, serialized.Length);

        var deserialized = BondingCurveService.DeserializeAccountData(serialized, ValidMint);
        Assert.Equal(state, deserialized);
    }
}
