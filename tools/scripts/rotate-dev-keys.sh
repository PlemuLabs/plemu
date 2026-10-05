#!/usr/bin/env bash
# rotate-dev-keys.sh — generate a fresh set of DEV signing keys and re-sign every
# fixture in this repo with them.
#
# WHY THIS EXISTS
# ---------------
# This repo ships with signatures produced by a keypair whose PRIVATE half was
# never included in the handoff. That is deliberate, and it has a useful property:
# you can verify the whole trust chain on arrival — proving the slice is real and
# the bytes were not altered in transit — without inheriting anyone else's signing
# key.
#
# It also means you cannot re-sign anything until you run this. `stage-content.sh`
# fails with "manifest signing key not found" until then.
#
# Run this ONCE, before you stage content for the first time. It is re-runnable
# (guarded by PLEMU_FORCE_ROTATE=1) and discovers the values it replaces from the
# tree, so it stays correct after the first rotation.
#
# These are TEST keys. Production keys live in KMS / CI secrets, and the emergency
# key is generated offline and never touches a repo or a runner (SPEC §2.2, §6).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS="$(cd "${HERE}/.." && pwd)"
ROOT="$(cd "${TOOLS}/.." && pwd)"
SECRETS="$(cd "${ROOT}/.." && pwd)/dev-secrets"
UNITY="${ROOT}/apps/device/unity"
FIXTURES="${UNITY}/Assets/_Game/Tests/EditMode/Fixtures"
TESTDATA="${TOOLS}/testdata/sample-manifest"
NOW="${PLEMU_NOW:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"

say() { printf '==> %s\n' "$*"; }

if [ -e "${SECRETS}/manifest-dev-a.pem" ] && [ "${PLEMU_FORCE_ROTATE:-0}" != "1" ]; then
  echo "ERROR: ${SECRETS}/manifest-dev-a.pem already exists." >&2
  echo "       Rotating would invalidate every signature made with it." >&2
  echo "       Set PLEMU_FORCE_ROTATE=1 only if you really mean to replace them." >&2
  exit 1
fi

# --- 1. discover what we are replacing -------------------------------------
# Read from the tree rather than hardcoding, so this stays correct on re-runs.
OLD_A="$(python3 -c "import json;print(json.load(open('${TOOLS}/keys/manifest-signers.json'))['keys'][0]['publicKey'])")"
OLD_B="$(python3 -c "import json;print(json.load(open('${TOOLS}/keys/manifest-signers.json'))['keys'][1]['publicKey'])")"
OLD_W="$(python3 -c "import json;print(json.load(open('${ROOT}/registry/beta/gb/libbet/attestation.json'))['wallet'])")"
OLD_SIG="$(python3 -c "import json;print(json.load(open('${TESTDATA}/manifest.sig'))['signature'])")"
say "rotating away from key ${OLD_A:0:12}... wallet ${OLD_W:0:12}..."

# --- 2. generate keys, outside the repo ------------------------------------
say "generating Ed25519 dev keys in ${SECRETS} (OUTSIDE the repo)"
mkdir -p "${SECRETS}"; chmod 700 "${SECRETS}"
for k in manifest-dev-a manifest-dev-b creator-dev; do
  openssl genpkey -algorithm ed25519 -out "${SECRETS}/${k}.pem" 2>/dev/null
  chmod 600 "${SECRETS}/${k}.pem"
done

# the 32-byte raw public key is the last 32 bytes of the SPKI DER
pubhex() { openssl pkey -in "$1" -pubout -outform DER | tail -c 32 | xxd -p -c 32; }
PUB_A="$(pubhex "${SECRETS}/manifest-dev-a.pem")"
PUB_B="$(pubhex "${SECRETS}/manifest-dev-b.pem")"
PUB_C="$(pubhex "${SECRETS}/creator-dev.pem")"
WALLET="$(python3 "${HERE}/b58.py" "${PUB_C}")"

say "manifest-dev-a  ${PUB_A}"
say "manifest-dev-b  ${PUB_B}"
say "creator wallet  ${WALLET}"

# --- 3. global replace across every text file -------------------------------
# A sweep, not a file list: the old key turns up in Rust test assertions, C#
# constants, JSON registries, prose and fixtures, and enumerating them by hand is
# how one gets missed. docs/specs/SPEC.md is EXCLUDED — it is frozen and never edited;
# the stale value there is recorded in SPEC-ERRATA.md below.
say "replacing old key material across the tree"
python3 "${HERE}/rotate_replace.py" "${ROOT}" "${OLD_A}" "${PUB_A}" "${OLD_B}" "${PUB_B}" "${OLD_W}" "${WALLET}"

say "writing tools/keys/manifest-signers.json"
cat > "${TOOLS}/keys/manifest-signers.json" << JSONEOF
{
  "keys": [
    {
      "keyId": "plemu-manifest-dev-a",
      "publicKey": "${PUB_A}",
      "status": "active",
      "validFrom": "${NOW}"
    },
    {
      "keyId": "plemu-manifest-dev-b",
      "publicKey": "${PUB_B}",
      "status": "next"
    }
  ],
  "_warning": "DEV KEYS FOR TESTS ONLY. Production active/next keys live in CI environment secrets or KMS; the emergency key is generated offline and is never committed anywhere."
}
JSONEOF

ERRATA="${ROOT}/docs/specs/SPEC-ERRATA.md"
if [ ! -f "${ERRATA}" ]; then
  printf '# SPEC v1 errata\n\nWhere `SPEC.md` prose is stale or superseded. **`SPEC.md` itself is never edited.**\n' > "${ERRATA}"
fi
cat >> "${ERRATA}" << ERREOF

## §6 — dev key values are stale (${NOW})

\`SPEC.md\` §6 documents the dev public keys of the original fixture set. Those keys were rotated
out by \`tools/scripts/rotate-dev-keys.sh\` and their private halves were never distributed. The
live values live in \`tools/keys/manifest-signers.json\` and \`PinnedKeys.cs\`, which are the
source of truth. Current active key: \`${PUB_A}\`.
ERREOF

# --- 4. re-sign the shared cross-language fixture ---------------------------
say "re-signing testdata/sample-manifest"
( cd "${TOOLS}" && cargo run -q -p plemu-manifest -- sign \
    --manifest "${TESTDATA}/manifest.json" \
    --key-file "${SECRETS}/manifest-dev-a.pem" \
    --key-id plemu-manifest-dev-a \
    --keys keys/manifest-signers.json \
    --out "${TESTDATA}/manifest.sig" )

NEW_SIG="$(python3 -c "import json;print(json.load(open('${TESTDATA}/manifest.sig'))['signature'])")"

# Two Rust tests keep a hardcoded copy of the fixture signature
# (plemu-manifest/src/crypto.rs and tests/verify_integration.rs). Sweep those too,
# or the suite fails after rotation. They would be better off reading the fixture
# file directly - noted in PLAN.md as a cleanup for Stream B.
say "replacing the fixture signature in hardcoded test copies"
python3 "${HERE}/rotate_replace.py" "${ROOT}" "${OLD_SIG}" "${NEW_SIG}" "${OLD_SIG}" "${NEW_SIG}" "${OLD_SIG}" "${NEW_SIG}"

# the tampered variant is the clean one with one byte changed; it MUST fail
python3 "${HERE}/rotate_tamper.py" "${TESTDATA}/manifest.json" "${TESTDATA}/manifest.tampered.json"

say "mirroring fixtures into Unity EditMode"
cp "${TESTDATA}/manifest.json" "${TESTDATA}/manifest.sig" \
   "${TESTDATA}/manifest.tampered.json" "${FIXTURES}/"
cp "${TOOLS}/keys/manifest-signers.json" "${FIXTURES}/manifest-signers.json"

# --- 5. re-attest the registry entries --------------------------------------
say "re-attesting registry entries with the new creator wallet"
for spec in "registry/beta/gb/libbet:curator" "registry/beta/wasm4/hello-plemu:creator"; do
  dir="${ROOT}/${spec%%:*}"; role="${spec##*:}"
  ( cd "${TOOLS}" && cargo run -q -p plemu-manifest --example dev_attest -- \
      --metadata "${dir}/metadata.json" \
      --key-file "${SECRETS}/creator-dev.pem" \
      --role "${role}" --now "${NOW}" \
      --out "${dir}/attestation.json" >/dev/null )
  python3 "${HERE}/rotate_inline.py" "${dir}/metadata.json" "${dir}/attestation.json"
done

# --- 6. re-stage and verify -------------------------------------------------
say "re-staging device content"
( cd "${UNITY}" && bash stage-content.sh >/dev/null )

say "verifying the staged manifest"
( cd "${TOOLS}" && cargo run -q -p plemu-manifest -- verify \
    --manifest "${UNITY}/Assets/StreamingAssets/Content/manifest-beta.json" \
    --sig "${UNITY}/Assets/StreamingAssets/Content/manifest-beta.json.sig" \
    --keys keys/manifest-signers.json >/dev/null )

say "running the Rust suite"
( cd "${TOOLS}" && cargo test --workspace -q >/dev/null 2>&1 ) \
  || { echo "ERROR: Rust suite failed after rotation" >&2; exit 1; }

say "rotate-dev-keys OK - the keys are yours now"
echo
echo "    Private keys:  ${SECRETS}   (outside the repo, never commit)"
echo "    Next:          open Unity and run the Test Runner (EditMode + PlayMode)"
