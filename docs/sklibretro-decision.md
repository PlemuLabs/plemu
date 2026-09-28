# Decision: custom libretro shim, not SK.Libretro (2026-07-13)

**Context.** [SK.Libretro](https://github.com/Skurdt/SK.Libretro) is a Unity wrapper around the
libretro API. We evaluated it as a way to embed emulator cores in the Plemu device app instead of
maintaining our own native C shim (`apps/device/native/libretro_host`).

**Decision.** Keep the custom shim. Use SK.Libretro (MIT) only as a reference for
`retro_environment` command semantics. No dependency added.

## Why

SK.Libretro is not viable for our target (PSG1 = Rockchip RK3588S, Android/EchOS, arm64-v8a, IL2CPP,
OpenGL ES 3):

1. **Hardware rendering is broken on Android** — open
   [Issue #19](https://github.com/Skurdt/SK.Libretro/issues/19) (GLFW has no Android backend), no
   fix, no PR. Cores that need `retro_hw_render_callback` — Mupen64Plus-Next (N64), PPSSPP (PSP),
   Flycast (Dreamcast) — cannot run. Those are exactly the systems that most need a bridge, and the
   ones our own shim's GLES design targets.
2. **IL2CPP marshalling unverified** — [Issue #10](https://github.com/Skurdt/SK.Libretro/issues/10)
   ("IL2CPP does not support marshaling delegates that point to instance methods") is closed without
   a documented fix. libretro requires the core to call back into managed code every frame; under
   IL2CPP those must be `[MonoPInvokeCallback]` static trampolines. Unverified on our target.
3. **Feature gaps** — multi-core audio broken
   ([#28](https://github.com/Skurdt/SK.Libretro/issues/28)); save-state support questionable
   ([#25](https://github.com/Skurdt/SK.Libretro/issues/25),
   [#32](https://github.com/Skurdt/SK.Libretro/issues/32)).
4. **Maturity** — single author, ~36 stars, one 0.9.0 release (Jan 2025), no evidence of any shipped
   Android product. Bus factor 1.

## What our shim gives us instead

`libretro_host` keeps the libretro callbacks **native-side** (the exact thing that sinks SK on
IL2CPP), exposes a narrow ~22-function C ABI the C# layer P/Invokes, already passes a deterministic
software-path smoke harness (video/audio/input/savestate), and its ABI reserves the GLES
hardware-render entry points for the N64/PSP/DC bridge SK.Libretro lacks. Full control, debuggable,
and on the well-trodden path RetroArch/EmulationStation have shipped on Android for years.
