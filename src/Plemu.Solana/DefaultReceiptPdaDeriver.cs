using System;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana;

public class DefaultReceiptPdaDeriver : IReceiptPdaDeriver
{
    public static DefaultReceiptPdaDeriver Instance { get; } = new();

    private static readonly byte[] SeedReceipt = Encoding.ASCII.GetBytes("receipt");
    private static readonly byte[] PdaMarker = Encoding.ASCII.GetBytes("ProgramDerivedAddress");
    private const int PublicKeyByteLength = 32;

    public string DeriveReceiptAddress(string programId, byte[] artifactSha256, string walletPubkey) =>
        DeriveReceiptAddressInternal(programId, artifactSha256, walletPubkey, 255, 0);

    public string DeriveReceiptAddress(string programId, string entrySha256Hex, string walletPubkey)
    {
        if (string.IsNullOrWhiteSpace(entrySha256Hex))
        {
            throw new ArgumentException("Artifact SHA-256 hex string cannot be null or empty.", nameof(entrySha256Hex));
        }

        byte[] shaBytes;
        try
        {
            shaBytes = Convert.FromHexString(entrySha256Hex);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Artifact SHA-256 must be a valid 64-character hex string.", nameof(entrySha256Hex), ex);
        }

        return DeriveReceiptAddress(programId, shaBytes, walletPubkey);
    }

    internal string DeriveReceiptAddressInternal(string programId, byte[] artifactSha256, string walletPubkey, int startBump, int endBump)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("Program ID cannot be null or empty.", nameof(programId));
        }

        if (artifactSha256 == null || artifactSha256.Length != PublicKeyByteLength)
        {
            throw new ArgumentException("Artifact SHA-256 must be a 32-byte array.", nameof(artifactSha256));
        }

        if (string.IsNullOrWhiteSpace(walletPubkey))
        {
            throw new ArgumentException("Wallet public key cannot be null or empty.", nameof(walletPubkey));
        }

        if (!Base58.TryDecode(programId, out var programIdBytes) || programIdBytes.Length != PublicKeyByteLength)
        {
            throw new ArgumentException("Program ID must be a valid 32-byte Base58 string.", nameof(programId));
        }

        if (!Base58.TryDecode(walletPubkey, out var walletBytes) || walletBytes.Length != PublicKeyByteLength)
        {
            throw new ArgumentException("Wallet public key must be a valid 32-byte Base58 string.", nameof(walletPubkey));
        }

        // Buffer: "receipt" (7) + artifactSha256 (32) + wallet (32) + bump (1) + programId (32) + "ProgramDerivedAddress" (21)
        byte[] buffer = new byte[SeedReceipt.Length + artifactSha256.Length + walletBytes.Length + 1 + programIdBytes.Length + PdaMarker.Length];
        int offset = 0;

        SeedReceipt.CopyTo(buffer, offset);
        offset += SeedReceipt.Length;

        artifactSha256.CopyTo(buffer, offset);
        offset += artifactSha256.Length;

        walletBytes.CopyTo(buffer, offset);
        offset += walletBytes.Length;

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
                return Base58.Encode(hash);
            }
        }

        throw new InvalidOperationException("Could not find a valid Program Derived Address for the given seeds.");
    }
}
