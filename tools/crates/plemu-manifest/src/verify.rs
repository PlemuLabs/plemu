//! Manifest verification. SPEC §2.3 — normative order, fail-closed.
//!
//! The ordering here is security-critical and MUST NOT be reordered:
//!   1. Read raw bytes of manifest + sig.
//!   2. Parse the envelope only; check key status + alg.
//!   3. Ed25519-verify over the raw manifest bytes. **Fail ⇒ stop; never parse.**
//!   4. Only now parse the manifest; check schemaVersion + signerKeyId.
//!   5. Anti-rollback: sequence ≥ min.
//!   6. Freshness relative to `now`.
//!
//! Step 3 gates step 4: we do not deserialize attacker-controlled JSON until
//! its signature has been checked against a pinned key.

use chrono::{DateTime, Duration, Utc};
use plemu_schemas::{KeyRegistry, Manifest, SigEnvelope, SCHEMA_VERSION};

use crate::crypto;
use crate::error::{ManifestError, Result};

/// Grace window after expiry during which installed titles still play but
/// downloads are blocked. SPEC §2.3.6.
const GRACE_DAYS: i64 = 7;

/// Only algorithm accepted at v1. SPEC §2.1 / §2.3.2.
const ALG_ED25519: &str = "ed25519";

/// Freshness state derived from `now` vs `expiresAt`. SPEC §2.3.6.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Freshness {
    /// `now <= expiresAt`: play + download allowed.
    Fresh,
    /// `expiresAt < now <= expiresAt + 7d`: installed titles play; downloads blocked.
    Grace,
    /// Beyond grace: CDN titles blocked; bundled starter-pack titles still play.
    Expired,
}

impl Freshness {
    /// Lowercase label for machine/stdout output.
    pub const fn as_str(self) -> &'static str {
        match self {
            Freshness::Fresh => "Fresh",
            Freshness::Grace => "Grace",
            Freshness::Expired => "Expired",
        }
    }
}

impl std::fmt::Display for Freshness {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// A fully-verified manifest plus its computed freshness.
#[derive(Debug, Clone)]
pub struct Verified {
    pub manifest: Manifest,
    pub freshness: Freshness,
}

/// Parse an ISO 8601 UTC timestamp (`YYYY-MM-DDTHH:MM:SSZ`) into a UTC instant.
pub fn parse_timestamp(s: &str) -> Result<DateTime<Utc>> {
    DateTime::parse_from_rfc3339(s)
        .map(|dt| dt.with_timezone(&Utc))
        .map_err(|e| ManifestError::Validation(format!("invalid timestamp {s:?}: {e}")))
}

/// Compute freshness from `now` and the manifest's `expiresAt`. SPEC §2.3.6.
pub fn freshness(now: DateTime<Utc>, expires_at: DateTime<Utc>) -> Freshness {
    if now <= expires_at {
        return Freshness::Fresh;
    }
    // `checked_add` guards against overflow at the far edge of the DateTime range.
    match expires_at.checked_add_signed(Duration::days(GRACE_DAYS)) {
        Some(grace_end) if now <= grace_end => Freshness::Grace,
        _ => Freshness::Expired,
    }
}

/// Verify raw manifest bytes against a raw envelope and a pinned key registry.
///
/// `now` is the reference instant for the freshness check (the CLI supplies
/// `--now` for determinism; a device uses its clock). `min_sequence`, if given,
/// enforces anti-rollback (SPEC §2.3.5).
///
/// Returns the parsed manifest and its freshness on success. Any failure is a
/// typed [`ManifestError`]; the manifest JSON is never parsed unless its
/// signature verifies (SPEC §2.3.3–4).
pub fn verify_manifest(
    manifest_bytes: &[u8],
    sig_bytes: &[u8],
    registry: &KeyRegistry,
    now: DateTime<Utc>,
    min_sequence: Option<u64>,
) -> Result<Verified> {
    // Step 2: parse the envelope ONLY, resolve key + status + alg.
    let envelope: SigEnvelope = serde_json::from_slice(sig_bytes)?;

    let key_entry = registry
        .find(&envelope.key_id)
        .ok_or_else(|| ManifestError::KeyNotFound(envelope.key_id.clone()))?;

    if !key_entry.status.is_accepted_for_verification() {
        return Err(ManifestError::KeyStatusRejected {
            key_id: envelope.key_id.clone(),
            status: key_entry.status.to_string(),
        });
    }

    if envelope.alg != ALG_ED25519 {
        return Err(ManifestError::AlgNotSupported(envelope.alg.clone()));
    }

    // Step 3: Ed25519-verify over the RAW manifest bytes. Fail ⇒ stop.
    let verifying_key = crypto::verifying_key_from_hex(&key_entry.public_key)?;
    let signature = crypto::signature_from_base64(&envelope.signature)?;
    crypto::verify_detached(&verifying_key, manifest_bytes, &signature)?;

    // Step 4: only now is it safe to parse the manifest.
    let manifest: Manifest = serde_json::from_slice(manifest_bytes)?;

    if manifest.schema_version != SCHEMA_VERSION {
        return Err(ManifestError::SchemaVersionMismatch(
            manifest.schema_version,
        ));
    }

    if manifest.signer_key_id != envelope.key_id {
        return Err(ManifestError::SignerKeyIdMismatch {
            manifest_key: manifest.signer_key_id.clone(),
            sig_key: envelope.key_id.clone(),
        });
    }

    // Step 5: anti-rollback.
    if let Some(min) = min_sequence {
        if manifest.sequence < min {
            return Err(ManifestError::SequenceTooLow {
                got: manifest.sequence,
                min,
            });
        }
    }

    // Step 6: freshness.
    let expires_at = parse_timestamp(&manifest.expires_at)?;
    let freshness = freshness(now, expires_at);

    Ok(Verified {
        manifest,
        freshness,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ts(s: &str) -> DateTime<Utc> {
        parse_timestamp(s).expect("valid ts")
    }

    #[test]
    fn freshness_boundaries() {
        let expires = ts("2030-01-01T00:00:00Z");
        // exactly at expiry is still Fresh (now <= expiresAt)
        assert_eq!(
            freshness(ts("2030-01-01T00:00:00Z"), expires),
            Freshness::Fresh
        );
        // one second before
        assert_eq!(
            freshness(ts("2029-12-31T23:59:59Z"), expires),
            Freshness::Fresh
        );
        // one second into grace
        assert_eq!(
            freshness(ts("2030-01-01T00:00:01Z"), expires),
            Freshness::Grace
        );
        // exactly at grace end (expiry + 7d) still Grace
        assert_eq!(
            freshness(ts("2030-01-08T00:00:00Z"), expires),
            Freshness::Grace
        );
        // one second past grace end
        assert_eq!(
            freshness(ts("2030-01-08T00:00:01Z"), expires),
            Freshness::Expired
        );
    }
}
