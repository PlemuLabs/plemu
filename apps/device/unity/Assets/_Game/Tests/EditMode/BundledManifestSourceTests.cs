using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Plemu.Content;
using Plemu.Core;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class BundledManifestSourceTests
    {
        private const string StarforgeTitle = "gb/starforge-demo";
        private static readonly DateTimeOffset ExpiredNow =
            new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero);

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
        public void Authorize_Expired_BundledTitle_StillIssuesTicket()
        {
            var bundle = new ManifestBundle(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn);
            var bundledSource = new BundledManifestSource(bundle);
            var store = new MemoryContentStore();
            store.Set(StarforgeTitle, _artifactBytes);

            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, bundledSource, store, new TestClock(ExpiredNow));

            Assert.That(decision.IsAuthorized, Is.True, "Bundled starter-pack titles must remain playable when Expired");
            Assert.That(decision.Ticket.TitleId, Is.EqualTo(StarforgeTitle));
        }

        [Test]
        public void Authorize_Expired_NonBundledTitle_StillDenies()
        {
            var bundle = new ManifestBundle(_manifestBytes, _sigBytes, PinnedKeys.BuiltIn);
            var nonBundledSource = new UnbundledManifestSource(bundle);
            var store = new MemoryContentStore();
            store.Set(StarforgeTitle, _artifactBytes);

            LaunchDecision decision = LaunchGate.Authorize(StarforgeTitle, nonBundledSource, store, new TestClock(ExpiredNow));

            Assert.That(decision.IsAuthorized, Is.False);
            Assert.That(decision.Denial.Reason, Is.EqualTo(DenialReason.ManifestExpired));
        }

        private sealed class UnbundledManifestSource : IManifestSource
        {
            private readonly ManifestBundle _bundle;
            public bool IsBundled => false;

            public UnbundledManifestSource(ManifestBundle bundle) => _bundle = bundle;
            public ManifestBundle GetVerifiedManifest() => _bundle;
        }

        private sealed class TestClock : IClock
        {
            public DateTimeOffset UtcNow { get; }
            public TestClock(DateTimeOffset now) => UtcNow = now;
        }

        private sealed class MemoryContentStore : IContentStore
        {
            private readonly Dictionary<string, byte[]> _content = new Dictionary<string, byte[]>();
            public void Set(string titleId, byte[] bytes) => _content[titleId] = bytes;
            public Stream OpenRead(string titleId) =>
                _content.TryGetValue(titleId, out var b) ? new MemoryStream(b) : null;
            public void Quarantine(string titleId) { }
        }
    }
}
