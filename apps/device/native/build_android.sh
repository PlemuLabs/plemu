#!/usr/bin/env bash
#
# build_android.sh — cross-compile the libretro host shim for Android arm64-v8a
# (the PlaySolana PSG1 target) with Unity's BUNDLED NDK. Pure NDK; no Unity, no
# CMake. Emits libretro_host.so (ELF aarch64), which Unity packages as a native
# plugin so [DllImport("libretro_host")] resolves on-device.
#
# Same sources + flags as build_editor.sh (C11, -Wall -Wextra -Werror -O2 -fPIC,
# -fvisibility=hidden so only the RH_API rh_* symbols export), differing only in
# the toolchain (NDK aarch64 clang), the output extension (.so, not .dylib), and
# the link recipe (ELF -shared linking libc/libdl from the NDK sysroot).
#
# The shim's logging is a caller-supplied C function pointer (rh_log_fn), NOT a
# direct __android_log call, so no -llog is required; it needs only libc + libdl
# (dlopen/dlsym/dlclose live in libdl on Bionic). NDK clang links both from its
# sysroot automatically. -Wl,-z,max-page-size=16384 = 16 KB page alignment
# (forward-compat with 16 KB-page arm64 Android devices).
#
# There is no on-device smoke harness here (no PSG1 hardware): the C smoke proof
# runs on the macOS editor path (build_editor.sh / build_realcore.sh). On-device
# validation is DevKit/RK3588-gated — a later task. This script's contract is:
# produce an ELF aarch64 libretro_host.so and fail loudly otherwise.
#
# Output: apps/device/native/build/android/arm64-v8a/libretro_host.so (gitignored).
# Idempotent. Exits nonzero on any failure.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOST_INC="${HERE}/libretro_host/include"
HOST_SRC="${HERE}/libretro_host/src"

# Android target. minSdk is 30 (BuildScript.BuildPSG1); use the API 30 clang.
ANDROID_API="${ANDROID_API:-30}"
ANDROID_ABI="${ANDROID_ABI:-arm64-v8a}"
NDK_TARGET="${NDK_TARGET:-aarch64-linux-android}"

# Unity bundles the NDK; no standalone install. Allow override via env.
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.19f1-arm64}"
NDK_ROOT="${NDK_ROOT:-${UNITY_EDITOR}/PlaybackEngines/AndroidPlayer/NDK}"

BUILD="${HERE}/build/android/${ANDROID_ABI}"
OBJ="${BUILD}/obj"

# --- locate the bundled NDK arm64 clang -------------------------------------
NDK_BIN="${NDK_ROOT}/toolchains/llvm/prebuilt/darwin-x86_64/bin"
if [ ! -d "${NDK_BIN}" ]; then
    ALT="$(find "${NDK_ROOT}/toolchains/llvm/prebuilt" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | head -1)"
    [ -n "${ALT}" ] && NDK_BIN="${ALT}/bin"
fi
CC="${CC:-${NDK_BIN}/${NDK_TARGET}${ANDROID_API}-clang}"

if [ ! -x "${CC}" ]; then
    echo "ERROR: NDK arm64 clang not found/executable: ${CC}" >&2
    echo "       Check UNITY_EDITOR/NDK_ROOT. Bundled NDK expected at:" >&2
    echo "       ${UNITY_EDITOR}/PlaybackEngines/AndroidPlayer/NDK" >&2
    exit 1
fi

# C11, all warnings as errors, optimized, position-independent (per brief).
CFLAGS="-std=c11 -Wall -Wextra -Werror -O2 -fPIC -fvisibility=hidden"

echo "==> NDK clang: ${CC}"
"${CC}" --version | head -1
echo "==> target: ${NDK_TARGET}${ANDROID_API} (${ANDROID_ABI})"

# Clean only OUR artifacts, not the whole ABI dir: the SameBoy core .so is
# emitted to this same build/android/<abi>/ folder by build_sameboy_android.sh,
# and stage-android.sh may build the core first. rm -rf on the ABI dir would
# wipe it. So remove just our obj tree + our .so.
rm -rf "${OBJ}"
rm -f "${BUILD}/libretro_host.so"
mkdir -p "${OBJ}"

# --- 1. compile each single-responsibility translation unit -----------------
HOST_UNITS=(libretro_host environment video audio input ring_buffer dylib)
HOST_OBJS=()
for u in "${HOST_UNITS[@]}"; do
    obj="${OBJ}/host_${u}.o"
    echo "==> CC  ${u}.c"
    "${CC}" ${CFLAGS} -I"${HOST_INC}" -I"${HOST_SRC}" \
        -c "${HOST_SRC}/${u}.c" -o "${obj}"
    HOST_OBJS+=("${obj}")
done

# --- 2. link libretro_host.so ----------------------------------------------
# ELF shared object. libdl (dlopen/dlsym/dlclose) + libc come from the NDK
# sysroot automatically; -Wl,-z,max-page-size=16384 for 16 KB-page arm64.
echo "==> LD  libretro_host.so"
"${CC}" -shared \
    -Wl,-soname,libretro_host.so \
    -Wl,-z,max-page-size=16384 \
    -o "${BUILD}/libretro_host.so" \
    "${HOST_OBJS[@]}" \
    -ldl

# --- 3. verify it is an ELF aarch64 shared object (the gate) -----------------
echo "==> verifying arch of libretro_host.so"
file "${BUILD}/libretro_host.so" || true
if ! file "${BUILD}/libretro_host.so" | grep -q "ELF 64-bit"; then
    echo "ERROR: libretro_host.so is not an ELF 64-bit object (cross-compile failed)." >&2
    exit 1
fi
if ! file "${BUILD}/libretro_host.so" | grep -Eq "aarch64|ARM aarch64"; then
    echo "ERROR: libretro_host.so is not aarch64 (wrong target)." >&2
    exit 1
fi

echo "==> build_android.sh OK: ${BUILD}/libretro_host.so"
