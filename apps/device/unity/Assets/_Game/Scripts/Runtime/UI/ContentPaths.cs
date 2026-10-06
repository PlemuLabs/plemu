using System.Collections.Generic;
using System.IO;
using Plemu.Content;
using Plemu.Core;
using UnityEngine;

namespace Plemu.App
{
    /// <summary>
    /// Resolves the editor content locations and wires the real <see cref="FileManifestSource"/> /
    /// <see cref="FileContentStore"/> that <see cref="LaunchGate"/> consumes, plus reads the verified
    /// manifest into a display <see cref="CatalogEntry"/> list for the UI. Single place that knows
    /// where staged content lives so the runtime types stay engine-free.
    /// </summary>
    public static class ContentPaths
    {
        /// <summary>The StreamingAssets subfolder that holds the staged manifest + artifacts.</summary>
        public const string ContentSubdir = "Content";

        private static string _overrideContentDirectory;

        /// <summary>Sets a custom content directory root (e.g. staged persistentDataPath on Android).</summary>
        public static void SetContentDirectory(string customDirectory)
        {
            _overrideContentDirectory = customDirectory;
        }

        /// <summary>Absolute path to the staged content directory (StreamingAssets/Content or override).</summary>
        public static string ContentDirectory =>
            _overrideContentDirectory ?? Path.Combine(Application.streamingAssetsPath, ContentSubdir);

        /// <summary>A file-backed manifest source over the staged beta manifest.</summary>
        public static FileManifestSource ManifestSource() => new FileManifestSource(ContentDirectory);

        /// <summary>A file-backed content store over the staged content-addressed artifacts.</summary>
        public static FileContentStore ContentStore() => new FileContentStore(ContentDirectory);

        /// <summary>
        /// Reads and verifies the staged manifest, returning the playable titles as display entries.
        /// Returns an empty list if the manifest is missing or fails verification (fail closed).
        /// </summary>
        public static IReadOnlyList<CatalogEntry> LoadCatalog(IClock clock)
        {
            var result = new List<CatalogEntry>();
            ManifestBundle bundle = ManifestSource().GetVerifiedManifest();
            if (bundle == null)
                return result;

            VerificationResult verification = ManifestVerifier.Verify(
                bundle.ManifestBytes, bundle.SignatureBytes, bundle.Keys, clock.UtcNow);
            if (!verification.IsValid || verification.Manifest?.Entries == null)
                return result;

            foreach (ManifestEntry entry in verification.Manifest.Entries)
            {
                if (entry == null || entry.IsRevoked)
                    continue;
                string license = entry.License?.Type ?? "unknown";
                result.Add(new CatalogEntry(entry.Id, entry.Title, entry.System, license));
            }

            return result;
        }
    }
}
