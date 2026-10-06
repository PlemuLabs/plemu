namespace Plemu.Solana;

public class DefaultPlaySolanaWalletDriver : IPlaySolanaWalletDriver
{
    private static readonly byte[] DeterministicSignature = new byte[64];

    public Task<string?> RequestPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>("11111111111111111111111111111111");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>((byte[])DeterministicSignature.Clone());
    }

    public Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializedTransaction);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<byte[]?>((byte[])DeterministicSignature.Clone());
    }
}
