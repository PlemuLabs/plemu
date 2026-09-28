# Curation Checklist (M2: Two-Tier)

This checklist applies differently depending on the target tier:

| Tier | Who uses this checklist | When to use it |
|---|---|---|
| **Beta** (`registry/beta/`) | Curators (optional) | Screening is advisory; the only hard gate is `screen:reject` (DAT_MATCH). Beta PRs do NOT require curator review to merge. |
| **Main** (`registry/main/`) | Curators (required) | All steps below are required before merging a beta→main promotion PR. |

Step-by-step gate for **main-store promotion PRs** and for flagging beta PRs that need
elevated attention. Complete all steps in order. Do not merge if any step cannot be confirmed.

---

## 0. Prerequisites

- [ ] You hold the `@plemu-labs/curators` role.
- [ ] You are not the submitter of this PR (no self-merges).

---

## 1. Automated Screening Verdict

Open the PR's **Checks** tab and locate the `plemu-screen` job.

**For beta PRs:**
- **exit 0 / label `screen:pass`** — merge allowed.
- **exit 10 / label `screen:review`** — advisory; does NOT block the beta publish. A curator
  may inspect and comment, but the PR may still be merged.
- **exit 20 / label `screen:reject`** — **STOP. Do not merge.** Close the PR, citing the
  check id (e.g. `DAT_MATCH`). Hard-fail is the only CI gate for beta.

**For main promotion PRs (required):**
- **exit 0 / label `screen:pass`** — proceed to Step 2.
- **exit 10 / label `screen:review`** — proceed, but apply elevated scrutiny; document in comment.
- **exit 20 / label `screen:reject`** — **STOP. Do not proceed.** Same as beta above.

Note: `plemu-screen` is a triage and moderation aid, not the primary publish gate.
Run it on-demand for main-store curation; do not treat `screen:review` as a block for beta.

Open `report.json` (linked from the check output) and confirm:
- [ ] `verdict` matches the label above.
- [ ] `digests.sha256` matches `metadata.json → artifact.sha256` exactly (64 lowercase hex, character-for-character).

---

## 2. License Evidence

Open `license-evidence/evidence.md` and the archived copies.

- [ ] The `license.type` in `metadata.json` is one of the allowed enum values.
- [ ] The evidence supports the claimed license type (see `docs/licensing-matrix.md`).
- [ ] `license.evidenceUrl` is accessible (or archived copy is present and explains the URL).
- [ ] Archive copies are within the 5 MB total limit.
- [ ] You have saved/confirmed an archive copy of the primary evidence URL (use archive.org or
  a local snapshot committed to `license-evidence/`). Record the archive URL in a PR comment.

**Special rules:**
- `custom-attested` → **two-person rule**: a second curator must also approve before you merge.
  Add the comment `curator-2-approved: @<handle>` when the second approval is confirmed.
- Curator-role attestations → same two-person rule applies.

---

## 3. Attestation Cross-Check

Open `attestation.json`.

- [ ] `role` is `"creator"` or `"curator"` (valid values only).
- [ ] `message` matches the verbatim attestation template in `registry/README.md §Attestation
  Messages` with the correct variant for the stated role. Check byte-by-byte if unsure — copy
  the expected text, fill in the placeholders from `metadata.json`, and diff.
- [ ] `message → SHA-256` line == `metadata.json → artifact.sha256`.
- [ ] `message → Wallet` line == `attestation.json → wallet` and == `metadata.json → creator.wallet`
  (creator role) OR a listed curator wallet (curator role).
- [ ] `signedAt` timestamp: not in the future by more than 24 h; not older than 90 days from today.
- [ ] `signature` field is present and non-empty (cryptographic verification is performed by
  `plemu-screen`; the curator checks presence and plausibility).

---

## 4. Metadata Sanity

- [ ] `id` equals `"<system>/<slug>"` and the slug matches the directory path.
- [ ] `system` is one of the 18 allowed enum values.
- [ ] `coreId` is in the 16-entry allowlist AND is legal for the stated `system` (check SPEC §1.6
  or the table in `registry/README.md`).
- [ ] `title` is present and non-empty.
- [ ] `sizeBytes` is a positive integer and plausible for the stated system.
- [ ] No FORBIDDEN core identifiers appear anywhere in the submitted files
  (`snes9x genesis_plus_gx picodrive fbneo fbalpha duckstation swanstation mame2000 mame2003
  mame2003_plus mame2010` or any `mesen*` prefix).

---

## 5. Merge Etiquette

- [ ] All checks above are confirmed.
- [ ] For `custom-attested` or curator-role: two-person approval confirmed (comment on PR).
- [ ] PR has exactly one entry directory changed (multi-entry PRs require individual entry review
  for each directory; confirm all are clean before merging).
- [ ] Merge using **"Create a merge commit"** (not squash, not rebase) so that `addedAt` can be
  derived from the merge commit timestamp during `plemu-manifest build`.
- [ ] After merge, verify the entry appears in the next nightly manifest run (check CI artifacts
  or the manifest viewer). If it does not appear within 48 h, file a bug.

---

## Quick Reference — Rejection Reasons

| Situation | Action |
|---|---|
| `DAT_MATCH` hard-fail | Close PR, cite check id, no appeal path via curation |
| `SCHEMA` fail | Ask submitter to fix and re-submit |
| `ATTESTATION` fail | Ask submitter to re-sign; do not merge |
| License evidence missing/insufficient | Request clarification; do not merge until resolved |
| `custom-attested` without second curator | Wait for second approval |
| Forbidden coreId in files | Close PR, explain |
