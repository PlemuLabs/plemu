using System;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana.BondingCurve;

/// <summary>
/// Default implementation of <see cref="IBondingCurvePdaDeriver"/> performing deterministic
/// off-curve SHA-256 PDA derivations conforming to Solana runtime and Anchor specifications.
/// </summary>
public class DefaultBondingCurvePdaDeriver : IBondingCurvePdaDeriver
{
    public static DefaultBondingCurvePdaDeriver Instance { get; } = new();

    private static readonly byte[] SeedBondingCurve = Encoding.ASCII.GetBytes("bonding_curve");
    private static readonly byte[] SeedCurveSolVault = Encoding.ASCII.GetBytes("curve_sol_vault");
    private static readonly byte[] SeedCurveTokenVault = Encoding.ASCII.GetBytes("curve_token_vault");
    private static readonly byte[] PdaMarker = Encoding.ASCII.GetBytes("ProgramDerivedAddress");
    private const int PublicKeyByteLength = 32;

    public (string Address, byte Bump) DeriveBondingCurveAddress(string mint, string? marketProgramId = null)
    {
        var (mintBytes, programIdBytes) = ValidateAndDecodeParameters(mint, marketProgramId);
        return DerivePdaInternal(SeedBondingCurve, mintBytes, programIdBytes);
    }

    public (string Address, byte Bump) DeriveCurveSolVaultAddress(string mint, string? marketProgramId = null)
    {
        var (mintBytes, programIdBytes) = ValidateAndDecodeParameters(mint, marketProgramId);
        return DerivePdaInternal(SeedCurveSolVault, mintBytes, programIdBytes);
    }

    public (string Address, byte Bump) DeriveCurveTokenVaultAddress(string mint, string? marketProgramId = null)
    {
        var (mintBytes, programIdBytes) = ValidateAndDecodeParameters(mint, marketProgramId);
        return DerivePdaInternal(SeedCurveTokenVault, mintBytes, programIdBytes);
    }

    private static (byte[] mintBytes, byte[] programIdBytes) ValidateAndDecodeParameters(string mint, string? marketProgramId)
    {
        if (string.IsNullOrWhiteSpace(mint))
        {
            throw new ArgumentException("Mint cannot be null or empty.", nameof(mint));
        }

        if (!Base58.TryDecode(mint, out var mintBytes) || mintBytes.Length != PublicKeyByteLength)
        {
            throw new ArgumentException("Mint must be a valid 32-byte Base58 string.", nameof(mint));
        }

        string programId = marketProgramId ?? PlemuMarketTransactionBuilder.DefaultMarketProgramId;
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("Program ID cannot be null or empty.", nameof(marketProgramId));
        }

        if (!Base58.TryDecode(programId, out var programIdBytes) || programIdBytes.Length != PublicKeyByteLength)
        {
            throw new ArgumentException("Program ID must be a valid 32-byte Base58 string.", nameof(marketProgramId));
        }

        return (mintBytes, programIdBytes);
    }

    internal (string Address, byte Bump) DerivePdaInternal(
        byte[] seedPrefix,
        byte[] mintBytes,
        byte[] programIdBytes,
        int startBump = 255,
        int endBump = 0)
    {
        // Buffer layout: seedPrefix + mintBytes (32) + bump (1) + programId (32) + "ProgramDerivedAddress" (21)
        byte[] buffer = new byte[seedPrefix.Length + mintBytes.Length + 1 + programIdBytes.Length + PdaMarker.Length];
        int offset = 0;

        seedPrefix.CopyTo(buffer, offset);
        offset += seedPrefix.Length;

        mintBytes.CopyTo(buffer, offset);
        offset += mintBytes.Length;

        int bumpOffset = offset;
        offset += 1;

        programIdBytes.CopyTo(buffer, offset);
        offset += programIdBytes.Length;

        PdaMarker.CopyTo(buffer, offset);

        for (int bump = startBump; bump >= endBump; bump--)
        {
            buffer[bumpOffset] = (byte)bump;
            byte[] hash = SHA256.HashData(buffer);

            if (!DefaultSeatPdaDeriver.IsOnCurve(hash))
            {
                return (Base58.Encode(hash), (byte)bump);
            }
        }

        throw new InvalidOperationException("Could not find a valid Program Derived Address for the given seeds.");
    }
}
