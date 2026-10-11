using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;

namespace Plemu.Tests
{
    [TestFixture]
    public class SolanaSdkMwaAdapterTests
    {
        private MockSolanaSdkBridge _bridge;
        private SolanaSdkMwaAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _bridge = new MockSolanaSdkBridge();
            _adapter = new SolanaSdkMwaAdapter(_bridge);
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
        public void SolanaSdkMwaAdapter_ImplementsIMobileWalletAdapter()
        {
            Assert.IsTrue(_adapter is IMobileWalletAdapter);
        }

        [Test]
        public void SolanaSdkMwaAdapter_Constants_HaveExpectedDappMetadata()
        {
            Assert.AreEqual("Plemu", SolanaSdkMwaAdapter.DappName);
            Assert.AreEqual("https://plemu.gg", SolanaSdkMwaAdapter.DappUri);
            Assert.AreEqual("https://plemu.gg/icon.png", SolanaSdkMwaAdapter.DappIcon);
            Assert.AreEqual("devnet", SolanaSdkMwaAdapter.Cluster);
        }

        [Test]
        public void SolanaSdkMwaAdapter_DefaultConstructor_OnNonAndroid_IsMwaAvailableReturnsFalse()
        {
            var defaultAdapter = new SolanaSdkMwaAdapter();
            Assert.IsFalse(defaultAdapter.IsMwaAvailable());
        }

        [Test]
        public void SolanaSdkMwaAdapter_WithBridge_IsMwaAvailable_DelegatesToBridge()
        {
            _bridge.IsAvailable = true;
            Assert.IsTrue(_adapter.IsMwaAvailable());

            _bridge.IsAvailable = false;
            Assert.IsFalse(_adapter.IsMwaAvailable());
        }

        [Test]
        public void SolanaSdkMwaAdapter_WithBridge_IsMwaAvailable_WhenBridgeThrows_ReturnsFalseGracefully()
        {
            _bridge.ThrowOnAvailableCheck = true;
            Assert.IsFalse(_adapter.IsMwaAvailable());
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_WhenPreCancelled_ReturnsCancelled()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                MwaConnectionResult result = await _adapter.ConnectAsync(cts.Token);
                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
                Assert.IsFalse(_adapter.IsConnected);
            }
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_WhenBridgeNotAvailable_ReturnsFailed()
        {
            _bridge.IsAvailable = false;
            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsFalse(_adapter.IsConnected);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_FirstTimeAuthorize_EstablishesSessionAndReturnsOk()
        {
            _bridge.ConfiguredPublicKey = "7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs";
            _bridge.ConfiguredAuthToken = "solana-sdk-auth-token-123";

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual("7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs", result.PublicKey);
            Assert.AreEqual("solana-sdk-auth-token-123", result.AuthToken);
            Assert.IsTrue(_adapter.IsConnected);
            Assert.AreEqual(result.PublicKey, _adapter.CurrentPublicKey);
            Assert.AreEqual(result.AuthToken, _adapter.CurrentAuthToken);
            Assert.AreEqual(1, _bridge.AuthorizeCallCount);
            Assert.AreEqual(0, _bridge.ReauthorizeCallCount);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_WhenUserCancels_ReturnsCancelled()
        {
            _bridge.SimulateUserCancellation = true;

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(_adapter.IsConnected);
            Assert.IsNull(_adapter.CurrentPublicKey);
            Assert.IsNull(_adapter.CurrentAuthToken);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_WhenBridgeThrows_ReturnsFailedWithoutCrashing()
        {
            _bridge.ThrowOnAuthorize = true;

            MwaConnectionResult result = await _adapter.ConnectAsync();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsFalse(_adapter.IsConnected);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_SubsequentConnect_AttemptsReauthorizeFirst()
        {
            _bridge.ConfiguredPublicKey = "7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs";
            _bridge.ConfiguredAuthToken = "token-initial";
            await _adapter.ConnectAsync();

            Assert.AreEqual(1, _bridge.AuthorizeCallCount);
            Assert.AreEqual(0, _bridge.ReauthorizeCallCount);

            // Reconnecting while session has cached auth token should attempt reauthorize
            _bridge.ConfiguredAuthToken = "token-renewed";
            MwaConnectionResult reauthResult = await _adapter.ConnectAsync();

            Assert.IsTrue(reauthResult.IsSuccess);
            Assert.AreEqual(1, _bridge.AuthorizeCallCount);
            Assert.AreEqual(1, _bridge.ReauthorizeCallCount);
            Assert.AreEqual("token-renewed", _adapter.CurrentAuthToken);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_ConnectAsync_WhenReauthorizeFails_FallsBackToAuthorize()
        {
            _bridge.ConfiguredPublicKey = "7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs";
            _bridge.ConfiguredAuthToken = "token-initial";
            await _adapter.ConnectAsync();

            _bridge.SimulateReauthorizeFailure = true;
            _bridge.ConfiguredAuthToken = "token-fresh-after-fallback";

            MwaConnectionResult fallbackResult = await _adapter.ConnectAsync();

            Assert.IsTrue(fallbackResult.IsSuccess);
            Assert.AreEqual(1, _bridge.ReauthorizeCallCount);
            Assert.AreEqual(2, _bridge.AuthorizeCallCount);
            Assert.AreEqual("token-fresh-after-fallback", _adapter.CurrentAuthToken);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenNotConnected_ReturnsFailed()
        {
            byte[] payload = Encoding.UTF8.GetBytes("test-transaction-bytes");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenPayloadNull_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _adapter.SignAndSendTransactionAsync(null));
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenPayloadEmpty_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _adapter.SignAndSendTransactionAsync(new byte[0]));
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenPreCancelled_ReturnsCancelled()
        {
            await _adapter.ConnectAsync();

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                byte[] payload = Encoding.UTF8.GetBytes("test-transaction-bytes");
                MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload, cts.Token);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCancelled);
            }
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_Success_ReturnsSignature()
        {
            await _adapter.ConnectAsync();

            _bridge.ConfiguredSignature = "5wHu1qwD964D37B5C8A79B32D15F6789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCD";
            byte[] payload = Encoding.UTF8.GetBytes("valid-wire-format-bytes");

            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.AreEqual(_bridge.ConfiguredSignature, result.Signature);
            Assert.AreEqual(1, _bridge.SignAndSendCallCount);
            Assert.AreEqual(payload, _bridge.LastSentPayload);
            Assert.AreEqual(_adapter.CurrentAuthToken, _bridge.LastAuthToken);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenUserDeclines_ReturnsCancelled()
        {
            await _adapter.ConnectAsync();
            _bridge.SimulateUserCancellationOnSigning = true;

            byte[] payload = Encoding.UTF8.GetBytes("valid-wire-format-bytes");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCancelled);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_SignAndSendTransactionAsync_WhenBridgeThrows_ReturnsFailedWithoutCrashing()
        {
            await _adapter.ConnectAsync();
            _bridge.ThrowOnSigning = true;

            byte[] payload = Encoding.UTF8.GetBytes("valid-wire-format-bytes");
            MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsCancelled);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public async Task SolanaSdkMwaAdapter_DisconnectAsync_CallsDeauthorizeAndClearsVolatileSession()
        {
            await _adapter.ConnectAsync();
            Assert.IsTrue(_adapter.IsConnected);
            Assert.IsNotNull(_adapter.CurrentAuthToken);
            Assert.IsNotNull(_adapter.CurrentPublicKey);

            await _adapter.DisconnectAsync();

            Assert.IsFalse(_adapter.IsConnected);
            Assert.IsNull(_adapter.CurrentAuthToken);
            Assert.IsNull(_adapter.CurrentPublicKey);
            Assert.AreEqual(1, _bridge.DeauthorizeCallCount);
        }

        [Test]
        public void MwaJsonRpcProtocol_CreateAuthorizeRequest_BuildsValidJsonRpc2()
        {
            string json = MwaJsonRpcProtocol.CreateAuthorizeRequest(1, "Plemu", "https://plemu.gg", "https://plemu.gg/icon.png", "devnet");
            Assert.IsTrue(json.Contains("\"jsonrpc\": \"2.0\"") || json.Contains("\"jsonrpc\":\"2.0\""));
            Assert.IsTrue(json.Contains("\"method\": \"authorize\"") || json.Contains("\"method\":\"authorize\""));
            Assert.IsTrue(json.Contains("Plemu"));
            Assert.IsTrue(json.Contains("https://plemu.gg"));
            Assert.IsTrue(json.Contains("devnet"));
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseAuthorizeResponse_ParsesPublicKeyAndAuthToken()
        {
            string json = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": 1,
                ""result"": {
                    ""auth_token"": ""auth-abc-123"",
                    ""accounts"": [
                        { ""address"": ""7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs"", ""label"": ""Main Account"" }
                    ]
                }
            }";

            MwaConnectionResult result = MwaJsonRpcProtocol.ParseAuthorizeResponse(json);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs", result.PublicKey);
            Assert.AreEqual("auth-abc-123", result.AuthToken);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseSignAndSendTransactionsResponse_ParsesSignature()
        {
            string json = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": 2,
                ""result"": {
                    ""signatures"": [
                        ""4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX""
                    ]
                }
            }";

            MwaTransactionResult result = MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse(json);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX", result.Signature);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseAuthorizeResponse_DecodesBase64AddressToSolanaBase58()
        {
            // MWA 2.0 returns accounts[0].address as Base64 encoded 32 bytes
            byte[] rawBytes = new byte[32];
            for (int i = 0; i < 32; i++) rawBytes[i] = (byte)(i + 1);
            string expectedBase58 = SolanaBase58.Encode(rawBytes);
            string base64Address = Convert.ToBase64String(rawBytes);

            string json = $@"{{
                ""jsonrpc"": ""2.0"",
                ""id"": 10,
                ""result"": {{
                    ""auth_token"": ""auth-token-test"",
                    ""accounts"": [
                        {{ ""address"": ""{base64Address}"", ""label"": ""Main Account"" }}
                    ]
                }}
            }}";

            MwaConnectionResult result = MwaJsonRpcProtocol.ParseAuthorizeResponse(json);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedBase58, result.PublicKey);
            Assert.AreEqual("auth-token-test", result.AuthToken);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseAuthorizeResponse_InvalidKeyLength_ReturnsFailed()
        {
            // Base64 encoding of 16 bytes (invalid for Solana 32-byte public key)
            string base64ShortKey = Convert.ToBase64String(new byte[16]);
            string json = $@"{{
                ""jsonrpc"": ""2.0"",
                ""id"": 11,
                ""result"": {{
                    ""auth_token"": ""auth-token-test"",
                    ""accounts"": [
                        {{ ""address"": ""{base64ShortKey}"" }}
                    ]
                }}
            }}";

            MwaConnectionResult result = MwaJsonRpcProtocol.ParseAuthorizeResponse(json);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseSignAndSendTransactionsResponse_DecodesBase64SignatureToSolanaBase58()
        {
            // MWA 2.0 returns signatures as Base64 encoded 64 bytes
            byte[] rawSig = new byte[64];
            for (int i = 0; i < 64; i++) rawSig[i] = (byte)(255 - i);
            string expectedBase58Sig = SolanaBase58.Encode(rawSig);
            string base64Sig = Convert.ToBase64String(rawSig);

            string json = $@"{{
                ""jsonrpc"": ""2.0"",
                ""id"": 20,
                ""result"": {{
                    ""signatures"": [
                        ""{base64Sig}""
                    ]
                }}
            }}";

            MwaTransactionResult result = MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse(json);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedBase58Sig, result.Signature);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseSignAndSendTransactionsResponse_InvalidSignatureLength_ReturnsFailed()
        {
            // Base64 encoding of 16 bytes (invalid for 64-byte Ed25519 signature)
            string base64ShortSig = Convert.ToBase64String(new byte[16]);
            string json = $@"{{
                ""jsonrpc"": ""2.0"",
                ""id"": 21,
                ""result"": {{
                    ""signatures"": [
                        ""{base64ShortSig}""
                    ]
                }}
            }}";

            MwaTransactionResult result = MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse(json);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void MwaJsonRpcProtocol_ParseErrorResponse_ExtractsErrorCodeAndMessage()
        {
            string json = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": 3,
                ""error"": {
                    ""code"": -32600,
                    ""message"": ""User cancelled request""
                }
            }";

            MwaConnectionResult connResult = MwaJsonRpcProtocol.ParseAuthorizeResponse(json);
            Assert.IsFalse(connResult.IsSuccess);
            Assert.IsTrue(connResult.IsCancelled);

            MwaTransactionResult txResult = MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse(json);
            Assert.IsFalse(txResult.IsSuccess);
            Assert.IsTrue(txResult.IsCancelled);
        }

        [Test]
        public void WalletFactory_CreateSolanaSdkWallet_InstantiatesMwaWalletWithSolanaSdkAdapter()
        {
            IWallet wallet = WalletFactory.CreateSolanaSdkWallet(_bridge);
            Assert.IsNotNull(wallet);
            Assert.IsTrue(wallet is MwaWallet);
        }
    }

    public class MockSolanaSdkBridge : ISolanaSdkBridge
    {
        public bool IsAvailable { get; set; } = true;
        public bool ThrowOnAvailableCheck { get; set; } = false;
        public bool SimulateUserCancellation { get; set; } = false;
        public bool SimulateUserCancellationOnSigning { get; set; } = false;
        public bool ThrowOnAuthorize { get; set; } = false;
        public bool SimulateReauthorizeFailure { get; set; } = false;
        public bool ThrowOnSigning { get; set; } = false;

        public string ConfiguredPublicKey { get; set; } = "7vfCXTUXx5WJV5JADk17DUJ4ksgau7utNKj4b963voxs";
        public string ConfiguredAuthToken { get; set; } = "mock-sdk-token";
        public string ConfiguredSignature { get; set; } = "MockSdkSignature1111111111111111111111111111111111111111111111111111111111111111111";

        public int AuthorizeCallCount { get; private set; }
        public int ReauthorizeCallCount { get; private set; }
        public int DeauthorizeCallCount { get; private set; }
        public int SignAndSendCallCount { get; private set; }

        public byte[] LastSentPayload { get; private set; }
        public string LastAuthToken { get; private set; }

        public bool IsSdkAvailable()
        {
            if (ThrowOnAvailableCheck)
            {
                throw new InvalidOperationException("Simulated SDK check crash");
            }
            return IsAvailable;
        }

        public Task<MwaConnectionResult> AuthorizeAsync(
            string cluster,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default)
        {
            AuthorizeCallCount++;
            if (ThrowOnAuthorize)
            {
                throw new InvalidOperationException("Simulated SDK authorize crash");
            }

            if (SimulateUserCancellation || cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(MwaConnectionResult.Cancelled("User cancelled in Seed Vault"));
            }

            return Task.FromResult(MwaConnectionResult.Ok(ConfiguredPublicKey, ConfiguredAuthToken));
        }

        public Task<MwaConnectionResult> ReauthorizeAsync(
            string authToken,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken = default)
        {
            ReauthorizeCallCount++;
            if (SimulateReauthorizeFailure)
            {
                return Task.FromResult(MwaConnectionResult.Failed("Auth token expired or invalid"));
            }

            if (SimulateUserCancellation || cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(MwaConnectionResult.Cancelled("User cancelled reauthorization"));
            }

            return Task.FromResult(MwaConnectionResult.Ok(ConfiguredPublicKey, ConfiguredAuthToken));
        }

        public Task DeauthorizeAsync(string authToken, CancellationToken cancellationToken = default)
        {
            DeauthorizeCallCount++;
            return Task.CompletedTask;
        }

        public Task<MwaTransactionResult> SignAndSendTransactionAsync(
            string authToken,
            byte[] serializedTransaction,
            string cluster = "devnet",
            CancellationToken cancellationToken = default)
        {
            SignAndSendCallCount++;
            LastSentPayload = serializedTransaction;
            LastAuthToken = authToken;

            if (ThrowOnSigning)
            {
                throw new InvalidOperationException("Simulated SDK signing crash");
            }

            if (SimulateUserCancellationOnSigning || cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(MwaTransactionResult.Cancelled("User declined transaction signing"));
            }

            return Task.FromResult(MwaTransactionResult.Ok(ConfiguredSignature));
        }
    }
}
