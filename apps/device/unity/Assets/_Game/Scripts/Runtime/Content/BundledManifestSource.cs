using System;

namespace Plemu.Content
{
    /// <summary>
    /// An <see cref="IManifestSource"/> backed by APK-bundled starter-pack assets.
    /// Titles authorized under this source are permitted to launch past the freshness grace period (SPEC §2.3 step 6).
    /// </summary>
    public sealed class BundledManifestSource : IManifestSource
    {
        private readonly Func<ManifestBundle> _bundleProvider;

        /// <inheritdoc/>
        public bool IsBundled => true;

        public BundledManifestSource(ManifestBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            _bundleProvider = () => bundle;
        }

        public BundledManifestSource(IManifestSource innerSource)
        {
            if (innerSource == null) throw new ArgumentNullException(nameof(innerSource));
            _bundleProvider = innerSource.GetVerifiedManifest;
        }

        public BundledManifestSource(byte[] manifestBytes, byte[] signatureBytes, KeyRegistry keys)
            : this(new ManifestBundle(manifestBytes, signatureBytes, keys))
        {
        }

        /// <inheritdoc/>
        public ManifestBundle GetVerifiedManifest() => _bundleProvider();
    }
}
