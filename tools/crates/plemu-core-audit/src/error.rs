//! Error types for plemu-core-audit.

use thiserror::Error;

#[derive(Debug, Error)]
pub enum AuditError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON parse error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("Missing file: {0}")]
    MissingFile(String),
}
