//! `plemu-schemas` — shared serde types, enums, and allowlist logic for the
//! Plemu content-trust manifest system.
//!
//! This crate is the single source of truth for the on-wire shapes described in
//! SPEC v1. It is intentionally dependency-light (serde + schemars) and does no
//! I/O or crypto — those live in `plemu-manifest`.
//!
//! Forward-compatibility rule (SPEC §1.3): manifest verifiers MUST ignore
//! unknown fields, so no struct here uses `#[serde(deny_unknown_fields)]`.

pub mod attestation;
pub mod cores;
pub mod enums;
pub mod manifest;
pub mod registry;
pub mod registry_entry;

pub use attestation::{build_attestation_message, AttestationParams};
pub use cores::{
    is_core_legal_for_system, is_forbidden_core_id, legal_core_ids, FORBIDDEN_CORE_IDS,
};
pub use enums::{AttestationRole, CoreId, KeyStatus, LicenseType, RevokedReason, System};
pub use manifest::{Artwork, Attestation, Creator, Entry, License, Manifest};
pub use registry::{KeyEntry, KeyRegistry, SigEnvelope};
pub use registry_entry::{
    RegistryArtifact, RegistryAttestation, RegistryCreator, RegistryLicense, RegistryMetadata,
    RegistrySubmission,
};

/// The only `schemaVersion` this crate implements. SPEC v1.
pub const SCHEMA_VERSION: u64 = 1;

/// Slug format for entry ids (the part after `<system>/`). SPEC §1.3.
pub const SLUG_PATTERN: &str = "^[a-z0-9]+(-[a-z0-9]+)*$";

/// Pretty-print JSON schemas for the manifest and a single entry.
///
/// Returns `(manifest_schema, entry_schema)` as newline-terminated pretty JSON
/// strings. Kept in the library (not just a bin) so a test can assert that the
/// committed schema files are byte-identical to freshly generated output.
pub mod schema {
    use super::{Entry, Manifest};

    /// Generate the pretty-printed manifest JSON schema, newline-terminated.
    pub fn manifest_schema_json() -> Result<String, serde_json::Error> {
        let schema = schemars::schema_for!(Manifest);
        let mut s = serde_json::to_string_pretty(&schema)?;
        s.push('\n');
        Ok(s)
    }

    /// Generate the pretty-printed single-entry JSON schema, newline-terminated.
    pub fn entry_schema_json() -> Result<String, serde_json::Error> {
        let schema = schemars::schema_for!(Entry);
        let mut s = serde_json::to_string_pretty(&schema)?;
        s.push('\n');
        Ok(s)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_sample_manifest_fixture() {
        // Repo-root-relative path from this crate dir.
        let path = concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../../testdata/sample-manifest/manifest.json"
        );
        let bytes = std::fs::read(path).expect("read sample manifest");
        let manifest: Manifest = serde_json::from_slice(&bytes).expect("parse sample manifest");

        assert_eq!(manifest.schema_version, SCHEMA_VERSION);
        assert_eq!(manifest.sequence, 1);
        assert_eq!(manifest.signer_key_id, "plemu-manifest-dev-a");
        assert_eq!(manifest.entries.len(), 2);

        let first = &manifest.entries[0];
        assert_eq!(first.id, "gb/starforge-demo");
        assert_eq!(first.system, System::Gb);
        assert_eq!(first.core_id, CoreId::Sameboy);
        assert_eq!(first.size_bytes, 37);
        assert!(!first.revoked);

        let second = &manifest.entries[1];
        assert!(second.revoked);
        assert_eq!(second.revoked_reason, Some(RevokedReason::CreatorRequest));
    }

    #[test]
    fn parses_key_registry_fixture() {
        let path = concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../../keys/manifest-signers.json"
        );
        let bytes = std::fs::read(path).expect("read key registry");
        let registry: KeyRegistry = serde_json::from_slice(&bytes).expect("parse key registry");

        let dev_a = registry
            .find("plemu-manifest-dev-a")
            .expect("dev-a present");
        assert_eq!(dev_a.status, KeyStatus::Active);
        assert_eq!(
            dev_a.public_key,
            "1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c"
        );

        let dev_b = registry
            .find("plemu-manifest-dev-b")
            .expect("dev-b present");
        assert_eq!(dev_b.status, KeyStatus::Next);
        assert!(dev_b.valid_from.is_none());
    }

    #[test]
    fn unknown_fields_are_ignored() {
        // Forward-compat: an entry with an extra field must still parse.
        let json = r#"{
            "schemaVersion": 1,
            "generatedAt": "2026-07-12T00:00:00Z",
            "expiresAt": "2030-01-01T00:00:00Z",
            "sequence": 1,
            "signerKeyId": "k",
            "futureTopLevelField": {"anything": true},
            "entries": []
        }"#;
        let manifest: Manifest = serde_json::from_str(json).expect("ignore unknown fields");
        assert_eq!(manifest.entries.len(), 0);
    }

    #[test]
    fn optional_revoked_defaults_false() {
        // An entry omitting `revoked`/`revokedReason`/`revokedAt` parses with
        // revoked=false.
        let json = r#"{
            "id": "gb/x",
            "title": "X",
            "system": "gb",
            "coreId": "sameboy",
            "minCoreVersion": "0.1.0",
            "sha256": "00",
            "sizeBytes": 1,
            "downloadUrls": [],
            "creator": {"wallet": "w", "displayName": "d"},
            "license": {"type": "CC0", "evidenceUrl": "u"},
            "attestation": {"role": "creator", "message": "m", "signature": "s", "signedAt": "t"},
            "artwork": {"screenshotUrls": []},
            "addedAt": "2026-07-12T00:00:00Z",
            "tags": []
        }"#;
        let entry: Entry = serde_json::from_str(json).expect("parse entry with defaults");
        assert!(!entry.revoked);
        assert!(entry.revoked_reason.is_none());
        assert!(entry.creator.play_id.is_none());
    }
}
