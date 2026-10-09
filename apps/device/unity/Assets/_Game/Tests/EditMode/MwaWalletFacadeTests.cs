using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class MwaWalletFacadeTests
    {
        private MockMwaAdapter _adapter;
        private MwaWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _adapter = new MockMwaAdapter();
            _wallet = new MwaWallet(_adapter);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_wallet != null && _wallet.IsConnected)
            {
                await _wallet.DisconnectAsync();
            }
        }

        [Test]
        public void Constructor_NullAdapter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MwaWallet(null));
        }

        [Test]
        public void InitialState_IsNotConnected_AndPublicKeyIsNull()
        {
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);
        }

        [Test]
        public async Task Connect_WhenAdapterSucceeds_ReturnsTrue_SetsIsConnectedAndPublicKey()
        {
            bool success = await _wallet.Connect();

            Assert.IsTrue(success);
            Assert.IsTrue(_wallet.IsConnected);
            Assert.AreEqual("MwaWa11etDevnet111111111111111111111111111", _wallet.PublicKey);
        }

        [Test]
        public async Task Connect_WhenAdapterCancelled_ReturnsFalse_RemainsDisconnected()
        {
            _adapter.SimulateUserCancellation = true;

            bool success = await _wallet.Connect();

            Assert.IsFalse(success);
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);
        }

        [Test]
        public async Task Connect_WhenAdapterFails_ReturnsFalse_RemainsDisconnected()
        {
            _adapter.SimulateFailure = true;

            bool success = await _wallet.Connect();

            Assert.IsFalse(success);
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);
        }

        [Test]
        public async Task Disconnect_Synchronous_ClearsSessionImmediately()
        {
            await _wallet.Connect();
            Assert.IsTrue(_wallet.IsConnected);
            Assert.IsNotNull(_wallet.PublicKey);

            _wallet.Disconnect();

            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);
        }

        [Test]
        public async Task DisconnectAsync_ClearsSessionImmediatelyAndPurgesAdapter()
        {
            await _wallet.Connect();
            Assert.IsTrue(_wallet.IsConnected);

            await _wallet.DisconnectAsync();

            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);
            Assert.IsFalse(_adapter.IsConnectedSession);
        }

        [Test]
        public async Task SendTip_WhenDisconnected_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.SendTip("recipient123", 1000);
            });
        }

        [Test]
        public async Task PurchaseSeat_WhenDisconnected_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseSeat("speedrun-club", 5000);
            });
        }

        [Test]
        public async Task PurchaseListing_WhenDisconnected_ThrowsInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseListing("entry-libbet", 2500);
            });
        }

        [Test]
        public async Task SendTip_Validation_InvalidRecipientOrZeroAmount_ThrowsArgumentException()
        {
            await _wallet.Connect();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.SendTip(null, 1000);
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.SendTip("", 1000);
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.SendTip("recipient123", 0);
            });
        }

        [Test]
        public async Task PurchaseSeat_Validation_NullOrEmptyPackId_ThrowsArgumentException()
        {
            await _wallet.Connect();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.PurchaseSeat(null, 5000);
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.PurchaseSeat("", 5000);
            });
        }

        [Test]
        public async Task PurchaseListing_Validation_NullOrEmptyEntryId_ThrowsArgumentException()
        {
            await _wallet.Connect();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.PurchaseListing(null, 2500);
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _wallet.PurchaseListing("", 2500);
            });
        }

        [Test]
        public async Task SendTip_WhenConnected_ReturnsDeterministicSignatureFromAdapter()
        {
            await _wallet.Connect();

            string signature = await _wallet.SendTip("recipient123", 1000);

            Assert.IsNotNull(signature);
            Assert.IsTrue(signature.StartsWith("MwaSig-devnet-"));
        }

        [Test]
        public async Task PurchaseSeat_WhenConnected_ReturnsDeterministicSignatureFromAdapter()
        {
            await _wallet.Connect();

            string signature = await _wallet.PurchaseSeat("speedrun-club", 5000);

            Assert.IsNotNull(signature);
            Assert.IsTrue(signature.StartsWith("MwaSig-devnet-"));
        }

        [Test]
        public async Task PurchaseListing_WhenConnected_ReturnsDeterministicSignatureFromAdapter()
        {
            await _wallet.Connect();

            string signature = await _wallet.PurchaseListing("entry-libbet", 2500);

            Assert.IsNotNull(signature);
            Assert.IsTrue(signature.StartsWith("MwaSig-devnet-"));
        }

        [Test]
        public async Task SendTip_WhenAdapterSimulatesCancellation_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateUserCancellation = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.SendTip("recipient123", 1000);
            });

            StringAssert.Contains("User cancelled authorization", ex.Message);
        }

        [Test]
        public async Task PurchaseSeat_WhenAdapterSimulatesCancellation_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateUserCancellation = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseSeat("speedrun-club", 5000);
            });

            StringAssert.Contains("User cancelled authorization", ex.Message);
        }

        [Test]
        public async Task PurchaseListing_WhenAdapterSimulatesCancellation_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateUserCancellation = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseListing("entry-libbet", 2500);
            });

            StringAssert.Contains("User cancelled authorization", ex.Message);
        }

        [Test]
        public async Task SendTip_WhenAdapterSimulatesFailure_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateFailure = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.SendTip("recipient123", 1000);
            });

            StringAssert.Contains("Seed Vault authentication failed", ex.Message);
        }

        [Test]
        public async Task PurchaseSeat_WhenAdapterSimulatesFailure_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateFailure = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseSeat("speedrun-club", 5000);
            });

            StringAssert.Contains("Seed Vault authentication failed", ex.Message);
        }

        [Test]
        public async Task PurchaseListing_WhenAdapterSimulatesFailure_ThrowsInvalidOperationException()
        {
            await _wallet.Connect();
            _adapter.SimulateFailure = true;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _wallet.PurchaseListing("entry-libbet", 2500);
            });

            StringAssert.Contains("Seed Vault authentication failed", ex.Message);
        }

        [Test]
        public async Task SessionLifecycle_ConnectThenDisconnectThenConnectAgain_MaintainsIntegrity()
        {
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);

            bool firstConnect = await _wallet.Connect();
            Assert.IsTrue(firstConnect);
            Assert.IsTrue(_wallet.IsConnected);
            Assert.AreEqual("MwaWa11etDevnet111111111111111111111111111", _wallet.PublicKey);

            _wallet.Disconnect();
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);

            bool secondConnect = await _wallet.Connect();
            Assert.IsTrue(secondConnect);
            Assert.IsTrue(_wallet.IsConnected);
            Assert.AreEqual("MwaWa11etDevnet111111111111111111111111111", _wallet.PublicKey);
        }
    }
}
