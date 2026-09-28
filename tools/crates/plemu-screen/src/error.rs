//! Error types for plemu-screen.

use thiserror::Error;

#[derive(Debug, Error)]
pub enum ScreenError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON parse error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("Missing file: {0}")]
    MissingFile(String),

    #[error("Digest mismatch: expected {expected}, got {got}")]
    DigestMismatch { expected: String, got: String },

    #[error("Attestation error: {0}")]
    Attestation(String),

    #[error("Archive safety error: {0}")]
    ArchiveSafety(String),

    #[error("Timestamp parse error: {0}")]
    TimestampParse(String),

    #[error("Crypto error: {0}")]
    Crypto(String),

    /// Denylist unusable: missing file, unreadable DB, absent tables, or a
    /// query failure mid-screen. Tool error — never a verdict.
    #[error("Denylist error: {0}")]
    Denylist(String),
}
