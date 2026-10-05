using System;
using System.Numerics;
using System.Text;

namespace Plemu.Solana.Cryptography;

public static class Base58
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly sbyte[] Indexes = new sbyte[128];

    static Base58()
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

        byte[] rawBytes = intData > 0 ? intData.ToByteArray(isUnsigned: true, isBigEndian: true) : Array.Empty<byte>();

        byte[] result = new byte[zeros + rawBytes.Length];
        Array.Fill<byte>(result, 0, 0, zeros);
        rawBytes.CopyTo(result, zeros);

        return result;
    }

    public static bool TryDecode(string input, out byte[] result)
    {
        try
        {
            result = Decode(input);
            return true;
        }
        catch (FormatException)
        {
            result = Array.Empty<byte>();
            return false;
        }
    }
}
