//! License lock file audit logic.

use std::path::Path;

use plemu_schemas::is_forbidden_core_id;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::error::AuditError;

// ── Lock-file types ───────────────────────────────────────────────────────────

/// Matches the shipped `expected-licenses.lock` shape
/// (`{schemaVersion, note, cores}`). Unknown fields are ignored (no
/// `deny_unknown_fields`) so additive keys in the cores repo never break the
/// audit. `schema_version`/`note` are optional so older/hand-written locks
/// still parse.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct LockFile {
    #[serde(
        rename = "schemaVersion",
        default,
        skip_serializing_if = "Option::is_none"
    )]
    pub schema_version: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub note: Option<String>,
    pub cores: Vec<LockCore>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct LockCore {
    #[serde(rename = "coreId")]
    pub core_id: String,
    #[serde(rename = "licenseFile")]
    pub license_file: String,
    #[serde(rename = "licenseSha256")]
    pub license_sha256: String,
    pub commit: String,
}

// ── Build-config types ────────────────────────────────────────────────────────

/// Matches the shipped `cores.build.json` shape (`{schemaVersion, note,
/// cores}`). Unknown fields ignored.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct BuildConfig {
    #[serde(
        rename = "schemaVersion",
        default,
        skip_serializing_if = "Option::is_none"
    )]
    pub schema_version: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub note: Option<String>,
    pub cores: Vec<BuildCore>,
}

/// The tool only needs `coreId` + `artifact`; the shipped file's
/// `systems`/`repo`/`commit`/`buildHint` keys are ignored.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct BuildCore {
    #[serde(rename = "coreId")]
    pub core_id: String,
    pub artifact: String,
}

// ── Finding types ─────────────────────────────────────────────────────────────

/// Finding code — serialises to SCREAMING_SNAKE_CASE.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum FindingCode {
    #[serde(rename = "FORBIDDEN")]
    Forbidden,
    #[serde(rename = "UNKNOWN_CORE")]
    UnknownCore,
    #[serde(rename = "TBD")]
    Tbd,
    #[serde(rename = "DRIFT")]
    Drift,
    #[serde(rename = "MISSING_LICENSE")]
    MissingLicense,
    #[serde(rename = "BUILD_FORBIDDEN")]
    BuildForbidden,
    #[serde(rename = "BUILD_ILLEGAL_ARTIFACT")]
    BuildIllegalArtifact,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AuditFinding {
    #[serde(rename = "coreId")]
    pub core_id: String,
    pub code: FindingCode,
    pub detail: String,
    /// Whether this finding makes `clean = false`.  Omitted from JSON when false.
    #[serde(skip_serializing_if = "std::ops::Not::not")]
    pub is_failure: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AuditReport {
    pub clean: bool,
    pub findings: Vec<AuditFinding>,
}

// ── Options ───────────────────────────────────────────────────────────────────

pub struct AuditOpts<'a> {
    pub cores_dir: &'a Path,
    pub build_config_path: Option<&'a Path>,
    pub strict: bool,
}

// ── Public API ────────────────────────────────────────────────────────────────

/// Audit a lock file parsed from JSON bytes, checking each core entry.
pub fn audit_lock(lock: &LockFile, opts: &AuditOpts<'_>) -> Result<AuditReport, AuditError> {
    let mut findings: Vec<AuditFinding> = Vec::new();
    let mut clean = true;

    for core in &lock.cores {
        let cid = &core.core_id;

        // a) Forbidden?
        if is_forbidden_core_id(cid) {
            findings.push(AuditFinding {
                core_id: cid.clone(),
                code: FindingCode::Forbidden,
                detail: format!("'{}' is in the forbidden core list", cid),
                is_failure: true,
            });
            clean = false;
            continue; // skip further checks for this core
        }

        // b) Valid CoreId enum?
        let core_id_json = format!("\"{}\"", cid);
        if serde_json::from_str::<plemu_schemas::CoreId>(&core_id_json).is_err() {
            findings.push(AuditFinding {
                core_id: cid.clone(),
                code: FindingCode::UnknownCore,
                detail: format!("'{}' is not a recognised CoreId", cid),
                is_failure: true,
            });
            clean = false;
            continue;
        }

        // c) TBD?
        let is_tbd = core.license_sha256.eq_ignore_ascii_case("TBD")
            || core.commit.eq_ignore_ascii_case("TBD");
        if is_tbd {
            let is_failure = opts.strict;
            findings.push(AuditFinding {
                core_id: cid.clone(),
                code: FindingCode::Tbd,
                detail: "licenseSha256 or commit is TBD".to_string(),
                is_failure,
            });
            if opts.strict {
                clean = false;
            }
            continue;
        }

        // d) License file exists and matches sha256
        let license_path = opts.cores_dir.join(cid).join(&core.license_file);
        if !license_path.exists() {
            findings.push(AuditFinding {
                core_id: cid.clone(),
                code: FindingCode::MissingLicense,
                detail: format!("license file '{}' not found", license_path.display()),
                is_failure: true,
            });
            clean = false;
            continue;
        }

        let contents = std::fs::read(&license_path)?;
        let actual_sha256 = hex::encode(Sha256::digest(&contents));
        if !actual_sha256.eq_ignore_ascii_case(&core.license_sha256) {
            findings.push(AuditFinding {
                core_id: cid.clone(),
                code: FindingCode::Drift,
                detail: format!(
                    "sha256 mismatch: expected {}, got {}",
                    core.license_sha256, actual_sha256
                ),
                is_failure: true,
            });
            clean = false;
        }
    }

    // e) Build config checks
    if let Some(bc_path) = opts.build_config_path {
        if bc_path.exists() {
            let bc_bytes = std::fs::read(bc_path)?;
            let bc: BuildConfig = serde_json::from_slice(&bc_bytes)?;
            for core in &bc.cores {
                let cid = &core.core_id;

                if is_forbidden_core_id(cid) {
                    findings.push(AuditFinding {
                        core_id: cid.clone(),
                        code: FindingCode::BuildForbidden,
                        detail: format!("build-config contains forbidden core '{}'", cid),
                        is_failure: true,
                    });
                    clean = false;
                }

                let expected_artifact = format!("{}_libretro_android.so", cid);
                if core.artifact != expected_artifact {
                    findings.push(AuditFinding {
                        core_id: cid.clone(),
                        code: FindingCode::BuildIllegalArtifact,
                        detail: format!(
                            "artifact '{}' does not match expected '{}'",
                            core.artifact, expected_artifact
                        ),
                        is_failure: true,
                    });
                    clean = false;
                }
            }
        }
    }

    Ok(AuditReport { clean, findings })
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    fn make_lock(cores: Vec<LockCore>) -> LockFile {
        LockFile {
            schema_version: Some(1),
            note: None,
            cores,
        }
    }

    fn pinned_core(core_id: &str, sha256: &str) -> LockCore {
        LockCore {
            core_id: core_id.to_string(),
            license_file: "LICENSE".to_string(),
            license_sha256: sha256.to_string(),
            commit: "abc123".to_string(),
        }
    }

    #[test]
    fn clean_pinned_core() {
        let tmp = TempDir::new().unwrap();
        let sameboy_dir = tmp.path().join("sameboy");
        std::fs::create_dir_all(&sameboy_dir).unwrap();
        let content = b"MIT License\n";
        std::fs::write(sameboy_dir.join("LICENSE"), content).unwrap();
        let sha = hex::encode(Sha256::digest(content));

        let lock = make_lock(vec![pinned_core("sameboy", &sha)]);
        let opts = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: None,
            strict: false,
        };
        let report = audit_lock(&lock, &opts).unwrap();
        assert!(report.clean, "Expected clean, got {:?}", report.findings);
        assert!(report.findings.is_empty());
    }

    #[test]
    fn drifted_license_fails() {
        let tmp = TempDir::new().unwrap();
        let sameboy_dir = tmp.path().join("sameboy");
        std::fs::create_dir_all(&sameboy_dir).unwrap();
        std::fs::write(sameboy_dir.join("LICENSE"), b"MIT License\n").unwrap();
        // Wrong sha256
        let lock = make_lock(vec![pinned_core(
            "sameboy",
            "0000000000000000000000000000000000000000000000000000000000000000",
        )]);
        let opts = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: None,
            strict: false,
        };
        let report = audit_lock(&lock, &opts).unwrap();
        assert!(!report.clean);
        assert!(report.findings.iter().any(|f| f.code == FindingCode::Drift));
    }

    #[test]
    fn forbidden_core_fails() {
        let tmp = TempDir::new().unwrap();
        let lock = make_lock(vec![pinned_core("snes9x", "abc")]);
        let opts = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: None,
            strict: false,
        };
        let report = audit_lock(&lock, &opts).unwrap();
        assert!(!report.clean);
        assert!(report
            .findings
            .iter()
            .any(|f| f.code == FindingCode::Forbidden));
    }

    #[test]
    fn tbd_nonstrict_warning_strict_fail() {
        let tmp = TempDir::new().unwrap();
        let lock = make_lock(vec![LockCore {
            core_id: "sameboy".to_string(),
            license_file: "LICENSE".to_string(),
            license_sha256: "TBD".to_string(),
            commit: "abc".to_string(),
        }]);

        // Non-strict: clean=true (warning)
        let opts_lax = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: None,
            strict: false,
        };
        let report_lax = audit_lock(&lock, &opts_lax).unwrap();
        assert!(report_lax.clean, "Expected clean in non-strict mode");
        assert_eq!(report_lax.findings.len(), 1);
        assert_eq!(report_lax.findings[0].code, FindingCode::Tbd);
        assert!(!report_lax.findings[0].is_failure);

        // Strict: clean=false
        let opts_strict = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: None,
            strict: true,
        };
        let report_strict = audit_lock(&lock, &opts_strict).unwrap();
        assert!(!report_strict.clean, "Expected dirty in strict mode");
        assert!(report_strict.findings[0].is_failure);
    }

    #[test]
    fn build_config_forbidden_fails() {
        let tmp = TempDir::new().unwrap();
        // Prepare a clean lock (no cores to check)
        let lock = make_lock(vec![]);
        let bc_path = tmp.path().join("build.json");
        let bc = serde_json::json!({
            "cores": [{ "coreId": "mesen_fast", "artifact": "mesen_fast_libretro_android.so" }]
        });
        std::fs::write(&bc_path, bc.to_string()).unwrap();
        let opts = AuditOpts {
            cores_dir: tmp.path(),
            build_config_path: Some(&bc_path),
            strict: false,
        };
        let report = audit_lock(&lock, &opts).unwrap();
        assert!(!report.clean);
        assert!(report
            .findings
            .iter()
            .any(|f| f.code == FindingCode::BuildForbidden));
    }
}
