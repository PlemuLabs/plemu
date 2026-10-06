using System.IO;
using Plemu.Core;

namespace Plemu.Content
{
    /// <summary>
    /// The ONLY path to launching a core. Fail-closed content-trust gate: it re-verifies the
    /// manifest (defense in depth), applies the freshness policy, and re-hashes the artifact at
    /// every launch (SPEC §2.3 step 7). Every failure returns a typed <see cref="LaunchDenial"/>
    /// — it never throws and never returns null.
    /// </summary>
    /// <remarks>
    /// Synchronous by design for v0; an async wrapper arrives with the download manager. The
    /// bundled-manifest exemption for Expired starter-pack titles (SPEC §2.3 step 6) is a later
    /// task — for now Expired always denies.
    /// </remarks>
    public static class LaunchGate
    {
        /// <summary>
        /// Authorizes launching <paramref name="titleId"/> using the manifest from
        /// <paramref name="manifests"/>, the artifact from <paramref name="store"/>, and the
        /// current time from <paramref name="clock"/>.
        /// </summary>
        public static LaunchDecision Authorize(
            string titleId,
            IManifestSource manifests,
            IContentStore store,
            IClock clock)
        {
            ManifestBundle bundle = manifests.GetVerifiedManifest();
            if (bundle == null)
                return LaunchDecision.Denied(DenialReason.ManifestUnavailable, "No manifest available.");

            // Defense in depth: re-verify the last-accepted bytes at launch.
            VerificationResult verification = ManifestVerifier.Verify(
                bundle.ManifestBytes,
                bundle.SignatureBytes,
                bundle.Keys,
                clock.UtcNow);

            if (!verification.IsValid)
            {
                return LaunchDecision.Denied(
                    DenialReason.ManifestUnverified,
                    $"Manifest failed re-verification: {verification.Failure}.");
            }

            // Freshness gate: Fresh and Grace may play; Expired denies unless bundled starter pack (SPEC §2.3 step 6).
            if (verification.Freshness == Freshness.Expired && !manifests.IsBundled)
            {
                return LaunchDecision.Denied(
                    DenialReason.ManifestExpired,
                    "manifest expired beyond grace and this is not the bundled starter pack");
            }

            ManifestEntry entry = FindEntry(verification.Manifest, titleId);
            if (entry == null)
                return LaunchDecision.Denied(DenialReason.TitleNotFound, $"Title not in manifest: {titleId}.");

            if (entry.IsRevoked)
                return LaunchDecision.Denied(DenialReason.Revoked, $"Title is revoked: {titleId}.");

            Stream content = store.OpenRead(titleId);
            if (content == null)
                return LaunchDecision.Denied(DenialReason.ContentMissing, $"Content missing: {titleId}.");

            string contentPath = ResolveContentPath(content, titleId);

            bool matches;
            using (content)
            {
                matches = Sha256Verifier.Matches(content, entry.Sha256);
            }

            if (!matches)
            {
                store.Quarantine(titleId);
                return LaunchDecision.Denied(DenialReason.HashMismatch, $"Hash mismatch: {titleId}.");
            }

            LaunchTicket ticket = new LaunchTicket(
                entry.Id,
                entry.System,
                entry.CoreId,
                entry.Sha256,
                contentPath);

            return LaunchDecision.Authorized(ticket);
        }

        private static ManifestEntry FindEntry(Manifest manifest, string titleId)
        {
            if (manifest == null || string.IsNullOrEmpty(titleId))
                return null;

            foreach (ManifestEntry entry in manifest.Entries)
            {
                if (entry != null && entry.Id == titleId)
                    return entry;
            }

            return null;
        }

        private static string ResolveContentPath(Stream content, string titleId)
        {
            if (content is FileStream fileStream)
                return fileStream.Name;

            return titleId;
        }
    }
}
