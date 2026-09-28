using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// Detached signature envelope (<c>manifest.sig</c>, SPEC §2.1). This is the only part
    /// of the input parsed before Ed25519 verification runs (SPEC §2.3 step 2).
    /// </summary>
    public sealed class SignatureEnvelope
    {
        [JsonProperty("keyId")]
        public string KeyId { get; set; }

        [JsonProperty("alg")]
        public string Alg { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonProperty("signedAt")]
        public string SignedAt { get; set; }
    }
}
