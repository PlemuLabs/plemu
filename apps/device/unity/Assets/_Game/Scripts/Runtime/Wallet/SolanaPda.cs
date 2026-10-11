using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Plemu.Wallet
{
    /// <summary>
    /// Utility for deriving Solana Program Derived Addresses (PDA) and validating off-curve addresses.
    /// </summary>
    public static class SolanaPda
    {
        private static readonly byte[] PdaMarker = Encoding.ASCII.GetBytes("ProgramDerivedAddress");

        // Ed25519 field parameters: p = 2^255 - 19
        private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
        private static readonly BigInteger D;
        private static readonly BigInteger Exponent = (P - 1) / 2;

        static SolanaPda()
        {
            // d = -121665 * (121666)^(-1) mod p
            BigInteger num = -121665;
            BigInteger den = 121666;
            BigInteger denInv = BigInteger.ModPow(den, P - 2, P);
            D = Mod(num * denInv, P);
        }

        /// <summary>
        /// Derives a Program Derived Address (PDA) for given seeds and program ID.
        /// Throws InvalidOperationException if no valid off-curve address can be found.
        /// </summary>
        public static string FindProgramAddress(byte[][] seeds, string programId, out byte bump)
        {
            if (TryFindProgramAddress(seeds, programId, out string pdaAddress, out bump))
            {
                return pdaAddress;
            }

            throw new InvalidOperationException($"Could not find a valid Program Derived Address for program '{programId}'.");
        }

        /// <summary>
        /// Attempts to find a Program Derived Address (PDA) by iterating bump seed from 255 down to 0.
        /// </summary>
        public static bool TryFindProgramAddress(byte[][] seeds, string programId, out string pdaAddress, out byte bump)
        {
            pdaAddress = null;
            bump = 0;

            if (seeds == null)
            {
                throw new ArgumentNullException(nameof(seeds));
            }

            if (seeds.Length == 0 || seeds.Length > 16)
            {
                throw new ArgumentException("Seeds must contain between 1 and 16 seed slices.", nameof(seeds));
            }

            if (string.IsNullOrWhiteSpace(programId))
            {
                throw new ArgumentException("Program ID cannot be null or empty.", nameof(programId));
            }

            byte[] programIdBytes = NormalizeKeyBytes(programId, nameof(programId));

            int totalSeedLength = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                byte[] seed = seeds[i];
                if (seed == null)
                {
                    throw new ArgumentNullException(nameof(seeds), $"Seed at index {i} cannot be null.");
                }

                if (seed.Length > 32)
                {
                    throw new ArgumentException($"Seed at index {i} exceeds max length of 32 bytes.", nameof(seeds));
                }

                totalSeedLength += seed.Length;
            }

            // Buffer: all seeds + bump (1 byte) + programId (32 bytes) + "ProgramDerivedAddress" (21 bytes)
            byte[] buffer = new byte[totalSeedLength + 1 + 32 + PdaMarker.Length];
            int offset = 0;

            for (int i = 0; i < seeds.Length; i++)
            {
                byte[] seed = seeds[i];
                Buffer.BlockCopy(seed, 0, buffer, offset, seed.Length);
                offset += seed.Length;
            }

            int bumpOffset = offset;
            offset += 1;

            Buffer.BlockCopy(programIdBytes, 0, buffer, offset, 32);
            offset += 32;

            Buffer.BlockCopy(PdaMarker, 0, buffer, offset, PdaMarker.Length);

            using var sha = SHA256.Create();

            for (int candidateBump = 255; candidateBump >= 0; candidateBump--)
            {
                buffer[bumpOffset] = (byte)candidateBump;
                byte[] hash = sha.ComputeHash(buffer);

                if (!IsOnCurve(hash))
                {
                    bump = (byte)candidateBump;
                    pdaAddress = SolanaBase58.Encode(hash);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a 32-byte point lies on the Ed25519 elliptic curve.
        /// Program Derived Addresses MUST NOT lie on the curve.
        /// </summary>
        public static bool IsOnCurve(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 32)
            {
                return false;
            }

            // Little-endian coordinate y with sign bit cleared
            byte[] yBytes = new byte[33];
            Array.Copy(bytes, 0, yBytes, 0, 32);
            yBytes[31] &= 0x7F;

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

        private static byte[] NormalizeKeyBytes(string base58Key, string paramName)
        {
            if (!SolanaBase58.TryDecode(base58Key, out byte[] decoded) || decoded == null || decoded.Length == 0)
            {
                throw new ArgumentException($"Invalid Base58 string for parameter '{paramName}'.", paramName);
            }

            if (decoded.Length == 32)
            {
                return decoded;
            }

            if (decoded.Length < 32)
            {
                byte[] padded = new byte[32];
                Array.Copy(decoded, 0, padded, 32 - decoded.Length, decoded.Length);
                return padded;
            }

            throw new ArgumentException($"Parameter '{paramName}' decoded length {decoded.Length} exceeds 32 bytes.", paramName);
        }

        private static BigInteger Mod(BigInteger a, BigInteger m)
        {
            BigInteger r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}
