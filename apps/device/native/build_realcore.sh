#!/usr/bin/env bash
#
# build_realcore.sh — macOS arm64 (Apple clang) build of the software-rendered
# libretro host shim, a REAL libretro core (SameBoy), and the rh_realcore_smoke
# harness; then runs the harness against the real core + a real, license-verified
# homebrew Game Boy ROM as the verification gate.
#
# This is the M3 risk-retirement: it proves the shim carries a REAL emulator core
# running a REAL game on macOS arm64, before the Unity runtime (M4) consumes it.
# The existing stub gate (build_editor.sh -> rh_smoke) stays separate and intact.
#
# Plain clang commands only (no CMake). The core itself is built by
# cores/build_sameboy_macos.sh (fetch pinned commit -> rgbds boot ROMs -> dylib).
# Artifacts go to native/build/ (gitignored). Exits nonzero if any step, or the
# smoke harness, fails.
#
# Prereqs: Apple clang, rgbds (brew install rgbds), git. The core source is
# fetched by the core build script the first time (needs network).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="${HERE}/build"
HOST_INC="${HERE}/libretro_host/include"
HOST_SRC="${HERE}/libretro_host/src"
STUB_SRC="${HERE}/stubcore/src"
SMOKE_SRC="${HERE}/smoke"
TESTDATA="${HERE}/testdata"
REPO_ROOT="$(cd "${HERE}/../../.." && pwd)"
CORE_BUILD="${REPO_ROOT}/cores/build_sameboy_macos.sh"

CORE_DYLIB="${BUILD}/sameboy_libretro.dylib"
GAME_ROM="${TESTDATA}/libbet.gb"

CC="${CC:-clang}"
# C11, all warnings as errors, optimized, position-independent (per brief).
CFLAGS="-std=c11 -Wall -Wextra -Werror -O2 -fPIC"
# Apple arm64 target; override ARCH=x86_64 on Intel Macs if ever needed.
ARCH="${ARCH:-arm64}"
ARCHFLAGS="-arch ${ARCH}"

echo "==> clang: $(${CC} --version | head -1)"
echo "==> target arch: ${ARCH}"
mkdir -p "${BUILD}"

# --- 0. sanity: fixture ROM present ---------------------------------------
if [ ! -f "${GAME_ROM}" ]; then
    echo "ERROR: test ROM not found at ${GAME_ROM}" >&2
    echo "       See ${TESTDATA}/README.md for provenance." >&2
    exit 1
fi

# --- 1. build the REAL core (SameBoy) via the pinned build script ----------
# Emits ${BUILD}/sameboy_libretro.dylib. Idempotent; first run fetches source.
echo "==> building real core (SameBoy) via cores/build_sameboy_macos.sh"
OUT_DIR="${BUILD}" bash "${CORE_BUILD}"
if [ ! -f "${CORE_DYLIB}" ]; then
    echo "ERROR: core build did not produce ${CORE_DYLIB}" >&2
    exit 1
fi

# --- 2. libretro_host.dylib ------------------------------------------------
# -fvisibility=hidden so only RH_API-marked symbols are exported (narrow ABI).
HOST_UNITS=(libretro_host environment video audio input ring_buffer dylib)
HOST_OBJS=()
for u in "${HOST_UNITS[@]}"; do
    obj="${BUILD}/host_${u}.o"
    echo "==> CC  ${u}.c"
    ${CC} ${CFLAGS} -fvisibility=hidden ${ARCHFLAGS} -I"${HOST_INC}" -I"${HOST_SRC}" \
        -c "${HOST_SRC}/${u}.c" -o "${obj}"
    HOST_OBJS+=("${obj}")
done

echo "==> LD  libretro_host.dylib"
${CC} ${ARCHFLAGS} -dynamiclib \
    -install_name "@rpath/libretro_host.dylib" \
    -o "${BUILD}/libretro_host.dylib" \
    "${HOST_OBJS[@]}"

# --- 3. rh_realcore_smoke harness ------------------------------------------
# Links the public host ABI; rpath @loader_path finds libretro_host.dylib in the
# same build/ dir at runtime.
echo "==> CC/LD rh_realcore_smoke"
${CC} ${CFLAGS} ${ARCHFLAGS} \
    -I"${HOST_INC}" \
    -o "${BUILD}/rh_realcore_smoke" \
    "${SMOKE_SRC}/rh_realcore_smoke.c" \
    -L"${BUILD}" -lretro_host \
    -Wl,-rpath,@loader_path

# --- 4. run the harness (the verification gate) ----------------------------
echo "==> RUN rh_realcore_smoke (SameBoy + libbet.gb)"
"${BUILD}/rh_realcore_smoke" "${CORE_DYLIB}" "${GAME_ROM}"
echo "==> build_realcore.sh OK"
