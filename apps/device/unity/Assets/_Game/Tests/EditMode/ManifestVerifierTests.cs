using System;
using System.Text;
using NUnit.Framework;
using Plemu.Content;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class ManifestVerifierTests
    {
        private static readonly DateTimeOffset NowJul2026 =
            new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

        private byte[] _manifestBytes;
        private byte[] _tamperedBytes;
        private byte[] _sigBytes;

        [SetUp]
        public void SetUp()
        {
            _manifestBytes = FixtureLoader.ReadBytes("manifest.json");
            _tamperedBytes = FixtureLoader.ReadBytes("manifest.tampered.json");
            _sigBytes = FixtureLoader.ReadBytes("manifest.sig");
        }

        [Test]
        public void Verify_ValidFixture_IsValidAndFresh()
        {
            // Arrange
            KeyRegistry keys = PinnedKeys.BuiltIn;

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, keys, NowJul2026);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Failure, Is.Null);
            Assert.That(result.Freshness, Is.EqualTo(Freshness.Fresh));
            Assert.That(result.Manifest, Is.Not.Null);
            Assert.That(result.Manifest.Sequence, Is.EqualTo(1));
            Assert.That(result.Manifest.SignerKeyId, Is.EqualTo(PinnedKeys.DevKeyIdA));
        }

        [Test]
        public void Verify_TamperedManifestWithValidSig_SignatureInvalidBeforeParse()
        {
            // Arrange
            KeyRegistry keys = PinnedKeys.BuiltIn;

            // Act
            VerificationResult result = ManifestVerifier.Verify(_tamperedBytes, _sigBytes, keys, NowJul2026);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(VerificationFailure.SignatureInvalid));
            // Proves the fail-before-parse guarantee: no manifest was parsed.
            Assert.That(result.Manifest, Is.Null);
        }

        [Test]
        public void Verify_EnvelopeKeyIdSwappedToDevBWithDevASignature_SignatureInvalid()
        {
            // Arrange: dev-b is a real pinned key, so lookup + status + alg all pass, but the
            // signature was made by dev-a, so Ed25519 verification against dev-b's key fails.
            byte[] rewrittenSig = RewriteEnvelopeKeyId(_sigBytes, PinnedKeys.DevKeyIdB);
            KeyRegistry keys = PinnedKeys.BuiltIn;

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, rewrittenSig, keys, NowJul2026);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(VerificationFailure.SignatureInvalid));
            Assert.That(result.Manifest, Is.Null);
        }

        [Test]
        public void Verify_UnknownKeyId_UnknownKey()
        {
            // Arrange
            byte[] rewrittenSig = RewriteEnvelopeKeyId(_sigBytes, "plemu-manifest-nonexistent");
            KeyRegistry keys = PinnedKeys.BuiltIn;

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, rewrittenSig, keys, NowJul2026);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(VerificationFailure.UnknownKey));
            Assert.That(result.Manifest, Is.Null);
        }

        [Test]
        public void Verify_RetiredKeyInRegistry_KeyNotAcceptable()
        {
            // Arrange: a test-only registry where the signing key id is present but retired.
            KeyRegistry keys = new KeyRegistry
            {
                Keys =
                {
                    new KeyEntry
                    {
                        KeyId = PinnedKeys.DevKeyIdA,
                        PublicKey = PinnedKeys.DevPublicKeyA,
                        Status = "retired",
                    },
                },
            };

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, keys, NowJul2026);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(VerificationFailure.KeyNotAcceptable));
            Assert.That(result.Manifest, Is.Null);
        }

        [Test]
        public void Verify_NowBeforeExpiry_Fresh()
        {
            // Arrange: fixture expiresAt is 2030-01-01T00:00:00Z.
            DateTimeOffset now = new DateTimeOffset(2029, 12, 31, 0, 0, 0, TimeSpan.Zero);

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn, now);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(Freshness.Fresh));
        }

        [Test]
        public void Verify_NowWithinGrace_Grace()
        {
            // Arrange
            DateTimeOffset now = new DateTimeOffset(2030, 1, 3, 0, 0, 0, TimeSpan.Zero);

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn, now);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(Freshness.Grace));
        }

        [Test]
        public void Verify_NowAtGraceBoundary_Grace()
        {
            // Arrange: exactly expiresAt + 7 days is inclusive (still Grace).
            DateTimeOffset now = new DateTimeOffset(2030, 1, 8, 0, 0, 0, TimeSpan.Zero);

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn, now);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(Freshness.Grace));
        }

        [Test]
        public void Verify_NowBeyondGrace_Expired()
        {
            // Arrange: one day past the grace boundary.
            DateTimeOffset now = new DateTimeOffset(2030, 1, 9, 0, 0, 0, TimeSpan.Zero);

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn, now);

            // Assert: still authenticated, but reported Expired (enforcement is LaunchGate's job).
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Freshness, Is.EqualTo(Freshness.Expired));
        }

        [Test]
        public void Verify_MinSequenceAboveManifestSequence_SequenceRollback()
        {
            // Arrange: fixture sequence is 1; require at least 2.
            KeyRegistry keys = PinnedKeys.BuiltIn;

            // Act
            VerificationResult result = ManifestVerifier.Verify(_manifestBytes, _sigBytes, keys, NowJul2026, minSequence: 2);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Failure, Is.EqualTo(VerificationFailure.SequenceRollback));
        }

        private static byte[] RewriteEnvelopeKeyId(byte[] sigBytes, string newKeyId)
        {
            // The fixture envelope's keyId is dev-a; swap that exact value for the target.
            // Pure string replacement keeps this test free of any JSON-library dependency.
            string json = Encoding.UTF8.GetString(sigBytes);
            string rewritten = json.Replace(
                "\"" + PinnedKeys.DevKeyIdA + "\"",
                "\"" + newKeyId + "\"");
            return Encoding.UTF8.GetBytes(rewritten);
        }
    }
}
