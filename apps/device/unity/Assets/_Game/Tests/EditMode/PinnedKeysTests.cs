using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class PinnedKeysTests
    {
        [Test]
        public void BuiltIn_ContainsExpectedPinnedKeys()
        {
            // Act
            KeyRegistry registry = PinnedKeys.BuiltIn;

            // Assert (dev-a, dev-b, prod-a, prod-b, emergency-1)
            Assert.That(registry.Keys, Has.Count.EqualTo(5));
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
        public void BuiltIn_ProdKeyA_IsActive()
        {
            // Act
            KeyEntry keyProdA = PinnedKeys.BuiltIn.Find("plemu-manifest-prod-a");

            // Assert
            Assert.That(keyProdA, Is.Not.Null);
            Assert.That(keyProdA.Status, Is.EqualTo("active"));
            Assert.That(keyProdA.IsAcceptable, Is.True);
            Assert.That(keyProdA.PublicKey, Is.EqualTo(PinnedKeys.ProdPublicKeyA));
        }

        [Test]
        public void BuiltIn_ProdKeyB_IsNext()
        {
            // Act
            KeyEntry keyProdB = PinnedKeys.BuiltIn.Find("plemu-manifest-prod-b");

            // Assert
            Assert.That(keyProdB, Is.Not.Null);
            Assert.That(keyProdB.Status, Is.EqualTo("next"));
            Assert.That(keyProdB.IsAcceptable, Is.True);
            Assert.That(keyProdB.PublicKey, Is.EqualTo(PinnedKeys.ProdPublicKeyB));
        }

        [Test]
        public void BuiltIn_EmergencyKey_IsEmergency()
        {
            // Act
            KeyEntry keyEmerg = PinnedKeys.BuiltIn.Find("plemu-emergency-1");

            // Assert
            Assert.That(keyEmerg, Is.Not.Null);
            Assert.That(keyEmerg.Status, Is.EqualTo("emergency"));
            Assert.That(keyEmerg.IsAcceptable, Is.True);
            Assert.That(keyEmerg.PublicKey, Is.EqualTo(PinnedKeys.EmergencyPublicKey));
        }

        [Test]
        public void BuiltIn_ReturnsFreshInstanceEachCall()
        {
            // Act
            KeyRegistry first = PinnedKeys.BuiltIn;
            KeyRegistry second = PinnedKeys.BuiltIn;

            // Assert: mutating one must not affect the other (no shared trust state).
            first.Keys.Clear();
            Assert.That(second.Keys, Has.Count.EqualTo(5));
        }
    }
}
