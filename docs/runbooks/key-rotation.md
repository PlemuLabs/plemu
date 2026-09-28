# Runbook: Manifest signing-key rotation

**Audience:** Plemu signer custodians.
**Goal:** rotate the manifest signing key on a quarterly cadence with **zero device
downtime**, using the active/next pinning scheme, and respond to a key compromise.

---

## Key lifecycle (SPEC §2.2)

Each key in `keys/manifest-signers.json` has a `status`:

| status | meaning | verifier accepts? |
|---|---|---|
| `active` | the key the nightly currently signs with | yes |
| `next` | pre-pinned rotation target, not yet signing | yes |
| `emergency` | offline break-glass key (see `emergency-publish.md`) | yes |
| `retired` | rotated out; must never sign a new manifest again | **no** |

A verifier (device + `plemu-manifest verify`) accepts signatures from
`active | next | emergency` only. The whole rotation trick is: **devices already
trust `next` before it ever signs**, because the app shipped with `next` pinned.
That is what makes the flip seamless.

---

## Quarterly rotation (planned, no downtime)

Let the outgoing key be **N** (`active`) and the pre-pinned successor be **N+1**
(`next`). One quarter ahead you also generate **N+2** so a `next` always exists.

### Step 1 — Generate N+1 offline (done a quarter early)

```sh
# On the offline signing machine. Ed25519, PKCS#8 PEM.
openssl genpkey -algorithm ed25519 -out plemu-manifest-<N+1>.pem
chmod 600 plemu-manifest-<N+1>.pem

# Public key as 64 lowercase hex for the registry:
openssl pkey -in plemu-manifest-<N+1>.pem -pubout -outform DER \
  | tail -c 32 | xxd -p -c 64
```

Add N+1 to `keys/manifest-signers.json` with `"status": "next"` and its
`publicKey` hex. Commit. The private PEM stays offline / in KMS; only the public
key is committed.

### Step 2 — Ship an app release pinning BOTH N and N+1

The device pins the key registry inside the signed APK/AAB. Cut an app release
whose baked-in `keys/manifest-signers.json` lists **both** N (`active`) and N+1
(`next`) as accepted. Wait until this release has rolled out widely enough (watch
adoption telemetry) that flipping the signer will not strand old installs.

> This is the load-bearing step: you cannot flip to N+1 until enough devices trust
> N+1. Never flip on the same day you publish the pinning release.

### Step 3 — Flip the nightly signer to N+1

- Promote N+1 to `active` and demote N to `retired` in
  `keys/manifest-signers.json` (commit).
- Point the nightly `sign-and-publish` secret `PLEMU_MANIFEST_KEY` at the N+1 PEM
  (KMS key id in prod). The very next nightly signs with N+1.
- Manifests signed by N+1 verify on every device that took the Step-2 release.

### Step 4 — Retire N

- N is now `retired`: verifiers reject any *new* manifest signed by N (anti-forgery
  if N later leaks). Already-published manifests remain valid only until they
  expire — retirement is about **new** signatures.
- Destroy or archive the N private key per custody policy. Generate N+2 offline and
  pin it as the new `next` (repeat Step 1) so a successor always exists.

### Rotation invariant

At any moment the registry has exactly one `active`, one `next`, and one
`emergency` key. Never have zero `next` keys — that would force an app release into
the critical path of the *following* rotation.

---

## Compromise response (unplanned)

If the `active` key (N) is compromised, an attacker who also controls a CDN edge or
a device's network could serve a forged manifest. Respond fast:

### 1. Emergency manifest on the emergency key

Immediately publish a fresh, correct manifest signed by `plemu-emergency-1`
(offline) using `emergency-publish.md`. This gives devices a trustworthy manifest
that is **not** signed by the compromised key.

### 2. Retire the compromised key

Set N to `retired` in `keys/manifest-signers.json` and cut an expedited app release
whose baked-in registry drops N and promotes N+1 (`next` → `active`). Point the
nightly at N+1's key. From here it is a compressed version of the quarterly flip.

### 3. Sequence anti-rollback is your safety net

Because devices persist the highest `sequence` they have accepted and reject any
manifest with a lower/equal sequence (SPEC §2.3.5), an attacker **cannot** replay an
old (validly-signed-by-N) manifest to downgrade a device past the emergency
manifest — as long as the emergency manifest's `sequence` is strictly higher than
anything previously published. Always build the emergency manifest with
`--previous <last published>` so its sequence advances.

### 4. Why the 72h expiry window matters here

Manifests expire 72h after `generatedAt` (SPEC §1.2), then enter a 7-day Grace
(installed titles still play, downloads blocked), then Expired (only the baked-in
starter pack plays). This bounds the blast radius of a compromise:

- A forged manifest an attacker minted with the leaked N key is only `Fresh` for at
  most 72h; after that devices stop honoring its download URLs even if the attacker
  keeps serving it.
- It also **forces** the pipeline to re-sign at least every 72h, so a healthy
  nightly naturally overwrites a stale/forged manifest with a fresh one on the new
  key within one cycle. The short window is a feature: it caps how long any bad
  manifest can matter and guarantees the rotation propagates quickly.

### 5. Post-incident

- Confirm every device population has moved to N+1 (telemetry).
- Confirm the compromised N key is destroyed and cannot be used again (it is
  `retired`, so even if it resurfaces, verifiers reject its signatures).
- Generate a new `next` (N+2) and restore the rotation invariant.
- Write up the incident: how N leaked, detection latency, time-to-emergency-publish
  vs the <30 min target.
