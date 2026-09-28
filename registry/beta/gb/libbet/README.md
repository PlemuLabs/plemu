# gb/libbet — Libbet and the Magic Floor (beta registry entry)

Homebrew Game Boy game by Damian Yerrick (pinobatch), used as the real-core
fixture that the M4 Unity emulation runtime plays through `LaunchGate`.

## Contents

- `artifact/libbet.gb` — the unmodified ROM image (the one documented committed
  artifact, per the same exception as `wasm4/hello-plemu`; production artifacts
  otherwise live in R2 and are never committed).
  - sha256: `3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9`
  - size: 32768 bytes
- `metadata.json` — SPEC §4 entry (system `gb`, core `sameboy`, license
  `custom-attested`).
- `attestation.json` — **curator-role** attestation signed by the dev curator key
  (`dev-secrets/creator-dev.pem`, kept outside all repos) via `plemu-manifest`'s
  `dev_attest` example.
- `license-evidence/evidence.md` — the zlib license evidence + why the manifest
  records it as `custom-attested`.

## Why `custom-attested` + curator role

The SPEC §1.3 license enum has no `zlib` value. Libbet is distributed under the
permissive zlib license, so it is recorded as `custom-attested` with an
`evidenceUrl` at the upstream license and a curator attestation ("I verified this
work is distributed under the stated license per the linked evidence.") rather
than a creator attestation.

## Regenerating the attestation

```sh
cd Plemu
cargo run -p plemu-manifest --manifest-path tools/Cargo.toml --example dev_attest -- \
  --metadata registry/beta/gb/libbet/metadata.json \
  --key-file ../dev-secrets/creator-dev.pem \
  --role curator \
  --now 2026-07-13T00:00:00Z \
  --out registry/beta/gb/libbet/attestation.json
```

Then copy `attestation.json`'s `message` + `signature` into `metadata.json`'s
inline `attestation` block (the inline copy is what `plemu-manifest build`
carries into the manifest).

## Staging into the Unity editor

`apps/device/unity/stage-content.sh` builds + signs the beta manifest from this
registry and stages `manifest-beta.json` (+`.sig`) and the content-addressed
artifact into `Assets/StreamingAssets/Content/` for `LaunchGate` to consume.
