using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class MwaResultModelTests
    {
        [Test]
        public void MwaConnectionResult_Ok_SetsSuccessAndPropertiesCorrectly()
        {
            var result = MwaConnectionResult.Ok("MwaWa11etDevnet111111111111111111111111111", "auth-token-xyz");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("MwaWa11etDevnet111111111111111111111111111", result.PublicKey);
            Assert.AreEqual("auth-token-xyz", result.AuthToken);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void MwaConnectionResult_Failed_SetsFailureAndErrorMessage()
        {
            var result = MwaConnectionResult.Failed("Connection timeout to Seed Vault");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.AreEqual("Connection timeout to Seed Vault", result.ErrorMessage);
        }

        [Test]
        public void MwaConnectionResult_Cancelled_DefaultReason_SetsCancelledAndExpectedMessage()
        {
            var result = MwaConnectionResult.Cancelled();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.AreEqual("User cancelled authorization", result.ErrorMessage);
        }

        [Test]
        public void MwaConnectionResult_Cancelled_CustomReason_SetsCancelledAndCustomMessage()
        {
            var result = MwaConnectionResult.Cancelled("User closed the wallet prompt");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.PublicKey);
            Assert.IsNull(result.AuthToken);
            Assert.AreEqual("User closed the wallet prompt", result.ErrorMessage);
        }

        [Test]
        public void MwaTransactionResult_Ok_SetsSuccessAndSignature()
        {
            var result = MwaTransactionResult.Ok("5xyzMwaSignatureDevnet12345");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("5xyzMwaSignatureDevnet12345", result.Signature);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void MwaTransactionResult_Failed_SetsFailureAndErrorMessage()
        {
            var result = MwaTransactionResult.Failed("Transaction simulation failed on devnet");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("Transaction simulation failed on devnet", result.ErrorMessage);
        }

        [Test]
        public void MwaTransactionResult_Cancelled_DefaultReason_SetsCancelledAndExpectedMessage()
        {
            var result = MwaTransactionResult.Cancelled();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("User cancelled signing", result.ErrorMessage);
        }

        [Test]
        public void MwaTransactionResult_Cancelled_CustomReason_SetsCancelledAndCustomMessage()
        {
            var result = MwaTransactionResult.Cancelled("User rejected transaction in Seed Vault");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.AreEqual("User rejected transaction in Seed Vault", result.ErrorMessage);
        }
    }
}
