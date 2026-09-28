# How a game gets into Plemu (upload process)

**Today there is no web upload portal** (it's on the roadmap). A game enters through the **git
registry** — a directory per title that a signed manifest is built from. This doc is the current
(manual/CLI) flow and where the permissionless-beta model is heading.

## The trust model in one line

The device only launches content whose SHA-256 is in a **Ed25519-signed manifest**, and it re-hashes
the bytes before every launch. So "uploading a game" ultimately means: get its hash into a signed
manifest the device trusts. Two stores: **beta** (permissionless, no screening/review) and **main**
(curated, promoted from beta). "Ungated beta = no content screening, NOT unsigned" — the device still
only runs signed content, which is also what lets us take content down.

## A registry entry

```
registry/beta/<system>/<slug>/
  metadata.json          # title, system, coreId, sha256, sizeBytes, license, creator wallet, artifact{…}
  attestation.json       # the creator's (or curator's) wallet-signed attestation over the frozen §3 message
  license-evidence/      # for custom-attested licenses: the author's grant / evidence
  artifact/<file>        # the game bytes (content-addressed; production artifacts live in R2, not git)
```

- `system`/`coreId` come from the SPEC enums (e.g. `gb` → `sameboy`).
- `license.type` is one of `PD | CC0 | CC-BY | CC-BY-SA | MIT | GPL | custom-attested`. A permissive
  license not in that list (e.g. zlib) uses `custom-attested` + evidence, with a **curator**-role
  attestation (see the Libbet entry, `registry/beta/gb/libbet/`).

## The attestation (what the wallet signs)

The creator signs the exact frozen bytes of the **Plemu Creator Attestation v1** message (SPEC §3):
title, system, SHA-256, license, a "I created this / have the right to distribute this" statement,
wallet, timestamp. The curator variant swaps the statement for "I verified this work is distributed
under the stated license per the linked evidence." Signature = Ed25519 by the wallet key over the
message. The `plemu-manifest` example `dev_attest` builds + signs this for dev fixtures:

```bash
cd tools
cargo run -p plemu-manifest --example dev_attest -- \
  --metadata ../registry/beta/gb/<slug>/metadata.json \
  --key-file ../../dev-secrets/creator-dev.pem --role creator \
  --now 2026-07-13T00:00:00Z --out ../registry/beta/gb/<slug>/attestation.json
```

## From entry to a live signed manifest

```bash
cd tools
# 1. Screen (advisory in beta — a triage aid, not a publish gate):
cargo run -p plemu-screen -- --entry ../registry/beta/gb/<slug> \
  --denylist <denylist.sqlite> --keys keys/manifest-signers.json --out report.json
# 2. Build the beta manifest from the whole beta tier:
cargo run -p plemu-manifest -- build --tier beta --registry ../registry/beta \
  --base-url https://cdn.plemu.xyz --signer-key-id plemu-manifest-dev-a \
  --now 2026-07-13T00:00:00Z --out manifest-beta.json
# 3. Sign + verify:
cargo run -p plemu-manifest -- sign   --manifest manifest-beta.json \
  --key-file ../../dev-secrets/manifest-dev-a.pem --key-id plemu-manifest-dev-a \
  --keys keys/manifest-signers.json --out manifest-beta.json.sig
cargo run -p plemu-manifest -- verify --manifest manifest-beta.json \
  --sig manifest-beta.json.sig --keys keys/manifest-signers.json
```

The device fetches `manifest-<tier>.json` (+`.sig`), verifies the signature, and re-hashes each
artifact before launch. In the editor, `stage-content.sh` runs exactly this flow and drops the
manifest + game into `StreamingAssets/Content/` (see `apps/device/unity/stage-content.sh`).

## Where this is heading (permissionless beta + the portal)

The **intended** flow, once the portal (Phase 2) is built:

1. A creator connects a wallet in the web portal, uploads the game, and signs the attestation
   in-browser.
2. The portal computes the hash, opens a `registry/beta/` entry automatically, and an **automated**
   signer publishes it into `manifest-beta.json` — **no human review** (permissionless).
3. Curators promote good entries `beta → main` (the only human gate).
4. Takedown/stop-order → write a `revocation.json` sidecar → republish → the device sweeps it
   (the reactive enforcement path; see `docs/runbooks/emergency-publish.md`).

Until the portal exists, steps 1–3 are done by hand with the commands above. There is deliberately
**no ROM file browser on the device** and no storage permission — content only ever arrives through
this signed pipeline.
