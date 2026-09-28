#!/usr/bin/env bash
#
# stage-content.sh — build + sign the beta manifest from the registry and stage it,
# together with the content-addressed game artifact, into the Unity editor's
# StreamingAssets so LaunchGate has real inputs at runtime.
#
# What it produces under apps/device/unity/Assets/StreamingAssets/Content/:
#   manifest-beta.json           (plemu-manifest build --tier beta)
#   manifest-beta.json.sig       (plemu-manifest sign, dev key manifest-dev-a)
#   artifacts/<sha256>           (the libbet.gb ROM, content-addressed by its sha256)
#
# The manifest freshness window is generatedAt + 72h (SPEC §1.2) + 7d grace. This
# script stamps generatedAt = now by default so a fresh checkout is immediately
# playable; pass --now <ISO8601> to pin it. Re-run this script if the staged
# manifest has aged past its grace window.
#
# Signing keys live OUTSIDE the repo in ../dev-secrets (relative to tools/):
# manifest-dev-a.pem is the manifest signer whose public key is pinned in
# PinnedKeys.BuiltIn. Nothing secret is written into the repo.
#
# Idempotent. Requires: cargo (builds plemu-manifest), the registry entry, and
# the game fixture. No network.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${HERE}/../../.." && pwd)"
TOOLS="${REPO_ROOT}/tools"
REGISTRY_BETA="${REPO_ROOT}/registry/beta"
SECRETS="${REPO_ROOT}/../dev-secrets"

CONTENT_DIR="${HERE}/Assets/StreamingAssets/Content"
ARTIFACTS_DIR="${CONTENT_DIR}/artifacts"
GAME_ROM="${REPO_ROOT}/apps/device/native/testdata/libbet.gb"

MANIFEST_KEY="${SECRETS}/manifest-dev-a.pem"
MANIFEST_KEY_ID="plemu-manifest-dev-a"
BASE_URL="${BASE_URL:-https://cdn.plemu.dev}"

# --now defaults to current UTC (seconds precision) so the manifest is Fresh.
NOW="${1:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"

echo "==> stage-content: now=${NOW}"

# --- sanity ---------------------------------------------------------------
[ -f "${GAME_ROM}" ] || { echo "ERROR: game ROM not found: ${GAME_ROM}" >&2; exit 1; }
[ -f "${MANIFEST_KEY}" ] || { echo "ERROR: manifest signing key not found: ${MANIFEST_KEY}" >&2; exit 1; }
[ -d "${REGISTRY_BETA}" ] || { echo "ERROR: beta registry not found: ${REGISTRY_BETA}" >&2; exit 1; }

mkdir -p "${ARTIFACTS_DIR}"

# --- 1. build plemu-manifest --------------------------------------------
echo "==> building plemu-manifest"
( cd "${TOOLS}" && cargo build --quiet -p plemu-manifest )
MANIFEST_BIN="${TOOLS}/target/debug/plemu-manifest"

# --- 2. build the beta manifest ------------------------------------------
echo "==> building beta manifest"
"${MANIFEST_BIN}" build \
    --tier beta \
    --registry "${REGISTRY_BETA}" \
    --base-url "${BASE_URL}" \
    --signer-key-id "${MANIFEST_KEY_ID}" \
    --now "${NOW}" \
    --out "${CONTENT_DIR}/manifest-beta.json"

# --- 3. sign it (self-verifies) ------------------------------------------
echo "==> signing beta manifest"
"${MANIFEST_BIN}" sign \
    --manifest "${CONTENT_DIR}/manifest-beta.json" \
    --key-file "${MANIFEST_KEY}" \
    --key-id "${MANIFEST_KEY_ID}" \
    --now "${NOW}" \
    --out "${CONTENT_DIR}/manifest-beta.json.sig"

# --- 4. stage the content-addressed artifact -----------------------------
SHA="$(shasum -a 256 "${GAME_ROM}" | awk '{print $1}')"
echo "==> staging artifact ${SHA}"
cp "${GAME_ROM}" "${ARTIFACTS_DIR}/${SHA}"

echo "==> stage-content OK"
echo "    manifest: ${CONTENT_DIR}/manifest-beta.json"
echo "    artifact: ${ARTIFACTS_DIR}/${SHA}"
