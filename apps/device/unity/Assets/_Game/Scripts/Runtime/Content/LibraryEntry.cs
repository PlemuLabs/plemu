using Newtonsoft.Json;

namespace Plemu.Content
{
    /// <summary>Per-title install-state snapshot persisted by <see cref="LibraryStore"/>.</summary>
    public sealed class LibraryEntry
    {
        /// <summary>SHA-256 (lowercase hex) of the installed artifact.</summary>
        [JsonProperty("installedHash")]
        public string InstalledHash { get; set; }

        /// <summary>Installed content version (e.g. <c>minCoreVersion</c> or a content tag).</summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>Install lifecycle state (e.g. <c>installed</c>, <c>downloading</c>, <c>quarantined</c>).</summary>
        [JsonProperty("installState")]
        public string InstallState { get; set; }

        /// <summary>Last-played timestamp (ISO 8601 UTC), or null if never played.</summary>
        [JsonProperty("lastPlayedUtc")]
        public string LastPlayedUtc { get; set; }
    }
}
