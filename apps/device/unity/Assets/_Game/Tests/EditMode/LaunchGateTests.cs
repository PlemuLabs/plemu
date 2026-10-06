using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Plemu.Content;
using Plemu.Core;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class LaunchGateTests
    {
        private const string StarforgeTitle = "gb/starforge-demo";
        private const string RevokedTitle = "tic80/revoked-example";

        private static readonly DateTimeOffset FreshNow =
            new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

        private byte[] _manifestBytes;
        private byte[] _sigBytes;
        private byte[] _artifactBytes;

        [SetUp]
        public void SetUp()
        {
            _manifestBytes = FixtureLoader.ReadBytes("manifest.json");
            _sigBytes = FixtureLoader.ReadBytes("manifest.sig");
            _artifactBytes = FixtureLoader.ReadBytes("starforge-demo.bytes");
        }

        [Test]
        public void Authorize_HappyPath_IssuesTicketWithCorrectFields()
        {
            // Arrange
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();
            store.Set(StarforgeTitle, _artifactBytes);

            // Act
            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
            Assert.That(decision.Ticket.TitleId, Is.EqualTo(StarforgeTitle));
            Assert.That(decision.Ticket.System, Is.EqualTo("gb"));
            Assert.That(decision.Ticket.CoreId, Is.EqualTo("sameboy"));
            Assert.That(decision.Ticket.Sha256,
                Is.EqualTo("70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd"));
            Assert.That(decision.Ticket.ContentPath, Is.EqualTo(StarforgeTitle));
            Assert.That(store.QuarantinedTitles, Is.Empty);
        }

        [Test]
        public void Authorize_RevokedEntry_DeniesRevoked()
        {
            // Arrange
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();
            store.Set(RevokedTitle, new byte[1024]);

            // Act
            LaunchDecision decision = LaunchGate.Authorize(RevokedTitle, source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.Revoked));
        }

        [Test]
        public void Authorize_TamperedContent_DeniesHashMismatchAndQuarantines()
        {
            // Arrange: store returns bytes that do not match the entry digest.
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();
            byte[] tampered = (byte[])_artifactBytes.Clone();
            tampered[0] ^= 0xFF;
            store.Set(StarforgeTitle, tampered);

            // Act
            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.HashMismatch));
            Assert.That(store.QuarantinedTitles, Does.Contain(StarforgeTitle));
        }

        [Test]
        public void Authorize_MissingContent_DeniesContentMissing()
        {
            // Arrange: store has no artifact for the title.
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.ContentMissing));
            Assert.That(store.QuarantinedTitles, Is.Empty);
        }

        [Test]
        public void Authorize_ClockBeyondGrace_DeniesManifestExpired()
        {
            // Arrange: fixture expires 2030-01-01; grace ends 2030-01-08; this is well beyond.
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();
            store.Set(StarforgeTitle, _artifactBytes);
            DateTimeOffset beyondGrace = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, source, store, new FakeClock(beyondGrace));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.ManifestExpired));
        }

        [Test]
        public void Authorize_UnknownTitle_DeniesTitleNotFound()
        {
            // Arrange
            FakeManifestSource source = ValidSource();
            FakeContentStore store = new FakeContentStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/does-not-exist", source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.TitleNotFound));
        }

        [Test]
        public void Authorize_PaidTitle_BuyOnce_NoEntitlementsStore_DeniesEntitlementMissing()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/paid-game", "buy_once", "pda_license", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/paid-game", artifact);

            // Act - Using 4-argument overload (null entitlements)
            LaunchDecision decision = LaunchGate.Authorize("gb/paid-game", source, store, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.EntitlementMissing));
        }

        [Test]
        public void Authorize_PaidTitle_BuyOnce_NoTicketInStore_DeniesEntitlementMissing()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/paid-game", "buy_once", "pda_license", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/paid-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/paid-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.EntitlementMissing));
        }

        [Test]
        public void Authorize_PaidTitle_Pwyw_NoTicketInStore_DeniesEntitlementMissing()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/pwyw-game", "pwyw", "pda_license", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/pwyw-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/pwyw-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.EntitlementMissing));
        }

        [Test]
        public void Authorize_PaidTitle_DemoFull_PaidVersion_NoTicket_DeniesEntitlementMissing()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/full-game", "demo_full", "pda_license", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/full-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/full-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.EntitlementMissing));
        }

        [Test]
        public void Authorize_PaidTitle_DemoFull_FreeDemoVersion_NoTicket_Authorizes()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/demo-game", "demo_full", "none", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/demo-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/demo-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
            Assert.That(decision.Ticket.TitleId, Is.EqualTo("gb/demo-game"));
        }

        [Test]
        public void Authorize_PaidTitle_BuyOnce_WithValidTicket_Authorizes()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/paid-game", "buy_once", "pda_license", artifact, out string sha256);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/paid-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();
            entitlements.AddTicket(sha256, FreshNow.AddDays(30));

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/paid-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
            Assert.That(decision.Ticket.TitleId, Is.EqualTo("gb/paid-game"));
        }

        [Test]
        public void Authorize_PaidTitle_WithinOfflineGracePeriod_Authorizes()
        {
            // Arrange: ticket expired 3 days ago; within 7-day GracePeriod
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/grace-game", "buy_once", "pda_license", artifact, out string sha256);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/grace-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();
            entitlements.AddTicket(sha256, FreshNow.AddDays(-3));

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/grace-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
        }

        [Test]
        public void Authorize_PaidTitle_BeyondOfflineGracePeriod_DeniesEntitlementMissing()
        {
            // Arrange: ticket expired 8 days ago; beyond 7-day GracePeriod
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/expired-game", "buy_once", "pda_license", artifact, out string sha256);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/expired-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();
            entitlements.AddTicket(sha256, FreshNow.AddDays(-8));

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/expired-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.EntitlementMissing));
        }

        [Test]
        public void Authorize_FreeTitle_FreeModel_WithoutTicket_Authorizes()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/free-game", "free", "none", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/free-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/free-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
        }

        [Test]
        public void Authorize_FreeTitle_TipOnlyModel_WithoutTicket_Authorizes()
        {
            // Arrange
            byte[] artifact = new byte[] { 1, 2, 3, 4 };
            FakeManifestSource source = CreateMonetizedSource("gb/tip-game", "tip_only", "none", artifact, out _);
            FakeContentStore store = new FakeContentStore();
            store.Set("gb/tip-game", artifact);
            FakeEntitlementStore entitlements = new FakeEntitlementStore();

            // Act
            LaunchDecision decision = LaunchGate.Authorize("gb/tip-game", source, store, entitlements, new FakeClock(FreshNow));

            // Assert
            Assert.That(decision.IsAuthorized, Is.True);
            Assert.That(decision.Denial, Is.Null);
        }

        private FakeManifestSource CreateMonetizedSource(
            string titleId,
            string model,
            string delivery,
            byte[] artifactBytes,
            out string sha256)
        {
            byte[] seed = new byte[32];
            for (int i = 0; i < 32; i++) seed[i] = (byte)(i + 42);
            Chaos.NaCl.Ed25519.KeyPairFromSeed(out byte[] pubKey, out byte[] privKey, seed);

            using var sha = System.Security.Cryptography.SHA256.Create();
            sha256 = Convert.ToHexString(sha.ComputeHash(artifactBytes)).ToLowerInvariant();

            const string testKeyId = "test-monetized-key";
            var keys = new KeyRegistry();
            keys.Keys.Add(new KeyEntry
            {
                KeyId = testKeyId,
                PublicKey = Convert.ToHexString(pubKey).ToLowerInvariant(),
                Status = "active",
                ValidFrom = "2026-01-01T00:00:00Z"
            });

            var entry = new ManifestEntry
            {
                Id = titleId,
                Title = $"Title {titleId}",
                System = "gb",
                CoreId = "sameboy",
                MinCoreVersion = "0.1.0",
                Sha256 = sha256,
                SizeBytes = artifactBytes.Length,
                DownloadUrls = new List<string> { $"https://cdn.plemu.xyz/artifacts/{sha256}" },
                Creator = new Creator { Wallet = "11111111111111111111111111111111", DisplayName = "Author" },
                License = new LicenseInfo { Type = "Commercial", EvidenceUrl = "https://plemu.xyz" },
                Attestation = new Attestation { Role = "creator", Message = "Attestation", Signature = "sig", SignedAt = "2026-07-12T00:00:00Z" },
                Artwork = new Artwork { IconUrl = "https://plemu.xyz/icon.png" },
                AddedAt = "2026-07-12T00:00:00Z",
                IsRevoked = false,
                Tags = new List<string> { "game" }
            };

            if (model != null)
            {
                entry.Monetization = new MonetizationInfo
                {
                    Model = model,
                    Delivery = delivery ?? "none"
                };
            }

            var manifest = new Manifest
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-07-12T00:00:00Z",
                ExpiresAt = "2030-01-01T00:00:00Z",
                Sequence = 1,
                SignerKeyId = testKeyId,
                Entries = new List<ManifestEntry> { entry }
            };

            string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
            byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);
            byte[] signature = Chaos.NaCl.Ed25519.Sign(manifestBytes, privKey);

            var envelope = new SignatureEnvelope
            {
                KeyId = testKeyId,
                Alg = "ed25519",
                Signature = Convert.ToBase64String(signature),
                SignedAt = "2026-07-12T00:00:00Z"
            };
            byte[] sigBytes = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(envelope));

            var bundle = new ManifestBundle(manifestBytes, sigBytes, keys);
            return new FakeManifestSource(bundle);
        }

        private sealed class FakeEntitlementStore : IEntitlementStore
        {
            private readonly Dictionary<string, DateTimeOffset> _tickets =
                new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

            public void AddTicket(string artifactSha256, DateTimeOffset expiresAt)
            {
                _tickets[artifactSha256] = expiresAt;
            }

            public bool HasValidTicket(string artifactSha256, DateTimeOffset currentTime)
            {
                if (!_tickets.TryGetValue(artifactSha256, out DateTimeOffset expiresAt))
                    return false;

                return currentTime <= expiresAt + FreshnessPolicy.GracePeriod;
            }
        }

        private FakeManifestSource ValidSource()
        {
            return new FakeManifestSource(
                new ManifestBundle(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn));
        }

        private sealed class FakeClock : IClock
        {
            public FakeClock(DateTimeOffset now) => UtcNow = now;

            public DateTimeOffset UtcNow { get; }
        }

        private sealed class FakeManifestSource : IManifestSource
        {
            private readonly ManifestBundle _bundle;

            public FakeManifestSource(ManifestBundle bundle) => _bundle = bundle;

            public ManifestBundle GetVerifiedManifest() => _bundle;
        }

        private sealed class FakeContentStore : IContentStore
        {
            private readonly Dictionary<string, byte[]> _content = new Dictionary<string, byte[]>();

            public List<string> QuarantinedTitles { get; } = new List<string>();

            public void Set(string titleId, byte[] bytes) => _content[titleId] = bytes;

            public Stream OpenRead(string titleId)
            {
                return _content.TryGetValue(titleId, out byte[] bytes)
                    ? new MemoryStream(bytes)
                    : null;
            }

            public void Quarantine(string titleId) => QuarantinedTitles.Add(titleId);
        }
    }
}
