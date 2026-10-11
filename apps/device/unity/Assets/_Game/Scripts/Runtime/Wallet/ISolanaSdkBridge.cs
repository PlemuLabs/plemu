using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Abstraction bridge representing the low-level Solana Unity SDK / MWA 2.0 communication transport.
    /// Enables decoupling of the game's commercial logic and test suites from native platform libraries.
    /// </summary>
    public interface ISolanaSdkBridge
    {
        /// <summary>
        /// Checks whether the Solana SDK and MWA 2.0 subsystem are available on the active platform.
        /// </summary>
        bool IsSdkAvailable();

        /// <summary>
        /// Executes the MWA 2.0 association handshake ("authorize") to obtain an authorized account and auth token.
        /// </summary>
        Task<MwaConnectionResult> AuthorizeAsync(
            string cluster,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reauthorizes an existing MWA session using a cached auth token.
        /// </summary>
        Task<MwaConnectionResult> ReauthorizeAsync(
            string authToken,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deauthorizes the session and terminates the MWA association.
        /// </summary>
        Task DeauthorizeAsync(string authToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a serialized transaction to the wallet via MWA 2.0 for user biometric approval, signing, and Devnet broadcast.
        /// </summary>
        Task<MwaTransactionResult> SignAndSendTransactionAsync(
            string authToken,
            byte[] serializedTransaction,
            string cluster = "devnet",
            CancellationToken cancellationToken = default);
    }
}
