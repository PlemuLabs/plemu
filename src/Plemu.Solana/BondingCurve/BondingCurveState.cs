namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Account state representing an on-chain bonding curve for a game token.
/// Layout matches the Anchor on-chain account structure.
/// </summary>
/// <param name="Mint">The base58-encoded public key of the token mint.</param>
/// <param name="VirtualSolReserves">Virtual SOL reserves in lamports.</param>
/// <param name="VirtualTokenReserves">Virtual token reserves in base token units.</param>
/// <param name="RealSolReserves">Real SOL reserves currently held in the curve vault in lamports.</param>
/// <param name="RealTokenReserves">Real token reserves currently available for purchase in base token units.</param>
/// <param name="TotalSupply">Total initial token supply minted for this bonding curve in base token units.</param>
/// <param name="IsComplete">Indicates whether the bonding curve migration threshold has been reached.</param>
/// <param name="Bump">Canonical PDA bump seed used to derive this bonding curve account.</param>
public sealed record BondingCurveState(
    string Mint,
    ulong VirtualSolReserves,
    ulong VirtualTokenReserves,
    ulong RealSolReserves,
    ulong RealTokenReserves,
    ulong TotalSupply,
    bool IsComplete,
    byte Bump);
