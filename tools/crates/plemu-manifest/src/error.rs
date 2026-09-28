//! Typed errors for manifest operations.
//!
//! Every failure mode a verifier or builder can hit is a distinct variant so
//! callers (and tests) can assert on the *reason* rather than string-matching.

/// Errors produced by the `plemu-manifest` library.
#[derive(Debug, thiserror::Error)]
pub enum ManifestError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("signature verification failed: {0}")]
    SignatureInvalid(String),

    #[error("key not found: {0}")]
    KeyNotFound(String),

    #[error("key status rejected: {key_id} has status {status}")]
    KeyStatusRejected { key_id: String, status: String },

    #[error("algorithm not supported: {0}")]
    AlgNotSupported(String),

    #[error("schema version mismatch: expected 1, got {0}")]
    SchemaVersionMismatch(u64),

    #[error("signerKeyId mismatch: manifest says {manifest_key}, sig says {sig_key}")]
    SignerKeyIdMismatch {
        manifest_key: String,
        sig_key: String,
    },

    #[error("sequence too low: got {got}, min required {min}")]
    SequenceTooLow { got: u64, min: u64 },

    #[error("base64 decode error: {0}")]
    Base64(#[from] base64::DecodeError),

    #[error("hex decode error: {0}")]
    Hex(#[from] hex::FromHexError),

    #[error("validation error: {0}")]
    Validation(String),
}

/// Convenience alias for library results.
pub type Result<T> = std::result::Result<T, ManifestError>;
