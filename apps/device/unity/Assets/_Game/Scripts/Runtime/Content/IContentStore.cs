using System.IO;

namespace Plemu.Content
{
    /// <summary>
    /// Local storage for downloaded content artifacts. <see cref="LaunchGate"/> reads the bytes
    /// for a title to re-hash them, and quarantines any artifact whose digest no longer matches
    /// the manifest.
    /// </summary>
    public interface IContentStore
    {
        /// <summary>
        /// Opens a readable stream over the artifact for <paramref name="titleId"/>, or returns
        /// <c>null</c> if the content is not present. Caller disposes the stream.
        /// </summary>
        Stream OpenRead(string titleId);

        /// <summary>
        /// Moves the artifact for <paramref name="titleId"/> out of the playable set after a
        /// hash mismatch, so it cannot be launched again until re-downloaded and re-verified.
        /// </summary>
        void Quarantine(string titleId);
    }
}
