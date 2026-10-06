using System;
using System.IO;
using NUnit.Framework;
using Plemu.App;
using Plemu.Content;
using Plemu.Core;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class LocalLaneIsolationTests
    {
        private string _tempDir;
        private string _localDir;
        private LocalLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PlemuTests_" + Guid.NewGuid().ToString("N"));
            _localDir = Path.Combine(_tempDir, "local");
            Directory.CreateDirectory(_localDir);
            _library = new LocalLibrary(_localDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { }
            }
        }

        [Test]
        public void LocalStore_RootIsOutsideEverySignedPath()
        {
            StringAssert.Contains("local", _library.Root);
            Assert.That(_library.Root.Contains("/Content/artifacts"), Is.False);
            Assert.That(_library.Root.Contains("\\Content\\artifacts"), Is.False);
        }

        [Test]
        public void LocalEntry_IsNeverEligibleForPlatformFeatures()
        {
            string fakeRom = CreateTempFile("testgame.gb", 1024);
            LocalEntry local = _library.Import(fakeRom);
            CatalogEntry entry = _library.ToCatalogEntry(local);

            Assert.That(entry.Lane, Is.EqualTo(ContentLane.Local));
            Assert.That(entry.CanTip, Is.False, "Local unverified files must never accept tips");
            Assert.That(entry.CanMint, Is.False, "Local unverified files must never be minted");
            Assert.That(entry.CanPromote, Is.False, "Local files cannot be promoted without passing signed review");
            Assert.That(entry.CreatorWallet, Is.Null, "Local files have no verified payee wallet");
        }

        [Test]
        public void SignedAuthorize_NeverAcceptsALocalEntry()
        {
            string fakeRom = CreateTempFile("homebrew.gb", 2048);
            LocalEntry local = _library.Import(fakeRom);

            // Using mock signed source
            var manifestBytes = FixtureLoader.ReadBytes("manifest.json");
            var sigBytes = FixtureLoader.ReadBytes("manifest.sig");
            var bundle = new ManifestBundle(manifestBytes, sigBytes, PinnedKeys.BuiltIn);
            var signedSource = new TestManifestSource(bundle);
            var signedStore = new LocalContentStore(_localDir);

            LaunchDecision decision = LaunchGate.Authorize(local.Id, signedSource, signedStore, new TestClock(DateTimeOffset.UtcNow));

            Assert.That(decision.IsAuthorized, Is.False, "Signed LaunchGate must reject local unverified IDs");
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.TitleNotFound));
        }

        [Test]
        public void Import_RejectsFileOverSystemSizeCap()
        {
            // Size limit is 64 MiB
            string hugeFile = Path.Combine(_tempDir, "huge.gb");
            using (var fs = new FileStream(hugeFile, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(65 * 1024 * 1024); // 65 MiB
            }

            var ex = Assert.Throws<LocalImportException>(() => _library.Import(hugeFile));
            Assert.That(ex.Error, Is.EqualTo(LocalImportError.TooLarge));
        }

        [Test]
        public void Import_RejectsArchive()
        {
            string archiveFile = CreateTempFile("bomb.zip", 512);

            var ex = Assert.Throws<LocalImportException>(() => _library.Import(archiveFile));
            Assert.That(ex.Error, Is.EqualTo(LocalImportError.ArchiveNotPermitted));
        }

        [Test]
        public void AuthorizeLocal_ValidFile_IssuesTicket()
        {
            string romFile = CreateTempFile("custom.gb", 1024);
            LocalEntry local = _library.Import(romFile);

            LaunchDecision decision = LocalLaunchGate.AuthorizeLocal(local.Id, _library, new TestClock(DateTimeOffset.UtcNow));

            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Ticket.TitleId, Is.EqualTo(local.Id));
            Assert.That(decision.Ticket.System, Is.EqualTo("gb"));
            Assert.That(decision.Ticket.ContentPath, Is.EqualTo(local.FilePath));
        }

        private string CreateTempFile(string fileName, int bytesCount)
        {
            string path = Path.Combine(_tempDir, fileName);
            byte[] data = new byte[bytesCount];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(path, data);
            return path;
        }

        private sealed class TestManifestSource : IManifestSource
        {
            private readonly ManifestBundle _bundle;
            public bool IsBundled => false;
            public TestManifestSource(ManifestBundle bundle) => _bundle = bundle;
            public ManifestBundle GetVerifiedManifest() => _bundle;
        }

        private sealed class TestClock : IClock
        {
            public DateTimeOffset UtcNow { get; }
            public TestClock(DateTimeOffset now) => UtcNow = now;
        }
    }
}
