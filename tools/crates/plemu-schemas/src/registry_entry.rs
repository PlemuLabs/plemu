//! Registry entry on-disk shapes: metadata.json and attestation.json.

use crate::enums::{AttestationRole, CoreId, LicenseType, System};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryMetadata {
    pub id: String,
    pub title: String,
    pub system: System,
    pub core_id: CoreId,
    pub min_core_version: String,
    pub sha256: String,
    pub size_bytes: u64,
    pub creator: RegistryCreator,
    pub license: RegistryLicense,
    pub submission: RegistrySubmission,
    pub artifact: RegistryArtifact,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryCreator {
    pub wallet: String,
    pub display_name: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryLicense {
    #[serde(rename = "type")]
    pub license_type: LicenseType,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub evidence_url: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistrySubmission {
    pub via: String,
    pub submitted_at: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryArtifact {
    pub sha256: String,
    pub size_bytes: u64,
    pub filename: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryAttestation {
    pub role: AttestationRole,
    pub message: String,
    pub signature: String,
    pub signed_at: String,
    pub wallet: String,
}
