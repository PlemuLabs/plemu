using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Checks pack membership and seat entitlements for the current wallet.
    /// Entitlements are evaluated at manifest acquisition, never at launch.
    /// </summary>
    public interface ISeatProvider
    {
        /// <summary>
        /// Checks if the currently active wallet holds a valid seat for the given pack.
        /// </summary>
        /// <param name="packId">Identifier of the pack.</param>
        Task<bool> HasSeat(string packId);

        /// <summary>
        /// Returns all pack identifiers for which the current wallet holds an active seat.
        /// </summary>
        Task<IReadOnlyList<string>> MyPacks();
    }
}
