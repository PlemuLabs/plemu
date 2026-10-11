namespace Plemu.Solana;

public interface ISolanaRpcClient
{
    Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default);
    Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default);
    Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default);
}
