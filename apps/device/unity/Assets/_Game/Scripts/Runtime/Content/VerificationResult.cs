namespace Plemu.Content
{
    /// <summary>
    /// Outcome of <see cref="ManifestVerifier.Verify"/>. On failure, <see cref="Failure"/> is
    /// set and <see cref="Manifest"/> is null for any failure that occurs before JSON parsing
    /// (envelope/key/alg/signature), proving the fail-before-parse guarantee of SPEC §2.3.
    /// </summary>
    public sealed class VerificationResult
    {
        /// <summary>True only when every SPEC §2.3 gate passed.</summary>
        public bool IsValid { get; private set; }

        /// <summary>The first gate that failed, or null when <see cref="IsValid"/> is true.</summary>
        public VerificationFailure? Failure { get; private set; }

        /// <summary>
        /// Freshness state (SPEC §2.3 step 6). Meaningful once the manifest has parsed and
        /// its <c>expiresAt</c> is known; <see cref="Freshness.Expired"/> otherwise.
        /// </summary>
        public Freshness Freshness { get; private set; }

        /// <summary>
        /// The parsed manifest, or null. Null for pre-parse failures; may be non-null for
        /// post-parse failures (schema/signerKeyId/sequence) and is always non-null on success.
        /// </summary>
        public Manifest Manifest { get; private set; }

        private VerificationResult()
        {
        }

        /// <summary>Builds a successful result carrying the parsed manifest and its freshness.</summary>
        public static VerificationResult Valid(Manifest manifest, Freshness freshness)
        {
            return new VerificationResult
            {
                IsValid = true,
                Failure = null,
                Freshness = freshness,
                Manifest = manifest,
            };
        }

        /// <summary>
        /// Builds a failed result. <paramref name="manifest"/> stays null for pre-parse
        /// failures and may be supplied for post-parse ones.
        /// </summary>
        public static VerificationResult Fail(VerificationFailure failure, Manifest manifest = null)
        {
            return new VerificationResult
            {
                IsValid = false,
                Failure = failure,
                Freshness = Freshness.Expired,
                Manifest = manifest,
            };
        }
    }
}
