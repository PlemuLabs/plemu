namespace Plemu.Solana;

public record SolanaAccountInfo(
    string Owner,
    byte[] Data,
    ulong Lamports,
    bool Executable = false,
    ulong RentEpoch = 0
);
