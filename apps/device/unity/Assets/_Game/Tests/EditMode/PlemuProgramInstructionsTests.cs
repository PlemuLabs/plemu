using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class PlemuProgramInstructionsTests
    {
        private const string TestBuyer = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
        private const string ValidSeatsKeyA = "ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL";
        private const string ValidMarketKeyA = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
        private const string ValidSeatsKeyB = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        private const string ValidMarketKeyB = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        private const string ValidExplicitKey = "4vJ9JU1bJJE96FCSDJvffWN8iCG3qUCo33pPtxcLd4FX";

        private class CapturingMwaAdapter : IMobileWalletAdapter
        {
            public bool IsAvailable => true;
            public byte[] LastSerializedTransaction { get; private set; }

            public bool IsMwaAvailable() => IsAvailable;

            public Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(MwaConnectionResult.Ok(TestBuyer, "auth-token-test"));
            }

            public Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
            {
                LastSerializedTransaction = serializedTransaction;
                return Task.FromResult(MwaTransactionResult.Ok("MwaSig-plemu-test"));
            }

            public Task DisconnectAsync()
            {
                return Task.CompletedTask;
            }
        }

        [SetUp]
        public void SetUp()
        {
            PlemuProgramInstructions.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            PlemuProgramInstructions.ResetToDefaults();
        }

        [Test]
        public void Defaults_MatchCanonicalConstants()
        {
            Assert.AreEqual(PlemuProgramInstructions.DefaultSeatsProgramId, PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual(PlemuProgramInstructions.DefaultMarketProgramId, PlemuProgramInstructions.MarketProgramId);
            Assert.AreEqual("SEAT111111111111111111111111111111111111111", PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual("MRKT111111111111111111111111111111111111111", PlemuProgramInstructions.MarketProgramId);
        }

        [Test]
        public void SetSeatsProgramId_WhenValid_UpdatesPropertyAndUsedByPurchaseSeat()
        {
            PlemuProgramInstructions.SeatsProgramId = ValidSeatsKeyA;

            Assert.AreEqual(ValidSeatsKeyA, PlemuProgramInstructions.SeatsProgramId);

            var ix = PlemuProgramInstructions.PurchaseSeat(TestBuyer, "pack-alpha", 5000);
            Assert.AreEqual(ValidSeatsKeyA, ix.ProgramId);
        }

        [Test]
        public void SetSeatsProgramId_WhenNullOrWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.SeatsProgramId = null);
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.SeatsProgramId = "");
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.SeatsProgramId = "   ");
        }

        [Test]
        public void SetSeatsProgramId_WhenInvalidFormatOrLength_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.SeatsProgramId = "invalid_base58_0OIl");
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.SeatsProgramId = "11111");
        }

        [Test]
        public void SetMarketProgramId_WhenValid_UpdatesPropertyAndUsedByPurchaseListing()
        {
            PlemuProgramInstructions.MarketProgramId = ValidMarketKeyA;

            Assert.AreEqual(ValidMarketKeyA, PlemuProgramInstructions.MarketProgramId);

            var ix = PlemuProgramInstructions.PurchaseListing(TestBuyer, "listing-beta", 8000);
            Assert.AreEqual(ValidMarketKeyA, ix.ProgramId);
        }

        [Test]
        public void SetMarketProgramId_WhenNullOrWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.MarketProgramId = null);
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.MarketProgramId = "");
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.MarketProgramId = "   ");
        }

        [Test]
        public void SetMarketProgramId_WhenInvalidFormatOrLength_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.MarketProgramId = "invalid_base58_0OIl");
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.MarketProgramId = "11111");
        }

        [Test]
        public void Configure_WhenValid_SetsBothProgramIds()
        {
            PlemuProgramInstructions.Configure(ValidSeatsKeyA, ValidMarketKeyA);

            Assert.AreEqual(ValidSeatsKeyA, PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual(ValidMarketKeyA, PlemuProgramInstructions.MarketProgramId);
        }

        [Test]
        public void Configure_WhenNullOrWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.Configure(null, ValidMarketKeyA));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.Configure(ValidSeatsKeyA, null));
        }

        [Test]
        public void ConfigureFromJson_StandardKeys_ResolvesAndSetsProgramIds()
        {
            string json = $@"{{
                ""network"": ""devnet"",
                ""plemuMint"": ""xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu"",
                ""seatsProgramId"": ""{ValidSeatsKeyA}"",
                ""marketProgramId"": ""{ValidMarketKeyA}""
            }}";

            PlemuProgramInstructions.ConfigureFromJson(json);

            Assert.AreEqual(ValidSeatsKeyA, PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual(ValidMarketKeyA, PlemuProgramInstructions.MarketProgramId);
        }

        [Test]
        public void ConfigureFromJson_AlternateKeys_ResolvesAndSetsProgramIds()
        {
            string json = $@"{{
                ""seatsProgram"": ""{ValidSeatsKeyB}"",
                ""marketProgram"": ""{ValidMarketKeyB}""
            }}";

            PlemuProgramInstructions.ConfigureFromJson(json);

            Assert.AreEqual(ValidSeatsKeyB, PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual(ValidMarketKeyB, PlemuProgramInstructions.MarketProgramId);
        }

        [Test]
        public void ConfigureFromJson_WhenInvalidOrEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.ConfigureFromJson(null));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.ConfigureFromJson(""));
            Assert.Throws<ArgumentException>(() => PlemuProgramInstructions.ConfigureFromJson("not-a-valid-json"));
        }

        [Test]
        public void ResetToDefaults_RestoresCanonicalDefaults()
        {
            PlemuProgramInstructions.SeatsProgramId = ValidSeatsKeyA;
            PlemuProgramInstructions.MarketProgramId = ValidMarketKeyA;

            PlemuProgramInstructions.ResetToDefaults();

            Assert.AreEqual(PlemuProgramInstructions.DefaultSeatsProgramId, PlemuProgramInstructions.SeatsProgramId);
            Assert.AreEqual(PlemuProgramInstructions.DefaultMarketProgramId, PlemuProgramInstructions.MarketProgramId);
        }

        [Test]
        public void PurchaseSeat_ExplicitProgramId_OverridesStaticDefault()
        {
            PlemuProgramInstructions.SeatsProgramId = ValidSeatsKeyA;

            var ix = PlemuProgramInstructions.PurchaseSeat(TestBuyer, "pack-override", 1234, ValidExplicitKey);

            Assert.AreEqual(ValidExplicitKey, ix.ProgramId);
        }

        [Test]
        public void PurchaseListing_ExplicitProgramId_OverridesStaticDefault()
        {
            PlemuProgramInstructions.MarketProgramId = ValidMarketKeyA;

            var ix = PlemuProgramInstructions.PurchaseListing(TestBuyer, "entry-override", 4321, ValidExplicitKey);

            Assert.AreEqual(ValidExplicitKey, ix.ProgramId);
        }

        [Test]
        public async Task MwaWallet_WithInjectedProgramIds_UsesInjectedIdsForTransactions()
        {
            var mockAdapter = new CapturingMwaAdapter();
            await mockAdapter.ConnectAsync();
            var mockBlockhash = new MockBlockhashProvider();

            var wallet = new MwaWallet(mockAdapter, mockBlockhash, ValidSeatsKeyA, ValidMarketKeyA);
            await wallet.Connect();

            await wallet.PurchaseSeat("pack-injected", 7000);
            byte[] lastPayload = mockAdapter.LastSerializedTransaction;
            Assert.IsNotNull(lastPayload);

            // Verify that the injected program ID is serialized into the transaction account keys
            var tx = SolanaTransaction.Deserialize(lastPayload);
            Assert.IsTrue(tx.AccountKeys.Contains(ValidSeatsKeyA));

            await wallet.PurchaseListing("entry-injected", 9000);
            byte[] lastMarketPayload = mockAdapter.LastSerializedTransaction;
            Assert.IsNotNull(lastMarketPayload);

            var txMarket = SolanaTransaction.Deserialize(lastMarketPayload);
            Assert.IsTrue(txMarket.AccountKeys.Contains(ValidMarketKeyA));
        }
    }
}
