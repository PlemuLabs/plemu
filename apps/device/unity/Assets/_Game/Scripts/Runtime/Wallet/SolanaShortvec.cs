using System;
using System.Buffers;
using System.IO;

namespace Plemu.Wallet
{
    /// <summary>
    /// Compact-u16 (shortvec) variable-length integer serialization used in Solana wire format.
    /// Values between 0 and 127 take 1 byte; 128 to 16383 take 2 bytes; 16384 to 65535 take 3 bytes.
    /// Utilizes pooled buffers and spans to avoid heap allocations in IL2CPP ARM64 execution.
    /// </summary>
    public static class SolanaShortvec
    {
        public static void Encode(int value, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (value < 0 || value > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Compact-u16 value must be between 0 and 65535.");
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                int len = 0;
                int rem = value;
                while (true)
                {
                    byte elem = (byte)(rem & 0x7F);
                    rem >>= 7;
                    if (rem == 0)
                    {
                        buffer[len++] = elem;
                        break;
                    }

                    elem |= 0x80;
                    buffer[len++] = elem;
                }

                stream.Write(buffer, 0, len);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static byte[] Encode(int value)
        {
            if (value < 0 || value > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Compact-u16 value must be between 0 and 65535.");
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                int len = 0;
                int rem = value;
                while (true)
                {
                    byte elem = (byte)(rem & 0x7F);
                    rem >>= 7;
                    if (rem == 0)
                    {
                        buffer[len++] = elem;
                        break;
                    }

                    elem |= 0x80;
                    buffer[len++] = elem;
                }

                byte[] result = new byte[len];
                Buffer.BlockCopy(buffer, 0, result, 0, len);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static int Encode(int value, Span<byte> destination)
        {
            if (value < 0 || value > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Compact-u16 value must be between 0 and 65535.");
            }

            int len = 0;
            int rem = value;
            while (true)
            {
                if (len >= destination.Length)
                {
                    throw new ArgumentException("Destination span is too small for compact-u16 encoding.", nameof(destination));
                }

                byte elem = (byte)(rem & 0x7F);
                rem >>= 7;
                if (rem == 0)
                {
                    destination[len++] = elem;
                    break;
                }

                elem |= 0x80;
                destination[len++] = elem;
            }

            return len;
        }

        public static int Decode(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            int value = 0;
            int shift = 0;
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1)
                {
                    throw new EndOfStreamException("Unexpected end of stream while decoding compact-u16.");
                }

                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
                if (shift > 14)
                {
                    throw new FormatException("Invalid compact-u16 encoding: too many bytes.");
                }
            }

            if (value > 0xFFFF)
            {
                throw new FormatException("Compact-u16 value exceeds 0xFFFF.");
            }

            return value;
        }

        public static int Decode(ReadOnlySpan<byte> bytes, out int bytesRead)
        {
            if (bytes.IsEmpty)
            {
                throw new EndOfStreamException("Unexpected end of span while decoding compact-u16.");
            }

            int value = 0;
            int shift = 0;
            int index = 0;
            while (true)
            {
                if (index >= bytes.Length)
                {
                    throw new EndOfStreamException("Unexpected end of span while decoding compact-u16.");
                }

                byte b = bytes[index++];
                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
                if (shift > 14)
                {
                    throw new FormatException("Invalid compact-u16 encoding: too many bytes.");
                }
            }

            if (value > 0xFFFF)
            {
                throw new FormatException("Compact-u16 value exceeds 0xFFFF.");
            }

            bytesRead = index;
            return value;
        }
    }
}
