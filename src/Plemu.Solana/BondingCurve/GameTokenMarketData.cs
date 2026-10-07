namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Snapshot of market statistics and liquidity metrics for a game token bonding curve.
/// </summary>
/// <param name="Mint">The base58-encoded public key of the token mint.</param>
/// <param name="Symbol">The ticker symbol of the game token.</param>
/// <param name="SpotPriceLamports">Current spot price in lamports per base token unit.</param>
/// <param name="MarketCapLamports">Implied market capitalization in lamports.</param>
/// <param name="VirtualSolReserves">Virtual SOL reserves in lamports.</param>
/// <param name="VirtualTokenReserves">Virtual token reserves in base token units.</param>
/// <param name="RealSolReserves">Real SOL reserves currently held in the vault in lamports.</param>
/// <param name="RealTokenReserves">Real token reserves currently available for purchase in base token units.</param>
/// <param name="IsComplete">Indicates whether the bonding curve migration threshold has been reached.</param>
public sealed record GameTokenMarketData(
    string Mint,
    string Symbol,
    ulong SpotPriceLamports,
    ulong MarketCapLamports,
    ulong VirtualSolReserves,
    ulong VirtualTokenReserves,
    ulong RealSolReserves,
    ulong RealTokenReserves,
    bool IsComplete);
