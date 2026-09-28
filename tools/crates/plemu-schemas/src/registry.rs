//! Detached signature envelope and pinned key registry. SPEC §2.1–2.2.

use schemars::JsonSchema;
use serde::{Deserialize, Serialize};

use crate::enums::KeyStatus;

/// The detached `manifest.sig` envelope. SPEC §2.1.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct SigEnvelope {
    pub key_id: String,
    /// Signature algorithm; only `"ed25519"` is accepted at v1.
    pub alg: String,
    /// base64 of the 64-byte ed25519 signature over raw manifest bytes.
    pub signature: String,
    pub signed_at: String,
}

/// The pinned signer-key registry (`keys/manifest-signers.json`). SPEC §2.2.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
pub struct KeyRegistry {
    pub keys: Vec<KeyEntry>,
}

impl KeyRegistry {
    /// Look up a key by its `keyId`. Returns the first match, if any.
    pub fn find(&self, key_id: &str) -> Option<&KeyEntry> {
        self.keys.iter().find(|k| k.key_id == key_id)
    }
}

/// A single registered signer key. SPEC §2.2.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct KeyEntry {
    pub key_id: String,
    /// 64 hex chars = 32-byte ed25519 public key.
    pub public_key: String,
    pub status: KeyStatus,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub valid_from: Option<String>,
}
