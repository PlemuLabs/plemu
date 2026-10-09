using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class AndroidMwaAdapterTests
    {
        private sealed class TestJniBridge : IAndroidJniBridge
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowOnIsAvailable { get; set; } = false;

            public string SimulatePublicKey { get; set; } = "SeekerPub1111111111111111111111111111111111";
            public string SimulateAuthToken { get; set; } = "seeker-auth-token-12345";
            public bool SimulateConnectCancel { get; set; } = false;
            public string ConnectCancelReason { get; set; } = "User cancelled wallet association";
            public bool ThrowOnConnect { get; set; } = false;
            public string ConnectErrorMessage { get; set; } = "Fatal JNI association error";

            public string SimulateSignature { get; set; } = "MwaSig-seeker-test-signature-12345";
            public bool SimulateSignCancel { get; set; } = false;
            public string SignCancelReason { get; set; } = "Biometric scan rejected by user";
            public bool ThrowOnSign { get; set; } = false;
            public string SignErrorMessage { get; set; } = "Fatal JNI transaction error";

            public string LastAssociateUri { get; private set; }
            public string LastTransactionUri { get; private set; }
            public byte[] LastPayload { get; private set; }

            public bool IsIntentAvailable(string schemeUri)
            {
                if (ThrowOnIsAvailable)
                {
                    throw new InvalidOperationException("Simulated JNI PackageManager crash.");
                }
                return IsAvailable;
            }

            public Task<MwaConnectionResult> LaunchAssociateIntentAsync(
                string uri,
                string dappName,
                string dappUri,
                string dappIcon,
                string cluster,
                CancellationToken cancellationToken)
            {
                LastAssociateUri = uri;

                if (ThrowOnConnect)
                {
                    throw new InvalidOperationException(ConnectErrorMessage);
                }

                if (cancellationToken.IsCancellationRequested || SimulateConnectCancel)
                {
                    return Task.FromResult(MwaConnectionResult.Cancelled(ConnectCancelReason));
                }

                return Task.FromResult(MwaConnectionResult.Ok(SimulatePublicKey, SimulateAuthToken));
            }

            public Task<MwaTransactionResult> LaunchTransactionIntentAsync(
                string uri,
                string authToken,
                byte[] payload,
                CancellationToken cancellationToken)
            {
                LastTransactionUri = uri;
                LastPayload = payload;

                if (ThrowOnSign)
                {
                    throw new InvalidOperationException(SignErrorMessage);
                }

                if (cancellationToken.IsCancellationRequested || SimulateSignCancel)
                {
                    return Task.FromResult(MwaTransactionResult.Cancelled(SignCancelReason));
                }

                return Task.FromResult(MwaTransactionResult.Ok(SimulateSignature));
            }
        }

        [Test]
        public void AndroidMwaAdapter_ImplementsIMobileWalletAdapter()
        {
            var adapter = new AndroidMwaAdapter();
            Assert.IsInstanceOf<IMobileWalletAdapter>(adapter);
        }

        [Test]
        public void AndroidMwaAdapter_Constants_HaveExpectedDappMetadataAndSchemes()
        {
            Assert.AreEqual("Plemu", AndroidMwaAdapter.DappName);
            Assert.AreEqual("https://plemu.gg", AndroidMwaAdapter.DappUri);
            Assert.AreEqual("https://plemu.gg/icon.png", AndroidMwaAdapter.DappIcon);
            Assert.AreEqual("devnet", AndroidMwaAdapter.Cluster);
            StringAssert.Contains("solana-wallet://v1/associate", AndroidMwaAdapter.AssociateScheme);
            StringAssert.Contains("solana-wallet://v1/sign_and_send_transactions", AndroidMwaAdapter.SignTransactionScheme);
        }

        [Test]
        public void AndroidMwaAdapter_BuildAssociateUri_ContainsExpectedParameters()
        {
            string uri = AndroidMwaAdapter.BuildAssociateUri();

            Assert.IsNotNull(uri);
            StringAssert.Contains("solana-wallet://v1/associate", uri);
            StringAssert.Contains("name=Plemu", uri);
            StringAssert.Contains("cluster=devnet", uri);
            StringAssert.Contains(Uri.EscapeDataString("https://plemu.gg"), uri);
        }

        [Test]
        public void AndroidMwaAdapter_BuildTransactionUri_ContainsExpectedParameters()
        {
            byte[] dummyPayload = Encoding.UTF8.GetBytes("sample-payload");
            string uri = AndroidMwaAdapter.BuildTransactionUri(dummyPayload);

            Assert.IsNotNull(uri);
            StringAssert.Contains("solana-wallet://v1/sign_and_send_transactions", uri);
            StringAssert.Contains("cluster=devnet", uri);
            StringAssert.Contains("payload=", uri);
        }

        [Test]
        public void AndroidMwaAdapter_BuildTransactionUri_WhenPayloadNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AndroidMwaAdapter.BuildTransactionUri(null));
        }

        [Test]
        public void AndroidMwaAdapter_DefaultConstructor_OnNonAndroid_IsMwaAvailableReturnsFalse()
        {
            var adapter = new AndroidMwaAdapter();
            Assert.IsFalse(adapter.IsMwaAvailable());
            Assert.IsFalse(adapter.IsConnected);
            Assert.IsNull(adapter.CurrentPublicKey);
            Assert.IsNull(adapter.CurrentAuthToken);
        }

        [Test]
        public async Task AndroidMwaAdapter_DefaultConstructor_OnNonAndroid_ConnectAsyncReturnsFailedWithoutThrowing()
        {
            var adapter = new AndroidMwaAdapter();
            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            StringAssert.Contains("only supported on Android", result.ErrorMessage);
            Assert.IsFalse(adapter.IsConnected);
        }

        [Test]
        public async Task AndroidMwaAdapter_DefaultConstructor_OnNonAndroid_SignAndSendTransactionAsyncReturnsFailedWithoutThrowing()
        {
            var adapter = new AndroidMwaAdapter();
            byte[] payload = Encoding.UTF8.GetBytes("test");
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.Signature);
            StringAssert.Contains("only supported on Android", result.ErrorMessage);
        }

        [Test]
        public async Task AndroidMwaAdapter_DefaultConstructor_OnNonAndroid_DisconnectAsyncCompletesSafely()
        {
            var adapter = new AndroidMwaAdapter();
            await adapter.DisconnectAsync();
            Assert.IsFalse(adapter.IsConnected);
        }

        [Test]
        public void AndroidMwaAdapter_WithBridge_IsMwaAvailable_ReturnsBridgeResult()
        {
            var bridge = new TestJniBridge { IsAvailable = true };
            var adapter = new AndroidMwaAdapter(bridge);
            Assert.IsTrue(adapter.IsMwaAvailable());

            bridge.IsAvailable = false;
            Assert.IsFalse(adapter.IsMwaAvailable());
        }

        [Test]
        public void AndroidMwaAdapter_WithBridge_IsMwaAvailable_WhenBridgeThrowsException_CatchesAndReturnsFalse()
        {
            var bridge = new TestJniBridge { ThrowOnIsAvailable = true };
            var adapter = new AndroidMwaAdapter(bridge);

            bool available = adapter.IsMwaAvailable();
            Assert.IsFalse(available);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_ConnectAsync_WhenPreCancelled_ReturnsCancelledResult()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                MwaConnectionResult result = await adapter.ConnectAsync(cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
                Assert.IsFalse(adapter.IsConnected);
            }
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_ConnectAsync_WhenMwaNotAvailable_ReturnsFailedWithoutLaunching()
        {
            var bridge = new TestJniBridge { IsAvailable = false };
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("not available", result.ErrorMessage);
            Assert.IsNull(bridge.LastAssociateUri);
            Assert.IsFalse(adapter.IsConnected);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_ConnectAsync_Success_EstablishesSessionAndReturnsOk()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("SeekerPub1111111111111111111111111111111111", result.PublicKey);
            Assert.AreEqual("seeker-auth-token-12345", result.AuthToken);
            Assert.IsNull(result.ErrorMessage);

            Assert.IsTrue(adapter.IsConnected);
            Assert.AreEqual(result.PublicKey, adapter.CurrentPublicKey);
            Assert.AreEqual(result.AuthToken, adapter.CurrentAuthToken);
            Assert.IsNotNull(bridge.LastAssociateUri);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_ConnectAsync_WhenUserCancels_ReturnsCancelledResult()
        {
            var bridge = new TestJniBridge { SimulateConnectCancel = true, ConnectCancelReason = "User pressed back button" };
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.AreEqual("User pressed back button", result.ErrorMessage);
            Assert.IsFalse(adapter.IsConnected);
            Assert.IsNull(adapter.CurrentPublicKey);
            Assert.IsNull(adapter.CurrentAuthToken);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_ConnectAsync_WhenJniThrows_ReturnsFailedWithoutCrashing()
        {
            var bridge = new TestJniBridge { ThrowOnConnect = true, ConnectErrorMessage = "ActivityNotFoundException" };
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("ActivityNotFoundException", result.ErrorMessage);
            Assert.IsFalse(adapter.IsConnected);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_DisconnectAsync_ClearsVolatileSession()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            await adapter.ConnectAsync();
            Assert.IsTrue(adapter.IsConnected);
            Assert.IsNotNull(adapter.CurrentPublicKey);
            Assert.IsNotNull(adapter.CurrentAuthToken);

            await adapter.DisconnectAsync();
            Assert.IsFalse(adapter.IsConnected);
            Assert.IsNull(adapter.CurrentPublicKey);
            Assert.IsNull(adapter.CurrentAuthToken);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenNotConnected_ReturnsFailed()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            byte[] payload = Encoding.UTF8.GetBytes("test-payload");
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("not connected", result.ErrorMessage);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenPayloadNull_ThrowsArgumentNullException()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await adapter.SignAndSendTransactionAsync(null));
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenPayloadEmpty_ThrowsArgumentException()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            await Assert.ThrowsAsync<ArgumentException>(async () => await adapter.SignAndSendTransactionAsync(new byte[0]));
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenPreCancelled_ReturnsCancelledResult()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);
            await adapter.ConnectAsync();

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                byte[] payload = Encoding.UTF8.GetBytes("test-payload");
                MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload, cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                StringAssert.Contains("cancelled", result.ErrorMessage);
            }
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_Success_ReturnsSignature()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);
            await adapter.ConnectAsync();

            byte[] payload = Encoding.UTF8.GetBytes("serialized-tx-bytes");
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("MwaSig-seeker-test-signature-12345", result.Signature);
            Assert.IsNull(result.ErrorMessage);
            Assert.AreEqual(payload, bridge.LastPayload);
            Assert.IsNotNull(bridge.LastTransactionUri);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenUserCancels_ReturnsCancelledResult()
        {
            var bridge = new TestJniBridge { SimulateSignCancel = true, SignCancelReason = "Biometric prompt rejected" };
            var adapter = new AndroidMwaAdapter(bridge);
            await adapter.ConnectAsync();

            byte[] payload = Encoding.UTF8.GetBytes("serialized-tx-bytes");
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.AreEqual("Biometric prompt rejected", result.ErrorMessage);
            Assert.IsNull(result.Signature);
        }

        [Test]
        public async Task AndroidMwaAdapter_WithBridge_SignAndSendTransactionAsync_WhenJniThrows_ReturnsFailedWithoutCrashing()
        {
            var bridge = new TestJniBridge { ThrowOnSign = true, SignErrorMessage = "Intent launch failed with SecurityException" };
            var adapter = new AndroidMwaAdapter(bridge);
            await adapter.ConnectAsync();

            byte[] payload = Encoding.UTF8.GetBytes("serialized-tx-bytes");
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            StringAssert.Contains("SecurityException", result.ErrorMessage);
        }
    }
}
