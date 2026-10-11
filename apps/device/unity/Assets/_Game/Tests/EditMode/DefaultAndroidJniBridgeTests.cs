using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class DefaultAndroidJniBridgeTests
    {
        private class MockActivityInvoker : IAndroidActivityInvoker
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowOnLaunch { get; set; } = false;
            public string ThrowMessage { get; set; } = "Intent launch failed";

            public string LastUri { get; private set; }
            public Dictionary<string, string> LastExtras { get; private set; }

            public bool IsIntentAvailable(string schemeUri)
            {
                return IsAvailable;
            }

            public void LaunchIntent(string uri, Dictionary<string, string> extras)
            {
                LastUri = uri;
                LastExtras = extras != null ? new Dictionary<string, string>(extras) : null;

                if (ThrowOnLaunch)
                {
                    throw new InvalidOperationException(ThrowMessage);
                }
            }
        }

        private class MockDeepLinkListener : IMwaDeepLinkListener
        {
            public event Action<string> DeepLinkReceived;
            public bool IsListening { get; private set; }
            public bool ThrowOnStartListening { get; set; } = false;
            public bool ThrowOnStopListening { get; set; } = false;
            public bool HasSubscribers => DeepLinkReceived != null;
            public int SubscriberCount => DeepLinkReceived?.GetInvocationList().Length ?? 0;

            public void StartListening()
            {
                if (ThrowOnStartListening)
                {
                    throw new InvalidOperationException("StartListening failed");
                }
                IsListening = true;
            }

            public void StopListening()
            {
                IsListening = false;
                if (ThrowOnStopListening)
                {
                    throw new InvalidOperationException("StopListening failed");
                }
            }

            public void TriggerCallback(string url)
            {
                DeepLinkReceived?.Invoke(url);
            }
        }

        [SetUp]
        public void SetUp()
        {
            UnityEngine.Application.ResetDeepLinkForTests();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Application.ResetDeepLinkForTests();
        }

        [Test]
        public void DefaultConstructor_ImplementsIAndroidJniBridge()
        {
            var bridge = new DefaultAndroidJniBridge();
            Assert.IsInstanceOf<IAndroidJniBridge>(bridge);
        }

        [Test]
        public void NonAndroid_DefaultInvoker_IsIntentAvailableReturnsFalse()
        {
            var bridge = new DefaultAndroidJniBridge();
            // In unit test environment without real Android device, it must return false safely
            Assert.IsFalse(bridge.IsIntentAvailable("solana-wallet://v1/associate"));
        }

        [Test]
        public async Task NonAndroid_DefaultInvoker_LaunchAssociateIntentAsyncReturnsFailed()
        {
            var bridge = new DefaultAndroidJniBridge();
            var result = await bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate",
                "Plemu",
                "https://plemu.gg",
                "https://plemu.gg/icon.png",
                "devnet",
                CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("not available", result.ErrorMessage);
        }

        [Test]
        public async Task NonAndroid_DefaultInvoker_LaunchTransactionIntentAsyncReturnsFailed()
        {
            var bridge = new DefaultAndroidJniBridge();
            var result = await bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions",
                "auth-token-123",
                Encoding.UTF8.GetBytes("test-payload"),
                CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("not available", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenPreCancelled_ReturnsCancelledImmediately()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var result = await bridge.LaunchAssociateIntentAsync(
                    "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
                Assert.IsNull(invoker.LastUri);
            }
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenPreCancelled_ReturnsCancelledImmediately()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var result = await bridge.LaunchTransactionIntentAsync(
                    "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("payload"), cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
                Assert.IsNull(invoker.LastUri);
            }
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_ZeroHardcodedData_AwaitsCallbackViaTaskCompletionSource()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            // Crucial assertion: Before callback arrives, task must NOT be completed immediately!
            Assert.IsFalse(task.IsCompleted, "Task must not return a hardcoded fake result immediately; it must await external callback.");
            Assert.IsTrue(listener.IsListening);
            Assert.IsNotNull(invoker.LastUri);
            Assert.AreEqual("Plemu", invoker.LastExtras["identity_name"]);
            Assert.AreEqual("plemu://mwa-callback", invoker.LastExtras["redirect_uri"]);

            // Simulate delayed callback from Seed Vault
            listener.TriggerCallback("plemu://mwa-callback?public_key=RealSeekerKey8888888888888888888888888888&auth_token=vault-auth-xyz-999");

            var result = await task;
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("RealSeekerKey8888888888888888888888888888", result.PublicKey);
            Assert.AreEqual("vault-auth-xyz-999", result.AuthToken);
            Assert.IsFalse(listener.IsListening, "Listener should be detached after resolution.");
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenUserCancels_ResolvesCancelledGracefully()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsFalse(task.IsCompleted);

            // Simulate Seed Vault returning cancel callback
            listener.TriggerCallback("plemu://mwa-callback?error=user_cancelled&error_message=User+declined+authorization");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("declined", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenBiometricRejected_ResolvesCancelled()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?error=biometric_rejected&error_message=Fingerprint+verification+failed");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("Fingerprint verification failed", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenCallbackContainsGenericError_ResolvesFailed()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?error=internal_vault_error&error_message=KeyStore+unavailable");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("KeyStore unavailable", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenCallbackMalformed_ResolvesFailed()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            // Missing auth_token
            listener.TriggerCallback("plemu://mwa-callback?public_key=OnlyPubKeyProvided");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("Malformed", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenTimeoutOccurs_ResolvesCancelledWithTimeout()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            // Short timeout of 50ms for test
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromMilliseconds(50));

            var result = await bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("timed out", result.ErrorMessage);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenCallerCancelsToken_ResolvesCancelled()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                    "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", cts.Token);

                Assert.IsFalse(task.IsCompleted);
                cts.Cancel();

                var result = await task;
                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
                Assert.IsFalse(listener.IsListening);
            }
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_ZeroHardcodedData_AwaitsCallbackViaTaskCompletionSource()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            byte[] payload = Encoding.UTF8.GetBytes("sample-solana-tx-wire-format");
            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "auth-token-456", payload, CancellationToken.None);

            Assert.IsFalse(task.IsCompleted, "Transaction task must not return a hardcoded fake signature immediately.");
            Assert.IsTrue(listener.IsListening);
            Assert.AreEqual("auth-token-456", invoker.LastExtras["auth_token"]);
            Assert.AreEqual(Convert.ToBase64String(payload), invoker.LastExtras["payload"]);
            Assert.AreEqual("plemu://mwa-callback", invoker.LastExtras["redirect_uri"]);

            // Simulate delayed callback with real signature
            listener.TriggerCallback("plemu://mwa-callback?signature=RealTxSignature55555555555555555555555555555555555555555");

            var result = await task;
            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("RealTxSignature55555555555555555555555555555555555555555", result.Signature);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenUserCancels_ResolvesCancelled()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?error=user_cancelled&error_message=User+declined+transaction");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("declined", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenBiometricRejected_ResolvesCancelled()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?error=biometric_failed&error_message=Biometric+auth+failed");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("Biometric auth failed", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenCallbackContainsGenericError_ResolvesFailed()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?error=cluster_node_offline&error_message=RPC+node+unreachable");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("RPC node unreachable", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenCallbackMalformed_ResolvesFailed()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            // Empty query string
            listener.TriggerCallback("plemu://mwa-callback");

            var result = await task;
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("Malformed", result.ErrorMessage);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenTimeoutOccurs_ResolvesCancelledWithTimeout()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromMilliseconds(50));

            var result = await bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            StringAssert.Contains("timed out", result.ErrorMessage);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenCallerCancelsToken_ResolvesCancelled()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                    "solana-wallet://v1/sign_and_send_transactions", "token", Encoding.UTF8.GetBytes("tx"), cts.Token);

                Assert.IsFalse(task.IsCompleted);
                cts.Cancel();

                var result = await task;
                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
                Assert.IsFalse(listener.IsListening);
            }
        }

        [Test]
        public async Task LaunchIntent_WhenInvokerThrows_CatchesAndReturnsFailed()
        {
            var invoker = new MockActivityInvoker { ThrowOnLaunch = true, ThrowMessage = "ActivityNotFoundException: No wallet app installed" };
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            var result = await bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("ActivityNotFoundException", result.ErrorMessage);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenConcurrentCallAttempted_RejectsWithControlledError()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(10));

            Task<MwaConnectionResult> task1 = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Task<MwaConnectionResult> task2 = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            var completed = await Task.WhenAny(task2, Task.Delay(500));
            Assert.AreEqual(task2, completed, "Concurrent associate call was not rejected immediately.");

            MwaConnectionResult result2 = await task2;
            Assert.IsFalse(result2.IsSuccess);
            Assert.IsFalse(result2.IsCancelled);
            StringAssert.Contains("already in progress", result2.ErrorMessage);

            // Complete first task
            listener.TriggerCallback("plemu://mwa-callback?public_key=SeekerPub111&auth_token=Token222");
            MwaConnectionResult result1 = await task1;
            Assert.IsTrue(result1.IsSuccess);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenConcurrentCallAttempted_RejectsWithControlledError()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(10));

            Task<MwaTransactionResult> task1 = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transaction", "Token111", new byte[] { 1, 2, 3 }, CancellationToken.None);

            Task<MwaTransactionResult> task2 = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transaction", "Token111", new byte[] { 4, 5, 6 }, CancellationToken.None);

            var completed = await Task.WhenAny(task2, Task.Delay(500));
            Assert.AreEqual(task2, completed, "Concurrent transaction call was not rejected immediately.");

            MwaTransactionResult result2 = await task2;
            Assert.IsFalse(result2.IsSuccess);
            Assert.IsFalse(result2.IsCancelled);
            StringAssert.Contains("already in progress", result2.ErrorMessage);

            listener.TriggerCallback("plemu://mwa-callback?signature=TestSig111");
            MwaTransactionResult result1 = await task1;
            Assert.IsTrue(result1.IsSuccess);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_AfterPreviousCompleted_SucceedsSequentially()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(10));

            Task<MwaConnectionResult> task1 = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);
            listener.TriggerCallback("plemu://mwa-callback?public_key=Pub1&auth_token=Tok1");
            var res1 = await task1;
            Assert.IsTrue(res1.IsSuccess);

            Task<MwaConnectionResult> task2 = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);
            listener.TriggerCallback("plemu://mwa-callback?public_key=Pub2&auth_token=Tok2");
            var res2 = await task2;
            Assert.IsTrue(res2.IsSuccess);
        }

        [Test]
        public async Task UnityDeepLinkListener_WhenApplicationDeepLinkTriggered_InvokesEvent()
        {
            var unityListener = new UnityDeepLinkListener();
            string receivedUrl = null;
            unityListener.DeepLinkReceived += url => receivedUrl = url;

            unityListener.StartListening();
            UnityEngine.Application.SimulateDeepLink("plemu://mwa-callback?public_key=abc&auth_token=def");

            Assert.AreEqual("plemu://mwa-callback?public_key=abc&auth_token=def", receivedUrl);

            unityListener.StopListening();
            receivedUrl = null;
            UnityEngine.Application.SimulateDeepLink("plemu://mwa-callback?public_key=ignored");
            Assert.IsNull(receivedUrl);
        }

        [Test]
        public void UnityDeepLinkListener_WhenStartedWithColdStartAbsoluteUrl_InvokesDeepLinkReceivedImmediately()
        {
            UnityEngine.Application.ResetDeepLinkForTests();
            UnityEngine.Application.absoluteURL = "plemu://mwa-callback?public_key=ColdStartPub111&auth_token=ColdStartToken222";

            var unityListener = new UnityDeepLinkListener();
            string receivedUrl = null;
            unityListener.DeepLinkReceived += url => receivedUrl = url;

            unityListener.StartListening();

            Assert.AreEqual("plemu://mwa-callback?public_key=ColdStartPub111&auth_token=ColdStartToken222", receivedUrl);

            unityListener.StopListening();
            UnityEngine.Application.ResetDeepLinkForTests();
        }

        [Test]
        public void UnityDeepLinkListener_ColdStartUrl_ConsumedOnlyOnce()
        {
            UnityEngine.Application.ResetDeepLinkForTests();
            UnityEngine.Application.absoluteURL = "plemu://mwa-callback?public_key=ColdStartOnce&auth_token=TokenOnce";

            var unityListener = new UnityDeepLinkListener();
            int callCount = 0;
            unityListener.DeepLinkReceived += url => callCount++;

            unityListener.StartListening();
            Assert.AreEqual(1, callCount);

            unityListener.StopListening();

            // Starting listening again without new URL should NOT fire cold start URL a second time
            unityListener.StartListening();
            Assert.AreEqual(1, callCount);

            unityListener.StopListening();
            UnityEngine.Application.ResetDeepLinkForTests();
        }

        [Test]
        public void MwaCallbackParser_ParseConnectionCallback_Success()
        {
            var res = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?public_key=SeekerPub111&auth_token=Token222");
            Assert.IsTrue(res.IsSuccess);
            Assert.AreEqual("SeekerPub111", res.PublicKey);
            Assert.AreEqual("Token222", res.AuthToken);
        }

        [Test]
        public void MwaCallbackParser_ParseConnectionCallback_AlternateParamNames()
        {
            var res = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?address=SeekerPubAlternate&token=TokenAlternate");
            Assert.IsTrue(res.IsSuccess);
            Assert.AreEqual("SeekerPubAlternate", res.PublicKey);
            Assert.AreEqual("TokenAlternate", res.AuthToken);
        }

        [Test]
        public void MwaCallbackParser_ParseConnectionCallback_CancellationAndRejections()
        {
            var res1 = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?error=user_cancelled&error_message=User+cancelled");
            Assert.IsFalse(res1.IsSuccess);
            Assert.IsTrue(res1.IsCancelled);

            var res2 = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?error=declined");
            Assert.IsFalse(res2.IsSuccess);
            Assert.IsTrue(res2.IsCancelled);

            var res3 = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?error_code=-32600");
            Assert.IsFalse(res3.IsSuccess);
            Assert.IsTrue(res3.IsCancelled);

            var res4 = MwaCallbackParser.ParseConnectionCallback("plemu://mwa-callback?error_code=4001");
            Assert.IsFalse(res4.IsSuccess);
            Assert.IsTrue(res4.IsCancelled);
        }

        [Test]
        public void MwaCallbackParser_ParseTransactionCallback_Success()
        {
            var res = MwaCallbackParser.ParseTransactionCallback("plemu://mwa-callback?signature=Sig33333333333333333333");
            Assert.IsTrue(res.IsSuccess);
            Assert.AreEqual("Sig33333333333333333333", res.Signature);
        }

        [Test]
        public void MwaCallbackParser_ParseTransactionCallback_Cancellation()
        {
            var res = MwaCallbackParser.ParseTransactionCallback("plemu://mwa-callback?error=biometric_rejected&error_message=Biometric+prompt+declined");
            Assert.IsFalse(res.IsSuccess);
            Assert.IsTrue(res.IsCancelled);
            StringAssert.Contains("Biometric prompt declined", res.ErrorMessage);
        }

        [Test]
        public void MwaCallbackParser_ParseTransactionCallback_Malformed()
        {
            var res = MwaCallbackParser.ParseTransactionCallback("plemu://mwa-callback?random=123");
            Assert.IsFalse(res.IsSuccess);
            Assert.IsFalse(res.IsCancelled);
            StringAssert.Contains("Malformed", res.ErrorMessage);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenSuccessfulCallbackReceived_UnregistersListenerImmediately()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsTrue(listener.HasSubscribers);
            Assert.AreEqual(1, listener.SubscriberCount);

            listener.TriggerCallback("plemu://mwa-callback?public_key=Pub111&auth_token=Token222");
            var result = await task;

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after successful callback.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenTimeoutOccurs_UnregistersListener()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromMilliseconds(50));

            var result = await bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after timeout.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenCancelledByToken_UnregistersListener()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                    "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", cts.Token);

                Assert.IsTrue(listener.HasSubscribers);
                cts.Cancel();
                var result = await task;

                Assert.IsTrue(result.IsCancelled);
                Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after token cancellation.");
                Assert.AreEqual(0, listener.SubscriberCount);
                Assert.IsFalse(listener.IsListening);
            }
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenInvokerThrows_UnregistersListener()
        {
            var invoker = new MockActivityInvoker { ThrowOnLaunch = true };
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            var result = await bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after launch exception.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchAssociateIntentAsync_WhenStopListeningThrows_DefensivelyUnregistersEventWithoutCrashing()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener { ThrowOnStopListening = true };
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaConnectionResult> task = bridge.LaunchAssociateIntentAsync(
                "solana-wallet://v1/associate", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet", CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?public_key=Pub111&auth_token=Token222");
            var result = await task;

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed even if StopListening throws.");
            Assert.AreEqual(0, listener.SubscriberCount);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenSuccessfulCallbackReceived_UnregistersListenerImmediately()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            Assert.IsTrue(listener.HasSubscribers);
            Assert.AreEqual(1, listener.SubscriberCount);

            listener.TriggerCallback("plemu://mwa-callback?signature=Sig999");
            var result = await task;

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after transaction callback.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenTimeoutOccurs_UnregistersListener()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromMilliseconds(50));

            var result = await bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after transaction timeout.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenCancelledByToken_UnregistersListener()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            using (var cts = new CancellationTokenSource())
            {
                Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                    "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("tx"), cts.Token);

                Assert.IsTrue(listener.HasSubscribers);
                cts.Cancel();
                var result = await task;

                Assert.IsTrue(result.IsCancelled);
                Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after transaction cancellation.");
                Assert.AreEqual(0, listener.SubscriberCount);
                Assert.IsFalse(listener.IsListening);
            }
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenInvokerThrows_UnregistersListener()
        {
            var invoker = new MockActivityInvoker { ThrowOnLaunch = true };
            var listener = new MockDeepLinkListener();
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            var result = await bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed after transaction launch exception.");
            Assert.AreEqual(0, listener.SubscriberCount);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public async Task LaunchTransactionIntentAsync_WhenStopListeningThrows_DefensivelyUnregistersEventWithoutCrashing()
        {
            var invoker = new MockActivityInvoker();
            var listener = new MockDeepLinkListener { ThrowOnStopListening = true };
            var bridge = new DefaultAndroidJniBridge(invoker, listener, TimeSpan.FromSeconds(5));

            Task<MwaTransactionResult> task = bridge.LaunchTransactionIntentAsync(
                "solana-wallet://v1/sign_and_send_transactions", "token-1", Encoding.UTF8.GetBytes("tx"), CancellationToken.None);

            listener.TriggerCallback("plemu://mwa-callback?signature=Sig999");
            var result = await task;

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(listener.HasSubscribers, "Listener must be unsubscribed even if StopListening throws.");
            Assert.AreEqual(0, listener.SubscriberCount);
        }
    }
}
