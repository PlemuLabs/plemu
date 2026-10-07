namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Service interface for deriving Program Derived Addresses (PDAs) associated with bonding curve accounts.
/// </summary>
public interface IBondingCurvePdaDeriver
{
    /// <summary>
    /// Derives the canonical bonding curve account address and its bump seed.
    /// Seed layout: ["bonding_curve", mint].
    /// </summary>
    (string Address, byte Bump) DeriveBondingCurveAddress(string mint, string? marketProgramId = null);

    /// <summary>
    /// Derives the SOL vault PDA account address and its bump seed.
    /// Seed layout: ["curve_sol_vault", mint].
    /// </summary>
    (string Address, byte Bump) DeriveCurveSolVaultAddress(string mint, string? marketProgramId = null);

    /// <summary>
    /// Derives the Token vault PDA account address and its bump seed.
    /// Seed layout: ["curve_token_vault", mint].
    /// </summary>
    (string Address, byte Bump) DeriveCurveTokenVaultAddress(string mint, string? marketProgramId = null);
}
