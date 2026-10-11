using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class SolanaTransactionWireFormatTests
    {
        private const string TestSender = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        private const string TestRecipient = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        private const string SystemProgramId = "11111111111111111111111111111111";
        private const string TestBlockhash = "4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX";

        [Test]
        public void Shortvec_EncodeAndDecode_RoundTripsAccurately()
        {
            int[] testValues = new int[] { 0, 1, 127, 128, 255, 300, 16383, 16384, 65535 };

            foreach (int val in testValues)
            {
                using var ms = new MemoryStream();
                SolanaShortvec.Encode(val, ms);
                ms.Position = 0;
                int decoded = SolanaShortvec.Decode(ms);
                Assert.AreEqual(val, decoded, $"Mismatch for shortvec value {val}");
            }
        }

        [Test]
        public void Shortvec_Encode_InvalidBoundsOrNull_ThrowsExpectedException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentNullException>(() => SolanaShortvec.Encode(10, (Stream)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaShortvec.Encode(-1, ms));
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaShortvec.Encode(65536, ms));
            Assert.Throws<ArgumentNullException>(() => SolanaShortvec.Decode(null));
        }

        [Test]
        public void Shortvec_Decode_StreamAtEnd_ThrowsEndOfStreamException()
        {
            using var ms = new MemoryStream(Array.Empty<byte>());
            Assert.Throws<EndOfStreamException>(() => SolanaShortvec.Decode(ms));
        }

        [Test]
        public void Shortvec_Decode_CorruptMultiByteStream_ThrowsFormatException()
        {
            // All bytes have high bit 1 (0x80) with no terminating byte
            byte[] corrupt = new byte[] { 0x80, 0x80, 0x80 };
            using var ms = new MemoryStream(corrupt);
            Assert.Throws<FormatException>(() => SolanaShortvec.Decode(ms));
        }

        [Test]
        public void Base58_EncodeAndDecode_RoundTrips32Bytes()
        {
            byte[] zeros = new byte[32];
            string encodedZeros = SolanaBase58.Encode(zeros);
            Assert.AreEqual("11111111111111111111111111111111", encodedZeros);

            byte[] decoded = SolanaBase58.Decode(encodedZeros);
            Assert.AreEqual(32, decoded.Length);
            CollectionAssert.AreEqual(zeros, decoded);

            byte[] senderBytes = SolanaBase58.Decode(TestSender);
            Assert.AreEqual(32, senderBytes.Length);
            string reEncoded = SolanaBase58.Encode(senderBytes);
            Assert.AreEqual(TestSender, reEncoded);
        }

        [Test]
        public void Base58_NullAndEmptyInputs_ReturnEmptyResults()
        {
            Assert.AreEqual(string.Empty, SolanaBase58.Encode(ReadOnlySpan<byte>.Empty));
            Assert.AreEqual(0, SolanaBase58.Decode(null).Length);
            Assert.AreEqual(0, SolanaBase58.Decode("").Length);

            Assert.IsTrue(SolanaBase58.TryDecode("", out var emptyDecoded));
            Assert.AreEqual(0, emptyDecoded.Length);
        }

        [Test]
        public void Base58_Decode_InvalidCharacter_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => SolanaBase58.Decode("0OIl-Invalid"));
            Assert.IsFalse(SolanaBase58.TryDecode("0OIl-Invalid", out _));
        }

        [Test]
        public void SystemProgram_Transfer_BuildsValidInstruction()
        {
            ulong lamports = 5000000; // 0.005 SOL
            var ix = SolanaSystemProgram.Transfer(TestSender, TestRecipient, lamports);

            Assert.AreEqual(SystemProgramId, ix.ProgramId);
            Assert.AreEqual(2, ix.Keys.Count);

            Assert.AreEqual(TestSender, ix.Keys[0].PublicKey);
            Assert.IsTrue(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            Assert.AreEqual(TestRecipient, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsTrue(ix.Keys[1].IsWritable);

            Assert.AreEqual(12, ix.Data.Length);
            uint instructionType = BitConverter.ToUInt32(ix.Data, 0);
            ulong amount = BitConverter.ToUInt64(ix.Data, 4);
            Assert.AreEqual(2u, instructionType);
            Assert.AreEqual(lamports, amount);
        }

        [Test]
        public void SystemProgram_Transfer_InvalidArguments_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SolanaSystemProgram.Transfer(null, TestRecipient, 100));
            Assert.Throws<ArgumentException>(() => SolanaSystemProgram.Transfer("", TestRecipient, 100));
            Assert.Throws<ArgumentException>(() => SolanaSystemProgram.Transfer(TestSender, null, 100));
            Assert.Throws<ArgumentException>(() => SolanaSystemProgram.Transfer(TestSender, "   ", 100));
        }

        [Test]
        public void PlemuProgramInstructions_PurchaseSeat_BuildsValidInstruction()
        {
            var ix = PlemuProgramInstructions.PurchaseSeat(TestSender, "pack-speedrun-42", 7500);

            Assert.AreEqual(PlemuProgramInstructions.DefaultSeatsProgramId, ix.ProgramId);
            Assert.AreEqual(4, ix.Keys.Count);

            // Buyer / Signer
            Assert.AreEqual(TestSender, ix.Keys[0].PublicKey);
            Assert.IsTrue(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            // Seat PDA: ["seat", packId, buyer]
            string expectedSeatPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("seat"), Encoding.UTF8.GetBytes("pack-speedrun-42"), SolanaBase58.Decode(TestSender) },
                PlemuProgramInstructions.DefaultSeatsProgramId,
                out _);
            Assert.AreEqual(expectedSeatPda, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsTrue(ix.Keys[1].IsWritable);

            // Pack PDA: ["pack", packId]
            string expectedPackPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("pack"), Encoding.UTF8.GetBytes("pack-speedrun-42") },
                PlemuProgramInstructions.DefaultSeatsProgramId,
                out _);
            Assert.AreEqual(expectedPackPda, ix.Keys[2].PublicKey);
            Assert.IsFalse(ix.Keys[2].IsSigner);
            Assert.IsTrue(ix.Keys[2].IsWritable);

            // System Program
            Assert.AreEqual(SystemProgramId, ix.Keys[3].PublicKey);
            Assert.IsFalse(ix.Keys[3].IsSigner);
            Assert.IsFalse(ix.Keys[3].IsWritable);

            Assert.IsTrue(ix.Data.Length > 8);
            byte[] expectedDiscriminator = PlemuProgramInstructions.GetAnchorDiscriminator("purchase_seat");
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(expectedDiscriminator[i], ix.Data[i]);
            }
        }

        [Test]
        public void PlemuProgramInstructions_PurchaseListing_BuildsValidInstruction()
        {
            var ix = PlemuProgramInstructions.PurchaseListing(TestSender, "entry-art-007", 12000);

            Assert.AreEqual(PlemuProgramInstructions.DefaultMarketProgramId, ix.ProgramId);
            Assert.AreEqual(4, ix.Keys.Count);

            // Buyer / Signer
            Assert.AreEqual(TestSender, ix.Keys[0].PublicKey);
            Assert.IsTrue(ix.Keys[0].IsSigner);
            Assert.IsTrue(ix.Keys[0].IsWritable);

            // Listing PDA: ["listing", entryId]
            string expectedListingPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("listing"), Encoding.UTF8.GetBytes("entry-art-007") },
                PlemuProgramInstructions.DefaultMarketProgramId,
                out _);
            Assert.AreEqual(expectedListingPda, ix.Keys[1].PublicKey);
            Assert.IsFalse(ix.Keys[1].IsSigner);
            Assert.IsTrue(ix.Keys[1].IsWritable);

            // Receipt PDA: ["receipt", entryId, buyer]
            string expectedReceiptPda = SolanaPda.FindProgramAddress(
                new[] { Encoding.UTF8.GetBytes("receipt"), Encoding.UTF8.GetBytes("entry-art-007"), SolanaBase58.Decode(TestSender) },
                PlemuProgramInstructions.DefaultMarketProgramId,
                out _);
            Assert.AreEqual(expectedReceiptPda, ix.Keys[2].PublicKey);
            Assert.IsFalse(ix.Keys[2].IsSigner);
            Assert.IsTrue(ix.Keys[2].IsWritable);

            // System Program
            Assert.AreEqual(SystemProgramId, ix.Keys[3].PublicKey);
            Assert.IsFalse(ix.Keys[3].IsSigner);
            Assert.IsFalse(ix.Keys[3].IsWritable);

            Assert.IsTrue(ix.Data.Length > 8);
            byte[] expectedDiscriminator = PlemuProgramInstructions.GetAnchorDiscriminator("purchase_listing");
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(expectedDiscriminator[i], ix.Data[i]);
            }
        }

        [Test]
        public void PlemuProgramInstructions_InvalidArguments_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.GetAnchorDiscriminator(null));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.GetAnchorDiscriminator(""));

            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseSeat(null, "pack-1", 100));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseSeat(TestSender, null, 100));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseSeat(TestSender, "", 100));

            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseListing(null, "entry-1", 100));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseListing(TestSender, null, 100));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.PurchaseListing(TestSender, "", 100));
        }

        [Test]
        public void SolanaTransaction_Create_DeduplicatesAndOrdersAccountsCorrectly()
        {
            // Instruction 1: Transfer from TestSender to TestRecipient
            var ix1 = SolanaSystemProgram.Transfer(TestSender, TestRecipient, 1000);
            // Instruction 2: Purchase seat with TestSender as buyer
            var ix2 = PlemuProgramInstructions.PurchaseSeat(TestSender, "pack-xyz", 500);

            var tx = SolanaTransaction.Create(TestSender, new[] { ix1, ix2 }, TestBlockhash);

            // Fee payer must be first (index 0)
            Assert.AreEqual(TestSender, tx.AccountKeys[0]);
            // SystemProgram and SeatsProgram must be present in AccountKeys
            Assert.IsTrue(tx.AccountKeys.Contains(SystemProgramId));
            Assert.IsTrue(tx.AccountKeys.Contains(PlemuProgramInstructions.DefaultSeatsProgramId));
            Assert.IsTrue(tx.AccountKeys.Contains(TestRecipient));

            // TestSender should appear only once despite being in both instructions
            int countSender = 0;
            foreach (var key in tx.AccountKeys)
            {
                if (key == TestSender) countSender++;
            }
            Assert.AreEqual(1, countSender);
        }

        [Test]
        public void SolanaTransaction_Create_InvalidArguments_ThrowsArgumentException()
        {
            var ix = SolanaSystemProgram.Transfer(TestSender, TestRecipient, 1000);
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(null, new[] { ix }, TestBlockhash));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create("", new[] { ix }, TestBlockhash));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(TestSender, null, TestBlockhash));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(TestSender, Array.Empty<SolanaInstruction>(), TestBlockhash));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(TestSender, new[] { ix }, null));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(TestSender, new[] { ix }, ""));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Create(TestSender, new[] { ix }, "   "));
        }

        [Test]
        public void SolanaTransaction_SerializeAndDeserialize_LegacyWireFormat_RoundTrips()
        {
            var ix = SolanaSystemProgram.Transfer(TestSender, TestRecipient, 1500000);
            var tx = SolanaTransaction.Create(TestSender, new[] { ix }, TestBlockhash);

            byte[] wireBytes = tx.Serialize();
            Assert.IsNotNull(wireBytes);
            Assert.IsTrue(wireBytes.Length > 0);

            SolanaTransaction parsed = SolanaTransaction.Deserialize(wireBytes);
            Assert.AreEqual(1, parsed.Header.NumRequiredSignatures);
            Assert.AreEqual(0, parsed.Header.NumReadonlySignedAccounts);
            Assert.AreEqual(1, parsed.Header.NumReadonlyUnsignedAccounts);

            Assert.AreEqual(3, parsed.AccountKeys.Count);
            Assert.AreEqual(TestSender, parsed.AccountKeys[0]);
            Assert.AreEqual(TestRecipient, parsed.AccountKeys[1]);
            Assert.AreEqual(SystemProgramId, parsed.AccountKeys[2]);

            Assert.AreEqual(TestBlockhash, parsed.RecentBlockhash);
            Assert.AreEqual(1, parsed.Instructions.Count);

            var compiledIx = parsed.Instructions[0];
            Assert.AreEqual(2, compiledIx.ProgramIdIndex); // points to SystemProgramId at index 2
            Assert.AreEqual(2, compiledIx.AccountIndices.Length);
            Assert.AreEqual(0, compiledIx.AccountIndices[0]);
            Assert.AreEqual(1, compiledIx.AccountIndices[1]);
            Assert.AreEqual(12, compiledIx.Data.Length);
        }

        [Test]
        public void SolanaTransaction_Deserialize_TruncatedBytes_ThrowsArgumentException()
        {
            byte[] truncated = new byte[10];
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Deserialize(truncated));
            Assert.Throws<ArgumentException>(() => SolanaTransaction.Deserialize(null));
        }

        [Test]
        public void SolanaTransaction_Serialize_InvalidAccountKey_ThrowsFormatException()
        {
            var badIx = new SolanaInstruction(
                SystemProgramId,
                new[] { new SolanaAccountMeta("InvalidAccountKeyWithIllegalChars!@#$===", false, false) },
                Array.Empty<byte>());
            var tx = SolanaTransaction.Create(TestSender, new[] { badIx }, TestBlockhash);

            Assert.Throws<FormatException>(() => tx.Serialize());
        }

        [Test]
        public void SolanaTransaction_Serialize_ShortAccountKey_ThrowsFormatException()
        {
            // "Token123" is valid Base58 (~6 bytes), but Solana account keys must be exactly 32 bytes
            var badIx = new SolanaInstruction(
                SystemProgramId,
                new[] { new SolanaAccountMeta("Token123", false, false) },
                Array.Empty<byte>());
            var tx = SolanaTransaction.Create(TestSender, new[] { badIx }, TestBlockhash);

            Assert.Throws<FormatException>(() => tx.Serialize());
        }

        [Test]
        public void SolanaTransaction_Serialize_InvalidRecentBlockhash_ThrowsFormatException()
        {
            var ix = SolanaSystemProgram.Transfer(TestSender, TestRecipient, 1000);
            var tx = SolanaTransaction.Create(TestSender, new[] { ix }, "InvalidBlockhashIllegalCharacters0000000000000000===");

            Assert.Throws<FormatException>(() => tx.Serialize());
        }

        [Test]
        public void SolanaTransaction_Serialize_ShortRecentBlockhash_ThrowsFormatException()
        {
            // "Token123" is valid Base58 (~6 bytes), but recent blockhashes must be exactly 32 bytes
            var ix = SolanaSystemProgram.Transfer(TestSender, TestRecipient, 1000);
            var tx = SolanaTransaction.Create(TestSender, new[] { ix }, "Token123");

            Assert.Throws<FormatException>(() => tx.Serialize());
        }

        [Test]
        public async Task DefaultBlockhashProvider_OfflineRpc_ThrowsInvalidOperationExceptionFailClosed()
        {
            // Point provider to non-existent / unreachable port
            using var provider = new DefaultBlockhashProvider("http://127.0.0.1:54321/rpc");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetRecentBlockhashAsync());
            StringAssert.Contains("Solana RPC", ex.Message);
        }

        [Test]
        public async Task DefaultBlockhashProvider_EmptyBlockhashResponse_ThrowsInvalidOperationExceptionFailClosed()
        {
            string jsonResponse = "{\"jsonrpc\":\"2.0\",\"result\":{\"value\":{\"blockhash\":\"\"}},\"id\":1}";
            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
            var httpClient = new HttpClient(handler);
            using var provider = new DefaultBlockhashProvider("http://mock-rpc", httpClient);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetRecentBlockhashAsync());
            StringAssert.Contains("Solana RPC", ex.Message);
        }

        [Test]
        public async Task DefaultBlockhashProvider_HttpErrorResponse_ThrowsInvalidOperationExceptionFailClosed()
        {
            var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Internal Server Error");
            var httpClient = new HttpClient(handler);
            using var provider = new DefaultBlockhashProvider("http://mock-rpc", httpClient);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetRecentBlockhashAsync());
            StringAssert.Contains("Solana RPC", ex.Message);
        }

        [Test]
        public async Task DefaultBlockhashProvider_SuccessfulRpcResponse_ParsesBlockhash()
        {
            string expectedBlockhash = "9wFFkRz7yqLwN3oB4YyvBwZJz2kR8kHq5tT8uA1vB2c";
            string jsonResponse = $"{{\"jsonrpc\":\"2.0\",\"result\":{{\"value\":{{\"blockhash\":\"{expectedBlockhash}\"}}}},\"id\":1}}";

            var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
            var httpClient = new HttpClient(handler);
            using var provider = new DefaultBlockhashProvider("http://mock-rpc", httpClient);

            string blockhash = await provider.GetRecentBlockhashAsync();
            Assert.AreEqual(expectedBlockhash, blockhash);
        }

        [Test]
        public async Task MwaWallet_SendTip_SendsWireFormatSolanaTransactionPayload()
        {
            var capturingAdapter = new CapturingMwaAdapter();
            var blockhashProvider = new MockBlockhashProvider(TestBlockhash);
            var wallet = new MwaWallet(capturingAdapter, blockhashProvider);

            await wallet.Connect();
            string sig = await wallet.SendTip(TestRecipient, 2500000);

            Assert.IsNotNull(sig);
            byte[] capturedPayload = capturingAdapter.LastSerializedTransaction;
            Assert.IsNotNull(capturedPayload);

            // Ensure payload is NOT json string
            string asString = Encoding.UTF8.GetString(capturedPayload);
            Assert.IsFalse(asString.StartsWith("{\"type\":\"tip\""), "Payload must be binary wire format, not JSON string.");

            // Deserialize wire bytes to verify Solana VM compatibility
            var tx = SolanaTransaction.Deserialize(capturedPayload);
            Assert.AreEqual(1, tx.Header.NumRequiredSignatures);
            Assert.AreEqual(2, tx.Instructions.Count, "Atomic transaction must contain 2 instructions (CreateIdempotent ATA + TransferChecked).");

            var ataIx = tx.Instructions[0];
            string ataProgramId = tx.AccountKeys[ataIx.ProgramIdIndex];
            Assert.AreEqual(SplTokenProgramInstructions.AssociatedTokenProgramId, ataProgramId);
            Assert.AreEqual(1, ataIx.Data.Length);
            Assert.AreEqual(1, ataIx.Data[0]); // CreateIdempotent opcode

            var ix = tx.Instructions[1];
            string programId = tx.AccountKeys[ix.ProgramIdIndex];
            Assert.AreEqual(SplTokenProgramInstructions.TokenProgramId, programId);

            byte opcode = ix.Data[0];
            ulong amount = BitConverter.ToUInt64(ix.Data, 1);
            byte decimals = ix.Data[9];
            Assert.AreEqual(12, opcode, "Opcode must be 12 (TransferChecked)");
            Assert.AreEqual(2500000ul, amount);
            Assert.AreEqual(6, decimals);
        }

        [Test]
        public async Task MwaWallet_PurchaseSeat_SendsWireFormatAnchorInstructionPayload()
        {
            var capturingAdapter = new CapturingMwaAdapter();
            var blockhashProvider = new MockBlockhashProvider(TestBlockhash);
            var wallet = new MwaWallet(capturingAdapter, blockhashProvider);

            await wallet.Connect();
            string sig = await wallet.PurchaseSeat("retro-pack-01", 1000);

            Assert.IsNotNull(sig);
            byte[] capturedPayload = capturingAdapter.LastSerializedTransaction;
            Assert.IsNotNull(capturedPayload);

            var tx = SolanaTransaction.Deserialize(capturedPayload);
            Assert.AreEqual(1, tx.Header.NumRequiredSignatures);
            Assert.AreEqual(1, tx.Instructions.Count);

            var ix = tx.Instructions[0];
            Assert.IsTrue(ix.Data.Length >= 8);
        }

        [Test]
        public async Task MwaWallet_PurchaseListing_SendsWireFormatAnchorInstructionPayload()
        {
            var capturingAdapter = new CapturingMwaAdapter();
            var blockhashProvider = new MockBlockhashProvider(TestBlockhash);
            var wallet = new MwaWallet(capturingAdapter, blockhashProvider);

            await wallet.Connect();
            string sig = await wallet.PurchaseListing("entry-99", 5000);

            Assert.IsNotNull(sig);
            byte[] capturedPayload = capturingAdapter.LastSerializedTransaction;
            Assert.IsNotNull(capturedPayload);

            var tx = SolanaTransaction.Deserialize(capturedPayload);
            Assert.AreEqual(1, tx.Header.NumRequiredSignatures);
            Assert.AreEqual(1, tx.Instructions.Count);

            var ix = tx.Instructions[0];
            Assert.IsTrue(ix.Data.Length >= 8);
        }

        private sealed class CapturingMwaAdapter : IMobileWalletAdapter
        {
            public byte[] LastSerializedTransaction { get; private set; }

            public bool IsMwaAvailable() => true;

            public Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(MwaConnectionResult.Ok(TestSender, "test-auth-token"));
            }

            public Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
            {
                LastSerializedTransaction = serializedTransaction;
                return Task.FromResult(MwaTransactionResult.Ok("MwaSig-test-12345"));
            }

            public Task DisconnectAsync() => Task.CompletedTask;
        }

        private sealed class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;

            public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
            {
                _statusCode = statusCode;
                _content = content;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }
    }
}
