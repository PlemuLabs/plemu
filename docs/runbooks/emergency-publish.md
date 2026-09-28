# Runbook: Emergency manifest publish (offline emergency key)

**Audience:** on-call manifest signer / Plemu ops.
**Goal:** publish a correctly-signed manifest to devices when the normal CI
signing path is unavailable, using the offline `plemu-emergency-1` key.

---

## When to use this

Use the **offline emergency key** path only when at least one of these holds:

- The nightly / `sign-and-publish` CI path is down (runner outage, GitHub
  incident, the `manifest-publish` environment secret is unavailable or suspected
  compromised) **and** a manifest must ship now (e.g. a legal takedown, an active
  malware entry, an expiry cliff about to strand devices in Grace/Expired).
- The active signing key (`plemu-manifest-dev-a` in dev; the KMS key in prod) is
  compromised or must be rotated out immediately (see `key-rotation.md` →
  "Compromise response"). The emergency key signs the stop-gap manifest while the
  active key is retired and a new `next` key is promoted.

If the normal CI path works, **do not use this** — use the `nightly-enforce` or
`emergency-revoke` workflow instead. The emergency key is a break-glass tool.

## Who holds the emergency key

- `plemu-emergency-1` is generated **offline** and **never touches a repo or a CI
  runner** (SPEC §2.2, §6). It lives on an offline medium (hardware token or an
  air-gapped machine) under M-of-N custody by the Plemu signers.
- Its **public** key is pinned in `keys/manifest-signers.json` with
  `"status": "emergency"`, so devices and `plemu-manifest verify` accept it.
- The private PEM is referenced below as `<offline PEM>` — it is only ever present
  on the signing operator's offline machine for the duration of this procedure.

> Dev note: in development the analogue is `dev-secrets/manifest-*.pem`, kept
> outside every repo. The real emergency key is stricter (offline, custodial).

---

## Procedure

All commands run on the **offline signing machine**, from a checkout of
`plemu-governance` (for the tools) plus a checkout of `plemu-registry` (for
the entries). Network is only needed for the final upload step, which is performed
from a separate, connected machine if the signer is fully air-gapped (carry the two
output files across on the offline medium).

### 1. Build the manifest from the registry

```sh
cargo run -p plemu-manifest -- build \
  --registry ../plemu-registry/registry \
  --base-url https://cdn.plemu.xyz \
  --signer-key-id plemu-emergency-1 \
  --previous ./last-manifest.json \
  --out manifest.json
```

- `--signer-key-id plemu-emergency-1` MUST match the `keyId` used when signing
  (step 2) — `verify` rejects a mismatch (SPEC §2.3.4).
- `--previous ./last-manifest.json` (the last published manifest, carried on the
  offline medium) makes the new `sequence` = previous + 1. **Do not skip this** —
  a manifest with a lower/equal sequence than what devices already hold is rejected
  by the anti-rollback rule (SPEC §2.3.5). If you truly have no previous manifest,
  omit the flag and sequence restarts at 1 (only valid for a first-ever publish).

### 2. Sign locally with the offline emergency PEM

```sh
cargo run -p plemu-manifest -- sign \
  --manifest manifest.json \
  --key-file <offline PEM> \
  --key-id plemu-emergency-1 \
  --keys keys/manifest-signers.json \
  --out manifest.sig
```

- `--key-file <offline PEM>` reads the raw PKCS#8 Ed25519 key from the offline
  medium. Never pass the key via `--key-env` on a shared machine.
- `--keys keys/manifest-signers.json` makes `sign` **self-verify** against the
  pinned registry after signing (SPEC §5): if the emergency public key is not
  pinned with an accepted status, signing aborts. This catches a mis-pinned key
  before you ship.

### 3. Verify before you publish

```sh
cargo run -p plemu-manifest -- verify \
  --manifest manifest.json \
  --sig manifest.sig \
  --keys keys/manifest-signers.json \
  --min-sequence <sequence of last-manifest.json>
```

- Expect exit `0` and a freshness line of `Fresh` on stdout. If it prints `Grace`
  or `Expired`, your `generatedAt` is stale — rebuild (step 1).
- `--min-sequence` set to the previously-published sequence proves the new manifest
  is strictly ahead. A non-zero exit here means **do not publish**.

### 4. Publish directly to R2 with wrangler

From the connected machine (carry `manifest.json` + `manifest.sig` across if
air-gapped):

```sh
# The single canonical URLs devices poll:
wrangler r2 object put plemu-manifests/manifest.json --file manifest.json
wrangler r2 object put plemu-manifests/manifest.sig  --file manifest.sig

# Immutable by-date copy for audit / rollback:
DATE=$(date -u +%Y-%m-%dT%H%M%SZ)
wrangler r2 object put plemu-manifests/by-date/$DATE/manifest.json --file manifest.json
wrangler r2 object put plemu-manifests/by-date/$DATE/manifest.sig  --file manifest.sig
```

> **TODO(infra):** the `plemu-manifests` R2 bucket, the `cdn.plemu.xyz`
> mapping, and the Cloudflare cache-purge endpoint do not exist yet. Once they do,
> add the single-URL cache purge here so devices see the new bytes immediately:
>
> ```sh
> # TODO(infra): curl -X POST https://api.cloudflare.com/client/v4/zones/<zone>/purge_cache \
> #   -H "Authorization: Bearer <token>" -H 'Content-Type: application/json' \
> #   -d '{"files":["https://cdn.plemu.xyz/manifest.json","https://cdn.plemu.xyz/manifest.sig"]}'
> ```

### 5. Re-verify the served bytes

Fetch the published files back and verify the bytes that devices will actually get:

```sh
curl -fsSL https://cdn.plemu.xyz/manifest.json -o served-manifest.json
curl -fsSL https://cdn.plemu.xyz/manifest.sig  -o served-manifest.sig
cargo run -p plemu-manifest -- verify \
  --manifest served-manifest.json --sig served-manifest.sig \
  --keys keys/manifest-signers.json
```

Because signing is over the **raw bytes as served** (SPEC §1.1), a proxy or CDN
that mangles the body would break verification — this step catches it.

---

## After the incident

- Record the run in the ops log: reason, `sequence`, manifest `sha256`, operator,
  time.
- If the emergency key was used because the active key was compromised, follow
  `key-rotation.md` → "Compromise response" to retire the active key and promote
  the `next` key. The emergency key returns to the safe.
- Keep the offline PEM offline. Do not leave a copy on the connected machine.
