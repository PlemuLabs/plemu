using System.Collections.Generic;
using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// The pinned Ed25519 key registry (SPEC §2.2, <c>keys/manifest-signers.json</c>).
    /// Injectable everywhere; <see cref="PinnedKeys.BuiltIn"/> is the default. Key lookup
    /// is by exact <c>keyId</c>.
    /// </summary>
    public sealed class KeyRegistry
    {
        [JsonProperty("keys")]
        public List<KeyEntry> Keys { get; set; } = new List<KeyEntry>();

        /// <summary>
        /// Returns the key with the given id, or <c>null</c> if none is pinned. A null result
        /// is an <c>UnknownKey</c> rejection at the call site.
        /// </summary>
        public KeyEntry Find(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
                return null;

            foreach (KeyEntry key in Keys)
            {
                if (key != null && key.KeyId == keyId)
                    return key;
            }

            return null;
        }
    }
}
