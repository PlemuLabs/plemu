using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class WalletFactoryTests
    {
        private class TestJniBridge : IAndroidJniBridge
        {
            public bool SchemeAvailableResult { get; set; } = true;
            public bool ShouldThrowOnSchemeCheck { get; set; } = false;
            public string AssociateAuthToken { get; set; } = "test_auth_token";
            public string AssociatePublicKey { get; set; } = "TestPublicKey111111111111111111111111111";
            public string TransactionSignature { get; set; } = "MwaSig-devnet-TestSignature1111111111111111";

            public bool IsIntentAvailable(string schemeUri)
            {
                if (ShouldThrowOnSchemeCheck)
                {
                    throw new InvalidOperationException("Simulated package manager error");
                }
                return SchemeAvailableResult;
            }

            public Task<MwaConnectionResult> LaunchAssociateIntentAsync(
                string uri,
                string dappName,
                string dappUri,
                string dappIcon,
                string cluster,
                System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(MwaConnectionResult.Ok(AssociatePublicKey, AssociateAuthToken));
            }

            public Task<MwaTransactionResult> LaunchTransactionIntentAsync(
                string uri,
                string authToken,
                byte[] payload,
                System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(MwaTransactionResult.Ok(TransactionSignature));
            }
        }

        [Test]
        public void CreateWallet_DefaultInEditor_ReturnsMwaWallet()
        {
            IWallet wallet = WalletFactory.CreateWallet();

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
            Assert.IsFalse(wallet.IsConnected);
            Assert.IsNull(wallet.PublicKey);
        }

        [Test]
        public async Task CreateWallet_DefaultInEditor_CanConnectAndDisconnect()
        {
            IWallet wallet = WalletFactory.CreateWallet();

            bool connected = await wallet.Connect();
            Assert.IsTrue(connected);
            Assert.IsTrue(wallet.IsConnected);
            Assert.IsNotNull(wallet.PublicKey);
            Assert.IsTrue(wallet.PublicKey.Length > 0);

            if (wallet is MwaWallet mwaWallet)
            {
                mwaWallet.Disconnect();
                Assert.IsFalse(wallet.IsConnected);
                Assert.IsNull(wallet.PublicKey);
            }
            else
            {
                Assert.Fail("Expected MwaWallet instance.");
            }
        }

        [Test]
        public void CreateWallet_WithExplicitAdapter_ReturnsMwaWalletWrappingAdapter()
        {
            var mockAdapter = new MockMwaAdapter();
            IWallet wallet = WalletFactory.CreateWallet(mockAdapter);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void CreateWallet_WithNullExplicitAdapter_FallsBackToDefault()
        {
            IWallet wallet = WalletFactory.CreateWallet(null);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void CreateEditorWallet_ReturnsMwaWalletWithMockAdapter()
        {
            IWallet wallet = WalletFactory.CreateEditorWallet();

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void CreateAndroidWallet_WhenMwaIsAvailable_ReturnsMwaWallet()
        {
            var bridge = new TestJniBridge { SchemeAvailableResult = true };
            IWallet wallet = WalletFactory.CreateAndroidWallet(bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void CreateAndroidWallet_WhenMwaIsNotAvailable_ReturnsMockWalletFallback()
        {
            var bridge = new TestJniBridge { SchemeAvailableResult = false };
            IWallet wallet = WalletFactory.CreateAndroidWallet(bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MockWallet>(wallet);
        }

        [Test]
        public void CreateAndroidWallet_WhenBridgeIsNull_ReturnsMockWalletFallback()
        {
            IWallet wallet = WalletFactory.CreateAndroidWallet(null);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MockWallet>(wallet);
        }

        [Test]
        public void CreateAndroidWallet_WhenBridgeThrows_ReturnsMockWalletFallbackGracefully()
        {
            var bridge = new TestJniBridge { ShouldThrowOnSchemeCheck = true };
            IWallet wallet = WalletFactory.CreateAndroidWallet(bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MockWallet>(wallet);
        }

        [Test]
        public void ResolveWallet_WhenAndroidPlatformAndAvailable_ReturnsMwaWallet()
        {
            var bridge = new TestJniBridge { SchemeAvailableResult = true };
            IWallet wallet = WalletFactory.ResolveWallet(true, bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void ResolveWallet_WhenAndroidPlatformAndUnavailable_ReturnsMockWallet()
        {
            var bridge = new TestJniBridge { SchemeAvailableResult = false };
            IWallet wallet = WalletFactory.ResolveWallet(true, bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MockWallet>(wallet);
        }

        [Test]
        public void ResolveWallet_WhenNotAndroidPlatform_ReturnsEditorMwaWallet()
        {
            var bridge = new TestJniBridge { SchemeAvailableResult = true };
            IWallet wallet = WalletFactory.ResolveWallet(false, bridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
        }

        [Test]
        public void CreateAndroidWallet_WhenSdkBridgeProvidedAndAvailable_ReturnsMwaWalletWithSolanaSdkAdapter()
        {
            var sdkBridge = new MockSolanaSdkBridge { IsAvailable = true };
            IWallet wallet = WalletFactory.CreateAndroidWallet(sdkBridge: sdkBridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
            if (wallet is MwaWallet mwaWallet)
            {
                Assert.IsInstanceOf<SolanaSdkMwaAdapter>(mwaWallet.Adapter);
            }
        }

        [Test]
        public void CreateAndroidWallet_PrioritizesSolanaSdkOverJniWhenBothAvailable()
        {
            var sdkBridge = new MockSolanaSdkBridge { IsAvailable = true };
            var jniBridge = new TestJniBridge { SchemeAvailableResult = true };
            IWallet wallet = WalletFactory.CreateAndroidWallet(bridge: jniBridge, sdkBridge: sdkBridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
            if (wallet is MwaWallet mwaWallet)
            {
                Assert.IsInstanceOf<SolanaSdkMwaAdapter>(mwaWallet.Adapter);
            }
        }

        [Test]
        public void CreateAndroidWallet_FallsBackToJniWhenSdkBridgeUnavailable()
        {
            var sdkBridge = new MockSolanaSdkBridge { IsAvailable = false };
            var jniBridge = new TestJniBridge { SchemeAvailableResult = true };
            IWallet wallet = WalletFactory.CreateAndroidWallet(bridge: jniBridge, sdkBridge: sdkBridge);

            Assert.IsNotNull(wallet);
            Assert.IsInstanceOf<MwaWallet>(wallet);
            if (wallet is MwaWallet mwaWallet)
            {
                Assert.IsInstanceOf<AndroidMwaAdapter>(mwaWallet.Adapter);
            }
        }

        [Test]
        public async Task CreateWallet_Default_ExecutesCommercialOperationsSuccessfully()
        {
            IWallet wallet = WalletFactory.CreateWallet();
            bool connected = await wallet.Connect();
            Assert.IsTrue(connected);

            string tipSig = await wallet.SendTip("4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL", 500);
            Assert.IsNotNull(tipSig);
            Assert.IsTrue(tipSig.StartsWith("MwaSig-devnet-"));

            string seatSig = await wallet.PurchaseSeat("retro-pack-01", 1000);
            Assert.IsNotNull(seatSig);
            Assert.IsTrue(seatSig.StartsWith("MwaSig-devnet-"));

            string listingSig = await wallet.PurchaseListing("entry-99", 2500);
            Assert.IsNotNull(listingSig);
            Assert.IsTrue(listingSig.StartsWith("MwaSig-devnet-"));
        }
    }
}
