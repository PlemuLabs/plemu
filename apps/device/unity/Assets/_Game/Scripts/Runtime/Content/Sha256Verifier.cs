using System;
using System.IO;
using System.Security.Cryptography;

namespace Plemu.Content
{
    /// <summary>
    /// Streaming SHA-256 for the per-launch content gate (SPEC §2.3 step 7). Hashes in
    /// 1 MiB chunks via <see cref="IncrementalHash"/> so large artifacts never load fully
    /// into memory, and compares against the manifest's lowercase-hex digest.
    /// </summary>
    public static class Sha256Verifier
    {
        private const int ChunkSize = 1024 * 1024;

        /// <summary>
        /// Computes the lowercase hex SHA-256 of <paramref name="stream"/> from its current
        /// position to end. The stream is read, not seeked back.
        /// </summary>
        public static string ComputeHex(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[ChunkSize];

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
            }

            byte[] digest = hasher.GetHashAndReset();
            return ToLowerHex(digest);
        }

        /// <summary>
        /// True when the streaming digest of <paramref name="stream"/> equals
        /// <paramref name="expectedHex"/>. Comparison is ordinal against the lowercased
        /// expected value, so callers may pass either case.
        /// </summary>
        public static bool Matches(Stream stream, string expectedHex)
        {
            if (string.IsNullOrEmpty(expectedHex))
                return false;

            string actual = ComputeHex(stream);
            return string.Equals(actual, expectedHex.ToLowerInvariant(), StringComparison.Ordinal);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string hexDigits = "0123456789abcdef";
            char[] chars = new char[bytes.Length * 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                chars[i * 2] = hexDigits[b >> 4];
                chars[i * 2 + 1] = hexDigits[b & 0x0F];
            }

            return new string(chars);
        }
    }
}
