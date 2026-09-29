//! `plemu-manifest` — build, sign, verify, and diff Plemu
//! content-trust manifests. SPEC v1 §2–§5.
//!
//! The library exposes pure functions (no process exit, no `unwrap` outside
//! tests); the CLI in `main.rs` is a thin wrapper that maps results to stdout /
//! stderr / exit codes.

pub mod build;
pub mod crypto;
pub mod diff;
pub mod error;
pub mod sign;
pub mod verify;

pub use build::{assemble_manifest, build_manifest, collect_entries};
pub use error::{ManifestError, Result};
pub use verify::{freshness, verify_manifest, Freshness, Verified};
