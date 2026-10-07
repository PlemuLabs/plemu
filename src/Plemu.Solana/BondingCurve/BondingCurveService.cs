using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Orchestrates game token bonding curve operations including state querying with offline fallback,
/// integer-only buy/sell quotes, fee liquidity simulations, and on-chain swap transaction generation.
/// </summary>
public class BondingCurveService : IBondingCurveService
{
    private const string AnchorAccountPrefix = "account:";
    private const string AccountName = "BondingCurve";
    public const int AccountDataLength = 50;
    public const int MinAccountDataLength = 48;

    public const ulong DefaultVirtualSol = 30_000_000_000UL; // 30 SOL
    public const ulong DefaultVirtualTokens = 1_073_000_000_000_000UL; // ~1.073B base units
    public const ulong DefaultRealTokens = 793_100_000_000_000UL; // ~793.1M base units
    public const ulong DefaultTotalSupply = 1_000_000_000_000_000UL; // 1B base units

    public static byte[] AccountDiscriminator { get; } = ComputeAccountDiscriminator(AccountName);

    private readonly IBondingCurvePdaDeriver _pdaDeriver;
    private readonly ISolanaRpcClient? _rpcClient;
    private readonly string _marketProgramId;

    public BondingCurveService(
        IBondingCurvePdaDeriver pdaDeriver,
        ISolanaRpcClient? rpcClient = null,
        string? marketProgramId = null)
    {
        _pdaDeriver = pdaDeriver ?? throw new ArgumentNullException(nameof(pdaDeriver));
        _rpcClient = rpcClient;
        _marketProgramId = marketProgramId ?? PlemuMarketTransactionBuilder.DefaultMarketProgramId;
    }

    public static byte[] ComputeAccountDiscriminator(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new ArgumentException("Account name cannot be null or empty.", nameof(accountName));
        }

        byte[] preimage = Encoding.UTF8.GetBytes(AnchorAccountPrefix + accountName);
        byte[] hash = SHA256.HashData(preimage);

        byte[] discriminator = new byte[8];
        Array.Copy(hash, 0, discriminator, 0, 8);
        return discriminator;
    }

    public static byte[] SerializeAccountData(BondingCurveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        byte[] data = new byte[AccountDataLength];
        Array.Copy(AccountDiscriminator, 0, data, 0, 8);
        BitConverter.TryWriteBytes(data.AsSpan(8, 8), state.VirtualSolReserves);
        BitConverter.TryWriteBytes(data.AsSpan(16, 8), state.VirtualTokenReserves);
        BitConverter.TryWriteBytes(data.AsSpan(24, 8), state.RealSolReserves);
        BitConverter.TryWriteBytes(data.AsSpan(32, 8), state.RealTokenReserves);
        BitConverter.TryWriteBytes(data.AsSpan(40, 8), state.TotalSupply);
        data[48] = (byte)(state.IsComplete ? 1 : 0);
        data[49] = state.Bump;

        return data;
    }

    public static BondingCurveState DeserializeAccountData(byte[] data, string mint)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < MinAccountDataLength)
        {
            throw new ArgumentException($"Account data length {data.Length} is below minimum {MinAccountDataLength} bytes.", nameof(data));
        }

        if (!data.AsSpan(0, 8).SequenceEqual(AccountDiscriminator))
        {
            throw new ArgumentException("Invalid BondingCurve account discriminator.", nameof(data));
        }

        ulong virtualSol = BitConverter.ToUInt64(data, 8);
        ulong virtualTokens = BitConverter.ToUInt64(data, 16);
        ulong realSol = BitConverter.ToUInt64(data, 24);
        ulong realTokens = BitConverter.ToUInt64(data, 32);
        ulong totalSupply = BitConverter.ToUInt64(data, 40);
        bool isComplete = data.Length > 48 && data[48] != 0;
        byte bump = data.Length > 49 ? data[49] : (byte)0;

        return new BondingCurveState(
            Mint: mint,
            VirtualSolReserves: virtualSol,
            VirtualTokenReserves: virtualTokens,
            RealSolReserves: realSol,
            RealTokenReserves: realTokens,
            TotalSupply: totalSupply,
            IsComplete: isComplete,
            Bump: bump);
    }

    public async Task<BondingCurveState> GetStateAsync(string mint, CancellationToken ct = default)
    {
        var (curveAddress, bump) = _pdaDeriver.DeriveBondingCurveAddress(mint, _marketProgramId);

        if (_rpcClient != null)
        {
            try
            {
                var accountInfo = await _rpcClient.GetAccountInfoAsync(curveAddress, ct).ConfigureAwait(false);
                if (accountInfo?.Data != null && accountInfo.Data.Length >= MinAccountDataLength)
                {
                    if (accountInfo.Data.AsSpan(0, 8).SequenceEqual(AccountDiscriminator))
                    {
                        return DeserializeAccountData(accountInfo.Data, mint);
                    }
                }
            }
            catch
            {
                // Fall back to deterministic genesis state on RPC network or parse error
            }
        }

        return new BondingCurveState(
            Mint: mint,
            VirtualSolReserves: DefaultVirtualSol,
            VirtualTokenReserves: DefaultVirtualTokens,
            RealSolReserves: 0UL,
            RealTokenReserves: DefaultRealTokens,
            TotalSupply: DefaultTotalSupply,
            IsComplete: false,
            Bump: bump);
    }

    public async Task<GameTokenQuote> GetBuyQuoteAsync(
        string mint,
        ulong solAmountIn,
        ulong slippageBasisPoints = 100,
        byte decimals = 6,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(solAmountIn);
        if (slippageBasisPoints > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(slippageBasisPoints), slippageBasisPoints, "Slippage cannot exceed 10000 basis points (100%).");
        }

        var state = await GetStateAsync(mint, ct).ConfigureAwait(false);

        ulong spotPriceBefore = BondingCurveMath.CalculateSpotPriceLamports(state.VirtualSolReserves, state.VirtualTokenReserves, decimals);
        ulong tokensOut = BondingCurveMath.CalculateBuyTokensOut(state.VirtualSolReserves, state.VirtualTokenReserves, state.RealTokenReserves, solAmountIn);
        ulong minTokensOut = (ulong)(((UInt128)tokensOut * (10000 - slippageBasisPoints)) / 10000);

        ulong newSol = checked(state.VirtualSolReserves + solAmountIn);
        ulong newToken = state.VirtualTokenReserves > tokensOut ? state.VirtualTokenReserves - tokensOut : 1UL;
        ulong spotPriceAfter = BondingCurveMath.CalculateSpotPriceLamports(newSol, newToken, decimals);

        ulong priceImpactBps = spotPriceBefore > 0 && spotPriceAfter > spotPriceBefore
            ? (ulong)(((UInt128)(spotPriceAfter - spotPriceBefore) * 10000) / spotPriceBefore)
            : 0;

        return new GameTokenQuote(
            Mint: mint,
            IsBuy: true,
            InAmount: solAmountIn,
            OutAmount: tokensOut,
            MinOutAmount: minTokensOut,
            SlippageBps: slippageBasisPoints,
            SpotPriceBefore: spotPriceBefore,
            SpotPriceAfter: spotPriceAfter,
            PriceImpactBps: priceImpactBps);
    }

    public async Task<GameTokenQuote> GetSellQuoteAsync(
        string mint,
        ulong tokenAmountIn,
        ulong slippageBasisPoints = 100,
        byte decimals = 6,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(tokenAmountIn);
        if (slippageBasisPoints > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(slippageBasisPoints), slippageBasisPoints, "Slippage cannot exceed 10000 basis points (100%).");
        }

        var state = await GetStateAsync(mint, ct).ConfigureAwait(false);

        ulong spotPriceBefore = BondingCurveMath.CalculateSpotPriceLamports(state.VirtualSolReserves, state.VirtualTokenReserves, decimals);
        ulong solOut = BondingCurveMath.CalculateSellSolOut(state.VirtualSolReserves, state.VirtualTokenReserves, state.RealSolReserves, tokenAmountIn);
        ulong minSolOut = (ulong)(((UInt128)solOut * (10000 - slippageBasisPoints)) / 10000);

        ulong newTokens = checked(state.VirtualTokenReserves + tokenAmountIn);
        ulong newSol = state.VirtualSolReserves > solOut ? state.VirtualSolReserves - solOut : 1UL;
        ulong spotPriceAfter = BondingCurveMath.CalculateSpotPriceLamports(newSol, newTokens, decimals);

        ulong priceImpactBps = spotPriceBefore > 0 && spotPriceBefore > spotPriceAfter
            ? (ulong)(((UInt128)(spotPriceBefore - spotPriceAfter) * 10000) / spotPriceBefore)
            : 0;

        return new GameTokenQuote(
            Mint: mint,
            IsBuy: false,
            InAmount: tokenAmountIn,
            OutAmount: solOut,
            MinOutAmount: minSolOut,
            SlippageBps: slippageBasisPoints,
            SpotPriceBefore: spotPriceBefore,
            SpotPriceAfter: spotPriceAfter,
            PriceImpactBps: priceImpactBps);
    }

    public async Task<GameTokenMarketData> GetMarketDataAsync(
        string mint,
        string? symbol = null,
        byte decimals = 6,
        CancellationToken ct = default)
    {
        var state = await GetStateAsync(mint, ct).ConfigureAwait(false);
        ulong spotPrice = BondingCurveMath.CalculateSpotPriceLamports(state.VirtualSolReserves, state.VirtualTokenReserves, decimals);

        UInt128 unitMultiplier = 1;
        for (int i = 0; i < decimals; i++)
        {
            unitMultiplier *= 10;
        }

        ulong marketCap = unitMultiplier > 0
            ? checked((ulong)(((UInt128)spotPrice * state.TotalSupply) / unitMultiplier))
            : 0UL;

        return new GameTokenMarketData(
            Mint: mint,
            Symbol: string.IsNullOrWhiteSpace(symbol) ? "PLAY" : symbol,
            SpotPriceLamports: spotPrice,
            MarketCapLamports: marketCap,
            VirtualSolReserves: state.VirtualSolReserves,
            VirtualTokenReserves: state.VirtualTokenReserves,
            RealSolReserves: state.RealSolReserves,
            RealTokenReserves: state.RealTokenReserves,
            IsComplete: state.IsComplete);
    }

    public SolanaInstruction BuildBuySwapInstruction(
        string buyer,
        string mint,
        string buyerTokenAccount,
        ulong solAmountIn,
        ulong minTokensOut)
    {
        var (curvePda, _) = _pdaDeriver.DeriveBondingCurveAddress(mint, _marketProgramId);
        var (solVaultPda, _) = _pdaDeriver.DeriveCurveSolVaultAddress(mint, _marketProgramId);
        var (tokenVaultPda, _) = _pdaDeriver.DeriveCurveTokenVaultAddress(mint, _marketProgramId);

        return PlemuMarketTransactionBuilder.BuildSwapBuyInstruction(
            buyer: buyer,
            mint: mint,
            bondingCurve: curvePda,
            curveSolVault: solVaultPda,
            curveTokenVault: tokenVaultPda,
            buyerTokenAccount: buyerTokenAccount,
            solAmount: solAmountIn,
            minTokensOut: minTokensOut,
            marketProgramId: _marketProgramId);
    }

    public SolanaInstruction BuildSellSwapInstruction(
        string seller,
        string mint,
        string sellerTokenAccount,
        ulong tokenAmountIn,
        ulong minSolOut)
    {
        var (curvePda, _) = _pdaDeriver.DeriveBondingCurveAddress(mint, _marketProgramId);
        var (solVaultPda, _) = _pdaDeriver.DeriveCurveSolVaultAddress(mint, _marketProgramId);
        var (tokenVaultPda, _) = _pdaDeriver.DeriveCurveTokenVaultAddress(mint, _marketProgramId);

        return PlemuMarketTransactionBuilder.BuildSwapSellInstruction(
            seller: seller,
            mint: mint,
            bondingCurve: curvePda,
            curveSolVault: solVaultPda,
            curveTokenVault: tokenVaultPda,
            sellerTokenAccount: sellerTokenAccount,
            tokenAmount: tokenAmountIn,
            minSolOut: minSolOut,
            marketProgramId: _marketProgramId);
    }

    public BondingCurveState SimulateLicensePurchaseFeeInjection(
        BondingCurveState currentState,
        ulong licensePriceSol,
        ulong feeBps = 2500)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        if (feeBps > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(feeBps), feeBps, "Fee cannot exceed 10000 basis points (100%).");
        }

        ulong feeAmount = (ulong)(((UInt128)licensePriceSol * feeBps) / 10000);
        var (newVirtualSol, newRealSol) = BondingCurveMath.InjectLiquidity(
            currentState.VirtualSolReserves,
            currentState.RealSolReserves,
            feeAmount);

        return currentState with
        {
            VirtualSolReserves = newVirtualSol,
            RealSolReserves = newRealSol
        };
    }
}
