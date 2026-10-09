using System;
using System.Text;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// High-level commercial wallet facade implementing IWallet via IMobileWalletAdapter.
    /// Manages fail-closed volatile session state and coordinates Seed Vault transaction signing.
    /// </summary>
    public sealed class MwaWallet : IWallet
    {
        private readonly IMobileWalletAdapter _adapter;
        private string _publicKey;
        private string _authToken;
        private bool _isConnected;

        public MwaWallet(IMobileWalletAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>True if the user's wallet is currently connected.</summary>
        public bool IsConnected => _isConnected;

        /// <summary>Base58 public key of the connected wallet, or null if disconnected.</summary>
        public string PublicKey => _isConnected ? _publicKey : null;

        /// <summary>Requests a connection to the user's wallet through the adapter.</summary>
        public async Task<bool> Connect()
        {
            MwaConnectionResult result = await _adapter.ConnectAsync();
            if (result != null && result.IsSuccess)
            {
                _publicKey = result.PublicKey;
                _authToken = result.AuthToken;
                _isConnected = true;
                return true;
            }

            _publicKey = null;
            _authToken = null;
            _isConnected = false;
            return false;
        }

        /// <summary>
        /// Clears volatile session credentials immediately and notifies the adapter.
        /// </summary>
        public void Disconnect()
        {
            _authToken = null;
            _publicKey = null;
            _isConnected = false;
            _ = _adapter.DisconnectAsync();
        }

        /// <summary>
        /// Clears volatile session credentials immediately and awaits adapter disconnection.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _authToken = null;
            _publicKey = null;
            _isConnected = false;
            await _adapter.DisconnectAsync();
        }

        /// <summary>
        /// Sends a direct tip to a recipient in PLEMU base units.
        /// </summary>
        public async Task<string> SendTip(string recipientWallet, ulong amountBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(recipientWallet))
            {
                throw new ArgumentException("Recipient wallet cannot be null or empty.", nameof(recipientWallet));
            }

            if (amountBaseUnits == 0)
            {
                throw new ArgumentException("Tip amount must be greater than zero.", nameof(amountBaseUnits));
            }

            string payload = $"{{\"type\":\"tip\",\"from\":\"{_publicKey}\",\"to\":\"{recipientWallet}\",\"amount\":{amountBaseUnits}}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
            return HandleTransactionResult(result);
        }

        /// <summary>
        /// Purchases a seat for a private pack.
        /// </summary>
        public async Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("Pack ID cannot be null or empty.", nameof(packId));
            }

            string payload = $"{{\"type\":\"purchase_seat\",\"buyer\":\"{_publicKey}\",\"pack_id\":\"{packId}\",\"max_price\":{maxPriceBaseUnits}}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
            return HandleTransactionResult(result);
        }

        /// <summary>
        /// Purchases a listing entry from the marketplace.
        /// </summary>
        public async Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry ID cannot be null or empty.", nameof(entryId));
            }

            string payload = $"{{\"type\":\"purchase_listing\",\"buyer\":\"{_publicKey}\",\"entry_id\":\"{entryId}\",\"max_price\":{maxPriceBaseUnits}}}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
            return HandleTransactionResult(result);
        }

        private void EnsureConnected()
        {
            if (!_isConnected || string.IsNullOrEmpty(_publicKey))
            {
                throw new InvalidOperationException("Wallet is not connected.");
            }
        }

        private static string HandleTransactionResult(MwaTransactionResult result)
        {
            if (result == null)
            {
                throw new InvalidOperationException("Transaction signing failed: empty response from adapter.");
            }

            if (result.IsSuccess)
            {
                return result.Signature;
            }

            if (result.IsCancelled)
            {
                throw new InvalidOperationException($"Transaction cancelled: {result.ErrorMessage}");
            }

            throw new InvalidOperationException($"Transaction failed: {result.ErrorMessage}");
        }
    }
}
