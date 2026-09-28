//! Integration tests for plemu-core-audit CLI.

use assert_cmd::Command;
use sha2::{Digest, Sha256};
use std::fs;
use std::path::Path;
use tempfile::TempDir;

fn write_lock(dir: &Path, sha256: &str) {
    // Shipped shape: {schemaVersion, note, cores} — no lockedAt.
    let lock = serde_json::json!({
        "schemaVersion": 1,
        "note": "test fixture",
        "cores": [{
            "coreId": "sameboy",
            "licenseFile": "LICENSE",
            "licenseSha256": sha256,
            "commit": "abc123"
        }]
    });
    fs::write(dir.join("lock.json"), lock.to_string()).unwrap();
}

fn write_lock_forbidden(dir: &Path) {
    let lock = serde_json::json!({
        "schemaVersion": 1,
        "cores": [{
            "coreId": "snes9x",
            "licenseFile": "LICENSE",
            "licenseSha256": "abc",
            "commit": "abc"
        }]
    });
    fs::write(dir.join("lock.json"), lock.to_string()).unwrap();
}

fn write_lock_tbd(dir: &Path) {
    let lock = serde_json::json!({
        "schemaVersion": 1,
        "cores": [{
            "coreId": "sameboy",
            "licenseFile": "LICENSE",
            "licenseSha256": "TBD",
            "commit": "abc"
        }]
    });
    fs::write(dir.join("lock.json"), lock.to_string()).unwrap();
}

#[test]
fn clean_lock_exits_0() {
    let tmp = TempDir::new().unwrap();
    let cores_dir = tmp.path().join("cores");
    let sameboy_dir = cores_dir.join("sameboy");
    fs::create_dir_all(&sameboy_dir).unwrap();

    let license_content = b"MIT License\n";
    fs::write(sameboy_dir.join("LICENSE"), license_content).unwrap();
    let sha = hex::encode(Sha256::digest(license_content));

    write_lock(tmp.path(), &sha);

    Command::cargo_bin("plemu-core-audit")
        .unwrap()
        .args([
            "--lock",
            &tmp.path().join("lock.json").to_string_lossy(),
            "--cores-dir",
            &cores_dir.to_string_lossy(),
        ])
        .assert()
        .success();
}

#[test]
fn drift_exits_20() {
    let tmp = TempDir::new().unwrap();
    let cores_dir = tmp.path().join("cores");
    let sameboy_dir = cores_dir.join("sameboy");
    fs::create_dir_all(&sameboy_dir).unwrap();
    fs::write(sameboy_dir.join("LICENSE"), b"MIT License\n").unwrap();

    // Wrong sha256 → DRIFT
    write_lock(
        tmp.path(),
        "0000000000000000000000000000000000000000000000000000000000000000",
    );

    let out_path = tmp.path().join("report.json");
    let status = Command::cargo_bin("plemu-core-audit")
        .unwrap()
        .args([
            "--lock",
            &tmp.path().join("lock.json").to_string_lossy(),
            "--cores-dir",
            &cores_dir.to_string_lossy(),
            "--out",
            &out_path.to_string_lossy(),
        ])
        .assert()
        .failure();

    // Exit code 20
    status.code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out_path).unwrap()).unwrap();
    assert_eq!(report["clean"], false);
    let findings = report["findings"].as_array().unwrap();
    assert!(findings.iter().any(|f| f["code"] == "DRIFT"));
}

#[test]
fn forbidden_core_exits_20() {
    let tmp = TempDir::new().unwrap();
    let cores_dir = tmp.path().join("cores");
    fs::create_dir_all(&cores_dir).unwrap();

    write_lock_forbidden(tmp.path());

    Command::cargo_bin("plemu-core-audit")
        .unwrap()
        .args([
            "--lock",
            &tmp.path().join("lock.json").to_string_lossy(),
            "--cores-dir",
            &cores_dir.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);
}

#[test]
fn tbd_nonstrict_exits_0() {
    let tmp = TempDir::new().unwrap();
    let cores_dir = tmp.path().join("cores");
    fs::create_dir_all(&cores_dir).unwrap();

    write_lock_tbd(tmp.path());

    Command::cargo_bin("plemu-core-audit")
        .unwrap()
        .args([
            "--lock",
            &tmp.path().join("lock.json").to_string_lossy(),
            "--cores-dir",
            &cores_dir.to_string_lossy(),
        ])
        .assert()
        .success(); // non-strict: clean=true
}

#[test]
fn tbd_strict_exits_20() {
    let tmp = TempDir::new().unwrap();
    let cores_dir = tmp.path().join("cores");
    fs::create_dir_all(&cores_dir).unwrap();

    write_lock_tbd(tmp.path());

    Command::cargo_bin("plemu-core-audit")
        .unwrap()
        .args([
            "--lock",
            &tmp.path().join("lock.json").to_string_lossy(),
            "--cores-dir",
            &cores_dir.to_string_lossy(),
            "--strict",
        ])
        .assert()
        .failure()
        .code(20);
}
