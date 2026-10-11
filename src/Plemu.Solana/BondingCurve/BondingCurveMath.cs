using System;

namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Deterministic integer-only mathematical engine for Solana game token bonding curves.
/// Uses 128-bit unsigned integers (<see cref="UInt128"/>) for cross-multiplication to prevent
/// intermediate overflow and zero floating-point arithmetic to guarantee bytecode consistency with Solana VM.
/// </summary>
public static class BondingCurveMath
{
    /// <summary>
    /// Calculates the number of tokens received when purchasing with a given amount of SOL lamports.
    /// Uses constant product invariant: k = virtualSol * virtualToken.
    /// The resulting tokens are capped at the available real token reserves.
    /// </summary>
    /// <param name="virtualSolReserves">Virtual SOL reserves in lamports.</param>
    /// <param name="virtualTokenReserves">Virtual token reserves in base token units.</param>
    /// <param name="realTokenReserves">Real token reserves in base token units currently held in the vault.</param>
    /// <param name="solAmountIn">SOL amount deposited in lamports.</param>
    /// <returns>Amount of base tokens received.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any parameter is zero.</exception>
    public static ulong CalculateBuyTokensOut(
        ulong virtualSolReserves,
        ulong virtualTokenReserves,
        ulong realTokenReserves,
        ulong solAmountIn)
    {
        ArgumentOutOfRangeException.ThrowIfZero(virtualSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(virtualTokenReserves);
        ArgumentOutOfRangeException.ThrowIfZero(realTokenReserves);
        ArgumentOutOfRangeException.ThrowIfZero(solAmountIn);

        UInt128 k = (UInt128)virtualSolReserves * virtualTokenReserves;
        UInt128 newSol = (UInt128)virtualSolReserves + solAmountIn;
        UInt128 newToken = k / newSol;
        UInt128 tokensOut = (UInt128)virtualTokenReserves - newToken;

        if (tokensOut > realTokenReserves)
        {
            return realTokenReserves;
        }

        return (ulong)tokensOut;
    }

    /// <summary>
    /// Calculates the amount of SOL lamports received when selling a given amount of base tokens.
    /// Uses constant product invariant: k = virtualSol * virtualToken.
    /// The resulting SOL is capped at the available real SOL reserves.
    /// </summary>
    /// <param name="virtualSolReserves">Virtual SOL reserves in lamports.</param>
    /// <param name="virtualTokenReserves">Virtual token reserves in base token units.</param>
    /// <param name="realSolReserves">Real SOL reserves in lamports currently held in the vault.</param>
    /// <param name="tokenAmountIn">Base token units deposited for sale.</param>
    /// <returns>Amount of SOL lamports received.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any parameter is zero.</exception>
    public static ulong CalculateSellSolOut(
        ulong virtualSolReserves,
        ulong virtualTokenReserves,
        ulong realSolReserves,
        ulong tokenAmountIn)
    {
        ArgumentOutOfRangeException.ThrowIfZero(virtualSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(virtualTokenReserves);
        ArgumentOutOfRangeException.ThrowIfZero(realSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(tokenAmountIn);

        UInt128 k = (UInt128)virtualSolReserves * virtualTokenReserves;
        UInt128 newToken = (UInt128)virtualTokenReserves + tokenAmountIn;
        UInt128 newSol = k / newToken;
        UInt128 solOut = (UInt128)virtualSolReserves - newSol;

        if (solOut > realSolReserves)
        {
            return realSolReserves;
        }

        return (ulong)solOut;
    }

    /// <summary>
    /// Calculates the required amount of SOL lamports needed to purchase an exact amount of desired tokens.
    /// Employs ceiling integer division to guarantee that the pool invariant is maintained.
    /// </summary>
    /// <param name="virtualSolReserves">Virtual SOL reserves in lamports.</param>
    /// <param name="virtualTokenReserves">Virtual token reserves in base token units.</param>
    /// <param name="tokensDesired">Amount of base tokens desired.</param>
    /// <returns>Required SOL lamports.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if reserves or desired tokens are zero, or if desired tokens >= virtualTokenReserves.</exception>
    public static ulong CalculateRequiredSolForTokens(
        ulong virtualSolReserves,
        ulong virtualTokenReserves,
        ulong tokensDesired)
    {
        ArgumentOutOfRangeException.ThrowIfZero(virtualSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(virtualTokenReserves);
        ArgumentOutOfRangeException.ThrowIfZero(tokensDesired);

        if (tokensDesired >= virtualTokenReserves)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokensDesired),
                tokensDesired,
                "Tokens desired cannot equal or exceed virtual token reserves.");
        }

        UInt128 newToken = (UInt128)virtualTokenReserves - tokensDesired;
        UInt128 k = (UInt128)virtualSolReserves * virtualTokenReserves;

        // Ceiling integer division: ceil(k / newToken) = (k + newToken - 1) / newToken
        UInt128 newSol = (k + newToken - 1) / newToken;
        UInt128 solRequired = newSol - virtualSolReserves;

        return checked((ulong)solRequired);
    }

    /// <summary>
    /// Calculates the spot price in lamports for one whole token (1,000,000 base units, assuming 6 decimals).
    /// </summary>
    /// <param name="virtualSolReserves">Virtual SOL reserves in lamports.</param>
    /// <param name="virtualTokenReserves">Virtual token reserves in base token units.</param>
    /// <returns>Spot price in lamports per 1 whole token.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if virtualSolReserves or virtualTokenReserves is zero.</exception>
    public static ulong CalculateSpotPriceLamports(
        ulong virtualSolReserves,
        ulong virtualTokenReserves,
        byte decimals = 6)
    {
        ArgumentOutOfRangeException.ThrowIfZero(virtualSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(virtualTokenReserves);
        if (decimals > 18)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Token decimals cannot exceed 18.");
        }

        UInt128 unitMultiplier = 1;
        for (int i = 0; i < decimals; i++)
        {
            unitMultiplier *= 10;
        }

        UInt128 numerator = (UInt128)virtualSolReserves * unitMultiplier;
        UInt128 price = numerator / virtualTokenReserves;

        return checked((ulong)price);
    }

    /// <summary>
    /// Injects fee liquidity directly into the bonding curve without minting tokens (e.g. from game license sales).
    /// Both virtual SOL and real SOL reserves increase monotonically by <paramref name="solFeeAmount"/>.
    /// </summary>
    /// <param name="virtualSolReserves">Current virtual SOL reserves in lamports.</param>
    /// <param name="realSolReserves">Current real SOL reserves in lamports.</param>
    /// <param name="solFeeAmount">SOL fee amount in lamports to inject.</param>
    /// <returns>A tuple with the updated virtual SOL and real SOL reserves.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if virtualSolReserves or solFeeAmount is zero.</exception>
    public static (ulong newVirtualSol, ulong newRealSol) InjectLiquidity(
        ulong virtualSolReserves,
        ulong realSolReserves,
        ulong solFeeAmount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(virtualSolReserves);
        ArgumentOutOfRangeException.ThrowIfZero(solFeeAmount);

        ulong newVirtualSol = checked(virtualSolReserves + solFeeAmount);
        ulong newRealSol = checked(realSolReserves + solFeeAmount);

        return (newVirtualSol, newRealSol);
    }
}
