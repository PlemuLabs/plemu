#!/usr/bin/env bash
#
# stage-editor-plugins.sh — build the native shim + SameBoy core (via
# build_realcore.sh, which also runs the C smoke gate) and stage the two dylibs
# into the Unity editor project so the emulation runtime can P/Invoke and dlopen
# them on macOS arm64.
#
# Staging layout:
#   Assets/Plugins/macOS/libretro_host.dylib
#       -> the shim, imported as a native plugin (Editor + Standalone macOS,
#          arm64) so [DllImport("libretro_host")] resolves. Its committed .meta
#          configures the import; the dylib itself is gitignored (rebuilt here).
#   Assets/StreamingAssets/Cores/Editor/sameboy_libretro.dylib
#       -> the core, which is DATA the shim dlopen's by path (CoreRegistry
#          resolves it), NOT a Unity native plugin. Lives in StreamingAssets.
#
# A fresh clone runs this ONCE (plus stage-content.sh) before opening the editor
# or running PlayMode tests. Idempotent. Requires: Apple clang, rgbds
# (brew install rgbds), git; first run fetches the core source (network).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NATIVE="${HERE}/../native"
NATIVE_BUILD="${NATIVE}/build"

PLUGINS_MAC="${HERE}/Assets/Plugins/macOS"
CORES_EDITOR="${HERE}/Assets/StreamingAssets/Cores/Editor"

SHIM_DYLIB="${NATIVE_BUILD}/libretro_host.dylib"
CORE_DYLIB="${NATIVE_BUILD}/sameboy_libretro.dylib"

echo "==> building native shim + core (build_realcore.sh, runs the C smoke gate)"
bash "${NATIVE}/build_realcore.sh"

[ -f "${SHIM_DYLIB}" ] || { echo "ERROR: shim dylib missing: ${SHIM_DYLIB}" >&2; exit 1; }
[ -f "${CORE_DYLIB}" ] || { echo "ERROR: core dylib missing: ${CORE_DYLIB}" >&2; exit 1; }

mkdir -p "${PLUGINS_MAC}" "${CORES_EDITOR}"

echo "==> staging shim  -> ${PLUGINS_MAC}/libretro_host.dylib"
cp "${SHIM_DYLIB}" "${PLUGINS_MAC}/libretro_host.dylib"

echo "==> staging core  -> ${CORES_EDITOR}/sameboy_libretro.dylib"
cp "${CORE_DYLIB}" "${CORES_EDITOR}/sameboy_libretro.dylib"

echo "==> stage-editor-plugins OK"
echo "    shim: ${PLUGINS_MAC}/libretro_host.dylib (native plugin, .meta committed)"
echo "    core: ${CORES_EDITOR}/sameboy_libretro.dylib (StreamingAssets data)"
