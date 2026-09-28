namespace Plemu.Content
{
    /// <summary>
    /// The raw manifest + detached signature the device last accepted, together with the key
    /// registry to verify them against. <see cref="IManifestSource"/> returns this so
    /// <see cref="LaunchGate"/> can re-verify (defense in depth) before authorizing a launch.
    /// </summary>
    public sealed class ManifestBundle
    {
        /// <summary>Raw <c>manifest.json</c> bytes exactly as stored/served.</summary>
        public byte[] ManifestBytes { get; }

        /// <summary>Raw <c>manifest.sig</c> bytes (the detached envelope).</summary>
        public byte[] SignatureBytes { get; }

        /// <summary>The pinned registry these bytes must verify against.</summary>
        public KeyRegistry Keys { get; }

        public ManifestBundle(byte[] manifestBytes, byte[] signatureBytes, KeyRegistry keys)
        {
            ManifestBytes = manifestBytes;
            SignatureBytes = signatureBytes;
            Keys = keys;
        }
    }
}
