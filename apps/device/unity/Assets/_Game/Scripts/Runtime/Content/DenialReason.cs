namespace Plemu.Content
{
    /// <summary>
    /// Typed reason <see cref="LaunchGate.Authorize"/> refused to launch a title. Every denial
    /// path fails closed with one of these — the gate never throws and never returns null.
    /// </summary>
    public enum DenialReason
    {
        /// <summary>No manifest bundle was available from the source.</summary>
        ManifestUnavailable,

        /// <summary>The manifest re-verification (defense in depth) failed at launch.</summary>
        ManifestUnverified,

        /// <summary>The manifest is beyond its grace window (SPEC §2.3 step 6, Expired).</summary>
        ManifestExpired,

        /// <summary>The requested title id is not present in the manifest entries.</summary>
        TitleNotFound,

        /// <summary>The manifest entry is marked <c>revoked</c>.</summary>
        Revoked,

        /// <summary>The content store has no artifact for the title.</summary>
        ContentMissing,

        /// <summary>The artifact's streaming SHA-256 does not equal the entry digest.</summary>
        HashMismatch,

        /// <summary>The imported local file exceeds the maximum permitted size cap.</summary>
        LocalFileTooLarge,

        /// <summary>Archive files (.zip, .7z, .rar) are rejected in the local lane to avoid decompression attacks.</summary>
        LocalArchiveNotPermitted,
    }
}
