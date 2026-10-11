using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Chaos.NaCl;
using Newtonsoft.Json;
using Plemu.Content;
using Plemu.Core;
using Xunit;

namespace Plemu.Solana.Tests;

public class LaunchGateOfflineTests
{
    private static readonly DateTimeOffset TestNow =
        new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly byte[] _privKey;
    private readonly byte[] _pubKey;
    private readonly KeyRegistry _keys;
    private const string KeyId = "test-key-a";

    public LaunchGateOfflineTests()
    {
        byte[] seed = new byte[32];
        for (int i = 0; i < 32; i++) seed[i] = (byte)(i + 1);
        Ed25519.KeyPairFromSeed(out _pubKey, out _privKey, seed);

        _keys = new KeyRegistry();
        _keys.Keys.Add(new KeyEntry
        {
            KeyId = KeyId,
            PublicKey = Convert.ToHexString(_pubKey).ToLowerInvariant(),
            Status = "active",
            ValidFrom = "2026-01-01T00:00:00Z"
        });
    }

    [Fact]
    public void Authorize_PaidTitle_BuyOnce_NoEntitlementsStore_DeniesEntitlementMissing()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-buy-once", "buy_once", "pda_license");

        // Act - Using 4-argument overload (null entitlements)
        LaunchDecision decision = LaunchGate.Authorize("game-buy-once", manifestSource, contentStore, new TestClock(TestNow));

        // Assert
        Assert.False(decision.IsAuthorized);
        Assert.NotNull(decision.Denial);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_PaidTitle_BuyOnce_NoTicketInStore_DeniesEntitlementMissing()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-buy-once", "buy_once", "pda_license");
        var entitlementStore = new TestEntitlementStore(); // empty

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-buy-once", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.False(decision.IsAuthorized);
        Assert.NotNull(decision.Denial);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_PaidTitle_Pwyw_NoTicketInStore_DeniesEntitlementMissing()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-pwyw", "pwyw", "pda_license");
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-pwyw", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.False(decision.IsAuthorized);
        Assert.NotNull(decision.Denial);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_PaidTitle_DemoFull_PaidVersion_NoTicket_DeniesEntitlementMissing()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-full", "demo_full", "pda_license");
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-full", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.False(decision.IsAuthorized);
        Assert.NotNull(decision.Denial);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_PaidTitle_DemoFull_FreeDemoVersion_NoTicket_Authorizes()
    {
        // Arrange: delivery is "none" for demo version
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-demo", "demo_full", "none");
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-demo", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public void Authorize_PaidTitle_BuyOnce_WithValidTicket_Authorizes()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-buy-once", "buy_once", "pda_license");
        var entitlementStore = new TestEntitlementStore();
        entitlementStore.AddTicket(sha256, expiresAt: TestNow.AddDays(30));

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-buy-once", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
        Assert.Equal("game-buy-once", decision.Ticket.TitleId);
    }

    [Fact]
    public void Authorize_PaidTitle_WithinOfflineGracePeriod_Authorizes()
    {
        // Arrange: ticket expired 3 days ago; within 7-day GracePeriod
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-grace", "buy_once", "pda_license");
        var entitlementStore = new TestEntitlementStore();
        DateTimeOffset expiredThreeDaysAgo = TestNow.AddDays(-3);
        entitlementStore.AddTicket(sha256, expiresAt: expiredThreeDaysAgo);

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-grace", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public void Authorize_PaidTitle_BeyondOfflineGracePeriod_DeniesEntitlementMissing()
    {
        // Arrange: ticket expired 8 days ago; beyond 7-day GracePeriod
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-expired", "buy_once", "pda_license");
        var entitlementStore = new TestEntitlementStore();
        DateTimeOffset expiredEightDaysAgo = TestNow.AddDays(-8);
        entitlementStore.AddTicket(sha256, expiresAt: expiredEightDaysAgo);

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-expired", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.False(decision.IsAuthorized);
        Assert.NotNull(decision.Denial);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_FreeTitle_FreeModel_WithoutTicket_Authorizes()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-free", "free", "none");
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-free", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public void Authorize_FreeTitle_TipOnlyModel_WithoutTicket_Authorizes()
    {
        // Arrange
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-tip", "tip_only", "none");
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-tip", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public void Authorize_FreeTitle_NoMonetizationBlock_WithoutTicket_Authorizes()
    {
        // Arrange: legacy title without monetization property
        var (manifestSource, contentStore, sha256) = CreateManifestAndStore("game-legacy", null, null);
        var entitlementStore = new TestEntitlementStore();

        // Act
        LaunchDecision decision = LaunchGate.Authorize("game-legacy", manifestSource, contentStore, entitlementStore, new TestClock(TestNow));

        // Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public void Authorize_ManifestNull_DeniesManifestUnavailable()
    {
        var manifestSource = new NullManifestSource();
        var contentStore = new TestContentStore();

        LaunchDecision decision = LaunchGate.Authorize("any", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.ManifestUnavailable, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_ManifestUnverified_DeniesManifestUnverified()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-bad-sig", null, null, tamperSig: true);

        LaunchDecision decision = LaunchGate.Authorize("game-bad-sig", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.ManifestUnverified, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_ManifestExpired_DeniesManifestExpired()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-exp", null, null, expiresAt: "2020-01-01T00:00:00Z");

        LaunchDecision decision = LaunchGate.Authorize("game-exp", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.ManifestExpired, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_TitleNotFound_DeniesTitleNotFound()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-exists", null, null);

        LaunchDecision decision = LaunchGate.Authorize("game-does-not-exist", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.TitleNotFound, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_EmptyTitleId_DeniesTitleNotFound()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-exists", null, null);

        LaunchDecision decision = LaunchGate.Authorize("", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.TitleNotFound, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_Revoked_DeniesRevoked()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-revoked", null, null, isRevoked: true);

        LaunchDecision decision = LaunchGate.Authorize("game-revoked", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.Revoked, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_ContentMissing_DeniesContentMissing()
    {
        var (manifestSource, _, _) = CreateManifestAndStore("game-missing", null, null);
        var emptyStore = new TestContentStore(); // no artifact set

        LaunchDecision decision = LaunchGate.Authorize("game-missing", manifestSource, emptyStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.ContentMissing, decision.Denial.Reason);
    }

    [Fact]
    public void Authorize_HashMismatch_DeniesHashMismatchAndQuarantines()
    {
        var (manifestSource, contentStore, _) = CreateManifestAndStore("game-tampered", null, null);
        contentStore.Set("game-tampered", Encoding.UTF8.GetBytes("completely-different-tampered-content"));

        LaunchDecision decision = LaunchGate.Authorize("game-tampered", manifestSource, contentStore, new TestClock(TestNow));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.HashMismatch, decision.Denial.Reason);
        Assert.Contains("game-tampered", contentStore.QuarantinedTitles);
    }

    [Fact]
    public void Authorize_FileStreamContent_ResolvesContentPath()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes("file-stream-rom-content");
            File.WriteAllBytes(tempFile, bytes);

            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var manifestSource = CreateManifestWithExactSha256("game-fs", sha256, bytes.Length);

            var fileStore = new FileSystemContentStore(tempFile);
            LaunchDecision decision = LaunchGate.Authorize("game-fs", manifestSource, fileStore, new TestClock(TestNow));

            Assert.True(decision.IsAuthorized);
            Assert.Equal(tempFile, decision.Ticket.ContentPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private (IManifestSource source, TestContentStore store, string sha256) CreateManifestAndStore(
        string titleId,
        string? model,
        string? delivery,
        bool isRevoked = false,
        string expiresAt = "2030-01-01T00:00:00Z",
        bool tamperSig = false)
    {
        byte[] artifactBytes = Encoding.UTF8.GetBytes($"binary-rom-content-for-{titleId}");
        string sha256 = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();

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
            IsRevoked = isRevoked,
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
            ExpiresAt = expiresAt,
            Sequence = 1,
            SignerKeyId = KeyId,
            Entries = new List<ManifestEntry> { null!, entry, new ManifestEntry { Id = "other" } }
        };

        string manifestJson = JsonConvert.SerializeObject(manifest);
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        byte[] signature = Ed25519.Sign(manifestBytes, _privKey);
        if (tamperSig)
        {
            signature[0] ^= 0xFF;
        }

        var envelope = new SignatureEnvelope
        {
            KeyId = KeyId,
            Alg = "ed25519",
            Signature = Convert.ToBase64String(signature),
            SignedAt = "2026-07-12T00:00:00Z"
        };
        byte[] sigBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));

        var bundle = new ManifestBundle(manifestBytes, sigBytes, _keys);
        var manifestSource = new TestManifestSource(bundle);

        var store = new TestContentStore();
        store.Set(titleId, artifactBytes);

        return (manifestSource, store, sha256);
    }

    private IManifestSource CreateManifestWithExactSha256(string titleId, string sha256, int sizeBytes)
    {
        var entry = new ManifestEntry
        {
            Id = titleId,
            Title = $"Title {titleId}",
            System = "gb",
            CoreId = "sameboy",
            MinCoreVersion = "0.1.0",
            Sha256 = sha256,
            SizeBytes = sizeBytes,
            DownloadUrls = new List<string> { $"https://cdn.plemu.xyz/artifacts/{sha256}" },
            Creator = new Creator { Wallet = "11111111111111111111111111111111", DisplayName = "Author" },
            License = new LicenseInfo { Type = "Commercial", EvidenceUrl = "https://plemu.xyz" },
            Attestation = new Attestation { Role = "creator", Message = "Attestation", Signature = "sig", SignedAt = "2026-07-12T00:00:00Z" },
            Artwork = new Artwork { IconUrl = "https://plemu.xyz/icon.png" },
            AddedAt = "2026-07-12T00:00:00Z",
            IsRevoked = false,
            Tags = new List<string> { "game" }
        };

        var manifest = new Manifest
        {
            SchemaVersion = 1,
            GeneratedAt = "2026-07-12T00:00:00Z",
            ExpiresAt = "2030-01-01T00:00:00Z",
            Sequence = 1,
            SignerKeyId = KeyId,
            Entries = new List<ManifestEntry> { entry }
        };

        string manifestJson = JsonConvert.SerializeObject(manifest);
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        byte[] signature = Ed25519.Sign(manifestBytes, _privKey);

        var envelope = new SignatureEnvelope
        {
            KeyId = KeyId,
            Alg = "ed25519",
            Signature = Convert.ToBase64String(signature),
            SignedAt = "2026-07-12T00:00:00Z"
        };
        byte[] sigBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));

        return new TestManifestSource(new ManifestBundle(manifestBytes, sigBytes, _keys));
    }

    private sealed class NullManifestSource : IManifestSource
    {
        public bool IsBundled => false;
        public ManifestBundle GetVerifiedManifest() => null!;
    }

    private sealed class FileSystemContentStore : IContentStore
    {
        private readonly string _path;
        public FileSystemContentStore(string path) => _path = path;
        public Stream OpenRead(string titleId) => new FileStream(_path, FileMode.Open, FileAccess.Read);
        public void Quarantine(string titleId) { }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class TestManifestSource : IManifestSource
    {
        private readonly ManifestBundle _bundle;
        public bool IsBundled => false;
        public TestManifestSource(ManifestBundle bundle) => _bundle = bundle;
        public ManifestBundle GetVerifiedManifest() => _bundle;
    }

    private sealed class TestContentStore : IContentStore
    {
        private readonly Dictionary<string, byte[]> _content = new(StringComparer.Ordinal);
        public List<string> QuarantinedTitles { get; } = new();

        public void Set(string titleId, byte[] bytes) => _content[titleId] = bytes;

#pragma warning disable CS8766
        public Stream? OpenRead(string titleId)
#pragma warning restore CS8766
        {
            return _content.TryGetValue(titleId, out byte[]? bytes)
                ? new MemoryStream(bytes)
                : null;
        }

        public void Quarantine(string titleId) => QuarantinedTitles.Add(titleId);
    }

    private sealed class TestEntitlementStore : IEntitlementStore
    {
        private readonly Dictionary<string, DateTimeOffset> _tickets = new(StringComparer.OrdinalIgnoreCase);

        public void AddTicket(string artifactSha256, DateTimeOffset expiresAt)
        {
            _tickets[artifactSha256] = expiresAt;
        }

        public bool HasValidTicket(string artifactSha256, DateTimeOffset currentTime)
        {
            if (!_tickets.TryGetValue(artifactSha256, out DateTimeOffset expiresAt))
            {
                return false;
            }

            // FreshnessPolicy.GracePeriod is 7 days
            return currentTime <= expiresAt + FreshnessPolicy.GracePeriod;
        }
    }
}
