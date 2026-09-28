using System.IO;

namespace Plemu.Content
{
    /// <summary>
    /// An <see cref="IManifestSource"/> backed by the filesystem: it reads the staged
    /// <c>manifest-beta.json</c> and its detached <c>manifest-beta.json.sig</c> from a content
    /// directory and returns them as a <see cref="ManifestBundle"/> pinned to
    /// <see cref="PinnedKeys.BuiltIn"/>. <see cref="LaunchGate"/> re-verifies the bytes anyway.
    /// </summary>
    /// <remarks>
    /// The content directory is injected (rather than resolved from <c>Application.streamingAssetsPath</c>
    /// here) so this type stays engine-free and unit-testable; callers pass
    /// <c>Path.Combine(Application.streamingAssetsPath, "Content")</c> in the editor.
    /// </remarks>
    public sealed class FileManifestSource : IManifestSource
    {
        /// <summary>Default manifest filename staged by <c>stage-content.sh</c>.</summary>
        public const string DefaultManifestName = "manifest-beta.json";

        /// <summary>Default detached-signature filename staged alongside the manifest.</summary>
        public const string DefaultSignatureName = "manifest-beta.json.sig";

        private readonly string _manifestPath;
        private readonly string _signaturePath;
        private readonly KeyRegistry _keys;

        /// <summary>
        /// Reads <see cref="DefaultManifestName"/> / <see cref="DefaultSignatureName"/> from
        /// <paramref name="contentDirectory"/>, verified against <see cref="PinnedKeys.BuiltIn"/>.
        /// </summary>
        public FileManifestSource(string contentDirectory)
            : this(
                Path.Combine(contentDirectory, DefaultManifestName),
                Path.Combine(contentDirectory, DefaultSignatureName),
                PinnedKeys.BuiltIn)
        {
        }

        /// <summary>Full control over the manifest path, signature path, and pinned key registry.</summary>
        public FileManifestSource(string manifestPath, string signaturePath, KeyRegistry keys)
        {
            _manifestPath = manifestPath;
            _signaturePath = signaturePath;
            _keys = keys;
        }

        /// <inheritdoc/>
        public ManifestBundle GetVerifiedManifest()
        {
            if (!File.Exists(_manifestPath) || !File.Exists(_signaturePath))
                return null;

            byte[] manifestBytes = File.ReadAllBytes(_manifestPath);
            byte[] signatureBytes = File.ReadAllBytes(_signaturePath);
            return new ManifestBundle(manifestBytes, signatureBytes, _keys);
        }
    }
}
