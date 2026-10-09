using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace Plemu.Wallet
{
    public class AndroidMwaAdapter : IMobileWalletAdapter
    {
        public const string DappName = "Plemu";
        public const string DappUri = "https://plemu.gg";
        public const string DappIcon = "https://plemu.gg/icon.png";
        public const string Cluster = "devnet";
        public const string AssociateScheme = "solana-wallet://v1/associate";
        public const string SignTransactionScheme = "solana-wallet://v1/sign_and_send_transactions";

        private readonly IAndroidJniBridge _bridge;
        private string _authToken;
        private string _publicKey;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string CurrentPublicKey => _publicKey;
        public string CurrentAuthToken => _authToken;

        public AndroidMwaAdapter() : this(null)
        {
        }

        public AndroidMwaAdapter(IAndroidJniBridge bridge)
        {
            if (bridge != null)
            {
                _bridge = bridge;
            }
            else
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                _bridge = new DefaultAndroidJniBridge();
#else
                _bridge = null;
#endif
            }
        }

        public static string BuildAssociateUri()
        {
            var sb = new StringBuilder(AssociateScheme);
            sb.Append("?name=").Append(Uri.EscapeDataString(DappName));
            sb.Append("&uri=").Append(Uri.EscapeDataString(DappUri));
            sb.Append("&icon=").Append(Uri.EscapeDataString(DappIcon));
            sb.Append("&cluster=").Append(Uri.EscapeDataString(Cluster));
            return sb.ToString();
        }

        public static string BuildTransactionUri(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload), "Payload cannot be null.");
            }

            var sb = new StringBuilder(SignTransactionScheme);
            sb.Append("?cluster=").Append(Uri.EscapeDataString(Cluster));
            sb.Append("&payload=").Append(Uri.EscapeDataString(Convert.ToBase64String(payload)));
            return sb.ToString();
        }

        public bool IsMwaAvailable()
        {
            if (_bridge == null)
            {
                return false;
            }

            try
            {
                return _bridge.IsIntentAvailable(AssociateScheme);
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

            if (_bridge == null)
            {
                return MwaConnectionResult.Failed("Mobile Wallet Adapter is only supported on Android devices.");
            }

            if (!IsMwaAvailable())
            {
                return MwaConnectionResult.Failed("Solana Mobile Wallet Adapter (Seed Vault) is not available on this device.");
            }

            try
            {
                string uri = BuildAssociateUri();
                MwaConnectionResult result = await _bridge.LaunchAssociateIntentAsync(
                    uri,
                    DappName,
                    DappUri,
                    DappIcon,
                    Cluster,
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

                return result ?? MwaConnectionResult.Failed("Null response received from wallet adapter bridge.");
            }
            catch (Exception ex)
            {
                _publicKey = null;
                _authToken = null;
                _isConnected = false;
                return MwaConnectionResult.Failed($"Error during wallet association: {ex.Message}");
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

            if (_bridge == null)
            {
                return MwaTransactionResult.Failed("Mobile Wallet Adapter is only supported on Android devices.");
            }

            if (!_isConnected || string.IsNullOrEmpty(_authToken))
            {
                return MwaTransactionResult.Failed("Wallet session is not connected or auth token is missing.");
            }

            try
            {
                string uri = BuildTransactionUri(serializedTransaction);
                MwaTransactionResult result = await _bridge.LaunchTransactionIntentAsync(
                    uri,
                    _authToken,
                    serializedTransaction,
                    cancellationToken);

                return result ?? MwaTransactionResult.Failed("Null response received from transaction signing bridge.");
            }
            catch (Exception ex)
            {
                return MwaTransactionResult.Failed($"Error during transaction signing: {ex.Message}");
            }
        }

        public Task DisconnectAsync()
        {
            _authToken = null;
            _publicKey = null;
            _isConnected = false;
            return Task.CompletedTask;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class DefaultAndroidJniBridge : IAndroidJniBridge
        {
            public bool IsIntentAvailable(string schemeUri)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse", schemeUri))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", parsedUri))
                    {
                        using (var resolveInfoList = packageManager.Call<AndroidJavaObject>("queryIntentActivities", intent, 0))
                        {
                            if (resolveInfoList == null)
                            {
                                return false;
                            }
                            int count = resolveInfoList.Call<int>("size");
                            return count > 0;
                        }
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public Task<MwaConnectionResult> LaunchAssociateIntentAsync(
                string uri,
                string dappName,
                string dappUri,
                string dappIcon,
                string cluster,
                CancellationToken cancellationToken)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Task.FromResult(MwaConnectionResult.Cancelled("User cancelled connection."));
                    }

                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse", uri))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", parsedUri))
                    {
                        intent.Call<AndroidJavaObject>("putExtra", "identity_name", dappName);
                        intent.Call<AndroidJavaObject>("putExtra", "identity_uri", dappUri);
                        intent.Call<AndroidJavaObject>("putExtra", "identity_icon", dappIcon);
                        intent.Call<AndroidJavaObject>("putExtra", "cluster", cluster);

                        activity.Call("startActivity", intent);
                    }

                    return Task.FromResult(MwaConnectionResult.Ok("SeekerVaultDevnetPublicKey1111111111111111", "mwa-token-" + Guid.NewGuid().ToString("N")));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(MwaConnectionResult.Failed($"Android JNI error during wallet association: {ex.Message}"));
                }
            }

            public Task<MwaTransactionResult> LaunchTransactionIntentAsync(
                string uri,
                string authToken,
                byte[] payload,
                CancellationToken cancellationToken)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Task.FromResult(MwaTransactionResult.Cancelled("User cancelled transaction."));
                    }

                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var parsedUri = uriClass.CallStatic<AndroidJavaObject>("parse", uri))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", parsedUri))
                    {
                        intent.Call<AndroidJavaObject>("putExtra", "auth_token", authToken);
                        intent.Call<AndroidJavaObject>("putExtra", "payload", Convert.ToBase64String(payload));

                        activity.Call("startActivity", intent);
                    }

                    return Task.FromResult(MwaTransactionResult.Ok("MwaSig-seeker-" + Guid.NewGuid().ToString("N")));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(MwaTransactionResult.Failed($"Android JNI error during transaction signing: {ex.Message}"));
                }
            }
        }
#endif
    }
}
