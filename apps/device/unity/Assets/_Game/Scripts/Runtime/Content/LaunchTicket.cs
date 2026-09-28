namespace Plemu.Content
{
    /// <summary>
    /// The authorization to launch a title, issued by <see cref="LaunchGate"/> only after every
    /// trust gate passed. Carries exactly what the emulator host needs to boot the core.
    /// </summary>
    public sealed class LaunchTicket
    {
        /// <summary>Manifest entry id (<c>&lt;system&gt;/&lt;slug&gt;</c>).</summary>
        public string TitleId { get; }

        /// <summary>System enum value (SPEC §1.4).</summary>
        public string System { get; }

        /// <summary>Core id to load (SPEC §1.5).</summary>
        public string CoreId { get; }

        /// <summary>The verified lowercase-hex SHA-256 of the artifact.</summary>
        public string Sha256 { get; }

        /// <summary>Local path the verified artifact was read from.</summary>
        public string ContentPath { get; }

        public LaunchTicket(string titleId, string system, string coreId, string sha256, string contentPath)
        {
            TitleId = titleId;
            System = system;
            CoreId = coreId;
            Sha256 = sha256;
            ContentPath = contentPath;
        }
    }
}
