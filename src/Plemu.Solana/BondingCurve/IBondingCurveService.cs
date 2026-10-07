using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Solana.BondingCurve;

/// <summary>
/// High-level service interface for querying bonding curve state, retrieving buy/sell quotes,
/// constructing on-chain swap instructions, and simulating fee liquidity injections.
/// </summary>
public interface IBondingCurveService
{
    /// <summary>
    /// Fetches the on-chain bonding curve state for a given game token mint,
    /// or returns the deterministic default/offline genesis state if the account does not exist or RPC is unavailable.
    /// </summary>
    Task<BondingCurveState> GetStateAsync(string mint, CancellationToken ct = default);

    /// <summary>
    /// Calculates a buy swap quote for depositing a given amount of SOL lamports.
    /// </summary>
    Task<GameTokenQuote> GetBuyQuoteAsync(string mint, ulong solAmountIn, ulong slippageBasisPoints = 100, byte decimals = 6, CancellationToken ct = default);

    /// <summary>
    /// Calculates a sell swap quote for depositing a given amount of base token units.
    /// </summary>
    Task<GameTokenQuote> GetSellQuoteAsync(string mint, ulong tokenAmountIn, ulong slippageBasisPoints = 100, byte decimals = 6, CancellationToken ct = default);

    /// <summary>
    /// Retrieves market data and telemetry (spot price, market cap, reserves) for a game token.
    /// </summary>
    Task<GameTokenMarketData> GetMarketDataAsync(string mint, string? symbol = null, byte decimals = 6, CancellationToken ct = default);

    /// <summary>
    /// Compiles a complete Anchor `swap_buy` instruction for a buyer.
    /// </summary>
    SolanaInstruction BuildBuySwapInstruction(string buyer, string mint, string buyerTokenAccount, ulong solAmountIn, ulong minTokensOut);

    /// <summary>
    /// Compiles a complete Anchor `swap_sell` instruction for a seller.
    /// </summary>
    SolanaInstruction BuildSellSwapInstruction(string seller, string mint, string sellerTokenAccount, ulong tokenAmountIn, ulong minSolOut);

    /// <summary>
    /// Simulates fee injection from a game license purchase directly into the bonding curve reserves.
    /// </summary>
    BondingCurveState SimulateLicensePurchaseFeeInjection(BondingCurveState currentState, ulong licensePriceSol, ulong feeBps = 2500);
}
