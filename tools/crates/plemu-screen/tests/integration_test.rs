//! Integration tests for plemu-screen CLI.

use assert_cmd::Command;
use base64::Engine;
use ed25519_dalek::Signer;
use plemu_schemas::{
    build_attestation_message, AttestationParams, AttestationRole, LicenseType, System,
};
use sha2::{Digest, Sha256};
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use tempfile::TempDir;

// ── Helpers ───────────────────────────────────────────────────────────────────

fn make_signing_key() -> ed25519_dalek::SigningKey {
    ed25519_dalek::SigningKey::from_bytes(&[42u8; 32])
}

fn wallet_from_key(key: &ed25519_dalek::SigningKey) -> String {
    bs58::encode(key.verifying_key().as_bytes()).into_string()
}

fn sign_str(key: &ed25519_dalek::SigningKey, message: &str) -> String {
    let sig = key.sign(message.as_bytes());
    base64::engine::general_purpose::STANDARD.encode(sig.to_bytes())
}

/// Create an empty denylist SQLite file with the correct schema.
fn make_empty_denylist(path: &Path) {
    let conn = rusqlite::Connection::open(path).unwrap();
    conn.execute_batch(
        "CREATE TABLE IF NOT EXISTS hashes(
            algo TEXT NOT NULL, hex TEXT NOT NULL,
            source TEXT, rom_name TEXT, game_name TEXT, system TEXT,
            PRIMARY KEY(algo, hex)
         );
         CREATE TABLE IF NOT EXISTS names(
            normalized TEXT NOT NULL PRIMARY KEY, game_name TEXT, system TEXT
         );
         CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT);
         INSERT OR IGNORE INTO meta VALUES('built_at', '2026-01-01T00:00:00Z');
         INSERT OR IGNORE INTO meta VALUES('dat_files', '');
         INSERT OR IGNORE INTO meta VALUES('hash_rows', '0');
         INSERT OR IGNORE INTO meta VALUES('name_rows', '0');",
    )
    .unwrap();
}

/// Insert a sha256 hash into the denylist.
fn denylist_add_sha256(path: &Path, sha256: &str) {
    let conn = rusqlite::Connection::open(path).unwrap();
    conn.execute(
        "INSERT OR IGNORE INTO hashes VALUES('sha256', ?1, 'test', 'test.wasm', 'Test Game', 'wasm4')",
        rusqlite::params![sha256],
    )
    .unwrap();
}

/// Insert a normalized name into the denylist.
fn denylist_add_name(path: &Path, normalized: &str, game_name: &str) {
    let conn = rusqlite::Connection::open(path).unwrap();
    conn.execute(
        "INSERT OR IGNORE INTO names VALUES(?1, ?2, 'gb')",
        rusqlite::params![normalized, game_name],
    )
    .unwrap();
}

/// Write a valid entry directory and return its path.
#[allow(clippy::too_many_arguments)]
fn make_entry(
    root: &Path,
    system_str: &str,
    system: System,
    core_id_str: &str,
    slug: &str,
    title: &str,
    license: LicenseType,
    signing_key: &ed25519_dalek::SigningKey,
    artifact_bytes: &[u8],
    artifact_name: &str,
) -> PathBuf {
    let entry_dir = root.join(system_str).join(slug);
    fs::create_dir_all(&entry_dir).unwrap();

    let sha256 = hex::encode(Sha256::digest(artifact_bytes));
    fs::write(entry_dir.join(artifact_name), artifact_bytes).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let submitted_at = "2026-07-12T11:00:00Z";
    let wallet = wallet_from_key(signing_key);

    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title,
        system,
        sha256: &sha256,
        license,
        wallet: &wallet,
        timestamp: signed_at,
    });
    let signature = sign_str(signing_key, &message);

    let evidence_url = if license == LicenseType::Cc0 {
        serde_json::json!("https://example.com/cc0")
    } else {
        serde_json::json!(null)
    };

    let metadata = serde_json::json!({
        "id": format!("{}/{}", system_str, slug),
        "title": title,
        "system": system_str,
        "coreId": core_id_str,
        "minCoreVersion": "0.1.0",
        "sha256": sha256,
        "sizeBytes": artifact_bytes.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": license.as_str(), "evidenceUrl": evidence_url },
        "submission": { "via": "pr", "submittedAt": submitted_at },
        "artifact": {
            "sha256": sha256,
            "sizeBytes": artifact_bytes.len() as u64,
            "filename": artifact_name
        }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator",
        "message": message,
        "signature": signature,
        "signedAt": signed_at,
        "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    entry_dir
}

fn make_valid_wasm4_cart() -> Vec<u8> {
    // 64-byte wasm4 cart starting with \0asm
    let mut bytes = vec![0u8; 64];
    bytes[0] = 0x00;
    bytes[1] = b'a';
    bytes[2] = b's';
    bytes[3] = b'm';
    bytes
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[test]
fn happy_path_wasm4_exits_0() {
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "my-game",
        "My Game",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);

    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .success();

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "pass", "full report: {}", report);
    assert_eq!(report["tool"], "plemu-screen");
}

#[test]
fn dat_match_fail_exits_20() {
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let sha256 = hex::encode(Sha256::digest(&artifact));
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "bad-game",
        "Bad Game",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    denylist_add_sha256(&denylist, &sha256);

    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let dat_check = checks.iter().find(|c| c["id"] == "DAT_MATCH").unwrap();
    assert_eq!(dat_check["status"], "fail");
}

#[test]
fn name_fuzzy_flag_exits_10() {
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    // Title very similar to a known denylist game
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "super-mario-land-clone",
        "Super Mario Land Clone",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    // Normalized "Super Mario Land (World)" → "super mario land"
    denylist_add_name(&denylist, "super mario land", "Super Mario Land (World)");

    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        // Should be review (exit 10)
        .code(10);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "review");
    let checks = report["checks"].as_array().unwrap();
    let fuzzy = checks.iter().find(|c| c["id"] == "NAME_FUZZY").unwrap();
    assert_eq!(fuzzy["status"], "flag");
}

#[test]
fn schema_fail_wrong_core_exits_20() {
    let tmp = TempDir::new().unwrap();
    // Use an illegally-built entry where metadata.json has coreId=mgba for wasm4 system
    // The JSON will parse OK (mgba is a valid CoreId enum value) but SCHEMA check
    // should detect it's not legal for wasm4.
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let sha256 = hex::encode(Sha256::digest(&artifact));
    let slug = "wrong-core";
    let entry_dir = tmp.path().join("wasm4").join(slug);
    fs::create_dir_all(&entry_dir).unwrap();
    fs::write(entry_dir.join("cart.wasm"), &artifact).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let wallet = wallet_from_key(&key);
    // Build message as if valid (wasm4 system) — the system field in message matches
    // metadata.system, so attestation itself should pass, but SCHEMA should catch bad coreId
    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title: "Wrong Core",
        system: System::Wasm4,
        sha256: &sha256,
        license: LicenseType::Cc0,
        wallet: &wallet,
        timestamp: signed_at,
    });
    let sig = sign_str(&key, &message);

    let metadata = serde_json::json!({
        "id": format!("wasm4/{}", slug),
        "title": "Wrong Core",
        "system": "wasm4",
        "coreId": "mgba",   // wrong: mgba is for gba, not wasm4
        "minCoreVersion": "0.1.0",
        "sha256": sha256,
        "sizeBytes": artifact.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": "CC0", "evidenceUrl": "https://example.com/cc0" },
        "submission": { "via": "pr", "submittedAt": "2026-07-12T11:00:00Z" },
        "artifact": { "sha256": sha256, "sizeBytes": artifact.len() as u64, "filename": "cart.wasm" }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator", "message": message, "signature": sig,
        "signedAt": signed_at, "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let schema_check = checks.iter().find(|c| c["id"] == "SCHEMA").unwrap();
    assert_eq!(
        schema_check["status"], "fail",
        "SCHEMA should fail: {}",
        report
    );
}

#[test]
fn attestation_wrong_key_exits_20() {
    let tmp = TempDir::new().unwrap();
    let real_key = make_signing_key();
    let wrong_key = ed25519_dalek::SigningKey::from_bytes(&[99u8; 32]);
    let artifact = make_valid_wasm4_cart();
    let sha256 = hex::encode(Sha256::digest(&artifact));
    let slug = "attest-bad";
    let entry_dir = tmp.path().join("wasm4").join(slug);
    fs::create_dir_all(&entry_dir).unwrap();
    fs::write(entry_dir.join("cart.wasm"), &artifact).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let wallet = wallet_from_key(&real_key);
    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title: "Attest Bad",
        system: System::Wasm4,
        sha256: &sha256,
        license: LicenseType::Cc0,
        wallet: &wallet,
        timestamp: signed_at,
    });
    // Sign with WRONG key
    let sig = sign_str(&wrong_key, &message);

    let metadata = serde_json::json!({
        "id": format!("wasm4/{}", slug),
        "title": "Attest Bad",
        "system": "wasm4",
        "coreId": "wasm4",
        "minCoreVersion": "0.1.0",
        "sha256": sha256,
        "sizeBytes": artifact.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": "CC0", "evidenceUrl": "https://example.com/cc0" },
        "submission": { "via": "pr", "submittedAt": "2026-07-12T11:00:00Z" },
        "artifact": { "sha256": sha256, "sizeBytes": artifact.len() as u64, "filename": "cart.wasm" }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator", "message": message, "signature": sig,
        "signedAt": signed_at, "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let attest_check = checks.iter().find(|c| c["id"] == "ATTESTATION").unwrap();
    assert_eq!(attest_check["status"], "fail");
}

#[test]
fn archive_safety_zip_for_gb_exits_20() {
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();

    // Create a zip file with path traversal (to trigger ARCHIVE_SAFETY hard-fail)
    let zip_bytes = {
        let mut buf = Vec::new();
        {
            let mut zip = zip::ZipWriter::new(std::io::Cursor::new(&mut buf));
            let opts = zip::write::SimpleFileOptions::default()
                .compression_method(zip::CompressionMethod::Stored);
            zip.start_file("../evil.sh", opts).unwrap();
            zip.write_all(b"evil").unwrap();
            zip.finish().unwrap();
        }
        buf
    };
    let sha256 = hex::encode(Sha256::digest(&zip_bytes));

    let slug = "bad-zip";
    let entry_dir = tmp.path().join("gb").join(slug);
    fs::create_dir_all(&entry_dir).unwrap();
    fs::write(entry_dir.join("game.zip"), &zip_bytes).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let wallet = wallet_from_key(&key);
    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title: "Bad Zip",
        system: System::Gb,
        sha256: &sha256,
        license: LicenseType::Cc0,
        wallet: &wallet,
        timestamp: signed_at,
    });
    let sig = sign_str(&key, &message);

    let metadata = serde_json::json!({
        "id": format!("gb/{}", slug),
        "title": "Bad Zip",
        "system": "gb",
        "coreId": "sameboy",
        "minCoreVersion": "0.1.0",
        "sha256": sha256,
        "sizeBytes": zip_bytes.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": "CC0", "evidenceUrl": "https://example.com/cc0" },
        "submission": { "via": "pr", "submittedAt": "2026-07-12T11:00:00Z" },
        "artifact": { "sha256": sha256, "sizeBytes": zip_bytes.len() as u64, "filename": "game.zip" }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator", "message": message, "signature": sig,
        "signedAt": signed_at, "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let arc = checks.iter().find(|c| c["id"] == "ARCHIVE_SAFETY").unwrap();
    assert_eq!(arc["status"], "fail");
}

#[test]
fn cli_built_denylist_yields_real_dat_snapshot() {
    // Fix-round-1 item 1: the REAL `plemu-dat build` CLI (not a test helper)
    // must populate meta so screen reports carry the build date, not "unknown".
    let tmp = TempDir::new().unwrap();

    let dat_dir = tmp.path().join("dats");
    fs::create_dir_all(&dat_dir).unwrap();
    fs::write(
        dat_dir.join("gb.dat"),
        r#"<?xml version="1.0"?>
<datafile>
  <game name="Some Commercial Game (USA)">
    <rom name="Some Commercial Game (USA).gb" size="65536" crc="0e7ce1cb" sha1="7c15643afd0f2d16ccdc6e20e37a57d5c2ba12a4"/>
  </game>
</datafile>"#,
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    Command::cargo_bin("plemu-dat")
        .unwrap()
        .args([
            "build",
            "--input",
            &dat_dir.to_string_lossy(),
            "--out",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-11T09:30:00Z",
        ])
        .assert()
        .success();

    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "snapshot-game",
        "Snapshot Game",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    let out = tmp.path().join("report.json");
    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .success();

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(
        report["datSnapshot"], "2026-07-11",
        "datSnapshot must be the --now date part, not 'unknown': {}",
        report
    );
    assert_eq!(report["verdict"], "pass", "full report: {}", report);
}

#[test]
fn archive_safety_clean_zip_for_gb_exits_20() {
    // Fix-round-1 item 2: even a structurally clean zip must hard-fail for a
    // non-disc system — ZIP is permitted only for ps1/dc.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();

    let zip_bytes = {
        let mut buf = Vec::new();
        {
            let mut zip = zip::ZipWriter::new(std::io::Cursor::new(&mut buf));
            let opts = zip::write::SimpleFileOptions::default()
                .compression_method(zip::CompressionMethod::Stored);
            zip.start_file("game.gb", opts).unwrap();
            zip.write_all(b"perfectly ordinary rom data").unwrap();
            zip.finish().unwrap();
        }
        buf
    };

    let entry_dir = make_entry(
        tmp.path(),
        "gb",
        System::Gb,
        "sameboy",
        "clean-zip",
        "Clean Zip",
        LicenseType::Cc0,
        &key,
        &zip_bytes,
        "game.zip",
    );

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let arc = checks.iter().find(|c| c["id"] == "ARCHIVE_SAFETY").unwrap();
    assert_eq!(arc["status"], "fail");
    assert!(
        arc["detail"]
            .as_str()
            .unwrap()
            .contains("only for disc systems"),
        "detail should cite the system rule: {}",
        report
    );
}

#[test]
fn digest_mismatch_flags_digests_and_fails_schema_exit_20() {
    // Fix-round-1 item 5: DIGESTS is not in the SPEC hard-fail set — a
    // declared-vs-computed sha256 mismatch flags DIGESTS and hard-fails SCHEMA.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    // Valid-format sha256 that does NOT match the artifact bytes.
    let wrong_sha = hex::encode(Sha256::digest(b"entirely different bytes"));

    let slug = "digest-lie";
    let entry_dir = tmp.path().join("wasm4").join(slug);
    fs::create_dir_all(&entry_dir).unwrap();
    fs::write(entry_dir.join("cart.wasm"), &artifact).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let wallet = wallet_from_key(&key);
    // Message consistent with the DECLARED (wrong) sha so ATTESTATION itself
    // passes; the digest lie must be caught by SCHEMA.
    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title: "Digest Lie",
        system: System::Wasm4,
        sha256: &wrong_sha,
        license: LicenseType::Cc0,
        wallet: &wallet,
        timestamp: signed_at,
    });
    let sig = sign_str(&key, &message);

    let metadata = serde_json::json!({
        "id": format!("wasm4/{}", slug),
        "title": "Digest Lie",
        "system": "wasm4",
        "coreId": "wasm4",
        "minCoreVersion": "0.1.0",
        "sha256": wrong_sha,
        "sizeBytes": artifact.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": "CC0", "evidenceUrl": "https://example.com/cc0" },
        "submission": { "via": "pr", "submittedAt": "2026-07-12T11:00:00Z" },
        "artifact": { "sha256": wrong_sha, "sizeBytes": artifact.len() as u64, "filename": "cart.wasm" }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator", "message": message, "signature": sig,
        "signedAt": signed_at, "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    let digests = checks.iter().find(|c| c["id"] == "DIGESTS").unwrap();
    assert_eq!(
        digests["status"], "flag",
        "DIGESTS mismatch must flag, not fail: {}",
        report
    );
    let schema = checks.iter().find(|c| c["id"] == "SCHEMA").unwrap();
    assert_eq!(
        schema["status"], "fail",
        "SCHEMA must carry the digest-mismatch hard-fail: {}",
        report
    );
}

#[test]
fn attestation_replay_diverging_metadata_sha_fails_schema_exit_20() {
    // Fix-round-3 item 1 — the replay case: the signed §3 attestation message
    // is rebuilt from the TOP-LEVEL metadata.sha256, while SCHEMA pins
    // artifact.sha256 == computed digest. Without pinning
    // metadata.sha256 == artifact.sha256, a submitter can take a creator's
    // existing PUBLIC attestation (message + signature over the original
    // sha256) verbatim, keep the original sha256 in metadata.sha256 so the
    // message still verifies, and attach arbitrary NEW artifact bytes with a
    // matching artifact.sha256 — smuggling unvetted bytes past both
    // ATTESTATION and SCHEMA. The three-way pin must hard-fail this.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();

    // New artifact bytes actually attached to the entry.
    let artifact = make_valid_wasm4_cart();
    let artifact_sha = hex::encode(Sha256::digest(&artifact));
    // The ORIGINAL sha256 the replayed attestation was signed over.
    let original_sha = hex::encode(Sha256::digest(b"the creator's original artifact"));
    assert_ne!(artifact_sha, original_sha);

    let slug = "replayed";
    let entry_dir = tmp.path().join("wasm4").join(slug);
    fs::create_dir_all(&entry_dir).unwrap();
    fs::write(entry_dir.join("cart.wasm"), &artifact).unwrap();

    let signed_at = "2026-07-12T10:00:00Z";
    let wallet = wallet_from_key(&key);
    // The replayed (valid!) attestation: message over the ORIGINAL sha256.
    let message = build_attestation_message(&AttestationParams {
        role: AttestationRole::Creator,
        title: "Replayed",
        system: System::Wasm4,
        sha256: &original_sha,
        license: LicenseType::Cc0,
        wallet: &wallet,
        timestamp: signed_at,
    });
    let sig = sign_str(&key, &message);

    let metadata = serde_json::json!({
        "id": format!("wasm4/{}", slug),
        "title": "Replayed",
        "system": "wasm4",
        "coreId": "wasm4",
        "minCoreVersion": "0.1.0",
        // Top-level sha256 kept at the ORIGINAL value so the replayed
        // message rebuild + signature verify cleanly.
        "sha256": original_sha,
        "sizeBytes": artifact.len() as u64,
        "creator": { "wallet": wallet, "displayName": "Tester" },
        "license": { "type": "CC0", "evidenceUrl": "https://example.com/cc0" },
        "submission": { "via": "pr", "submittedAt": "2026-07-12T11:00:00Z" },
        // artifact.sha256 matches the NEW bytes, so DIGESTS and the
        // computed-digest SCHEMA comparison alone would both be happy.
        "artifact": { "sha256": artifact_sha, "sizeBytes": artifact.len() as u64, "filename": "cart.wasm" }
    });
    fs::write(
        entry_dir.join("metadata.json"),
        serde_json::to_string_pretty(&metadata).unwrap(),
    )
    .unwrap();

    let attestation = serde_json::json!({
        "role": "creator", "message": message, "signature": sig,
        "signedAt": signed_at, "wallet": wallet,
    });
    fs::write(
        entry_dir.join("attestation.json"),
        serde_json::to_string_pretty(&attestation).unwrap(),
    )
    .unwrap();

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(20);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "fail");
    let checks = report["checks"].as_array().unwrap();
    // The replayed attestation itself verifies — proving SCHEMA's three-way
    // pin is the guard that catches this attack.
    let attest = checks.iter().find(|c| c["id"] == "ATTESTATION").unwrap();
    assert_eq!(attest["status"], "pass", "full report: {}", report);
    let digests = checks.iter().find(|c| c["id"] == "DIGESTS").unwrap();
    assert_eq!(digests["status"], "pass", "full report: {}", report);
    let schema = checks.iter().find(|c| c["id"] == "SCHEMA").unwrap();
    assert_eq!(schema["status"], "fail", "full report: {}", report);
    assert!(
        schema["detail"]
            .as_str()
            .unwrap()
            .contains("does not match artifact.sha256"),
        "detail must name the divergence: {}",
        report
    );
}

#[test]
fn nonexistent_denylist_path_exits_2_naming_path() {
    // Fix-round-3 item 2: a typo'd --denylist path must be a loud tool error
    // (exit 2), never an empty-DB all-pass run.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "fine-game",
        "Fine Game",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    let bogus = tmp.path().join("no-such-dir").join("denylist.sqlite");

    let output = Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &bogus.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
        ])
        .output()
        .unwrap();

    assert_eq!(output.status.code(), Some(2), "must exit 2 (tool error)");
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(
        stderr.contains(&*bogus.to_string_lossy()),
        "stderr must name the denylist path: {stderr}"
    );
    // The read-only open must not have created a file at the typo'd path.
    assert!(!bogus.exists(), "open must not create the denylist file");
}

#[test]
fn tableless_denylist_exits_2() {
    // Fix-round-3 item 2: an existing sqlite file without the SPEC §5.1
    // tables (hashes/names/meta) is a tool error, exit 2.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = make_valid_wasm4_cart();
    let entry_dir = make_entry(
        tmp.path(),
        "wasm4",
        System::Wasm4,
        "wasm4",
        "fine-game-2",
        "Fine Game Two",
        LicenseType::Cc0,
        &key,
        &artifact,
        "cart.wasm",
    );

    // Real sqlite file, wrong schema.
    let tableless = tmp.path().join("tableless.sqlite");
    {
        let conn = rusqlite::Connection::open(&tableless).unwrap();
        conn.execute_batch("CREATE TABLE unrelated(x INTEGER);")
            .unwrap();
    }

    let output = Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &tableless.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
        ])
        .output()
        .unwrap();

    assert_eq!(output.status.code(), Some(2), "must exit 2 (tool error)");
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(
        stderr.contains("missing required table"),
        "stderr must name the schema problem: {stderr}"
    );
}

#[test]
fn n64_not_implemented_hdr_flags_and_exits_10() {
    // Fix-round-2 item 1: systems without structural HDR_VALID validation
    // must always reach human review — an otherwise-clean n64 entry exits 10.
    let tmp = TempDir::new().unwrap();
    let key = make_signing_key();
    let artifact = b"plain homebrew payload bytes with no markers".to_vec();

    let entry_dir = make_entry(
        tmp.path(),
        "n64",
        System::N64,
        "mupen64plus_next",
        "orbit-demo",
        "Orbit Demo",
        LicenseType::Cc0,
        &key,
        &artifact,
        "demo.z64",
    );

    let denylist = tmp.path().join("denylist.sqlite");
    make_empty_denylist(&denylist);
    let out = tmp.path().join("report.json");

    Command::cargo_bin("plemu-screen")
        .unwrap()
        .args([
            "--entry",
            &entry_dir.to_string_lossy(),
            "--denylist",
            &denylist.to_string_lossy(),
            "--now",
            "2026-07-12T12:00:00Z",
            "--out",
            &out.to_string_lossy(),
        ])
        .assert()
        .failure()
        .code(10);

    let report: serde_json::Value =
        serde_json::from_str(&fs::read_to_string(&out).unwrap()).unwrap();
    assert_eq!(report["verdict"], "review", "full report: {}", report);
    let checks = report["checks"].as_array().unwrap();
    let hdr = checks.iter().find(|c| c["id"] == "HDR_VALID").unwrap();
    assert_eq!(hdr["status"], "flag", "full report: {}", report);
    assert!(
        hdr["detail"].as_str().unwrap().contains("not_implemented"),
        "detail must contain not_implemented: {}",
        report
    );
    // HDR_VALID must be the ONLY flag — proves the entry is otherwise clean
    // and the exit-10 verdict comes from this check.
    let flagged: Vec<&str> = checks
        .iter()
        .filter(|c| c["status"] == "flag")
        .map(|c| c["id"].as_str().unwrap())
        .collect();
    assert_eq!(flagged, vec!["HDR_VALID"], "full report: {}", report);
}

#[test]
fn hdr_valid_gb_pass_and_flag() {
    use plemu_schemas::System;
    use plemu_screen::checks::{check_hdr_valid, CheckStatus};

    // Build a valid 32 KiB GB ROM
    let mut rom = vec![0u8; 32768];
    // Boot logo at 0x104
    let logo: [u8; 48] = [
        0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00,
        0x0D, 0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD,
        0xD9, 0x99, 0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB,
        0xB9, 0x33, 0x3E,
    ];
    rom[0x104..0x134].copy_from_slice(&logo);
    // Compute checksum
    let mut x: u8 = 0;
    for &b in &rom[0x134..=0x14C] {
        x = x.wrapping_sub(b).wrapping_sub(1);
    }
    rom[0x14D] = x;

    let result = check_hdr_valid(&rom, System::Gb);
    assert_eq!(
        result.status,
        CheckStatus::Pass,
        "Expected pass: {}",
        result.detail
    );

    // Corrupt the logo
    rom[0x104] ^= 0xFF;
    let result2 = check_hdr_valid(&rom, System::Gb);
    assert_eq!(
        result2.status,
        CheckStatus::Flag,
        "Expected flag: {}",
        result2.detail
    );
}
