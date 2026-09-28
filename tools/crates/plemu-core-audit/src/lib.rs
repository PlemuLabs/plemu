//! plemu-core-audit: audit the license lock file for approved cores.
//!
//! Validates that each core in the lock file:
//! - is not in the forbidden list
//! - is a recognised CoreId
//! - has a licence file whose sha256 matches the locked value
//!
//! Also validates an optional build-config for forbidden cores and naming
//! conventions.

pub mod audit;
pub mod error;

pub use audit::{
    audit_lock, AuditFinding, AuditOpts, AuditReport, BuildConfig, BuildCore, FindingCode,
    LockCore, LockFile,
};
pub use error::AuditError;
