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
