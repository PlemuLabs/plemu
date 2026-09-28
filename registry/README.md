# Plemu Registry — Submission Contract (M2: Two-Tier Model)

This directory is the source of truth for all game entries on Plemu.

The registry has two tiers, each compiled into a separate signed manifest:

| Tier | Path | Policy | Manifest |
|---|---|---|---|
| **Beta** | `registry/beta/<system>/<slug>/` | Permissionless — creator wallet-signs the §3 attestation; merging the PR is the only gate; **no content pre-screening, no human review** | `manifest-beta.json` |
| **Main** | `registry/main/<system>/<slug>/` | Curated — a curator promotes an entry from beta; the only human gate for main | `manifest-main.json` |

**"Ungated" beta means:** no content or piracy pre-screening before publish. It does NOT mean unsigned — the device only runs content listed in a manifest signed by a pinned Ed25519 key. Integrity, provenance, and reactive takedown are the security properties. `plemu-screen` is a triage/moderation aid (not a publish gate); it informs curators and flags obvious piracy for proactive revocation.

---

## Directory Layout (SPEC §4 M2)

```
registry/
  beta/
    <system>/<slug>/    # Permissionless beta entry
      metadata.json
      attestation.json
      license-evidence/
      [revocation.json] # Only when revoked
  main/
    <system>/<slug>/    # Curated main entry (promoted from beta)
      (same layout)
  docs/                 # Policy docs
  LICENSE
  removals.ndjson       # Removal log
```

### `<system>` values (18 allowed)

`nes snes md sms gg gb gbc gba n64 ps1 nds psp dc a26 tic80 pico8 wasm4 microw8`

### `<slug>` rules

Lowercase alphanumeric and hyphens only: `^[a-z0-9]+(-[a-z0-9]+)*$`. Immutable after merge.

---

## File Contents

### `metadata.json`

All fields from SPEC §1.3 Entry except: `downloadUrls` (derived at build time from artifact sha256
+ CDN base URL), `addedAt` (set from git history at manifest-build time), and `revoked*` fields
(managed via the emergency-revoke workflow, not by PR). Required fields:

```jsonc
{
  "id": "<system>/<slug>",
  "title": "Human-readable title",
  "system": "<system enum>",
  "coreId": "<coreId enum — must be legal for the system per SPEC §1.6>",
  "minCoreVersion": "x.y.z",
  "sha256": "<64 lowercase hex — digest of the exact playable artifact>",
  "sizeBytes": 12345,
  "creator": { "wallet": "<base58>", "playId": null, "displayName": "handle" },
  "license": {
    "type": "<license enum>",
    "evidenceUrl": "<URL or null>",
    "evidenceArchiveUrl": "<archive URL or null>"
  },
  "artwork": { "iconUrl": null, "screenshotUrls": [] },
  "tags": []
}
```

### `attestation.json`

```jsonc
{
  "role": "creator",
  "message": "<verbatim attestation text — see §Attestation Messages below>",
  "signature": "<base64 ed25519 sig of the message bytes by the wallet key>",
  "signedAt": "2026-07-12T00:00:00Z",
  "wallet": "<base58 wallet address>"
}
```

### `license-evidence/`

Must contain `evidence.md` explaining the license claim. All archived files (web page snapshots,
email replies, LICENSE file copies) go here. Total ≤ 5 MB.

---

## Attestation Messages (SPEC §3 — verbatim)

The wallet signs the UTF-8 bytes of exactly one of these messages. `\n` (LF) line endings,
**no trailing newline**, no BOM. Placeholders (`<title>`, `<system enum value>`, etc.) are
replaced; everything else is byte-exact.

### Creator variant (`attestation.role == "creator"`)

```
Plemu Creator Attestation v1

Title: <title>
System: <system enum value>
SHA-256: <64 lowercase hex>
License: <license.type enum value>
Statement: I created this work or have the right to distribute it under the stated license; it contains no third-party copyrighted material.
Wallet: <base58 wallet address>
Timestamp: <ISO 8601 UTC, e.g. 2026-08-01T14:22:00Z>
```

### Curator variant (`attestation.role == "curator"`)

Same as above except the Statement line is replaced with:

```
Statement: I verified this work is distributed under the stated license per the linked evidence.
```

---

## License Type Enum

| `license.type` | Meaning |
|---|---|
| `PD` | Public domain dedication or pre-1927 work; no rights reserved |
| `CC0` | Creative Commons Zero — waiver of all copyright; functionally equivalent to PD |
| `CC-BY` | Creative Commons Attribution — redistribution allowed with credit |
| `CC-BY-SA` | CC Attribution-ShareAlike — redistribution allowed; derivatives must share alike |
| `MIT` | MIT/Expat — permissive; attribution required in source/docs |
| `GPL` | GNU GPL (any version) — copyleft; source must be available |
| `custom-attested` | Author-provided written grant archived in `license-evidence/`; requires two-person curator review |

---

## Beta Publish Pipeline

```
Creator opens PR touching registry/beta/<system>/<slug>/
        ↓
plemu-screen runs automatically (pr-checks.yml) — ADVISORY ONLY for beta
  exit 0  → label: screen:pass   → merge allowed
  exit 10 → label: screen:review → informational; does NOT block beta merge
  exit 20 → label: screen:reject → PR blocked (DAT_MATCH / hard-fail only)
        ↓
PR merges  →  entry is in registry/beta
        ↓
Next nightly run: plemu-manifest build --tier beta → entry appears in manifest-beta.json
        ↓
Device clients pick up manifest-beta.json on next refresh (within 72h freshness window)
```

## Beta → Main Promotion

```
Curator reviews beta entry (license evidence, attestation, sha256 cross-check)
        ↓
Curator runs plemu-screen on-demand (triage / main-store curation only)
        ↓
Curator copies entry from registry/beta/<system>/<slug>/ to registry/main/<system>/<slug>/
        ↓
Curator opens a main-promotion PR; another curator reviews and merges
        ↓
Next nightly run: plemu-manifest build --tier main → entry appears in manifest-main.json
```

## Revocation (both tiers)

Revocation (DMCA, license dispute, etc.) goes through the emergency-revoke workflow, which adds
a `revocation.json` sidecar to the entry dir. The next manifest rebuild propagates `revoked: true`
to devices. See `docs/dmca-policy.md`.
