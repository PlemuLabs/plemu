#!/usr/bin/env bash
#
# build_sameboy_android.sh — cross-compile the SameBoy libretro core for
# Android arm64-v8a (the PlaySolana PSG1 target) using Unity's BUNDLED NDK.
#
# This is the M5 core half: it proves the SAME pinned SameBoy commit that built
# cleanly for macOS arm64 (build_sameboy_macos.sh) also cross-compiles to an ELF
# aarch64 shared object the shim can dlopen on-device. On-hardware execution is
# DevKit/RK3588-gated (we have no PSG1); this script only produces the .so.
#
# What it does:
#   1. Reuse the SameBoy source already pinned + fetched by the macOS build into
#      cores/.cache/sameboy (shallow-clone it there if absent), at the SAME
#      commit 208ba4afabffab9edde416f2dbb8ae459e34adb8.
#   2. Build the GB/GBC boot ROMs from source with rgbds (HOST tools; the core
#      bakes them in, so they must exist before the core links) — identical to
#      the macOS path.
#   3. Cross-compile the libretro core with the bundled NDK arm64 clang. The
#      pinned Makefile has NO platform=android block, so we drive the generic
#      `platform=unix` recipe (`-fPIC -shared --version-script=link.T
#      --no-undefined`) with CC overridden to the NDK aarch64 clang; NDK clang
#      pulls libc/libm/libdl from its own sysroot, satisfying --no-undefined.
#   4. Apply -Wl,-z,max-page-size=16384 (16 KB page alignment — forward-compat
#      with 16 KB-page arm64 Android devices) and emit the .so to
#      apps/device/native/build/android/arm64-v8a/sameboy_libretro_android.so.
#
# Output is gitignored (rebuilt here); only this script is committed.
#
# Toolchain: Unity's bundled Android NDK r27c (27.2.12479018). No separate NDK
# install. Override NDK_ROOT / API / ARCH env vars if needed.
#
# Prereqs (macOS arm64 host): rgbds (brew install rgbds), git, hexdump (system),
# and the bundled Unity Android NDK. First run fetches SameBoy source (network).
#
# Idempotent. Exits nonzero on any failure.
set -euo pipefail

# --- config -----------------------------------------------------------------
SAMEBOY_REPO="${SAMEBOY_REPO:-https://github.com/LIJI32/SameBoy.git}"
SAMEBOY_COMMIT="${SAMEBOY_COMMIT:-208ba4afabffab9edde416f2dbb8ae459e34adb8}" # v1.0.3

# Android target. minSdk is 30 (BuildScript.BuildPSG1); use API 30 clang.
ANDROID_API="${ANDROID_API:-30}"
ANDROID_ABI="${ANDROID_ABI:-arm64-v8a}"
NDK_TARGET="${NDK_TARGET:-aarch64-linux-android}"

# Unity bundles the NDK/SDK/JDK; no standalone install. Allow override.
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.19f1-arm64}"
NDK_ROOT="${NDK_ROOT:-${UNITY_EDITOR}/PlaybackEngines/AndroidPlayer/NDK}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${HERE}/.." && pwd)"
OUT_DIR="${OUT_DIR:-${REPO_ROOT}/apps/device/native/build/android/${ANDROID_ABI}}"
SRC_ROOT="${SRC_ROOT:-${REPO_ROOT}/cores/.cache}"
SRC_DIR="${SRC_ROOT}/sameboy"
ARTIFACT="sameboy_libretro_android.so"

echo "==> SameBoy libretro build (Android ${ANDROID_ABI}, API ${ANDROID_API})"
echo "    repo   : ${SAMEBOY_REPO}"
echo "    commit : ${SAMEBOY_COMMIT}"
echo "    ndk    : ${NDK_ROOT}"
echo "    out    : ${OUT_DIR}/${ARTIFACT}"

# --- locate the bundled NDK arm64 clang -------------------------------------
HOST_TAG="darwin-x86_64" # NDK prebuilt tag on macOS (works on Apple Silicon via Rosetta-less arm64 host tag fallback)
NDK_BIN="${NDK_ROOT}/toolchains/llvm/prebuilt/${HOST_TAG}/bin"
if [ ! -d "${NDK_BIN}" ]; then
    # Some NDKs ship a darwin-arm64 prebuilt; fall back to whatever single prebuilt exists.
    ALT="$(find "${NDK_ROOT}/toolchains/llvm/prebuilt" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | head -1)"
    if [ -n "${ALT}" ]; then
        NDK_BIN="${ALT}/bin"
    fi
fi
NDK_CC="${NDK_BIN}/${NDK_TARGET}${ANDROID_API}-clang"
NDK_AR="${NDK_BIN}/llvm-ar"

if [ ! -x "${NDK_CC}" ]; then
    echo "ERROR: NDK arm64 clang not found/executable: ${NDK_CC}" >&2
    echo "       Check UNITY_EDITOR/NDK_ROOT. Bundled NDK expected at:" >&2
    echo "       ${UNITY_EDITOR}/PlaybackEngines/AndroidPlayer/NDK" >&2
    exit 1
fi
echo "==> NDK clang: ${NDK_CC}"
"${NDK_CC}" --version | head -1

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

# --- 1. fetch pinned source (shared cache with the macOS build) -------------
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

# --- 2. build boot ROMs (rgbds, HOST tools) ---------------------------------
# The libretro core bakes the GB/GBC boot ROMs in (falls back to the built-in
# image when the system dir has none), so they MUST exist before the core links.
# Built with host rgbds — unaffected by the NDK cross-toolchain.
echo "==> make bootroms (rgbds)"
make -C "${SRC_DIR}" bootroms -j"$(sysctl -n hw.ncpu)"

# --- 3. cross-compile the libretro core (arm64-v8a .so) ---------------------
# platform=unix drives: TARGET=sameboy_libretro.so, fpic=-fPIC,
# SHARED=-shared --version-script=link.T --no-undefined. Override CC to the NDK
# arm64 clang (LD=$(CC) in the Makefile, so link uses it too). Inject 16 KB page
# alignment via LDFLAGS. Build SERIALLY (no -j): the *_boot.c arrays are
# generated via shell `echo` appends that race under parallel make; bootroms are
# already built so this is fast even serial.
#
# NOTE: passing LDFLAGS= on the make command line OVERRIDES the Makefile's
# `LDFLAGS += $(LIBM)` (line 341, LIBM=-lm). On Android/Bionic the math funcs
# (cos/sin/pow/exp2 in apu.c/sgb.c/display.c) live in libm.so — a real separate
# lib, unlike glibc where they fold into libc — so --no-undefined fails to link
# without -lm. We therefore carry -lm ourselves in the LDFLAGS override.
echo "==> make libretro (platform=unix, NDK CC=${NDK_TARGET}${ANDROID_API}, serial)"
# Clean any stale objects from a prior (e.g. macOS) build so we don't relink
# host-arch objects into the Android .so.
make -C "${SRC_DIR}/libretro" platform=unix clean 2>/dev/null || true
rm -rf "${SRC_DIR}/build/obj/libretro"

make -C "${SRC_DIR}/libretro" \
    platform=unix \
    CC="${NDK_CC}" \
    AR="${NDK_AR}" \
    LDFLAGS="-lm -Wl,-z,max-page-size=16384" \
    BOOTROMS_DIR="${SRC_DIR}/build/bin/BootROMs" \
    BIN="${SRC_DIR}/build/bin"

# --- 4. locate + emit the .so -----------------------------------------------
BUILT="$(find "${SRC_DIR}" -name 'sameboy_libretro.so' -type f | head -1)"
if [ -z "${BUILT}" ]; then
    echo "ERROR: build finished but sameboy_libretro.so not found under ${SRC_DIR}" >&2
    exit 1
fi
cp -f "${BUILT}" "${OUT_DIR}/${ARTIFACT}"

echo "==> verifying arch of ${ARTIFACT}"
file "${OUT_DIR}/${ARTIFACT}" || true

# Hard gate: must be an ELF aarch64 shared object, not a host Mach-O.
if ! file "${OUT_DIR}/${ARTIFACT}" | grep -q "ELF 64-bit"; then
    echo "ERROR: ${ARTIFACT} is not an ELF 64-bit object (cross-compile failed)." >&2
    exit 1
fi
if ! file "${OUT_DIR}/${ARTIFACT}" | grep -Eq "aarch64|ARM aarch64"; then
    echo "ERROR: ${ARTIFACT} is not aarch64 (wrong target)." >&2
    exit 1
fi

echo "==> OK: ${OUT_DIR}/${ARTIFACT}"
