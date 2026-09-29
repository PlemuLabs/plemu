//! Manifest building from a registry tree. SPEC §4 + §5 (`plemu-manifest build`).
//!
//! Walks `registry/<system>/<slug>/`, reads each `metadata.json`
//! (+ `attestation.json`), validates it against SPEC §1.3/§1.6, derives the
//! computed fields (`downloadUrls`, `addedAt`, freshness window), and assembles
//! a signed-ready [`Manifest`]. Signing is a separate step.

use std::path::Path;

use chrono::{DateTime, Duration, Utc};
use plemu_schemas::{
    is_core_legal_for_system, Artwork, Attestation, CoreId, Creator, Entry, License, Manifest,
    RevokedReason, System, SCHEMA_VERSION,
};
use serde::{Deserialize, Serialize};

use crate::error::{ManifestError, Result};

/// Production freshness window: `expiresAt = generatedAt + 72h`. SPEC §1.2.
const EXPIRY_HOURS: i64 = 72;

/// The optional `revocation.json` sidecar in an entry dir. SPEC §4.
///
/// Present ONLY when the entry is revoked. Keeping revocation OUT of
/// `metadata.json` preserves the SPEC §4 invariant that `metadata.json` is the
/// `Entry` minus `revoked*`; a revoked entry is explicit and greppable (the file
/// either exists or it does not), and `build` fails closed on a malformed one so
/// a broken revocation can never silently produce a live manifest entry.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Revocation {
    /// Why the entry was revoked (`dmca|license-dispute|malware|creator-request`).
    pub reason: RevokedReason,
    /// When it was revoked, ISO 8601 UTC `YYYY-MM-DDTHH:MM:SSZ`.
    pub revoked_at: String,
}

/// Read the optional `revocation.json` sidecar from an entry directory.
///
/// Returns `Ok(None)` when the file is absent (the entry is live). When the file
/// exists it MUST parse as `{reason, revokedAt}` with `reason` a valid
/// [`RevokedReason`] and `revokedAt` a well-formed ISO 8601 UTC timestamp; any
/// deviation is a hard error naming the entry (fail closed). `entry_id` is used
/// only for error messages.
pub fn read_revocation(entry_dir: &Path, entry_id: &str) -> Result<Option<Revocation>> {
    let path = entry_dir.join("revocation.json");
    if !path.exists() {
        return Ok(None);
    }
    let bytes = std::fs::read(&path)?;
    let revocation: Revocation = serde_json::from_slice(&bytes).map_err(|e| {
        ManifestError::Validation(format!(
            "entry {entry_id:?}: malformed revocation.json: {e}"
        ))
    })?;
    // Validate the timestamp shape so a garbage `revokedAt` cannot ship.
    crate::verify::parse_timestamp(&revocation.revoked_at).map_err(|e| {
        ManifestError::Validation(format!(
            "entry {entry_id:?}: revocation.json revokedAt is not ISO 8601 UTC: {e}"
        ))
    })?;
    Ok(Some(revocation))
}

/// On-disk artifact descriptor inside `metadata.json`. SPEC §4.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Artifact {
    /// 64 lowercase hex.
    pub sha256: String,
    pub size_bytes: u64,
    pub filename: String,
}

/// On-disk submission provenance inside `metadata.json`. SPEC §4.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Submission {
    /// `"portal"` | `"cli"`.
    pub via: String,
    pub submitted_at: String,
}

/// The registry `metadata.json` shape: an [`Entry`] minus computed fields
/// (`downloadUrls`, `addedAt`, `revoked*`) plus `artifact` and `submission`.
/// SPEC §4.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegistryMetadata {
    pub id: String,
    pub title: String,
    pub system: System,
    pub core_id: CoreId,
    pub min_core_version: String,
    pub creator: Creator,
    pub license: License,
    pub attestation: Attestation,
    #[serde(default)]
    pub artwork: Artwork,
    #[serde(default)]
    pub tags: Vec<String>,
    pub artifact: Artifact,
    pub submission: Submission,
}

/// Validate one metadata record against SPEC §1.3 / §1.6.
///
/// Checks performed:
/// - `id == "<system>/<dir_slug>"`,
/// - slug matches `^[a-z0-9]+(-[a-z0-9]+)*$`,
/// - `coreId` legal for `system`,
/// - `sha256` is 64 lowercase hex.
pub fn validate_metadata(meta: &RegistryMetadata, dir_slug: &str) -> Result<()> {
    let expected_id = format!("{}/{}", meta.system.as_str(), dir_slug);
    if meta.id != expected_id {
        return Err(ManifestError::Validation(format!(
            "id {:?} does not match derived id {:?}",
            meta.id, expected_id
        )));
    }

    if !is_valid_slug(dir_slug) {
        return Err(ManifestError::Validation(format!(
            "slug {dir_slug:?} does not match ^[a-z0-9]+(-[a-z0-9]+)*$"
        )));
    }

    if !is_core_legal_for_system(meta.system, meta.core_id) {
        return Err(ManifestError::Validation(format!(
            "coreId {} is not legal for system {}",
            meta.core_id, meta.system
        )));
    }

    if !is_lower_hex_64(&meta.artifact.sha256) {
        return Err(ManifestError::Validation(format!(
            "artifact.sha256 {:?} is not 64 lowercase hex chars",
            meta.artifact.sha256
        )));
    }

    Ok(())
}

/// Convert a validated metadata record into a manifest [`Entry`], deriving
/// computed fields. `base_url` is joined with `artifact.sha256` for the download
/// URL; `added_at` stamps the entry. `revocation` is the optional
/// `revocation.json` sidecar (SPEC §4): when `Some`, the entry is marked
/// `revoked: true` with the sidecar's reason + timestamp; when `None`, the entry
/// is live (`revoked: false`).
pub fn metadata_to_entry(
    meta: RegistryMetadata,
    base_url: &str,
    added_at: &str,
    revocation: Option<Revocation>,
) -> Entry {
    let download_url = format!(
        "{}/artifacts/{}",
        base_url.trim_end_matches('/'),
        meta.artifact.sha256
    );

    let (revoked, revoked_reason, revoked_at) = match revocation {
        Some(r) => (true, Some(r.reason), Some(r.revoked_at)),
        None => (false, None, None),
    };

    Entry {
        id: meta.id,
        title: meta.title,
        system: meta.system,
        core_id: meta.core_id,
        min_core_version: meta.min_core_version,
        sha256: meta.artifact.sha256,
        size_bytes: meta.artifact.size_bytes,
        download_urls: vec![download_url],
        creator: meta.creator,
        license: meta.license,
        attestation: meta.attestation,
        artwork: meta.artwork,
        added_at: added_at.to_string(),
        revoked,
        revoked_reason,
        revoked_at,
        tags: meta.tags,
    }
}

/// Walk `registry/<system>/<slug>/`, read + validate each `metadata.json`,
/// honor the optional `revocation.json` sidecar, and return manifest [`Entry`]s
/// sorted by id for deterministic output.
///
/// A revoked entry (one with a `revocation.json`) is still emitted — with
/// `revoked: true` — so the device can deny it and sweep it. A malformed
/// `revocation.json` is a hard error (fail closed): a broken revocation must
/// never silently produce a live entry.
pub fn collect_entries(
    registry: &Path,
    base_url: &str,
    added_at: &str,
    allow_empty: bool,
) -> Result<Vec<Entry>> {
    let mut entries = Vec::new();

    for system_path in read_dir_sorted(registry)? {
        if !system_path.is_dir() {
            continue;
        }
        let system_name = file_name_str(&system_path)?;
        // Only descend into directories that name a known system.
        if serde_json::from_value::<System>(serde_json::Value::String(system_name.to_string()))
            .is_err()
        {
            continue;
        }

        for slug_path in read_dir_sorted(&system_path)? {
            if !slug_path.is_dir() {
                continue;
            }
            let slug = file_name_str(&slug_path)?.to_string();
            let meta_path = slug_path.join("metadata.json");
            if !meta_path.exists() {
                continue;
            }

            let meta_bytes = std::fs::read(&meta_path)?;
            let meta: RegistryMetadata = serde_json::from_slice(&meta_bytes)?;
            validate_metadata(&meta, &slug)?;
            let revocation = read_revocation(&slug_path, &meta.id)?;
            let entry_added_at =
                git_first_commit_date(&slug_path).unwrap_or_else(|| added_at.to_string());
            entries.push(metadata_to_entry(meta, base_url, &entry_added_at, revocation));
        }
    }

    if entries.is_empty() && !allow_empty {
        return Err(ManifestError::Validation(format!(
            "no entries found in registry at {:?}; empty manifest is forbidden without --allow-empty",
            registry
        )));
    }

    entries.sort_by(|a, b| a.id.cmp(&b.id));
    Ok(entries)
}

/// Query git for the first commit timestamp of the given directory.
/// Format returned is ISO 8601 UTC timestamp e.g. "2026-07-12T00:00:00Z".
pub fn git_first_commit_date(entry_dir: &Path) -> Option<String> {
    let output = std::process::Command::new("git")
        .current_dir(entry_dir)
        .args([
            "log",
            "--diff-filter=A",
            "--format=%aI",
            "-1",
            "--",
            "metadata.json",
        ])
        .output()
        .ok()?;

    if !output.status.success() {
        return None;
    }

    let stdout = String::from_utf8(output.stdout).ok()?;
    let trimmed = stdout.trim();
    if trimmed.is_empty() {
        return None;
    }

    if let Ok(dt) = chrono::DateTime::parse_from_rfc3339(trimmed) {
        Some(format_utc(dt.with_timezone(&Utc)))
    } else {
        None
    }
}

/// Build a complete, validated manifest from a registry directory.
///
/// If `tier` is `None` and `registry_root` contains tiered subdirectories (`beta` or `main`),
/// this fails closed to prevent emitting an empty manifest.
pub fn build_manifest(
    registry_root: &Path,
    tier: Option<&str>,
    base_url: &str,
    signer_key_id: &str,
    previous: Option<&Manifest>,
    now: Option<DateTime<Utc>>,
    allow_empty: bool,
) -> Result<Manifest> {
    if tier.is_none()
        && (registry_root.join("beta").is_dir() || registry_root.join("main").is_dir())
    {
        return Err(ManifestError::Validation(format!(
            "registry root {:?} contains tiered subdirectories (beta/main) but no --tier was specified; refusing to emit tierless manifest",
            registry_root
        )));
    }

    let generated_at = now.unwrap_or_else(Utc::now);
    let added_at = format_utc(generated_at);
    let sequence = next_sequence(previous)?;
    let entries = collect_entries(registry_root, base_url, &added_at, allow_empty)?;
    assemble_manifest(entries, generated_at, signer_key_id, sequence)
}

/// Read a directory's children as sorted paths.
fn read_dir_sorted(dir: &Path) -> Result<Vec<std::path::PathBuf>> {
    let mut paths: Vec<std::path::PathBuf> = std::fs::read_dir(dir)?
        .filter_map(|e| e.ok().map(|e| e.path()))
        .collect();
    paths.sort();
    Ok(paths)
}

/// Extract a path's final component as a `&str`.
fn file_name_str(path: &Path) -> Result<&str> {
    path.file_name()
        .and_then(|s| s.to_str())
        .ok_or_else(|| ManifestError::Validation(format!("non-UTF8 path component: {path:?}")))
}

/// Assemble a manifest from ordered entries.
///
/// `sequence` is `previous.sequence + 1` when a previous manifest is supplied,
/// else 1 (computed by the caller). `expiresAt = generatedAt + 72h`.
pub fn assemble_manifest(
    entries: Vec<Entry>,
    generated_at: DateTime<Utc>,
    signer_key_id: &str,
    sequence: u64,
) -> Result<Manifest> {
    let expires_at = generated_at
        .checked_add_signed(Duration::hours(EXPIRY_HOURS))
        .ok_or_else(|| ManifestError::Validation("generatedAt + 72h overflowed".into()))?;

    Ok(Manifest {
        schema_version: SCHEMA_VERSION,
        generated_at: format_utc(generated_at),
        expires_at: format_utc(expires_at),
        sequence,
        signer_key_id: signer_key_id.to_string(),
        entries,
    })
}

/// Next sequence from an optional previous manifest. SPEC §5.
pub fn next_sequence(previous: Option<&Manifest>) -> Result<u64> {
    match previous {
        None => Ok(1),
        Some(prev) => prev
            .sequence
            .checked_add(1)
            .ok_or_else(|| ManifestError::Validation("sequence overflow".into())),
    }
}

/// Format a UTC instant as ISO 8601 `YYYY-MM-DDTHH:MM:SSZ` (seconds precision).
pub fn format_utc(dt: DateTime<Utc>) -> String {
    dt.format("%Y-%m-%dT%H:%M:%SZ").to_string()
}

/// True if `s` matches `^[a-z0-9]+(-[a-z0-9]+)*$` (SPEC §1.3).
///
/// Hand-rolled to avoid a regex dependency: lowercase-alnum groups separated by
/// single hyphens, no leading/trailing/double hyphen, non-empty.
fn is_valid_slug(s: &str) -> bool {
    if s.is_empty() {
        return false;
    }
    let mut prev_was_hyphen = true; // treat start as "after separator" to forbid leading '-'
    for ch in s.chars() {
        if ch == '-' {
            if prev_was_hyphen {
                return false; // leading or double hyphen
            }
            prev_was_hyphen = true;
        } else if ch.is_ascii_lowercase() || ch.is_ascii_digit() {
            prev_was_hyphen = false;
        } else {
            return false; // illegal character
        }
    }
    !prev_was_hyphen // forbid trailing hyphen
}

/// True if `s` is exactly 64 lowercase hex characters.
fn is_lower_hex_64(s: &str) -> bool {
    s.len() == 64
        && s.chars()
            .all(|c| c.is_ascii_digit() || ('a'..='f').contains(&c))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn slug_rules() {
        assert!(is_valid_slug("starforge-demo"));
        assert!(is_valid_slug("abc"));
        assert!(is_valid_slug("a1-b2-c3"));
        assert!(!is_valid_slug(""));
        assert!(!is_valid_slug("-lead"));
        assert!(!is_valid_slug("trail-"));
        assert!(!is_valid_slug("double--hyphen"));
        assert!(!is_valid_slug("Upper"));
        assert!(!is_valid_slug("under_score"));
        assert!(!is_valid_slug("has space"));
    }

    #[test]
    fn hex64_rules() {
        assert!(is_lower_hex_64(
            "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd"
        ));
        assert!(!is_lower_hex_64("ABCD")); // too short + uppercase
        assert!(!is_lower_hex_64(
            "3F7EDC6A755F8ADD804136BF0EE430C105DC98FFF892025EB9ABF55F2C11EFC4"
        )); // uppercase
        assert!(!is_lower_hex_64("zz")); // non-hex
    }

    #[test]
    fn next_sequence_logic() {
        assert_eq!(next_sequence(None).unwrap(), 1);
    }
}
