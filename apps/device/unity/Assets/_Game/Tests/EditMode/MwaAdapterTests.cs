using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class MwaAdapterTests
    {
        private MockMwaAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _adapter = new MockMwaAdapter();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_adapter != null)
            {
                await _adapter.DisconnectAsync();
            }
        }

        [Test]
        public void MockMwaAdapter_IsMwaAvailable_ReturnsDefaultTrueAndRespectsConfiguration()
        {
            Assert.IsTrue(_adapter.IsMwaAvailable());

            _adapter.IsAvailable = false;
            Assert.IsFalse(_adapter.IsMwaAvailable());

            _adapter.IsAvailable = true;
            Assert.IsTrue(_adapter.IsMwaAvailable());
        }

        [Test]
        public async Task MockMwaAdapter_ConnectAsync_Success_ReturnsExpectedPublicKeyAndAuthToken()
        {
            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("MwaWa11etDevnet111111111111111111111111111", result.PublicKey);
            Assert.AreEqual("mock-mwa-auth-token-devnet", result.AuthToken);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsTrue(_adapter.IsConnectedSession);
            Assert.AreEqual(result.PublicKey, _adapter.CurrentPublicKey);
            Assert.AreEqual(result.AuthToken, _adapter.CurrentAuthToken);
        }

        [Test]
        public async Task MockMwaAdapter_ConnectAsync_WhenNotAvailable_ReturnsFailed()
        {
            _adapter.IsAvailable = false;

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.IsFalse(_adapter.IsConnectedSession);
            Assert.IsTrue(result.ErrorMessage.Contains("not available"));
        }

        [Test]
        public async Task MockMwaAdapter_ConnectAsync_WhenSimulateUserCancellation_ReturnsCancelledWithReason()
        {
            _adapter.SimulateUserCancellation = true;
            _adapter.CancellationReason = "User rejected Seed Vault prompt";

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.IsFalse(_adapter.IsConnectedSession);
            Assert.AreEqual("User rejected Seed Vault prompt", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_ConnectAsync_WhenSimulateFailure_ReturnsFailedWithMessage()
        {
            _adapter.SimulateFailure = true;
            _adapter.FailureMessage = "Biometric timeout";

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.IsFalse(_adapter.IsConnectedSession);
            Assert.AreEqual("Biometric timeout", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_ConnectAsync_WhenCancellationTokenCancelled_ReturnsCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            MwaConnectionResult result = await _adapter.ConnectAsync(cts.Token);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(_adapter.IsConnectedSession);
            Assert.AreEqual("Operation cancelled", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_WhenNotConnected_ReturnsFailedNoActiveSession()
        {
            byte[] payload = Encoding.UTF8.GetBytes("sample-tx-bytes");

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("No active wallet session. Connect before signing.", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_WhenConnected_ReturnsDeterministicSignature()
        {
            await _adapter.ConnectAsync();
            byte[] payload = Encoding.UTF8.GetBytes("sample-tx-bytes");

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNotNull(result.Signature);
            Assert.IsTrue(result.Signature.StartsWith("MwaSig-devnet-"));
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_SamePayload_ProducesIdenticalSignatures()
        {
            await _adapter.ConnectAsync();
            byte[] payload = Encoding.UTF8.GetBytes("identical-tx-data-12345");

            MwaTransactionResult result1 = await _adapter.SignAndSendTransactionAsync(payload);
            MwaTransactionResult result2 = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsTrue(result1.IsSuccess);
            Assert.IsTrue(result2.IsSuccess);
            Assert.AreEqual(result1.Signature, result2.Signature);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_DifferentPayloads_ProducesDifferentSignatures()
        {
            await _adapter.ConnectAsync();
            byte[] payloadA = Encoding.UTF8.GetBytes("payload-alpha");
            byte[] payloadB = Encoding.UTF8.GetBytes("payload-beta");

            MwaTransactionResult resultA = await _adapter.SignAndSendTransactionAsync(payloadA);
            MwaTransactionResult resultB = await _adapter.SignAndSendTransactionAsync(payloadB);

            Assert.IsTrue(resultA.IsSuccess);
            Assert.IsTrue(resultB.IsSuccess);
            Assert.AreNotEqual(resultA.Signature, resultB.Signature);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_NullOrEmptyPayload_ReturnsFailed()
        {
            await _adapter.ConnectAsync();

            MwaTransactionResult nullResult = await _adapter.SignAndSendTransactionAsync(null);
            Assert.IsFalse(nullResult.IsSuccess);
            Assert.AreEqual("Transaction payload cannot be null or empty", nullResult.ErrorMessage);

            MwaTransactionResult emptyResult = await _adapter.SignAndSendTransactionAsync(new byte[0]);
            Assert.IsFalse(emptyResult.IsSuccess);
            Assert.AreEqual("Transaction payload cannot be null or empty", emptyResult.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_WhenSimulateUserCancellation_ReturnsCancelled()
        {
            await _adapter.ConnectAsync();
            _adapter.SimulateUserCancellation = true;
            _adapter.CancellationReason = "User rejected transaction in Seed Vault";

            byte[] payload = Encoding.UTF8.GetBytes("sample-tx");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("User rejected transaction in Seed Vault", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_WhenSimulateFailure_ReturnsFailed()
        {
            await _adapter.ConnectAsync();
            _adapter.SimulateFailure = true;
            _adapter.FailureMessage = "Hardware vault connection lost";

            byte[] payload = Encoding.UTF8.GetBytes("sample-tx");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("Hardware vault connection lost", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_SignAndSendTransactionAsync_WhenCancellationTokenCancelled_ReturnsCancelled()
        {
            await _adapter.ConnectAsync();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            byte[] payload = Encoding.UTF8.GetBytes("sample-tx");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload, cts.Token);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("Operation cancelled", result.ErrorMessage);
        }

        [Test]
        public async Task MockMwaAdapter_DisconnectAsync_ClearsSessionAndInvalidatesSubsequentSigning()
        {
            await _adapter.ConnectAsync();
            Assert.IsTrue(_adapter.IsConnectedSession);
            Assert.IsNotNull(_adapter.CurrentPublicKey);
            Assert.IsNotNull(_adapter.CurrentAuthToken);

            await _adapter.DisconnectAsync();

            Assert.IsFalse(_adapter.IsConnectedSession);
            Assert.IsNull(_adapter.CurrentPublicKey);
            Assert.IsNull(_adapter.CurrentAuthToken);

            byte[] payload = Encoding.UTF8.GetBytes("tx-after-disconnect");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("No active wallet session. Connect before signing.", result.ErrorMessage);
        }
    }
}
