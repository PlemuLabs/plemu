//! SQLite denylist builder.

use crate::parser::{DatRecord, NameRecord};
use rusqlite::{params, Connection, Result as SqlResult};

/// Build metadata recorded in the SPEC §5.1 `meta` table.
#[derive(Debug, Clone)]
pub struct DenylistMeta {
    /// Build timestamp, ISO 8601 Z (from `--now` or the system clock).
    pub built_at: String,
    /// Number of DAT files ingested.
    pub dat_files: u64,
}

/// Build a SQLite denylist database at `path` from the given records.
///
/// Creates three tables per SPEC §5.1:
/// - `hashes(algo TEXT, hex TEXT, source TEXT, rom_name TEXT, game_name TEXT, system TEXT)`
/// - `names(normalized TEXT, game_name TEXT, system TEXT)`
/// - `meta(key TEXT PRIMARY KEY, value TEXT)` — `built_at`, `dat_files`,
///   `hash_rows`, `name_rows`
pub fn build_denylist(
    path: &str,
    hash_records: &[DatRecord],
    name_records: &[NameRecord],
    meta: &DenylistMeta,
) -> SqlResult<()> {
    let conn = Connection::open(path)?;
    setup_schema(&conn)?;
    insert_hashes(&conn, hash_records)?;
    insert_names(&conn, name_records)?;
    write_meta(&conn, meta)?;
    Ok(())
}

/// Build an in-memory SQLite denylist from the given records.
pub fn build_denylist_in_memory(
    hash_records: &[DatRecord],
    name_records: &[NameRecord],
) -> SqlResult<Connection> {
    let conn = Connection::open_in_memory()?;
    setup_schema(&conn)?;
    insert_hashes(&conn, hash_records)?;
    insert_names(&conn, name_records)?;
    write_meta(
        &conn,
        &DenylistMeta {
            built_at: chrono::Utc::now().to_rfc3339_opts(chrono::SecondsFormat::Secs, true),
            dat_files: 0,
        },
    )?;
    Ok(conn)
}

fn setup_schema(conn: &Connection) -> SqlResult<()> {
    conn.execute_batch(
        "CREATE TABLE IF NOT EXISTS hashes (
            algo      TEXT NOT NULL,
            hex       TEXT NOT NULL,
            source    TEXT,
            rom_name  TEXT,
            game_name TEXT,
            system    TEXT,
            PRIMARY KEY (algo, hex)
        );
        CREATE TABLE IF NOT EXISTS names (
            normalized TEXT NOT NULL PRIMARY KEY,
            game_name  TEXT,
            system     TEXT
        );
        CREATE TABLE IF NOT EXISTS meta (
            key   TEXT PRIMARY KEY,
            value TEXT
        );",
    )
}

/// Populate the SPEC §5.1 `meta` table. Row counts reflect the actual table
/// contents after dedup (INSERT OR IGNORE), not the input record counts.
fn write_meta(conn: &Connection, meta: &DenylistMeta) -> SqlResult<()> {
    let hash_rows: i64 = conn.query_row("SELECT COUNT(*) FROM hashes", [], |r| r.get(0))?;
    let name_rows: i64 = conn.query_row("SELECT COUNT(*) FROM names", [], |r| r.get(0))?;

    let mut stmt = conn.prepare("INSERT OR REPLACE INTO meta (key, value) VALUES (?1, ?2)")?;
    stmt.execute(params!["built_at", meta.built_at])?;
    stmt.execute(params!["dat_files", meta.dat_files.to_string()])?;
    stmt.execute(params!["hash_rows", hash_rows.to_string()])?;
    stmt.execute(params!["name_rows", name_rows.to_string()])?;
    Ok(())
}

fn insert_hashes(conn: &Connection, records: &[DatRecord]) -> SqlResult<()> {
    let mut stmt = conn.prepare(
        "INSERT OR IGNORE INTO hashes (algo, hex, source, rom_name, game_name, system)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
    )?;
    for r in records {
        stmt.execute(params![
            r.algo,
            r.hex,
            r.source,
            r.rom_name,
            r.game_name,
            r.system,
        ])?;
    }
    Ok(())
}

fn insert_names(conn: &Connection, records: &[NameRecord]) -> SqlResult<()> {
    let mut stmt = conn.prepare(
        "INSERT OR IGNORE INTO names (normalized, game_name, system)
         VALUES (?1, ?2, ?3)",
    )?;
    for r in records {
        stmt.execute(params![r.normalized, r.game_name, r.system])?;
    }
    Ok(())
}

/// Check if a hash exists in the denylist.
pub fn is_hash_denied(conn: &Connection, algo: &str, hex: &str) -> SqlResult<bool> {
    let count: i64 = conn.query_row(
        "SELECT COUNT(*) FROM hashes WHERE algo = ?1 AND hex = ?2",
        params![algo, hex],
        |row| row.get(0),
    )?;
    Ok(count > 0)
}

/// Check if a normalized name exists in the denylist.
pub fn is_name_denied(conn: &Connection, normalized: &str) -> SqlResult<bool> {
    let count: i64 = conn.query_row(
        "SELECT COUNT(*) FROM names WHERE normalized = ?1",
        params![normalized],
        |row| row.get(0),
    )?;
    Ok(count > 0)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::parser::{DatRecord, NameRecord};

    fn make_hash(algo: &str, hex: &str) -> DatRecord {
        DatRecord {
            algo: algo.to_string(),
            hex: hex.to_string(),
            source: Some("test".to_string()),
            rom_name: None,
            game_name: Some("Test Game".to_string()),
            system: Some("gb".to_string()),
        }
    }

    fn make_name(normalized: &str) -> NameRecord {
        NameRecord {
            normalized: normalized.to_string(),
            game_name: Some("Test Game".to_string()),
            system: Some("gb".to_string()),
        }
    }

    #[test]
    fn build_and_query_in_memory() {
        let hashes = vec![make_hash("crc32", "deadbeef"), make_hash("sha1", "aabbcc")];
        let names = vec![make_name("super mario land")];

        let conn = build_denylist_in_memory(&hashes, &names).unwrap();

        assert!(is_hash_denied(&conn, "crc32", "deadbeef").unwrap());
        assert!(!is_hash_denied(&conn, "crc32", "00000000").unwrap());
        assert!(is_name_denied(&conn, "super mario land").unwrap());
        assert!(!is_name_denied(&conn, "unknown game").unwrap());
    }

    #[test]
    fn duplicate_hashes_ignored() {
        let hashes = vec![
            make_hash("crc32", "deadbeef"),
            make_hash("crc32", "deadbeef"), // duplicate
        ];
        let conn = build_denylist_in_memory(&hashes, &[]).unwrap();
        let count: i64 = conn
            .query_row("SELECT COUNT(*) FROM hashes", [], |r| r.get(0))
            .unwrap();
        assert_eq!(count, 1);
    }

    fn read_meta(conn: &Connection, key: &str) -> String {
        conn.query_row("SELECT value FROM meta WHERE key = ?1", params![key], |r| {
            r.get(0)
        })
        .unwrap()
    }

    #[test]
    fn build_denylist_populates_meta() {
        let dir = tempfile::TempDir::new().unwrap();
        let db_path = dir.path().join("denylist.sqlite");
        let hashes = vec![
            make_hash("crc32", "deadbeef"),
            make_hash("crc32", "deadbeef"), // duplicate: hash_rows must count post-dedup
            make_hash("sha256", "aa11"),
        ];
        let names = vec![make_name("super mario land")];
        let meta = DenylistMeta {
            built_at: "2026-07-12T00:00:00Z".to_string(),
            dat_files: 3,
        };

        build_denylist(&db_path.to_string_lossy(), &hashes, &names, &meta).unwrap();

        let conn = Connection::open(&db_path).unwrap();
        assert_eq!(read_meta(&conn, "built_at"), "2026-07-12T00:00:00Z");
        assert_eq!(read_meta(&conn, "dat_files"), "3");
        assert_eq!(read_meta(&conn, "hash_rows"), "2");
        assert_eq!(read_meta(&conn, "name_rows"), "1");
    }

    #[test]
    fn in_memory_denylist_has_meta_table() {
        let conn = build_denylist_in_memory(&[make_hash("sha1", "bb22")], &[]).unwrap();
        // built_at present (system clock) and row counts correct
        assert!(!read_meta(&conn, "built_at").is_empty());
        assert_eq!(read_meta(&conn, "hash_rows"), "1");
        assert_eq!(read_meta(&conn, "name_rows"), "0");
    }
}
