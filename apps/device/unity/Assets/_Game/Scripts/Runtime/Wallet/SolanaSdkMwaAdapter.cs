using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Official Solana Unity SDK Mobile Wallet Adapter (MWA 2.0) implementation.
    /// Integrates the Magicblock / Solana Mobile SDK via UPM behind the IMobileWalletAdapter facade.
    /// Operates fail-closed: stores auth token and public key strictly in volatile memory.
    /// </summary>
    public class SolanaSdkMwaAdapter : IMobileWalletAdapter
    {
        public const string DappName = "Plemu";
        public const string DappUri = "https://plemu.gg";
        public const string DappIcon = "https://plemu.gg/icon.png";
        public const string Cluster = "devnet";

        private readonly ISolanaSdkBridge _bridge;
        private string _authToken;
        private string _publicKey;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string CurrentPublicKey => _publicKey;
        public string CurrentAuthToken => _authToken;

        public SolanaSdkMwaAdapter() : this(null)
        {
        }

        public SolanaSdkMwaAdapter(ISolanaSdkBridge bridge)
        {
            if (bridge != null)
            {
                _bridge = bridge;
            }
            else
            {
                _bridge = new DefaultSolanaSdkBridge();
            }
        }

        public bool IsMwaAvailable()
        {
            if (_bridge == null)
            {
                return false;
            }

            try
            {
                return _bridge.IsSdkAvailable();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return MwaConnectionResult.Cancelled("Connection request was cancelled by caller.");
            }

            if (_bridge == null || !IsMwaAvailable())
            {
                return MwaConnectionResult.Failed("Solana Unity SDK MWA transport is not available on this platform.");
            }

            try
            {
                MwaConnectionResult result = null;

                // If we already have a volatile auth token, attempt fast reauthorization first
                if (!string.IsNullOrEmpty(_authToken))
                {
                    result = await _bridge.ReauthorizeAsync(
                        _authToken,
                        DappName,
                        DappUri,
                        DappIcon,
                        cancellationToken);

                    if (result != null && result.IsSuccess)
                    {
                        _publicKey = result.PublicKey ?? _publicKey;
                        _authToken = result.AuthToken;
                        _isConnected = true;
                        return result;
                    }
                }

                // Initial connection or fallback after expired reauthorization
                result = await _bridge.AuthorizeAsync(
                    Cluster,
                    DappName,
                    DappUri,
                    DappIcon,
                    cancellationToken);

                if (result != null && result.IsSuccess)
                {
                    _publicKey = result.PublicKey;
                    _authToken = result.AuthToken;
                    _isConnected = true;
                }
                else
                {
                    _publicKey = null;
                    _authToken = null;
                    _isConnected = false;
                }

                return result ?? MwaConnectionResult.Failed("Null response received from Solana SDK bridge.");
            }
            catch (Exception ex)
            {
                _publicKey = null;
                _authToken = null;
                _isConnected = false;
                return MwaConnectionResult.Failed($"Error during Solana SDK authorization: {ex.Message}");
            }
        }

        public async Task<MwaTransactionResult> SignAndSendTransactionAsync(
            byte[] serializedTransaction,
            CancellationToken cancellationToken = default)
        {
            if (serializedTransaction == null)
            {
                throw new ArgumentNullException(nameof(serializedTransaction), "Transaction payload cannot be null.");
            }

            if (serializedTransaction.Length == 0)
            {
                throw new ArgumentException("Transaction payload cannot be empty.", nameof(serializedTransaction));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return MwaTransactionResult.Cancelled("Transaction signing was cancelled by caller.");
            }

            if (_bridge == null || !IsMwaAvailable())
            {
                return MwaTransactionResult.Failed("Solana Unity SDK MWA transport is not available on this platform.");
            }

            if (!_isConnected || string.IsNullOrEmpty(_authToken))
            {
                return MwaTransactionResult.Failed("Wallet session is not connected or auth token is missing.");
            }

            try
            {
                MwaTransactionResult result = await _bridge.SignAndSendTransactionAsync(
                    _authToken,
                    serializedTransaction,
                    Cluster,
                    cancellationToken);

                return result ?? MwaTransactionResult.Failed("Null response received from Solana SDK signing bridge.");
            }
            catch (Exception ex)
            {
                return MwaTransactionResult.Failed($"Error during transaction signing via Solana SDK: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_bridge != null && !string.IsNullOrEmpty(_authToken))
            {
                try
                {
                    await _bridge.DeauthorizeAsync(_authToken);
                }
                catch (Exception)
                {
                    // Fail-safe cleanup
                }
            }

            _authToken = null;
            _publicKey = null;
            _isConnected = false;
        }

        public override string ToString()
        {
            return $"SolanaSdkMwaAdapter(Connected={_isConnected}, PublicKey={_publicKey ?? "null"}, AuthToken=[REDACTED])";
        }
    }
}
