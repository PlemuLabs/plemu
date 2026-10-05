namespace Plemu.Solana;

public class DefaultPlaySolanaWalletDriver : IPlaySolanaWalletDriver
{
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
}
