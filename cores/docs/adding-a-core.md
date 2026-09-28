# Adding or Bumping a Core

This document describes the process for adding a new emulator core to the allowlist or
bumping an existing core to a newer commit. Read it fully before opening a PR.

---

## Background

Plemu uses a deny-by-default core allowlist defined in SPEC §1.5. Only the 16 cores listed
in `cores/cores.build.json` may appear in signed manifests. Any core not on the allowlist is
rejected at the device level — unknown `coreId` values cause a manifest-parse hard fail.

The `cores/expected-licenses.lock` file pins each core to a specific commit and records the
SHA-256 of its LICENSE file at that commit. The nightly `plemu-core-audit --lock` verifies
that the on-disk LICENSE hash matches the lock; any drift (e.g. an upstream relicensing) is
a hard failure.

---

## Allowed vs Forbidden

`cores/forbidden.json` lists cores that **must never appear** in any config or build:

- Non-commercial licensed cores (snes9x, genesis_plus_gx, picodrive, fbneo, fbalpha, mame\*).
- Cores relicensed in 2024 to non-commercial terms (duckstation, swanstation).
- Cores pending license re-verification (mesen\*).

`plemu-core-audit` hard-fails if any forbidden id or forbidden prefix appears anywhere in
`cores.build.json` or `expected-licenses.lock`.

---

## Adding a New Core

Before opening a PR, verify:

1. **License is commercially compatible.** Read the LICENSE file in the upstream repo at the
   commit you intend to pin. Common compatible licenses: MIT, MPL-2.0, ISC, Apache-2.0,
   GPL-2.0-only, GPL-2.0-or-later, GPL-3.0-only, GPL-3.0-or-later. Non-commercial clauses
   are hard blocks.

2. **The coreId is not on the forbidden list.** Check `cores/forbidden.json`.

3. **The system is in SPEC §1.4.** Only the 18 system enum values are legal.

4. **The system → coreId mapping is in SPEC §1.6.** A new core may require a SPEC update
   (SPEC is frozen; SPEC changes require a `schemaVersion` bump and a separate review).

If all checks pass, open a PR that updates **both files together** — a PR that updates
only one of the two is incomplete and will not be merged:

- `cores/cores.build.json` — add the new entry with the pinned `commit`.
- `cores/expected-licenses.lock` — add the new entry with the pinned `commit`,
  the correct `licenseFile` path, the SHA-256 of that file at the pinned commit, and the
  verified `spdx` identifier.

Include in the PR description:

- A link to the upstream repo at the exact commit.
- A copy of (or link to) the LICENSE text.
- The SHA-256 you computed: `git show <commit>:<licenseFile> | sha256sum`.
- Confirmation that the license is commercially compatible.

A human reviewer **must read the license diff** — not just the SPDX identifier. SPDX
identifiers can be wrong or outdated; the LICENSE file is authoritative.

---

## Bumping an Existing Core

Bumping to a newer upstream commit follows the same process:

1. Check whether the LICENSE file changed between the old and new commit.
   ```
   git diff <old-commit>..<new-commit> -- <licenseFile>
   ```
2. If the license changed, re-verify commercial compatibility.
3. Update **both** `commit` and `licenseSha256` in `cores/expected-licenses.lock`.
4. Update `commit` in `cores/cores.build.json`.
5. Open a PR with the diff and the license review confirmation.

Never bump only the commit without updating `licenseSha256` — the lock is how the nightly
audit detects silent relicensing.

---

## How the Nightly Audit Works

The nightly CI job runs:

```
plemu-core-audit --lock cores/expected-licenses.lock --cores-dir sources/
```

`plemu-core-audit` (SPEC §5) does the following:

- Checks every `coreId` in the lock is in the allowlist (SPEC §1.5).
- Checks no forbidden id or prefix appears anywhere in the lock.
- For each entry where `commit != "TBD"` and `licenseSha256 != "TBD"`:
  - Verifies the `sources/<coreId>/` directory is at the pinned commit.
  - Hashes the `licenseFile` and compares to `licenseSha256`.
  - Any mismatch → exit 20 (drift detected).
- In `--strict` mode (used by nightly): any `commit` or `licenseSha256` equal to `"TBD"`
  is a failure (exit 20). Unpinned cores do not reach production.

Exit codes: 0 = clean, 20 = drift or missing.

---

## Pinning Commits (Initial Setup)

All commits in `cores/expected-licenses.lock` are currently `"TBD"`. To pin a core:

1. Clone the core repo at a known-good commit (e.g. latest stable tag).
2. Compute the LICENSE SHA-256:
   ```
   sha256sum sources/<coreId>/<licenseFile>
   ```
3. Update `cores/expected-licenses.lock` with `commit` and `licenseSha256`.
4. Update `cores/cores.build.json` with the same `commit`.
5. Open a PR following the process above.

Do not pin in bulk without reviewing each license individually.
