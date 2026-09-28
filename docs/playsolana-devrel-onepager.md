# Plemu — PlaySolana partnership one-pager

*Intended to be pasted into an email to the PlaySolana devrel / PlayVerse policy
team. One product paragraph, the legal architecture in five bullets, six questions
we need answered, and a specific ask.*

---

Hi PlaySolana team,

**What we're building.** Plemu is a multi-emulator retro gaming hub
shipping as a PSG1 title: one Unity player that runs libretro cores for classic
consoles alongside fantasy-console platforms (WASM-4, TIC-80, PICO-8) and a homebrew
+ user-generated-content creator platform where players publish their own games and
carts. Crucially, it is a **legal-content-only** platform — every playable title is
an original homebrew, a public-domain / CC0 work, or a creator-attested UGC piece;
there is no commercial ROM distribution and no way for a user to side-load arbitrary
files. We want it to be a flagship "make and play games on-chain" experience on the
PSG1.

**The legal architecture (fail-closed by design):**

- **Ed25519-signed allowlist manifest.** The device only loads content that appears
  in a single JSON manifest signed by a pinned Ed25519 key; the signature is
  verified over the raw manifest bytes *before* any parsing, and an unknown or
  retired signer is rejected. Nothing outside the manifest can run.
- **Per-launch SHA-256 gate.** Every launch re-hashes the exact artifact bytes and
  refuses to run if the digest does not match the manifest entry — so a title cannot
  be swapped or tampered with after approval.
- **No file browser, no storage permissions.** The app ships with no "open local
  file" path and requests no external-storage permission, so there is no user
  mechanism to introduce unapproved content in the first place.
- **Nightly re-audit.** A nightly CI pipeline re-screens every entry against
  No-Intro / Redump denylists (hash + fuzzy-name matching to catch commercial dumps)
  and runs a per-core license audit against a pinned allowlist that hard-fails on any
  non-commercial or relicensed core, then republishes the signed manifest — failing
  closed if anything is off.
- **<24h DMCA + emergency revocation.** A takedown or dispute flips an entry to
  `revoked` and republishes a fresh signed manifest within minutes; the goal is the
  entry off every device within 24 hours (emergency path targets <30 minutes).

**Six questions we need answered before we commit to the full device build:**

1. Is there a **written PlayVerse content policy for emulator apps** that operate on
   fail-closed, legal-only content (signed allowlist, no side-loading)? We want to
   align to it exactly and, ideally, get a sign-off that our architecture satisfies
   it.
2. What is the **JIT / W^X policy on EchOS** for dynarec-based cores? Several
   performance-sensitive cores (e.g. N64, PSP, Dreamcast) use dynamic recompilation;
   we need to know whether executable-writable memory / JIT is permitted, and under
   what constraints, or whether we must run interpreter-only cores on device.
3. What are the **APK / AAB size limits on PlayGate**? Bundling multiple cores plus
   the Unity player plus a baked-in starter-pack manifest is sizable, and we need the
   ceiling to plan which cores ship on-device vs. download-on-demand.
4. **Is the PSG1 panel touch-capable**, or is input strictly gamepad/buttons? This
   drives our UI (creator tools, on-screen keyboards for fantasy consoles) and
   whether we design a touch or a D-pad-first interface.
5. What is the **recommended Unity version for the PlaySolana.Unity-SDK** on PSG1?
   We want to pin the exact LTS the SDK is validated against so our CI and device
   builds match yours.
6. Can Plemu get a **DevKit hardware request approved and a PlayGate developer
   account** provisioned? We need real hardware to validate JIT behavior, input, and
   performance before investing in the full build.

**The ask:** a **30-minute call** to walk through the architecture, plus a
**policy sign-off** on the emulator/legal-only content model, before we invest in the
full device build. If it helps, we can share the SPEC, the signed-manifest tooling,
and a runnable offline end-to-end demo (synthetic pirate entry rejected, clean
creator entry approved, manifest signed + verified) ahead of the call.

Thanks,
Plemu
