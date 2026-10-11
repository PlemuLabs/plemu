using System;
using System.Collections.Generic;

namespace Plemu.Wallet
{
    public static class SolanaSystemProgram
    {
        public const string ProgramId = "11111111111111111111111111111111";

        public static SolanaInstruction Transfer(string fromPublicKey, string toPublicKey, ulong lamports)
        {
            if (string.IsNullOrWhiteSpace(fromPublicKey))
            {
                throw new ArgumentException("Sender public key cannot be null or empty.", nameof(fromPublicKey));
            }

            if (string.IsNullOrWhiteSpace(toPublicKey))
            {
                throw new ArgumentException("Recipient public key cannot be null or empty.", nameof(toPublicKey));
            }

            var keys = new List<SolanaAccountMeta>
            {
                new SolanaAccountMeta(fromPublicKey, isSigner: true, isWritable: true),
                new SolanaAccountMeta(toPublicKey, isSigner: false, isWritable: true)
            };

            byte[] data = new byte[12];
            BitConverter.GetBytes(2u).CopyTo(data, 0); // Instruction index 2 = Transfer
            BitConverter.GetBytes(lamports).CopyTo(data, 4);

            return new SolanaInstruction(ProgramId, keys, data);
        }
    }
}
