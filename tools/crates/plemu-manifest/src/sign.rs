//! Manifest signing. SPEC §5 (`plemu-manifest sign`).
//!
//! Signs the **raw bytes** of the manifest file as served (no canonicalization,
//! SPEC §1.1) and emits a detached [`SigEnvelope`]. When a key registry is
//! supplied, self-verifies the produced signature against the registered public
//! key for `key_id` before returning (SPEC §5 comment).

use ed25519_dalek::{Signer, SigningKey};
use plemu_schemas::{KeyRegistry, SigEnvelope};

use crate::crypto;
use crate::error::{ManifestError, Result};

/// Only algorithm produced at v1.
const ALG_ED25519: &str = "ed25519";

/// Sign raw manifest bytes, producing a detached envelope.
///
/// `signed_at` is the ISO 8601 timestamp to stamp into the envelope (the CLI
/// supplies `--now` or the system clock). If `registry` is `Some`, the freshly
/// produced signature is self-verified against that registry's public key for
/// `key_id`; a mismatch (or missing/unusable key) is a hard error so a
/// misconfigured signer never ships an unverifiable manifest.
pub fn sign_manifest(
    signing_key: &SigningKey,
    manifest_bytes: &[u8],
    key_id: &str,
    signed_at: &str,
    registry: Option<&KeyRegistry>,
) -> Result<SigEnvelope> {
    let signature = signing_key.sign(manifest_bytes);
    let signature_b64 = crypto::signature_to_base64(&signature);

    if let Some(registry) = registry {
        let key_entry = registry
            .find(key_id)
            .ok_or_else(|| ManifestError::KeyNotFound(key_id.to_string()))?;

        let expected_pub = crypto::verifying_key_from_hex(&key_entry.public_key)?;
        let produced_pub = signing_key.verifying_key();
        if produced_pub != expected_pub {
            return Err(ManifestError::Validation(format!(
                "signing key public part does not match registry entry for {key_id}: \
                 registry has {}, signer has {}",
                key_entry.public_key,
                crypto::verifying_key_to_hex(&produced_pub),
            )));
        }

        // Belt-and-suspenders: verify the actual signature end-to-end.
        crypto::verify_detached(&expected_pub, manifest_bytes, &signature)?;
    }

    Ok(SigEnvelope {
        key_id: key_id.to_string(),
        alg: ALG_ED25519.to_string(),
        signature: signature_b64,
        signed_at: signed_at.to_string(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use ed25519_dalek::SigningKey;

    #[test]
    fn sign_then_verify_roundtrip() {
        // Deterministic key from a fixed seed (test-only).
        let signing_key = SigningKey::from_bytes(&[7u8; 32]);
        let msg = b"hello manifest bytes";
        let envelope = sign_manifest(&signing_key, msg, "test-key", "2026-07-12T00:00:00Z", None)
            .expect("sign");

        assert_eq!(envelope.alg, "ed25519");
        assert_eq!(envelope.key_id, "test-key");

        let vk = signing_key.verifying_key();
        let sig = crypto::signature_from_base64(&envelope.signature).expect("decode");
        crypto::verify_detached(&vk, msg, &sig).expect("roundtrip verify");
    }
}
