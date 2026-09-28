namespace Plemu.Content
{
    /// <summary>
    /// Typed reason a manifest failed verification (SPEC §2.3). Ordered to mirror the
    /// normative verification steps so the first failing gate is the reported one.
    /// </summary>
    public enum VerificationFailure
    {
        /// <summary>Step 2: <c>manifest.sig</c> could not be parsed into a valid envelope.</summary>
        EnvelopeMalformed,

        /// <summary>Step 2: the envelope <c>keyId</c> is not in the pinned registry.</summary>
        UnknownKey,

        /// <summary>Step 2: the key exists but its status is not <c>active|next|emergency</c>.</summary>
        KeyNotAcceptable,

        /// <summary>Step 2: the envelope <c>alg</c> is not <c>ed25519</c>.</summary>
        AlgorithmUnsupported,

        /// <summary>Step 3: Ed25519 verification of the signature over the raw bytes failed.</summary>
        SignatureInvalid,

        /// <summary>Step 4: the (already-authenticated) manifest bytes are not valid JSON.</summary>
        ManifestMalformed,

        /// <summary>Step 4: <c>schemaVersion</c> is not 1.</summary>
        SchemaVersionUnsupported,

        /// <summary>Step 4: manifest <c>signerKeyId</c> does not match the envelope <c>keyId</c>.</summary>
        SignerKeyIdMismatch,

        /// <summary>Step 5: <c>sequence</c> is below the required minimum (anti-rollback).</summary>
        SequenceRollback,
    }
}
