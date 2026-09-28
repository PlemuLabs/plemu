using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// One pinned signing key in the registry (SPEC §2.2). <see cref="Status"/> is one of
    /// <c>active</c>, <c>next</c>, <c>emergency</c>, or <c>retired</c>; a verifier accepts
    /// signatures only from the first three (see <see cref="IsAcceptable"/>).
    /// </summary>
    public sealed class KeyEntry
    {
        [JsonProperty("keyId")]
        public string KeyId { get; set; }

        [JsonProperty("publicKey")]
        public string PublicKey { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("validFrom")]
        public string ValidFrom { get; set; }

        /// <summary>
        /// True when this key's status permits accepting new manifests (SPEC §2.2:
        /// <c>active | next | emergency</c>). <c>retired</c> and any unknown status reject.
        /// </summary>
        [JsonIgnore]
        public bool IsAcceptable =>
            Status == "active" || Status == "next" || Status == "emergency";
    }
}
