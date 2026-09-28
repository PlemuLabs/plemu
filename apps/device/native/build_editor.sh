#!/usr/bin/env bash
#
# build_editor.sh — macOS arm64 (Apple clang) build of the software-rendered
# libretro host shim, the stub test core, and the rh_smoke harness; then runs
# rh_smoke as the verification gate.
#
# Plain clang commands only (no CMake — not installed on this box). Artifacts go
# to native/build/ (gitignored). Script exits nonzero if any step, or the smoke
# harness, fails.
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="${HERE}/build"
HOST_INC="${HERE}/libretro_host/include"
HOST_SRC="${HERE}/libretro_host/src"
STUB_SRC="${HERE}/stubcore/src"
SMOKE_SRC="${HERE}/smoke"

CC="${CC:-clang}"
# C11, all warnings as errors, optimized, position-independent (per brief).
CFLAGS="-std=c11 -Wall -Wextra -Werror -O2 -fPIC"
# Apple arm64 target; override ARCH=x86_64 on Intel Macs if ever needed.
ARCH="${ARCH:-arm64}"
ARCHFLAGS="-arch ${ARCH}"

echo "==> clang: $(${CC} --version | head -1)"
echo "==> target arch: ${ARCH}"
rm -rf "${BUILD}"
mkdir -p "${BUILD}"

# --- 1. libretro_host.dylib ------------------------------------------------
# Compile each single-responsibility translation unit, then link the dylib.
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

# --- 2. stub core dylib ----------------------------------------------------
# A libretro core is a plain shared object exporting the retro_* symbols; the
# host dlopen's it at runtime. No link against the host.
echo "==> CC/LD stub_libretro.dylib"
${CC} ${CFLAGS} ${ARCHFLAGS} -dynamiclib \
    -I"${HOST_INC}" \
    -o "${BUILD}/stub_libretro.dylib" \
    "${STUB_SRC}/stub_libretro.c"

# --- 3. rh_smoke harness ---------------------------------------------------
# Links the public host ABI; rpath @loader_path finds libretro_host.dylib in the
# same build/ dir at runtime.
echo "==> CC/LD rh_smoke"
${CC} ${CFLAGS} ${ARCHFLAGS} \
    -I"${HOST_INC}" \
    -o "${BUILD}/rh_smoke" \
    "${SMOKE_SRC}/rh_smoke.c" \
    -L"${BUILD}" -lretro_host \
    -Wl,-rpath,@loader_path

# --- 4. run the smoke harness (the verification gate) ----------------------
echo "==> RUN rh_smoke"
"${BUILD}/rh_smoke" "${BUILD}/stub_libretro.dylib"
echo "==> build_editor.sh OK"
