//! JSON schema generation + drift guard.
//!
//! `schemas/manifest.schema.json` and `schemas/manifest-entry.schema.json` are
//! generated from the Rust types and committed. This test regenerates them and
//! asserts byte-for-byte equality, so schema drift breaks CI.
//!
//! To (re)write the committed files after an intentional type change:
//!   `PLEMU_REGEN_SCHEMAS=1 cargo test -p plemu-schemas --test schema_regen`

use std::path::{Path, PathBuf};

use plemu_schemas::schema::{entry_schema_json, manifest_schema_json};

fn schemas_dir() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../../schemas")
}

fn check_or_regen(rel: &str, generated: &str) {
    let path = schemas_dir().join(rel);

    if std::env::var("PLEMU_REGEN_SCHEMAS").is_ok() {
        std::fs::create_dir_all(schemas_dir()).expect("create schemas dir");
        std::fs::write(&path, generated).expect("write schema");
        return;
    }

    let committed = std::fs::read_to_string(&path).unwrap_or_else(|e| {
        panic!(
            "missing committed schema {path:?} ({e}); run with PLEMU_REGEN_SCHEMAS=1 to create it"
        )
    });

    assert_eq!(
        committed, generated,
        "schema {rel} is stale; run PLEMU_REGEN_SCHEMAS=1 cargo test -p plemu-schemas --test schema_regen"
    );
}

#[test]
fn manifest_schema_matches_committed() {
    let generated = manifest_schema_json().expect("generate manifest schema");
    check_or_regen("manifest.schema.json", &generated);
}

#[test]
fn entry_schema_matches_committed() {
    let generated = entry_schema_json().expect("generate entry schema");
    check_or_regen("manifest-entry.schema.json", &generated);
}
