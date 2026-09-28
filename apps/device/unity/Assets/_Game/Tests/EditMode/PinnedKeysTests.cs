using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class PinnedKeysTests
    {
        [Test]
        public void BuiltIn_ContainsExactlyTwoDevKeys()
        {
            // Act
            KeyRegistry registry = PinnedKeys.BuiltIn;

            // Assert
            Assert.That(registry.Keys, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuiltIn_DevKeyA_IsActiveWithSpecHex()
        {
            // Act
            KeyEntry keyA = PinnedKeys.BuiltIn.Find("plemu-manifest-dev-a");

            // Assert
            Assert.That(keyA, Is.Not.Null);
            Assert.That(keyA.Status, Is.EqualTo("active"));
            Assert.That(keyA.IsAcceptable, Is.True);
            Assert.That(keyA.PublicKey,
                Is.EqualTo("1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c"));
        }

        [Test]
        public void BuiltIn_DevKeyB_IsNextWithSpecHex()
        {
            // Act
            KeyEntry keyB = PinnedKeys.BuiltIn.Find("plemu-manifest-dev-b");

            // Assert
            Assert.That(keyB, Is.Not.Null);
            Assert.That(keyB.Status, Is.EqualTo("next"));
            Assert.That(keyB.IsAcceptable, Is.True);
            Assert.That(keyB.PublicKey,
                Is.EqualTo("b3a544ba95bc44519de7782b6bd0d712808a602bccd3ef016dbb896960cce1f7"));
        }

        [Test]
        public void BuiltIn_ReturnsFreshInstanceEachCall()
        {
            // Act
            KeyRegistry first = PinnedKeys.BuiltIn;
            KeyRegistry second = PinnedKeys.BuiltIn;

            // Assert: mutating one must not affect the other (no shared trust state).
            first.Keys.Clear();
            Assert.That(second.Keys, Has.Count.EqualTo(2));
        }
    }
}
