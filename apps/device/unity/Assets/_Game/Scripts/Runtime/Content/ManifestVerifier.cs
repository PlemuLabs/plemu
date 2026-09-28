using System;
using System.Text;
using Chaos.NaCl;
using Newtonsoft.Json;
using Plemu.Core;

namespace Plemu.Content
{
    /// <summary>
    /// Verifies a signed manifest against the pinned key registry, implementing the normative
    /// order of SPEC §2.3 steps 1–6 EXACTLY. The signature is checked over the raw manifest
    /// bytes BEFORE any manifest JSON is parsed; if verification fails the manifest is never
    /// parsed. Parser exceptions are caught and mapped to typed failures — this API never
    /// throws for control flow.
    /// </summary>
    public static class ManifestVerifier
    {
        private const string ExpectedAlg = "ed25519";
        private const int SchemaVersion = 1;
        private const int SignatureSizeBytes = 64;
        private const int PublicKeySizeBytes = 32;

        /// <summary>
        /// Verifies <paramref name="manifestBytes"/> against <paramref name="sigBytes"/> using
        /// <paramref name="keys"/>. Freshness is evaluated at <paramref name="now"/>. Anti-rollback
        /// rejects when <c>sequence &lt; <paramref name="minSequence"/></c>; the default of -1
        /// disables the check.
        /// </summary>
        public static VerificationResult Verify(
            byte[] manifestBytes,
            byte[] sigBytes,
            KeyRegistry keys,
            DateTimeOffset now,
            long minSequence = -1)
        {
            if (manifestBytes == null || sigBytes == null || keys == null)
                return VerificationResult.Fail(VerificationFailure.EnvelopeMalformed);

            // Step 2: parse the envelope ONLY.
            SignatureEnvelope envelope = TryParseEnvelope(sigBytes);
            if (envelope == null || string.IsNullOrEmpty(envelope.KeyId) ||
                string.IsNullOrEmpty(envelope.Signature))
            {
                return VerificationResult.Fail(VerificationFailure.EnvelopeMalformed);
            }

            // Step 2: key lookup + status gate.
            KeyEntry key = keys.Find(envelope.KeyId);
            if (key == null)
                return VerificationResult.Fail(VerificationFailure.UnknownKey);

            if (!key.IsAcceptable)
                return VerificationResult.Fail(VerificationFailure.KeyNotAcceptable);

            // Step 2: algorithm gate.
            if (envelope.Alg != ExpectedAlg)
                return VerificationResult.Fail(VerificationFailure.AlgorithmUnsupported);

            // Step 3: Ed25519-verify over the RAW manifest bytes. Fail => stop, never parse.
            if (!TryDecodeSignature(envelope.Signature, out byte[] signature) ||
                !HexCodec.TryDecode(key.PublicKey, out byte[] publicKey) ||
                signature.Length != SignatureSizeBytes ||
                publicKey.Length != PublicKeySizeBytes)
            {
                return VerificationResult.Fail(VerificationFailure.SignatureInvalid);
            }

            if (!Ed25519.Verify(signature, manifestBytes, publicKey))
                return VerificationResult.Fail(VerificationFailure.SignatureInvalid);

            // Step 4: only now parse the (authenticated) manifest JSON.
            Manifest manifest = TryParseManifest(manifestBytes);
            if (manifest == null)
                return VerificationResult.Fail(VerificationFailure.ManifestMalformed);

            if (manifest.SchemaVersion != SchemaVersion)
                return VerificationResult.Fail(VerificationFailure.SchemaVersionUnsupported, manifest);

            if (manifest.SignerKeyId != envelope.KeyId)
                return VerificationResult.Fail(VerificationFailure.SignerKeyIdMismatch, manifest);

            // Step 5: anti-rollback.
            if (minSequence >= 0 && manifest.Sequence < minSequence)
                return VerificationResult.Fail(VerificationFailure.SequenceRollback, manifest);

            // Step 6: freshness (reported, not enforced here — enforcement lives in LaunchGate).
            Freshness freshness = EvaluateFreshness(manifest, now);
            return VerificationResult.Valid(manifest, freshness);
        }

        private static Freshness EvaluateFreshness(Manifest manifest, DateTimeOffset now)
        {
            if (!Iso8601.TryParse(manifest.ExpiresAt, out DateTimeOffset expiresAt))
                return Freshness.Expired;

            return FreshnessPolicy.Evaluate(expiresAt, now);
        }

        private static SignatureEnvelope TryParseEnvelope(byte[] sigBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(sigBytes);
                return JsonConvert.DeserializeObject<SignatureEnvelope>(json);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Manifest TryParseManifest(byte[] manifestBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(manifestBytes);
                return JsonConvert.DeserializeObject<Manifest>(json);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool TryDecodeSignature(string base64, out byte[] signature)
        {
            try
            {
                signature = Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
                signature = null;
                return false;
            }
        }
    }
}
