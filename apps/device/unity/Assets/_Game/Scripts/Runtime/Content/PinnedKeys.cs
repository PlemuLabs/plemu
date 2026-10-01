namespace Plemu.Content
{
    /// <summary>
    /// The device's built-in key registry, pinned as constants (SPEC §2.2 and §6). The
    /// registry is injectable at every verification/launch call site; this type supplies
    /// the default trusted keys for both local dev and production environments.
    /// </summary>
    public static class PinnedKeys
    {
        /// <summary>Nightly signer key id for local dev/testing (status <c>active</c>).</summary>
        public const string DevKeyIdA = "plemu-manifest-dev-a";

        /// <summary>Pre-pinned rotation-target key id for dev (status <c>next</c>).</summary>
        public const string DevKeyIdB = "plemu-manifest-dev-b";

        /// <summary>Production nightly signer key id (status <c>active</c>).</summary>
        public const string ProdKeyIdA = "plemu-manifest-prod-a";

        /// <summary>Production pre-pinned rotation-target key id (status <c>next</c>).</summary>
        public const string ProdKeyIdB = "plemu-manifest-prod-b";

        /// <summary>Offline break-glass emergency key id (status <c>emergency</c>).</summary>
        public const string EmergencyKeyId = "plemu-emergency-1";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="DevKeyIdA"/> (SPEC §6).</summary>
        public const string DevPublicKeyA =
            "1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="DevKeyIdB"/> (SPEC §6).</summary>
        public const string DevPublicKeyB =
            "b3a544ba95bc44519de7782b6bd0d712808a602bccd3ef016dbb896960cce1f7";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="ProdKeyIdA"/>.</summary>
        public const string ProdPublicKeyA =
            "b5dfdd5351083c6e8e535aba3b50d7ab928eadc310caf4e7ee649ef0f4f3454a";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="ProdKeyIdB"/>.</summary>
        public const string ProdPublicKeyB =
            "536d8a0f159ed921c4f0e843f06d3e4fff73fdea78224f3f685ce953277a460c";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="EmergencyKeyId"/> (SPEC §2.2).</summary>
        public const string EmergencyPublicKey =
            "e7cd7046da18591d08cb72ca5d582d3cbaf3966255a5cab3347aaacb29858685";

        /// <summary>
        /// A fresh default registry containing the pinned active, next, and emergency keys. A new
        /// instance is returned each call so callers cannot mutate shared trust state.
        /// </summary>
        public static KeyRegistry BuiltIn => new KeyRegistry
        {
            Keys =
            {
                new KeyEntry
                {
                    KeyId = DevKeyIdA,
                    PublicKey = DevPublicKeyA,
                    Status = "active",
                    ValidFrom = "2026-07-12T00:00:00Z",
                },
                new KeyEntry
                {
                    KeyId = DevKeyIdB,
                    PublicKey = DevPublicKeyB,
                    Status = "next",
                },
                new KeyEntry
                {
                    KeyId = ProdKeyIdA,
                    PublicKey = ProdPublicKeyA,
                    Status = "active",
                    ValidFrom = "2026-09-30T00:00:00Z",
                },
                new KeyEntry
                {
                    KeyId = ProdKeyIdB,
                    PublicKey = ProdPublicKeyB,
                    Status = "next",
                },
                new KeyEntry
                {
                    KeyId = EmergencyKeyId,
                    PublicKey = EmergencyPublicKey,
                    Status = "emergency",
                },
            },
        };
    }
}
