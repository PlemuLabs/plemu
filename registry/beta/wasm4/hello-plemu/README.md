# hello-plemu (dev example entry)

This is a **development example** registry entry used by the tooling test suite
(`plemu-governance/scripts/e2e-demo.sh`) and as a reference for the SPEC §4
registry layout.

## Why the artifact is committed here

Per SPEC §4 and the registry policy, **production artifacts live in R2 storage and
are never committed to git** — the registry holds only `metadata.json`,
`attestation.json`, and `license-evidence/`. The `downloadUrls` in the compiled
manifest are derived from the artifact `sha256`, not from any file in this repo.

`artifact/hello-plemu.wasm` is the **one documented exception**: it is an original,
8-byte, minimal WebAssembly module (`\0asm` + version header, nothing proprietary)
committed only so the offline end-to-end demo has a real artifact to hash, screen,
and build a manifest from without any network access. It is not distributed to
devices from git.

## Contents

- `artifact/hello-plemu.wasm` — 8-byte original WASM module.
  - sha256: `93a44bbb96c751218e4c00d479e4c14358122a389acca16205b1e4d0dc5f9476`
  - size: 8 bytes
- `metadata.json` — SPEC §4 entry metadata (system `wasm4`, core `wasm4`, license CC0).
- `attestation.json` — creator attestation signed by the dev creator key
  (`dev-secrets/creator-dev.pem`, kept outside all repos) via
  `plemu-manifest`'s `dev_attest` example. Wallet
  `Dos7vSEN2rFFsinucJsaLyJ8B2EhkkcyrM6zu73rjcHa` = base58 of that key's public key.
- `license-evidence/evidence.md` — CC0 dedication statement.

## Regenerating the attestation

```sh
cd plemu-governance
cargo run -p plemu-manifest --example dev_attest -- \
  --metadata ../plemu-registry/registry/wasm4/hello-plemu/metadata.json \
  --key-file ../dev-secrets/creator-dev.pem \
  --role creator \
  --now 2026-07-12T00:00:00Z \
  --out ../plemu-registry/registry/wasm4/hello-plemu/attestation.json
```

Then sync `metadata.json`'s inline `attestation.signature` to the value in
`attestation.json` (the inline copy is what `plemu-manifest build` carries into
the manifest; the separate file is what `plemu-screen` verifies).
