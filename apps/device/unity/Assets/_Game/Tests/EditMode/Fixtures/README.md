# EditMode Test Fixtures

**Frozen cross-language fixtures. Source of truth lives in `plemu-governance`
(`testdata/sample-manifest/` and `keys/`).** Do NOT edit these copies by hand — regenerate them
upstream and re-copy. The Rust `plemu-manifest verify` and this project's C# `ManifestVerifier`
must both accept the valid fixture and reject the tampered one byte-for-byte (SPEC §6).

Tests read these via `File.ReadAllBytes(Path.Combine(Application.dataPath, "_Game/Tests/EditMode/
Fixtures/..."))` — raw filesystem reads, NOT Unity asset loading — so the signed bytes are exact.

## Files

| File | Upstream origin | Notes |
|---|---|---|
| `manifest.json` | `testdata/sample-manifest/manifest.json` | Valid manifest, sequence 1, expires 2030-01-01, 2 entries. |
| `manifest.sig` | `testdata/sample-manifest/manifest.sig` | Detached Ed25519 envelope signed by `plemu-manifest-dev-a` (openssl-produced). |
| `manifest.tampered.json` | `testdata/sample-manifest/manifest.tampered.json` | One byte changed (`sizeBytes 37 → 38`); MUST fail verification against `manifest.sig`. |
| `starforge-demo.bytes` | `testdata/sample-manifest/starforge-demo.bin` | **Renamed** `.bin → .bytes` so Unity imports it as a `TextAsset` cleanly and never rewrites the bytes. Content artifact, 37 bytes, sha256 `70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd`. |
| `manifest-signers.json` | `keys/manifest-signers.json` | Dev key registry: `plemu-manifest-dev-a` (active), `plemu-manifest-dev-b` (next). Test-only. |

## Rename provenance

The only deviation from the upstream bytes is the filename: `starforge-demo.bin` → `starforge-demo.bytes`.
The file contents are identical (verified by sha256). The `.bytes` extension is Unity's convention for
raw binary `TextAsset` imports; it prevents the importer from treating the file as an unknown asset.
