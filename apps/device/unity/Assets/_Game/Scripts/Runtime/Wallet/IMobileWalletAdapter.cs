using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Low-level transport interface for the Solana Mobile Wallet Adapter (MWA 2.0 / Seed Vault).
    /// Handles intent communication, session lifecycle, and transaction signing.
    /// </summary>
    public interface IMobileWalletAdapter
    {
        /// <summary>
        /// Checks whether a compatible MWA wallet / Seed Vault provider is installed and available.
        /// </summary>
        bool IsMwaAvailable();

        /// <summary>
        /// Initiates an MWA authorization session with the wallet.
        /// </summary>
        Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs and sends a serialized Solana transaction payload through the MWA session.
        /// </summary>
        Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);

        /// <summary>
        /// Terminates the current MWA session and purges any session tokens from memory.
        /// </summary>
        Task DisconnectAsync();
    }
}
