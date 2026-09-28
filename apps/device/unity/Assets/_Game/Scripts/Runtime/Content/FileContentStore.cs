using System.IO;
using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>
    /// An <see cref="IContentStore"/> over content-addressed artifacts on disk: each title's bytes
    /// live at <c>&lt;contentDirectory&gt;/artifacts/&lt;sha256&gt;</c>, where the sha256 is the
    /// title's manifest digest. <see cref="OpenRead"/> maps a titleId to its digest by reading the
    /// staged manifest, then opens the addressed file; <see cref="Quarantine"/> moves a mismatched
    /// artifact into a sibling <c>quarantine/</c> folder so it cannot be launched again.
    /// </summary>
    /// <remarks>
    /// Content-addressing means a tampered artifact simply will not exist at its claimed address —
    /// but <see cref="LaunchGate"/> still re-hashes whatever stream this returns, so a byte flipped
    /// in place is caught as a <see cref="DenialReason.HashMismatch"/>. Engine-free by design; the
    /// caller passes the StreamingAssets content directory.
    /// </remarks>
    public sealed class FileContentStore : IContentStore
    {
        /// <summary>Subfolder holding the content-addressed artifacts.</summary>
        public const string ArtifactsSubdir = "artifacts";

        /// <summary>Subfolder mismatched artifacts are moved into.</summary>
        public const string QuarantineSubdir = "quarantine";

        private readonly string _contentDirectory;
        private readonly string _manifestPath;

        public FileContentStore(string contentDirectory)
            : this(contentDirectory, Path.Combine(contentDirectory, FileManifestSource.DefaultManifestName))
        {
        }

        public FileContentStore(string contentDirectory, string manifestPath)
        {
            _contentDirectory = contentDirectory;
            _manifestPath = manifestPath;
        }

        /// <inheritdoc/>
        public Stream OpenRead(string titleId)
        {
            string sha256 = ResolveSha256(titleId);
            if (string.IsNullOrEmpty(sha256))
                return null;

            string artifactPath = ArtifactPath(sha256);
            if (!File.Exists(artifactPath))
                return null;

            return new FileStream(artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <inheritdoc/>
        public void Quarantine(string titleId)
        {
            string sha256 = ResolveSha256(titleId);
            if (string.IsNullOrEmpty(sha256))
                return;

            string artifactPath = ArtifactPath(sha256);
            if (!File.Exists(artifactPath))
                return;

            string quarantineDir = Path.Combine(_contentDirectory, QuarantineSubdir);
            Directory.CreateDirectory(quarantineDir);
            string dest = Path.Combine(quarantineDir, sha256);
            if (File.Exists(dest))
                File.Delete(dest);
            File.Move(artifactPath, dest);
        }

        /// <summary>Absolute path to the artifact addressed by <paramref name="sha256"/>.</summary>
        public string ArtifactPath(string sha256) =>
            Path.Combine(_contentDirectory, ArtifactsSubdir, sha256);

        /// <summary>
        /// Reads the staged manifest to map <paramref name="titleId"/> to its content digest. The
        /// manifest is used only for addressing here; the gate authenticates it independently.
        /// Returns null if the manifest is missing/unparseable or the title is absent.
        /// </summary>
        private string ResolveSha256(string titleId)
        {
            if (string.IsNullOrEmpty(titleId) || !File.Exists(_manifestPath))
                return null;

            Manifest manifest;
            try
            {
                string json = File.ReadAllText(_manifestPath);
                manifest = JsonConvert.DeserializeObject<Manifest>(json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (manifest?.Entries == null)
                return null;

            foreach (ManifestEntry entry in manifest.Entries)
            {
                if (entry != null && entry.Id == titleId)
                    return entry.Sha256;
            }

            return null;
        }
    }
}
