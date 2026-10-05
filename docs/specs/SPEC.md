# Plemu — Content Trust SPEC v1 (FROZEN 2026-07-12)

This document is the single source of truth for the signed-manifest content-allowlist system.
The Rust tools (`plemu-*`), the Unity device client, the registry repo, and all CI workflows
implement **exactly** this. Changes require a `schemaVersion` bump and a review.

Threat model in one line: the device must never load content whose bytes are not explicitly
approved; approval is a SHA-256 listed in a manifest signed by a pinned Ed25519 key; everything
else fails closed.

---

## 1. Manifest

### 1.0 Two-manifest model (M2)

The content store is now served as **two separate signed manifest files**:

| File | Tier | Source | Policy |
|---|---|---|---|
| `manifest-beta.json` (+`manifest-beta.sig`) | Beta | `registry/beta/<system>/<slug>/` | Permissionless: creator wallet-signs the §3 attestation; auto-published; no human pre-screening |
| `manifest-main.json` (+`manifest-main.sig`) | Main | `registry/main/<system>/<slug>/` | Curated: entries promoted from beta by a curator; the only human gate |

Both manifests use the **identical** §1.2/§1.3 schema — the tier is an operational convention
(which file/URL the device fetched), not a wire-format field. **Do NOT add a `tier` field to
the manifest or to any Entry.** Each manifest is independently signed, sequenced, and revoked
(its own `sequence`, its own `manifest.sig`). The device fetches both and renders beta entries
behind an "unverified / community" warning.

Reactive takedown applies to both tiers: the existing `revocation.json` sidecar mechanism
(SPEC §4) is the only revocation path. Revoked entries remain in the manifest with `revoked:
true` so the device can deny and sweep them. `plemu-screen` is **no longer a publish gate**
for either tier — it is a triage and moderation aid (run on-demand for main-store curation
and to flag obvious commercial dumps for proactive takedown; SPEC §4, §5.2).

A single JSON document (`manifest-beta.json` or `manifest-main.json`) plus a **detached**
signature file (`manifest-<tier>.sig`).

### 1.1 Signing model (normative)

- The Ed25519 signature is computed over the **raw bytes of `manifest.json` exactly as served**.
  There is NO canonicalization step. Producers sign the file they publish; verifiers verify the
  bytes they received **before parsing any JSON**.
- `manifest.json` MUST be UTF-8, and SHOULD be pretty-printed with 2-space indent and `\n` line
  endings (cosmetic only — verification never depends on formatting).

### 1.2 `manifest.json` top level

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-07-12T00:00:00Z",   // ISO 8601 UTC, seconds precision, trailing Z
  "expiresAt": "2026-07-15T00:00:00Z",     // production policy: generatedAt + 72h
  "sequence": 1,                            // u64, strictly increasing per publish
  "signerKeyId": "plemu-manifest-dev-a",  // must match manifest.sig keyId
  "entries": [ /* Entry[], see 1.3 */ ]
}
```

### 1.3 Entry

```jsonc
{
  "id": "gb/starforge-demo",               // "<system>/<slug>", slug ^[a-z0-9]+(-[a-z0-9]+)*$, immutable
  "title": "Starforge Demo",
  "system": "gb",                          // enum, see 1.4
  "coreId": "sameboy",                     // enum, see 1.5; must be legal for `system` (1.6)
  "minCoreVersion": "0.16.3",
  "sha256": "70b8...64 lowercase hex",     // digest of the exact playable artifact
  "sizeBytes": 37,                          // integer
  "downloadUrls": ["https://cdn.plemu.xyz/artifacts/70b8..."],
  "creator": { "wallet": "FGUr...base58", "playId": null, "displayName": "izma" },
  "license": { "type": "CC0",              // enum: PD | CC0 | CC-BY | CC-BY-SA | MIT | GPL | custom-attested
               "evidenceUrl": "https://example.com/source",
               "evidenceArchiveUrl": null },
  "attestation": { "role": "creator",      // "creator" | "curator"
                   "message": "<exact attestation text, see §3>",
                   "signature": "<base64 ed25519 sig by creator/curator wallet>",
                   "signedAt": "2026-07-12T00:00:00Z" },
  "artwork": { "iconUrl": null, "screenshotUrls": [] },
  "addedAt": "2026-07-12T00:00:00Z",
  "revoked": false,                         // optional, default false
  "revokedReason": null,                    // "dmca" | "license-dispute" | "malware" | "creator-request" | null
  "revokedAt": null,
  "tags": ["starter-pack"]
}
```

- Unknown fields: verifiers MUST ignore unknown fields (forward compat); producers MUST NOT emit them at schemaVersion 1.
- `sha256`/hex fields are lowercase. Timestamps are ISO 8601 UTC `YYYY-MM-DDTHH:MM:SSZ`.

### 1.4 `system` enum (18)

`nes snes md sms gg gb gbc gba n64 ps1 nds psp dc a26 tic80 pico8 wasm4 microw8`

### 1.5 `coreId` allowlist (16) — deny-by-default

`fceumm bsnes blastem gearsystem sameboy mgba stella mupen64plus_next beetle_psx melonds ppsspp flycast tic80 fake08 wasm4 microw8`

**FORBIDDEN core identifiers** (non-commercial or relicensed; must hard-fail any config/build that
mentions them): `snes9x genesis_plus_gx picodrive fbneo fbalpha duckstation swanstation mame2000
mame2003 mame2003_plus mame2010 mesen*`†

† `mesen` stays forbidden until its license is re-verified and added to `expected-licenses.lock`;
NES v1 core is `fceumm`.

### 1.6 system → legal coreIds

| system | coreIds |
|---|---|
| nes | fceumm |
| snes | bsnes |
| md | blastem |
| sms, gg | gearsystem |
| gb, gbc | sameboy |
| gba | mgba |
| a26 | stella |
| n64 | mupen64plus_next |
| ps1 | beetle_psx |
| nds | melonds |
| psp | ppsspp |
| dc | flycast |
| tic80 | tic80 |
| pico8 | fake08 |
| wasm4 | wasm4 |
| microw8 | microw8 |

---

## 2. Signature envelope + key registry

### 2.1 `manifest.sig`

```json
{
  "keyId": "plemu-manifest-dev-a",
  "alg": "ed25519",
  "signature": "<base64(64-byte ed25519 signature over raw manifest.json bytes)>",
  "signedAt": "2026-07-12T00:00:00Z"
}
```

### 2.2 Key registry — `keys/manifest-signers.json`

```json
{
  "keys": [
    { "keyId": "plemu-manifest-dev-a", "publicKey": "<64 hex chars = 32 bytes>", "status": "active",    "validFrom": "2026-07-12T00:00:00Z" },
    { "keyId": "plemu-manifest-dev-b", "publicKey": "<64 hex>",                   "status": "next" },
    { "keyId": "plemu-emergency-1",    "publicKey": "<64 hex>",                   "status": "emergency" }
  ]
}
```

Statuses: `active` (nightly signer), `next` (pre-pinned rotation target), `emergency` (offline key,
manual runbook only), `retired` (verify-only rejection: MUST NOT accept new manifests). A verifier
accepts signatures from `active | next | emergency` keys only.

**DEV KEYS in this repo are for tests only. Production keys live in CI secrets/KMS; the emergency
key is generated offline and never touches a repo or runner.**

### 2.3 Verification algorithm (device + `plemu-manifest verify`) — normative order

1. Read raw bytes of `manifest.json` and `manifest.sig`.
2. Parse `manifest.sig` only. Look up `keyId` in the pinned key registry; status must be
   `active|next|emergency`; `alg` must be `ed25519`.
3. Ed25519-verify `signature` over the raw manifest bytes. **Fail ⇒ stop. Never parse.**
4. Parse `manifest.json`. Check `schemaVersion == 1` and `signerKeyId == sig.keyId`.
5. Anti-rollback: `sequence` must be ≥ the highest previously-accepted sequence (device persists
   this; equal allowed for re-reads of the same manifest, lower rejected).
6. Freshness (device policy; `verify` CLI takes `--now <ISO>` for determinism):
   - `now ≤ expiresAt` → **Fresh**: play + download allowed.
   - `expiresAt < now ≤ expiresAt + 7d` → **Grace**: installed titles play; downloads blocked.
   - beyond → **Expired**: CDN-installed titles blocked; bundled starter-pack titles (verified
     against the baked-in manifest shipped inside the signed APK) remain playable.
7. Per-launch content gate (device): streaming SHA-256 of the artifact must equal the entry's
   `sha256` **at every launch**, entry not `revoked`. Any failure ⇒ typed denial, no bypass.

Cross-implementation note: the Ed25519 verifier backends (Rust `ed25519-dalek` `verify_strict`
vs the C# client's Chaos.NaCl cofactored verify) need not agree bit-for-bit on adversarially
crafted small-order or non-canonical inputs. This is acceptable because manifest signatures come
only from pinned registry keys signing well-formed manifests, never from attacker-chosen key or
signature material, so the divergent edge cases are unreachable in this protocol.

---

## 3. Creator attestation v1 (frozen bytes)

The wallet signs the UTF-8 bytes of exactly this message. `\n` (LF) endings, **no trailing
newline**, no leading BOM. Placeholders replaced; everything else byte-exact.

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

Curator variant (`attestation.role == "curator"`) replaces only the Statement line with:

```
Statement: I verified this work is distributed under the stated license per the linked evidence.
```

Screening cross-checks: SHA-256 line == artifact digest; Wallet line == `creator.wallet` (creator
role) or a listed curator wallet (curator role); Timestamp not in the future (>24h skew) and not
older than 90 days at submission time. Signature: Ed25519 by the wallet key over the message bytes.

---

## 4. Registry repo layout (`plemu-registry`) — M2 two-tier model

The registry is split into two subtrees, one per tier:

```
registry/
  beta/                 # Permissionless: anyone publishes; no pre-screening / human review
    <system>/<slug>/    # Entry directory (same layout as main)
  main/                 # Curated: entries promoted from beta by a curator
    <system>/<slug>/    # Entry directory
```

Each entry directory has the same internal layout in both tiers:

```
<system>/<slug>/
  metadata.json         # Entry minus computed fields (downloadUrls, addedAt, revoked*) plus:
                        #   artifact: { sha256, sizeBytes, filename }
                        #   submission: { via: "portal"|"cli", submittedAt }
  attestation.json      # { role, message, signature, signedAt, wallet }
  revocation.json       # present ONLY when revoked:
                        #   { reason: <dmca|license-dispute|malware|creator-request>, revokedAt: <ISO8601Z> }
  license-evidence/     # evidence.md + archived copies (≤5 MB total)
```

### 4.1 Publish model (M2)

**Beta (permissionless):** A creator wallet-signs the §3 attestation and opens a PR touching
`registry/beta/<system>/<slug>/`. Merging the PR is the only gate — there is **no content
pre-screening and no human review requirement**. `plemu-screen` runs as an advisory triage
check (`screen:review` label); a `screen:review` result does NOT block the beta publish.
Only a hard-fail (`screen:reject`, DAT_MATCH) blocks the merge. The signed `manifest-beta.json`
is rebuilt on the next nightly run.

**Main (curated):** Curators promote entries from `registry/beta/` to `registry/main/` by
opening a PR. This is the only human gate for main. Curators run `plemu-screen` on-demand as a
triage aid before promoting; it is not a CI requirement.

**"Ungated" beta means:** no content pre-screening, no piracy pre-screen, no human review
before publish. It does **NOT** mean unsigned — the device only runs content whose bytes are
listed in a manifest signed by a pinned Ed25519 key (SPEC §2). Integrity + provenance +
reactive takedown (§4.2) are the security properties, not pre-publication gating.

**Screening reframe:** `plemu-screen` is a moderation aid, not a publish gate. It is run:
- On-demand by curators evaluating beta→main promotion.
- Proactively to flag obvious commercial dumps for preemptive revocation.
- Never as a blocking CI gate for beta publishes (advisory only).

### 4.2 Reactive takedown / revocation

Revocation works identically in both tiers via the `revocation.json` sidecar mechanism. Adding
`revocation.json` to an entry dir and rebuilding the manifest propagates `revoked: true` to all
devices on the next refresh. The revoked entry stays in the manifest so devices can deny-and-
sweep it. `plemu-manifest build` reads `revocation.json` when present and sets the manifest
entry's `revoked`/`revokedReason`/`revokedAt`; live entries have no such file and `build` emits
`revoked: false`.

`plemu-manifest build` compiles a registry tier subdir into a manifest (deriving `downloadUrls`
from `--base-url` + `artifact.sha256`, `addedAt` from git/`--now`). Use `--tier beta` or
`--tier main` as a convenience selector (see §5).

---

## 5. CLI contracts

All tools: `--help` works, errors to stderr, machine output to `--out` or stdout, no panics on bad
input (kit rust rules: no `unwrap()` outside tests).

```
plemu-manifest build  (--registry DIR | --tier beta|main) --base-url URL [--previous FILE] [--now ISO] [--out manifest-<tier>.json]
plemu-manifest sign   --manifest FILE (--key-file PEM | --key-env VAR) --key-id ID [--keys FILE] [--now ISO] --out manifest.sig
                       # after signing, MUST self-verify against the key registry if --keys given
plemu-manifest verify --manifest FILE --sig FILE --keys FILE [--now ISO] [--min-sequence N]
                       # exit 0 = valid; exit 1 = invalid (reason on stderr); prints freshness state
plemu-manifest diff   --old FILE --new FILE --out changelog.json   # {added[],removed[],revoked[],changed[]}

plemu-dat build       --input DIR --out denylist.sqlite            # ingests Logiqx XML .dat files
plemu-screen          (--entry DIR | --all --registry DIR) --denylist FILE [--keys FILE] --out report.json
                       # exit 0 pass | 10 needs-human-review | 20 hard-fail
plemu-core-audit      --lock FILE --cores-dir DIR                  # exit 0 clean | 20 drift/missing
```

### 5.1 `denylist.sqlite` schema

```sql
CREATE TABLE hashes(algo TEXT NOT NULL, hex TEXT NOT NULL, source TEXT, rom_name TEXT, game_name TEXT, system TEXT);
CREATE INDEX idx_hashes ON hashes(algo, hex);
CREATE TABLE names(normalized TEXT NOT NULL, game_name TEXT, system TEXT);
CREATE INDEX idx_names ON names(normalized);
CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT);  -- additive (v1.0.1): built_at ISO date,
                                                      -- dat file count, entry counts
-- algo ∈ {crc32, md5, sha1, sha256}; hex lowercase
```

### 5.2 Screening checks (id → classification)

Hard-fail (exit 20): `DAT_MATCH`, `ATTESTATION`, `SCHEMA`, `ARCHIVE_SAFETY`.
Flag → needs-human-review (exit 10): `HDR_VALID`, `SIZE_SANITY`, `DEDUP`, `NAME_FUZZY`,
`STR_SCAN`, `EVIDENCE_URL`, plus any check reporting `not_implemented`.
`DIGESTS` always runs first and feeds the others. Headerless digest variants: NES (skip 16-byte
iNES header), SNES (skip 512-byte copier header when `size % 1024 == 512`), A78 (skip 128), Lynx
(skip 64). Report shape:

```json
{ "tool": "plemu-screen", "version": "x.y.z", "datSnapshot": "YYYY-MM-DD",
  "entry": "gb/starforge-demo",
  "digests": { "crc32": "...", "md5": "...", "sha1": "...", "sha256": "..." },
  "checks": [ { "id": "DAT_MATCH", "status": "pass|flag|fail", "detail": "..." } ],
  "verdict": "pass|review|fail" }
```

### 5.3 `expected-licenses.lock` (plemu-cores)

```json
{ "cores": [ { "coreId": "sameboy", "repo": "https://github.com/LIJI32/SameBoy",
               "commit": "TBD", "licenseFile": "LICENSE", "licenseSha256": "TBD",
               "spdx": "MIT" } ] }
```

`plemu-core-audit` fails on: coreId not in allowlist §1.5, any forbidden id present anywhere in
the file, commit/licenseSha256 == "TBD" (unpinned cores are a failure in nightly `--strict` mode),
or the on-disk LICENSE hash differing from the lock.

---

## 6. Shared test fixtures (`testdata/`) — generated at groundwork, used by Rust AND C#

- `testdata/sample-manifest/starforge-demo.bin` — content artifact,
  sha256 `70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd`, 37 bytes.
- `testdata/sample-manifest/manifest.json` — valid manifest (sequence 1, expires 2030) with 2
  entries: `gb/starforge-demo` (the artifact above) and `tic80/revoked-example` (`revoked: true`).
- `testdata/sample-manifest/manifest.sig` — envelope signed by `plemu-manifest-dev-a`
  (openssl-produced; any conforming implementation MUST verify it).
- `testdata/sample-manifest/manifest.tampered.json` — one byte changed; MUST fail verification.
- `keys/manifest-signers.json` — dev key registry:
  - `plemu-manifest-dev-a` (active): `1bc81e91d8b1cbb13472d9461c227d01287abdbaf642fa2d0471316981346b9c`
  - `plemu-manifest-dev-b` (next):   `b3a544ba95bc44519de7782b6bd0d712808a602bccd3ef016dbb896960cce1f7`
- Private dev keys: `plemu/dev-secrets/manifest-dev-{a,b}.pem` — OUTSIDE all repos, never commit.

Cross-implementation rule: Rust `plemu-manifest verify` and the Unity `ManifestVerifier` must both
accept the valid fixture and reject the tampered one, byte-for-byte, with no coordination beyond
this SPEC.
