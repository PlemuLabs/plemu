namespace Plemu.Content
{
    /// <summary>
    /// The device's built-in key registry, pinned as constants (SPEC §6 dev keys). The
    /// registry is injectable at every verification/launch call site; this type merely
    /// supplies the default. Swapping in the production registry (active/next from KMS)
    /// is a later task and requires only passing a different <see cref="KeyRegistry"/>.
    /// </summary>
    public static class PinnedKeys
    {
        /// <summary>Nightly signer key id (status <c>active</c>).</summary>
        public const string DevKeyIdA = "plemu-manifest-dev-a";

        /// <summary>Pre-pinned rotation-target key id (status <c>next</c>).</summary>
        public const string DevKeyIdB = "plemu-manifest-dev-b";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="DevKeyIdA"/> (SPEC §6).</summary>
        public const string DevPublicKeyA =
            "1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c";

        /// <summary>64-hex (32-byte) Ed25519 public key for <see cref="DevKeyIdB"/> (SPEC §6).</summary>
        public const string DevPublicKeyB =
            "b3a544ba95bc44519de7782b6bd0d712808a602bccd3ef016dbb896960cce1f7";

        /// <summary>
        /// A fresh default registry containing exactly the two pinned dev keys. A new
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
            },
        };
    }
}
