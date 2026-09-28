//! Manifest document types. SPEC §1.2–1.3.
//!
//! Timestamps are kept as `String` (raw ISO 8601 UTC `YYYY-MM-DDTHH:MM:SSZ` as
//! served) rather than parsed types, so that (a) round-tripping never mutates
//! the bytes that were signed and (b) parsing a timestamp is a policy concern of
//! the verifier, not of deserialization.
//!
//! Per SPEC §1.3 no struct uses `#[serde(deny_unknown_fields)]`: verifiers MUST
//! ignore unknown fields for forward compatibility.

use schemars::JsonSchema;
use serde::{Deserialize, Serialize};

use crate::enums::{AttestationRole, CoreId, LicenseType, RevokedReason, System};

/// Top-level signed manifest. SPEC §1.2.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Manifest {
    pub schema_version: u64,
    pub generated_at: String,
    pub expires_at: String,
    pub sequence: u64,
    pub signer_key_id: String,
    pub entries: Vec<Entry>,
}

/// A single approved content entry. SPEC §1.3.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Entry {
    /// `"<system>/<slug>"`, slug `^[a-z0-9]+(-[a-z0-9]+)*$`, immutable.
    pub id: String,
    pub title: String,
    pub system: System,
    pub core_id: CoreId,
    pub min_core_version: String,
    /// 64 lowercase hex chars — digest of the exact playable artifact.
    pub sha256: String,
    pub size_bytes: u64,
    pub download_urls: Vec<String>,
    pub creator: Creator,
    pub license: License,
    pub attestation: Attestation,
    pub artwork: Artwork,
    pub added_at: String,
    /// Optional, default false.
    #[serde(default)]
    pub revoked: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub revoked_reason: Option<RevokedReason>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub revoked_at: Option<String>,
    #[serde(default)]
    pub tags: Vec<String>,
}

/// Creator identity. SPEC §1.3.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Creator {
    /// base58 wallet address.
    pub wallet: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub play_id: Option<String>,
    pub display_name: String,
}

/// License block. SPEC §1.3.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct License {
    #[serde(rename = "type")]
    pub license_type: LicenseType,
    pub evidence_url: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub evidence_archive_url: Option<String>,
}

/// Creator/curator attestation. SPEC §1.3 / §3.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Attestation {
    pub role: AttestationRole,
    /// Exact attestation text (see SPEC §3).
    pub message: String,
    /// base64 ed25519 signature by creator/curator wallet.
    pub signature: String,
    pub signed_at: String,
}

/// Cosmetic artwork references. SPEC §1.3.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Artwork {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub icon_url: Option<String>,
    #[serde(default)]
    pub screenshot_urls: Vec<String>,
}
