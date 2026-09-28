//! Integration test: real `plemu-dat build` CLI populates the SPEC §5.1
//! `meta` table, honoring `--now`.

use assert_cmd::Command;
use std::fs;
use tempfile::TempDir;

const TINY_DAT: &str = r#"<?xml version="1.0"?>
<datafile>
  <game name="Super Mario Land (World)">
    <rom name="Super Mario Land (World).gb" size="65536" crc="0e7ce1cb" md5="4e1a2be0f328ac2bfb5e9ab03c7a1fcf" sha1="7c15643afd0f2d16ccdc6e20e37a57d5c2ba12a4"/>
  </game>
  <game name="Kirby's Dream Land (USA, Europe)">
    <rom name="Kirby's Dream Land (USA, Europe).gb" size="131072" crc="f7d86d95" sha1="01f04dd450fb8a5c4da04804b58e23a2ecddc9cb"/>
  </game>
</datafile>"#;

fn read_meta(conn: &rusqlite::Connection, key: &str) -> String {
    conn.query_row(
        "SELECT value FROM meta WHERE key = ?1",
        rusqlite::params![key],
        |r| r.get(0),
    )
    .unwrap()
}

#[test]
fn cli_build_populates_meta_with_now_override() {
    let tmp = TempDir::new().unwrap();
    let dat_dir = tmp.path().join("dats");
    fs::create_dir_all(&dat_dir).unwrap();
    fs::write(dat_dir.join("gb.dat"), TINY_DAT).unwrap();
    let out_db = tmp.path().join("denylist.sqlite");

    Command::cargo_bin("plemu-dat")
        .unwrap()
        .args([
            "build",
            "--input",
            &dat_dir.to_string_lossy(),
            "--out",
            &out_db.to_string_lossy(),
            "--now",
            "2026-07-11T09:30:00Z",
        ])
        .assert()
        .success();

    let conn = rusqlite::Connection::open(&out_db).unwrap();
    assert_eq!(read_meta(&conn, "built_at"), "2026-07-11T09:30:00Z");
    assert_eq!(read_meta(&conn, "dat_files"), "1");
    // Mario: crc32+md5+sha1 = 3 rows; Kirby: crc32+sha1 = 2 rows
    assert_eq!(read_meta(&conn, "hash_rows"), "5");
    assert_eq!(read_meta(&conn, "name_rows"), "2");
}

#[test]
fn cli_build_defaults_built_at_to_system_clock() {
    let tmp = TempDir::new().unwrap();
    let dat_dir = tmp.path().join("dats");
    fs::create_dir_all(&dat_dir).unwrap();
    fs::write(dat_dir.join("gb.dat"), TINY_DAT).unwrap();
    let out_db = tmp.path().join("denylist.sqlite");

    Command::cargo_bin("plemu-dat")
        .unwrap()
        .args([
            "build",
            "--input",
            &dat_dir.to_string_lossy(),
            "--out",
            &out_db.to_string_lossy(),
        ])
        .assert()
        .success();

    let conn = rusqlite::Connection::open(&out_db).unwrap();
    let built_at = read_meta(&conn, "built_at");
    // ISO 8601 Z from the system clock, parseable
    assert!(built_at.ends_with('Z'), "built_at not Z-form: {built_at}");
    assert!(chrono::DateTime::parse_from_rfc3339(&built_at).is_ok());
}

#[test]
fn cli_build_rejects_invalid_now() {
    let tmp = TempDir::new().unwrap();
    let dat_dir = tmp.path().join("dats");
    fs::create_dir_all(&dat_dir).unwrap();
    fs::write(dat_dir.join("gb.dat"), TINY_DAT).unwrap();
    let out_db = tmp.path().join("denylist.sqlite");

    Command::cargo_bin("plemu-dat")
        .unwrap()
        .args([
            "build",
            "--input",
            &dat_dir.to_string_lossy(),
            "--out",
            &out_db.to_string_lossy(),
            "--now",
            "not-a-timestamp",
        ])
        .assert()
        .failure();
}
