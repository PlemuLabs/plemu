//! plemu-screen: full-spectrum content screening for registry submissions.
//!
//! Performs 11 checks:
//!  1. DIGESTS      — sha256/md5/sha1/crc32 of the artifact (feeder; mismatch flags)
//!  2. DAT_MATCH    — denylist hash lookup, headered + headerless (hard-fail)
//!  3. HDR_VALID    — system-specific header validation
//!  4. SIZE_SANITY  — size cap and declared-vs-actual
//!  5. DEDUP        — sha256 duplicate across registry
//!  6. NAME_FUZZY   — jaro-winkler name matching against denylist
//!  7. STR_SCAN     — copyright string scan
//!  8. ARCHIVE_SAFETY — zip only for ps1/dc; zip-bomb / path-traversal (hard-fail)
//!  9. ATTESTATION  — ed25519 signature + wallet + timestamp (hard-fail)
//! 10. SCHEMA       — structural metadata validation incl.
//!     declared-vs-computed sha256 (hard-fail)
//! 11. EVIDENCE_URL — license evidence URL

pub mod checks;
pub mod error;

pub use checks::{
    check_archive_safety, check_attestation, check_dat_match, check_dedup, check_digests,
    check_evidence_url, check_hdr_valid, check_name_fuzzy, check_schema, check_size_sanity,
    check_str_scan, compute_all_digests, ArchiveSafetyResult, ArtifactDigests, AttestationResult,
    CheckResult, CheckStatus, DigestsResult, ScreenResult,
};
pub use error::ScreenError;

use plemu_schemas::{RegistryAttestation, RegistryMetadata};
use std::path::Path;

/// Verdict for the full screen.
#[derive(Debug, Clone, PartialEq, Eq, serde::Serialize)]
#[serde(rename_all = "lowercase")]
pub enum Verdict {
    Pass,
    Review,
    Fail,
}

/// Full screen report (SPEC §5.2 shape; `dat_snapshot` serializes as `datSnapshot`).
#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FullScreenReport {
    pub tool: &'static str,
    pub version: &'static str,
    pub dat_snapshot: String,
    pub entry: String,
    pub digests: serde_json::Value,
    pub checks: Vec<CheckResult>,
    pub verdict: Verdict,
}

/// Options for a full entry screen.
pub struct ScreenOpts<'a> {
    /// Path to the entry directory.
    pub entry_dir: &'a Path,
    /// Path to denylist SQLite (None = skip DAT_MATCH and NAME_FUZZY).
    pub denylist_path: Option<&'a Path>,
    /// Override current time (RFC 3339).
    pub now_override: Option<&'a str>,
}

/// Open a denylist for screening: read-only (never creates a file at a
/// typo'd path — plain `Connection::open` would silently create an empty DB
/// and every DAT_MATCH would pass) and structurally validated (the SPEC §5.1
/// `hashes`, `names`, and `meta` tables must exist).
///
/// Any failure here is a TOOL error, never a screening verdict.
pub fn open_denylist(path: &Path) -> Result<rusqlite::Connection, ScreenError> {
    let conn =
        rusqlite::Connection::open_with_flags(path, rusqlite::OpenFlags::SQLITE_OPEN_READ_ONLY)
            .map_err(|e| {
                ScreenError::Denylist(format!("cannot open denylist '{}': {e}", path.display()))
            })?;

    for table in ["hashes", "names", "meta"] {
        let exists: bool = conn
            .query_row(
                "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=?1)",
                rusqlite::params![table],
                |r| r.get(0),
            )
            .map_err(|e| {
                ScreenError::Denylist(format!("cannot inspect denylist '{}': {e}", path.display()))
            })?;
        if !exists {
            return Err(ScreenError::Denylist(format!(
                "denylist '{}' is missing required table '{}' (not built by plemu-dat?)",
                path.display(),
                table
            )));
        }
    }

    Ok(conn)
}

/// Determine the overall verdict from a list of check results.
pub fn verdict_from_checks(checks: &[CheckResult]) -> Verdict {
    if checks.iter().any(|c| c.status == CheckStatus::Fail) {
        Verdict::Fail
    } else if checks.iter().any(|c| c.status == CheckStatus::Flag) {
        Verdict::Review
    } else {
        Verdict::Pass
    }
}

/// Run all 11 checks on an entry directory.
pub fn screen_entry_full(opts: &ScreenOpts<'_>) -> Result<FullScreenReport, ScreenError> {
    let entry_dir = opts.entry_dir;

    // Load metadata.json
    let metadata_path = entry_dir.join("metadata.json");
    if !metadata_path.exists() {
        return Err(ScreenError::MissingFile("metadata.json".to_string()));
    }
    let metadata_bytes = std::fs::read(&metadata_path)?;
    let metadata: RegistryMetadata = serde_json::from_slice(&metadata_bytes)?;

    // Load attestation.json
    let attestation_path = entry_dir.join("attestation.json");
    if !attestation_path.exists() {
        return Err(ScreenError::MissingFile("attestation.json".to_string()));
    }
    let attestation_bytes = std::fs::read(&attestation_path)?;
    let attestation: RegistryAttestation = serde_json::from_slice(&attestation_bytes)?;

    // Find artifact file
    let artifact_path = entry_dir.join(&metadata.artifact.filename);
    if !artifact_path.exists() {
        return Err(ScreenError::MissingFile(metadata.artifact.filename.clone()));
    }
    let artifact_bytes = std::fs::read(&artifact_path)?;

    // Parse now
    let now: chrono::DateTime<chrono::Utc> = if let Some(now_str) = opts.now_override {
        chrono::DateTime::parse_from_rfc3339(now_str)
            .map_err(|e| ScreenError::TimestampParse(e.to_string()))?
            .with_timezone(&chrono::Utc)
    } else {
        chrono::Utc::now()
    };

    // Open denylist connection (optional): read-only + schema-validated.
    let conn_opt: Option<rusqlite::Connection> =
        opts.denylist_path.map(open_denylist).transpose()?;

    // Read dat_snapshot from meta table if available.
    // SPEC §5.2: datSnapshot is the DATE part ("YYYY-MM-DD") of meta.built_at.
    let dat_snapshot = if let Some(ref conn) = conn_opt {
        conn.query_row(
            "SELECT value FROM meta WHERE key='built_at' LIMIT 1",
            [],
            |row| row.get::<_, String>(0),
        )
        .map(|built_at| {
            built_at
                .split('T')
                .next()
                .unwrap_or(built_at.as_str())
                .to_string()
        })
        .unwrap_or_else(|_| "unknown".to_string())
    } else {
        "no-denylist".to_string()
    };

    // Compute multi-algo digests
    let digests = compute_all_digests(&artifact_bytes);
    let digests_json = serde_json::json!({
        "crc32": digests.crc32,
        "md5": digests.md5,
        "sha1": digests.sha1,
        "sha256": digests.sha256,
    });

    let mut all_checks: Vec<CheckResult> = Vec::new();

    // 1. DIGESTS — feeder check; not in the SPEC §5.2 hard-fail set. A
    // declared-vs-computed mismatch flags here; the hard-fail is SCHEMA's job.
    let dr = check_digests(&artifact_path, &metadata.artifact.sha256)?;
    all_checks.push(CheckResult {
        id: "DIGESTS".to_string(),
        status: if dr.ok {
            CheckStatus::Pass
        } else {
            CheckStatus::Flag
        },
        detail: if dr.ok {
            "sha256 matches metadata".to_string()
        } else {
            format!("sha256 mismatch: expected {}, got {}", dr.expected, dr.got)
        },
    });

    // 2. DAT_MATCH (a denylist query failure aborts — it must never pass)
    if let Some(ref conn) = conn_opt {
        all_checks.push(check_dat_match(&artifact_bytes, metadata.system, conn)?);
    } else {
        all_checks.push(CheckResult {
            id: "DAT_MATCH".to_string(),
            status: CheckStatus::Pass,
            detail: "skipped: no denylist".to_string(),
        });
    }

    // 3. HDR_VALID
    all_checks.push(check_hdr_valid(&artifact_bytes, metadata.system));

    // 4. SIZE_SANITY
    all_checks.push(check_size_sanity(&artifact_bytes, &metadata));

    // 5. DEDUP
    all_checks.push(check_dedup(entry_dir, &metadata.artifact.sha256));

    // 6. NAME_FUZZY (a denylist query failure aborts — it must never pass)
    if let Some(ref conn) = conn_opt {
        all_checks.push(check_name_fuzzy(
            &metadata.title,
            &metadata.artifact.filename,
            conn,
        )?);
    } else {
        all_checks.push(CheckResult {
            id: "NAME_FUZZY".to_string(),
            status: CheckStatus::Pass,
            detail: "skipped: no denylist".to_string(),
        });
    }

    // 7. STR_SCAN
    all_checks.push(check_str_scan(&artifact_bytes, metadata.system));

    // 8. ARCHIVE_SAFETY
    let ar = check_archive_safety(&artifact_path, metadata.system)?;
    all_checks.push(CheckResult {
        id: "ARCHIVE_SAFETY".to_string(),
        status: if ar.ok {
            CheckStatus::Pass
        } else {
            CheckStatus::Fail
        },
        detail: ar.message.unwrap_or_else(|| {
            if ar.checked {
                "zip valid".to_string()
            } else {
                "not a zip".to_string()
            }
        }),
    });

    // 9. ATTESTATION
    let attest_r = check_attestation(&metadata, &attestation, now)?;
    all_checks.push(CheckResult {
        id: "ATTESTATION".to_string(),
        status: if attest_r.ok {
            CheckStatus::Pass
        } else {
            CheckStatus::Fail
        },
        detail: attest_r
            .message
            .unwrap_or_else(|| "signature valid".to_string()),
    });

    // 10. SCHEMA (carries the hard-fail for declared-vs-computed sha256)
    all_checks.push(check_schema(
        entry_dir,
        &metadata,
        &attestation_bytes,
        &digests.sha256,
    ));

    // 11. EVIDENCE_URL
    all_checks.push(check_evidence_url(metadata.license.evidence_url.as_deref()));

    let verdict = verdict_from_checks(&all_checks);
    let entry_id = metadata.id.clone();

    Ok(FullScreenReport {
        tool: "plemu-screen",
        version: env!("CARGO_PKG_VERSION"),
        dat_snapshot,
        entry: entry_id,
        digests: digests_json,
        checks: all_checks,
        verdict,
    })
}

/// Run the original three checks on an entry directory (backward-compat).
pub fn screen_entry(
    entry_dir: &std::path::Path,
    now_override: Option<&str>,
) -> Result<ScreenResult, ScreenError> {
    checks::screen_entry(entry_dir, now_override)
}

impl serde::Serialize for CheckResult {
    fn serialize<S: serde::Serializer>(&self, s: S) -> Result<S::Ok, S::Error> {
        use serde::ser::SerializeMap;
        let mut m = s.serialize_map(Some(3))?;
        m.serialize_entry("id", &self.id)?;
        m.serialize_entry("status", self.status.as_str())?;
        m.serialize_entry("detail", &self.detail)?;
        m.end()
    }
}
