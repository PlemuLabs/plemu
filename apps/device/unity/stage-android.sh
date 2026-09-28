#!/usr/bin/env bash
#
# stage-android.sh — cross-compile the native shim + SameBoy core for Android
# arm64-v8a (Unity's bundled NDK) and stage both as native plugins into the
# Unity project so BuildScript.BuildPSG1 packages them into the APK's
# nativeLibraryDir (arm64-v8a jniLibs).
#
# Staging layout (mirrors the macOS layout, but for the device target):
#   Assets/Plugins/Android/libs/arm64-v8a/libretro_host.so
#       -> the shim, a native plugin (Android/ARM64) so
#          [DllImport("libretro_host")] resolves on-device. Its committed .meta
#          configures the import; the .so is gitignored (rebuilt here).
#   Assets/Plugins/Android/libs/arm64-v8a/libsameboy_libretro_android.so
#       -> the core. Android >=10 forbids dlopen from app data dirs, so the core
#          MUST ship in the APK's native lib dir; staging it as a native plugin
#          lands it in nativeLibraryDir, where CoreRegistry.ResolveCorePath
#          points the shim's dlopen. The `lib` prefix is REQUIRED for Unity/
#          Gradle to extract it (Unity ignores non-lib*.so in a plugin dir).
#          The build script emits `sameboy_libretro_android.so`; we add the
#          `lib` prefix on copy. Committed .meta; the .so is gitignored.
#
# Content (manifest-beta + artifact) is already committed in StreamingAssets and
# ships in the APK automatically — but see the TODO at the bottom: on Android
# StreamingAssets is INSIDE the APK (jar), so File.IO reads won't work on-device;
# that needs UnityWebRequest/copy-to-persistentDataPath (a later task). For THIS
# build the APK only needs to compile + package, not run on hardware.
#
# A fresh clone runs this ONCE before a PSG1 build. Idempotent. Requires: the
# bundled Unity Android NDK, rgbds (brew install rgbds), git; first run fetches
# the core source (network).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${HERE}/../../.." && pwd)"
NATIVE="${HERE}/../native"
CORE_BUILD="${REPO_ROOT}/cores/build_sameboy_android.sh"

ANDROID_ABI="${ANDROID_ABI:-arm64-v8a}"
NATIVE_BUILD="${NATIVE}/build/android/${ANDROID_ABI}"

PLUGINS_ANDROID="${HERE}/Assets/Plugins/Android/libs/${ANDROID_ABI}"

SHIM_SO="${NATIVE_BUILD}/libretro_host.so"
CORE_SO="${NATIVE_BUILD}/sameboy_libretro_android.so"

# --- 1. build the SameBoy core (arm64-v8a) FIRST ---------------------------
# (build_android.sh cleans only its own artifacts, so order-independent, but the
# core build is the slow/network one — do it first for a clear failure signal.)
echo "==> building SameBoy core (Android ${ANDROID_ABI})"
bash "${CORE_BUILD}"

# --- 2. build the native shim (arm64-v8a) ----------------------------------
echo "==> building native shim (Android ${ANDROID_ABI})"
bash "${NATIVE}/build_android.sh"

[ -f "${SHIM_SO}" ] || { echo "ERROR: shim .so missing: ${SHIM_SO}" >&2; exit 1; }
[ -f "${CORE_SO}" ] || { echo "ERROR: core .so missing: ${CORE_SO}" >&2; exit 1; }

mkdir -p "${PLUGINS_ANDROID}"

# --- 3. stage both as native plugins ---------------------------------------
echo "==> staging shim -> ${PLUGINS_ANDROID}/libretro_host.so"
cp "${SHIM_SO}" "${PLUGINS_ANDROID}/libretro_host.so"

# Add the mandatory `lib` prefix so Unity/Gradle extract it into nativeLibraryDir.
echo "==> staging core -> ${PLUGINS_ANDROID}/libsameboy_libretro_android.so"
cp "${CORE_SO}" "${PLUGINS_ANDROID}/libsameboy_libretro_android.so"

echo "==> stage-android OK"
echo "    shim: ${PLUGINS_ANDROID}/libretro_host.so (Android/ARM64 plugin, .meta committed)"
echo "    core: ${PLUGINS_ANDROID}/libsameboy_libretro_android.so (Android/ARM64 plugin, .meta committed)"
file "${PLUGINS_ANDROID}/libretro_host.so" || true
file "${PLUGINS_ANDROID}/libsameboy_libretro_android.so" || true
