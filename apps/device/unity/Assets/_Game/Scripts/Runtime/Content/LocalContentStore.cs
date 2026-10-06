using System.IO;

namespace Plemu.Content
{
    /// <summary>
    /// An <see cref="IContentStore"/> dedicated exclusively to locally-imported user files.
    /// Rooted outside every signed-domain path (e.g. <c>persistentDataPath/local/</c>).
    /// </summary>
    public sealed class LocalContentStore : IContentStore
    {
        public const string QuarantineSubdir = "quarantine";

        private readonly string _rootDirectory;

        public string RootDirectory => _rootDirectory;

        public LocalContentStore(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        /// <inheritdoc/>
        public Stream OpenRead(string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
                return null;

            string filePath = ResolveFilePath(titleId);
            if (!File.Exists(filePath))
                return null;

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <inheritdoc/>
        public void Quarantine(string titleId)
        {
            string filePath = ResolveFilePath(titleId);
            if (!File.Exists(filePath))
                return;

            string quarantineDir = Path.Combine(_rootDirectory, QuarantineSubdir);
            Directory.CreateDirectory(quarantineDir);
            string dest = Path.Combine(quarantineDir, Path.GetFileName(filePath));
            if (File.Exists(dest))
                File.Delete(dest);
            File.Move(filePath, dest);
        }

        public string ResolveFilePath(string titleId)
        {
            // Sanitized local path within root
            string safeName = Path.GetFileName(titleId);
            return Path.Combine(_rootDirectory, safeName);
        }
    }
}
