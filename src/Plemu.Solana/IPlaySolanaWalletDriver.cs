namespace Plemu.Solana;

public interface IPlaySolanaWalletDriver
{
    Task<string?> RequestPublicKeyAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
