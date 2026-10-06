namespace Plemu.Solana;

public interface IPlaySolanaWalletDriver
{
    Task<string?> RequestPublicKeyAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default);
    Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
}
