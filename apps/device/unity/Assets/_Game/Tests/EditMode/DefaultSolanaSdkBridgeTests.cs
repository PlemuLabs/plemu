using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class DefaultSolanaSdkBridgeTests
    {
        private class MockSolanaSdkClient : ISolanaSdkClient
        {
            public bool IsAvailable { get; set; } = true;
            public bool ShouldThrowOnLogin { get; set; } = false;
            public bool ShouldThrowOnSign { get; set; } = false;

            public SolanaSdkAccountResult ConfiguredLoginResult { get; set; } =
                SolanaSdkAccountResult.Success("RealClientKey11111111111111111111111111111111", "real-client-auth-token");

            public SolanaSdkAccountResult ConfiguredReauthResult { get; set; } =
                SolanaSdkAccountResult.Success("RealClientKey11111111111111111111111111111111", "real-client-auth-token");

            public SolanaSdkTransactionResult ConfiguredSignResult { get; set; } =
                SolanaSdkTransactionResult.Success("RealSolanaTxSignature1111111111111111111111111111111111111111111111111111111111111111111");

            public int LoginCallCount { get; private set; }
            public int ReauthCallCount { get; private set; }
            public int DisconnectCallCount { get; private set; }
            public int SignCallCount { get; private set; }

            public string LastCluster { get; private set; }
            public string LastIdentityName { get; private set; }
            public string LastIdentityUri { get; private set; }
            public string LastIdentityIcon { get; private set; }
            public string LastAuthToken { get; private set; }
            public byte[] LastPayload { get; private set; }

            public Task<SolanaSdkAccountResult> LoginWalletAdapterAsync(
                string cluster,
                string identityName,
                string identityUri,
                string identityIcon,
                CancellationToken cancellationToken)
            {
                LoginCallCount++;
                LastCluster = cluster;
                LastIdentityName = identityName;
                LastIdentityUri = identityUri;
                LastIdentityIcon = identityIcon;

                if (ShouldThrowOnLogin)
                {
                    throw new InvalidOperationException("Simulated SDK login crash");
                }

                return Task.FromResult(ConfiguredLoginResult);
            }

            public Task<SolanaSdkAccountResult> ReauthorizeWalletAdapterAsync(
                string authToken,
                string identityName,
                string identityUri,
                string identityIcon,
                CancellationToken cancellationToken)
            {
                ReauthCallCount++;
                LastAuthToken = authToken;
                LastIdentityName = identityName;
                LastIdentityUri = identityUri;
                LastIdentityIcon = identityIcon;

                return Task.FromResult(ConfiguredReauthResult);
            }

            public Task DisconnectWalletAdapterAsync(string authToken, CancellationToken cancellationToken)
            {
                DisconnectCallCount++;
                LastAuthToken = authToken;
                return Task.CompletedTask;
            }

            public Task<SolanaSdkTransactionResult> SignAndSendTransactionAsync(
                string authToken,
                byte[] serializedTransaction,
                string cluster,
                CancellationToken cancellationToken)
            {
                SignCallCount++;
                LastAuthToken = authToken;
                LastPayload = serializedTransaction;
                LastCluster = cluster;

                if (ShouldThrowOnSign)
                {
                    throw new InvalidOperationException("Simulated SDK sign crash");
                }

                return Task.FromResult(ConfiguredSignResult);
            }
        }

        [Test]
        public void NonAndroid_DefaultConstructor_IsSdkAvailableReturnsFalse()
        {
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: false);
            Assert.IsFalse(bridge.IsSdkAvailable());
        }

        [Test]
        public async Task NonAndroid_AuthorizeAsync_ReturnsFailedWithoutInvokingClient()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: false, client: mockClient);

            var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("not supported"));
            Assert.AreEqual(0, mockClient.LoginCallCount);
        }

        [Test]
        public async Task NonAndroid_SignAndSendTransactionAsync_ReturnsFailedWithoutInvokingClient()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: false, client: mockClient);

            var result = await bridge.SignAndSendTransactionAsync("token-123", new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("not supported"));
            Assert.AreEqual(0, mockClient.SignCallCount);
        }

        [Test]
        public async Task AuthorizeAsync_WhenCancelled_ReturnsCancelledImmediately()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                Assert.AreEqual(0, mockClient.LoginCallCount);
            }
        }

        [Test]
        public async Task AuthorizeAsync_DelegatesToClient_ReturnsRealPublicKeyAndAuthToken()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("RealClientKey11111111111111111111111111111111", result.PublicKey);
            Assert.AreEqual("real-client-auth-token", result.AuthToken);
            Assert.AreNotEqual("SeekerVaultDevnetPublicKey1111111111111111", result.PublicKey);
            Assert.AreEqual(1, mockClient.LoginCallCount);
            Assert.AreEqual("devnet", mockClient.LastCluster);
            Assert.AreEqual("Plemu", mockClient.LastIdentityName);
        }

        [Test]
        public async Task AuthorizeAsync_WhenClientReturnsCancelled_MapsToCancelledResult()
        {
            var mockClient = new MockSolanaSdkClient
            {
                ConfiguredLoginResult = SolanaSdkAccountResult.Cancelled("User declined login")
            };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsTrue(result.ErrorMessage.Contains("declined"));
        }

        [Test]
        public async Task AuthorizeAsync_WhenClientReturnsFailed_MapsToFailedResult()
        {
            var mockClient = new MockSolanaSdkClient
            {
                ConfiguredLoginResult = SolanaSdkAccountResult.Failed("Connection timed out")
            };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsTrue(result.ErrorMessage.Contains("Connection timed out"));
        }

        [Test]
        public async Task AuthorizeAsync_WhenClientThrowsException_CatchesGracefullyAndReturnsFailed()
        {
            var mockClient = new MockSolanaSdkClient { ShouldThrowOnLogin = true };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.AuthorizeAsync("devnet", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("Simulated SDK login crash"));
        }

        [Test]
        public async Task ReauthorizeAsync_DelegatesToClient_ReturnsOk()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.ReauthorizeAsync("cached-token-456", "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("RealClientKey11111111111111111111111111111111", result.PublicKey);
            Assert.AreEqual(1, mockClient.ReauthCallCount);
            Assert.AreEqual("cached-token-456", mockClient.LastAuthToken);
        }

        [Test]
        public async Task DeauthorizeAsync_DelegatesToClient()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            await bridge.DeauthorizeAsync("active-token-789");

            Assert.AreEqual(1, mockClient.DisconnectCallCount);
            Assert.AreEqual("active-token-789", mockClient.LastAuthToken);
        }

        [Test]
        public async Task SignAndSendTransactionAsync_DelegatesToClient_ReturnsRealSignature()
        {
            var mockClient = new MockSolanaSdkClient();
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            byte[] sampleTx = Encoding.UTF8.GetBytes("sample-transaction-data");
            var result = await bridge.SignAndSendTransactionAsync("auth-token-123", sampleTx, "devnet");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("RealSolanaTxSignature1111111111111111111111111111111111111111111111111111111111111111111", result.Signature);
            Assert.IsFalse(result.Signature.StartsWith("MwaSdkSig-"));
            Assert.AreEqual(1, mockClient.SignCallCount);
            Assert.AreEqual("auth-token-123", mockClient.LastAuthToken);
            Assert.AreEqual(sampleTx, mockClient.LastPayload);
        }

        [Test]
        public async Task SignAndSendTransactionAsync_WhenClientReturnsCancelled_MapsToCancelled()
        {
            var mockClient = new MockSolanaSdkClient
            {
                ConfiguredSignResult = SolanaSdkTransactionResult.Cancelled("User rejected signing")
            };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.SignAndSendTransactionAsync("token-123", new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsTrue(result.ErrorMessage.Contains("rejected"));
        }

        [Test]
        public async Task SignAndSendTransactionAsync_WhenClientReturnsFailed_MapsToFailed()
        {
            var mockClient = new MockSolanaSdkClient
            {
                ConfiguredSignResult = SolanaSdkTransactionResult.Failed("Node rejected transaction: custom program error")
            };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.SignAndSendTransactionAsync("token-123", new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsTrue(result.ErrorMessage.Contains("custom program error"));
        }

        [Test]
        public async Task SignAndSendTransactionAsync_WhenClientThrows_CatchesAndReturnsFailed()
        {
            var mockClient = new MockSolanaSdkClient { ShouldThrowOnSign = true };
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true, client: mockClient);

            var result = await bridge.SignAndSendTransactionAsync("token-123", new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.ErrorMessage.Contains("Simulated SDK sign crash"));
        }

        [Test]
        public void DefaultReflectionClient_WhenSdkNotLoaded_IsSdkAvailableReturnsFalse()
        {
            // Default bridge on simulated Android without mocked client should safely inspect AppDomain
            var bridge = new DefaultSolanaSdkBridge(forcePlatformSupport: true);
            // Since Solana.Unity.SDK is not loaded in this test runner, it should fail closed
            Assert.IsFalse(bridge.IsSdkAvailable());
        }
    }
}
