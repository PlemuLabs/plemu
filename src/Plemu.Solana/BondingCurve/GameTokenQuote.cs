namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Immutable quote for a game token swap trade (buy or sell) along the bonding curve.
/// </summary>
/// <param name="Mint">The base58-encoded public key of the token mint.</param>
/// <param name="IsBuy">True if the trade is buying tokens with SOL; false if selling tokens for SOL.</param>
/// <param name="InAmount">The input amount (SOL lamports if buy, base tokens if sell).</param>
/// <param name="OutAmount">The estimated output amount (base tokens if buy, SOL lamports if sell).</param>
/// <param name="MinOutAmount">The minimum acceptable output amount after applying slippage tolerance.</param>
/// <param name="SlippageBps">The slippage tolerance in basis points (e.g. 100 = 1.00%).</param>
/// <param name="SpotPriceBefore">The spot price in lamports per base token before execution.</param>
/// <param name="SpotPriceAfter">The expected spot price in lamports per base token after execution.</param>
/// <param name="PriceImpactBps">The price impact in basis points incurred by this trade.</param>
public sealed record GameTokenQuote(
    string Mint,
    bool IsBuy,
    ulong InAmount,
    ulong OutAmount,
    ulong MinOutAmount,
    ulong SlippageBps,
    ulong SpotPriceBefore,
    ulong SpotPriceAfter,
    ulong PriceImpactBps);
