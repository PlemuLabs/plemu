# Testing on / emulating PlaySolana (what's installed, what's left)

## Where the app is today

The device app is **engine + trust chain + emulation**, deliberately **without** the PlaySolana SDK
yet. It runs the SameBoy core on a real game through the fail-closed `LaunchGate`, driven by **raw
keyboard input** in the editor. It does **not** call SvalGuard (wallet), PlayDex (quests), PlayID
(identity), or use `PSG1Input` / the PlaySolana PSG1 screen simulator. Those are the next layer.

So there are two different "simulators":
- **Plain Unity editor (works now):** play Libbet, run the trust chain, run the tests. No PlaySolana
  account or SDK needed. This is your fastest test loop.
- **PlaySolana PSG1 Simulator (not wired yet):** simulates the actual PSG1 device (1240×1080 portrait
  screen, the D-Pad/ABXY/L·R input device, wallet flows). Needs the PlaySolana SDK.

## What you need to INSTALL to emulate PlaySolana

1. A **PlaySolana developer account** — <https://developers.playsolana.com/> (for the SDK, the PSG1
   Simulator, and a DevKit request).
2. The **PlaySolana Unity SDK** — `com.playsolana.psdk`
   (<https://github.com/playsolana/PlaySolana.Unity-SDK>). Add it to
   `apps/device/unity/Packages/manifest.json`. It bundles the PSG1 Simulator and the `PSG1Input`
   device. (Confirm the exact package id + Unity-version compatibility with devrel — the SDK's
   legacy TMP dependency may need a tweak on Unity 6000.3; see the notes in the kit's
   `playsolana.md`.)
3. The companion **Solana.Unity-SDK 3.1.0** if you want on-device wallet/RPC
   (<https://github.com/magicblock-labs/Solana.Unity-SDK>).
4. Optionally a **PSG1 DevKit** for real-hardware runs (request via the developer portal).

## What you need to IMPLEMENT (next increment)

Behind the existing `PLAYSOLANA_PSG1` scripting define (already set for device builds), wire:

- **Input:** implement a `PSG1InputSource : IHubInput` (or the equivalent) that reads `PSG1Input`,
  and select it over the editor keyboard source in `EmulationInputSource` when the SDK is present.
  Our RetroPad mapping already assumes a SNES-class pad, which matches the PSG1 layout.
- **Wallet:** a `SvalGuardWalletAdapter` behind the app's wallet interface — for tipping creators and
  minting a game as an NFT (the creator-economy features, not built yet).
- **PlayDex / PlayID:** quest + identity hooks (`first_game_played`, `tip_sent`, `game_published`),
  gracefully degraded in-editor.
- **Screen:** the app already targets 1240×1080 portrait; verify against the PSG1 Simulator's exact
  safe area.

None of this changes the trust chain — it's all additive around the emulation runtime.

## What's left until the slice is FULLY testable

| Milestone | Status |
|---|---|
| Emulator runs a real game through the trust gate, in the editor | ✅ done (Libbet, PlayMode) |
| Builds to an arm64 IL2CPP APK | ✅ done (packaging validated) |
| **On-device content access** (StreamingAssets is inside the APK — `File.IO` fails there) | ⛔ needs `UnityWebRequest(jar:…)` / copy-to-`persistentDataPath` (fail-closed today: empty catalog, never a bypass) |
| PlaySolana SDK + PSG1 Simulator wired (input/wallet/quests) | ⛔ next increment (this doc) |
| Run on real PSG1 hardware | ⛔ needs a DevKit |
| Broader core set (SNES/Genesis/GBA…) + GLES bridge for N64/PSP/DC | ⛔ later increments |
| Web upload portal (permissionless publish) | ⛔ later (see `docs/upload-process.md`) |

**Bottom line:** you can fully test the emulation + trust chain **today** in the plain Unity editor
(after staging — see the README). To test the *PlaySolana-specific* bits you need their SDK +
Simulator installed and the input/wallet layer wired (above); to test on the *actual device* you need
the on-device content-access fix and a DevKit.
