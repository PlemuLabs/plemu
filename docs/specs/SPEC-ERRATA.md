# SPEC v1 Errata & Implementation Extensions

This document records accepted deviations, extensions, and operational errata relative to `spec/SPEC.md` (v1, FROZEN 2026-07-12). In accordance with Plemu governance, `spec/SPEC.md` is strictly frozen and never edited directly; all ratified amendments and operational adaptations are tracked here.

---

## 1. Manifest Tooling & CLI Extensions (`plemu-manifest`)

### 1.1. `--allow-empty` Flag
- **SPEC Reference:** §1.1, §4, §5.
- **Extension:** Added `--allow-empty` flag to `plemu-manifest build`.
- **Rationale:** By default, building a manifest against an empty directory fails closed with a typed validation error to prevent accidental emission of empty manifests. However, during initialization or staging of new tiers (such as `registry/main` prior to game promotion), an empty tier is legitimate. The `--allow-empty` flag explicitly authorizes generating an empty manifest (`entries: []`) with valid signatures and sequence progression.

### 1.2. Automatic Tier Directory Resolution
- **SPEC Reference:** §4 M2 Two-Tier Model, §5 Manifest CLI.
- **Extension:** When `--tier <beta|main>` is specified, the CLI automatically resolves the registry root to `<registry>/<tier>` (or `<cwd>/<tier>` if `--registry` is omitted). Additionally, the default output path is set to `manifest-<tier>.json`.
- **Fail-Closed Protection:** Attempting to run a tierless build (`--tier` omitted) against a registry root containing tiered subdirectories (`beta/` or `main/`) fails closed loudly to avoid unintended compilation of parent trees.

### 1.3. Git-Derived `addedAt` Timestamps
- **SPEC Reference:** §1.3 (Entry Schema — `addedAt` format ISO 8601 UTC).
- **Extension:** For each entry, `addedAt` is dynamically extracted from the repository's git commit history (`git log --diff-filter=A --format=%aI -1 -- metadata.json`) representing the first commit adding the entry's `metadata.json`.
- **Fallback:** If git history is unavailable (e.g. shallow clone or untracked directory), it falls back safely to the manifest `generatedAt` timestamp.

---

## 2. Removals & Takedown Event Journal (`registry/removals.ndjson`)

### 2.1. Removals Schema & Reinstatement Actions
- **SPEC Reference:** §1.4 (Sidecar Files), §3 (Revocation Lifecycle).
- **Extension:** Emergency takedown and reinstatement events recorded in `registry/removals.ndjson` adhere strictly to the schema:
  - `eventType`: `"revocation"` or `"reinstatement"`
  - `entryId`: String matching `"<system>/<slug>"`
  - `reason`: `"dmca"` | `"malware"` | `"creator-request"` | `"licensing-dispute"` | `"reinstated"`
  - `timestamp`: ISO 8601 UTC timestamp
  - `noticeRef`: Optional string referencing the ticket, issue URL, or legal filing
- **Reinstatement Workflow:** An entry previously revoked can be reinstated via `emergency-revoke.yml` (`action: reinstate`), removing its `revocation.json` sidecar and logging a reinstatement event in `removals.ndjson`.

---

## 3. Distribution & Architecture Conventions

### 3.1. Two-Tier Naming Conventions
- **SPEC Reference:** §1.1, §2.1, §2.3.
- **Convention:** With the two-tier distribution model (`beta` and `main`), manifests and detached signatures are served per-tier:
  - `manifest-beta.json` and `manifest-beta.json.sig`
  - `manifest-main.json` and `manifest-main.json.sig`
- **Signature Extension:** Detached signatures use the `.json.sig` extension to avoid MIME collisions and ensure explicit association with the signed JSON payload.

### 3.2. Monorepo Consolidation
- **Workflows & Paths:** Earlier prose referenced standalone external repositories (`plemu/plemu-cores` and `plemu/plemu-registry`). All governance, tooling, cores specifications, and registry tiers are unified within the single monorepo at `cores/`, `registry/`, and `tools/`.
