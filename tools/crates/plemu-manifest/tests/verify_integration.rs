//! Integration tests for `plemu-manifest` against the shared fixtures in
//! `testdata/` and the dev key registry in `keys/`. SPEC §2.3 + §6.
//!
//! These drive the public library API (`verify_manifest`, `sign_manifest`,
//! `assemble_manifest`, `diff_manifests`) exactly as the CLI does.

use std::path::{Path, PathBuf};

use chrono::{DateTime, Utc};
use ed25519_dalek::SigningKey;
use plemu_manifest::build::{
    assemble_manifest, collect_entries, metadata_to_entry, validate_metadata, Artifact,
    RegistryMetadata, Submission,
};
use plemu_manifest::crypto;
use plemu_manifest::diff::diff_manifests;
use plemu_manifest::sign::sign_manifest;
use plemu_manifest::verify::{verify_manifest, Freshness};
use plemu_schemas::{
    Attestation, AttestationRole, CoreId, Creator, KeyEntry, KeyRegistry, KeyStatus, License,
    LicenseType, Manifest, SigEnvelope, System,
};

const REPO_ROOT: &str = env!("CARGO_MANIFEST_DIR");

/// Resolve a path relative to the repo root (this crate lives at
/// `crates/plemu-manifest`, so `../..` reaches the repo root).
fn repo_path(rel: &str) -> PathBuf {
    Path::new(REPO_ROOT).join("../..").join(rel)
}

fn ts(s: &str) -> DateTime<Utc> {
    DateTime::parse_from_rfc3339(s)
        .expect("valid ts")
        .with_timezone(&Utc)
}

fn read(rel: &str) -> Vec<u8> {
    std::fs::read(repo_path(rel)).expect("read fixture")
}

fn dev_registry() -> KeyRegistry {
    let bytes = read("keys/manifest-signers.json");
    serde_json::from_slice(&bytes).expect("parse dev registry")
}

const DEV_A_SIG_B64: &str =
    "vlsjOsqeyT8/x6un8G/18xN0Uo58J2qvn5s67eBBn71sTwKifhv4qfxpy89Kh19tx8SupGvFcy51w/w5JNLJCA==";

// -----------------------------------------------------------------------------
// 1. Valid fixture verifies and is Fresh at 2026-07-12.
// -----------------------------------------------------------------------------
#[test]
fn valid_fixture_fresh_at_2026() {
    let manifest = read("testdata/sample-manifest/manifest.json");
    let sig = read("testdata/sample-manifest/manifest.sig");
    let registry = dev_registry();

    let verified = verify_manifest(&manifest, &sig, &registry, ts("2026-07-12T00:00:00Z"), None)
        .expect("valid fixture must verify");

    assert_eq!(verified.freshness, Freshness::Fresh);
    assert_eq!(verified.manifest.sequence, 1);
    assert_eq!(verified.manifest.signer_key_id, "plemu-manifest-dev-a");
    assert_eq!(verified.manifest.entries.len(), 2);
}

// -----------------------------------------------------------------------------
// 2. Tampered manifest fails at the signature stage (before parse).
// -----------------------------------------------------------------------------
#[test]
fn tampered_manifest_fails() {
    let manifest = read("testdata/sample-manifest/manifest.tampered.json");
    let sig = read("testdata/sample-manifest/manifest.sig");
    let registry = dev_registry();

    let err = verify_manifest(&manifest, &sig, &registry, ts("2026-07-12T00:00:00Z"), None)
        .expect_err("tampered manifest must fail");

    // Must fail specifically at signature verification, not later parsing.
    assert!(
        matches!(err, plemu_manifest::ManifestError::SignatureInvalid(_)),
        "expected SignatureInvalid, got {err:?}"
    );
}

// -----------------------------------------------------------------------------
// 3. Envelope claiming dev-b but carrying dev-a's signature fails.
// -----------------------------------------------------------------------------
#[test]
fn wrong_key_fails() {
    let manifest = read("testdata/sample-manifest/manifest.json");
    let registry = dev_registry();

    // Same signature bytes, but keyId points at dev-b (whose public key won't
    // validate dev-a's signature).
    let envelope = SigEnvelope {
        key_id: "plemu-manifest-dev-b".to_string(),
        alg: "ed25519".to_string(),
        signature: DEV_A_SIG_B64.to_string(),
        signed_at: "2026-07-12T00:00:00Z".to_string(),
    };
    let sig = serde_json::to_vec(&envelope).unwrap();

    let err = verify_manifest(&manifest, &sig, &registry, ts("2026-07-12T00:00:00Z"), None)
        .expect_err("wrong key must fail");
    assert!(
        matches!(err, plemu_manifest::ManifestError::SignatureInvalid(_)),
        "expected SignatureInvalid, got {err:?}"
    );
}

// -----------------------------------------------------------------------------
// 4. Unknown keyId fails with KeyNotFound.
// -----------------------------------------------------------------------------
#[test]
fn unknown_key_fails() {
    let manifest = read("testdata/sample-manifest/manifest.json");
    let registry = dev_registry();

    let envelope = SigEnvelope {
        key_id: "plemu-manifest-does-not-exist".to_string(),
        alg: "ed25519".to_string(),
        signature: DEV_A_SIG_B64.to_string(),
        signed_at: "2026-07-12T00:00:00Z".to_string(),
    };
    let sig = serde_json::to_vec(&envelope).unwrap();

    let err = verify_manifest(&manifest, &sig, &registry, ts("2026-07-12T00:00:00Z"), None)
        .expect_err("unknown key must fail");
    assert!(
        matches!(err, plemu_manifest::ManifestError::KeyNotFound(_)),
        "expected KeyNotFound, got {err:?}"
    );
}

// -----------------------------------------------------------------------------
// 5. Retired key is rejected even with a valid signature.
// -----------------------------------------------------------------------------
#[test]
fn retired_key_fails() {
    let manifest = read("testdata/sample-manifest/manifest.json");
    let sig = read("testdata/sample-manifest/manifest.sig");

    // Test-only registry: dev-a's real public key but marked retired.
    let registry = KeyRegistry {
        keys: vec![KeyEntry {
            key_id: "plemu-manifest-dev-a".to_string(),
            public_key: "1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c"
                .to_string(),
            status: KeyStatus::Retired,
            valid_from: None,
        }],
    };

    let err = verify_manifest(&manifest, &sig, &registry, ts("2026-07-12T00:00:00Z"), None)
        .expect_err("retired key must be rejected");
    assert!(
        matches!(err, plemu_manifest::ManifestError::KeyStatusRejected { .. }),
        "expected KeyStatusRejected, got {err:?}"
    );
}

// -----------------------------------------------------------------------------
// 6. Freshness matrix against the fixture's expiresAt = 2030-01-01.
// -----------------------------------------------------------------------------
#[test]
fn freshness_matrix() {
    let manifest = read("testdata/sample-manifest/manifest.json");
    let sig = read("testdata/sample-manifest/manifest.sig");
    let registry = dev_registry();

    let cases = [
        ("2029-12-31T00:00:00Z", Freshness::Fresh),
        ("2030-01-03T00:00:00Z", Freshness::Grace), // 3 days past expiry
        ("2030-01-20T00:00:00Z", Freshness::Expired), // 19 days past
    ];

    for (now, expected) in cases {
        let verified = verify_manifest(&manifest, &sig, &registry, ts(now), None)
            .unwrap_or_else(|e| panic!("verify at {now} failed: {e:?}"));
        assert_eq!(verified.freshness, expected, "at now={now}");
    }
}

// -----------------------------------------------------------------------------
// 7. Sign + verify roundtrip with an ephemeral key and temp registry, driving
//    the full six-step `verify_manifest` chain (SPEC §2.3), not crypto helpers.
// -----------------------------------------------------------------------------
#[test]
fn sign_verify_roundtrip() {
    // Ephemeral, deterministic key (test-only seed).
    let signing_key = SigningKey::from_bytes(&[42u8; 32]);
    let pub_hex = crypto::verifying_key_to_hex(&signing_key.verifying_key());

    // Registry pinning the ephemeral key as active.
    let registry = KeyRegistry {
        keys: vec![KeyEntry {
            key_id: "test-ephemeral".to_string(),
            public_key: pub_hex,
            status: KeyStatus::Active,
            valid_from: None,
        }],
    };

    // Positive: a minimal manifest whose signerKeyId matches the envelope
    // keyId. The raw bytes below are exactly what gets signed and verified.
    let manifest_bytes: &[u8] = br#"{
  "schemaVersion": 1,
  "generatedAt": "2026-07-12T00:00:00Z",
  "expiresAt": "2026-07-15T00:00:00Z",
  "sequence": 1,
  "signerKeyId": "test-ephemeral",
  "entries": []
}
"#;

    let envelope = sign_manifest(
        &signing_key,
        manifest_bytes,
        "test-ephemeral",
        "2026-07-12T00:00:00Z",
        Some(&registry),
    )
    .expect("sign");
    assert_eq!(envelope.alg, "ed25519");
    let sig_bytes = serde_json::to_vec(&envelope).expect("serialize envelope");

    let verified = verify_manifest(
        manifest_bytes,
        &sig_bytes,
        &registry,
        ts("2026-07-12T00:00:00Z"),
        Some(1),
    )
    .expect("full-chain verify_manifest must succeed");
    assert_eq!(verified.freshness, Freshness::Fresh);
    assert_eq!(verified.manifest.signer_key_id, "test-ephemeral");
    assert_eq!(verified.manifest.sequence, 1);

    // Negative: sign the fixture manifest (signerKeyId "plemu-manifest-dev-a")
    // with the ephemeral key. Steps 1-3 pass (the signature over the raw bytes
    // is genuinely valid), but step 4's signerKeyId cross-check must reject it.
    let fixture_bytes = read("testdata/sample-manifest/manifest.json");
    let envelope = sign_manifest(
        &signing_key,
        &fixture_bytes,
        "test-ephemeral",
        "2026-07-12T00:00:00Z",
        Some(&registry),
    )
    .expect("sign fixture bytes");
    let sig_bytes = serde_json::to_vec(&envelope).expect("serialize envelope");

    let err = verify_manifest(
        &fixture_bytes,
        &sig_bytes,
        &registry,
        ts("2026-07-12T00:00:00Z"),
        None,
    )
    .expect_err("signerKeyId mismatch must fail");
    assert!(
        matches!(
            err,
            plemu_manifest::ManifestError::SignerKeyIdMismatch { .. }
        ),
        "expected SignerKeyIdMismatch, got {err:?}"
    );
}

// -----------------------------------------------------------------------------
// 8. Build golden: 2 valid entries assemble; illegal core-for-system rejected.
// -----------------------------------------------------------------------------
fn sample_metadata(system: System, core_id: CoreId, slug: &str, sha: &str) -> RegistryMetadata {
    RegistryMetadata {
        id: format!("{}/{}", system.as_str(), slug),
        title: format!("Title {slug}"),
        system,
        core_id,
        min_core_version: "1.0.0".to_string(),
        creator: Creator {
            wallet: "FGUrXnKtsJaXxZwB2NwiJFx2XqH4KmBdpypqgsskcor2".to_string(),
            play_id: None,
            display_name: "dev".to_string(),
        },
        license: License {
            license_type: LicenseType::Cc0,
            evidence_url: "https://example.com/evidence".to_string(),
            evidence_archive_url: None,
        },
        attestation: Attestation {
            role: AttestationRole::Creator,
            message: "m".to_string(),
            signature: "s".to_string(),
            signed_at: "2026-07-12T00:00:00Z".to_string(),
        },
        artwork: Default::default(),
        tags: vec![],
        artifact: Artifact {
            sha256: sha.to_string(),
            size_bytes: 50,
            filename: format!("{slug}.bin"),
        },
        submission: Submission {
            via: "cli".to_string(),
            submitted_at: "2026-07-12T00:00:00Z".to_string(),
        },
    }
}

#[test]
fn metadata_json_is_camel_case() {
    // Regression guard: `metadata.json` (incl. nested artifact/submission) must
    // round-trip through camelCase JSON exactly as SPEC §4 shows. Constructing
    // in-memory would miss a wrong serde rename, so we go through the wire.
    let sha = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";
    let meta = sample_metadata(System::Gb, CoreId::Sameboy, "starforge-demo", sha);
    let json = serde_json::to_value(&meta).expect("serialize metadata");

    // Top-level camelCase.
    assert!(json.get("coreId").is_some(), "coreId key missing");
    assert!(json.get("minCoreVersion").is_some());
    // Nested artifact camelCase.
    let artifact = json.get("artifact").expect("artifact present");
    assert!(
        artifact.get("sizeBytes").is_some(),
        "artifact.sizeBytes missing"
    );
    assert!(
        artifact.get("size_bytes").is_none(),
        "must not use snake_case"
    );
    // Nested submission camelCase.
    let submission = json.get("submission").expect("submission present");
    assert!(
        submission.get("submittedAt").is_some(),
        "submission.submittedAt missing"
    );

    // Round-trip back to the struct.
    let back: RegistryMetadata = serde_json::from_value(json).expect("deserialize metadata");
    validate_metadata(&back, "starforge-demo").expect("round-tripped metadata valid");
}

#[test]
fn build_golden() {
    let sha_a = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";
    let sha_b = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    let m1 = sample_metadata(System::Gb, CoreId::Sameboy, "starforge-demo", sha_a);
    let m2 = sample_metadata(System::Tic80, CoreId::Tic80, "puzzle-quest", sha_b);

    validate_metadata(&m1, "starforge-demo").expect("m1 valid");
    validate_metadata(&m2, "puzzle-quest").expect("m2 valid");

    let base = "https://cdn.plemu.xyz";
    let e1 = metadata_to_entry(m1, base, "2026-07-12T00:00:00Z", None);
    let e2 = metadata_to_entry(m2, base, "2026-07-12T00:00:00Z", None);

    // downloadUrls derived as base/artifacts/<sha256>.
    assert_eq!(e1.download_urls, vec![format!("{base}/artifacts/{sha_a}")]);

    let manifest = assemble_manifest(
        vec![e1, e2],
        ts("2026-07-12T00:00:00Z"),
        "plemu-manifest-dev-a",
        1,
    )
    .expect("assemble");

    assert_eq!(manifest.schema_version, 1);
    assert_eq!(manifest.sequence, 1);
    assert_eq!(manifest.generated_at, "2026-07-12T00:00:00Z");
    // expiresAt = generatedAt + 72h.
    assert_eq!(manifest.expires_at, "2026-07-15T00:00:00Z");
    assert_eq!(manifest.entries.len(), 2);

    // Illegal core-for-system must be rejected: gb with mgba (mgba is gba's core).
    let bad = sample_metadata(System::Gb, CoreId::Mgba, "wrong-core", sha_a);
    let err = validate_metadata(&bad, "wrong-core").expect_err("illegal core must fail");
    assert!(matches!(err, plemu_manifest::ManifestError::Validation(_)));

    // Mismatched id/slug must be rejected.
    let mismatch = sample_metadata(System::Gb, CoreId::Sameboy, "real-slug", sha_a);
    assert!(validate_metadata(&mismatch, "different-slug").is_err());

    // Non-hex sha256 must be rejected.
    let bad_sha = sample_metadata(System::Gb, CoreId::Sameboy, "bad-sha", "not-hex");
    assert!(validate_metadata(&bad_sha, "bad-sha").is_err());
}

// -----------------------------------------------------------------------------
// 9. Diff covering all four buckets.
// -----------------------------------------------------------------------------
fn entry(id: &str, title: &str, sha: &str, revoked: bool) -> plemu_schemas::Entry {
    let (system, slug) = id.split_once('/').unwrap();
    let system: System =
        serde_json::from_value(serde_json::Value::String(system.to_string())).unwrap();
    let core_id = plemu_schemas::legal_core_ids(system)[0];
    let _ = slug;
    plemu_schemas::Entry {
        id: id.to_string(),
        title: title.to_string(),
        system,
        core_id,
        min_core_version: "1.0.0".to_string(),
        sha256: sha.to_string(),
        size_bytes: 1,
        download_urls: vec![],
        creator: Creator {
            wallet: "w".to_string(),
            play_id: None,
            display_name: "d".to_string(),
        },
        license: License {
            license_type: LicenseType::Cc0,
            evidence_url: "u".to_string(),
            evidence_archive_url: None,
        },
        attestation: Attestation {
            role: AttestationRole::Creator,
            message: "m".to_string(),
            signature: "s".to_string(),
            signed_at: "t".to_string(),
        },
        artwork: Default::default(),
        added_at: "2026-07-12T00:00:00Z".to_string(),
        revoked,
        revoked_reason: None,
        revoked_at: None,
        tags: vec![],
    }
}

fn manifest_of(entries: Vec<plemu_schemas::Entry>) -> Manifest {
    Manifest {
        schema_version: 1,
        generated_at: "2026-07-12T00:00:00Z".to_string(),
        expires_at: "2026-07-15T00:00:00Z".to_string(),
        sequence: 1,
        signer_key_id: "k".to_string(),
        entries,
    }
}

#[test]
fn diff_all_buckets() {
    let sha1 = "1111111111111111111111111111111111111111111111111111111111111111";
    let sha2 = "2222222222222222222222222222222222222222222222222222222222222222";

    // old: kept-unchanged, kept-changed-sha, kept-to-revoke, removed-entry
    let old = manifest_of(vec![
        entry("gb/kept", "Kept", sha1, false),
        entry("gb/changed", "Changed", sha1, false),
        entry("gb/torevoke", "ToRevoke", sha1, false),
        entry("gb/removed", "Removed", sha1, false),
    ]);

    // new: kept-unchanged, kept-changed-sha (sha2), kept-to-revoke (revoked), added-entry
    let new = manifest_of(vec![
        entry("gb/kept", "Kept", sha1, false),
        entry("gb/changed", "Changed", sha2, false),
        entry("gb/torevoke", "ToRevoke", sha1, true),
        entry("gb/added", "Added", sha1, false),
    ]);

    let diff = diff_manifests(&old, &new);

    assert_eq!(diff.added, vec!["gb/added".to_string()]);
    assert_eq!(diff.removed, vec!["gb/removed".to_string()]);
    assert_eq!(diff.revoked, vec!["gb/torevoke".to_string()]);

    // changed includes both the sha-changed and the revoked-flipped entries.
    let changed_ids: Vec<&str> = diff.changed.iter().map(|c| c.id.as_str()).collect();
    assert!(changed_ids.contains(&"gb/changed"));
    assert!(changed_ids.contains(&"gb/torevoke"));
    assert!(!changed_ids.contains(&"gb/kept"));

    // The sha-changed entry records from/to sha.
    let changed = diff.changed.iter().find(|c| c.id == "gb/changed").unwrap();
    assert_eq!(changed.sha256_from.as_deref(), Some(sha1));
    assert_eq!(changed.sha256_to.as_deref(), Some(sha2));

    // Wire format: changed[] records serialize camelCase like everything else.
    let json = serde_json::to_value(&diff).unwrap();
    let rec = json["changed"]
        .as_array()
        .unwrap()
        .iter()
        .find(|c| c["id"] == "gb/changed")
        .unwrap();
    assert_eq!(rec["sha256From"], sha1, "expected camelCase sha256From");
    assert_eq!(rec["sha256To"], sha2, "expected camelCase sha256To");
    assert!(
        rec.get("sha256_from").is_none(),
        "must not emit snake_case keys"
    );
    let revoked_rec = json["changed"]
        .as_array()
        .unwrap()
        .iter()
        .find(|c| c["id"] == "gb/torevoke")
        .unwrap();
    assert_eq!(revoked_rec["revokedFrom"], false);
    assert_eq!(revoked_rec["revokedTo"], true);
}

// -----------------------------------------------------------------------------
// 10. Golden: revocation.json sidecar materializes revoked:true in the manifest.
// -----------------------------------------------------------------------------

/// Write a registry entry dir (metadata.json), optionally with a revocation.json.
fn write_entry(
    registry: &Path,
    system: System,
    core_id: CoreId,
    slug: &str,
    sha: &str,
    revocation_json: Option<&str>,
) {
    let dir = registry.join(system.as_str()).join(slug);
    std::fs::create_dir_all(&dir).unwrap();
    let meta = sample_metadata(system, core_id, slug, sha);
    std::fs::write(
        dir.join("metadata.json"),
        serde_json::to_string_pretty(&meta).unwrap(),
    )
    .unwrap();
    if let Some(body) = revocation_json {
        std::fs::write(dir.join("revocation.json"), body).unwrap();
    }
}

#[test]
fn build_honors_revocation_sidecar() {
    let tmp = tempfile::TempDir::new().unwrap();
    let registry = tmp.path();

    let sha_live = "1111111111111111111111111111111111111111111111111111111111111111";
    let sha_revoked = "2222222222222222222222222222222222222222222222222222222222222222";

    // A live entry (no sidecar) and a revoked entry (revocation.json present).
    write_entry(
        registry,
        System::Gb,
        CoreId::Sameboy,
        "live-game",
        sha_live,
        None,
    );
    write_entry(
        registry,
        System::Tic80,
        CoreId::Tic80,
        "revoked-game",
        sha_revoked,
        Some(r#"{ "reason": "dmca", "revokedAt": "2026-07-13T09:30:00Z" }"#),
    );

    let entries = collect_entries(
        registry,
        "https://cdn.plemu.xyz",
        "2026-07-13T00:00:00Z",
        false,
    )
    .expect("collect_entries");
    let manifest = assemble_manifest(
        entries,
        ts("2026-07-13T00:00:00Z"),
        "plemu-manifest-dev-a",
        1,
    )
    .expect("assemble");

    // Both entries are present (sorted by id: gb/live-game before tic80/revoked-game).
    assert_eq!(manifest.entries.len(), 2, "both entries must materialize");

    let live = manifest
        .entries
        .iter()
        .find(|e| e.id == "gb/live-game")
        .expect("live entry present");
    assert!(!live.revoked, "live entry must be revoked:false");
    assert_eq!(live.revoked_reason, None);
    assert_eq!(live.revoked_at, None);

    let revoked = manifest
        .entries
        .iter()
        .find(|e| e.id == "tic80/revoked-game")
        .expect("revoked entry present (still emitted so the device can deny+sweep)");
    assert!(revoked.revoked, "revoked entry must be revoked:true");
    assert_eq!(
        revoked.revoked_reason,
        Some(plemu_schemas::RevokedReason::Dmca)
    );
    assert_eq!(
        revoked.revoked_at.as_deref(),
        Some("2026-07-13T09:30:00Z"),
        "revokedAt must come from the sidecar verbatim"
    );
}

#[test]
fn build_fails_closed_on_malformed_revocation() {
    let tmp = tempfile::TempDir::new().unwrap();
    let registry = tmp.path();
    let sha = "3333333333333333333333333333333333333333333333333333333333333333";

    // Unknown reason value -> RevokedReason parse failure -> hard build error.
    write_entry(
        registry,
        System::Gb,
        CoreId::Sameboy,
        "bad-reason",
        sha,
        Some(r#"{ "reason": "not-a-real-reason", "revokedAt": "2026-07-13T09:30:00Z" }"#),
    );
    let err = collect_entries(
        registry,
        "https://cdn.plemu.xyz",
        "2026-07-13T00:00:00Z",
        false,
    )
    .expect_err("malformed revocation reason must fail the build");
    let msg = format!("{err}");
    assert!(
        msg.contains("bad-reason") && msg.contains("revocation.json"),
        "error must name the entry + file: {msg}"
    );

    // Non-ISO revokedAt -> timestamp validation failure -> hard build error.
    let tmp2 = tempfile::TempDir::new().unwrap();
    let registry2 = tmp2.path();
    write_entry(
        registry2,
        System::Gb,
        CoreId::Sameboy,
        "bad-time",
        sha,
        Some(r#"{ "reason": "malware", "revokedAt": "yesterday" }"#),
    );
    let err2 = collect_entries(
        registry2,
        "https://cdn.plemu.xyz",
        "2026-07-13T00:00:00Z",
        false,
    )
    .expect_err("non-ISO revokedAt must fail the build");
    assert!(
        format!("{err2}").contains("revokedAt"),
        "error must name the bad field: {err2}"
    );
}

// -----------------------------------------------------------------------------
// 13. Two-tier model: --tier beta builds from registry/beta; --tier main builds
//     from registry/main. SPEC §4 M2 / CLI SPEC §5.
//
// This exercises the same `collect_entries` library call that the CLI uses when
// `--tier` resolves the registry root to `<root>/<tier>`.  We don't invoke the
// CLI binary here — that would require a PATH setup — but the tier routing in
// the CLI is a thin wrapper around exactly this call, so coverage is equivalent.
// -----------------------------------------------------------------------------
#[test]
fn tier_beta_builds_from_beta_subdir() {
    let tmp = tempfile::TempDir::new().unwrap();
    let root = tmp.path();

    // Create a two-tier registry layout mirroring SPEC §4 M2.
    let beta_dir = root.join("beta");
    let main_dir = root.join("main");
    std::fs::create_dir_all(&beta_dir).unwrap();
    std::fs::create_dir_all(&main_dir).unwrap();
    // main is empty (just the keeper); main build must succeed with 0 entries.
    std::fs::write(main_dir.join(".gitkeep"), "").unwrap();

    // Populate beta with one wasm4 entry.
    let sha = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";
    write_entry(
        &beta_dir,
        plemu_schemas::System::Wasm4,
        plemu_schemas::CoreId::Wasm4,
        "hello-plemu",
        sha,
        None,
    );

    // Build from the beta subdir — must contain the beta entry.
    let beta_entries = collect_entries(
        &beta_dir,
        "https://cdn.plemu.xyz",
        "2026-07-13T00:00:00Z",
        false,
    )
    .expect("collect beta entries");
    assert_eq!(beta_entries.len(), 1, "beta must have exactly 1 entry");
    assert_eq!(beta_entries[0].id, "wasm4/hello-plemu");
    assert!(!beta_entries[0].revoked, "beta entry must be live");

    // Build from the main subdir — must be empty (no entries promoted yet).
    let main_entries = collect_entries(
        &main_dir,
        "https://cdn.plemu.xyz",
        "2026-07-13T00:00:00Z",
        true,
    )
    .expect("collect main entries (empty is valid)");
    assert_eq!(
        main_entries.len(),
        0,
        "main starts empty; .gitkeep is ignored"
    );

    // Both tier dirs must assemble into valid manifests.
    let beta_manifest = assemble_manifest(
        beta_entries,
        ts("2026-07-13T00:00:00Z"),
        "plemu-manifest-dev-a",
        1,
    )
    .expect("assemble beta manifest");
    assert_eq!(beta_manifest.entries.len(), 1);

    let main_manifest = assemble_manifest(
        main_entries,
        ts("2026-07-13T00:00:00Z"),
        "plemu-manifest-dev-a",
        1,
    )
    .expect("assemble main manifest (zero entries allowed)");
    assert_eq!(main_manifest.entries.len(), 0);
}

#[test]
fn build_without_tier_on_a_tiered_registry_fails_loudly() {
    let tmp = tempfile::TempDir::new().unwrap();
    let root = tmp.path();

    let beta_dir = root.join("beta");
    std::fs::create_dir_all(&beta_dir).unwrap();
    write_entry(
        &beta_dir,
        plemu_schemas::System::Wasm4,
        plemu_schemas::CoreId::Wasm4,
        "hello-plemu",
        "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd",
        None,
    );

    // Building root without tier must fail closed because it detects beta/ subdirectory
    let err = plemu_manifest::build_manifest(
        root,
        None,
        "https://cdn.plemu.xyz",
        "plemu-manifest-dev-a",
        None,
        None,
        false,
    )
    .unwrap_err();

    assert!(
        matches!(err, plemu_manifest::ManifestError::Validation(ref m) if m.contains("tiered subdirectories")),
        "expected error about tiered subdirectories, got: {err:?}"
    );
}

#[test]
fn build_empty_directory_without_allow_empty_fails() {
    let tmp = tempfile::TempDir::new().unwrap();
    let empty_dir = tmp.path().join("empty_sys");
    std::fs::create_dir_all(&empty_dir).unwrap();

    let err = plemu_manifest::build_manifest(
        &empty_dir,
        None,
        "https://cdn.plemu.xyz",
        "plemu-manifest-dev-a",
        None,
        None,
        false,
    )
    .unwrap_err();

    assert!(
        matches!(err, plemu_manifest::ManifestError::Validation(ref m) if m.contains("no entries found")),
        "expected error about no entries found, got: {err:?}"
    );

    // With allow_empty = true, it should succeed with 0 entries
    let manifest = plemu_manifest::build_manifest(
        &empty_dir,
        None,
        "https://cdn.plemu.xyz",
        "plemu-manifest-dev-a",
        None,
        None,
        true,
    )
    .expect("allow_empty must permit empty manifest");

    assert_eq!(manifest.entries.len(), 0);
}

#[test]
fn git_first_commit_date_returns_timestamp_for_existing_git_repo() {
    let beta_dir = Path::new(REPO_ROOT).join("../../..").join("registry/beta");
    let sentinel = ts("1999-01-01T00:00:00Z");
    let manifest = plemu_manifest::build_manifest(
        &beta_dir,
        Some("beta"),
        "https://cdn.plemu.xyz",
        "plemu-manifest-dev-a",
        None,
        Some(sentinel),
        false,
    )
    .expect("build real beta registry");

    let libbet = manifest
        .entries
        .iter()
        .find(|e| e.id == "gb/libbet")
        .expect("libbet exists");
    // Libbet was committed to git; its addedAt should be a valid RFC 3339 timestamp
    assert!(!libbet.added_at.is_empty());
    assert!(chrono::DateTime::parse_from_rfc3339(&libbet.added_at).is_ok());
    assert_ne!(libbet.added_at, plemu_manifest::build::format_utc(sentinel));
}
