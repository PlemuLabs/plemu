# Game Submission Checklist

Thank you for submitting to Plemu Registry. Complete every item before requesting review.
Incomplete submissions will be returned without curator review.

---

## Entry Details

- **Entry id** (`<system>/<slug>`): <!-- e.g. gb/starforge-demo -->
- **Title**: 
- **System**: <!-- one of: nes snes md sms gg gb gbc gba n64 ps1 nds psp dc a26 tic80 pico8 wasm4 microw8 -->
- **License type**: <!-- PD | CC0 | CC-BY | CC-BY-SA | MIT | GPL | custom-attested -->

---

## Files

- [ ] `registry/<system>/<slug>/metadata.json` — present and valid JSON
- [ ] `registry/<system>/<slug>/attestation.json` — present and valid JSON
- [ ] `registry/<system>/<slug>/license-evidence/evidence.md` — present, explains the license claim

## Metadata

- [ ] `id` matches the directory path (`<system>/<slug>`)
- [ ] `system` is one of the 18 allowed enum values
- [ ] `coreId` is in the 16-entry allowlist and legal for the stated system (see `registry/README.md §1.6`)
- [ ] `sha256` is 64 lowercase hex — double-checked against the actual artifact file
- [ ] `sizeBytes` matches the artifact file size in bytes
- [ ] No forbidden core identifiers appear anywhere in the submitted files

## Attestation

- [ ] `message` follows the verbatim template in `registry/README.md §Attestation Messages`
- [ ] `message → SHA-256` line matches `metadata.json → artifact.sha256`
- [ ] `message → Wallet` matches `attestation.json → wallet` and `metadata.json → creator.wallet`
- [ ] `signedAt` is within the last 90 days and not in the future
- [ ] `signature` is a valid base64 ed25519 signature of the message bytes (verified by `plemu-screen`)

## License Evidence

- [ ] Evidence supports the claimed `license.type` (see `docs/licensing-matrix.md`)
- [ ] If `CC-BY-NC-SA` (PICO-8 BBS default): I understand this is **NOT acceptable** without an explicit author grant waiving the NC clause
- [ ] Evidence is archived (archive.org or local snapshot committed) — archive URL: <!-- paste here -->
- [ ] `license-evidence/` total size ≤ 5 MB

## Content Policy

- [ ] This work is public domain, freely licensed, or creator-owned
- [ ] No third-party copyrighted material (commercial game IP, samples, assets) is included
- [ ] I understand that hash-match against a commercial ROM denylist is an automatic hard rejection

## For `custom-attested` submissions

- [ ] Author's written grant is committed verbatim to `license-evidence/`
- [ ] I understand this requires two-person curator approval

---

## Notes for Curators

<!-- Optional: any context that helps the curator — e.g. "author confirmed via email thread in license-evidence/" -->
