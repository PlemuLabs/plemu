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
        private const string ValidRecipient = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        private MockMwaAdapter _adapter;
        private MockBlockhashProvider _blockhashProvider;
        private MwaWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _adapter = new MockMwaAdapter();
            _blockhashProvider = new MockBlockhashProvider();
            _wallet = new MwaWallet(_adapter, _blockhashProvider);
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
            Assert.AreEqual(MockMwaAdapter.DefaultMockPublicKey, _wallet.PublicKey);
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

            string signature = await _wallet.SendTip(ValidRecipient, 1000);

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
                await _wallet.SendTip(ValidRecipient, 1000);
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
                await _wallet.SendTip(ValidRecipient, 1000);
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
            Assert.AreEqual(MockMwaAdapter.DefaultMockPublicKey, _wallet.PublicKey);

            _wallet.Disconnect();
            Assert.IsFalse(_wallet.IsConnected);
            Assert.IsNull(_wallet.PublicKey);

            bool secondConnect = await _wallet.Connect();
            Assert.IsTrue(secondConnect);
            Assert.IsTrue(_wallet.IsConnected);
            Assert.AreEqual(MockMwaAdapter.DefaultMockPublicKey, _wallet.PublicKey);
        }

        [Test]
        public async Task Connect_WhenConcurrentCallsAttempted_SerializesAndBothSucceed()
        {
            var blockingAdapter = new BlockingMockMwaAdapter();
            var wallet = new MwaWallet(blockingAdapter, _blockhashProvider);

            Task<bool> task1 = wallet.Connect();
            Task<bool> task2 = wallet.Connect();

            // Release the adapter's connect task
            blockingAdapter.ConnectTcs.SetResult(MwaConnectionResult.Ok(MockMwaAdapter.DefaultMockPublicKey, "token-concurrent"));

            bool[] results = await Task.WhenAll(task1, task2);

            Assert.IsTrue(results[0]);
            Assert.IsTrue(results[1]);
            Assert.IsTrue(wallet.IsConnected);
            Assert.AreEqual(MockMwaAdapter.DefaultMockPublicKey, wallet.PublicKey);
        }

        [Test]
        public async Task SendTip_WhenConcurrentTransactionAttempted_RejectsWithControlledException()
        {
            var blockingAdapter = new BlockingMockMwaAdapter();
            var wallet = new MwaWallet(blockingAdapter, _blockhashProvider);

            // Connect first
            blockingAdapter.ConnectTcs.SetResult(MwaConnectionResult.Ok(MockMwaAdapter.DefaultMockPublicKey, "token-concurrent"));
            await wallet.Connect();

            // First SendTip in-flight
            Task<string> task1 = wallet.SendTip(ValidRecipient, 1000);

            // Second concurrent SendTip must be rejected immediately
            Task<string> task2 = wallet.SendTip(ValidRecipient, 2000);
            var completedTask = await Task.WhenAny(task2, Task.Delay(500));
            Assert.AreEqual(task2, completedTask, "Concurrent SendTip was not rejected immediately.");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
            StringAssert.Contains("in progress", ex.Message);

            // Complete task1
            blockingAdapter.TxTcs.SetResult(MwaTransactionResult.Ok("MwaSig-devnet-tx1"));
            string sig1 = await task1;
            Assert.AreEqual("MwaSig-devnet-tx1", sig1);
        }

        [Test]
        public async Task PurchaseSeat_WhenConcurrentTransactionAttempted_RejectsWithControlledException()
        {
            var blockingAdapter = new BlockingMockMwaAdapter();
            var wallet = new MwaWallet(blockingAdapter, _blockhashProvider);

            blockingAdapter.ConnectTcs.SetResult(MwaConnectionResult.Ok(MockMwaAdapter.DefaultMockPublicKey, "token-concurrent"));
            await wallet.Connect();

            Task<string> task1 = wallet.PurchaseSeat("pack-speedrun", 5000);

            Task<string> task2 = wallet.PurchaseSeat("pack-speedrun-2", 6000);
            var completedTask = await Task.WhenAny(task2, Task.Delay(500));
            Assert.AreEqual(task2, completedTask, "Concurrent PurchaseSeat was not rejected immediately.");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
            StringAssert.Contains("in progress", ex.Message);

            blockingAdapter.TxTcs.SetResult(MwaTransactionResult.Ok("MwaSig-devnet-seat1"));
            string sig1 = await task1;
            Assert.AreEqual("MwaSig-devnet-seat1", sig1);
        }

        [Test]
        public async Task PurchaseListing_WhenConcurrentTransactionAttempted_RejectsWithControlledException()
        {
            var blockingAdapter = new BlockingMockMwaAdapter();
            var wallet = new MwaWallet(blockingAdapter, _blockhashProvider);

            blockingAdapter.ConnectTcs.SetResult(MwaConnectionResult.Ok(MockMwaAdapter.DefaultMockPublicKey, "token-concurrent"));
            await wallet.Connect();

            Task<string> task1 = wallet.PurchaseListing("entry-libbet", 2500);

            Task<string> task2 = wallet.PurchaseListing("entry-libbet-2", 3000);
            var completedTask = await Task.WhenAny(task2, Task.Delay(500));
            Assert.AreEqual(task2, completedTask, "Concurrent PurchaseListing was not rejected immediately.");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
            StringAssert.Contains("in progress", ex.Message);

            blockingAdapter.TxTcs.SetResult(MwaTransactionResult.Ok("MwaSig-devnet-listing1"));
            string sig1 = await task1;
            Assert.AreEqual("MwaSig-devnet-listing1", sig1);
        }

        private class BlockingMockMwaAdapter : IMobileWalletAdapter
        {
            public bool IsConnectedSession { get; set; } = true;
            public TaskCompletionSource<MwaTransactionResult> TxTcs { get; } = new();
            public TaskCompletionSource<MwaConnectionResult> ConnectTcs { get; } = new();

            public bool IsMwaAvailable() => true;

            public Task<MwaConnectionResult> ConnectAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return ConnectTcs.Task;
            }

            public Task DisconnectAsync()
            {
                IsConnectedSession = false;
                return Task.CompletedTask;
            }

            public Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, System.Threading.CancellationToken cancellationToken = default)
            {
                return TxTcs.Task;
            }
        }
    }
}
