# CI & testing

## What runs where

| Layer | Test | Local command | CI |
|---|---|---|---|
| Rust tools (`plemu-*`) | 95 unit/integration tests + fmt + clippy | `cd tools && cargo test --workspace` | `ci.yml` → `rust` (ubuntu) |
| Native shim | `rh_smoke` (stub core, 16.7k assertions) | `bash apps/device/native/build_editor.sh` | `ci.yml` → `native-smoke` (macOS) |
| Native shim + real core | `rh_realcore_smoke` (SameBoy + a real game) | `bash apps/device/native/build_realcore.sh` | local/nightly (needs core source + rgbds) |
| Enforcement chain | offline end-to-end proof | `cd tools && bash scripts/e2e-demo.sh` | `ci.yml` → `e2e` (ubuntu) |
| Unity content-trust | 34 EditMode tests | Test Runner, or batchmode (below) | `ci.yml` → `unity-editmode` (needs `UNITY_LICENSE`) |
| Unity emulation | 5 PlayMode tests (money-shot + tamper) | batchmode (below), after staging | local / PSG1 DevKit only |

`ci.yml` is the build+test gate; `pr-checks.yml` is the separate content-screening gate for registry PRs.

## Running the Unity tests locally (batchmode)

Stage the native plugins + content first (see the README), then:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.19f1-arm64/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath apps/device/unity -runTests -testPlatform EditMode \
  -testResults editmode.xml -logFile editmode.log
"$UNITY" -batchmode -projectPath apps/device/unity -runTests -testPlatform PlayMode \
  -testResults playmode.xml -logFile playmode.log
```

Do **not** pass `-quit` with `-runTests` (it produces a 0-test run). PlayMode needs the staged native
shim + core dylibs and a graphics/audio device, which is why it is local-only — CI runs EditMode
(pure content-trust, no native dependency).

## Enabling the Unity CI job

The `unity-editmode` job skips cleanly until you add a Unity license:

1. Get a license file (`.ulf`): run the game-ci activation once
   (see <https://game.ci/docs/github/activation>), or export your Personal license.
2. Add it as the `UNITY_LICENSE` repo secret (Settings → Secrets and variables → Actions).
3. The job then runs EditMode via `game-ci/unity-test-runner@v4` on every push/PR.

Unity Personal is permitted on CI while revenue is under Unity's threshold — keep the receipt. If
activation flakes, move the job to a self-hosted runner with a persistent activation.

## Known CI notes

- `build_realcore.sh` reuses `cores/.cache/`; a stale cross-OS object cache there can fail the macOS
  linker. If a native job ever fails on a linker error, clear `cores/.cache/` (add a cache-key on the
  SameBoy commit if you cache it).
- The `e2e` job runs without `PLEMU_DEV_KEY_PEM` (dev keys never live in the repo) — it still asserts
  screening + manifest build; it only skips the final sign/verify, by design.
