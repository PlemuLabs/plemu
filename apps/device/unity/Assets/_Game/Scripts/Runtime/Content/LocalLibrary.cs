using System;
using System.Collections.Generic;
using System.IO;
using Plemu.App;

namespace Plemu.Content
{
    public enum LocalImportError
    {
        FileNotFound,
        TooLarge,
        ArchiveNotPermitted,
        InvalidExtension,
        IoFailure
    }

    public sealed class LocalImportException : Exception
    {
        public LocalImportError Error { get; }

        public LocalImportException(LocalImportError error, string message) : base(message)
        {
            Error = error;
        }
    }

    public sealed class LocalEntry
    {
        public string Id { get; }
        public string Title { get; }
        public string System { get; }
        public string FilePath { get; }
        public long FileSize { get; }

        public LocalEntry(string id, string title, string system, string filePath, long fileSize)
        {
            Id = id;
            Title = title;
            System = system;
            FilePath = filePath;
            FileSize = fileSize;
        }
    }

    /// <summary>
    /// Manages locally-imported ROMs in the isolated local lane.
    /// Strictly guards against hostile files (size cap + archive bombs) and ensures no local
    /// entry crosses into the signed/attested domain.
    /// </summary>
    public sealed class LocalLibrary
    {
        public const long MaxFileSizeBytes = 64 * 1024 * 1024; // 64 MiB limit

        private static readonly HashSet<string> BannedArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso"
        };

        private readonly string _root;
        private readonly LocalContentStore _store;
        private readonly List<LocalEntry> _entries = new List<LocalEntry>();

        public string Root => _root;
        public LocalContentStore Store => _store;
        public IReadOnlyList<LocalEntry> Entries => _entries;

        public LocalLibrary(string rootDirectory)
        {
            _root = rootDirectory;
            _store = new LocalContentStore(rootDirectory);
            if (!Directory.Exists(_root))
                Directory.CreateDirectory(_root);
        }

        /// <summary>
        /// Imports an unverified local ROM file into the isolated local library.
        /// Throws <see cref="LocalImportException"/> if the file exceeds size limits or is an archive.
        /// </summary>
        public LocalEntry Import(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                throw new LocalImportException(LocalImportError.FileNotFound, $"File not found: {sourceFilePath}");

            string ext = Path.GetExtension(sourceFilePath);
            if (BannedArchiveExtensions.Contains(ext))
            {
                throw new LocalImportException(
                    LocalImportError.ArchiveNotPermitted,
                    $"Compressed archives ({ext}) are forbidden in the local lane to prevent decompression bombs.");
            }

            var info = new FileInfo(sourceFilePath);
            if (info.Length > MaxFileSizeBytes)
            {
                throw new LocalImportException(
                    LocalImportError.TooLarge,
                    $"File exceeds maximum permitted local lane size cap of {MaxFileSizeBytes} bytes (got {info.Length} bytes).");
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destPath = Path.Combine(_root, fileName);
            if (!string.Equals(sourceFilePath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFilePath, destPath, overwrite: true);
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string system = DetectSystemFromExtension(ext);
            string id = $"local/{fileName}";

            var entry = new LocalEntry(id, baseName, system, destPath, info.Length);
            _entries.Add(entry);
            return entry;
        }

        public LocalEntry Find(string id)
        {
            return _entries.Find(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public CatalogEntry ToCatalogEntry(LocalEntry local)
        {
            if (local == null) return null;

            return new CatalogEntry(
                id: local.Id,
                title: local.Title,
                system: local.System,
                license: "[LOCAL — UNVERIFIED]",
                lane: ContentLane.Local,
                canTip: false,
                canMint: false,
                canPromote: false,
                creatorWallet: null);
        }

        private static string DetectSystemFromExtension(string ext)
        {
            return (ext?.ToLowerInvariant()) switch
            {
                ".gb" => "gb",
                ".gbc" => "gbc",
                ".gba" => "gba",
                ".sfc" or ".smc" => "snes",
                ".md" or ".bin" or ".gen" => "megadrive",
                ".wasm" => "wasm4",
                _ => "generic"
            };
        }
    }
}
