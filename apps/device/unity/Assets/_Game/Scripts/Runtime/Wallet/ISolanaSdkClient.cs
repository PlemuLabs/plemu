using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Driver abstraction for interacting with the official Solana.Unity.SDK runtime.
    /// Provides low-level MWA 2.0 communication, session negotiation, and transaction signing.
    /// </summary>
    public interface ISolanaSdkClient
    {
        bool IsAvailable { get; }

        Task<SolanaSdkAccountResult> LoginWalletAdapterAsync(
            string cluster,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken);

        Task<SolanaSdkAccountResult> ReauthorizeWalletAdapterAsync(
            string authToken,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken);

        Task DisconnectWalletAdapterAsync(string authToken, CancellationToken cancellationToken);

        Task<SolanaSdkTransactionResult> SignAndSendTransactionAsync(
            string authToken,
            byte[] serializedTransaction,
            string cluster,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Account authorization outcome from the Solana Unity SDK.
    /// </summary>
    public sealed class SolanaSdkAccountResult
    {
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public string PublicKey { get; }
        public string AuthToken { get; }
        public string ErrorMessage { get; }

        public SolanaSdkAccountResult(string publicKey, string authToken)
        {
            IsSuccess = true;
            IsCancelled = false;
            PublicKey = publicKey;
            AuthToken = authToken;
            ErrorMessage = null;
        }

        private SolanaSdkAccountResult(bool isCancelled, string errorMessage)
        {
            IsSuccess = false;
            IsCancelled = isCancelled;
            PublicKey = null;
            AuthToken = null;
            ErrorMessage = errorMessage;
        }

        public static SolanaSdkAccountResult Success(string publicKey, string authToken) => new SolanaSdkAccountResult(publicKey, authToken);
        public static SolanaSdkAccountResult Cancelled(string reason = "User cancelled login.") => new SolanaSdkAccountResult(true, reason);
        public static SolanaSdkAccountResult Failed(string error) => new SolanaSdkAccountResult(false, error);
    }

    /// <summary>
    /// Transaction signing outcome from the Solana Unity SDK.
    /// </summary>
    public sealed class SolanaSdkTransactionResult
    {
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public string Signature { get; }
        public string ErrorMessage { get; }

        public SolanaSdkTransactionResult(string signature)
        {
            IsSuccess = true;
            IsCancelled = false;
            Signature = signature;
            ErrorMessage = null;
        }

        private SolanaSdkTransactionResult(bool isCancelled, string errorMessage)
        {
            IsSuccess = false;
            IsCancelled = isCancelled;
            Signature = null;
            ErrorMessage = errorMessage;
        }

        public static SolanaSdkTransactionResult Success(string signature) => new SolanaSdkTransactionResult(signature);
        public static SolanaSdkTransactionResult Cancelled(string reason = "User declined transaction signing.") => new SolanaSdkTransactionResult(true, reason);
        public static SolanaSdkTransactionResult Failed(string error) => new SolanaSdkTransactionResult(false, error);
    }
}
