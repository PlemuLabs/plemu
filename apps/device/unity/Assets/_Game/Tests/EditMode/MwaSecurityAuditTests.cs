using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class MwaSecurityAuditTests
    {
        private class TestJniBridge : IAndroidJniBridge
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowOnAssociate { get; set; } = false;
            public string Token { get; set; } = "mock_secret_token_123456789";
            public string Key { get; set; } = "MockPublicKey111111111111111111111111111";

            public bool IsIntentAvailable(string schemeUri) => IsAvailable;

            public Task<MwaConnectionResult> LaunchAssociateIntentAsync(
                string uri,
                string dappName,
                string dappUri,
                string dappIcon,
                string cluster,
                CancellationToken cancellationToken)
            {
                if (ThrowOnAssociate)
                {
                    throw new InvalidOperationException("Simulated bridge fault with token leakage risk");
                }
                return Task.FromResult(MwaConnectionResult.Ok(Key, Token));
            }

            public Task<MwaTransactionResult> LaunchTransactionIntentAsync(
                string uri,
                string authToken,
                byte[] payload,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(MwaTransactionResult.Ok("MwaSig-devnet-TestSignature1111111111111111"));
            }
        }

        private static string ResolveWalletScriptsDirectory()
        {
            string[] candidates = new[]
            {
                Path.Combine("apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Wallet"),
                Path.Combine("plemu", "apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Wallet"),
                Path.Combine(Directory.GetCurrentDirectory(), "plemu", "apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Wallet"),
                Path.Combine(Directory.GetCurrentDirectory(), "apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Wallet")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return Path.Combine("plemu", "apps", "device", "unity", "Assets", "_Game", "Scripts", "Runtime", "Wallet");
        }

        [Test]
        public async Task Disconnect_PurgesAllAuthTokensAndKeysFromVolatileMemory()
        {
            var adapter = new MockMwaAdapter();
            var wallet = new MwaWallet(adapter);

            bool connected = await wallet.Connect();
            Assert.IsTrue(connected);
            Assert.IsTrue(wallet.IsConnected);
            Assert.IsNotNull(wallet.PublicKey);

            // Verify via reflection that internal state holds credentials
            FieldInfo tokenField = typeof(MwaWallet).GetField("_authToken", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo keyField = typeof(MwaWallet).GetField("_publicKey", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(tokenField, "_authToken field must exist for fail-closed checks");
            Assert.IsNotNull(keyField, "_publicKey field must exist for fail-closed checks");
            Assert.IsNotNull(tokenField.GetValue(wallet), "_authToken must be populated while connected");
            Assert.IsNotNull(keyField.GetValue(wallet), "_publicKey must be populated while connected");

            // Purge via synchronous Disconnect()
            wallet.Disconnect();

            Assert.IsFalse(wallet.IsConnected);
            Assert.IsNull(wallet.PublicKey);
            Assert.IsNull(tokenField.GetValue(wallet), "_authToken must be strictly nullified on Disconnect()");
            Assert.IsNull(keyField.GetValue(wallet), "_publicKey must be strictly nullified on Disconnect()");
        }

        [Test]
        public async Task DisconnectAsync_PurgesAllAuthTokensAndKeysFromVolatileMemory()
        {
            var adapter = new MockMwaAdapter();
            var wallet = new MwaWallet(adapter);

            await wallet.Connect();
            Assert.IsTrue(wallet.IsConnected);

            FieldInfo tokenField = typeof(MwaWallet).GetField("_authToken", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo keyField = typeof(MwaWallet).GetField("_publicKey", BindingFlags.NonPublic | BindingFlags.Instance);

            await wallet.DisconnectAsync();

            Assert.IsFalse(wallet.IsConnected);
            Assert.IsNull(wallet.PublicKey);
            Assert.IsNull(tokenField.GetValue(wallet), "_authToken must be nullified on DisconnectAsync()");
            Assert.IsNull(keyField.GetValue(wallet), "_publicKey must be nullified on DisconnectAsync()");
            Assert.IsNull(adapter.CurrentAuthToken, "Adapter CurrentAuthToken must be wiped on DisconnectAsync()");
        }

        [Test]
        public async Task MockMwaAdapter_DisconnectAsync_PurgesVolatileTokens()
        {
            var adapter = new MockMwaAdapter();
            MwaConnectionResult result = await adapter.ConnectAsync();
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(adapter.CurrentAuthToken);
            Assert.IsNotNull(adapter.CurrentPublicKey);

            await adapter.DisconnectAsync();

            Assert.IsNull(adapter.CurrentAuthToken, "MockMwaAdapter CurrentAuthToken must be null after disconnect");
            Assert.IsNull(adapter.CurrentPublicKey, "MockMwaAdapter CurrentPublicKey must be null after disconnect");
        }

        [Test]
        public async Task AndroidMwaAdapter_DisconnectAsync_PurgesVolatileTokens()
        {
            var bridge = new TestJniBridge();
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(adapter.IsConnected);
            Assert.AreEqual("mock_secret_token_123456789", adapter.CurrentAuthToken);

            await adapter.DisconnectAsync();

            Assert.IsFalse(adapter.IsConnected);
            Assert.IsNull(adapter.CurrentAuthToken, "AndroidMwaAdapter CurrentAuthToken must be null after disconnect");
            Assert.IsNull(adapter.CurrentPublicKey, "AndroidMwaAdapter CurrentPublicKey must be null after disconnect");
        }

        [Test]
        public void MwaConnectionResult_ToString_RedactsAuthToken()
        {
            const string secretToken = "super_confidential_session_token_xyz987";
            const string pubkey = "7Nf11111111111111111111111111111111111111111";

            var result = MwaConnectionResult.Ok(pubkey, secretToken);
            string output = result.ToString();

            Assert.IsFalse(output.Contains(secretToken), "ToString() must never reveal raw auth token in logs");
            Assert.IsTrue(output.Contains("[REDACTED]"), "ToString() must explicitly mark auth token as [REDACTED]");
            Assert.IsTrue(output.Contains(pubkey), "ToString() should include public key for diagnostics");

            var failed = MwaConnectionResult.Failed("Connection refused");
            Assert.IsTrue(failed.ToString().Contains("Connection refused"));
        }

        [Test]
        public void MwaTransactionResult_ToString_SanitizesDiagnosticString()
        {
            const string sig = "MwaSig-devnet-TestSignature123456789";

            var result = MwaTransactionResult.Ok(sig);
            string output = result.ToString();

            Assert.IsTrue(output.Contains(sig), "ToString() should include signature");

            var failed = MwaTransactionResult.Failed("User cancelled biometric check");
            Assert.IsTrue(failed.ToString().Contains("User cancelled biometric check"));
        }

        [Test]
        public async Task UserCancellation_ReturnsTypedResultWithoutThrowingUnhandledException()
        {
            var adapter = new MockMwaAdapter { SimulateUserCancellation = true };
            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.AuthToken);
            Assert.IsNull(result.PublicKey);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("cancelled"));
        }

        [Test]
        public async Task UserTransactionCancellation_ReturnsTypedCancelledResult()
        {
            var adapter = new MockMwaAdapter();
            await adapter.ConnectAsync();

            adapter.SimulateUserCancellation = true;
            MwaTransactionResult result = await adapter.SignAndSendTransactionAsync(new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsNull(result.Signature);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("cancelled"));
        }

        [Test]
        public void StaticSecurityAudit_NoPlayerPrefsPersistenceInWalletNamespace()
        {
            string walletDir = ResolveWalletScriptsDirectory();
            Assert.IsTrue(Directory.Exists(walletDir), $"Wallet source directory must exist at {walletDir}");

            string[] sourceFiles = Directory.GetFiles(walletDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsTrue(sourceFiles.Length > 0, "Wallet source files must be found");

            foreach (string file in sourceFiles)
            {
                string content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains("PlayerPrefs.SetString"),
                    $"Security Violation: {Path.GetFileName(file)} references PlayerPrefs.SetString. Tokens must be volatile.");
                Assert.IsFalse(content.Contains("PlayerPrefs.Save"),
                    $"Security Violation: {Path.GetFileName(file)} references PlayerPrefs.Save.");
            }
        }

        [Test]
        public void StaticSecurityAudit_NoFileIoTokenPersistenceInWalletNamespace()
        {
            string walletDir = ResolveWalletScriptsDirectory();
            string[] sourceFiles = Directory.GetFiles(walletDir, "*.cs", SearchOption.TopDirectoryOnly);

            foreach (string file in sourceFiles)
            {
                string content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains("File.WriteAllText"),
                    $"Security Violation: {Path.GetFileName(file)} references File.WriteAllText.");
                Assert.IsFalse(content.Contains("StreamWriter"),
                    $"Security Violation: {Path.GetFileName(file)} references StreamWriter.");
                Assert.IsFalse(content.Contains("BinaryWriter"),
                    $"Security Violation: {Path.GetFileName(file)} references BinaryWriter.");
            }
        }

        [Test]
        public void StaticSecurityAudit_NoSensitiveTokensLoggedViaDebugLog()
        {
            string walletDir = ResolveWalletScriptsDirectory();
            string[] sourceFiles = Directory.GetFiles(walletDir, "*.cs", SearchOption.AllDirectories);

            foreach (string file in sourceFiles)
            {
                string content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains("Debug.Log($") && content.Contains("_authToken"),
                    $"Security Violation: {Path.GetFileName(file)} may log _authToken.");
                Assert.IsFalse(content.Contains("Debug.LogWarning($") && content.Contains("_authToken"),
                    $"Security Violation: {Path.GetFileName(file)} may log _authToken.");
            }
        }

        [Test]
        public async Task FailClosed_CommercialFlowsDoNotLeakDataWhenDisconnected()
        {
            var adapter = new MockMwaAdapter();
            var wallet = new MwaWallet(adapter);

            Assert.IsFalse(wallet.IsConnected);

            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.SendTip("Recipient1111111111111111111111111111", 100));
            Assert.AreEqual("Wallet is not connected.", ex1.Message);

            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.PurchaseSeat("pack-123", 500));
            Assert.AreEqual("Wallet is not connected.", ex2.Message);

            var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => wallet.PurchaseListing("entry-456", 1000));
            Assert.AreEqual("Wallet is not connected.", ex3.Message);
        }

        [Test]
        public async Task ErrorSanitization_ExceptionBridgeNeverLeavesConnectedState()
        {
            var bridge = new TestJniBridge { ThrowOnAssociate = true };
            var adapter = new AndroidMwaAdapter(bridge);

            MwaConnectionResult result = await adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(adapter.IsConnected);
            Assert.IsNull(adapter.CurrentAuthToken);
            Assert.IsNull(adapter.CurrentPublicKey);
            Assert.IsTrue(result.ErrorMessage.Contains("Error during wallet association"));
        }
    }
}
