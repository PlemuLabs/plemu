using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Default production implementation of IAndroidJniBridge.
    /// Manages the real asynchronous lifecycle of Mobile Wallet Adapter Android intents using TaskCompletionSource,
    /// deep link callbacks, and defensive timeouts. Contains zero hardcoded fake credentials.
    /// </summary>
    public class DefaultAndroidJniBridge : IAndroidJniBridge
    {
        public const string DefaultRedirectUri = "plemu://mwa-callback";
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        private readonly IAndroidActivityInvoker _invoker;
        private readonly IMwaDeepLinkListener _listener;
        private readonly TimeSpan _timeout;
        private readonly SemaphoreSlim _inFlightLock = new SemaphoreSlim(1, 1);

        public DefaultAndroidJniBridge()
            : this(new DefaultAndroidActivityInvoker(), new UnityDeepLinkListener(), DefaultTimeout)
        {
        }

        public DefaultAndroidJniBridge(
            IAndroidActivityInvoker invoker,
            IMwaDeepLinkListener listener,
            TimeSpan? defaultTimeout = null)
        {
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _timeout = defaultTimeout ?? DefaultTimeout;
        }

        public bool IsIntentAvailable(string schemeUri)
        {
            if (string.IsNullOrEmpty(schemeUri))
            {
                return false;
            }

            try
            {
                return _invoker.IsIntentAvailable(schemeUri);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<MwaConnectionResult> LaunchAssociateIntentAsync(
            string uri,
            string dappName,
            string dappUri,
            string dappIcon,
            string cluster,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return MwaConnectionResult.Cancelled("User cancelled connection request.");
            }

            if (!await _inFlightLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return MwaConnectionResult.Failed("An MWA operation is already in progress.");
            }

            try
            {
                if (!IsIntentAvailable(uri))
                {
                    return MwaConnectionResult.Failed("Solana Mobile Wallet Adapter (Seed Vault) intent is not available.");
                }

                var tcs = new TaskCompletionSource<MwaConnectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnDeepLinkReceived(string callbackUrl)
                {
                    try
                    {
                        MwaConnectionResult result = MwaCallbackParser.ParseConnectionCallback(callbackUrl);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetResult(MwaConnectionResult.Failed($"Error parsing callback URL: {ex.Message}"));
                    }
                }

                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linkedCts.CancelAfter(_timeout);

                    using (linkedCts.Token.Register(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            tcs.TrySetResult(MwaConnectionResult.Cancelled("User cancelled connection request."));
                        }
                        else
                        {
                            tcs.TrySetResult(MwaConnectionResult.Cancelled("Wallet association timed out waiting for Seed Vault callback."));
                        }
                    }))
                    {
                        try
                        {
                            _listener.DeepLinkReceived += OnDeepLinkReceived;
                            _listener.StartListening();

                            var extras = new Dictionary<string, string>
                            {
                                { "identity_name", dappName ?? string.Empty },
                                { "identity_uri", dappUri ?? string.Empty },
                                { "identity_icon", dappIcon ?? string.Empty },
                                { "cluster", cluster ?? "devnet" },
                                { "redirect_uri", DefaultRedirectUri }
                            };

                            _invoker.LaunchIntent(uri, extras);

                            return await tcs.Task.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            return MwaConnectionResult.Failed($"Failed to launch association intent: {ex.Message}");
                        }
                        finally
                        {
                            try
                            {
                                _listener.DeepLinkReceived -= OnDeepLinkReceived;
                            }
                            catch (Exception)
                            {
                                // Defensive: ensure unregistration errors do not block stop listening
                            }

                            try
                            {
                                _listener.StopListening();
                            }
                            catch (Exception)
                            {
                                // Defensive: ensure stop errors do not leak unhandled exceptions
                            }
                        }
                    }
                }
            }
            finally
            {
                _inFlightLock.Release();
            }
        }

        public async Task<MwaTransactionResult> LaunchTransactionIntentAsync(
            string uri,
            string authToken,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return MwaTransactionResult.Cancelled("User cancelled transaction.");
            }

            if (!await _inFlightLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return MwaTransactionResult.Failed("An MWA operation is already in progress.");
            }

            try
            {
                if (!IsIntentAvailable(uri))
                {
                    return MwaTransactionResult.Failed("Solana Mobile Wallet Adapter (Seed Vault) intent is not available.");
                }

                var tcs = new TaskCompletionSource<MwaTransactionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnDeepLinkReceived(string callbackUrl)
                {
                    try
                    {
                        MwaTransactionResult result = MwaCallbackParser.ParseTransactionCallback(callbackUrl);
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetResult(MwaTransactionResult.Failed($"Error parsing callback URL: {ex.Message}"));
                    }
                }

                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linkedCts.CancelAfter(_timeout);

                    using (linkedCts.Token.Register(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            tcs.TrySetResult(MwaTransactionResult.Cancelled("User cancelled transaction."));
                        }
                        else
                        {
                            tcs.TrySetResult(MwaTransactionResult.Cancelled("Transaction signing timed out waiting for Seed Vault callback."));
                        }
                    }))
                    {
                        try
                        {
                            _listener.DeepLinkReceived += OnDeepLinkReceived;
                            _listener.StartListening();

                            var extras = new Dictionary<string, string>
                            {
                                { "auth_token", authToken ?? string.Empty },
                                { "payload", payload != null ? Convert.ToBase64String(payload) : string.Empty },
                                { "redirect_uri", DefaultRedirectUri }
                            };

                            _invoker.LaunchIntent(uri, extras);

                            return await tcs.Task.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            return MwaTransactionResult.Failed($"Failed to launch transaction intent: {ex.Message}");
                        }
                        finally
                        {
                            try
                            {
                                _listener.DeepLinkReceived -= OnDeepLinkReceived;
                            }
                            catch (Exception)
                            {
                                // Defensive: ensure unregistration errors do not block stop listening
                            }

                            try
                            {
                                _listener.StopListening();
                            }
                            catch (Exception)
                            {
                                // Defensive: ensure stop errors do not leak unhandled exceptions
                            }
                        }
                    }
                }
            }
            finally
            {
                _inFlightLock.Release();
            }
        }
    }
}
