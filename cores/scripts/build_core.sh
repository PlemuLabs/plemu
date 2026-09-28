#!/usr/bin/env bash
# build_core.sh — Build a single libretro core for Android arm64.
#
# Usage:
#   ./scripts/build_core.sh <coreId>
#
# Example:
#   ./scripts/build_core.sh sameboy
#
# Prerequisites:
#   - Android NDK r27 on PATH or ANDROID_NDK_HOME set.
#   - cores/cores.build.json present and the coreId pinned (commit != "TBD").
#   - The core source already cloned at sources/<coreId>/ at the pinned commit.
#
# Output:
#   artifacts/<coreId>_libretro_android.so
#
# TODO(release): This script is a skeleton. It documents the intended invocation
# for each build system; actual integration requires:
#   1. Pin all commits in cores.build.json (currently all "TBD").
#   2. Set up CI with NDK r27 and cloned sources.
#   3. Validate each core's actual Makefile/cmake target names against the buildHint.
#   4. Add signing / strip / verify steps.

set -euo pipefail

CORE_ID="${1:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_JSON="$REPO_ROOT/cores/cores.build.json"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
SOURCES_DIR="$REPO_ROOT/sources"

# ── Guards ────────────────────────────────────────────────────────────────────

if [ -z "$CORE_ID" ]; then
  echo "Usage: $0 <coreId>" >&2
  exit 1
fi

if [ ! -f "$BUILD_JSON" ]; then
  echo "ERROR: cores.build.json not found at $BUILD_JSON" >&2
  exit 1
fi

# Verify NDK is available.
if [ -z "${ANDROID_NDK_HOME:-}" ] && ! command -v ndk-build &>/dev/null; then
  echo "ERROR: NDK not installed. Set ANDROID_NDK_HOME or ensure ndk-build is on PATH." >&2
  echo "Required: Android NDK r27 (https://developer.android.com/ndk/downloads)" >&2
  exit 1
fi

# Read the core entry from cores.build.json.
CORE_ENTRY=$(python3 -c "
import json, sys
data = json.load(open('$BUILD_JSON'))
cores = [c for c in data['cores'] if c['coreId'] == '$CORE_ID']
if not cores:
    print('NOT_FOUND', file=sys.stderr)
    sys.exit(1)
c = cores[0]
print(c['commit'])
print(c['buildHint'])
print(c['artifact'])
" 2>/dev/null) || {
  echo "ERROR: coreId '$CORE_ID' not found in $BUILD_JSON" >&2
  exit 1
}

COMMIT=$(echo "$CORE_ENTRY" | sed -n '1p')
BUILD_HINT=$(echo "$CORE_ENTRY" | sed -n '2p')
ARTIFACT=$(echo "$CORE_ENTRY" | sed -n '3p')

# Guard: core must be pinned before building.
if [ "$COMMIT" = "TBD" ]; then
  echo "ERROR: coreId '$CORE_ID' is not pinned (commit = TBD)." >&2
  echo "Pin the commit in cores/cores.build.json and cores/expected-licenses.lock" >&2
  echo "via a reviewed PR before building. See docs/adding-a-core.md." >&2
  exit 1
fi

if [ "$BUILD_HINT" = "TBD-verify" ]; then
  echo "ERROR: buildHint for '$CORE_ID' is TBD-verify." >&2
  echo "Verify the correct build invocation and update cores.build.json." >&2
  exit 1
fi

SOURCE_DIR="$SOURCES_DIR/$CORE_ID"
if [ ! -d "$SOURCE_DIR" ]; then
  echo "ERROR: Source directory not found: $SOURCE_DIR" >&2
  echo "Clone the core source at the pinned commit before building:" >&2
  echo "  mkdir -p $SOURCE_DIR" >&2
  echo "  git -C $SOURCE_DIR clone <repo> . && git -C $SOURCE_DIR checkout $COMMIT" >&2
  exit 1
fi

mkdir -p "$ARTIFACTS_DIR"

# ── Build ─────────────────────────────────────────────────────────────────────

echo "Building coreId=$CORE_ID commit=$COMMIT"
echo "Build hint: $BUILD_HINT"
echo "Source: $SOURCE_DIR"

# LINKER FLAG: all cores must be built with -Wl,-z,max-page-size=16384 for
# Android 15+ 16 KB page-size compatibility. Pass via LDFLAGS or per-build-system
# mechanism (see inline notes per system below).
#
# TODO(release): Confirm each core's Makefile/cmake respects LDFLAGS; some may
# require patching. Verify arm64 page-size output with:
#   readelf -l artifacts/<artifact>.so | grep LOAD

export LDFLAGS="${LDFLAGS:-} -Wl,-z,max-page-size=16384"

cd "$SOURCE_DIR"

case "$BUILD_HINT" in
  make*platform=android-arm64*)
    # Standard libretro Makefile convention.
    # LDFLAGS is forwarded by most libretro Makefiles; if not, patch the Makefile
    # to add LDFLAGS to the linker invocation.
    eval "$BUILD_HINT"
    ;;
  cmake*)
    # CMake-based cores (mgba, gearsystem, melonds, flycast, tic80, wasm4).
    BUILD_DIR="build-android-arm64"
    mkdir -p "$BUILD_DIR"
    eval "$BUILD_HINT \
      -DCMAKE_BUILD_TYPE=Release \
      -DCMAKE_TOOLCHAIN_FILE=\"\${ANDROID_NDK_HOME}/build/cmake/android.toolchain.cmake\" \
      -DANDROID_PLATFORM=android-24 \
      -DCMAKE_EXE_LINKER_FLAGS=\"\$LDFLAGS\" \
      -DCMAKE_SHARED_LINKER_FLAGS=\"\$LDFLAGS\" \
      -B $BUILD_DIR"
    cmake --build "$BUILD_DIR" --config Release
    ;;
  ndk-build*)
    # NDK-build based cores (ppsspp).
    eval "$BUILD_HINT APP_OPTIM=release"
    ;;
  *)
    echo "ERROR: Unrecognised buildHint pattern: $BUILD_HINT" >&2
    echo "Update $0 to handle this pattern, or fix buildHint in cores.build.json." >&2
    exit 1
    ;;
esac

# ── Copy artifact ──────────────────────────────────────────────────────────────

# Locate the built .so; search common output locations.
BUILT_SO=$(find "$SOURCE_DIR" -name "$ARTIFACT" | head -1)
if [ -z "$BUILT_SO" ]; then
  echo "ERROR: Build completed but artifact '$ARTIFACT' not found under $SOURCE_DIR" >&2
  exit 1
fi

cp "$BUILT_SO" "$ARTIFACTS_DIR/$ARTIFACT"
echo "OK: $ARTIFACTS_DIR/$ARTIFACT"
