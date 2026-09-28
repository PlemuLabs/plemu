using System;

namespace Plemu.Core
{
    /// <summary>Minimal, allocation-light hex decoding for fixed-size key material.</summary>
    public static class HexCodec
    {
        /// <summary>
        /// Decodes a lowercase-or-uppercase hex string into bytes. Returns <c>false</c> (not
        /// throw) on odd length or any non-hex character, so callers can map failure to a
        /// typed rejection.
        /// </summary>
        public static bool TryDecode(string hex, out byte[] bytes)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0)
            {
                bytes = null;
                return false;
            }

            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int hi = FromHexDigit(hex[i * 2]);
                int lo = FromHexDigit(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                {
                    bytes = null;
                    return false;
                }

                result[i] = (byte)((hi << 4) | lo);
            }

            bytes = result;
            return true;
        }

        private static int FromHexDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }
    }
}
