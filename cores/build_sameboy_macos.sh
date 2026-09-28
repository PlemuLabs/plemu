#!/usr/bin/env bash
#
# build_sameboy_macos.sh — fetch + build the SameBoy libretro core for
# macOS arm64 (the Unity editor target). This is the M3 risk-retirement build:
# it proves a REAL libretro core can be produced and loaded by the native shim.
#
# What it does:
#   1. Shallow-clone https://github.com/LIJI32/SameBoy at the PINNED commit
#      into a scratch dir (gitignored; NOT committed).
#   2. Build its boot ROMs from source with rgbds (rgbasm/rgblink/rgbgfx),
#      then build the libretro core (platform=osx) as an arm64 dylib.
#   3. Emit sameboy_libretro.dylib into apps/device/native/build/ (gitignored).
#
# Android arm64 is a LATER task (M5); this script is macOS-only on purpose.
#
# Prerequisites (macOS arm64):
#   - Apple clang (xcode-select --install)
#   - rgbds        (brew install rgbds)  — required to assemble the GB boot ROMs
#   - git, hexdump (system)
#
# Pin: SameBoy v1.0.3, commit 208ba4afabffab9edde416f2dbb8ae459e34adb8 (MIT/Expat).
# Kept consistent with cores/cores.build.json + cores/expected-licenses.lock.
#
# Output: $OUT_DIR/sameboy_libretro.dylib  (default: apps/device/native/build)
# Idempotent: re-running rebuilds cleanly. Exits nonzero on any failure.
set -euo pipefail

# --- config -----------------------------------------------------------------
SAMEBOY_REPO="${SAMEBOY_REPO:-https://github.com/LIJI32/SameBoy.git}"
SAMEBOY_COMMIT="${SAMEBOY_COMMIT:-208ba4afabffab9edde416f2dbb8ae459e34adb8}" # v1.0.3
ARCH="${ARCH:-arm64}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${HERE}/.." && pwd)"
OUT_DIR="${OUT_DIR:-${REPO_ROOT}/apps/device/native/build}"
# Source is fetched into a gitignored scratch cache OUTSIDE the shim's build/ dir
# (so `build_editor.sh`'s `rm -rf build` never has to delete a nested git repo).
# See .gitignore: cores/.cache/ is ignored; nothing here is committed.
SRC_ROOT="${SRC_ROOT:-${REPO_ROOT}/cores/.cache}"
SRC_DIR="${SRC_ROOT}/sameboy"
ARTIFACT="sameboy_libretro.dylib"

echo "==> SameBoy libretro build (macOS ${ARCH})"
echo "    repo   : ${SAMEBOY_REPO}"
echo "    commit : ${SAMEBOY_COMMIT}"
echo "    out    : ${OUT_DIR}/${ARTIFACT}"

# --- prerequisite checks ----------------------------------------------------
if ! command -v rgbasm >/dev/null 2>&1; then
    echo "ERROR: rgbds not found (need rgbasm/rgblink/rgbgfx to build the GB boot ROMs)." >&2
    echo "       Install with: brew install rgbds" >&2
    exit 1
fi
if ! command -v git >/dev/null 2>&1; then
    echo "ERROR: git not found." >&2
    exit 1
fi
echo "==> rgbds: $(rgbasm --version)"

# --- 1. fetch pinned source -------------------------------------------------
mkdir -p "${SRC_ROOT}" "${OUT_DIR}"
if [ ! -d "${SRC_DIR}/.git" ]; then
    echo "==> cloning SameBoy"
    rm -rf "${SRC_DIR}"
    git clone "${SAMEBOY_REPO}" "${SRC_DIR}"
fi
echo "==> checkout ${SAMEBOY_COMMIT}"
git -C "${SRC_DIR}" fetch --depth 1 origin "${SAMEBOY_COMMIT}" 2>/dev/null || git -C "${SRC_DIR}" fetch origin
git -C "${SRC_DIR}" checkout -q "${SAMEBOY_COMMIT}"
ACTUAL="$(git -C "${SRC_DIR}" rev-parse HEAD)"
if [ "${ACTUAL}" != "${SAMEBOY_COMMIT}" ]; then
    echo "ERROR: checked-out commit ${ACTUAL} != pinned ${SAMEBOY_COMMIT}" >&2
    exit 1
fi

# --- 2. build boot ROMs (rgbds) --------------------------------------------
# SameBoy assembles its GB/GBC boot ROMs from .asm sources; the libretro core
# bakes them in (falls back to the built-in image when the system dir has none),
# so they MUST exist before the core links.
echo "==> make bootroms (rgbds)"
make -C "${SRC_DIR}" bootroms -j"$(sysctl -n hw.ncpu)"

# --- 3. build the libretro core (arm64 dylib) ------------------------------
# Force arm64 explicitly: the osx target only sets -arch under UNIVERSAL=1, and
# we want a deterministic single-arch arm64 dylib regardless of the host default.
# We pass ARCHFLAGS ONLY (the osx block does `CFLAGS += $(ARCHFLAGS)` and
# `LDFLAGS += $(ARCHFLAGS)`); overriding CFLAGS/LDFLAGS on the command line would
# clobber the Makefile's INCFLAGS (-I.. for <Core/gb.h>) and break the build.
#
# Build SERIALLY (no -j): SameBoy's libretro Makefile generates the *_boot.c
# arrays via shell `echo` appends that race against compilation under parallel
# make. The bootroms above are already built, so this step is fast even serial.
echo "==> make libretro (platform=osx, -arch ${ARCH}, serial)"
make -C "${SRC_DIR}/libretro" \
    platform=osx \
    "ARCHFLAGS=-arch ${ARCH}" \
    BOOTROMS_DIR="${SRC_DIR}/build/bin/BootROMs" \
    BIN="${SRC_DIR}/build/bin"

# --- 4. locate + emit the dylib --------------------------------------------
BUILT="$(find "${SRC_DIR}" -name "${ARTIFACT}" -type f | head -1)"
if [ -z "${BUILT}" ]; then
    echo "ERROR: build finished but ${ARTIFACT} not found under ${SRC_DIR}" >&2
    exit 1
fi
cp -f "${BUILT}" "${OUT_DIR}/${ARTIFACT}"

echo "==> verifying arch of ${ARTIFACT}"
if command -v lipo >/dev/null 2>&1; then
    lipo -info "${OUT_DIR}/${ARTIFACT}" || true
fi
file "${OUT_DIR}/${ARTIFACT}" || true

echo "==> OK: ${OUT_DIR}/${ARTIFACT}"
