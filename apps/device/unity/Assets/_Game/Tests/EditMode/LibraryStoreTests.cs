using System.IO;
using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class LibraryStoreTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "plemu-librarystore-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "library.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void SaveThenLoad_Roundtrip_PreservesEntries()
        {
            // Arrange
            LibraryStore store = new LibraryStore();
            store.Entries["gb/starforge-demo"] = new LibraryEntry
            {
                InstalledHash = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd",
                Version = "0.16.3",
                InstallState = "installed",
                LastPlayedUtc = "2026-07-12T00:00:00Z",
            };

            // Act
            store.Save(_path);
            LibraryStore loaded = LibraryStore.Load(_path);

            // Assert
            Assert.That(loaded.RecoveredCorrupt, Is.False);
            Assert.That(loaded.Entries, Has.Count.EqualTo(1));
            LibraryEntry entry = loaded.Entries["gb/starforge-demo"];
            Assert.That(entry.InstalledHash, Is.EqualTo(store.Entries["gb/starforge-demo"].InstalledHash));
            Assert.That(entry.Version, Is.EqualTo("0.16.3"));
            Assert.That(entry.InstallState, Is.EqualTo("installed"));
            Assert.That(entry.LastPlayedUtc, Is.EqualTo("2026-07-12T00:00:00Z"));
        }

        [Test]
        public void Load_MissingFile_ReturnsEmptyStore()
        {
            // Arrange
            string missing = Path.Combine(_dir, "does-not-exist.json");

            // Act
            LibraryStore loaded = LibraryStore.Load(missing);

            // Assert
            Assert.That(loaded.Entries, Is.Empty);
            Assert.That(loaded.RecoveredCorrupt, Is.False);
        }

        [Test]
        public void Load_CorruptFile_ReturnsEmptyStoreAndFlagsRecovered()
        {
            // Arrange
            File.WriteAllText(_path, "{ this is not valid json ][");

            // Act
            LibraryStore loaded = LibraryStore.Load(_path);

            // Assert
            Assert.That(loaded.RecoveredCorrupt, Is.True);
            Assert.That(loaded.Entries, Is.Empty);
        }

        [Test]
        public void Save_OnSuccess_LeavesNoTempFile()
        {
            // Arrange
            LibraryStore store = new LibraryStore();
            store.Entries["a"] = new LibraryEntry { InstallState = "installed" };

            // Act: save twice to exercise both the first-write (move) and replace paths.
            store.Save(_path);
            store.Save(_path);

            // Assert
            Assert.That(File.Exists(_path), Is.True);
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }
    }
}
