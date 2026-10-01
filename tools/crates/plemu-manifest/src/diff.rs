//! Manifest diffing. SPEC §5 (`plemu-manifest diff`).
//!
//! Emits `{ added, removed, revoked, changed }` keyed by entry `id`:
//! - `added`: ids present in new but not old,
//! - `removed`: ids present in old but not new,
//! - `revoked`: ids that flipped `revoked` false→true,
//! - `changed`: same id, differing `sha256`, `revoked`, or `title`.

use std::collections::BTreeMap;

use plemu_schemas::Manifest;
use serde::Serialize;

/// One `changed` record: the id plus what differs. Serialized camelCase to
/// match the manifest wire format (TypeScript consumers).
#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Changed {
    pub id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub sha256_from: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub sha256_to: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title_from: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title_to: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub revoked_from: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub revoked_to: Option<bool>,
}

/// The diff changelog. SPEC §5.
#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
pub struct Diff {
    pub added: Vec<String>,
    pub removed: Vec<String>,
    pub revoked: Vec<String>,
    pub changed: Vec<Changed>,
}

/// Compute the diff between an old and new manifest.
///
/// Output vectors are sorted by id (via `BTreeMap` iteration) for deterministic,
/// review-friendly changelogs. An id that is both revoked-flipped and otherwise
/// changed appears in `revoked` and in `changed`.
pub fn diff_manifests(old: &Manifest, new: &Manifest) -> Diff {
    let old_by_id: BTreeMap<&str, &_> = old.entries.iter().map(|e| (e.id.as_str(), e)).collect();
    let new_by_id: BTreeMap<&str, &_> = new.entries.iter().map(|e| (e.id.as_str(), e)).collect();

    let mut added = Vec::new();
    let mut removed = Vec::new();
    let mut revoked = Vec::new();
    let mut changed = Vec::new();

    for (id, new_entry) in &new_by_id {
        match old_by_id.get(id) {
            None => added.push((*id).to_string()),
            Some(old_entry) => {
                if !old_entry.revoked && new_entry.revoked {
                    revoked.push((*id).to_string());
                }

                let sha_changed = old_entry.sha256 != new_entry.sha256;
                let title_changed = old_entry.title != new_entry.title;
                let revoked_changed = old_entry.revoked != new_entry.revoked;

                if sha_changed || title_changed || revoked_changed {
                    changed.push(Changed {
                        id: (*id).to_string(),
                        sha256_from: sha_changed.then(|| old_entry.sha256.clone()),
                        sha256_to: sha_changed.then(|| new_entry.sha256.clone()),
                        title_from: title_changed.then(|| old_entry.title.clone()),
                        title_to: title_changed.then(|| new_entry.title.clone()),
                        revoked_from: revoked_changed.then_some(old_entry.revoked),
                        revoked_to: revoked_changed.then_some(new_entry.revoked),
                    });
                }
            }
        }
    }

    for id in old_by_id.keys() {
        if !new_by_id.contains_key(id) {
            removed.push((*id).to_string());
        }
    }

    Diff {
        added,
        removed,
        revoked,
        changed,
    }
}
