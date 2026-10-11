using System;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Default implementation of ISolanaSdkBridge.
    /// Interacts with the native Solana Mobile Wallet Adapter runtime on Android devices via ISolanaSdkClient,
    /// providing fail-closed fallbacks on unsupported desktop/editor platforms.
    /// </summary>
    public class DefaultSolanaSdkBridge : ISolanaSdkBridge
    {
        private readonly bool _isAndroid;
        private readonly ISolanaSdkClient _client;

        public DefaultSolanaSdkBridge() : this(null)
        {
        }

        public DefaultSolanaSdkBridge(ISolanaSdkClient client)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _isAndroid = true;
#else
            _isAndroid = false;
#endif
            _client = client ?? new ReflectionSolanaSdkClient();
        }

        public DefaultSolanaSdkBridge(bool forcePlatformSupport, ISolanaSdkClient client = null)
        {
            _isAndroid = forcePlatformSupport;
            _client = client ?? new ReflectionSolanaSdkClient();
        }

        public bool IsSdkAvailable()
        {
            if (!_isAndroid)
            {
                return false;
            }

            return _client != null && _client.IsAvailable;
        }

        public async Task<MwaConnectionResult> AuthorizeAsync(
            string cluster,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default)
        {
            if (!_isAndroid)
            {
                return MwaConnectionResult.Failed("Solana Unity SDK is not supported on non-Android platforms.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return MwaConnectionResult.Cancelled("User cancelled connection request.");
            }

            if (_client == null || !_client.IsAvailable)
            {
                return MwaConnectionResult.Failed("Solana Unity SDK client is not available in the current runtime.");
            }

            try
            {
                var result = await _client.LoginWalletAdapterAsync(
                    cluster,
                    identityName,
                    identityUri,
                    identityIcon,
                    cancellationToken);

                if (result == null)
                {
                    return MwaConnectionResult.Failed("Solana SDK client returned a null response.");
                }

                if (result.IsCancelled)
                {
                    return MwaConnectionResult.Cancelled(result.ErrorMessage ?? "User cancelled connection request.");
                }

                if (!result.IsSuccess)
                {
                    return MwaConnectionResult.Failed(result.ErrorMessage ?? "Failed to authorize with Solana SDK.");
                }

                return MwaConnectionResult.Ok(result.PublicKey, result.AuthToken);
            }
            catch (OperationCanceledException)
            {
                return MwaConnectionResult.Cancelled("Connection request was cancelled.");
            }
            catch (Exception ex)
            {
                return MwaConnectionResult.Failed($"Solana SDK authorization failed: {ex.Message}");
            }
        }

        public async Task<MwaConnectionResult> ReauthorizeAsync(
            string authToken,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default)
        {
            if (!_isAndroid)
            {
                return MwaConnectionResult.Failed("Solana Unity SDK is not supported on non-Android platforms.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return MwaConnectionResult.Cancelled("User cancelled reauthorization request.");
            }

            if (_client == null || !_client.IsAvailable)
            {
                return MwaConnectionResult.Failed("Solana Unity SDK client is not available in the current runtime.");
            }

            try
            {
                var result = await _client.ReauthorizeWalletAdapterAsync(
                    authToken,
                    identityName,
                    identityUri,
                    identityIcon,
                    cancellationToken);

                if (result == null)
                {
                    return MwaConnectionResult.Failed("Solana SDK client returned a null reauthorization response.");
                }

                if (result.IsCancelled)
                {
                    return MwaConnectionResult.Cancelled(result.ErrorMessage ?? "User cancelled reauthorization request.");
                }

                if (!result.IsSuccess)
                {
                    return MwaConnectionResult.Failed(result.ErrorMessage ?? "Failed to reauthorize with Solana SDK.");
                }

                return MwaConnectionResult.Ok(result.PublicKey, result.AuthToken);
            }
            catch (OperationCanceledException)
            {
                return MwaConnectionResult.Cancelled("Reauthorization request was cancelled.");
            }
            catch (Exception ex)
            {
                return MwaConnectionResult.Failed($"Solana SDK reauthorization failed: {ex.Message}");
            }
        }

        public async Task DeauthorizeAsync(string authToken, CancellationToken cancellationToken = default)
        {
            if (_client != null && _isAndroid)
            {
                try
                {
                    await _client.DisconnectWalletAdapterAsync(authToken, cancellationToken);
                }
                catch (Exception)
                {
                    // Fail-safe cleanup
                }
            }
        }

        public async Task<MwaTransactionResult> SignAndSendTransactionAsync(
            string authToken,
            byte[] serializedTransaction,
            string cluster = "devnet",
            CancellationToken cancellationToken = default)
        {
            if (!_isAndroid)
            {
                return MwaTransactionResult.Failed("Solana Unity SDK is not supported on non-Android platforms.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return MwaTransactionResult.Cancelled("User cancelled transaction signing.");
            }

            if (_client == null || !_client.IsAvailable)
            {
                return MwaTransactionResult.Failed("Solana Unity SDK client is not available in the current runtime.");
            }

            try
            {
                var result = await _client.SignAndSendTransactionAsync(
                    authToken,
                    serializedTransaction,
                    cluster,
                    cancellationToken);

                if (result == null)
                {
                    return MwaTransactionResult.Failed("Solana SDK client returned a null transaction response.");
                }

                if (result.IsCancelled)
                {
                    return MwaTransactionResult.Cancelled(result.ErrorMessage ?? "User cancelled transaction signing.");
                }

                if (!result.IsSuccess)
                {
                    return MwaTransactionResult.Failed(result.ErrorMessage ?? "Transaction rejected or failed in Solana SDK.");
                }

                return MwaTransactionResult.Ok(result.Signature);
            }
            catch (OperationCanceledException)
            {
                return MwaTransactionResult.Cancelled("Transaction signing was cancelled.");
            }
            catch (Exception ex)
            {
                return MwaTransactionResult.Failed($"Transaction signing via Solana SDK failed: {ex.Message}");
            }
        }
    }
}
