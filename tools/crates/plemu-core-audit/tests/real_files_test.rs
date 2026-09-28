//! Drift guard: the audit tool must agree with the files the cores repo
//! actually ships. `testdata/expected-licenses.lock` and
//! `testdata/cores.build.json` are verbatim synced copies of
//! `plemu-cores/cores/{expected-licenses.lock,cores.build.json}`.
//!
//! These tests prove the audit PARSES both real files and then fails only on
//! the intended "cores not pinned" (TBD) business logic — never on a
//! deserialization error. If the cores repo's schema drifts, this is the test
//! that goes red.

use plemu_core_audit::{audit_lock, AuditOpts, BuildConfig, LockFile};
use std::path::PathBuf;

fn testdata(name: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("testdata")
        .join(name)
}

#[test]
fn real_lock_file_parses() {
    let bytes = std::fs::read(testdata("expected-licenses.lock")).unwrap();
    // Deserialization must succeed — this is the drift guard.
    let lock: LockFile = serde_json::from_slice(&bytes)
        .expect("real expected-licenses.lock must deserialize (schema drift?)");
    assert_eq!(lock.schema_version, Some(1));
    assert_eq!(
        lock.cores.len(),
        16,
        "cores repo ships 16 allowlisted cores"
    );
    // Fields the audit relies on are populated.
    assert!(lock.cores.iter().any(|c| c.core_id == "sameboy"));
    assert!(lock.cores.iter().all(|c| !c.license_file.is_empty()));
}

#[test]
fn real_build_config_parses() {
    let bytes = std::fs::read(testdata("cores.build.json")).unwrap();
    let bc: BuildConfig = serde_json::from_slice(&bytes)
        .expect("real cores.build.json must deserialize (schema drift?)");
    assert_eq!(bc.schema_version, Some(1));
    assert_eq!(bc.cores.len(), 16);
    // The `artifact` key (not `artifactName`) is what the tool reads, and the
    // shipped names satisfy the `<coreId>_libretro_android.so` rule.
    for core in &bc.cores {
        assert_eq!(
            core.artifact,
            format!("{}_libretro_android.so", core.core_id)
        );
    }
}

#[test]
fn real_files_audit_fails_only_on_tbd_in_strict() {
    // The shipped lock is intentionally all-TBD (unpinned). Running the audit
    // against it in --strict mode must FAIL — but as the TBD business-logic
    // finding, not a parse error. cores-dir points at an empty temp dir; TBD
    // short-circuits before any license-file lookup, so no license files are
    // needed.
    let lock_bytes = std::fs::read(testdata("expected-licenses.lock")).unwrap();
    let lock: LockFile = serde_json::from_slice(&lock_bytes).unwrap();

    let empty_cores = tempfile::TempDir::new().unwrap();
    let build_cfg = testdata("cores.build.json");

    let opts = AuditOpts {
        cores_dir: empty_cores.path(),
        build_config_path: Some(&build_cfg),
        strict: true,
    };
    let report = audit_lock(&lock, &opts).unwrap();

    assert!(
        !report.clean,
        "all-TBD lock must fail in --strict: {:?}",
        report.findings
    );

    // Every lock core is flagged, and every one is the TBD finding — proving
    // the failure is the intended unpinned-cores logic. No FORBIDDEN /
    // UNKNOWN_CORE / MISSING_LICENSE / DRIFT / BUILD_* findings: all 16 cores
    // are allowlisted, and the build config's artifact names are all valid.
    let tbd_count = report
        .findings
        .iter()
        .filter(|f| matches!(f.code, plemu_core_audit::FindingCode::Tbd))
        .count();
    assert_eq!(
        tbd_count, 16,
        "expected 16 TBD findings, got: {:?}",
        report.findings
    );
    assert!(
        report
            .findings
            .iter()
            .all(|f| matches!(f.code, plemu_core_audit::FindingCode::Tbd)),
        "the ONLY findings must be TBD (no parse/forbidden/artifact errors): {:?}",
        report.findings
    );
    assert!(report.findings.iter().all(|f| f.is_failure));
}

#[test]
fn real_files_audit_non_strict_warns_but_clean() {
    // Same all-TBD lock in non-strict mode: TBD is a warning, so clean stays
    // true (unpinned cores are surfaced but do not block). Still proves the
    // files parse and audit end to end.
    let lock_bytes = std::fs::read(testdata("expected-licenses.lock")).unwrap();
    let lock: LockFile = serde_json::from_slice(&lock_bytes).unwrap();

    let empty_cores = tempfile::TempDir::new().unwrap();
    let build_cfg = testdata("cores.build.json");

    let opts = AuditOpts {
        cores_dir: empty_cores.path(),
        build_config_path: Some(&build_cfg),
        strict: false,
    };
    let report = audit_lock(&lock, &opts).unwrap();

    assert!(report.clean, "non-strict all-TBD must be clean (warnings)");
    assert_eq!(report.findings.len(), 16);
    assert!(report.findings.iter().all(|f| !f.is_failure));
}
