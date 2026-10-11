using System.Collections.Generic;
using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// Deserialized view of <c>manifest.json</c> (SPEC §1.2/§1.3). Unknown JSON fields are
    /// ignored for forward compatibility; this type is populated only AFTER the raw bytes
    /// have passed Ed25519 verification (SPEC §2.3).
    /// </summary>
    public sealed class Manifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("expiresAt")]
        public string ExpiresAt { get; set; }

        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        [JsonProperty("signerKeyId")]
        public string SignerKeyId { get; set; }

        [JsonProperty("entries")]
        public List<ManifestEntry> Entries { get; set; } = new List<ManifestEntry>();
    }

    /// <summary>A single approved content entry (SPEC §1.3).</summary>
    public sealed class ManifestEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("system")]
        public string System { get; set; }

        [JsonProperty("coreId")]
        public string CoreId { get; set; }

        [JsonProperty("minCoreVersion")]
        public string MinCoreVersion { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("sizeBytes")]
        public long SizeBytes { get; set; }

        [JsonProperty("downloadUrls")]
        public List<string> DownloadUrls { get; set; } = new List<string>();

        [JsonProperty("creator")]
        public Creator Creator { get; set; }

        [JsonProperty("license")]
        public LicenseInfo License { get; set; }

        [JsonProperty("attestation")]
        public Attestation Attestation { get; set; }

        [JsonProperty("artwork")]
        public Artwork Artwork { get; set; }

        [JsonProperty("addedAt")]
        public string AddedAt { get; set; }

        // SPEC §1.3: optional, default false when absent.
        [JsonProperty("revoked", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IsRevoked { get; set; }

        [JsonProperty("revokedReason")]
        public string RevokedReason { get; set; }

        [JsonProperty("revokedAt")]
        public string RevokedAt { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("monetization")]
        public MonetizationInfo Monetization { get; set; }
    }

    /// <summary>Monetization policy block (SPEC §1.3/Monetization Design §3).</summary>
    public sealed class MonetizationInfo
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("delivery")]
        public string Delivery { get; set; }

        public bool RequiresTicket
        {
            get
            {
                if (string.Equals(Model, "buy_once", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Model, "pwyw", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(Model, "demo_full", System.StringComparison.OrdinalIgnoreCase))
                {
                    return !string.Equals(Delivery, "none", System.StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
        }
    }

    /// <summary>Creator identity block (SPEC §1.3).</summary>
    public sealed class Creator
    {
        [JsonProperty("wallet")]
        public string Wallet { get; set; }

        [JsonProperty("playId")]
        public string PlayId { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }

    /// <summary>License block (SPEC §1.3).</summary>
    public sealed class LicenseInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("evidenceUrl")]
        public string EvidenceUrl { get; set; }

        [JsonProperty("evidenceArchiveUrl")]
        public string EvidenceArchiveUrl { get; set; }
    }

    /// <summary>Creator/curator attestation block (SPEC §1.3/§3).</summary>
    public sealed class Attestation
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonProperty("signedAt")]
        public string SignedAt { get; set; }
    }

    /// <summary>Artwork block (SPEC §1.3).</summary>
    public sealed class Artwork
    {
        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("screenshotUrls")]
        public List<string> ScreenshotUrls { get; set; } = new List<string>();
    }
}
