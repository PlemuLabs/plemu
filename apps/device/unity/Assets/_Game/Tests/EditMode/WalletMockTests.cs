using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Plemu.Wallet.Mock;

namespace Plemu.Tests
{
    [TestFixture]
    public class WalletMockTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PlemuTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public async Task MockWallet_ConnectLifecycle_UpdatesConnectionAndKey()
        {
            var wallet = new MockWallet();
            Assert.IsFalse(wallet.IsConnected);
            Assert.IsNull(wallet.PublicKey);

            bool connected = await wallet.Connect();
            Assert.IsTrue(connected);
            Assert.IsTrue(wallet.IsConnected);
            Assert.IsNotNull(wallet.PublicKey);
            Assert.IsTrue(wallet.PublicKey.StartsWith("MockWa11et"));

            wallet.Disconnect();
            Assert.IsFalse(wallet.IsConnected);
            Assert.IsNull(wallet.PublicKey);
        }

        [Test]
        public async Task MockWallet_Operations_ReturnDeterministicSignatures()
        {
            var wallet = new MockWallet();
            await wallet.Connect();

            string tipSig = await wallet.SendTip("recipient123", 1000);
            Assert.IsNotNull(tipSig);
            Assert.IsTrue(tipSig.StartsWith("MockSig-tip-"));

            string seatSig = await wallet.PurchaseSeat("speedrun-club", 5000);
            Assert.IsNotNull(seatSig);
            Assert.IsTrue(seatSig.StartsWith("MockSig-seat-"));

            string listingSig = await wallet.PurchaseListing("entry-libbet", 2500);
            Assert.IsNotNull(listingSig);
            Assert.IsTrue(listingSig.StartsWith("MockSig-listing-"));
        }

        [Test]
        public async Task MockSeatProvider_WhenFileNotFound_ReturnsNoSeats()
        {
            string nonExistentPath = Path.Combine(_tempDir, "non-existent-seats.json");
            var provider = new MockSeatProvider(nonExistentPath);

            bool hasSeat = await provider.HasSeat("speedrun-club");
            Assert.IsFalse(hasSeat);

            var packs = await provider.MyPacks();
            Assert.IsNotNull(packs);
            Assert.AreEqual(0, packs.Count);
        }

        [Test]
        public async Task MockSeatProvider_WhenFilePresent_ReturnsConfiguredSeats()
        {
            string seatsFile = Path.Combine(_tempDir, "mock-seats.json");
            File.WriteAllText(seatsFile, "{\"packs\":[\"speedrun-club\",\"translation-club\"]}");

            var provider = new MockSeatProvider(seatsFile);

            Assert.IsTrue(await provider.HasSeat("speedrun-club"));
            Assert.IsTrue(await provider.HasSeat("translation-club"));
            Assert.IsFalse(await provider.HasSeat("retro-pack"));

            var packs = await provider.MyPacks();
            Assert.AreEqual(2, packs.Count);
            Assert.Contains("speedrun-club", (System.Collections.ICollection)packs);
            Assert.Contains("translation-club", (System.Collections.ICollection)packs);
        }
    }
}
