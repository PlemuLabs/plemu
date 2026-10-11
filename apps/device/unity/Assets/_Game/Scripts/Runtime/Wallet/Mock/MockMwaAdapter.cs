using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Wallet;

namespace Plemu.Wallet.Mock
{
    /// <summary>
    /// Deterministic mock implementation of IMobileWalletAdapter for Editor, CI,
    /// and non-Android runtime environments without requiring physical Seed Vault hardware.
    /// </summary>
    public sealed class MockMwaAdapter : IMobileWalletAdapter
    {
        private bool _isConnected;
        private string _publicKey;
        private string _authToken;

        public bool IsAvailable { get; set; } = true;
        public bool SimulateUserCancellation { get; set; } = false;
        public bool SimulateFailure { get; set; } = false;
        public string CancellationReason { get; set; } = "User cancelled authorization";
        public string FailureMessage { get; set; } = "Seed Vault authentication failed";
        public const string DefaultMockPublicKey = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        public string MockPublicKey { get; set; } = DefaultMockPublicKey;
        public string MockAuthToken { get; set; } = "mock-mwa-auth-token-devnet";

        public bool IsConnectedSession => _isConnected;
        public string CurrentPublicKey => _publicKey;
        public string CurrentAuthToken => _authToken;

        public bool IsMwaAvailable()
        {
            return IsAvailable;
        }

        public Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(MwaConnectionResult.Cancelled("Operation cancelled"));
            }

            if (!IsAvailable)
            {
                return Task.FromResult(MwaConnectionResult.Failed("Mobile Wallet Adapter is not available on this device"));
            }

            if (SimulateUserCancellation)
            {
                return Task.FromResult(MwaConnectionResult.Cancelled(CancellationReason));
            }

            if (SimulateFailure)
            {
                return Task.FromResult(MwaConnectionResult.Failed(FailureMessage));
            }

            _isConnected = true;
            _publicKey = MockPublicKey;
            _authToken = MockAuthToken;

            return Task.FromResult(MwaConnectionResult.Ok(_publicKey, _authToken));
        }

        public Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(MwaTransactionResult.Cancelled("Operation cancelled"));
            }

            if (!_isConnected)
            {
                return Task.FromResult(MwaTransactionResult.Failed("No active wallet session. Connect before signing."));
            }

            if (serializedTransaction == null || serializedTransaction.Length == 0)
            {
                return Task.FromResult(MwaTransactionResult.Failed("Transaction payload cannot be null or empty"));
            }

            if (SimulateUserCancellation)
            {
                return Task.FromResult(MwaTransactionResult.Cancelled(CancellationReason));
            }

            if (SimulateFailure)
            {
                return Task.FromResult(MwaTransactionResult.Failed(FailureMessage));
            }

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(serializedTransaction);
            string payloadHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            string signature = $"MwaSig-devnet-{payloadHash}";

            return Task.FromResult(MwaTransactionResult.Ok(signature));
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            _publicKey = null;
            _authToken = null;
            return Task.CompletedTask;
        }
    }
}
