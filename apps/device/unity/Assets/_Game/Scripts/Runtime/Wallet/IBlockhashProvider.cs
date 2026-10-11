using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Provides recent Solana blockhashes for transaction serialization.
    /// </summary>
    public interface IBlockhashProvider
    {
        Task<string> GetRecentBlockhashAsync(CancellationToken cancellationToken = default);
    }
}
