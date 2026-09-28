using System.Collections.Generic;

namespace Plemu.Emulation
{
    /// <summary>
    /// A per-system record (SPEC §1.4 system → its emulator core, file extensions, and the default
    /// editor keyboard mapping). v0 ships only the <c>gb</c> profile (SameBoy); more systems are
    /// added as their cores are proven in the shim.
    /// </summary>
    public sealed class SystemProfile
    {
        /// <summary>SPEC §1.4 system enum value (e.g. <c>gb</c>).</summary>
        public string System { get; }

        /// <summary>SPEC §1.5 core id that runs this system (e.g. <c>sameboy</c>).</summary>
        public string CoreId { get; }

        /// <summary>Lowercase file extensions the core accepts, without the dot.</summary>
        public IReadOnlyList<string> Extensions { get; }

        /// <summary>The default editor keyboard → RetroPad mapping for this system.</summary>
        public RetroPad DefaultMapping { get; }

        public SystemProfile(string system, string coreId, IReadOnlyList<string> extensions, RetroPad defaultMapping)
        {
            System = system;
            CoreId = coreId;
            Extensions = extensions;
            DefaultMapping = defaultMapping;
        }

        /// <summary>The known system profiles, keyed by SPEC §1.4 system enum value.</summary>
        public static readonly IReadOnlyDictionary<string, SystemProfile> Known =
            new Dictionary<string, SystemProfile>
            {
                ["gb"] = new SystemProfile(
                    system: "gb",
                    coreId: "sameboy",
                    extensions: new[] { "gb" },
                    defaultMapping: RetroPad.DefaultGameBoyMapping),
                // gbc shares SameBoy; added when a gbc title is onboarded.
            };

        /// <summary>Returns the profile for <paramref name="system"/>, or null if unsupported.</summary>
        public static SystemProfile ForSystem(string system)
        {
            if (system != null && Known.TryGetValue(system, out SystemProfile profile))
                return profile;
            return null;
        }
    }
}
