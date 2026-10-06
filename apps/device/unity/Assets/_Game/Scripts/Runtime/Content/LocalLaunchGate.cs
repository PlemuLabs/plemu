using System.IO;
using Plemu.Core;

namespace Plemu.Content
{
    /// <summary>
    /// The isolated launch authorization gate for unverified local-lane ROMs.
    /// Ensures that local files can only be launched through this distinct local gate,
    /// and never receive signed trust tickets or platform eligibility (tipping/minting).
    /// </summary>
    public static class LocalLaunchGate
    {
        public static LaunchDecision AuthorizeLocal(
            string localId,
            LocalLibrary library,
            IClock clock)
        {
            if (string.IsNullOrEmpty(localId))
            {
                return LaunchDecision.Denied(DenialReason.TitleNotFound, "Invalid local title id.");
            }

            if (library == null)
            {
                return LaunchDecision.Denied(DenialReason.ContentMissing, "Local library is unavailable.");
            }

            LocalEntry entry = library.Find(localId);
            if (entry == null)
            {
                return LaunchDecision.Denied(DenialReason.TitleNotFound, $"Local title not found: {localId}.");
            }

            if (!File.Exists(entry.FilePath))
            {
                return LaunchDecision.Denied(DenialReason.ContentMissing, $"Local file missing on disk: {entry.FilePath}.");
            }

            // Calculate sha256 for integrity and session identification
            string sha256;
            using (var stream = File.OpenRead(entry.FilePath))
            {
                sha256 = Sha256Verifier.ComputeHashHex(stream);
            }

            string coreId = ResolveCoreId(entry.System);

            var ticket = new LaunchTicket(
                titleId: entry.Id,
                system: entry.System,
                coreId: coreId,
                sha256: sha256,
                contentPath: entry.FilePath);

            return LaunchDecision.Authorized(ticket);
        }

        private static string ResolveCoreId(string system)
        {
            return (system?.ToLowerInvariant()) switch
            {
                "gb" or "gbc" => "sameboy",
                "snes" => "snes9x_stub",
                "megadrive" => "genesis_plus_gx_stub",
                "wasm4" => "wasm4",
                _ => "sameboy"
            };
        }
    }
}
