using System;
using System.Buffers;
using System.Numerics;
using System.Text;

namespace Plemu.Wallet
{
    /// <summary>
    /// Base58 (Bitcoin alphabet) encoding and decoding for Solana public keys and blockhashes.
    /// Utilizes pooled buffers and spans to avoid heap allocations in IL2CPP ARM64 execution.
    /// </summary>
    public static class SolanaBase58
    {
        private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static readonly sbyte[] Indexes = new sbyte[128];

        static SolanaBase58()
        {
            Array.Fill(Indexes, (sbyte)-1);
            for (int i = 0; i < Alphabet.Length; i++)
            {
                Indexes[Alphabet[i]] = (sbyte)i;
            }
        }

        public static string Encode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            int zeros = 0;
            while (zeros < bytes.Length && bytes[zeros] == 0)
            {
                zeros++;
            }

            ReadOnlySpan<byte> nonZeros = bytes.Slice(zeros);
            if (nonZeros.IsEmpty)
            {
                return new string('1', zeros);
            }

            BigInteger intData = new BigInteger(nonZeros, isUnsigned: true, isBigEndian: true);
            var sb = new StringBuilder();

            while (intData > 0)
            {
                intData = BigInteger.DivRem(intData, 58, out var remainder);
                sb.Append(Alphabet[(int)remainder]);
            }

            for (int i = 0; i < zeros; i++)
            {
                sb.Append('1');
            }

            char[] chars = sb.ToString().ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public static byte[] Decode(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return Array.Empty<byte>();
            }

            int zeros = 0;
            while (zeros < input.Length && input[zeros] == '1')
            {
                zeros++;
            }

            BigInteger intData = BigInteger.Zero;
            for (int i = zeros; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= 128)
                {
                    throw new FormatException($"Invalid Base58 character: {c}");
                }

                int digit = Indexes[c];
                if (digit < 0)
                {
                    throw new FormatException($"Invalid Base58 character: {c}");
                }

                intData = (intData * 58) + digit;
            }

            if (intData == 0)
            {
                return new byte[zeros];
            }

            int rawByteCount = intData.GetByteCount(isUnsigned: true);
            int totalLength = zeros + rawByteCount;

            byte[] rented = ArrayPool<byte>.Shared.Rent(totalLength);
            try
            {
                if (zeros > 0)
                {
                    Array.Clear(rented, 0, zeros);
                }

                if (!intData.TryWriteBytes(rented.AsSpan(zeros, rawByteCount), out int written, isUnsigned: true, isBigEndian: true))
                {
                    byte[] fallback = intData.ToByteArray(isUnsigned: true, isBigEndian: true);
                    fallback.CopyTo(rented, zeros);
                    written = fallback.Length;
                }

                int actualTotal = zeros + written;
                byte[] result = new byte[actualTotal];
                Buffer.BlockCopy(rented, 0, result, 0, actualTotal);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }

        public static int Decode(string input, Span<byte> destination)
        {
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            int zeros = 0;
            while (zeros < input.Length && input[zeros] == '1')
            {
                zeros++;
            }

            BigInteger intData = BigInteger.Zero;
            for (int i = zeros; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= 128)
                {
                    throw new FormatException($"Invalid Base58 character: {c}");
                }

                int digit = Indexes[c];
                if (digit < 0)
                {
                    throw new FormatException($"Invalid Base58 character: {c}");
                }

                intData = (intData * 58) + digit;
            }

            if (intData == 0)
            {
                if (destination.Length < zeros)
                {
                    throw new ArgumentException("Destination span is too small for Base58 decoding.", nameof(destination));
                }
                destination.Slice(0, zeros).Clear();
                return zeros;
            }

            int rawByteCount = intData.GetByteCount(isUnsigned: true);
            int totalLength = zeros + rawByteCount;
            if (destination.Length < totalLength)
            {
                throw new ArgumentException("Destination span is too small for Base58 decoding.", nameof(destination));
            }

            if (zeros > 0)
            {
                destination.Slice(0, zeros).Clear();
            }

            if (intData.TryWriteBytes(destination.Slice(zeros, rawByteCount), out int written, isUnsigned: true, isBigEndian: true))
            {
                return zeros + written;
            }
            else
            {
                byte[] raw = intData.ToByteArray(isUnsigned: true, isBigEndian: true);
                raw.CopyTo(destination.Slice(zeros));
                return zeros + raw.Length;
            }
        }

        public static bool TryDecode(string input, out byte[] result)
        {
            try
            {
                result = Decode(input);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        public static bool TryDecode(string input, Span<byte> destination, out int bytesWritten)
        {
            try
            {
                bytesWritten = Decode(input, destination);
                return true;
            }
            catch
            {
                bytesWritten = 0;
                return false;
            }
        }
    }
}
