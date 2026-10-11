using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Plemu.Wallet
{
    public sealed class SolanaAccountMeta
    {
        public string PublicKey { get; }
        public bool IsSigner { get; }
        public bool IsWritable { get; }

        public SolanaAccountMeta(string publicKey, bool isSigner, bool isWritable)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                throw new ArgumentException("Public key cannot be null or empty.", nameof(publicKey));
            }

            PublicKey = publicKey;
            IsSigner = isSigner;
            IsWritable = isWritable;
        }
    }

    public sealed class SolanaInstruction
    {
        public string ProgramId { get; }
        public IReadOnlyList<SolanaAccountMeta> Keys { get; }
        public byte[] Data { get; }

        public SolanaInstruction(string programId, IList<SolanaAccountMeta> keys, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(programId))
            {
                throw new ArgumentException("Program ID cannot be null or empty.", nameof(programId));
            }

            ProgramId = programId;
            Keys = keys?.ToList() ?? new List<SolanaAccountMeta>();
            Data = data ?? Array.Empty<byte>();
        }
    }

    public sealed class SolanaMessageHeader
    {
        public byte NumRequiredSignatures { get; }
        public byte NumReadonlySignedAccounts { get; }
        public byte NumReadonlyUnsignedAccounts { get; }

        public SolanaMessageHeader(byte numRequiredSignatures, byte numReadonlySignedAccounts, byte numReadonlyUnsignedAccounts)
        {
            NumRequiredSignatures = numRequiredSignatures;
            NumReadonlySignedAccounts = numReadonlySignedAccounts;
            NumReadonlyUnsignedAccounts = numReadonlyUnsignedAccounts;
        }
    }

    public sealed class SolanaCompiledInstruction
    {
        public byte ProgramIdIndex { get; }
        public byte[] AccountIndices { get; }
        public byte[] Data { get; }

        public SolanaCompiledInstruction(byte programIdIndex, byte[] accountIndices, byte[] data)
        {
            ProgramIdIndex = programIdIndex;
            AccountIndices = accountIndices ?? Array.Empty<byte>();
            Data = data ?? Array.Empty<byte>();
        }
    }

    public sealed class SolanaTransaction
    {
        public IReadOnlyList<byte[]> Signatures { get; }
        public SolanaMessageHeader Header { get; }
        public IReadOnlyList<string> AccountKeys { get; }
        public string RecentBlockhash { get; }
        public IReadOnlyList<SolanaCompiledInstruction> Instructions { get; }

        public SolanaTransaction(
            IList<byte[]> signatures,
            SolanaMessageHeader header,
            IList<string> accountKeys,
            string recentBlockhash,
            IList<SolanaCompiledInstruction> instructions)
        {
            Signatures = signatures?.ToList() ?? new List<byte[]>();
            Header = header ?? throw new ArgumentNullException(nameof(header));
            AccountKeys = accountKeys?.ToList() ?? new List<string>();
            RecentBlockhash = recentBlockhash ?? throw new ArgumentNullException(nameof(recentBlockhash));
            Instructions = instructions?.ToList() ?? new List<SolanaCompiledInstruction>();
        }

        public static SolanaTransaction Create(string feePayer, IEnumerable<SolanaInstruction> instructions, string recentBlockhash)
        {
            if (string.IsNullOrWhiteSpace(feePayer))
            {
                throw new ArgumentException("Fee payer cannot be null or empty.", nameof(feePayer));
            }

            var ixList = instructions?.ToList();
            if (ixList == null || ixList.Count == 0)
            {
                throw new ArgumentException("Transaction must contain at least one instruction.", nameof(instructions));
            }

            if (string.IsNullOrWhiteSpace(recentBlockhash))
            {
                throw new ArgumentException("Recent blockhash cannot be null or empty.", nameof(recentBlockhash));
            }

            string blockhash = recentBlockhash;

            // Collect all unique accounts across instructions
            var accountMap = new Dictionary<string, (bool isSigner, bool isWritable)>();

            // Ensure fee payer is present and marked as signer & writable
            accountMap[feePayer] = (true, true);

            foreach (var ix in ixList)
            {
                foreach (var meta in ix.Keys)
                {
                    if (accountMap.TryGetValue(meta.PublicKey, out var existing))
                    {
                        accountMap[meta.PublicKey] = (existing.isSigner || meta.IsSigner, existing.isWritable || meta.IsWritable);
                    }
                    else
                    {
                        accountMap[meta.PublicKey] = (meta.IsSigner, meta.IsWritable);
                    }
                }

                // Program ID is non-signer and read-only
                if (!accountMap.ContainsKey(ix.ProgramId))
                {
                    accountMap[ix.ProgramId] = (false, false);
                }
            }

            // Categorize into Solana's 4 account groups:
            // 1. Writable signers (fee payer first)
            // 2. Read-only signers
            // 3. Writable non-signers
            // 4. Read-only non-signers
            var writableSigners = new List<string> { feePayer };
            var readonlySigners = new List<string>();
            var writableNonSigners = new List<string>();
            var readonlyNonSigners = new List<string>();

            foreach (var kvp in accountMap)
            {
                if (kvp.Key == feePayer) continue;

                if (kvp.Value.isSigner)
                {
                    if (kvp.Value.isWritable) writableSigners.Add(kvp.Key);
                    else readonlySigners.Add(kvp.Key);
                }
                else
                {
                    if (kvp.Value.isWritable) writableNonSigners.Add(kvp.Key);
                    else readonlyNonSigners.Add(kvp.Key);
                }
            }

            var orderedAccounts = new List<string>();
            orderedAccounts.AddRange(writableSigners);
            orderedAccounts.AddRange(readonlySigners);
            orderedAccounts.AddRange(writableNonSigners);
            orderedAccounts.AddRange(readonlyNonSigners);

            var accountIndices = new Dictionary<string, byte>();
            for (int i = 0; i < orderedAccounts.Count; i++)
            {
                accountIndices[orderedAccounts[i]] = (byte)i;
            }

            var header = new SolanaMessageHeader(
                (byte)(writableSigners.Count + readonlySigners.Count),
                (byte)readonlySigners.Count,
                (byte)readonlyNonSigners.Count);

            var compiledInstructions = new List<SolanaCompiledInstruction>();
            foreach (var ix in ixList)
            {
                byte programIndex = accountIndices[ix.ProgramId];
                byte[] keyIndices = ix.Keys.Select(k => accountIndices[k.PublicKey]).ToArray();
                compiledInstructions.Add(new SolanaCompiledInstruction(programIndex, keyIndices, ix.Data));
            }

            int numSignatures = header.NumRequiredSignatures;
            var signatures = new List<byte[]>(numSignatures);
            for (int i = 0; i < numSignatures; i++)
            {
                signatures.Add(new byte[64]);
            }

            return new SolanaTransaction(signatures, header, orderedAccounts, blockhash, compiledInstructions);
        }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();

            int sigCount = Signatures != null && Signatures.Count > 0 ? Signatures.Count : Header.NumRequiredSignatures;
            SolanaShortvec.Encode(sigCount, ms);

            byte[] scratch = ArrayPool<byte>.Shared.Rent(64);
            try
            {
                for (int i = 0; i < sigCount; i++)
                {
                    if (Signatures != null && i < Signatures.Count && Signatures[i] != null && Signatures[i].Length == 64)
                    {
                        ms.Write(Signatures[i], 0, 64);
                    }
                    else
                    {
                        Array.Clear(scratch, 0, 64);
                        ms.Write(scratch, 0, 64);
                    }
                }

                ms.WriteByte(Header.NumRequiredSignatures);
                ms.WriteByte(Header.NumReadonlySignedAccounts);
                ms.WriteByte(Header.NumReadonlyUnsignedAccounts);

                SolanaShortvec.Encode(AccountKeys.Count, ms);
                foreach (var key in AccountKeys)
                {
                    if (SolanaBase58.TryDecode(key, scratch.AsSpan(0, 32), out int written) && written == 32)
                    {
                        ms.Write(scratch, 0, 32);
                    }
                    else
                    {
                        throw new FormatException($"Account key '{key}' is not a valid 32-byte Base58 encoded public key.");
                    }
                }

                if (SolanaBase58.TryDecode(RecentBlockhash, scratch.AsSpan(0, 32), out int bhWritten) && bhWritten == 32)
                {
                    ms.Write(scratch, 0, 32);
                }
                else
                {
                    throw new FormatException($"RecentBlockhash '{RecentBlockhash}' is not a valid 32-byte Base58 encoded blockhash.");
                }

                SolanaShortvec.Encode(Instructions.Count, ms);
                foreach (var ix in Instructions)
                {
                    ms.WriteByte(ix.ProgramIdIndex);
                    SolanaShortvec.Encode(ix.AccountIndices.Length, ms);
                    ms.Write(ix.AccountIndices, 0, ix.AccountIndices.Length);
                    SolanaShortvec.Encode(ix.Data.Length, ms);
                    ms.Write(ix.Data, 0, ix.Data.Length);
                }

                return ms.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scratch, clearArray: true);
            }
        }

        public static SolanaTransaction Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 68)
            {
                throw new ArgumentException("Transaction bytes are too short to be valid Solana wire format.", nameof(bytes));
            }

            using var ms = new MemoryStream(bytes);

            int sigCount = SolanaShortvec.Decode(ms);
            var signatures = new List<byte[]>(sigCount);
            for (int i = 0; i < sigCount; i++)
            {
                byte[] sig = new byte[64];
                int read = ms.Read(sig, 0, 64);
                if (read < 64)
                {
                    throw new ArgumentException("Truncated signature data.", nameof(bytes));
                }
                signatures.Add(sig);
            }

            int numReqSig = ms.ReadByte();
            int numRoSigned = ms.ReadByte();
            int numRoUnsigned = ms.ReadByte();
            if (numReqSig == -1 || numRoSigned == -1 || numRoUnsigned == -1)
            {
                throw new ArgumentException("Truncated message header.", nameof(bytes));
            }

            var header = new SolanaMessageHeader((byte)numReqSig, (byte)numRoSigned, (byte)numRoUnsigned);

            int numAccounts = SolanaShortvec.Decode(ms);
            var accountKeys = new List<string>(numAccounts);

            byte[] keyBuffer = ArrayPool<byte>.Shared.Rent(32);
            string recentBlockhash;
            try
            {
                for (int i = 0; i < numAccounts; i++)
                {
                    int read = ms.Read(keyBuffer, 0, 32);
                    if (read < 32)
                    {
                        throw new ArgumentException("Truncated account key data.", nameof(bytes));
                    }
                    accountKeys.Add(SolanaBase58.Encode(keyBuffer.AsSpan(0, 32)));
                }

                int bhRead = ms.Read(keyBuffer, 0, 32);
                if (bhRead < 32)
                {
                    throw new ArgumentException("Truncated blockhash data.", nameof(bytes));
                }
                recentBlockhash = SolanaBase58.Encode(keyBuffer.AsSpan(0, 32));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyBuffer, clearArray: true);
            }

            int numInstructions = SolanaShortvec.Decode(ms);
            var instructions = new List<SolanaCompiledInstruction>(numInstructions);
            for (int i = 0; i < numInstructions; i++)
            {
                int progIdIdx = ms.ReadByte();
                if (progIdIdx == -1)
                {
                    throw new ArgumentException("Truncated instruction program ID index.", nameof(bytes));
                }

                int numIxAccounts = SolanaShortvec.Decode(ms);
                byte[] ixAccounts = new byte[numIxAccounts];
                int ixAccRead = ms.Read(ixAccounts, 0, numIxAccounts);
                if (ixAccRead < numIxAccounts)
                {
                    throw new ArgumentException("Truncated instruction account indices.", nameof(bytes));
                }

                int dataLen = SolanaShortvec.Decode(ms);
                byte[] data = new byte[dataLen];
                int dataRead = ms.Read(data, 0, dataLen);
                if (dataRead < dataLen)
                {
                    throw new ArgumentException("Truncated instruction data.", nameof(bytes));
                }

                instructions.Add(new SolanaCompiledInstruction((byte)progIdIdx, ixAccounts, data));
            }

            return new SolanaTransaction(signatures, header, accountKeys, recentBlockhash, instructions);
        }
    }
}
