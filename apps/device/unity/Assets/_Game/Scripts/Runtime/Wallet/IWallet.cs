using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Client-side wallet abstraction for Plemu commercial flows.
    /// Amounts are always PLEMU BASE UNITS (never raw lamports or decimals).
    /// </summary>
    public interface IWallet
    {
        /// <summary>True if the user's wallet is currently connected.</summary>
        bool IsConnected { get; }

        /// <summary>Base58 public key of the connected wallet, or null if disconnected.</summary>
        string PublicKey { get; }

        /// <summary>Requests a connection to the user's wallet.</summary>
        Task<bool> Connect();

        /// <summary>
        /// Sends a direct tip to a recipient in PLEMU base units.
        /// </summary>
        /// <param name="recipientWallet">Base58 recipient public key.</param>
        /// <param name="amountBaseUnits">Amount in base units of the PLEMU token.</param>
        /// <returns>Transaction signature string.</returns>
        Task<string> SendTip(string recipientWallet, ulong amountBaseUnits);

        /// <summary>
        /// Purchases a seat for a private pack.
        /// </summary>
        /// <param name="packId">Identifier of the pack.</param>
        /// <param name="maxPriceBaseUnits">Slippage ceiling price in PLEMU base units.</param>
        /// <returns>Transaction signature string.</returns>
        Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits);

        /// <summary>
        /// Purchases a listing entry from the marketplace.
        /// </summary>
        /// <param name="entryId">Entry identifier.</param>
        /// <param name="maxPriceBaseUnits">Slippage ceiling price in PLEMU base units.</param>
        /// <returns>Transaction signature string.</returns>
        Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits);
    }
}
