using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana;

public class DefaultSeatPdaDeriver : ISeatPdaDeriver, IReceiptPdaDeriver
{
    public static DefaultSeatPdaDeriver Instance { get; } = new();

    public string DeriveReceiptAddress(string programId, byte[] artifactSha256, string walletPubkey) =>
        DefaultReceiptPdaDeriver.Instance.DeriveReceiptAddress(programId, artifactSha256, walletPubkey);

    public string DeriveReceiptAddress(string programId, string entrySha256Hex, string walletPubkey) =>
        DefaultReceiptPdaDeriver.Instance.DeriveReceiptAddress(programId, entrySha256Hex, walletPubkey);

    private static readonly byte[] SeedSeat = Encoding.ASCII.GetBytes("seat");
    private static readonly byte[] PdaMarker = Encoding.ASCII.GetBytes("ProgramDerivedAddress");

    // Ed25519 field parameters: p = 2^255 - 19
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger D;
    private static readonly BigInteger Exponent = (P - 1) / 2;

    static DefaultSeatPdaDeriver()
    {
        // d = -121665 * (121666)^(-1) mod p
        BigInteger num = -121665;
        BigInteger den = 121666;
        BigInteger denInv = BigInteger.ModPow(den, P - 2, P);
        D = Mod(num * denInv, P);
    }

    public string DeriveSeatAddress(string programId, string packPubkey, string walletPubkey) =>
        DeriveSeatAddressInternal(programId, packPubkey, walletPubkey, 255, 0);

    internal string DeriveSeatAddressInternal(string programId, string packPubkey, string walletPubkey, int startBump, int endBump)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            throw new ArgumentException("Program ID cannot be null or empty.", nameof(programId));
        }

        if (string.IsNullOrWhiteSpace(packPubkey))
        {
            throw new ArgumentException("Pack public key cannot be null or empty.", nameof(packPubkey));
        }

        if (string.IsNullOrWhiteSpace(walletPubkey))
        {
            throw new ArgumentException("Wallet public key cannot be null or empty.", nameof(walletPubkey));
        }

        if (!Base58.TryDecode(programId, out var programIdBytes) || programIdBytes.Length != 32)
        {
            throw new ArgumentException("Program ID must be a valid 32-byte Base58 string.", nameof(programId));
        }

        if (!Base58.TryDecode(packPubkey, out var packBytes) || packBytes.Length != 32)
        {
            throw new ArgumentException("Pack public key must be a valid 32-byte Base58 string.", nameof(packPubkey));
        }

        if (!Base58.TryDecode(walletPubkey, out var walletBytes) || walletBytes.Length != 32)
        {
            throw new ArgumentException("Wallet public key must be a valid 32-byte Base58 string.", nameof(walletPubkey));
        }

        // Buffer: "seat" (4) + pack (32) + wallet (32) + bump (1) + programId (32) + "ProgramDerivedAddress" (21)
        byte[] buffer = new byte[SeedSeat.Length + packBytes.Length + walletBytes.Length + 1 + programIdBytes.Length + PdaMarker.Length];
        int offset = 0;

        SeedSeat.CopyTo(buffer, offset);
        offset += SeedSeat.Length;

        packBytes.CopyTo(buffer, offset);
        offset += packBytes.Length;

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

            if (!IsOnCurve(hash))
            {
                return Base58.Encode(hash);
            }
        }

        throw new InvalidOperationException("Could not find a valid Program Derived Address for the given seeds.");
    }

    internal static bool IsOnCurve(byte[] bytes)
    {
        if (bytes.Length != 32)
        {
            return false;
        }

        // Little-endian coordinate y with sign bit cleared
        byte[] yBytes = new byte[33];
        Array.Copy(bytes, 0, yBytes, 0, 32);
        yBytes[31] &= 0x7F; // clear highest bit

        BigInteger y = new BigInteger(yBytes, isUnsigned: true, isBigEndian: false);
        if (y >= P)
        {
            return false;
        }

        BigInteger y2 = Mod(y * y, P);
        BigInteger u = Mod(y2 - 1, P);
        BigInteger v = Mod(D * y2 + 1, P);

        BigInteger vInv = BigInteger.ModPow(v, P - 2, P);
        BigInteger w = Mod(u * vInv, P);

        if (w == 0)
        {
            return true;
        }

        BigInteger legendre = BigInteger.ModPow(w, Exponent, P);
        return legendre == 1;
    }

    private static BigInteger Mod(BigInteger a, BigInteger m)
    {
        BigInteger r = a % m;
        return r < 0 ? r + m : r;
    }
}
