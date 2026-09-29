//! Ed25519 primitives: verify a detached signature over raw bytes, parse a
//! hex public key from the registry, and load a PKCS#8 PEM signing key.
//!
//! All decoding is fallible and typed — no `unwrap`/`expect` outside tests.

use base64::Engine as _;
use ed25519_dalek::{Signature, SigningKey, VerifyingKey, PUBLIC_KEY_LENGTH, SIGNATURE_LENGTH};

use crate::error::{ManifestError, Result};

/// Parse a 32-byte Ed25519 public key from 64 lowercase hex chars (SPEC §2.2).
pub fn verifying_key_from_hex(public_key_hex: &str) -> Result<VerifyingKey> {
    let bytes = hex::decode(public_key_hex)?;
    let arr: [u8; PUBLIC_KEY_LENGTH] = bytes.as_slice().try_into().map_err(|_| {
        ManifestError::Validation(format!(
            "public key must be {PUBLIC_KEY_LENGTH} bytes, got {}",
            bytes.len()
        ))
    })?;
    VerifyingKey::from_bytes(&arr)
        .map_err(|e| ManifestError::Validation(format!("invalid ed25519 public key: {e}")))
}

/// Decode a base64 Ed25519 signature (64 bytes) from the envelope (SPEC §2.1).
pub fn signature_from_base64(sig_b64: &str) -> Result<Signature> {
    let bytes = base64::engine::general_purpose::STANDARD.decode(sig_b64)?;
    let arr: [u8; SIGNATURE_LENGTH] = bytes.as_slice().try_into().map_err(|_| {
        ManifestError::Validation(format!(
            "signature must be {SIGNATURE_LENGTH} bytes, got {}",
            bytes.len()
        ))
    })?;
    Ok(Signature::from_bytes(&arr))
}

/// Verify a detached Ed25519 signature over `message` (the raw manifest bytes).
///
/// This is the security-critical step of SPEC §2.3.3. Returns
/// `SignatureInvalid` on any mismatch.
pub fn verify_detached(
    verifying_key: &VerifyingKey,
    message: &[u8],
    signature: &Signature,
) -> Result<()> {
    verifying_key
        .verify_strict(message, signature)
        .map_err(|e| ManifestError::SignatureInvalid(e.to_string()))
}

/// Load an Ed25519 signing key from a PKCS#8 PEM string.
pub fn signing_key_from_pkcs8_pem(pem: &str) -> Result<SigningKey> {
    use ed25519_dalek::pkcs8::DecodePrivateKey;
    SigningKey::from_pkcs8_pem(pem)
        .map_err(|e| ManifestError::Validation(format!("invalid PKCS#8 Ed25519 PEM: {e}")))
}

/// Encode a signature as base64 (STANDARD) for envelope output.
pub fn signature_to_base64(signature: &Signature) -> String {
    base64::engine::general_purpose::STANDARD.encode(signature.to_bytes())
}

/// Encode a verifying key as 64 lowercase hex chars.
pub fn verifying_key_to_hex(key: &VerifyingKey) -> String {
    hex::encode(key.to_bytes())
}

#[cfg(test)]
mod tests {
    use super::*;

    const DEV_A_PUB_HEX: &str = "1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c";
    const DEV_A_SIG_B64: &str =
        "vlsjOsqeyT8/x6un8G/18xN0Uo58J2qvn5s67eBBn71sTwKifhv4qfxpy89Kh19tx8SupGvFcy51w/w5JNLJCA==";

    #[test]
    fn verifies_sample_fixture_over_raw_bytes() {
        let key = verifying_key_from_hex(DEV_A_PUB_HEX).expect("parse dev-a key");
        let sig = signature_from_base64(DEV_A_SIG_B64).expect("decode sig");
        let path = concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../../testdata/sample-manifest/manifest.json"
        );
        let msg = std::fs::read(path).expect("read manifest");
        verify_detached(&key, &msg, &sig).expect("fixture signature must verify");
    }

    #[test]
    fn rejects_tampered_fixture() {
        let key = verifying_key_from_hex(DEV_A_PUB_HEX).expect("parse dev-a key");
        let sig = signature_from_base64(DEV_A_SIG_B64).expect("decode sig");
        let path = concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../../testdata/sample-manifest/manifest.tampered.json"
        );
        let msg = std::fs::read(path).expect("read tampered manifest");
        assert!(verify_detached(&key, &msg, &sig).is_err());
    }

    #[test]
    fn bad_hex_length_rejected() {
        assert!(verifying_key_from_hex("abcd").is_err());
    }

    #[test]
    fn bad_signature_length_rejected() {
        let short = base64::engine::general_purpose::STANDARD.encode([0u8; 10]);
        assert!(signature_from_base64(&short).is_err());
    }
}
