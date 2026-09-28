#!/usr/bin/env bash
#
# e2e-demo.sh — offline end-to-end integration proof for the Plemu
# content-trust pipeline. Runs entirely locally with NO network access.
#
# It exercises the whole chain and ASSERTS every outcome (any deviation exits 1):
#   (a) build the plemu-* tools
#   (b) synthesize a commercial-dump DAT and build a denylist from it
#   (c) screen the committed dev creator entry (wasm4/hello-plemu) -> exit 0 (pass)
#   (d) screen a pirate entry whose artifact IS the commercial dump -> exit 20,
#       DAT_MATCH=fail (the pirate is otherwise clean: valid attestation, schema)
#   (e) build a manifest over the registry, sign it, and verify it -> exit 0 + Fresh
#   (f) print a summary table of the five outcomes
#
# Signing key (step e): $PLEMU_DEV_KEY_PEM, default ../../dev-secrets/manifest-dev-a.pem
# relative to the governance repo root. If that file is absent, signing + verify are
# SKIPPED with a loud message and the script still exits 0 (CI-safe: dev-secrets/ is
# never committed).
#
# Requires (all offline): cargo, openssl, python3, jq, sha1sum/shasum.

set -euo pipefail

# ── Locate the governance repo root (this script lives in scripts/) ────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

REGISTRY="${REPO_ROOT}/../registry"
KEYS="${REPO_ROOT}/keys/manifest-signers.json"
# M2: two-tier registry — the beta subdir is the permissionless store.
# DEV_ENTRY is now under registry/beta/wasm4/ (moved from registry/wasm4/).
DEV_ENTRY="${REGISTRY}/beta/wasm4/hello-plemu"
PLEMU_DEV_KEY_PEM="${PLEMU_DEV_KEY_PEM:-${REPO_ROOT}/../../dev-secrets/manifest-dev-a.pem}"

# Deterministic clocks so timestamp checks are reproducible.
NOW_SIGN="2026-07-12T00:00:00Z"   # attestation / manifest generation time
NOW_SCREEN="2026-07-12T12:00:00Z" # screening / verification "now" (12h later)

# Work area (sandbox-writable). Cleaned on exit.
WORK="$(mktemp -d "${TMPDIR:-/tmp}/plemu-e2e.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

# Outcome tracking for the final table.
declare -a RESULTS=()
record() { RESULTS+=("$1|$2"); }

fail() {
  echo "" >&2
  echo "E2E FAILURE: $*" >&2
  exit 1
}

section() { echo ""; echo "==== $* ===="; }

# base58-encode a hex string (Bitcoin alphabet) — offline, no external deps.
b58_from_hex() {
  python3 - "$1" <<'PY'
import sys
raw = bytes.fromhex(sys.argv[1])
ALPH = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"
n = int.from_bytes(raw, "big")
s = ""
while n > 0:
    n, r = divmod(n, 58)
    s = ALPH[r] + s
pad = 0
for b in raw:
    if b == 0:
        pad += 1
    else:
        break
print(("1" * pad) + s)
PY
}

# sha1 of a file, portable across sha1sum/shasum.
sha1_of() {
  if command -v sha1sum >/dev/null 2>&1; then sha1sum "$1" | awk '{print $1}';
  else shasum -a 1 "$1" | awk '{print $1}'; fi
}
sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}';
  else shasum -a 256 "$1" | awk '{print $1}'; fi
}
# crc32 as 8 lowercase hex, via python zlib (offline).
crc32_of() {
  python3 - "$1" <<'PY'
import sys, zlib
data = open(sys.argv[1], "rb").read()
print(format(zlib.crc32(data) & 0xffffffff, "08x"))
PY
}
md5_of() {
  if command -v md5sum >/dev/null 2>&1; then md5sum "$1" | awk '{print $1}';
  else md5 -q "$1"; fi
}

# ── (a) Build the tools ────────────────────────────────────────────────────────
section "(a) Build plemu-* tools + dev_attest example"
cargo build -q -p plemu-dat -p plemu-screen -p plemu-manifest
cargo build -q -p plemu-manifest --example dev_attest
echo "built: plemu-dat, plemu-screen, plemu-manifest, dev_attest"

DAT_BIN="${REPO_ROOT}/target/debug/plemu-dat"
SCREEN_BIN="${REPO_ROOT}/target/debug/plemu-screen"
MANIFEST_BIN="${REPO_ROOT}/target/debug/plemu-manifest"
ATTEST_BIN="${REPO_ROOT}/target/debug/examples/dev_attest"
for b in "$DAT_BIN" "$SCREEN_BIN" "$MANIFEST_BIN" "$ATTEST_BIN"; do
  [ -x "$b" ] || fail "expected built binary missing: $b"
done

# ── (b) Synthetic commercial dump + denylist ───────────────────────────────────
section "(b) Synthesize a commercial-dump DAT and build denylist"
# The "commercial dump" is a wasm module so a pirate entry claiming system wasm4
# passes HDR_VALID (proving DAT_MATCH — not a header failure — is what rejects it).
printf '\x00\x61\x73\x6d\x01\x00\x00\x00PIRATED-COMMERCIAL-GAME-PAYLOAD' > "$WORK/commercial.bin"
C_CRC="$(crc32_of "$WORK/commercial.bin")"
C_MD5="$(md5_of "$WORK/commercial.bin")"
C_SHA1="$(sha1_of "$WORK/commercial.bin")"
C_SHA256="$(sha256_of "$WORK/commercial.bin")"
C_SIZE="$(wc -c < "$WORK/commercial.bin" | tr -d ' ')"
echo "commercial.bin: size=$C_SIZE crc32=$C_CRC sha1=$C_SHA1"

mkdir -p "$WORK/dats"
# Logiqx XML naming a fake commercial game whose rom hashes == commercial.bin's.
# plemu-dat ingests crc/md5/sha1 attributes; plemu-screen recomputes them on the
# artifact and any match on any algo is a DAT_MATCH hard-fail.
cat > "$WORK/dats/commercial.dat" <<EOF
<?xml version="1.0"?>
<datafile>
  <header><name>Synthetic Commercial Collection</name></header>
  <game name="Totally Commercial Game (USA)">
    <rom name="Totally Commercial Game (USA).bin" size="${C_SIZE}" crc="${C_CRC}" md5="${C_MD5}" sha1="${C_SHA1}"/>
  </game>
</datafile>
EOF

"$DAT_BIN" build --input "$WORK/dats" --out "$WORK/denylist.sqlite" \
  --source "synthetic-commercial" --now "$NOW_SIGN"
[ -f "$WORK/denylist.sqlite" ] || fail "denylist not created"
echo "denylist built at $WORK/denylist.sqlite"

# ── (c) Clean creator entry passes ─────────────────────────────────────────────
section "(c) Screen committed dev entry wasm4/hello-plemu (expect exit 0 / pass)"
[ -d "$DEV_ENTRY" ] || fail "dev example entry missing: $DEV_ENTRY (registry deliverable)"
set +e
"$SCREEN_BIN" --entry "$DEV_ENTRY" --denylist "$WORK/denylist.sqlite" \
  --keys "$KEYS" --now "$NOW_SCREEN" --out "$WORK/clean-report.json"
CLEAN_CODE=$?
set -e
CLEAN_VERDICT="$(jq -r '.verdict' "$WORK/clean-report.json" 2>/dev/null || echo "unknown")"
echo "clean entry: exit=$CLEAN_CODE verdict=$CLEAN_VERDICT"
[ "$CLEAN_CODE" -eq 0 ] || fail "clean creator entry must exit 0, got $CLEAN_CODE"
[ "$CLEAN_VERDICT" = "pass" ] || fail "clean creator entry must be verdict 'pass', got '$CLEAN_VERDICT'"
record "clean creator entry (wasm4/hello-plemu)" "PASS exit 0, verdict=pass"

# ── (d) Pirate entry rejected by DAT_MATCH ─────────────────────────────────────
section "(d) Screen pirate entry (artifact == commercial dump; expect exit 20 / DAT_MATCH fail)"
# Throwaway creator key so the attestation itself is valid — the ONLY hard-fail
# must be DAT_MATCH, proving anti-piracy screening (not some incidental defect)
# rejects the pirate.
openssl genpkey -algorithm ed25519 -out "$WORK/pirate.pem" 2>/dev/null
PIRATE_PUBHEX="$(openssl pkey -in "$WORK/pirate.pem" -pubout -outform DER 2>/dev/null | tail -c 32 | xxd -p -c 64)"
PIRATE_WALLET="$(b58_from_hex "$PIRATE_PUBHEX")"
echo "pirate wallet: $PIRATE_WALLET"

PIRATE_ENTRY="$WORK/pirate-registry/wasm4/definitely-not-piracy"
mkdir -p "$PIRATE_ENTRY/artifact"
cp "$WORK/commercial.bin" "$PIRATE_ENTRY/artifact/game.wasm"

cat > "$PIRATE_ENTRY/metadata.json" <<EOF
{
  "id": "wasm4/definitely-not-piracy",
  "title": "Definitely Not Piracy",
  "system": "wasm4",
  "coreId": "wasm4",
  "minCoreVersion": "2.5.0",
  "sha256": "${C_SHA256}",
  "sizeBytes": ${C_SIZE},
  "creator": { "wallet": "${PIRATE_WALLET}", "displayName": "Definitely Legit" },
  "license": { "type": "CC0", "evidenceUrl": "https://example.com/totally-legit" },
  "submission": { "via": "cli", "submittedAt": "${NOW_SIGN}" },
  "artifact": { "sha256": "${C_SHA256}", "sizeBytes": ${C_SIZE}, "filename": "artifact/game.wasm" }
}
EOF
python3 -m json.tool "$PIRATE_ENTRY/metadata.json" >/dev/null || fail "pirate metadata.json is not valid JSON"

# Sign a genuine §3 attestation with the throwaway key (valid on its own).
"$ATTEST_BIN" --metadata "$PIRATE_ENTRY/metadata.json" --key-file "$WORK/pirate.pem" \
  --role creator --now "$NOW_SIGN" --out "$PIRATE_ENTRY/attestation.json"

set +e
"$SCREEN_BIN" --entry "$PIRATE_ENTRY" --denylist "$WORK/denylist.sqlite" \
  --keys "$KEYS" --now "$NOW_SCREEN" --out "$WORK/pirate-report.json"
PIRATE_CODE=$?
set -e
PIRATE_VERDICT="$(jq -r '.verdict' "$WORK/pirate-report.json" 2>/dev/null || echo "unknown")"
DAT_STATUS="$(jq -r '.checks[] | select(.id=="DAT_MATCH") | .status' "$WORK/pirate-report.json" 2>/dev/null || echo "unknown")"
ATT_STATUS="$(jq -r '.checks[] | select(.id=="ATTESTATION") | .status' "$WORK/pirate-report.json" 2>/dev/null || echo "unknown")"
echo "pirate entry: exit=$PIRATE_CODE verdict=$PIRATE_VERDICT DAT_MATCH=$DAT_STATUS ATTESTATION=$ATT_STATUS"
[ "$PIRATE_CODE" -eq 20 ] || fail "pirate entry must exit 20 (hard-fail), got $PIRATE_CODE"
[ "$PIRATE_VERDICT" = "fail" ] || fail "pirate entry must be verdict 'fail', got '$PIRATE_VERDICT'"
[ "$DAT_STATUS" = "fail" ] || fail "pirate entry DAT_MATCH must be 'fail', got '$DAT_STATUS'"
[ "$ATT_STATUS" = "pass" ] || fail "pirate ATTESTATION must be 'pass' (so DAT_MATCH is the sole rejecter), got '$ATT_STATUS'"
record "pirate entry (commercial dump)" "REJECTED exit 20, DAT_MATCH=fail"

# ── (e) Manifest build + sign + verify ─────────────────────────────────────────
section "(e) Build + sign + verify manifest over the registry (expect exit 0 / Fresh)"
# M2: build the BETA manifest via --tier beta (selects registry/beta/).
"$MANIFEST_BIN" build --tier beta --registry "$REGISTRY/beta" --base-url https://cdn.plemu.xyz \
  --signer-key-id plemu-manifest-dev-a --now "$NOW_SIGN" --out "$WORK/manifest.json"
MANIFEST_ENTRIES="$(jq '.entries | length' "$WORK/manifest.json")"
echo "manifest built: $MANIFEST_ENTRIES entr(y/ies), sequence=$(jq -r .sequence "$WORK/manifest.json")"
[ "$MANIFEST_ENTRIES" -ge 1 ] || fail "manifest must contain >=1 entry, got $MANIFEST_ENTRIES"

if [ -f "$PLEMU_DEV_KEY_PEM" ]; then
  "$MANIFEST_BIN" sign --manifest "$WORK/manifest.json" --key-file "$PLEMU_DEV_KEY_PEM" \
    --key-id plemu-manifest-dev-a --keys "$KEYS" --now "$NOW_SIGN" --out "$WORK/manifest.sig"
  set +e
  FRESHNESS="$("$MANIFEST_BIN" verify --manifest "$WORK/manifest.json" --sig "$WORK/manifest.sig" \
    --keys "$KEYS" --now "$NOW_SCREEN" 2>"$WORK/verify.err")"
  VERIFY_CODE=$?
  set -e
  echo "verify: exit=$VERIFY_CODE freshness=$FRESHNESS"
  [ "$VERIFY_CODE" -eq 0 ] || fail "manifest verify must exit 0, got $VERIFY_CODE ($(cat "$WORK/verify.err"))"
  [ "$FRESHNESS" = "Fresh" ] || fail "manifest freshness must be 'Fresh', got '$FRESHNESS'"
  record "manifest build+sign+verify" "OK exit 0, freshness=Fresh"
else
  echo ""
  echo "############################################################"
  echo "# SKIPPING sign + verify: dev signing key not found at"
  echo "#   $PLEMU_DEV_KEY_PEM"
  echo "# (dev-secrets/ is never committed; this is CI-safe.)"
  echo "# The manifest BUILD step above still ran and was asserted."
  echo "############################################################"
  record "manifest build (sign+verify SKIPPED — no dev key)" "BUILD OK; sign/verify skipped"
fi

# ── (f) Revocation propagates via the revocation.json sidecar ──────────────────
section "(f) Revoke the dev entry via revocation.json sidecar (expect revoked:true in manifest)"
# Work on a COPY of the registry so the real committed registry is never dirtied.
REV_REG="$WORK/rev-registry"
cp -r "$REGISTRY" "$REV_REG"
# M2: entry is now under registry/beta/wasm4/.
REV_ENTRY_DIR="$REV_REG/beta/wasm4/hello-plemu"
[ -d "$REV_ENTRY_DIR" ] || fail "dev entry missing in registry copy: $REV_ENTRY_DIR"

# Build the manifest BEFORE revoking to prove the entry starts live.
"$MANIFEST_BIN" build --tier beta --registry "$REV_REG/beta" --base-url https://cdn.plemu.xyz \
  --signer-key-id plemu-manifest-dev-a --now "$NOW_SIGN" --out "$WORK/pre-revoke.json"
PRE_REVOKED="$(jq -r '.entries[] | select(.id=="wasm4/hello-plemu") | .revoked' "$WORK/pre-revoke.json")"
echo "before sidecar: wasm4/hello-plemu revoked=$PRE_REVOKED"
[ "$PRE_REVOKED" = "false" ] || fail "entry must start live (revoked=false), got '$PRE_REVOKED'"

# Write the revocation.json sidecar (exactly what emergency-revoke.yml writes).
jq -n --arg reason "dmca" --arg now "$NOW_SIGN" \
  '{reason:$reason, revokedAt:$now}' > "$REV_ENTRY_DIR/revocation.json"
python3 -m json.tool "$REV_ENTRY_DIR/revocation.json" > /dev/null || fail "revocation.json is not valid JSON"

# Rebuild and assert the entry now carries revoked:true + reason/timestamp.
"$MANIFEST_BIN" build --tier beta --registry "$REV_REG/beta" --base-url https://cdn.plemu.xyz \
  --signer-key-id plemu-manifest-dev-a --now "$NOW_SIGN" --out "$WORK/post-revoke.json"
POST_REVOKED="$(jq -r '.entries[] | select(.id=="wasm4/hello-plemu") | .revoked' "$WORK/post-revoke.json")"
POST_REASON="$(jq -r '.entries[] | select(.id=="wasm4/hello-plemu") | .revokedReason' "$WORK/post-revoke.json")"
POST_AT="$(jq -r '.entries[] | select(.id=="wasm4/hello-plemu") | .revokedAt' "$WORK/post-revoke.json")"
echo "after sidecar:  wasm4/hello-plemu revoked=$POST_REVOKED reason=$POST_REASON revokedAt=$POST_AT"
[ "$POST_REVOKED" = "true" ] || fail "entry must be revoked:true after sidecar, got '$POST_REVOKED'"
[ "$POST_REASON" = "dmca" ] || fail "revokedReason must be 'dmca', got '$POST_REASON'"
[ "$POST_AT" = "$NOW_SIGN" ] || fail "revokedAt must be '$NOW_SIGN', got '$POST_AT'"
record "revocation via revocation.json sidecar" "OK revoked:true (reason=dmca) in manifest"

# ── (g) Summary table ──────────────────────────────────────────────────────────
section "(g) Summary — pipeline outcomes"
printf '%-48s | %s\n' "STAGE" "OUTCOME"
printf '%-48s-+-%s\n' "------------------------------------------------" "------------------------------------------"
printf '%-48s | %s\n' "commercial DAT -> denylist build" "OK (crc32/md5/sha1 ingested)"
for r in "${RESULTS[@]}"; do
  printf '%-48s | %s\n' "${r%%|*}" "${r#*|}"
done

echo ""
echo "E2E DEMO PASSED: all assertions held."
