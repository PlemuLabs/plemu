using System.Collections.Generic;

namespace Plemu.Solana;

public sealed record SolanaAccountMeta(string PublicKey, bool IsSigner, bool IsWritable);

public sealed record SolanaInstruction(
    string ProgramId,
    IReadOnlyList<SolanaAccountMeta> Accounts,
    byte[] Data);

public sealed record SolanaTransaction(
    string FeePayer,
    string RecentBlockhash,
    IReadOnlyList<SolanaInstruction> Instructions,
    IReadOnlyList<byte[]>? Signatures = null);
