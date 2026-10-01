# Plemu Platform Expansion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand Plemu from a verified single-lane signed-content emulator slice into the full four-rung distribution ladder — local lane, private packs with on-chain seats, public beta store, curated main store — with a PLEMU-denominated marketplace, across three parallel workstreams executed by three fullstack developers.

**Architecture:** Two trust domains that share zero code path. The **signed domain** keeps SPEC v1 frozen byte-for-byte: store manifests and pack manifests both use the existing `Manifest` wire format, so there is no `schemaVersion` bump, no fixture regeneration, and no coordinated Rust+C# break. The **local domain** is a new, separately specified lane with its own entry point, its own store, and a machine-checked invariant that nothing crosses from it into the signed domain. Seats are enforced at **manifest acquisition** (a Cloudflare Worker checks an on-chain PDA before issuing signed URLs), never at launch — so offline play survives untouched and the Worker gates *access* while the Ed25519 signature continues to gate *authenticity*.

**Tech Stack:** Unity 6000.3.19f1 (IL2CPP, arm64-v8a) · C11 libretro shim · Rust (stable, edition 2021) · Anchor 1.x on Solana devnet · classic SPL Token + Metaplex Core · Cloudflare R2 + Workers · Supabase · Next.js + `@solana/kit` + Codama.

**Signing keys:** this repo ships signatures whose **private keys were never included**. Verify on arrival, then run `tools/scripts/rotate-dev-keys.sh` (Task 0.5) to generate your own before staging anything.

**Entity:** **Plemu Labs** — GitHub org `plemu-labs`, domain `plemu.xyz`. Use these everywhere. The repo carries no prior-organisation or personal identifiers — verified across every tracked file — and it should stay that way.

**Spec:** This document is self-contained. It carries the design decisions (§2) and interface contracts (§3) inline because the three new specs it mandates — `spec/SPEC-LOCAL.md`, `spec/SPEC-PACK.md`, `spec/ATTESTATION-V2.md` — are themselves deliverables of Stream B (tasks B2, B3). The existing frozen contract at `spec/SPEC.md` (v1, FROZEN 2026-07-12) is normative and **must not be edited by this plan**.

---

## Global Constraints

Every task's requirements implicitly include this section. Exact values, copied verbatim from the repo.

**Toolchain (do not substitute versions):**
- Unity **6000.3.19f1**, revision `7689f4515d75`. Android Build Support module with OpenJDK + Android SDK/NDK.
- Android NDK **r27c (27.2.12479018)** — bundled inside the Unity Android module at `$UNITY_EDITOR/PlaybackEngines/AndroidPlayer/NDK`. Do not install a standalone NDK.
- Scripting backend **IL2CPP**; `AndroidTargetArchitectures: ARM64` only; `minSdkVersion 30`; `targetSdkVersion 34`; `UIOrientation.Portrait`; graphics APIs **GLES3 only** (`SetUseDefaultGraphicsAPIs(false)`, Vulkan removed).
- Application identifier `com.playsolana.games.plemu`. Scripting define `PLAYSOLANA_PSG1` on device builds.
- Rust **stable**, edition **2021**. Nightly workflows pin `RUST_VERSION: "1.93.1"`.
- Native shim: **C11**, clang, `-Wall -Wextra -Werror -O2 -fPIC -fvisibility=hidden`.
- `rgbds` (`brew install rgbds`) — required only to build SameBoy boot ROMs.

**Pinned content:**
- SameBoy **v1.0.3**, MIT, commit `208ba4afabffab9edde416f2dbb8ae459e34adb8`.
- Libbet and the Magic Floor **v0.08**, zlib, **32768 bytes**, SHA-256 `3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9`.

**Frozen trust contract — `spec/SPEC.md` v1 (FROZEN 2026-07-12):**
- `schemaVersion` stays **1**. Verifiers compare with exact equality, not `>=`.
- Producers **MUST NOT** emit unknown fields at schemaVersion 1; verifiers MUST ignore them.
- **Do NOT add a `tier` field** to the manifest or to any Entry.
- Ed25519 is computed over the **raw bytes of the manifest exactly as served**. There is **no canonicalization step**. Verify **before parsing any JSON**; on failure, stop and never parse.
- Key statuses `active | next | emergency` verify; `retired` does not.
- Freshness: expiry = `generatedAt + 72h`; grace = **7 days** past expiry, inclusive on both boundaries.
- Anti-rollback: `sequence` must be `>=` the highest previously accepted. Equal allowed, lower rejected.
- Per-launch gate: streaming SHA-256 must match at **every** launch, entry not revoked. Any failure is a typed denial with **no bypass**.

**Solana (see `.claude/rules/anchor.md`, `rust.md`, `typescript.md`):**
- **Anchor 1.x.** `CpiContext::new(...)` takes the program **`Pubkey`** (`program.key()`), not an `AccountInfo`. SPL transfers use `transfer_checked`. Space is `T::DISCRIMINATOR.len() + T::INIT_SPACE` — never a magic `8`. One `#[error_code]` enum per program by convention (a second compiles but silently collides at `ERROR_CODE_OFFSET = 6000`).
- **No `unwrap()` or `expect()` in program code.** Checked arithmetic only (`checked_add`/`checked_sub`/`checked_mul`). Store canonical PDA bumps; never recalculate. Validate every account (owner, signer, PDA). Never use `init_if_needed`.
- Unit tests use **LiteSVM or Mollusk**, never `solana-test-validator`. Integration uses **Surfpool**.
- **Devnet only.** Mainnet deployment requires explicit user confirmation and is out of scope for this plan.
- TypeScript: `@solana/kit` pinned **per workspace** to the major its own dependencies peer against — never one repo-wide digit. Generate clients with **Codama** from the Anchor IDLs; do not use Anchor's `Program` client (it pins web3.js v1).

**Forbidden emulator cores (deny-by-default, hard-fail any config mentioning them):**
`snes9x`, `genesis_plus_gx`, `picodrive`, `fbneo`, `fbalpha`, `duckstation`, `swanstation`, `mame2000`, `mame2003`, `mame2003_plus`, `mame2010`, and any id prefixed `mesen`.

**Error handling — every layer, no exceptions:**

This is a fail-closed system, which makes it unusually easy to build something that refuses to work and never says why. The current code is already good at the first half and poor at the second: `LaunchGate` returns a typed `DenialReason` and `PlemuApp.cs:182` renders it as `DENIED: HashMismatch`, which tells a user nothing and tells a QA engineer almost nothing. Every task in this plan inherits these rules:

- **Fail closed, but never fail silent.** A refusal must carry a typed reason, a human-readable sentence, and enough context to reproduce it. `catch` that swallows is a defect, not defensive coding.
- **Typed errors at every boundary.** Rust: `thiserror` enums, never `anyhow` in a library, never `unwrap()`/`expect()` outside tests. C#: typed result objects, never exceptions for control flow. Anchor: one `#[error_code]` enum with a `#[msg]` on every variant.
- **Two audiences per error.** A short user-facing sentence that says what happened and what to do, plus a diagnostic line carrying the machine detail (hash, path, key id, sequence, program error code). Never show a raw enum name or a bare `0x1771` to a user; never hide the underlying value from a log.
- **Never leak secrets or absolute paths** into a user-facing message. Key material, PEM paths and wallet private data never appear in any string that reaches a screen.
- **Log at the point of decision, not the point of catch**, so the log says why something was refused rather than merely where it surfaced.
- **Every new denial or failure variant ships with a test that asserts the message**, not just the code. A reason nobody can read is a reason nobody will fix.

**Process (from `CLAUDE.md`):**
- Branch per unit of work: `git checkout -b <type>/<scope>-<description>-<DD-MM-YYYY>`.
- Every Rust change: `cargo fmt` → `cargo clippy --workspace --all-targets -- -D warnings` → `cargo test --workspace`.
- Dev signing keys live **outside the repo** in `../dev-secrets/` and are test-only. Production keys go to KMS/CI secrets. The emergency key is generated offline and never touches a repo or runner.

---

## Review Focus

Five failure modes this design implies that no obvious task would otherwise test. Each is pinned to the task that owns the code.

1. **Unparseable `expiresAt` diverges across implementations.** Today C# treats it as `Expired` (soft-deny, `ManifestVerifier.cs:93-94`) while Rust returns a hard `Validation` error. A pack manifest with a malformed timestamp therefore behaves differently on device than in CI. → pinned to **B4**.
2. **The same artifact in two lanes with two sequences.** A sha256 listed in both a store manifest and a pack manifest is one file on disk under content-addressed storage. Revoking it in one manifest must not leave it playable via the other. → pinned to **A4**.
3. **Hostile local file.** SAF hands the app an arbitrary user file. `plemu-screen`'s size caps and archive-safety checks run server-side only; the local lane has none. A 4 GB file, a zip bomb, or a symlink must fail gracefully, not OOM the device. → pinned to **A3**.
4. **Paid seat, revoked pack.** A `Seat` PDA exists, the Worker issues URLs, and the device then denies every entry because the pack was revoked. The user paid and sees nothing with no explanation. → pinned to **C7**.
5. **Cold device, long shelf, no network.** A device that sits unpowered past the 7-day grace and boots offline currently plays *nothing* — SPEC §2.3 step 6 exempts bundled starter-pack titles, but `LaunchGate.cs:13-15` documents that exemption as unimplemented ("for now Expired always denies"). → pinned to **A1**.

---

## 0. How to use this document

Read §1–§4 once, all three of you, before touching anything. Then:

- **Phase 0** and **Phase 1** are done by all three developers together. Nothing parallelizes before Phase 1 lands.
- After Phase 1, each developer owns one stream and its file tree exclusively. **Two people never edit the same tree.** If you need a change in someone else's tree, you file it against their stream — you do not edit it.
- Tasks inside a stream are ordered by dependency. Where the order is arbitrary it says so.
- Every task states its verification command and the exact expected output. "Done" means that command printed that output.
- `⛔ BLOCKS` means another stream is waiting. Do those first.

**Naming:** branches `<type>/<stream><task>-<description>-<DD-MM-YYYY>`, e.g. `feat/a3-local-lane-saf-06-10-2026`.

---

## 1. Verified current state (assessed 2026-09-28)

### 1.1 What works

The emulation vertical slice is real and reproducible, not a slide.

| Suite | Count | Command |
|---|---|---|
| Rust workspace | **95 tests** (54 unit + 41 integration) | `cd tools && cargo test --workspace` |
| Native stub-core smoke | **16,750 assertions** | `bash apps/device/native/build_editor.sh` |
| Native real-core smoke | **~42 assertions** | `bash apps/device/native/build_realcore.sh` |
| Unity EditMode | **34 tests** | Unity Test Runner |
| Unity PlayMode | **5 tests** | Unity Test Runner (needs staging) |
| Offline e2e enforcement | pass | `bash tools/scripts/e2e-demo.sh` |

The C# and Rust verifiers implement SPEC §2.3 **step for step identically** — envelope parse, key lookup, status gate, alg check, Ed25519-before-parse, schema check, signerKeyId match, anti-rollback, freshness. Verified line-by-line. One divergence only, listed in Review Focus #1.

The native shim is genuinely core-agnostic: 22 exported `rh_*` functions, `dlopen` + 22 `dlsym`, geometry and timing read from the core at runtime. **Adding SNES/MD/GBA requires zero shim changes.**

`LaunchGate.Authorize(titleId, IManifestSource, IContentStore, IClock)` takes its sources as parameters, so a second lane is a new entry point, not surgery on the existing one.

### 1.2 What is broken right now

This is repair work, not expansion. It is assigned to Stream B and, per an explicit decision, **does not gate feature work** — but the dependency is real and marked where it bites.

1. **The takedown button does not work.** `.github/workflows/emergency-revoke.yml` checks out `plemu/plemu-registry` (line 62); `nightly-enforce.yml` checks out both `plemu/plemu-registry` (lines 258, 364) and `plemu/plemu-cores` (lines 139, 193) — repos that do not exist. The monorepo consolidation (`9b2c256`) never updated them. Both fail closed at the first real step. Reactive takedown is the single legal property that makes an ungated beta tier defensible.
2. **Both publish paths would publish an empty manifest.** They invoke `plemu-manifest build --registry .../registry` with no `--tier`, which walks for `<system>/` directories and finds only `beta/` and `main/`. Zero entries.
3. **`plemu-screen` in PR checks is a stub** that always exits 10. Its `--denylist dats/latest/denylist.sqlite` and `--keys keys/manifest-signers.json` paths do not resolve in this repo (the real key file is `tools/keys/manifest-signers.json`).
4. **No infrastructure exists**: no R2 bucket, no `cdn.plemu.xyz`, no Cloudflare purge endpoint, no production signing keys, no Android keystore.
5. **Core pins are all `TBD`.** All 16 `commit` and all 16 `licenseSha256` values in `cores/cores.build.json` and `cores/expected-licenses.lock` are `"TBD"` — yet SameBoy *is* pinned to `208ba4a` in the shell scripts, which claim consistency with the lock. Real drift. Nightly `--strict` fails on TBD by design.
6. **DMCA designated agent is unregistered** — `registry/docs/dmca-policy.md` carries `TODO` and three `TBD-verify` fields. A §512 safe-harbor gap today; materially worse once money moves.
7. **`addedAt` is not derived from git history** despite `registry/README.md` and the curation checklist saying so; `main.rs:242` sets it to `generatedAt`.
8. **Curator attestations have no wallet allowlist.** `check_attestation` skips the wallet-identity check for `role: "curator"` (`checks.rs:234` gates it on `AttestationRole::Creator`), with a comment at `checks.rs:232-233` deferring a curator allowlist to `--keys`. Any keypair can currently mint a valid curator attestation — and the only non-CC0 registry entry uses exactly that role.
9. **`removals.ndjson` schema mismatch**: the policy documents `{eventType, entryId, reason, timestamp, noticeRef}`; the workflow writes `{id, reason, evidenceUrl, revokedAt}`. No reinstatement path exists despite the policy promising one.
10. **`.github/PULL_REQUEST_TEMPLATE.md` is pre-M2** — every path lacks the `beta/`/`main/` tier segment.

### 1.3 The device blocker

`apps/device/unity/Assets/_Game/Scripts/Runtime/Content/FileManifestSource.cs:51`

```csharp
if (!File.Exists(_manifestPath) || !File.Exists(_signaturePath)) return null;
```

On Android, `Application.streamingAssetsPath` is a `jar:file:///…/base.apk!/assets` URL, not a filesystem path. `File.Exists` always returns false, `GetVerifiedManifest()` always returns null, the catalog is always empty, and `LaunchGate` denies with `ManifestUnavailable`. It fails **closed** — never a bypass — but this is the exact line between "the APK installs" and "the APK plays". Every downstream content read is also `System.IO`: 12 call sites in `FileContentStore.cs` (lines 31, 49, 63, 66–71, 76, 85, 91) and 2 in `EmulationSession.cs` (100, 102). `Application.persistentDataPath` is used **nowhere** in the project.

### 1.4 Native defects found during assessment

Five, one significant. All assigned to A2.

- **B1 — audio ring data race (significant).** `rh_ring` (`internal.h:40-47`) has **no mutex and no atomics**, while `EmulationThread.cs:13-14` documents `rh_read_audio` as "mutex-guarded ring". Unity's audio thread mutates `tail`/`count` in `rh_ring_pop` while the emulation thread mutates `head`/`count`/`tail`/`drops` in `rh_ring_push`. `count` is read-modify-written on both sides. `EmulationSession._handleLock` guards teardown only; the frame loop does not take it. This also violates the shim header's own rule 1.
- **B2 — `rh_unload_game` does not release the one-live-core slot.** `libretro_host.h:20-21` attributes the release to the **pair** (`rh_unload_game() + rh_destroy()`), so the header is defensible — but `libretro_host.c:311-323` clears only `game_loaded` and `g_active` is cleared solely in `rh_destroy` (`:108-109`), so `unload_game` → `load_core` on the same handle returns `RH_ERR_BUSY`. Doc and behaviour are easy to read as contradictory.
- **B3 — mutex destroyed while held.** `rh_lock_frame` returns *holding* `v->mtx`. An unload while a frame is held runs `pthread_mutex_destroy` on a locked mutex. Compounding it, `rh_unlock_frame` bails on `!c->game_loaded` *before* unlocking, permanently leaking the lock.
- **B4 — double `rh_lock_frame` self-deadlocks** on a non-recursive mutex; `v->locked` is set but never checked on entry.
- **B5 — `video_refresh` over-read risk.** `width` is never validated against `max_w` and nothing checks `pitch >= dst_pitch`.

### 1.5 The three collisions with the frozen SPEC

| Feature | Collides with | Verbatim |
|---|---|---|
| Local lane | Threat model; §2.3 step 7 | *"the device must never load content whose bytes are not explicitly approved"* / *"Any failure ⇒ typed denial, no bypass"* |
| Pack manifests | §1.0; §2.2/§2.3.2 | *"**Do NOT add a `tier` field** to the manifest or to any Entry"*; only pinned keys verify |
| Seats | §2.3 is a closed numbered algorithm | no per-user authorization concept exists anywhere |

Plus the amendment clause: *"Changes require a `schemaVersion` bump and a review."* §2 resolves all three without editing SPEC v1.

### 1.6 External representations that must be corrected

- `docs/playsolana-devrel-onepager.md` promises PlaySolana *"No file browser, no storage permissions… no way for a user to side-load arbitrary files"* — and asks them to sign off on that model. The SAF design (§2.3) keeps "no storage permission" **literally true**, but "no file browser" becomes false. The one-pager must be revised **before** the devrel call. → B9.
- `registry/docs/outreach-template.md` tells authors the platform is *"Free to download and play for users"* with commercial flows limited to *"a tip and NFT creator economy"*. No real author has received this yet — both registry entries are dev-wallet fixtures citing upstream licenses (zlib, CC0), not personal grants. It must be rewritten for the paid model **before the first real author email**. → B9.

---

## 2. Design decisions (locked)

Each decision below was made explicitly. Do not relitigate them mid-implementation; if one turns out wrong, stop and escalate rather than working around it.

### 2.1 SPEC strategy — isolated second specs, plus one deliberate v1 amendment

Two new documents (`SPEC-LOCAL`, `SPEC-PACK`) are written alongside `spec/SPEC.md`, each with exactly one machine-checkable invariant, and neither changes the manifest wire format. That covers the local lane and packs with no fixture regeneration.

**One thing is not coverable that way, and we are amending v1 for it: revocation.** SPEC §4.2 states the per-entry `revocation.json` sidecar is *"the only revocation path"*, and §1.0 makes each manifest independently revoked. With N packs that is not a takedown mechanism, it is an enumeration problem — you must find and rebuild every manifest listing a sha256, including packs no device has ever fetched, and no server-side artifact→manifest index exists or is planned. A sidecar you forget is content that stays live after a notice.

**SPEC v2 therefore adds one thing: a global, platform-signed, sha256-keyed revocation list**, fetched independently of any manifest and consulted at launch for every lane. A takedown then reaches every manifest **by construction** — no enumeration, no index, and it works for packs and local-lane content alike.

Scope of the amendment, deliberately minimal:
- **The manifest wire format does not change.** No new fields, no `tier` field, entries keep `revoked`/`revokedReason`/`revokedAt` as a second, redundant signal.
- **§2.3 gains one clause in step 7**, not an eighth step: the artifact's sha256 must not appear in the verified revocation list.
- **§4.2 is rewritten**: the sidecar remains the *authoring* mechanism; the global list is the *enforcement* mechanism.
- **`schemaVersion` goes to 2, and the verifier relaxes from `==` to `>=`** for reading. Devices accept v1 and v2 manifests; producers emit v2. This is the only part that touches existing code, and it is deliberately the smallest possible change to `verify.rs:123` and `ManifestVerifier.cs:76` — a one-line comparison each. Existing frozen fixtures stay valid *as v1 documents*, so they are not regenerated.

The cost is real: the contract stops being frozen, and "frozen" was what made it trustworthy. The tradeoff is that the alternative leaves the load-bearing legal control dependent on an enumeration nobody has built. Task **B2b** carries it.

### 2.2 Pack manifests — same wire format, outer platform signature, inner creator attestation as a sidecar

A pack is **one `Manifest` file at a pack-specific URL**, using the SPEC v1 schema unchanged.

- **Outer:** an ordinary `manifest.sig` envelope signed by Plemu's pinned `active`/`next`/`emergency` keys. This preserves the pinned-keys-only rule that SPEC §2.3's Rust/C# equivalence waiver depends on, and it keeps takedown working *inside private packs* — revoke, rebuild, re-sign, device sweeps.
- **Inner:** the pack operator's creator attestation, as a **separate sidecar file** `pack-<packId>.attest.json`. It is a sidecar precisely so the manifest JSON gains no new field and stays schema-v1-identical.

Device-side anti-rollback becomes keyed by manifest identity (pack id) rather than one global high-water mark. That is a client storage change, not a wire change.

### 2.3 Local lane — SAF picker, no storage permission

Files enter through Android's `ACTION_OPEN_DOCUMENT` system picker. The app declares **no storage permission of any kind**. Every import is an explicit, individually-consented user act. This keeps the "no blanket storage permission" claim to PlaySolana literally true and gives us a per-file provenance record.

**The invariant, normative:** *No byte admitted through the local lane may become a signed-domain entry, appear in any public listing, receive a tip, be minted, or be promoted to a pack or store — without traversing the full signed-domain submission path from scratch.*

Enforced structurally, not by convention:
- A distinct entry point `LaunchGate.AuthorizeLocal(...)`. The existing `Authorize(...)` keeps its "the ONLY path" docstring and its entire test suite unchanged.
- A `LocalContentStore` rooted at `Application.persistentDataPath/local/`, outside every signed-domain path.
- A `Lane` discriminator on `CatalogEntry`, rendered as a visible badge.
- A test asserting no local entry can ever produce a `LaunchTicket` from `Authorize()`.

### 2.4 Seats — enforced at acquisition, never at launch

```
operator creates Pack PDA
  → buys capacity in PLEMU (tiered by audience band)
  → assigns Seat{pack, member} PDA
device requests pack
  → Worker: one getAccountInfo on the Seat PDA
  → Worker issues short-lived signed R2 URLs
device: verifies Ed25519 signature + streaming SHA-256, exactly as today
```

**The Worker gates access; the signature gates authenticity.** A compromised Worker cannot forge content — the device re-verifies the Ed25519 signature and re-hashes every artifact. State the property precisely, though: the Worker is not an *authenticity* trust root, but it **is** the sole *authorization* trust root, and it decides **which** authentic manifest you receive. That is why the pack-binding sidecar below exists. Offline play is untouched — once cached, normal Fresh/Grace rules apply with no network in the launch path, and SPEC §2.3 gains no eighth step.

A seat is a **PDA row** in `plemu-seats`, keyed `(pack, member)`. Cheap, revocable, non-transferable, and readable by the Worker in one RPC round trip. Three properties must be stated honestly rather than glossed:

- **Revocation stops future fetches, not existing copies.** Once the bytes are on a device they are a validly-signed, self-verifying bundle that plays offline forever. A seat is a paywall at acquisition, not DRM. Say so in `contracts/seat-pda.md`; an operator selling "revocable memberships" will otherwise discover this the hard way.
- **A closed Seat PDA can be resurrected by dusting.** Revocation closes the account, and the address is derivable offline by anyone from `(pack, their own wallet)`. A 1-lamport System transfer makes `getAccountInfo` return non-null. The Worker MUST check `owner == plemu_seats::ID`, then the discriminator, then deserialize and confirm both `pack` and `member` — never a bare null check. C7 tests this explicitly.
- **A pack manifest carries no intrinsic identity.** It is a bare SPEC-v1 `Manifest`; the only thing naming it is the URL it came from — which the *requester* chooses. Without a binding, a Worker (or anyone holding an old validly-signed manifest) can serve pack A's superseded manifest in answer to a request for an unseen pack Z, where the per-manifest rollback floor is zero, resurrecting revoked entries. The **pack-binding sidecar** in §3 closes this.

### 2.5 Attestation v2

v1's frozen bytes grant right-to-distribute only — not monetization. v2 adds an explicit monetization grant. Both existing registry entries are dev-wallet fixtures, so they are regenerated rather than migrated; there is no third party to re-consent. `plemu-screen` accepts v2 only.

### 2.6 Token — single PLEMU, classic SPL, fixed supply

One token for payments, quest rewards, and AI-mod burns. **Classic SPL Token + Metaplex metadata**, not Token-2022: the binding constraint is Futard/MetaDAO launchpad and Meteora LP compatibility, and no Token-2022 extension is needed by anything in this design. Fixed supply, mint authority burned at launch, a pre-allocated rewards treasury funds quests, and AI-mod burns are terminal.

Acknowledged consequence: creators are paid in a volatile asset and every seat buyer needs PLEMU first. Mitigated by a **Jupiter swap at the purchase boundary** (C10) so the UX is "pay with anything." Note Jupiter has no devnet coverage — C10 documents the two-transaction, idempotent-receipt design that makes this testable.

### 2.7 On-chain surface — four Anchor programs, compose everything else

| Ours | Composed |
|---|---|
| `plemu-seats` — pack registry, capacity purchase, tiered expansion, seat assignment/revocation | classic SPL Token (PLEMU) |
| `plemu-market` — listings, fixed-price and pay-what-you-want, tips, on-chain receipts | Metaplex Core (collectibles) |
| `plemu-splits` — recipients + bps, **derivative lineage from day one** | Jupiter (swap at purchase) |
| `plemu-quests` — quest definitions, completions, treasury claims | |

### 2.8 Storage and serving

- **Cloudflare R2** — artifact bytes, content-addressed, under **two separate prefixes**. Zero egress, which is the difference between viable and expensive once PSP/DC images run to hundreds of megabytes.
  - `artifacts/public/<sha256>` — publicly readable, for store content.
  - `artifacts/gated/<sha256>` — reachable only through a signed URL.

  The split is not optional. Content addressing plus a single public prefix would make every paid pack artifact fetchable by anyone who knows its sha256 — and hashes are not secret here; `plemu-dat` exists precisely because artifact hashes are published in DAT files. The cost is that the same ROM sold in a pack and listed in the store is stored twice. Pay it.
- **Cloudflare Worker** — the seat gate. Reads the `Seat` and `Pack` accounts in one `getMultipleAccounts` call (same slot, so you cannot grant on a live seat plus a stale un-revoked pack) and issues signed URLs with a TTL of **60 seconds or less**. Public store manifests and `artifacts/public/` bypass the gate.
- **Supabase** — everything sensitive or relational: submission queue, DMCA notices and counter-notices, creator contact details and PII, license-evidence metadata, an on-chain receipt mirror (the chain remains truth), quest progress cache.

### 2.9 Monorepo

Everything stays in `plemu/`. New top-level trees `programs/`, `services/`, `portal/`. This also lets B1 finally delete the phantom `plemu-registry`/`plemu-cores` references that currently break both publish workflows — the multi-repo assumption is precisely what broke them.

### 2.10 Cluster

**Devnet throughout.** Mainnet is an explicit gate after the external audit, outside this plan.

### 2.11 Licensing and legal work is deferred to Phase M

**Decision: these three streams build technology and product. Licensing, legal and compliance work is collected into Phase M — Mainnet Launch Preparation — and executed there, alongside the external audit.**

What that means concretely:

- **Core licence verification is deferred.** `plemu-core-audit --strict` stays red on `TBD` values and that is expected, not a failure. B6 pins **commits only** — enough for reproducible builds, which is a technical requirement — and leaves `licenseSha256` alone. The strict gate is a Phase M task.
- **DMCA agent registration, the licensing matrix, the outreach template and the PlaySolana one-pager correction all move to Phase M.** They were tasks B8 and B9.
- **Attestation v2's grant language is provisional.** B3 still ships it, because A5 is blocked on the wire format and the frozen bytes must exist. Treat the wording as a placeholder that Phase M replaces after counsel reads it — and because the bytes are frozen and fixture-tested, replacing them later is a fixture regeneration, not a redesign.
- **What does NOT move:** revocation. B1 and B2b stay in Stream B at their current positions. Revocation is a technical capability with a legal motivation, and the global revocation list (B2b) is an architecture decision already taken. Building it now and using it later is cheap; retrofitting it is not.

**Why this is a reasonable staging decision rather than an oversight.** Nothing here ships to the public during these three streams: the cluster is devnet, the store has two dev-wallet fixtures in it, and no real creator has been contacted. The exposure the reviewers identified is real but it is almost entirely *launch* exposure, and launch is Phase M by construction. The cost of deferring is that Phase M is larger than it looks — it is not a checklist, it is a workstream — so it is written out in full below rather than left as a line item.

**The one thing to avoid:** letting a Phase M item become load-bearing for a Phase 1–3 demo. If a demo needs a real creator, a real licence, or a public URL, that demo has crossed into Phase M and should be re-scoped or the specific gate pulled forward deliberately.

---

## 3. Interface contracts (the three seams)

These are frozen on Contracts Day (Phase 1) and are the reason three people can work without blocking each other. Each seam ships a **contract document plus a working mock**, so a stream codes against its neighbour's mock and never waits.

### Seam A↔B — content formats

**Owner:** Stream B authors; Stream A consumes.
**Artifacts:** `contracts/pack-manifest.md`, `contracts/attestation-v2.md`, `contracts/local-lane-invariant.md`, plus frozen fixtures in `contracts/fixtures/` mirrored into `apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures/`.

**Rule for every frozen-bytes builder in this system:** no placeholder value may contain CR or LF. Return an error, and test it per placeholder. These messages are newline-delimited and unescaped, so a `packId` of `x\nSeats: 999999` — or, in v1 today, a `title` of `X\nWallet: <attacker>` — makes what the signer signed and what a line-oriented parser reads two different things. Consumers must **reconstruct and compare**, never parse. `packId` is additionally constrained to the entry-slug grammar `^[a-z0-9]+(-[a-z0-9]+)*$`, enforced in `build-pack`, in the Worker route, and on chain.

**Pack attestation frozen bytes** (UTF-8, LF line endings, **no trailing newline**, no BOM — identical discipline to Attestation v1):

```
Plemu Pack Attestation v1

Pack: <packId>
Operator: <base58 wallet>
Entries: <64 lowercase hex — sha256 of the sorted entry ids joined by \n>
Seats: <u32 capacity>
Timestamp: <YYYY-MM-DDTHH:MM:SSZ>
```

**Attestation v2 frozen bytes** — v1 with a version bump and one added line:

```
Plemu Creator Attestation v2

Title: <title>
System: <system enum value>
SHA-256: <64 lowercase hex>
License: <license.type enum value>
Statement: <role-dependent, unchanged from v1>
Rights: I have the right to authorize commercial distribution of this work.
Grant: I grant Plemu a worldwide, non-exclusive, royalty-free licence to reproduce,
  store, cache, transmit, publicly display and distribute this work, and to sublicense
  those rights to Plemu pack operators, for the purpose of operating the Plemu platform.
Monetization: <ROLE-DEPENDENT — see below>
Derivatives: <yes|no>
Revocation: I may revoke this grant at any time by written notice. Revocation is
  prospective: copies already delivered and collectibles already minted are unaffected.
Wallet: <base58 — signer identity>
Payee: <base58 wallet or splits address>
Timestamp: <YYYY-MM-DDTHH:MM:SSZ>
```

Six things the one-line version got wrong, all cheap to fix now and expensive after real creators sign:

1. **"Distribute" is §106(3) alone.** You also reproduce into R2, cache at the edge, bake into the bundled starter pack, copy to the device, and display title and artwork. A grant without reproduction and public display has a hole in the middle.
2. **Sublicensing is the highest-value addition.** The model is creator → Plemu → **operator** → members. Without an express right to appoint distributors, every pack is an unauthorised sublicence.
3. **Territory, term and exclusivity must be stated.** German UrhG §31(5) narrows unspecified grants to the contract's purpose; French CPI L.131-3 requires each right, its scope, place and duration separately. A lot of homebrew authors are German and French.
4. **Derivatives are absent, and Appendix A ships them.** Nothing grants §106(2). Make it an explicit opt-in — many authors will grant distribution and refuse mods.
5. **`Wallet` was doing double duty** as signer identity and payee. Keys rotate; a payee may be a splits PDA. Separate them.
6. **A curator cannot grant monetization rights — they do not own the work.** `gb/libbet` is exactly this: a curator-role attestation over a third party's zlib ROM, signed by a dev wallet, with that wallet as `Wallet:`. Regenerating it under a role-blind v2 produces a curator granting rights they do not hold and pointing payment at themselves. **`Monetization` must be role-dependent:**
   - creator → `This licence covers paid packs, paid listings and collectible mints. Net proceeds owed to me are routed to the Payee below.`
   - curator → `The stated licence permits commercial redistribution by third parties, per the linked evidence. I grant no rights of my own and claim no proceeds.`

   B3 step 4 regenerates both registry fixtures — it must not ship the creator wording into a curator entry.

**R2 path scheme** (also consumed by Seam B↔C):

```
artifacts/public/<sha256>                   publicly readable (store content)
artifacts/gated/<sha256>                    signed-URL only (pack content)
manifests/store/manifest-beta.json[.sig]
manifests/store/manifest-main.json[.sig]
manifests/packs/<packId>.json[.sig]
manifests/packs/<packId>.attest.json        creator attestation (inner)
manifests/packs/<packId>.binding.json[.sig] platform binding — see below
```

**The pack-binding sidecar.** A pack manifest is a bare SPEC-v1 `Manifest`; nothing inside the signed bytes says which pack it is. Without a binding, the manifest's only identity is the URL the *requester* chose, so serving pack A's superseded manifest for an unseen pack Z defeats anti-rollback entirely. The binding is a separate file — not a manifest field — so SPEC v1 is untouched:

```json
{ "packId": "speedrun-club", "manifestSha256": "<64 hex>", "sequence": 7 }
```

Signed with the **same pinned platform keys** as the manifest, in an ordinary `manifest.sig` envelope. The device verifies the binding **first**, confirms `packId` equals the pack it asked for and `manifestSha256` equals the sha256 of the manifest bytes it received, and only then verifies and parses the manifest. `sequence` in the binding must equal the manifest's own. A mismatch is a typed denial.

**This applies to every manifest identity, not only packs** — `store-beta`, `store-main` and `bundled` each get a binding too, under `manifests/store/<id>.binding.json[.sig]`. Without it, any validly-signed manifest is interchangeable with any other: `store-beta`'s bytes presented as a never-seen pack identity have a rollback floor of zero, so last month's pre-takedown manifest verifies cleanly and its revoked entries come back. A single-identity fix would leave that door open.

Note the honest tradeoff: this is a precondition on *which bytes reach step 1*, not an eighth step in SPEC §2.3 — which is what keeps v1 intact. But it means manifest identity, which §1.0 deliberately left as "operational convention," is now load-bearing for a security control. The binding is what turns that convention back into something signed.

### Seam A↔C — wallet and commerce surface

**Owner:** Stream C authors; Stream A consumes.
**Artifacts:** `contracts/wallet-interface.md` plus `MockWallet` and `MockSeatProvider` committed into `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/Mock/`.

Stream C must ship the mocks on Contracts Day. Stream A's tasks A5 and A4 code against them and switch to real implementations only when C2/C3 land. The C# interface:

```csharp
namespace Plemu.Wallet
{
    public interface IWallet
    {
        bool IsConnected { get; }
        string PublicKey { get; }                                  // base58, null when disconnected
        Task<bool> Connect();
        // Amounts are PLEMU BASE UNITS, never lamports. transfer_checked requires the
        // decimals to match the mint exactly, so read decimals from the mint account —
        // never hardcode 9.
        Task<string> SendTip(string recipientWallet, ulong amountBaseUnits);   // returns signature
        Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits);     // a CEILING, not the price
        Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits);
    }

    public interface ISeatProvider
    {
        Task<bool> HasSeat(string packId);                         // wallet-scoped
        Task<IReadOnlyList<string>> MyPacks();                     // packIds this wallet holds a seat in
    }
}
```

`MockWallet` returns a deterministic fake signature and always succeeds. `MockSeatProvider` reads a JSON file at `Application.persistentDataPath/mock-seats.json` so A can test both the has-seat and no-seat paths without a chain.

### Seam B↔C — publish and gate

**Owner:** jointly authored; both consume.
**Artifacts:** `contracts/worker-api.md`, plus the `Pack` and `Seat` Anchor account structs frozen in `contracts/seat-pda.md`.

Worker request/response:

```
GET /challenge
  200 → { nonce, expiresAt }     SERVER-chosen nonce. A client-chosen nonce plus a skew
                                  window is a timestamp, not a single-use token.

GET /packs/<packId>/manifest
  Authorization: Solana-SignedMessage <base64 envelope over the server nonce>
                 The challenge bytes are FROZEN like every other wallet-signed message
                 in this system (contracts/fixtures/worker-challenge-v1.txt, byte-equality
                 tested in both languages):

                     Plemu Worker Challenge v1

                     Origin: <https origin>
                     Pack: <packId>
                     Nonce: <server-issued>
                     Expires: <YYYY-MM-DDTHH:MM:SSZ>

                 The first line must never be a prefix of any attestation message. This
                 is not stylistic: the SAME wallet key signs Worker challenges, Attestation
                 v2's monetization grant, and Pack Attestations. With challenge bytes left
                 unconstrained, a malicious portal shows "sign in to fetch your pack" over
                 bytes that are exactly a v2 monetization grant — harvesting a signed
                 right-to-distribute-and-monetize from someone who thought they were logging
                 in. Add a negative test in plemu-schemas: no wallet-signed message format
                 may equal or prefix any other.
  200 → { manifestUrl, sigUrl, attestUrl, artifactUrls: { "<sha256>": "<signed url>" }, expiresInSeconds }
  402 → { error: "no-seat", packId }              wallet holds no Seat PDA for this pack
  404 → { error: "unknown-pack", packId }
  410 → { error: "pack-revoked", packId }         see Review Focus #4
  503 → { error: "rpc-unavailable" }              FAIL CLOSED; never grant on a failed read
```

Three properties the implementation must honour, none of them obvious:

- **Nonce single-use requires strongly-consistent storage.** Workers KV is eventually consistent (propagation up to ~60s) and will accept the same nonce in two colos. Use a Durable Object or D1.
- **Read at `confirmed` or better.** `processed` is fork-visible, and devnet forks.
- **Fail closed on RPC error, with two independent providers.** Fail-open grants free access during an outage. Fail-closed denies paying customers during one — which is why the second provider is part of the design, not an optimisation.

Frozen account layout (Stream C must not change these field names or order after Contracts Day without notifying B):

```rust
#[account]
#[derive(InitSpace)]
pub struct Config {
    pub authority: Pubkey,          // 32 — the PLATFORM. Can revoke any pack. Not the operator.
    pub mint: Pubkey,               // 32 — the PLEMU mint. Every payment validates against this.
    pub treasury: Pubkey,           // 32 — PDA authority owning the treasury ATA
    pub price_per_seat: u64,        // 8  — updatable without a program upgrade
    pub paused: bool,               // 1  — halts purchase/assign when a notice lands
    pub bump: u8,                   // 1
    pub _reserved: [u8; 64],        // 64
}

#[account]
#[derive(InitSpace)]
pub struct Pack {
    pub operator: Pubkey,           // 32
    pub pack_id: [u8; 32],          // 32 — zero-padded ASCII slug, charset [a-z0-9-], NOT a hash
    pub entries_digest: [u8; 32],   // 32 — sha256 of sorted entry ids; ties the pack to its content
    pub capacity: u32,              // 4  — seats PURCHASED (create_pack always sets 0)
    pub seats_issued: u32,          // 4  — monotone
    pub seats_revoked: u32,         // 4  — monotone; assigned = issued - revoked
    pub created_at: i64,            // 8
    pub revoked: bool,              // 1
    pub bump: u8,                   // 1
    pub _reserved: [u8; 64],        // 64
}

#[account]
#[derive(InitSpace)]
pub struct Seat {
    pub pack: Pubkey,               // 32
    pub member: Pubkey,             // 32
    pub assigned_at: i64,           // 8
    pub expires_at: i64,            // 8 — 0 means perpetual; present so subscriptions need no migration
    pub bump: u8,                   // 1
    pub _reserved: [u8; 32],        // 32
}
```

Seeds: `Config` = `[b"config"]`; `Pack` = `[b"pack", pack_id]`; `Seat` = `[b"seat", pack.key().as_ref(), member.as_ref()]`. All store their canonical bump.

Five things about this layout are deliberate and must not be "simplified" away:

1. **`pack_id` is a zero-padded ASCII slug, not a hash.** A hash is unverifiable on chain — the program cannot check the preimage — so it buys nothing while costing the account any human-readable identity, permitting `SpeedRun-Club` and `speedrun-club` to become two packs, and requiring an off-chain slug↔hash map as load-bearing infrastructure. The slug is the same 32 bytes, is directly equal to the URL segment and to the attestation's `Pack:` line, and lets `create_pack` enforce canonicalization on chain. **This also resolves an inconsistency**: the R2 scheme, the Worker route, `SequenceStore`'s `pack:<packId>` and Task 1.2's fixture all treat packId as a string.
2. **`Config.authority` is the platform, separate from `Pack.operator`.** Without it, `set_pack_revoked` is operator-gated and a DMCA takedown of a pack depends on the operator's cooperation. Either the operator **or** the platform authority may revoke. This is a legal requirement, not a convenience.
3. **`entries_digest` ties the `Pack` account to the content it sells**, and is the on-chain half of the binding sidecar.
4. **Monotone counters instead of a mutable `assigned`.** Since revocation closes the `Seat` account, a single `assigned` field is a cache that every future close path must remember to decrement; drift oversells capacity or locks out paid seats. Two monotone counters make drift detectable against `getProgramAccounts`.
5. **`_reserved` tails.** This layout is frozen on day one, before anyone has written an instruction against it. The reserved bytes make that freeze survivable: a later field needs only a struct-definition change, not a realloc of every live account.

---

## 4. File structure

New and modified trees, with ownership. **Only the owning stream writes to its tree.**

```
plemu/
├── docs/
│   ├── superpowers/plans/platform-expansion.md    this document
│   ├── specs/                         [B]    SPEC.md stays FROZEN — do not edit
│   │   ├── SPEC.md                           (file untouched; superseded prose in ERRATA)
│   │   ├── SPEC-LOCAL.md             [B2]   new
│   │   ├── SPEC-PACK.md              [B2]   new
│   │   ├── SPEC-V2-DELTA.md          [B2b]  the global revocation list amendment
│   │   ├── SPEC-ERRATA.md            [B1]   where v1 prose is stale or superseded
│   │   └── ATTESTATION-V2.md         [B3]   new
│   └── vision-pt.md                          Portuguese product vision & roadmap
├── contracts/                     [Phase 1]  the three seams + frozen fixtures
├── tools/                             [B]    Rust workspace
│   └── crates/
│       ├── plemu-schemas/             [B4]   + PackAttestation, + AttestationV2
│       ├── plemu-manifest/            [B4]   + build-pack / sign-pack / verify-pack
│       ├── plemu-screen/              [B5]   v2 attestation, curator allowlist, release binary
│       ├── plemu-dat/                        (unchanged)
│       └── plemu-core-audit/          [B6]   (unchanged code; its data gets pinned)
├── registry/                          [B7]   + Supabase submission bridge
├── cores/                            [A/B]   build_*.sh [A];  cores.build.json +
│                                             expected-licenses.lock [B]
├── apps/device/
│   ├── native/                        [A2]   libretro_host — race fixes, GLES bridge
│   └── unity/                         [A]
│       └── Assets/_Game/Scripts/Runtime/
│           ├── Content/               [A1,A3,A4]  AndroidContentBootstrap, LocalContentStore,
│           │                                      LaunchGate.AuthorizeLocal, PackManifestSource
│           ├── Wallet/                [A5]   IWallet, SvalGuardWallet, Mock/ (from C)
│           ├── Emulation/             [A6,A7] SystemProfile entries, GLES surface
│           └── UI/                    [A3,A4,A5] lane badges, pack browser, tip button
├── programs/                          [C]    Anchor workspace
│   ├── plemu-seats/                   [C2]
│   ├── plemu-market/                  [C3]
│   ├── plemu-splits/                  [C4]
│   └── plemu-quests/                  [C5]
├── services/                          [C]
│   ├── worker/                        [C7]   Cloudflare Worker seat gate
│   └── supabase/                      [C8]   schema + RLS migrations
├── portal/                            [B]    Next.js + @solana/kit + Codama (moved from C)
└── .github/workflows/                 [B1]   repair all four
```

---

# Phase 0 — Environment & Verification

**All three developers. Nobody writes feature code until they have personally completed this.**

The point is not just installation. It is that each of you has seen the green suite, played the game in the editor, and **personally reproduced the on-device failure** — so when you later argue about the content-access fix you are arguing from having watched it fail.

### Task 0.1: Base toolchain

- [ ] **Step 1: Install Unity**

Unity Hub → install **6000.3.19f1** exactly (revision `7689f4515d75`). In the module picker, check **Android Build Support** with both **OpenJDK** and **Android SDK & NDK Tools**. This is what supplies NDK r27c; do not install a standalone NDK.

- [ ] **Step 2: Install the rest**

```bash
xcode-select --install                                    # macOS clang; the shim needs no CMake
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
rustup component add rustfmt clippy
brew install rgbds jq
```

`jq` is required — ai-kit's hooks parse tool JSON with it and fail silently without it.

- [ ] **Step 3: Verify versions**

```bash
rustc --version && cargo --version && rgbasm --version && jq --version && clang --version | head -1
ls /Applications/Unity/Hub/Editor/6000.3.19f1-arm64/PlaybackEngines/AndroidPlayer/NDK/source.properties
```

Expected: rustc reports a stable version, and the final `ls` prints the path (not "No such file"). If the NDK path is missing, the Android module was not installed — go back to Step 1.

- [ ] **Step 4: Clone and confirm a clean tree**

```bash
git clone <repo-url> && cd plemu
git status --short
```

Expected: empty output.

### Task 0.2: Agent tooling — ai-kit

**Read this before running anything.** `install.sh` **overwrites `CLAUDE.md`** in the target directory and adds `.claude/`, `CLAUDE.md`, `.mcp.json` and `.gitmodules` to `.gitignore`. This repo's parent workspace already carries a working ai-kit install with a hand-tuned `CLAUDE.md` and five `.claude/rules/` files. Back up before you install, then diff.

- [ ] **Step 1: Back up any existing config**

```bash
cp CLAUDE.md CLAUDE.md.pre-aikit 2>/dev/null || true
cp -r .claude .claude.pre-aikit 2>/dev/null || true
```

- [ ] **Step 2: Install ai-kit**

```bash
curl -fsSL https://raw.githubusercontent.com/solanabr/ai-kit/main/install.sh | bash
git submodule update --init --recursive
```

Use this raw GitHub URL rather than the vanity shortener the ai-kit README advertises: that shortener still redirects to the project's **pre-rename** path (`solanabr/solana-ai-kit`), which resolves today only through GitHub's own redirect and will break if anyone claims that name.

- [ ] **Step 3: Restore the project's own rules**

```bash
diff -rq .claude.pre-aikit/rules .claude/rules 2>/dev/null
# Keep this project's versions of anchor.md, pinocchio.md, rust.md, typescript.md, dotnet.md
cp -n .claude.pre-aikit/rules/*.md .claude/rules/ 2>/dev/null || true
cp CLAUDE.md.pre-aikit CLAUDE.md 2>/dev/null || true
```

- [ ] **Step 4: Do NOT install solana-game-skill separately**

It is already vendored as ai-kit's submodule at `.claude/skills/ext/solana-game`. Its own `install.sh` **overwrites your global `~/.claude/CLAUDE.md`** and destructively `rm -rf`s an existing `solana-dev` skill. It is also dormant — two commits, last touched 2026-01-30 — and ai-kit's copies of its agents and commands are the maintained ones.

- [ ] **Step 5: Configure keys and verify**

```bash
cp .env.example .env && $EDITOR .env      # set HELIUS_API_KEY
bash validate.sh
ls .claude/skills/ext/                     # expect 18 entries incl. solana-game, colosseum, metaplex
```

Then inside Claude Code: `/doctor`. Expected: no duplicate-command or duplicate-hook warnings.

### Task 0.3: Agent tooling — Unity

Two servers, chosen deliberately. Unity's **in-Editor MCP server is deprecated** — Unity's docs say "Unity MCP server is deprecated. Use the Unity CLI instead." The CLI is the supported first-party path, is free, and runs out-of-process. IvanMurzak's plugin is the in-Editor one: actively developed (v0.93.2, 2026-09-27), **CI-tested on Unity 6000.3**, and the only one exposing `tests-run` and `script-execute`.

- [ ] **Step 1: Unity CLI (first-party path)**

```bash
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | bash
cd apps/device/unity
unity pipeline install
unity mcp configure claude
unity mcp configure --list
```

Expected: `--list` shows `claude` with status configured. Do not install `com.unity.ai.assistant` — it is pre-release, subscription-gated, deprecated, and its coexistence with IvanMurzak's plugin on Unity 6.3 is untested with a known `System.Collections.Immutable` collision class.

- [ ] **Step 2: IvanMurzak plugin (in-Editor path)**

Requires Node `^20.19.0 || >=22.12.0`. Install via OpenUPM, **not** the git URL — the documented path `?path=/Unity-MCP-Plugin/Assets/root` no longer exists on `main`.

```bash
openupm add com.ivanmurzak.unity.mcp
```

- [ ] **Step 3: Generate the MCP config from the Editor — do not hand-write it**

Open the project in Unity, then **Window → AI Game Developer → Configure**. The server port is **auto-generated per project in the range 20000–29999**; a hand-written `port=8080` will silently fail to connect. The binary is `gamedev-mcp-server` at `Library/mcp-server/osx-arm64/` — the wiki's `unity-mcp-server` name is stale.

- [ ] **Step 4: Verify both**

Restart your MCP client **after** Unity has finished loading, then run `/mcp`.

Expected: `ai-game-developer` listed with 70+ tools. Then ask the agent: *"Run the EditMode tests and summarize failures."* Expected: it reports **34 passed, 0 failed**.

- [ ] **Step 5: Enable one Editor-driving agent at a time**

Two MCP servers writing the same scene concurrently is a data-loss hazard no config solves. Keep one enabled while working.

### Task 0.4: Prove the existing slice is green

- [ ] **Step 1: Rust workspace**

```bash
cd tools && cargo fmt --all --check && cargo clippy --workspace --all-targets -- -D warnings && cargo test --workspace
```

Expected: fmt and clippy silent, and **every** `test result:` line reports `0 failed`. Note there is no single line saying 95 — the 95 tests are spread across ~15 test binaries, each printing its own summary (the largest is `plemu-screen`'s 19). Count with `cargo test --workspace 2>&1 | grep -oE '^test result: ok\. [0-9]+' | grep -oE '[0-9]+' | paste -sd+ | bc` → `95`.

- [ ] **Step 2: Native stub smoke**

```bash
cd ../apps/device/native && bash build_editor.sh
```

Expected, in the final two lines: `PASS 16750 assertions` followed by `==> build_editor.sh OK`.

- [ ] **Step 3: Native real-core smoke**

```bash
bash build_realcore.sh
```

Expected: `REALCORE PASS 42 assertions` (the count is data-dependent; anything in the low 40s with `REALCORE PASS` is correct). First run clones SameBoy at the pinned commit and builds boot ROMs with rgbds — allow a few minutes.

- [ ] **Step 4: Offline enforcement e2e**

```bash
cd ../../../tools && bash scripts/e2e-demo.sh
```

Expected: `E2E DEMO PASSED`. Without `PLEMU_DEV_KEY_PEM` it skips signing and still asserts screening and manifest build — that is correct behaviour, not a failure.

### Task 0.5: Generate your own dev signing keys ⛔ BLOCKS every later task

**This repo ships with signatures whose private keys were never included.** That is deliberate, and it has a property worth understanding before you rotate: you can verify the entire trust chain on arrival — Task 0.4 just did — which proves the slice is real and the bytes were not altered in transit, *without* inheriting anyone else's signing key.

It also means you cannot sign anything yet. `stage-content.sh` will fail with `manifest signing key not found` until you finish this task, so do it before Task 0.6.

One developer runs this and commits the result; the other two pull it. The private keys are generated on that developer's machine, land **outside** the repo, and are shared out of band — never committed, never in the zip, never in CI.

- [ ] **Step 1: Read what it will touch**

```bash
head -25 tools/scripts/rotate-dev-keys.sh
```

It rewrites 11 files across two key families — the manifest signing keys (`tools/keys/manifest-signers.json`, the `testdata/sample-manifest` fixtures, their Unity mirrors, `PinnedKeys.cs`, `PinnedKeysTests.cs`) and the creator attestation wallet (both `registry/beta/*` entries). It replaces old key material by sweeping every text file rather than working from a list, because the old public key also appears in Rust test assertions and prose, and a hand-maintained list is how one gets missed.

- [ ] **Step 2: Run it**

```bash
bash tools/scripts/rotate-dev-keys.sh
```

Expected, on the last lines:

```
==> verifying the staged manifest
==> running the Rust suite
==> rotate-dev-keys OK - the keys are yours now
```

It refuses to run if `../dev-secrets/manifest-dev-a.pem` already exists, so a second developer cannot silently invalidate the first one's signatures. `PLEMU_FORCE_ROTATE=1` overrides that; the script is re-runnable and discovers what to replace from the tree rather than assuming the original values.

- [ ] **Step 3: Confirm the old material is gone**

```bash
grep -rl 'Dos7vSEN2rFFsinucJsaLyJ8B2EhkkcyrM6zu73rjcHa' . | grep -v '^./spec/SPEC.md$'
```

Expected: no output. `spec/SPEC.md` §6 still documents the original key values and **that is correct** — the file is frozen and never edited. The drift is recorded in `spec/SPEC-ERRATA.md`, which the script appends to.

- [ ] **Step 4: Re-verify everything**

```bash
cd tools && cargo test --workspace   # every suite green, 95 total
```

Then open Unity and run the Test Runner: **34 EditMode, 5 PlayMode**. PlayMode reads the freshly staged manifest, and the test clock derives from its `generatedAt`, so there is no wall-clock dependency.

- [ ] **Step 5: Commit the public half only**

```bash
git add tools/keys apps/device/unity/Assets registry spec/SPEC-ERRATA.md tools/testdata
git commit -m "chore: rotate to our own dev signing keys"
```

Confirm `git status` shows nothing under `../dev-secrets/` — it is outside the repo and `.gitignore` covers `*.pem` besides, but check anyway. Distribute the three PEMs to the other two developers out of band.

- [ ] **Step 6: File the test-fixture cleanup against Stream B**

`plemu-manifest/src/crypto.rs` and `tests/verify_integration.rs` keep **hardcoded copies** of the fixture signature and public key. The rotation script sweeps them, but they would be better off reading `testdata/sample-manifest/manifest.sig` and `keys/manifest-signers.json` directly — those files are the source of truth, and a copy that can go stale is a copy that will. Small, safe, and it makes the fixture set self-maintaining. Stream B, alongside B4.

### Task 0.6: Play the game

- [ ] **Step 1: Confirm your keys are in place**

Task 0.5 generated `../dev-secrets/manifest-dev-a.pem`. If you skipped it, staging fails here.

- [ ] **Step 2: Stage**

Open `apps/device/unity` in Unity 6000.3.19f1, then menu **Plemu → Stage Native + Content**. This builds the shim and the SameBoy core, assembles the beta manifest from `registry/beta`, signs it with the dev key, and copies the ROM to `StreamingAssets/Content/artifacts/<sha256>`.

- [ ] **Step 3: Play**

Open the `Boot` scene, press Play, navigate **Library → Libbet → Play** (Enter, Keypad Enter, or Z).

Expected: Libbet renders, animates, and produces audio. If the library is empty, staging did not complete — check the console for the staging script's output.

- [ ] **Step 4: Run the Unity suites**

Test Runner → EditMode, then PlayMode.

Expected: **34/34 EditMode**, **5/5 PlayMode**. PlayMode requires staging to have run and the staged manifest to be inside its 72h + 7d window — if PlayMode fails on freshness, re-run staging.

- [ ] **Step 5: Try other ROMs**

Drop another legally-obtained Game Boy homebrew into `registry/beta/gb/<slug>/artifact/`, write its `metadata.json` following `registry/beta/gb/libbet/`, regenerate its attestation with `cargo run -p plemu-manifest --example dev_attest`, re-stage, and play it. This is how you learn what the submission path actually demands.

### Task 0.7: Reproduce the device blocker

**Do not skip this.** Everyone needs to have seen it.

- [ ] **Step 1: Stage the Android natives**

```bash
cd apps/device/unity && bash stage-android.sh
```

Expected: both `libretro_host.so` and `libsameboy_libretro_android.so` reported as `ELF 64-bit … aarch64` and copied into `Assets/Plugins/Android/libs/arm64-v8a/`.

- [ ] **Step 2: Build the APK**

Menu **Plemu → Build PSG1 APK**.

Expected: `apps/device/build/android/Plemu.apk`, roughly 30 MB.

- [ ] **Step 3: Install on any arm64 Android device or emulator**

```bash
adb install -r apps/device/build/android/Plemu.apk
adb shell am start -n com.playsolana.games.plemu/com.unity3d.player.UnityPlayerGameActivity
adb logcat -s Unity:V | grep -i -E "manifest|catalog|denied"
```

Expected: the app launches, the library is **empty**, and the boot text reads "Run stage-content.sh, then reopen". This is the failure described in §1.3. It fails closed — no bypass — and fixing it is task **A1**.

### Task 0.8: Build and commit the shared `.claude` config

Done **once**, by one developer, then everyone else gets it by pulling. Phase 0.2 installed base ai-kit per machine; this turns it into a reviewable repo artifact so all three agents behave identically and the config is diffable like any other code.

- [ ] **Step 1: Start from the installed base, do not hand-roll**

```bash
cd /path/to/plemu
cp -r .claude .claude.base          # snapshot the stock ai-kit install for diffing
```

- [ ] **Step 2: Un-gitignore it**

ai-kit's installer adds `.claude/`, `CLAUDE.md`, `.mcp.json` and `.gitmodules` to `.gitignore`. Remove those four lines — a config nobody can review is a config that silently drifts between three machines.

- [ ] **Step 3: Keep this project's rules, drop what does not apply**

Keep `.claude/rules/{anchor,rust,typescript,dotnet,pinocchio}.md` — they carry the Anchor 1.x specifics (`CpiContext` takes a `Pubkey`, `transfer_checked`, `DISCRIMINATOR.len() + INIT_SPACE`) that this plan depends on. Delete agents and commands this project will never run, so `/doctor` stays quiet and the agent's choices stay narrow.

- [ ] **Step 4: Add the project-specific layer**

Write `CLAUDE.md` for Plemu specifically, on top of the stock Solana one:
- The three-stream file-ownership table from §4, so an agent refuses to edit another stream's tree.
- **`spec/SPEC.md` is never edited** — amendments go to `SPEC-V2-DELTA.md`.
- The frozen-bytes discipline: no CR/LF in placeholders, no trailing newline, cross-language fixtures.
- The error-handling rules from Global Constraints above.
- The verification commands per layer (`cargo test --workspace`, `build_editor.sh`, Unity Test Runner), with their real expected output.

- [ ] **Step 5: Pin the MCP set**

`.mcp.json` keeps `helius`, `solana-dev`, `context7`, `context-mode`, `surfpool`; add the IvanMurzak Unity server via **Window → AI Game Developer → Configure** (the port is per-project and must not be hand-written). Commit `.mcp.json`; keep every key in `.env`, which stays gitignored.

- [ ] **Step 6: Add a `/plemu-check` command**

One command that runs the whole gate — `cargo fmt --check`, `clippy -D warnings`, `cargo test --workspace`, `build_editor.sh` — and prints a single pass/fail summary. It is what a developer runs before every commit and what the Definition of Done points at.

- [ ] **Step 7: Verify and commit**

```bash
bash validate.sh && rm -rf .claude.base
git add .claude .mcp.json CLAUDE.md .gitignore && git commit -m "chore: shared Claude config for the three streams"
```

Expected: `/doctor` reports no duplicate commands or hooks; a second developer clones, opens Claude Code, and `/plemu-check` runs without any local setup.

### Phase 0 exit gate

All three developers can state, from their own machine: 95 Rust tests pass, `PASS 16750`, `REALCORE PASS`, `E2E DEMO PASSED`, 34 EditMode + 5 PlayMode pass, Libbet is playable in the editor, and the APK installs with an empty catalog. The dev keys have been rotated to the team's own (Task 0.5) and no shipped key material remains outside `spec/SPEC.md`. The shared `.claude` config is committed and `/plemu-check` runs green for all three.

---

# Phase 1 — Contracts Day

**Three days. All three developers, synchronous. Nothing parallelizes before this lands.**

The output is contract documents and working mocks, committed. After this, each stream codes against its neighbours' mocks and never blocks.

> **Budget three days, not one.** Task 1.4 alone — installing the Anchor 1.x / Agave toolchain on three machines and getting `anchor build` green — is a day by itself.
>
> **Five seams, not three.** The original plan froze three and left two unmocked, each of which becomes a stall:
> - **Submission record (B7 ↔ C8/C9).** B7's only stated verification is "submit through the portal (C9)", while C9's upload writes what B7 consumes — a two-way dependency with no contract. Freeze `contracts/submission-record.md` and have C ship a fixture row.
> - **Quest events (A8 ↔ C5).** Both tasks say "coordinate with the other". Freeze `contracts/quest-events.md` now; neither task owns it otherwise.
> - **A mock Worker.** A5's `WorkerSeatProvider` has nothing to talk to until C7 deploys. Ship a local stub serving the frozen 200/402/410/503 shapes, so A can build and test the device side immediately.
>
> **Also decide the assembly boundary here** (see A1's callout). `Plemu` is `noEngineReferences: true`; A1, A3 and A4 all want engine APIs in `Content/`. It is a 30-minute decision that blocks three tasks, and it belongs in `contracts/`.

### Task 1.1: Create the contracts tree

- [ ] **Step 1: Scaffold**

```bash
mkdir -p contracts/fixtures
git checkout -b chore/contracts-day-$(date +%d-%m-%Y)
```

- [ ] **Step 2: Copy §3 of this plan into the six contract files**

`contracts/pack-manifest.md`, `attestation-v2.md`, `local-lane-invariant.md`, `wallet-interface.md`, `worker-api.md`, `seat-pda.md`. Each gets the relevant §3 subsection verbatim as its normative body, plus a "Changes require notifying stream(s) X" line at the top.

### Task 1.2: Freeze the attestation fixtures (B leads)

- [ ] **Step 1: Write the failing test**

In `tools/crates/plemu-schemas/src/attestation.rs`:

```rust
#[test]
fn pack_attestation_message_matches_fixture_bytes() {
    let params = PackAttestationParams {
        pack_id: "speedrun-club",
        operator: "Dos7vSEN2rFFsinucJsaLyJ8B2EhkkcyrM6zu73rjcHa",
        entries_digest: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        seats: 50,
        timestamp: "2026-10-06T12:00:00Z",
    };
    let got = build_pack_attestation_message(&params);
    let want = include_str!("../../../../contracts/fixtures/pack-attestation-v1.txt");
    assert_eq!(got, want);
    assert!(!got.ends_with('\n'), "signed bytes must not end with a newline");
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
cd tools && cargo test -p plemu-schemas pack_attestation
```

Expected: FAIL — `build_pack_attestation_message` not found.

**Then mark it `#[ignore]` with a comment pointing at B4 step 3**, which is where the builder is implemented. Otherwise `cargo test --workspace` stays red through all of B1, B2 and B3 — making B3 step 5's stated expected output ("all tests pass") unreachable and the all-streams Definition of Done false for weeks.

- [ ] **Step 3: Write the fixture file**

`contracts/fixtures/pack-attestation-v1.txt`, exactly the §3 Seam A↔B template with those values substituted, LF endings, **no trailing newline**:

```bash
printf 'Plemu Pack Attestation v1\n\nPack: speedrun-club\nOperator: Dos7vSEN2rFFsinucJsaLyJ8B2EhkkcyrM6zu73rjcHa\nEntries: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\nSeats: 50\nTimestamp: 2026-10-06T12:00:00Z' > contracts/fixtures/pack-attestation-v1.txt
xxd contracts/fixtures/pack-attestation-v1.txt | tail -1
```

Expected: the last byte is `5a` (`Z`), not `0a`.

- [ ] **Step 4: Do the same for Attestation v2**

`contracts/fixtures/attestation-v2-creator.txt` and `attestation-v2-curator.txt`, following the §3 template. Same no-trailing-newline check.

- [ ] **Step 5: Mirror into Unity**

```bash
cp contracts/fixtures/*.txt apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures/
```

These are the cross-language frozen fixtures. Rust and C# both assert byte-equality against them — that discipline is what has kept the two verifiers in step so far, and it is how the new formats stay in step too.

- [ ] **Step 6: Commit**

```bash
git add contracts apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures
git commit -m "contracts: freeze pack attestation v1 and creator attestation v2 bytes"
```

### Task 1.3: Ship the wallet mocks (C leads) ⛔ BLOCKS A4, A5

- [ ] **Step 1: Create the assembly**

`apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/Plemu.Wallet.asmdef`, referencing `Plemu` only. Do **not** add wallet references to the `Plemu` assembly — it is deliberately `noEngineReferences: true` and the trust chain must stay engine-free.

- [ ] **Step 2: Write `IWallet.cs` and `ISeatProvider.cs`**

Exactly the interfaces in §3 Seam A↔C. No additions — if you need another method, amend the contract first and tell Stream A.

- [ ] **Step 3: Write `Mock/MockWallet.cs`**

```csharp
using System.Threading.Tasks;

namespace Plemu.Wallet.Mock
{
    public sealed class MockWallet : IWallet
    {
        private bool _isConnected;
        public bool IsConnected => _isConnected;
        public string PublicKey => _isConnected ? "MockWa11etDoNotUse11111111111111111111111111" : null;

        public Task<bool> Connect() { _isConnected = true; return Task.FromResult(true); }
        public Task<string> SendTip(string recipientWallet, ulong lamportsOfPlemu) => Sig("tip");
        public Task<string> PurchaseSeat(string packId, ulong priceInPlemu) => Sig("seat");
        public Task<string> PurchaseListing(string entryId, ulong priceInPlemu) => Sig("listing");

        private static Task<string> Sig(string kind) => Task.FromResult($"MockSig-{kind}-1111111111111111111111111111");
    }
}
```

- [ ] **Step 4: Write `Mock/MockSeatProvider.cs`**

Reads `Application.persistentDataPath/mock-seats.json` — shape `{"packs":["speedrun-club","translation-club"]}` — and returns membership from it. A missing file means no seats, so Stream A can test the 402 path by deleting it.

- [ ] **Step 5: Verify it compiles**

In Unity, wait for the domain reload.

Expected: no console errors, and `Plemu.Wallet` appears in the assembly list.

- [ ] **Step 6: Commit**

```bash
git add apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet
git commit -m "contracts: IWallet + ISeatProvider with mocks for stream A"
```

### Task 1.4: Freeze the seat PDA layout (C leads) ⛔ BLOCKS B's Worker integration tests

- [ ] **Step 1: Scaffold the Anchor workspace**

```bash
mkdir -p programs && cd programs && anchor init plemu --no-git && cd ..
```

- [ ] **Step 2: Write `Config`, `Pack` and `Seat` exactly as frozen in §3**

Field names, types and order are the contract. All three store `bump` and carry their `_reserved` tail. Do not drop the reserved bytes to "save rent" — they are what makes a day-one freeze survivable.

- [ ] **Step 3: Verify it builds**

```bash
cd programs/plemu && anchor build
```

Expected: build succeeds and `target/idl/plemu_seats.json` exists.

- [ ] **Step 4: Commit the IDL alongside the source**

```bash
git add programs && git commit -m "contracts: freeze Pack and Seat account layouts"
```

### Phase 1 exit gate

`contracts/` holds six documents and the frozen fixtures. `Plemu.Wallet` compiles with mocks. `anchor build` emits an IDL containing `Pack` and `Seat` with the frozen fields. All three developers have read all six contracts. Merge to `main` before streams diverge.

---

# Stream A — Runtime

**Owns:** `apps/device/unity/**`, `apps/device/native/**`, `cores/build_*.sh`
**Never edits:** `spec/`, `tools/`, `registry/`, `.github/`, `programs/`, `services/`, `portal/`, `contracts/`, `docs/`, and the two pin files `cores/cores.build.json` / `cores/expected-licenses.lock`

> **Ownership correction.** The original split gave A all of `cores/` except two files that B owns. That creates a deadlock: B6 computes `licenseSha256` by hashing `cores/.cache/<core>/LICENSE`, but that cache is populated only by the clone inside A6's build scripts — and A6 cannot write those scripts without the commit pin, because they hard-fail if `git rev-parse HEAD` does not match. Each core becomes a three-message round trip between two people, fifteen times, recreating exactly the drift §1.2 item 5 diagnoses. **B owns the two pin files and does the clone in B6 step 2; A owns only the build scripts and consumes the pins.**

**Order: A1 → A2 → A3 → A4 → A5 → A6 → A9 → A8**, with A7 running in background from the start — it is the longest pole in the plan and the least coupled. A6 is independent of A3–A5; slot it into whatever gaps open. A9 is a release blocker, not a late nicety.

### Task A1: On-device content access ⛔ BLOCKS every device demo

The single highest-value task in this plan. Until it lands, the APK installs and plays nothing.

> **Assembly boundary — read before writing a single line.** `Assets/_Game/Scripts/Runtime/_Game.asmdef` is assembly `Plemu` with **`"noEngineReferences": true`**, and it covers `Content/` and `Core/`. Zero files in either directory reference `UnityEngine` today; the trust chain is deliberately engine-free. **Any file using `UnityEngine`, `UnityWebRequest`, `Application.*` or `AndroidJavaObject` cannot live in `Content/`.** Create a new assembly `Plemu.Platform` (`Scripts/Runtime/Platform/Plemu.Platform.asmdef`, references `Plemu`) and put every engine-dependent shim there. Affects A1, A3 and A4 — decide once, here.
>
> Follow `LibraryStore`'s pattern for anything that touches disk: it takes an **injected path** and never calls `Application.*`. That is why it lives in the engine-free assembly and why its tests run in EditMode.

**Files:**
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Platform/Plemu.Platform.asmdef`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Platform/AndroidContentBootstrap.cs`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/BundledManifestSource.cs` (engine-free)
- Modify: `apps/device/unity/Assets/_Game/Tests/EditMode/_Game.Tests.asmdef` (add `Plemu.App`, `Plemu.Platform`)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/ContentPaths.cs:21-28` (platform branch)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/LaunchGate.cs:49-50` (Expired exemption)
- Test: `apps/device/unity/Assets/_Game/Tests/EditMode/BundledManifestSourceTests.cs`

**Interfaces:**
- Consumes: `IManifestSource`, `IContentStore`, `FileManifestSource`, `FileContentStore` (existing).
- Produces: `AndroidContentBootstrap.EnsureContentStagedAsync() → Task<string>` returning the resolved content root; `BundledManifestSource : IManifestSource`; a new `bool IsBundled { get; }` member on `IManifestSource` (`FileManifestSource` returns `false`, `BundledManifestSource` returns `true`). A4 extends the same interface with `ManifestId` — coordinate so the interface changes once.

- [ ] **Step 1: Write the failing test for the Expired exemption**

SPEC §2.3 step 6 says bundled starter-pack titles remain playable when Expired. `LaunchGate.cs:13-15` documents this as unimplemented. This is Review Focus #5 — a device that sits in a box past the grace window and boots offline currently plays nothing.

```csharp
[Test]
public void Authorize_Expired_BundledTitle_StillIssuesTicket()
{
    var clock = new FakeClock(DateTimeOffset.Parse("2027-01-01T00:00:00Z")); // far past grace
    var decision = LaunchGate.Authorize("gb/libbet", _bundledSource, _store, clock);
    Assert.IsTrue(decision.IsAuthorized, "bundled starter-pack titles must survive Expired");
    Assert.AreEqual("gb/libbet", decision.Ticket.TitleId);
}

[Test]
public void Authorize_Expired_NonBundledTitle_StillDenies()
{
    var clock = new FakeClock(DateTimeOffset.Parse("2027-01-01T00:00:00Z"));
    var decision = LaunchGate.Authorize("gb/libbet", _cdnSource, _store, clock);
    Assert.IsFalse(decision.IsAuthorized);
    Assert.AreEqual(DenialReason.ManifestExpired, decision.Denial.Reason);
}
```

- [ ] **Step 2: Run and watch both fail**

Unity Test Runner → EditMode, filter `Authorize_Expired`.
Expected: the first fails with `ManifestExpired`; the second passes already.

- [ ] **Step 3: Add the bundled discriminator**

`BundledManifestSource` wraps a `FileManifestSource` rooted at the APK-baked content and exposes `bool IsBundled => true`. Add `bool IsBundled { get; }` to `IManifestSource` with `FileManifestSource` returning `false`. In `LaunchGate.cs`, change the freshness gate from an unconditional deny to:

```csharp
if (verification.Freshness == Freshness.Expired && !manifests.IsBundled)
    return LaunchDecision.Denied(DenialReason.ManifestExpired,
        "manifest expired beyond grace and this is not the bundled starter pack");
```

- [ ] **Step 4: Run the tests**

Expected: both pass; the other 34 EditMode tests still pass (`36 passed` total after this task's two additions).

- [ ] **Step 4b: Make the bundled manifest genuinely unreachable to `System.IO`**

The exemption you just built is only as trustworthy as the bytes it exempts. SPEC §2.3 step 6 says bundled titles are *"verified against the baked-in manifest shipped inside the signed APK"* — so `BundledManifestSource` must **not** wrap a `FileManifestSource` over `persistentDataPath`. That copy is mutable: anyone with `persistentDataPath` write access (a rooted device, `adb` on a debuggable build, a backup restore, or the bug fixed in Step 5b) could drop in an older validly-signed manifest and get a **permanently playable, never-expiring, unrevocable lane** — exactly the property the threat model exists to prevent.

Ship the manifest and its signature as compiled-in bytes: a `TextAsset` under `Resources/`, or byte constants. Pin `BundledManifestSha256` as a compile-time constant and refuse the exemption on mismatch. Also set `android:allowBackup="false"` in a custom `AndroidManifest.xml` — there is none in the repo today, so Unity generates one and the attribute defaults to true.

```csharp
[Test]
public void BundledExemption_IsRefusedWhenTheBytesDoNotMatchTheCompiledPin()
{
    var tampered = BundledManifestSource.FromBytes(OlderValidlySignedManifest());
    var decision = LaunchGate.Authorize("gb/libbet", tampered, _store, _farFutureClock);
    Assert.IsFalse(decision.IsAuthorized);
}
```

- [ ] **Step 4c: Fix the C# unparseable-`expiresAt` divergence here, not later**

`ManifestVerifier.cs:93-94` falls back to `Expired` when `expiresAt` will not parse; Rust returns a hard error. Once Step 3 exempts Expired for bundled manifests, a malformed timestamp in a bundled manifest becomes **permanently playable** instead of soft-denied. Make it a `VerificationFailure`, matching Rust. (B4 step 1 identifies this defect; **A1 is the task that owns the file** — without this step the handoff is addressed to nobody.)

- [ ] **Step 5: Write `AndroidContentBootstrap`** (in `Plemu.Platform`, not `Content/`)

On Android, `StreamingAssets` lives inside the APK as a `jar:file://` URL. `System.IO` cannot read it. Copy once to `persistentDataPath` on first run, then every existing `System.IO` path works unchanged — which is why this fix touches only `ContentPaths`, not `FileManifestSource`, `FileContentStore`, or `LaunchGate`.

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Plemu.Content
{
    public static class AndroidContentBootstrap
    {
        private const string MarkerFile = ".staged-v1";

        // Every file that must exist on a real filesystem before the trust chain runs.
        private static readonly string[] RequiredFiles =
        {
            "manifest-beta.json",
            "manifest-beta.json.sig",
        };

        public static async Task<string> EnsureContentStagedAsync()
        {
            var dest = Path.Combine(Application.persistentDataPath, "Content");
            if (File.Exists(Path.Combine(dest, MarkerFile))) return dest;

            Directory.CreateDirectory(Path.Combine(dest, "artifacts"));
            var src = Path.Combine(Application.streamingAssetsPath, "Content");

            foreach (var rel in RequiredFiles)
                if (!await CopyOneAsync(Path.Combine(src, rel), Path.Combine(dest, rel)))
                    return null;   // fail closed: caller sees an empty catalog, never a bypass

            foreach (var sha in await ReadArtifactListAsync(Path.Combine(dest, "manifest-beta.json")))
                await CopyOneAsync(Path.Combine(src, "artifacts", sha), Path.Combine(dest, "artifacts", sha));

            File.WriteAllText(Path.Combine(dest, MarkerFile), Application.version);
            return dest;
        }

        private static async Task<bool> CopyOneAsync(string srcUrl, string destPath)
        {
            using var req = UnityWebRequest.Get(srcUrl);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Plemu] staging failed for {srcUrl}: {req.error}");
                return false;
            }
            File.WriteAllBytes(destPath, req.downloadHandler.data);
            return true;
        }

        private static async Task<IReadOnlyList<string>> ReadArtifactListAsync(string manifestPath)
        {
            await Task.Yield();
            // Parsing here is for COPY PLANNING ONLY. It is unverified JSON and grants no trust
            // over CONTENT: LaunchGate re-verifies the signature and re-hashes every artifact.
            // But it DOES grant control over a DESTINATION PATH, which no later check audits —
            // so each digest must be validated before it is ever combined into a path.
            // .NET Path.Combine does NOT normalise, and returns its second argument verbatim
            // when that argument is rooted: an unvalidated "sha" of "/data/.../manifest-beta.json"
            // writes exactly there.
            var json = File.ReadAllText(manifestPath);
            var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<Manifest>(json);
            var shas = new List<string>();
            foreach (var e in manifest?.Entries ?? new List<ManifestEntry>())
                if (Digest.IsHex64(e?.Sha256)) shas.Add(e.Sha256);
            return shas;
        }
    }
}
```

- [ ] **Step 5b: Add `Digest.IsHex64` and apply it at EVERY digest→path sink**

`Digest.IsHex64(s)` is `s?.Length == 64 && every char in [0-9a-f]`, in the engine-free `Plemu` assembly. Apply it in `ReadArtifactListAsync` **and** in the already-shipped signed path, which has the same unvalidated sink today: `FileContentStore.ArtifactPath` (`Path.Combine(_contentDirectory, "artifacts", sha256)`) and `FileContentStore.Quarantine` (`Path.Combine(quarantineDir, sha256)` followed by `File.Delete` and `File.Move` — a delete/move primitive). Also assert `Path.GetFullPath(dest)` starts with the artifacts root.

```csharp
[TestCase("../../manifest-beta.json")]
[TestCase("/data/data/com.playsolana.games.plemu/files/Content/manifest-beta.json")]
[TestCase("3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a")]  // 63 chars
[TestCase("3607412031C8287CF878299CE96E581E85B852DDE703806343B95576FA3FF1A9")] // uppercase
public void ArtifactPath_RefusesAnythingThatIsNotALowercaseHex64Digest(string bad)
{
    Assert.Throws<ArgumentException>(() => _store.ArtifactPath(bad));
}
```

`plemu-manifest build` already enforces this server-side (`build.rs:137`, `is_hex64` at `:316`) — B4's `build-pack` must reuse that same function, and the device needs the check independently as defence in depth.

- [ ] **Step 6: Branch `ContentPaths`**

```csharp
public static string ContentDirectory =>
#if UNITY_ANDROID && !UNITY_EDITOR
    _stagedRoot ?? Path.Combine(Application.persistentDataPath, "Content");
#else
    Path.Combine(Application.streamingAssetsPath, "Content");
#endif
```

`PlemuApp` awaits `EnsureContentStagedAsync()` before `LoadCatalogIntoLibrary()`, and shows "Preparing content…" while it runs. A null return leaves the catalog empty — the existing fail-closed behaviour, unchanged.

- [ ] **Step 7: Build, install, and verify on device**

```bash
cd apps/device/unity && bash stage-android.sh
# Unity: Plemu -> Build PSG1 APK
adb install -r ../build/android/Plemu.apk
adb shell am start -n com.playsolana.games.plemu/com.unity3d.player.UnityPlayerGameActivity
adb logcat -s Unity:V | grep -i -E "plemu|catalog|staging"
```

Expected: the library shows **Libbet**, selecting it and pressing Play renders the game on the device. This is the moment the APK becomes a product.

- [ ] **Step 8: Commit**

```bash
git add apps/device/unity/Assets/_Game
git commit -m "feat(device): stage StreamingAssets to persistentDataPath on Android; honour bundled Expired exemption"
```

### Task A2: Fix the five native defects

**Files:**
- Modify: `apps/device/native/libretro_host/src/internal.h:40-47` (ring struct)
- Modify: `apps/device/native/libretro_host/src/ring_buffer.c:48-118`
- Modify: `apps/device/native/libretro_host/src/libretro_host.c:311-323, 371-396, 423`
- Modify: `apps/device/native/libretro_host/src/video.c:56-129`
- Modify: `apps/device/native/smoke/rh_smoke.c` (new assertions)

**Interfaces:**
- Consumes: nothing new.
- Produces: no ABI change. All 22 exported signatures stay identical; `LibretroHostNative.cs` needs no edit.

- [ ] **Step 1: Write the failing concurrency assertion**

Add to `rh_smoke.c` a phase that spawns a reader thread hammering `rh_read_audio` while the main thread runs 5,000 frames, then asserts `count <= RH_AUDIO_RING_FRAMES` and that total frames read plus frames still resident plus drops equals frames pushed. Run it under ThreadSanitizer.

```bash
cd apps/device/native
clang -std=c11 -fsanitize=thread -g -O1 -Ilibretro_host/include \
  libretro_host/src/*.c smoke/rh_smoke.c -o build/rh_smoke_tsan -ldl
./build/rh_smoke_tsan build/stub_libretro.dylib
```

Expected: TSan reports data races on `head`, `tail`, and `count` in `ring_buffer.c`. That is defect B1 from §1.4, reproduced.

- [ ] **Step 2: Make the ring a real SPSC queue**

Single producer (emulation thread), single consumer (audio thread). Use C11 atomics rather than a mutex — a mutex in `OnAudioFilterRead` risks audio-thread priority inversion.

```c
#include <stdatomic.h>

typedef struct {
    int16_t  *buf;
    uint32_t  cap;              /* frames; power of two */
    _Atomic uint32_t head;      /* producer writes */
    _Atomic uint32_t tail;      /* consumer writes */
    _Atomic uint32_t drops;
} rh_ring;
```

Derive occupancy as `head - tail` with unsigned wraparound instead of a shared `count` — that removes the read-modify-write both sides were racing on. Producer: load `tail` with `memory_order_acquire`, store `head` with `memory_order_release`. Consumer mirrors it. Keep drop-oldest by advancing `tail` from the producer only when the ring is full, and document that this is the one place the producer touches `tail`.

- [ ] **Step 3: Rerun TSan**

Expected: no data races reported, and `PASS <n> assertions` where `<n>` exceeds 16,750 by your new assertions.

- [ ] **Step 4: Fix B2 — `rh_unload_game` releases the slot**

`libretro_host.h:20-21` credits the release to `rh_unload_game() + rh_destroy()` as a pair, which today means `unload_game` → `load_core` on one handle returns `RH_ERR_BUSY`. Either clear `c->core_loaded` and `g_active` in `rh_unload_game`, or tighten the header to say the slot is released by `rh_destroy` alone. Pick one and make the smoke test assert it. Add a smoke assertion: `unload_game` then `load_core` on a fresh handle returns `RH_OK`, not `RH_ERR_BUSY`.

- [ ] **Step 5: Fix B3 and B4 — frame lock lifecycle**

In `rh_unlock_frame`, unlock **before** the `!c->game_loaded` early return, so an unload-then-unlock ordering cannot leak the lock permanently. In `rh_lock_frame`, return `RH_ERR_STATE` if `v->locked` is already set instead of self-deadlocking. In `rh_video_free`, refuse to destroy a held mutex — unlock it first if `v->locked`. And in `video.c:56-59`, do not call `rh_video_free` when `pthread_mutex_init` failed; free the buffers directly.

- [ ] **Step 6: Fix B5 — validate video geometry**

In `rh_video_refresh_cb`, reject a frame where `width > v->max_w`, `height > v->max_h`, or `pitch < width * bpp`, logging once and treating it as a dupe rather than copying. A misbehaving core must not cause a source over-read.

- [ ] **Step 7: Full gate**

```bash
bash build_editor.sh && bash build_realcore.sh
```

Expected: `PASS <n> assertions` (n > 16750) and `REALCORE PASS <n> assertions`. Then re-run the Unity PlayMode suite — `AudioReadDuringStop_IsRaceSafe_NoCrash` must still pass.

- [ ] **Step 8: Commit**

```bash
git add apps/device/native
git commit -m "fix(native): lock-free SPSC audio ring, slot release on unload, frame-lock lifecycle, geometry validation"
```

### Task A3: The local lane

**Files:**
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/LocalContentStore.cs`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/LocalLibrary.cs`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/SafPicker.cs` (AndroidJNI)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/LaunchGate.cs` (add `AuthorizeLocal`)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/DenialReason.cs` (append variants)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/CatalogEntry.cs` (add `Lane`)
- Test: `apps/device/unity/Assets/_Game/Tests/EditMode/LocalLaneIsolationTests.cs`

**Interfaces:**
- Consumes: `contracts/local-lane-invariant.md` (from B2), `IContentStore`, `LaunchDecision`, `LaunchTicket`.
- Produces: `LaunchGate.AuthorizeLocal(string localId, LocalLibrary library, IClock clock) → LaunchDecision`; `enum ContentLane { Signed, Local }`; `LocalLibrary.Import(string sourceUri) → LocalEntry`.

- [ ] **Step 1: Write the isolation test first — it is the whole point of the lane**

```csharp
[Test]
public void SignedAuthorize_NeverAcceptsALocalEntry()
{
    var local = _library.Import(TestFile("homebrew.gb"));
    var decision = LaunchGate.Authorize(local.Id, _signedSource, _signedStore, _clock);
    Assert.IsFalse(decision.IsAuthorized);
    Assert.AreEqual(DenialReason.TitleNotFound, decision.Denial.Reason);
}

[Test]
public void LocalEntry_IsNeverEligibleForPlatformFeatures()
{
    var local = _library.Import(TestFile("homebrew.gb"));
    var entry = _library.ToCatalogEntry(local);
    Assert.AreEqual(ContentLane.Local, entry.Lane);
    Assert.IsFalse(entry.CanTip);
    Assert.IsFalse(entry.CanMint);
    Assert.IsFalse(entry.CanPromote);
    Assert.IsNull(entry.CreatorWallet, "a local entry has no attested payee");
}

[Test]
public void LocalStore_RootIsOutsideEverySignedPath()
{
    StringAssert.Contains("/local/", _library.Root);
    Assert.IsFalse(_library.Root.Contains("/Content/artifacts"));
}
```

- [ ] **Step 2: Write the hostile-file tests (Review Focus #3)**

SAF hands the app an arbitrary user file. `plemu-screen`'s caps run server-side only, so the local lane needs its own.

```csharp
[Test]
public void Import_RejectsFileOverSystemSizeCap()
{
    var huge = TestFileOfSize(129 * 1024 * 1024);   // gb cap is 64 MiB
    var ex = Assert.Throws<LocalImportException>(() => _library.Import(huge));
    Assert.AreEqual(LocalImportError.TooLarge, ex.Error);
}

[Test]
public void Import_RejectsArchive()
{
    var ex = Assert.Throws<LocalImportException>(() => _library.Import(TestFile("bomb.zip")));
    Assert.AreEqual(LocalImportError.ArchiveNotPermitted, ex.Error);
}

[Test]
public void Import_RejectsUnrecognisedHeader()
{
    var ex = Assert.Throws<LocalImportException>(() => _library.Import(TestFile("random.bin")));
    Assert.AreEqual(LocalImportError.UnknownFormat, ex.Error);
}
```

Reuse the per-system caps `plemu-screen` already defines (`checks.rs::system_size_cap`): 1 MiB for tic80/pico8/wasm4/microw8, 64 MiB default, 256 MiB psp, 700 MiB ps1, 1 GiB dc. Archives are refused outright in the local lane — the archive-safety analysis that makes them acceptable server-side does not exist on device.

- [ ] **Step 3: Run and watch them fail**

Expected: all six fail — `LocalLibrary` does not exist.

- [ ] **Step 4: Implement `LocalContentStore` and `LocalLibrary`**

Root at `Application.persistentDataPath/local/`. Store imported files content-addressed by their SHA-256 under `local/artifacts/<sha256>`, with an index at `local/index.json` holding `{sha256, displayName, system, importedAt, sourceDisplayName}`. Detect `system` by header sniffing, reusing the same magic checks `plemu-screen`'s `check_hdr_valid` uses (GB logo + checksum, NES iNES magic, GBA, MD `SEGA`, SMS/GG `TMR SEGA`, A26, TIC-80 chunk stream, WASM-4). Unrecognised header is a hard reject.

- [ ] **Step 5: Implement `LaunchGate.AuthorizeLocal`**

A separate entry point. It re-hashes the artifact on every launch exactly as the signed path does — local content is untrusted for *provenance*, but integrity checking is free and catches corrupted imports. It performs no manifest verification, no freshness check, and no revocation check, because none of those concepts apply. Append `DenialReason.LocalContentMissing` and `DenialReason.LocalHashMismatch`.

Leave the existing `Authorize` untouched, including its docstring. That is deliberate: it keeps its invariant and its whole test suite.

- [ ] **Step 6: Implement `SafPicker`**

`AndroidJavaObject` wrapper over `ACTION_OPEN_DOCUMENT` with `Intent.CATEGORY_OPENABLE` and MIME `*/*`. Read the returned `content://` URI through a `ContentResolver` input stream. **Declare no permission in the manifest** — the picker grants per-URI access and nothing more. Verify with:

```bash
aapt dump permissions apps/device/build/android/Plemu.apk
```

Expected: **no** `READ_EXTERNAL_STORAGE`, no `MANAGE_EXTERNAL_STORAGE`, no storage permission of any kind.

- [ ] **Step 7: Add the lane badge to the UI**

`CatalogEntry` gains `ContentLane Lane`, `bool CanTip`, `bool CanMint`, `bool CanPromote`, `string CreatorWallet`. `PlemuApp.BuildLibraryCards` renders a visible `[LOCAL — UNVERIFIED]` badge and the library shows two sections. The tip button (A5) is hidden, not merely disabled, for local entries.

- [ ] **Step 8: Run the full suite**

Expected: all EditMode tests pass, including the six new ones and the original 34.

- [ ] **Step 9: Commit**

```bash
git add apps/device/unity/Assets/_Game
git commit -m "feat(device): local lane with SAF import, isolation invariant, and per-system import limits"
```

### Task A4: Multi-manifest fetch, per-pack anti-rollback, pack browser

**Files:**
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/PackManifestSource.cs`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/SequenceStore.cs`
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/ContentPaths.cs` (multi-source catalog)
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/PlemuApp.cs:142-151, 307-343` (sections, scroll)
- Test: `apps/device/unity/Assets/_Game/Tests/EditMode/SequenceStoreTests.cs`, `CrossLaneRevocationTests.cs`

**Interfaces:**
- Consumes: `contracts/pack-manifest.md`, `ISeatProvider` (mock from Phase 1), `IManifestSource.IsBundled` (from A1).
- Produces: `SequenceStore.HighWater(string manifestId) → long`, `SequenceStore.Accept(string manifestId, long sequence) → bool`; `IManifestSource.ManifestId { get; }` returning the identity strings frozen in `SPEC-PACK.md` (`store-beta`, `store-main`, `pack:<packId>`, `bundled`); `ContentCatalog.ManifestsListing(string sha256) → IReadOnlyList<string>`.

- [ ] **Step 1: Write the anti-rollback test**

The current `LaunchGate` never passes `minSequence` — it defaults to `-1`, disabled. With N manifests, one global high-water mark is wrong; each manifest identity needs its own.

```csharp
[Test]
public void SequenceStore_IsKeyedPerManifest()
{
    _store.Accept("store-beta", 42);
    Assert.AreEqual(42, _store.HighWater("store-beta"));
    Assert.AreEqual(0,  _store.HighWater("pack:speedrun-club"),
        "a store sequence must not raise a pack's floor");
}

[Test]
public void SequenceStore_RejectsRollback()
{
    _store.Accept("pack:speedrun-club", 7);
    Assert.IsFalse(_store.Accept("pack:speedrun-club", 6));
    Assert.IsTrue(_store.Accept("pack:speedrun-club", 7), "equal is allowed for re-reads");
}
```

- [ ] **Step 2: Write the cross-lane revocation test (Review Focus #2)**

One sha256 can appear in both a store manifest and a pack manifest. Content-addressed storage means one file on disk. Revoking it in one manifest must not leave it playable through the other.

```csharp
[Test]
public void RevokedInStore_StillPlayableViaPackThatDidNotRevokeIt()
{
    // Documented, intentional: each manifest is independently sequenced and revoked (SPEC 1.0).
    // A takedown must therefore be applied to EVERY manifest listing the sha256.
    // This test pins the behaviour so it is a decision, not an accident.
    var viaStore = LaunchGate.Authorize("gb/shared", _storeSourceRevoked, _store, _clock);
    var viaPack  = LaunchGate.Authorize("gb/shared", _packSourceLive,     _store, _clock);
    Assert.AreEqual(DenialReason.Revoked, viaStore.Denial.Reason);
    Assert.IsTrue(viaPack.IsAuthorized);
}

[Test]
public void ContentStore_ReportsEveryManifestListingAnArtifact()
{
    var manifests = _catalog.ManifestsListing("3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9");
    CollectionAssert.AreEquivalent(new[] { "store-beta", "pack:speedrun-club" }, manifests);
}
```

The second test is the operational hook: Stream B's takedown runbook uses the same query server-side to enumerate every manifest that must be rebuilt for one sha256. File that requirement against B1 when this lands.

- [ ] **Step 3: Run and watch them fail**

Expected: FAIL — `SequenceStore` and `ManifestsListing` do not exist.

- [ ] **Step 4: Implement `SequenceStore`**

Atomic JSON at `persistentDataPath/sequences.json`, keyed by manifest id, written via temp-file-then-rename. Follow `LibraryStore`'s existing pattern exactly — including never throwing on a corrupt file, recovering to empty instead.

- [ ] **Step 5: Wire `minSequence` in — as an OVERLOAD, not a signature change**

§2.3 is a locked decision: *"The existing `Authorize(...)` keeps its 'the ONLY path' docstring and **its entire test suite unchanged**."* Changing its arity breaks all 34 EditMode tests. Add an overload taking a `SequenceStore`, and have the 4-argument form delegate with anti-rollback disabled — or thread the store through `IManifestSource`, which A1 and A4 are both already extending. It passes `store.HighWater(manifests.ManifestId)` to `ManifestVerifier.Verify` and calls `Accept` on success, activating anti-rollback on device for the first time.

- [ ] **Step 5a: Make `IContentStore` digest-keyed, not title-keyed**

Today `FileContentStore.ResolveSha256` maps titleId → sha256 by reading **its own, unverified** manifest, while `LaunchGate` compares against the **verified** manifest's `entry.Sha256`. With N manifests these are different documents, and nothing requires entry ids to be unique across them — §1.0's two-tier model explicitly expects the same id in beta and main. A pack whose entry id collides with a store title but carries a different sha256 makes the gate mismatch and call `store.Quarantine(titleId)`, which **moves the store's legitimate artifact out of `artifacts/`** — a confused deputy that lets any pack evict content for every manifest listing it.

Change `IContentStore` to `OpenRead(string sha256)` / `Quarantine(string sha256)`, with `LaunchGate` passing `entry.Sha256` from the manifest it just verified. This deletes the unverified-manifest read entirely. Do it now, before three sources exist.

- [ ] **Step 5c: Make `SequenceStore` fail CLOSED on corruption**

Do **not** copy `LibraryStore`'s recover-to-empty behaviour here. That is correct for an install-state cache and a vulnerability for an anti-rollback floor: deleting or truncating one file resets every floor to zero and every old signed manifest — including pre-revocation ones — is accepted again. One `rm` turns anti-rollback off.

Ship a compile-time `MinimumSequence` per known identity inside the APK, so a wiped store cannot drop below the value that shipped. On corrupt-or-missing, deny non-bundled Fresh/Grace treatment until a manifest at or above that floor is accepted.

```csharp
[Test]
public void SequenceStore_CorruptFile_DoesNotResetTheFloorToZero()
{
    File.WriteAllText(_path, "{ not json");
    Assert.AreEqual(CompiledFloors.For("store-beta"), _store.HighWater("store-beta"));
}
```

Also: **never prune** identities absent from `MyPacks()`. A pack that disappears and returns with a pruned floor is a rollback with no file access required.

- [ ] **Step 5b: Verify the pack binding before the manifest**

The manifest identity used as the `SequenceStore` key must come from the **signed binding sidecar**, never from the URL the device requested — otherwise the rollback floor is attacker-chosen (§2.4). Order: verify `<packId>.binding.json` against the pinned keys → confirm its `packId` matches the pack requested and its `manifestSha256` matches the sha256 of the received manifest bytes → only then run the normal §2.3 sequence. Add:

```csharp
[Test]
public void PackManifest_WithMismatchedBinding_IsRejectedBeforeParsing()
{
    var src = PackSourceWithBindingFor("other-pack");
    var result = src.GetVerifiedManifest();
    Assert.IsNull(result, "a manifest whose binding names a different pack must never load");
}
```

- [ ] **Step 6: Implement `PackManifestSource` and multi-source catalog**

`ContentPaths.LoadCatalog` becomes `LoadCatalog(ISeatProvider seats)`, concatenating: the bundled starter manifest, `manifest-beta.json`, `manifest-main.json`, and one `PackManifestSource` per pack from `seats.MyPacks()`. Each source verifies independently; a failing source yields zero entries and never poisons the others.

- [ ] **Step 7: Build the pack browser and the beta warning**

SPEC §1.0 mandates that beta entries render behind an "unverified / community" warning — currently unimplemented. `PlemuApp` gains sections (Main · Beta · Packs · Local) inside a `ScrollRect`; the current flat `VerticalLayoutGroup` with a single `_selectedIndex` cannot express this, so `SetSelection` becomes section-aware.

- [ ] **Step 8: Run the full suite**

Expected: all EditMode tests pass; PlayMode 5/5 still passes.

- [ ] **Step 9: Commit**

```bash
git add apps/device/unity/Assets/_Game
git commit -m "feat(device): per-manifest anti-rollback, multi-source catalog, pack browser, beta warning UI"
```

### Task A5: Wallet, tips, seat-gated pack fetch

**Files:**
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/SvalGuardWallet.cs`
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/WorkerSeatProvider.cs`
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/PlemuApp.cs:73-79, 153-162, 345-353` (tip button)
- Test: `apps/device/unity/Assets/_Game/Tests/EditMode/TipEligibilityTests.cs`

**Interfaces:**
- Consumes: `IWallet`, `ISeatProvider` (Phase 1 contract); `contracts/worker-api.md`.
- Produces: nothing other streams consume.

- [ ] **Step 1: Write the tip-eligibility test**

The tip recipient must come from the **verified** manifest, never from an unverified re-read. `FileContentStore.ResolveSha256` parses the manifest without authenticating it — reading a payee from there would let a tampered file redirect money.

```csharp
[Test]
public void TipRecipient_ComesFromVerifiedManifestOnly()
{
    var entry = _catalog.Find("gb/libbet");
    Assert.AreEqual(_verifiedManifest.Entries[0].Creator.Wallet, entry.CreatorWallet);
}

[Test]
public void TipButton_HiddenForLocalEntries()
{
    Assert.IsFalse(_catalog.Find(_localId).CanTip);
}

[Test]
public void TipButton_HiddenWhenWalletDisconnected()
{
    _wallet.Disconnect();
    Assert.IsFalse(PlemuApp.ShouldShowTipButton(_catalog.Find("gb/libbet"), _wallet));
}
```

- [ ] **Step 2: Run and watch them fail**

Expected: FAIL — `CatalogEntry.CreatorWallet` is not populated. `ContentPaths.cs:51` currently maps only id/title/system/license and **drops** the creator.

- [ ] **Step 3: Widen `CatalogEntry` and populate it from the verified manifest**

One-line prerequisite for everything wallet-facing.

- [ ] **Step 4: Implement the tip button**

On the Detail screen, bound to `T` (Enter and `Z` are already Play). Hidden — not disabled — for local entries and when disconnected.

- [ ] **Step 5: Implement `SvalGuardWallet` and `WorkerSeatProvider`**

`SvalGuardWallet : IWallet` over the PlaySolana SDK. `WorkerSeatProvider : ISeatProvider` calls the Worker per `contracts/worker-api.md`, signing a challenge to prove wallet control. Handle 402 (no seat) by hiding the pack rather than erroring, and 410 (pack revoked) with an explicit message — see Review Focus #4.

- [ ] **Step 6: Swap mocks for real implementations behind a define**

```csharp
#if PLEMU_MOCK_WALLET
    IWallet wallet = new Plemu.Wallet.Mock.MockWallet();
#else
    IWallet wallet = new SvalGuardWallet();
#endif
```

Keep `PLEMU_MOCK_WALLET` on in EditMode so tests never touch a network.

- [ ] **Step 7: Verify end to end on devnet**

With Stream C's devnet deployment live, connect a funded devnet wallet, tip a creator, and confirm the signature on the explorer.

Expected: the transaction confirms and the creator's PLEMU balance increases.

- [ ] **Step 8: Commit**

```bash
git add apps/device/unity/Assets/_Game
git commit -m "feat(device): wallet integration, creator tips from verified manifest, seat-gated pack fetch"
```

### Task A6: Three more cores — SNES, Mega Drive, GBA

Independent of A3–A5. The native shim needs **zero** changes; this is ~80% data.

**Files:**
- Create: `cores/build_bsnes_{macos,android}.sh`, `cores/build_blastem_{macos,android}.sh`, `cores/build_mgba_{macos,android}.sh`
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Emulation/SystemProfile.cs:33-42`
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Emulation/RetroPad.cs`
- Modify: `apps/device/unity/stage-editor-plugins.sh`, `stage-android.sh:43-67`

- [ ] **Step 1: Write the SystemProfile test**

```csharp
[TestCase("snes", "bsnes")]
[TestCase("md",   "blastem")]
[TestCase("gba",  "mgba")]
[TestCase("gb",   "sameboy")]
[TestCase("gbc",  "sameboy")]
public void SystemProfile_MapsSystemToLegalCore(string system, string expectedCore)
{
    Assert.AreEqual(expectedCore, SystemProfile.ForSystem(system).CoreId);
}
```

- [ ] **Step 2: Run and watch it fail**

Expected: FAIL for snes/md/gba/gbc — `SystemProfile.Known` currently holds exactly one entry (`gb`), with a comment at line 41 noting gbc is deferred.

- [ ] **Step 3: Add the profiles and pad mappings**

SNES and GBA need X/Y/L/R; Mega Drive needs A/B/C plus X/Y/Z for 6-button. Add `DefaultSnesMapping`, `DefaultMdMapping`, `DefaultGbaMapping` alongside the existing GB mapping.

- [ ] **Step 4: Write the build scripts**

Follow `cores/build_sameboy_macos.sh` exactly: pin the commit, hard-fail if `git rev-parse HEAD` does not match, clone into `cores/.cache/<core>`, and gate on `file` reporting `ELF 64-bit … aarch64` for Android. Do **not** use `cores/scripts/build_core.sh` — it is a documented skeleton with two open `TODO(release)` items (`build_core.sh:18`, `:109`) and nothing calls it.

Coordinate the commit pins with Stream B task B6; both must land the same values in `cores/cores.build.json` and `cores/expected-licenses.lock`. **B owns those two files** — file the values against B, do not edit them.

- [ ] **Step 5: Generalise staging**

`stage-android.sh:43-67` hardcodes `sameboy_libretro_android.so` in two places. Replace with a loop over a core list. The `lib` prefix on the destination filename is mandatory — Gradle only extracts `lib*.so` into `nativeLibraryDir`, and Android 10+ forbids `dlopen` from app data directories.

- [ ] **Step 6: Verify each core against a real ROM**

```bash
bash apps/device/native/build_realcore.sh   # after parameterising it per core
```

Expected: `REALCORE PASS` for each. Use freely-licensed homebrew for each system; record the license evidence and file the registry entries against Stream B.

- [ ] **Step 7: Commit**

```bash
git add cores apps/device/unity
git commit -m "feat(cores): add bsnes, blastem and mgba with pad mappings and multi-core staging"
```

### Task A7: GLES hardware-render bridge — start early, longest pole

Unlocks N64, PSP and Dreamcast. Everything is currently reserved-and-inert: `rh_create_desc.hw_mode` logs a warning and falls back to software; `rh_hw_requested` always returns `0`; `rh_hw_color_texture` always returns `0`; `rh_render_event_fn` returns `NULL`; `RETRO_ENVIRONMENT_SET_HW_RENDER` is unhandled so `environment.c` returns false and forces software.

**Files:**
- Create: `apps/device/native/libretro_host/src/hw_render.c`
- Modify: `apps/device/native/libretro_host/src/environment.c:126-210` (handle `SET_HW_RENDER`)
- Modify: `apps/device/native/libretro_host/src/libretro_host.c:546-569` (implement the three stubs)
- Create: `apps/device/unity/Assets/_Game/Scripts/Runtime/Emulation/HwVideoSurface.cs`

- [ ] **Step 1: Fix the return-value ambiguity first**

`rh_hw_requested` returns `-1` for a NULL handle and `0` for software — indistinguishable without a sign check. Define `RH_HW_NONE 0`, `RH_HW_GLES3 1` and keep `RH_ERR_NULL_HANDLE` negative, then assert the distinction in `rh_smoke.c`.

- [ ] **Step 2: Handle `SET_HW_RENDER`**

Accept `RETRO_HW_CONTEXT_OPENGLES3`; reject everything else so an unsupported request still falls back cleanly rather than half-initialising. Store the core's `context_reset`/`context_destroy` callbacks and its `get_current_framebuffer`/`get_proc_address` expectations.

- [ ] **Step 3: Implement the FBO path**

Create a GLES3 FBO with a colour texture and depth buffer sized to the core's `max_width`/`max_height`. `rh_hw_color_texture` returns the texture name. `rh_render_event_fn` returns a function pointer Unity invokes via `GL.IssuePluginEvent` on the render thread — which is the only thread allowed to touch GL state.

- [ ] **Step 4: Implement `HwVideoSurface`**

Wrap the GL texture with `Texture2D.CreateExternalTexture` and blit. Keep `VideoSurface` (the software path) untouched and select between them on `rh_hw_requested`.

- [ ] **Step 5: Verify against a real hardware-rendered core**

Bring up `mupen64plus_next` on devnet-equivalent test content.

Expected: a rendered frame that changes across frames, verified with the same FNV-1a framebuffer digest plus `single_constant` check that `rh_realcore_smoke.c` already uses for the software path.

- [ ] **Step 6: Confirm the JIT question before going further**

N64, PSP and Dreamcast cores use dynamic recompilation and need W^X-exempt executable memory. Whether EchOS permits this is **open question 2 to PlaySolana** and unanswered. Until it is answered, build and test on a generic arm64 Android device and treat PSG1 support as unconfirmed. If JIT is denied, these cores fall back to interpreters at a large performance cost — that is a product decision, not an engineering one; escalate rather than absorbing it.

- [ ] **Step 7: Commit**

```bash
git add apps/device/native apps/device/unity
git commit -m "feat(native): GLES3 hardware-render bridge for dynarec cores"
```

### Task A9: Replace the device's pinned DEV keys ⛔ RELEASE BLOCKER

`PinnedKeys.cs` hardcodes `plemu-manifest-dev-a` (active) and `plemu-manifest-dev-b` (next) — the keys SPEC §6 labels "for tests only", whose private PEMs Task 0.5 step 1 distributes to all three developers. B1 step 10 generates production keys but registers them only in `tools/keys/manifest-signers.json`; that file is in Stream B's tree and **`PinnedKeys.cs` is in Stream A's**. Without this task, no step in this plan replaces the device's trust anchor, and Stream A's Definition of Done ("plays a signed title") is satisfied by a key that was passed around in onboarding.

**Files:** Modify `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/PinnedKeys.cs`, `Tests/EditMode/PinnedKeysTests.cs`

- [ ] **Step 1: Write the release gate as a test**

```csharp
[Test]
public void NoDevKeyIsPinnedInAReleaseBuild()
{
    foreach (var k in PinnedKeys.BuiltIn.Keys)
        Assert.IsFalse(k.KeyId.StartsWith("plemu-manifest-dev-"),
            $"{k.KeyId} is a test key and must never ship in a release build");
}
```

Guard it with `#if !DEVELOPMENT_BUILD` so local work keeps using the dev keys.

- [ ] **Step 2: Run it.** Expected: FAIL — both pinned keys are dev keys.
- [ ] **Step 3: Take the production values from Stream B.** B1 step 10 owns generation and custody; A owns pinning. File the request against B1 and paste the 64-hex public keys in. Keep exactly one `active`, one `next`, one `emergency` — the same invariant `docs/runbooks/key-rotation.md` imposes on the registry.
- [ ] **Step 4: Run the test.** Expected: PASS in a release build, skipped in a development build.
- [ ] **Step 5: Add it to Stream A's Definition of Done**, next to the `spec/SPEC.md` byte-identity check.
- [ ] **Step 6: Commit.**

### Task A10: Make every refusal legible

Runs alongside A3–A5, not after. Today `PlemuApp.cs:182` renders a denial as `DENIED: HashMismatch` — which tells a user nothing and a QA engineer almost nothing. This plan roughly doubles the number of ways the device can refuse (local-lane import errors, seat denials, pack revocation, stale revocation lists, wallet failures), so the gap compounds fast.

**Files:** Create `Runtime/Content/DenialCopy.cs`; modify `UI/PlemuApp.cs:178-184`; test `Tests/EditMode/DenialCopyTests.cs`

- [ ] **Step 1: Write the test that pins the contract**

Every denial reason needs both audiences. A missing case must fail the build, not fall through to a default.

```csharp
[Test]
public void EveryDenialReasonHasUserCopyAndADiagnostic()
{
    foreach (DenialReason r in Enum.GetValues(typeof(DenialReason)))
    {
        var c = DenialCopy.For(r, SampleDetail(r));
        Assert.IsNotEmpty(c.UserMessage, $"{r} has no user-facing message");
        Assert.IsNotEmpty(c.Diagnostic,  $"{r} has no diagnostic line");
        StringAssert.DoesNotContain("DenialReason", c.UserMessage, "never show an enum name to a user");
        Assert.Less(c.UserMessage.Length, 160, $"{r} user copy is too long to read on a handheld");
    }
}

[Test]
public void DiagnosticsNeverLeakKeyMaterialOrAbsolutePaths()
{
    var c = DenialCopy.For(DenialReason.HashMismatch, DetailWithFullPath());
    Assert.IsFalse(Path.IsPathRooted(FirstPathIn(c.Diagnostic)),
        "diagnostics carry paths relative to the content root, never absolute — an absolute "
        + "path leaks the developer's home directory into a bug report");
    StringAssert.DoesNotContain("-----BEGIN", c.Diagnostic);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL — `DenialCopy` does not exist.

- [ ] **Step 3: Write the copy.** Two strings per reason. The user line says what happened and what to do; the diagnostic carries the machine detail. For example, `HashMismatch` → *"This game's files don't match what was published. It's been quarantined and won't run."* / `hash mismatch: expected 3607412…, got 9af21b…; quarantined to quarantine/9af21b…`. Paths are relative to the content root, never absolute.

- [ ] **Step 4: Wire it into `PlemuApp`.** User line on screen; diagnostic to the log and to a copyable "details" toggle, so a tester can paste it into an issue without a cable.

- [ ] **Step 5: Run the suite.** Expected: all green, and adding a new `DenialReason` without copy now fails the build — which is the point.

- [ ] **Step 6: Commit.**

### Task A8: PlaySolana integration

Blocked on external inputs: a DevKit and policy sign-off. Do everything that does not need hardware first.

- [ ] **Step 1: Add the SDK**

`com.playsolana.psdk` to `Packages/manifest.json`. Confirm the exact package id and Unity 6000.3 compatibility with devrel first — the SDK's legacy TMP dependency may need adjusting.

- [ ] **Step 2: Implement `PSG1InputSource`**

The existing RetroPad mapping already assumes a SNES-class pad, which matches the PSG1 layout. This should be a thin adapter.

- [ ] **Step 3: Wire PlayDex quest hooks**

`first_game_played`, `tip_sent`, `game_published`. Coordinate the event shapes with Stream C's `plemu-quests` (C5).

- [ ] **Step 4: Fix the resolution discrepancy**

`PlemuApp.cs:255` sets `referenceResolution = new Vector2(720, 1280)` while `docs/playsolana-integration.md` specifies the PSG1 panel as **1240×1080 portrait**. Set it to 1240×1080 and re-check every UI layout against the PSG1 simulator.

- [ ] **Step 5: Validate against the simulator, then hardware**

Expected: the library, detail, and player screens all lay out correctly with no clipping at 1240×1080.

- [ ] **Step 6: Commit**

```bash
git add apps/device/unity
git commit -m "feat(device): PlaySolana SDK, PSG1 input and quest hooks, correct panel resolution"
```

---

# Stream B — Trust & Supply

**Owns:** `spec/**`, `tools/**`, `registry/**`, `.github/**`, `docs/**`, `contracts/**` (custodian after Phase 1), `cores/cores.build.json`, `cores/expected-licenses.lock`
**Never edits:** `apps/`, `programs/`, `services/`, `portal/`, `cores/build_*.sh`, and never `spec/SPEC.md`

> **`contracts/` needs a custodian.** After Phase 1 all three streams write to it — the one tree where a silent edit is most expensive. B is custodian: any change to a frozen contract needs a PR B reviews and a note to the affected stream. `docs/` and the root `README` are also B's; the Definition of Done's "ripple check" otherwise puts three people in one file. (There is no `CHANGELOG` in the repo — B creates one in B1 or the DoD drops the reference.)

**Order: B2 → B2b → B3 → B1 → B4 → B6 → B5 → B7 → portal.** (B8 and B9 moved to Phase M — see §2.11.)

This is deliberately **not** repair-first, and the reasoning matters. B2 and B3 are ⛔ blockers on three of Stream A's tasks, and B1 is a 13-step task including an R2 standup, a production key ceremony, and debugging four workflows end to end — realistically three weeks. Putting it first idles Stream A for most of a month waiting on `SPEC-LOCAL`. B2 + B2b + B3 is about two weeks, unblocks A3/A4/A5, and still lands B1 well before any paid surface exists (C's first is week 5+).

The legal urgency of B1 is real and unchanged — it is third, not tenth.

Stream B also owns **`portal/`** (see C9, relocated). It pairs with B7, which is the portal's backend, and it dissolves the B7↔C9 circular dependency that existed when they sat in different streams.

### Task B1: Make the pipeline actually work ⛔ the takedown button is currently dead

**Files:**
- Modify: `.github/workflows/emergency-revoke.yml`, `nightly-enforce.yml`, `pr-checks.yml`, `ci.yml`
- Modify: `.github/PULL_REQUEST_TEMPLATE.md`
- Modify: `tools/crates/plemu-manifest/src/main.rs:242` (`addedAt`)
- Test: `tools/crates/plemu-manifest/tests/verify_integration.rs`

- [ ] **Step 1: Write the failing test for the tierless-build bug**

Both publish paths call `build --registry .../registry` with no `--tier`. That walks for `<system>/` directories and finds only `beta/` and `main/` — neither is a valid `System` enum value, so they are skipped and the manifest ships with **zero entries**.

```rust
#[test]
fn build_without_tier_on_a_tiered_registry_fails_loudly() {
    // A tiered registry root contains beta/ and main/, not <system>/ dirs.
    // Silently emitting an empty manifest is the worst possible outcome: it
    // publishes a valid signature over nothing and sweeps every device clean.
    let err = build_manifest(tiered_registry_root(), None).unwrap_err();
    assert!(matches!(err, ManifestError::Validation(ref m) if m.contains("no entries")));
}
```

- [ ] **Step 2: Run and watch it fail**

```bash
cd tools && cargo test -p plemu-manifest build_without_tier
```

Expected: FAIL. Note `build_manifest` is not a public symbol today — `lib.rs` exports `verify_manifest` but no builder; the real code is `build::collect_entries` + `cmd_build`. Extracting a testable `build_manifest(root, tier) -> Result<Manifest>` is part of this step.

- [ ] **Step 3: Fail closed on an empty build — but exempt a configured-empty tier**

`collect_entries` returns a `Validation` error when it finds zero entries, naming the root it walked and the tier values it recognises. An empty manifest must never be signable.

One exception, or you break the nightly permanently: `registry/main/` contains only `.gitkeep`, and no task in any stream promotes an entry from beta to main. Step 4 adds `--tier main` to every build invocation, so a blanket fail-closed makes the main-tier nightly red forever. Add `--allow-empty` for a tier that is deliberately unpopulated, used **only** by the main-tier build until the first promotion, and make the workflow log a warning every time it fires so nobody forgets.

- [ ] **Step 4: Delete every phantom repo reference**

`nightly-enforce.yml` checks out `plemu/plemu-registry` and `plemu/plemu-cores`; `emergency-revoke.yml` checks out `plemu/plemu-registry` only. Those repos do not exist — everything lives in this monorepo since `9b2c256`. Replace all cross-repo checkouts with local paths, and add `--tier beta` / `--tier main` to every `build` invocation.

- [ ] **Step 5: Prove revocation works end to end**

```bash
gh workflow run emergency-revoke.yml \
  -f entry_ids=gb/libbet -f reason=dmca -f evidence_url=https://example.test/notice
gh run watch
```

Expected: the run succeeds; `registry/beta/gb/libbet/revocation.json` exists; the post-condition assertion (`jq '[.entries[] | select(.id==$id and .revoked==true)] | length'` non-zero) passes; the rebuilt manifest verifies. Then revert the sidecar and re-run the build to confirm reinstatement.

This step is the point of the whole task. Until it passes, Plemu cannot take anything down.

- [ ] **Step 6: Add a reinstatement mode**

`registry/docs/dmca-policy.md` promises reinstatement "by re-running the emergency-revoke workflow in reverse". No such mode exists. Add `-f action=revoke|reinstate`.

- [ ] **Step 7: Fix the `removals.ndjson` schema mismatch**

The policy documents `{eventType, entryId, reason, timestamp, noticeRef}`; the workflow writes `{id, reason, evidenceUrl, revokedAt}`. Make the workflow match the policy — the policy is the published commitment — and add `eventType` so reinstatement is distinguishable from removal.

- [ ] **Step 8: Fix `addedAt`**

`main.rs:242` sets `added_at = format_utc(generated_at)`, so every entry's "added" date is the build timestamp. `registry/README.md` and the curation checklist both say it comes from git history, and the checklist mandates merge commits specifically so it can be derived. Derive it from the entry directory's first commit:

```rust
let added_at = git_first_commit_date(entry_dir)?;
```

- [ ] **Step 9: Stand up R2 and the CDN**

Create the `plemu-manifests` and `plemu-artifacts` buckets, map `cdn.plemu.xyz`, and implement the publish steps that are currently `TODO(infra)` echo scaffolds in both workflows. Follow the path scheme frozen in §3 Seam A↔B. Coordinate with Stream C — C7's Worker signs URLs against the same buckets.

- [ ] **Step 10: Generate production keys**

Production signing key to KMS/CI secrets as `PLEMU_MANIFEST_KEY`; Android keystore to CI secrets. The **emergency key is generated offline under M-of-N custody and never touches a repo or a runner** — follow `docs/runbooks/emergency-publish.md` exactly. Register all three in `tools/keys/manifest-signers.json` with correct statuses, and make sure the invariant holds: exactly one `active`, one `next`, one `emergency`.

- [ ] **Step 11: Update the stale runbook — the part that does not depend on Stream A**

`docs/runbooks/emergency-publish.md` is pre-M2: single manifest, single canonical URL. Rewrite for the two-tier model plus packs.

**Do not** put the "enumerate every manifest listing this sha256" step here. It needs a **server-side** artifact→manifest index covering every pack including ones no device has fetched, and no task builds one — A4's `ManifestsListing` is a Unity method over the manifests a single device happens to have loaded, which is device-scoped, not fleet-scoped. Wiring B1 (B's first task, the plan's #3 priority) to A4 (A's fourth) means **B1 can never close**. That work moves to **B8 step 3**, where it belongs.

- [ ] **Step 12: Fix the PR template and SPEC prose drift**

`.github/PULL_REQUEST_TEMPLATE.md` uses pre-M2 paths without the tier segment and cites a nonexistent "§1.6". Separately, SPEC §1.1/§2.1/§2.3 still say `manifest.json`/`manifest.sig` while §1.0 says `manifest-<tier>.json` — and filenames disagree across docs (`manifest-beta.sig` in SPEC vs `manifest-beta.json.sig` in `docs/upload-process.md`). **Do not edit `spec/SPEC.md`.** Record the drift in `spec/SPEC-ERRATA.md` and make every other document consistent with the `.json.sig` form the code actually uses.

- [ ] **Step 13: Commit**

```bash
git add .github tools registry docs spec/SPEC-ERRATA.md
git commit -m "fix(infra): working revocation, tiered publish, R2, production keys, addedAt from git"
```

### Task B2: Write SPEC-LOCAL and SPEC-PACK ⛔ BLOCKS A3, A4

**Files:**
- Create: `spec/SPEC-LOCAL.md`, `spec/SPEC-PACK.md`
- Never modify: `spec/SPEC.md`

- [ ] **Step 1: Write `SPEC-LOCAL.md`**

Structure it like SPEC v1 so it reads as a sibling. It must state, normatively:

1. **The isolation invariant**, verbatim from §2.3 of this plan, as the document's one-line threat model.
2. That the local lane performs **no** manifest verification, freshness check, or revocation check — those concepts do not apply — but **does** re-hash on every launch for integrity.
3. That local entries are ineligible for listing, tips, mints, promotion, and quests, and that the ineligibility is a property of the entry, not of the UI.
4. The per-system size caps, byte-identical to `plemu-screen`'s `system_size_cap`, and that archives are refused outright on device.
5. The header-sniff table for system detection, referencing `check_hdr_valid`'s existing magic checks as the single source of truth.
6. That the app declares **no storage permission**; SAF per-URI grants only.
7. Explicitly, in its own section: **why this does not contradict SPEC v1.** SPEC v1 governs the signed domain. The local lane is a separate domain with no path into it. State the test that proves it (`LocalLaneIsolationTests.SignedAuthorize_NeverAcceptsALocalEntry`) by name, so the claim is falsifiable.

- [ ] **Step 2: Write `SPEC-PACK.md`**

1. A pack manifest is a **SPEC v1 `Manifest`, schema unchanged**. No new fields. No `tier` field. Reference SPEC §1.0's prohibition and say explicitly that this document does not relax it.
2. Outer signature: an ordinary `manifest.sig` from the **pinned** key registry. Explain that this is what preserves SPEC §2.3's Rust/C# equivalence waiver, which depends on signatures never coming from attacker-chosen key material.
3. Inner attestation: the `pack-<packId>.attest.json` sidecar, with the frozen bytes from §3 Seam A↔B reproduced verbatim.
3a. **Who verifies the inner attestation, and where.** State normatively: **the device verifies pinned-key signatures only.** All wallet-signature verification happens server-side, in Rust, with `verify_strict`. This is not a preference — SPEC §2.3's Rust/C# equivalence waiver holds *because* "signatures come only from pinned registry keys signing well-formed manifests, never from attacker-chosen key or signature material." Verifying an operator-chosen wallet key over operator-chosen bytes on device with Chaos.NaCl's cofactored verify makes that sentence false and puts the small-order/non-canonical divergence back in reach.

  So the sidecar is verified by **`plemu-manifest verify-pack` in CI, before the platform signs** — checking that the operator wallet equals the on-chain `Pack.operator`, that `Entries` equals the sha256 of the sorted entry ids in the accompanying manifest, and that `Seats` equals `Pack.capacity`. Failure means refuse to sign. Without this the inner attestation provides no security property at all, and the monetization grant that is the legal basis for charging for a pack goes unenforced.

3b. **The platform binding sidecar** `<packId>.binding.json` + `.sig`, per §3. State normatively that the device verifies the binding **before** the manifest and refuses any manifest whose sha256 or packId does not match. Explain why in one sentence: a SPEC-v1 manifest carries no intrinsic identity, so without this the per-pack rollback floor is chosen by whoever serves the file.
4. Anti-rollback is keyed by manifest identity, **taken from the verified binding, not the request URL**. Define the identity strings: `store-beta`, `store-main`, `pack:<packId>`, `bundled`. State that a packId must match `^[a-z0-9]+(-[a-z0-9]+)*$` and can therefore never collide with `store-beta`, `store-main` or `bundled`.
5. Freshness for packs is the **same** 72h + 7d as stores. Do not invent a second policy.
6. Revocation: identical sidecar mechanism, per pack manifest. State the cross-manifest obligation — a takedown must be applied to **every** manifest listing the sha256 (this is Review Focus #2, and A4 pins the behaviour in a test).
7. Seats are **not** part of this document. Acquisition-time entitlement is `contracts/worker-api.md`. Say so, and say why: keeping entitlement out of the wire format is what leaves SPEC §2.3 at seven steps.

- [ ] **Step 3: Self-review for contradictions**

Read both against `spec/SPEC.md` with fresh eyes. Any sentence that could be read as amending v1 must be rewritten. Grep both for `schemaVersion` and confirm neither proposes a bump.

- [ ] **Step 4: Commit and notify Stream A**

```bash
git add spec && git commit -m "spec: add SPEC-LOCAL and SPEC-PACK as siblings to the frozen v1"
```

### Task B2b: SPEC v2 — the global revocation list ⛔ BLOCKS A4's launch gate

**Files:** Create `spec/SPEC-V2-DELTA.md`, `contracts/revocation-list.md`; modify `tools/crates/plemu-schemas/src/lib.rs`, `plemu-manifest/src/verify.rs:123`, add `plemu-manifest/src/revocations.rs`

- [ ] **Step 1: Write the failing test**

```rust
#[test]
fn a_revoked_sha256_is_denied_even_when_its_manifest_says_revoked_false() {
    let list = signed_revocation_list(&["3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9"]);
    let err = gate_artifact(&clean_manifest_entry(), &list).unwrap_err();
    assert!(matches!(err, ManifestError::GloballyRevoked(_)));
}

#[test]
fn a_v1_manifest_still_verifies_under_a_v2_reader() {
    // The frozen fixtures are v1 documents and are NOT regenerated.
    let v = verify_manifest(FROZEN_V1_BYTES, FROZEN_V1_SIG, &keys, now, None).unwrap();
    assert_eq!(v.manifest.schema_version, 1);
}

#[test]
fn an_absent_or_stale_revocation_list_FAILS_CLOSED() {
    // A takedown that a device can dodge by going offline is not a takedown.
    // Grace mirrors the manifest policy: 7 days, then deny non-bundled content.
    let err = gate_artifact(&clean_manifest_entry(), &expired_list()).unwrap_err();
    assert!(matches!(err, ManifestError::RevocationListStale { .. }));
}
```

- [ ] **Step 2: Run and watch all three fail.** `cd tools && cargo test -p plemu-manifest revocation` → FAIL, no such module.
- [ ] **Step 3: Define the list format** in `contracts/revocation-list.md`: `{ schemaVersion: 1, generatedAt, expiresAt, sequence, signerKeyId, revoked: [{ sha256, reason, revokedAt }] }`, with an ordinary `manifest.sig` envelope from the **pinned** keys, its own monotonic sequence, and the same 72h + 7d freshness policy. Served at `revocations.json[.sig]`, outside every manifest path, ungated.
- [ ] **Step 4: Write `SPEC-V2-DELTA.md`** — the §2.3 step-7 clause, the §4.2 rewrite, and the `==` → `>=` relaxation, each quoted against the v1 text it changes.

  **Fold Attestation v2 into this same delta.** SPEC §1.3 defines `attestation.message` as "the exact attestation text, see §3", and §3 is normative and v1-only. Making `plemu-screen` v2-only (B3) would otherwise put a message §3 does not define into a field §3 is supposed to define — an amendment in substance, carried in-band in free text and discoverable only by string-matching the first line. Since v2 is now on the table anyway, define the v2 attestation properly in §3 of the delta. This is the one place the two amendments help each other. Do **not** edit `spec/SPEC.md`; the delta document plus a review is the amendment, and `spec/SPEC-ERRATA.md` records where v1's prose is now superseded.
- [ ] **Step 5: Implement**, relaxing exactly two lines (`verify.rs:123`, and file the C# one-liner at `ManifestVerifier.cs:76` against Stream A).
- [ ] **Step 6: Wire it into emergency-revoke.** The workflow appends the sha256 to the list and re-signs it **in the same run** as the sidecar write. That is the step that makes the published <24h commitment true.
- [ ] **Step 7: Run the tests.** Expected: all pass, and `cargo test --workspace` still green with the v1 fixtures untouched.
- [ ] **Step 8: Commit.**

### Task B3: Attestation v2 ⛔ BLOCKS A5 tip eligibility

**Files:**
- Create: `spec/ATTESTATION-V2.md`
- Modify: `tools/crates/plemu-schemas/src/attestation.rs`
- Modify: `tools/crates/plemu-screen/src/checks.rs` (`check_attestation`)
- Modify: `registry/beta/gb/libbet/*`, `registry/beta/wasm4/hello-plemu/*`
- Modify: `tools/crates/plemu-manifest/examples/dev_attest.rs`

- [ ] **Step 1: Write the failing byte-exactness test**

```rust
#[test]
fn v2_creator_message_matches_frozen_fixture() {
    let got = build_attestation_message_v2(&AttestationParamsV2 { /* fixture values */ });
    assert_eq!(got, include_str!("../../../../contracts/fixtures/attestation-v2-creator.txt"));
    assert!(!got.ends_with('\n'));
}

#[test]
fn v1_and_v2_are_not_interchangeable() {
    let v1 = build_attestation_message(&params_v1());
    let v2 = build_attestation_message_v2(&params_v2());
    assert_ne!(v1, v2, "a v1 signature must never validate as a v2 grant");
}
```

- [ ] **Step 2: Run and watch both fail**

```bash
cd tools && cargo test -p plemu-schemas v2_
```

Expected: FAIL — `build_attestation_message_v2` not found.

- [ ] **Step 3: Implement v2 and close the curator hole**

Add `build_attestation_message_v2` per the frozen bytes in §3. While you are here, fix §1.2 item 8: `check_attestation` **skips the wallet-identity check for `role: "curator"`** (`checks.rs:234`), so any keypair can mint a valid curator attestation — and the one non-CC0 registry entry uses exactly that role. The `--keys` flag is already declared but unused in `plemu-screen/src/main.rs:44-46`; wire it to a curator allowlist and require membership for curator-role attestations.

- [ ] **Step 4: Regenerate both registry fixtures**

```bash
cd tools && cargo run -p plemu-manifest --example dev_attest
```

Both entries are dev-wallet artifacts, so there is no third party to re-consent. Update `attestation.json` and the inline `attestation` block in `metadata.json` together — the two are kept in sync manually and both entry READMEs warn about it. Also fix `hello-plemu`'s stale `evidenceUrl`, which still points at the pre-M2 path without the `beta/` segment.

- [ ] **Step 5: Verify**

```bash
cargo test --workspace && bash scripts/e2e-demo.sh
```

Expected: all tests pass, `E2E DEMO PASSED`.

- [ ] **Step 6: Commit**

```bash
git add spec tools registry contracts
git commit -m "feat(trust): Attestation v2 with monetization grant; enforce curator wallet allowlist"
```

### Task B4: Pack manifest tooling

**Files:**
- Modify: `tools/crates/plemu-schemas/src/manifest.rs`, `lib.rs`, `src/attestation.rs`
- Modify: `tools/crates/plemu-manifest/src/{main,build,sign,verify}.rs`
- Modify: `tools/crates/plemu-manifest/src/error.rs`
- Modify: `tools/crates/plemu-schemas/tests/schema_regen.rs`
- Test: `tools/crates/plemu-manifest/tests/pack_integration.rs`

**Interfaces:**
- Produces: `plemu-manifest build-pack --pack <id> --registry <dir> --base-url <url> --signer-key-id <id>`, `sign-pack`, `verify-pack --pack-id <id> --min-sequence <n>`.

- [ ] **Step 1: Write the Review Focus #1 test — the timestamp divergence**

C# treats an unparseable `expiresAt` as `Expired` (soft-deny, `ManifestVerifier.cs:93-94`); Rust returns a hard `Validation` error from `parse_timestamp`. That divergence is tolerable today only because no producer emits malformed timestamps. Pack manifests multiply the number of producers, so pin the behaviour.

```rust
#[test]
fn unparseable_expires_at_is_a_hard_error_not_a_soft_deny() {
    let err = verify_manifest(&bytes_with_expires("not-a-timestamp"), &sig, &keys, now, None)
        .unwrap_err();
    assert!(matches!(err, ManifestError::Validation(ref m) if m.contains("expiresAt")));
}
```

Then file the matching C# test against Stream A: `Verify_UnparseableExpiresAt_ReportsVerificationFailure_NotExpired`. Both implementations must reject rather than one denying softly — a soft deny is indistinguishable from a legitimately stale manifest and hides a producer bug.

- [ ] **Step 2: Run and watch it fail**

Expected: **FAIL**, and not for the reason you would guess. `parse_timestamp` (`verify.rs:63-67`) formats its message as `invalid timestamp "not-a-timestamp": …` and `verify.rs:147` calls it with no added context, so the string never contains `expiresAt`. Rust already rejects correctly; only the assertion is wrong. Fix the message to name the field, then the test passes. The C# half is fixed in **A1 step 4c**, which owns that file.

- [ ] **Step 3: Add `PackAttestation` to `plemu-schemas`**

New struct plus `build_pack_attestation_message` per §3. Re-export from `lib.rs:23`. Add a third `check_or_regen` case in `tests/schema_regen.rs` emitting `tools/schemas/pack-attestation.schema.json` — the drift test fails otherwise.

```bash
PLEMU_REGEN_SCHEMAS=1 cargo test -p plemu-schemas --test schema_regen
```

- [ ] **Step 4: Generalise the verifier without forking it**

`verify_manifest` (declared at `verify.rs:90`) hardcodes `serde_json::from_slice::<Manifest>` at step 4, `verify.rs:121`. Steps 2 and 3 — envelope parse, key lookup, status gate, alg check, Ed25519 over raw bytes — are already type-agnostic and must be **reused, not copied**. Extract them into `verify_envelope_and_signature`, then have both `verify_manifest` and `verify_pack_manifest` call it. Duplicating the signature path is how the two implementations would drift.

- [ ] **Step 5: Add the three subcommands**

New `Command` variants in `main.rs:32-41` and arms in the `match` at line 141. `build-pack` reads `registry/packs/<packId>/` with the same entry layout, resolves `downloadUrls` to `{base_url}/artifacts/{sha256}`, and writes the attestation sidecar.

- [ ] **Step 6: Write the integration suite**

Mirror `tests/verify_integration.rs`: valid fixture Fresh, tampered manifest fails, wrong key fails, unknown key fails, retired key fails, freshness matrix, sign/verify roundtrip, build golden, revocation sidecar honoured, malformed revocation fails closed. Same discipline, pack shapes.

- [ ] **Step 7: Verify**

```bash
cargo fmt --all --check && cargo clippy --workspace --all-targets -- -D warnings && cargo test --workspace
```

Expected: `ok. <n> passed; 0 failed` with n well above 95.

- [ ] **Step 8: Commit**

```bash
git add tools contracts && git commit -m "feat(tools): pack manifest build/sign/verify sharing the v1 signature path"
```

### Task B5: Ship a real `plemu-screen` binary

`pr-checks.yml` installs a **stub that always exits 10** and points at `dats/latest/denylist.sqlite` and `keys/manifest-signers.json` — neither path resolves here; the real key file is `tools/keys/manifest-signers.json`. The screening gate is theatre until this lands.

- [ ] **Step 1: Add a release workflow**

Build `plemu-screen` for `x86_64-unknown-linux-gnu`, publish as a GitHub release asset, and record its SHA-256.

- [ ] **Step 2: Pin it in `pr-checks.yml`**

Replace the stub with a download of that asset, verifying the SHA-256 before executing it. Fix both wrong paths. Remove the dead `const isBeta = true;` variable.

- [ ] **Step 3: Publish a real denylist**

`nightly-enforce.yml`'s `dat-refresh` writes a literal `dats/PLACEHOLDER.dat`; the real libretro-database clone is commented out and unpinned. Pin a No-Intro/Redump DAT source, build `denylist.sqlite` nightly, and upload it to R2 so PR checks can fetch it.

- [ ] **Step 4: Verify on a real PR**

Open a PR adding a deliberately DAT-matching artifact.

Expected: exit 20, the `screen:reject` label applied, the job fails, and the sticky comment explains why. Then open a clean PR and expect exit 0 with `screen:pass`.

- [ ] **Step 5: Commit**

```bash
git add .github && git commit -m "ci: ship and pin a real plemu-screen binary; publish nightly denylist"
```

### Task B6: Pin core COMMITS ⛔ BLOCKS A6 — licence hashes deferred to Phase M

Every `commit` and `licenseSha256` in `cores/cores.build.json` and `cores/expected-licenses.lock` is `"TBD"` — while SameBoy *is* pinned to `208ba4afabffab9edde416f2dbb8ae459e34adb8` in the shell scripts, which claim consistency with the lock.

**Scope for now: commits only.** A pinned commit is a *technical* requirement — reproducible builds, and A6's scripts hard-fail without one. `licenseSha256` is a *compliance* artifact and moves to Phase M per §2.11. Leave those values `TBD`, leave `plemu-core-audit --strict` red, and do not treat the red nightly as a regression — B1 step 12 should annotate the workflow so nobody spends a morning on it.

Pin only the cores actually being built: SameBoy (done), plus bsnes, blastem and mgba for A6. The other twelve stay `TBD` until their core is scheduled.

- [ ] **Step 0: Clone each core at its pin — B does this, not A**

`licenseSha256` is the hash of a file that only exists after a clone, and A's build scripts refuse to run without a pin. B breaks the cycle by cloning directly:

```bash
git clone https://github.com/<org>/<repo> cores/.cache/<coreId>
cd cores/.cache/<coreId> && git checkout <commit> && cd -
shasum -a 256 cores/.cache/<coreId>/<licenseFile>
```

Repo URLs and `licenseFile` names are already in `cores/cores.build.json` and `cores/expected-licenses.lock` — note the per-core variation (`License.txt` for stella, `COPYING` for beetle_psx, `LICENSE.TXT` for ppsspp).

- [ ] **Step 1: Pin SameBoy — the value is already known**

Set `commit` to `208ba4afabffab9edde416f2dbb8ae459e34adb8` in both files. This also closes the drift §1.2 item 5 describes, where the shell scripts claim consistency with a lock that says `TBD`.

- [ ] **Step 2: Pin bsnes, blastem and mgba**

Stream A needs these for A6. Clone each at a chosen upstream commit (Step 0), record it, and confirm the build script's `git rev-parse HEAD` check passes against it.

- [ ] **Step 3: Verify non-strict audit is clean**

```bash
cd tools && cargo run -p plemu-core-audit -- \
  --lock ../cores/expected-licenses.lock --cores-dir ../cores \
  --build-config ../cores/cores.build.json
echo "exit=$?"
```

Expected: `exit=0`. Non-strict mode reports `TBD` as a finding but not a failure, so it verifies the parts that matter now — no forbidden core id, every id a known `CoreId`, artifact naming correct — without gating on Phase M's licence work.

Then confirm the deferral is real and visible:

```bash
cargo run -p plemu-core-audit -- --lock ../cores/expected-licenses.lock \
  --cores-dir ../cores --build-config ../cores/cores.build.json --strict; echo "exit=$?"
```

Expected: `exit=20` with `TBD` findings only. Any *other* finding code is a real problem — read the report.

- [ ] **Step 4: Commit**

```bash
git add cores && git commit -m "chore(cores): pin commits for sameboy, bsnes, blastem, mgba (licence hashes deferred to Phase M)"
```

### Task B7: Registry ↔ Supabase submission bridge

- [ ] **Step 1: Agree the submission record shape with Stream C**

C8 owns the Supabase schema. B owns what a submission must contain to become a registry PR. Write it into `registry/docs/submission-api.md`.

- [ ] **Step 2: Write the intake worker**

A submission in Supabase becomes a registry PR: artifact uploaded to R2, `metadata.json` and `attestation.json` generated, branch pushed, PR opened. `pr-checks.yml` then screens it exactly as a hand-made PR.

- [ ] **Step 3: Keep humans in the loop where the docs promise it**

`registry/docs/curation-checklist.md` requires no self-merges and a two-person rule for `custom-attested` entries and curator-role attestations. Automation opens PRs; it does not merge them.

- [ ] **Step 4: Verify**

Submit through the portal (C9) and confirm a PR appears, screened and labelled, unmerged.

- [ ] **Step 5: Commit**

```bash
git add registry services && git commit -m "feat(registry): Supabase submission intake opens screened PRs"
```

### Tasks B8 and B9 → moved to Phase M

Both were compliance work and are deferred per §2.11. They are written out in full in **Phase M** below, not deleted — the analysis behind them is the expensive part and it survives the move.

<details>
<summary>Original B8 / B9 content, retained for Phase M</summary>

### Task B8: DMCA agent and policy alignment

`registry/docs/dmca-policy.md` carries `TODO` for the designated agent and three `TBD-verify` fields. It is a §512 safe-harbor gap today and materially worse once money moves.

- [ ] **Step 1: Register the designated agent** with the U.S. Copyright Office and fill in every `TBD-verify`: agent name, `dmca@plemu.xyz`, and the mailing address.
- [ ] **Step 2: Confirm the &lt;24h and &lt;30min targets** are achievable now that B1 makes revocation work. Time an actual dry run and record the number.
- [ ] **Step 3: Document the cross-manifest obligation** — enumerate every manifest listing the sha256, rebuild and re-sign each. This is the operational half of Review Focus #2.
- [ ] **Step 4: Write the minted-collectible clause.** SPEC §4.2 keeps revoked entries in the manifest forever with `revoked: true`; an already-minted NFT has no equivalent sweep. State what happens to a collectible whose underlying work is later revoked, and coordinate with C3's mint metadata so it is achievable.
- [ ] **Step 5: Commit.**

### Task B9: Correct the external representations

- [ ] **Step 1: Revise `docs/playsolana-devrel-onepager.md`.** It currently promises *"No file browser, no storage permissions… no way for a user to side-load arbitrary files."* SAF keeps "no storage permission" **literally true** — say so precisely, and describe the local lane honestly: per-file user-granted URIs, a separate untrusted lane, no platform features, no path into the signed domain. Add the isolation test by name as evidence. **Do this before the devrel call** — open question 1 asks them to sign off on the model as written.
- [ ] **Step 2: Rewrite `registry/docs/outreach-template.md`.** It tells authors the platform is *"Free to download and play for users"* with commercial flows limited to *"a tip and NFT creator economy."* That is no longer accurate. No real author has received it yet, so there is nothing to un-say — but it must be corrected **before the first real author email**.
- [ ] **Step 3: Re-check `registry/docs/licensing-matrix.md`.** The PICO-8 BBS CC-BY-NC-SA exclusion is currently justified by the tip/NFT economy; paid seats strengthen that reasoning. Make sure the stated rationale matches the actual commercial model.
- [ ] **Step 4: Commit.**

</details>

---

# Stream C — Chain & Commerce

**Owns:** `programs/**`, `services/**`
**Never edits:** `apps/`, `spec/`, `tools/`, `registry/`, `.github/`

**Order: C1 → C2 → C7 → C3 → C4 → C5 → C6 → C8 → C10.**

C7 moves from seventh to third. It needs only C2 plus R2, takes about 1.5 weeks, and it is the last piece required for the demo the whole plan exists to produce — buy a seat, the device shows the pack, the pack plays. At position 7 that lands around week 12; at position 3 it lands around week 5. `plemu-market`, `plemu-splits` and `plemu-quests` are additive economics and none of them gates the ladder.

**`portal/` has moved to Stream B.** C was carrying four audited Anchor programs plus a Worker, a Supabase schema and a full Next.js app — roughly 18 weeks against B's 13, and a skill mix no single person does well. C is now ~13.5 weeks of chain and services work.

C's Phase 1 deliverables (mocks, frozen account layouts) unblock A and B. Devnet only.

### Task C1: The PLEMU token

**Files:** `programs/plemu/scripts/create-token.ts`, `programs/plemu/TOKENOMICS.md`

- [ ] **Step 1: Write `TOKENOMICS.md` before any code**

Fixed supply, mint authority burned at launch, a pre-allocated rewards treasury funding quests, AI-mod burns terminal. State the numbers as parameters and mark which are settled with MetaDAO at raise time. Classic SPL Token, **not** Token-2022 — the binding constraint is Futard launchpad and Meteora LP compatibility, and no extension is needed here.

- [ ] **Step 2: Create the devnet mint**

```bash
spl-token create-token --decimals 9 --url devnet
spl-token create-account <MINT> --url devnet
spl-token mint <MINT> <TOTAL_SUPPLY> --url devnet
```

- [ ] **Step 3: Attach Metaplex metadata** (name, symbol `PLEMU`, URI).

- [ ] **Step 4: Do NOT burn the mint authority on devnet** — tests need to mint. Document that burning is a mainnet launch step, gated separately.

- [ ] **Step 5: Initialise the `Config` PDA** with `authority` (the platform), `mint`, `treasury`, `price_per_seat` and `paused = false`. Every program reads it. Pricing lives here rather than compiled into the program so a 10× PLEMU move is an admin update, not a program upgrade — and `paused` is the only on-chain way to halt sales when a legal notice lands.
- [ ] **Step 6: Record the devnet mint and Config addresses** in `contracts/seat-pda.md` so A and B can reference them. Commit.

### Task C2: `plemu-seats` ⛔ its account layout is already frozen (Phase 1)

**Files:** `programs/plemu/programs/plemu-seats/src/{lib,state,errors}.rs`, `instructions/*.rs`, `tests/seats.rs`

**Interfaces:**
- Produces: `create_pack`, `purchase_capacity`, `assign_seat`, `revoke_seat`, `set_pack_revoked`. Accounts `Pack` and `Seat` exactly as frozen in §3.

- [ ] **Step 1: Write the failing LiteSVM test**

Per `.claude/rules/rust.md`, unit tests use LiteSVM or Mollusk — never `solana-test-validator`.

```rust
#[test]
fn assign_seat_fails_when_capacity_is_exhausted() {
    let mut svm = setup();
    let pack = create_pack(&mut svm, /* capacity */ 1);
    assign_seat(&mut svm, pack, member_a()).unwrap();
    let err = assign_seat(&mut svm, pack, member_b()).unwrap_err();
    assert_eq!(err, PlemuSeatsError::CapacityExhausted.into());
}

#[test]
fn purchase_capacity_uses_checked_arithmetic() {
    let mut svm = setup();
    let pack = create_pack(&mut svm, u32::MAX);
    let err = purchase_capacity(&mut svm, pack, 1).unwrap_err();
    assert_eq!(err, PlemuSeatsError::Overflow.into());
}

#[test]
fn only_the_operator_can_assign() {
    let mut svm = setup();
    let pack = create_pack(&mut svm, 10);
    let err = assign_seat_as(&mut svm, pack, member_a(), stranger()).unwrap_err();
    assert_eq!(err, PlemuSeatsError::Unauthorized.into());
}
```

- [ ] **Step 2: Run and watch them fail**

```bash
cd programs/plemu && cargo test -p plemu-seats
```

Expected: FAIL — the instructions do not exist.

- [ ] **Step 2b: Write the payment-substitution test — this is the bug that ships**

An unvalidated payment CPI is the most common Anchor exploit. A buyer passes their own worthless mint and their own "treasury" ATA and mints capacity for free.

```rust
#[test]
fn purchase_capacity_rejects_a_foreign_mint() {
    let mut svm = setup();
    let pack = create_pack(&mut svm);
    let fake = create_mint(&mut svm);          // attacker's own mint
    let err = purchase_capacity_with_mint(&mut svm, pack, fake, 100).unwrap_err();
    assert_eq!(err, PlemuSeatsError::InvalidMint.into());
}

#[test]
fn purchase_capacity_rejects_a_foreign_destination() {
    let mut svm = setup();
    let pack = create_pack(&mut svm);
    let err = purchase_capacity_to(&mut svm, pack, attacker_ata()).unwrap_err();
    assert_eq!(err, PlemuSeatsError::InvalidTreasury.into());
}

#[test]
fn purchase_capacity_honours_the_buyers_price_ceiling() {
    let mut svm = setup();
    set_price(&mut svm, 1_000);
    let err = purchase_capacity_max(&mut svm, pack, 1, /* max */ 999).unwrap_err();
    assert_eq!(err, PlemuSeatsError::PriceExceedsMaximum.into());
}

#[test]
fn the_platform_authority_can_revoke_a_pack_the_operator_will_not() {
    // A DMCA takedown cannot depend on the infringing operator's cooperation.
    let mut svm = setup();
    let pack = create_pack_as(&mut svm, uncooperative_operator());
    set_pack_revoked_as(&mut svm, pack, platform_authority()).unwrap();
    assert!(fetch_pack(&svm, pack).revoked);
}
```

- [ ] **Step 3: Implement**

Non-negotiables from `.claude/rules/anchor.md`: store the canonical bump (`ctx.bumps.pack`) and reuse it — never recalculate, it costs ~1500 CU per access. Checked arithmetic everywhere. No `unwrap()` or `expect()`. `space = Pack::DISCRIMINATOR.len() + Pack::INIT_SPACE` — never a magic `8`. One `#[error_code]` enum. Never `init_if_needed`.

Payment validation, all four parts required:

```rust
#[account(mut, address = config.mint @ PlemuSeatsError::InvalidMint)]
pub plemu_mint: Account<'info, Mint>,

#[account(mut,
    associated_token::mint = plemu_mint,
    associated_token::authority = treasury_authority @ PlemuSeatsError::InvalidTreasury)]
pub treasury_ata: Account<'info, TokenAccount>,

pub token_program: Program<'info, Token>,   // classic SPL; do NOT widen to TokenInterface

#[account(mut, has_one = operator @ PlemuSeatsError::Unauthorized)]
pub pack: Account<'info, Pack>,
pub operator: Signer<'info>,   // has_one checks the KEY; Signer checks they SIGNED. Both.
```

Three more rules:
- `create_pack` initialises `capacity = 0`. Capacity is bought, never granted — a `capacity` argument on creation makes it free.
- `require!(!config.paused, …)` and `require!(!pack.revoked, …)` on `purchase_capacity` and `assign_seat`. Solana serialises writes so there is no torn state, but a purchase can land in the slot after revocation.
- Do every bps and price multiplication in `u128` before narrowing. `amount: u64 × bps: u16` overflows `u64` above ~1.8e15 base units — 1.8M PLEMU at 9 decimals, well inside any plausible supply.

Payment is a `transfer_checked` CPI with `CpiContext::new(ctx.accounts.token_program.key(), ...)` — the program **`Pubkey`**, not an `AccountInfo`; passing `.to_account_info()` does not compile on Anchor 1.x. Read decimals from the mint account.

- [ ] **Step 4: Run the tests**

Expected: all pass.

- [ ] **Step 4b: Add a `refund` instruction now, not later**

Review Focus #4 handles the *UX* of "paid seat, revoked pack" with a 410. It does not handle the *money*, and `purchase_capacity` is one-way today. Retrofitting refunds into a deployed program that already holds real funds is a migration; adding the instruction before first deploy is an afternoon. Fund it from a reserve held by the treasury PDA and gate it on `Config.authority`. The policy, the reserve size and the EU 14-day withdrawal consent checkbox are Phase M (M6) — the instruction is not.

```rust
#[test]
fn refund_returns_capacity_payment_and_decrements_capacity() { /* ... */ }

#[test]
fn only_the_platform_authority_can_refund() { /* ... */ }
```

- [ ] **Step 5: Emit events** for `PackCreated`, `CapacityPurchased`, `SeatAssigned`, `SeatRevoked` — but treat them as **observability, not a data source**. Solana truncates transaction logs above ~10 KB and a Cloudflare Worker cannot hold a log subscription. Index from account state (`getProgramAccounts`, or a Helius webhook writing into Supabase) and let `ISeatProvider.MyPacks()` read that mirror — it is *discovery*, not entitlement, so it does not violate C8's "never read entitlement from Supabase in the Worker's hot path".

- [ ] **Step 5b: Give every error variant a `#[msg]` a human wrote**

Anchor surfaces a bare `0x1771` to clients unless the IDL carries a message, and the portal and device both read errors through Codama. One `#[error_code]` enum, a `#[msg]` on every variant, written for the person who will see it:

```rust
#[error_code]
pub enum PlemuSeatsError {
    #[msg("This pack has no seats left. The operator needs to buy more capacity.")]
    CapacityExhausted,
    #[msg("Only the pack operator can assign seats.")]
    Unauthorized,
    #[msg("The price is higher than the maximum you approved. Refresh and try again.")]
    PriceExceedsMaximum,
    #[msg("Payment must use the PLEMU mint.")]
    InvalidMint,
    // ...
}
```

Add a test asserting no variant's message is empty and none contains a bare hex code. Do the same in C3, C4 and C5.

- [ ] **Step 6: Audit and profile**

```bash
cargo fmt && cargo clippy --all-targets -- -D warnings
```

Then run `/audit-solana` and `/profile-cu` per the project's Done Checklist.

- [ ] **Step 7: Deploy to devnet and commit**

```bash
anchor deploy --provider.cluster devnet
git add programs && git commit -m "feat(programs): plemu-seats — pack capacity and seat assignment"
```

### Task C3: `plemu-market`

**Interfaces:** `create_listing`, `purchase` (fixed price), `purchase_pwyw` (minimum-enforced), `tip`, all emitting a `Receipt` the Worker can read as entitlement proof.

- [ ] **Step 1: Write the failing tests**

```rust
#[test]
fn pwyw_rejects_below_minimum() { /* ... */ }

#[test]
fn purchase_emits_a_receipt_bound_to_the_artifact_hash() {
    // The receipt must carry the sha256, not just an entry id: ids are
    // registry-scoped strings, hashes are what the device actually gates on.
}

#[test]
fn tip_routes_to_the_attested_creator_wallet_only() { /* ... */ }
```

- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement**, with the same Anchor 1.x rules as C2. `transfer_checked` for every token movement.
- [ ] **Step 4: Run the tests.** Expected: all pass.
- [ ] **Step 5: Mint collectibles via Metaplex Core** — put the **artifact sha256 in the metadata** so the collectible points at the exact bytes. Coordinate the revoked-work clause with B8.
- [ ] **Step 6: Give a purchased listing somewhere to be delivered.** The frozen Worker API has exactly one route, `/packs/<packId>/manifest`, and the §2.4 flow reads only Seat PDAs — so buying a single entry currently entitles the buyer to nothing they can fetch. Either add `GET /entries/<sha256>` gated on a `Receipt` PDA (and freeze the `Receipt` layout in `contracts/seat-pda.md`, which today does not define one), or narrow C3's scope to tips and collectibles and say so in the contract. **Decide before C7 implements the Worker.**
- [ ] **Step 7: Reconcile tips with splits.** As written, `tip_routes_to_the_attested_creator_wallet_only` makes tipping the royalty-evasion path: tip a derivative's author directly and the base author receives nothing, contradicting Appendix A.4's "derivative royalties flow through `plemu-splits`". Either route tips into the split vault when the entry has a split, or narrow A.4's claim to purchases. Pick one and write it down.
- [ ] **Step 8: Audit, profile, deploy to devnet, commit.**

### Task C4: `plemu-splits`

**Interfaces:** `create_split(recipients: Vec<(Pubkey, u16 bps)>, parent: Option<Pubkey>)`, `distribute`.

- [ ] **Step 1: Write the failing tests**

```rust
#[test]
fn bps_must_sum_to_exactly_10_000() { /* ... */ }

#[test]
fn a_derivative_inherits_its_parent_split() {
    // The lineage field is why this program exists now rather than later:
    // retrofitting parentage into already-created splits is a migration,
    // and the AI-mod work (Appendix A) depends on it.
}

#[test]
fn distribute_twice_pays_once() {
    // Use the standard accumulator, not an "epoch" flag: store total_received on the
    // split and claimed per recipient, then a payout is
    //   floor(total_received * bps / 10_000) - claimed
    // Idempotency falls out for free, and it survives partial failures, late deposits,
    // and recipients who never claim.
}
```

- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement — as a pull model, not a chain walk.** `distribute` must **not** recurse. Self-CPI dies at nesting depth 4 regardless of your own cap, and the account limit bites first: each payout needs a destination ATA, and a transaction tops out around 20–25 of them (1,232-byte message cap, 64-account lock limit) long before CU does.

Instead, treat `parent` as just another recipient whose destination is **the parent split's vault ATA**. One hop per transaction, O(1) depth, no self-CPI, no unbounded account list. Money reaches the root in `depth` cranks, which is fine for royalties.

Four specifics:
- **Cycle detection at create, not distribute.** Store `depth: u8`: root is 0, a child is `parent.depth + 1`, reject `depth > 8`. You load only the immediate parent, and a cycle becomes structurally impossible because depth strictly increases. This requires `parent` to be **immutable after creation** — never add a `set_parent` instruction, or the invariant collapses.
- **`#[derive(InitSpace)]` will not compile** over `Vec<(Pubkey, u16)>` without `#[max_len(16)]`. You need the cap anyway for the account-limit reason above.
- **Derive every destination ATA from the stored recipient pubkey and the pinned mint, then compare.** Accepting destinations from `remaining_accounts` without deriving them lets an attacker redirect a recipient's share — the most commonly botched `remaining_accounts` pattern, and one Appendix B tells auditors to attack.
- **Deterministic dust.** Compute the last recipient's share as `total − sum(others)` so the arithmetic closes exactly and the vault can actually reach zero.
- [ ] **Step 4: Run the tests.** Expected: all pass.
- [ ] **Step 5: Record the depth honesty note in `TOKENOMICS.md`.** With a per-hop cut, a deep lineage pays the root almost nothing — a 5% share four hops down leaves the original author with a rounding error. Appendix A.4 claims "the base author is paid automatically on every derivative"; state the actual decay so the claim is honest.
- [ ] **Step 6: Audit, profile, deploy, commit.**

### Task C5: `plemu-quests`

**Interfaces:** `define_quest`, `record_completion`, `claim_reward`.

This is the only program that can drain a treasury, and `record_completion` must be signed by an off-chain authority that observes PlayDex hooks. That authority is a **hot key with full drain power**. Treat it accordingly.

- [ ] **Step 1: Write the failing tests**

```rust
#[test]
fn a_completion_pda_makes_double_recording_structurally_impossible() {
    // Seed [b"completion", quest, wallet] so a second record fails at `init`,
    // rather than passing a check someone might later remove.
}

#[test]
fn one_quest_cannot_spend_another_quests_funding() {
    // "a claim cannot exceed the TREASURY balance" is the wrong invariant:
    // it lets a single compromised or buggy quest drain everything.
    // The right one is per-quest budget_remaining.
}

#[test]
fn tip_sent_cannot_be_farmed_by_tipping_yourself() {
    // Requires recipient != sender AND a minimum tip amount. Without both,
    // a 1-base-unit self-tip mints a reward worth more than it cost.
}

#[test]
fn claim_reward_is_idempotent() { /* claimed: bool on the Completion PDA */ }
```

- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement.** Rewards come from the pre-allocated treasury — no minting. Every quest carries `budget_remaining` and a per-wallet cap. Quest ids match PlaySolana's PlayDex hooks (`first_game_played`, `tip_sent`, `game_published`); coordinate the exact shapes with Stream A task A8.
- [ ] **Step 4: Run the tests. Audit, profile, deploy, commit.**

### Task C6: Codama clients

- [ ] **Step 1: Generate kit-native clients from the four IDLs**

```bash
npm i -D @codama/nodes-from-anchor @codama/renderers-js
node scripts/generate-clients.mjs
```

Do **not** use Anchor's `Program` client — it pins web3.js v1. Codama emits typed `getXInstructionAsync(...)` builders, `findXPda` helpers and `fetchX` decoders.

- [ ] **Step 2: Pin `@solana/kit` per workspace**, to the major that workspace's own dependencies peer against. Never one repo-wide digit — that is exactly what manufactures peer conflicts.
- [ ] **Step 3: Write a round-trip test** building and simulating one instruction per program against devnet.
- [ ] **Step 4: Commit.**

### Task C7: The Worker seat gate

**Files:** `services/worker/src/index.ts`, `services/worker/wrangler.toml`, `services/worker/test/gate.test.ts`

- [ ] **Step 1: Write the failing tests, including Review Focus #4**

```ts
it('402s when the wallet holds no Seat PDA', async () => { /* ... */ });

it('rejects a DUSTED seat address', async () => {
  // Revocation closes the Seat account. Its address is derivable offline by anyone
  // from (pack, their own wallet). A 1-lamport System transfer makes getAccountInfo
  // return non-null — so a bare null check grants free access to every pack and
  // silently un-revokes every revoked seat.
  await systemTransfer(deriveSeatPda(packId, strangerWallet), 1);
  const res = await gate(packId, strangerWallet);
  expect(res.status).toBe(402);
});

it('rejects an account owned by the wrong program', async () => { /* ... */ });
it('rejects a Seat whose member field does not match the caller', async () => { /* ... */ });
it('fails CLOSED with 503 when the RPC read errors', async () => { /* ... */ });

it('410s with pack-revoked rather than serving URLs into a dead pack', async () => {
  // A paid seat plus a revoked pack must NOT return 200. Otherwise the device
  // downloads a manifest in which every entry is revoked, denies all of them,
  // and the user who just paid sees an empty pack with no explanation.
  const res = await gate(revokedPackId, walletWithSeat);
  expect(res.status).toBe(410);
  expect(await res.json()).toMatchObject({ error: 'pack-revoked' });
});

it('rejects a replayed signed challenge', async () => { /* ... */ });

it('issues URLs that expire', async () => { /* ... */ });
```

- [ ] **Step 2: Run and watch them fail.**

- [ ] **Step 3: Implement per `contracts/worker-api.md`**

Sequence, and every step matters:

1. Issue server nonces from `GET /challenge`, stored in a **Durable Object or D1** — Workers KV is eventually consistent and will accept the same nonce in two colos. Verify the signature over a domain-separated message (origin + packId + nonce + expiry).
2. One **`getMultipleAccounts`** for `Seat` and `Pack` together at `confirmed` or better, so you cannot grant on a live seat plus a stale un-revoked pack read from different slots.
3. Validate the Seat properly: `owner === PLEMU_SEATS_PROGRAM_ID`, then the 8-byte discriminator, then deserialize and confirm `pack` and `member`. **Never a bare null check** — see the dusting test above.
4. Check `Pack.revoked` before issuing anything.
5. Mint signed R2 URLs against `artifacts/gated/` with a TTL of **60 seconds or less**. Revocation stops future fetches, not URLs already issued; a short TTL is what bounds that window, and proxying bytes to close it entirely would forfeit the zero-egress property R2 was chosen for.
6. On RPC error, return 503 and grant nothing. Configure two independent RPC providers — fail-closed denies paying customers during a single-provider outage, which is why the second provider is part of the design rather than an optimisation.

Write this comment at the top of the file, because it is the property a reviewer must not accidentally remove:

```ts
// The Worker gates ACCESS. It does not gate AUTHENTICITY.
// A compromised Worker can leak bytes. It cannot forge content: the device
// re-verifies the Ed25519 manifest signature and re-hashes every artifact
// before launch. Never add a code path that asks the device to trust this
// service's word about what content is valid.
```

- [ ] **Step 4: Run the tests.** Expected: all pass.
- [ ] **Step 5: Deploy to a Cloudflare staging environment** and verify against Stream A's device build.
- [ ] **Step 6: Commit.**

### Task C8: Supabase schema

- [ ] **Step 1: Write the migrations** — `submissions`, `dmca_notices`, `dmca_counter_notices`, `creator_profiles` (PII), `license_evidence`, `receipts_mirror`, `quest_progress`.
- [ ] **Step 2: Write RLS policies and test them as an anonymous role.** Every table denies by default. PII is readable only by its owner and by an admin role. Prove it with a test that attempts a cross-tenant read and expects zero rows.
- [ ] **Step 3: Document what is authoritative.** The chain is truth for receipts, seats and splits; Supabase mirrors them for query convenience. Never read entitlement from Supabase in the Worker's hot path.
- [ ] **Step 4: Commit.**

### Task C9 → reassigned to Stream B: The web portal

**Owner: Stream B** (see the ordering note at the top of each stream). Kept here because it consumes C6's Codama clients and C2/C3's instructions; B executes it after B7, whose intake it feeds.

**Files:** `portal/**` — Next.js, `@solana/kit`, `@solana/react` + `@wallet-standard/react`, Codama clients from C6.

- [ ] **Step 1: Scaffold and pin.** Pin `@solana/kit` to the major this workspace's dependencies peer against.
- [ ] **Step 2: Wallet connection** via `useWallets`/`useConnect`, then `useWalletAccountTransactionSendingSigner(account, chain)` for a signer usable as both instruction signer and fee payer.
- [ ] **Step 3: Upload flow** — file to R2, compute sha256 client-side, collect the Attestation v2 signature from the creator's wallet, write the submission to Supabase. B7's intake opens the PR.
- [ ] **Step 4: Pack management** — create a pack, buy capacity, assign and revoke seats, see utilisation.
- [ ] **Step 5: Creator dashboard** — listings, receipts, splits, tips received.
- [ ] **Step 6: Simulate before sending, always.** Set the compute-unit limit from the simulation with a 20% buffer, and surface a readable error rather than a raw program error code.
- [ ] **Step 7: Commit.**

### Task C10: Jupiter swap at the purchase boundary

- [ ] **Step 1: Write the failing test** — a user holding only SOL can complete a seat purchase in one flow.
- [ ] **Step 2: Implement as TWO transactions, not one atomic flow.** Jupiter's quote/swap API is **mainnet-only** — there is no devnet route coverage — and a route instruction plus `purchase_capacity` in one transaction will usually blow the 1,232-byte message limit and the account cap without address lookup tables, on top of Jupiter routes routinely needing 300–600k CU alone. Make the purchase leg idempotent with a `Receipt` PDA seeded `(pack, buyer, nonce)` and put a `min_out` on the swap and a `max_price` on the purchase, so a swap that lands without a purchase is recoverable rather than lost.
- [ ] **Step 3: Be honest about what can be tested pre-mainnet.** A Surfpool mainnet fork has no PLEMU pool, because PLEMU exists only on devnet — so you can test `SOL→USDC` on a fork and `purchase(PLEMU)` on devnet and never the composed flow. Test each leg separately, and record in `TOKENOMICS.md` that the joined path is unverified until mainnet.
- [ ] **Step 4: Commit.**

---

# Phase M — Mainnet Launch Preparation

**Not executed by these three streams.** Everything licensing, legal and compliance lands here, alongside the external audit (Appendix B). It is written out because the analysis is the expensive part, and deferring work should not mean losing it.

**Entry condition:** the three streams are done, the product works on devnet, and someone has decided to launch.
**Exit condition:** you can take money from a stranger, for content made by someone you have never met, and defend every part of that.

### M1: Name the entity, register the agent ⛔ BLOCKS most of Phase M

**Plemu Labs** is the name. What remains is incorporation, jurisdiction and a mailing address — and that one decision gates the DMCA registration, the terms of service, the tax analysis and any banking relationship. Do it first; six other Phase M tasks queue behind it.

Then register the DMCA designated agent with the U.S. Copyright Office and fill every `TBD-verify` in `registry/docs/dmca-policy.md`: agent name, `dmca@plemu.xyz`, mailing address. Roughly a $6 filing. One honest caveat: §512(c) protects storage *at the direction of a user*, and the curated main tier is selected and promoted by Plemu — register anyway, it is free upside, but do not plan as though the safe harbour blankets the curated and paid surfaces.

### M2: Core licences — make `--strict` green

B6 pinned commits; this pins licences. For each of the 16 cores compute `licenseSha256` from the real LICENSE file — note the per-core filename variation (`License.txt` for stella, `COPYING` for beetle_psx, `LICENSE.TXT` for ppsspp, and `TBD-verify` for microw8, whose upstream repo must be identified before it can be pinned at all) — and follow `cores/docs/adding-a-core.md`: both files change in one PR, and **a human reads the licence diff** rather than trusting the SPDX id.

```bash
cd tools && cargo run -p plemu-core-audit -- --lock ../cores/expected-licenses.lock \
  --cores-dir ../cores --build-config ../cores/cores.build.json --strict; echo "exit=$?"
```

Expected: `exit=0`. Then remove B1 step 12's "expected red" annotation so the nightly means something again.

Two findings to resolve here rather than gloss:

- **GPLv3 in a store-distributed APK.** bsnes and blastem are GPL-3.0-only, and A7 unlocks four more GPL cores. GPLv2 §3 / GPLv3 §6 require *the distributor* to provide corresponding source or a written three-year offer — and GPLv2 §3(c)'s pass-along-a-link route is available **only for noncommercial distribution**, which Plemu stops being. An upstream link is evidence, not compliance. Recommendation: **Plemu hosts corresponding source itself** at a stable R2 path recorded in the entry, and GPL cores stay free-tier only pending counsel on GPLv3 §6's installation-information provisions versus a signed-allowlist device that refuses unsigned binaries.
- **CC-BY-SA §2(a)(5)(B)** forbids applying effective technological measures that restrict recipients from exercising the licensed rights, and a seat-gated pack is arguably exactly that. Recommendation: **exclude CC-BY-SA from paid packs**, allow it free in the public store, and encode that as a machine-checked rule. BY-SA homebrew is rare enough that this removes an unsettled question from the critical path for almost nothing.

### M3: The three agreements

Counsel drafts; engineering builds the acceptance machinery — versioned text, timestamped per-wallet acceptance in Supabase, alongside the portal.

1. **User terms** — limitation of liability, warranty disclaimer, acceptable use, termination, dispute resolution, governing law, minimum age, and the collectible framing from M5.
2. **Pack operator agreement** — the most under-examined relationship in the design. An operator is a distributor in their own right. Unanswered today: by what right do they distribute (the sublicence M4 must grant); are they a DSA trader needing traceability before first sale; who takes the DMCA notice for pack content (Plemu — it controls the signer and the CDN); can Plemu revoke over the operator's objection (yes: `Config.authority`, already built in C2); may a pack contain another operator's content; and what do members get when a pack is revoked.
3. **Creator terms** — the contractual half of the attestation.

### M4: Attestation v2 — final language

B3 ships provisional wording. Counsel reviews and replaces it, and because the bytes are frozen and fixture-tested in both languages, replacing them is a fixture regeneration rather than a redesign. The structural points B3 already encodes, which must survive review: reproduction and public display (not distribution alone); **sublicensing to pack operators** (without it every pack is an unauthorised sublicence); territory and term (German UrhG §31(5) and French CPI L.131-3 both narrow unspecified grants, and many homebrew authors are German or French); an explicit derivatives opt-in; a revocation clause with a prospective-only wind-down; a `Payee` separate from the signer `Wallet`; and the **role-dependent Monetization line**, because a curator cannot grant rights they do not hold.

Pair every attestation with a **clickthrough acceptance** in the portal recording the exact text shown, the timestamp and a contact email. The signature proves key control; the clickthrough proves a human agreed. You want both.

### M5: Content policy and screening

- **Licensing matrix rewrite.** Decompose `custom-attested` into permission flags — `{redistribute, commercial, derivatives, share_alike, attribution_required, source_required}` — so derivative-inheritance rules become machine-executable instead of prose. Carry precise licences in **registry metadata only**, never in the frozen manifest enum. Add explicit rejected rows for every NC variant, not just PICO-8's CC-BY-NC-SA.
- **Ship licence text and attribution to the device.** MIT, zlib and CC-BY all require the notice to travel with the binary, and CC-BY 4.0 §3(a)(1)(B) and zlib both require marking altered versions. Nothing does this today — it affects **every** MIT/zlib/CC-BY entry, not only mods.
- **Trademark screening.** Copyright status says nothing about marks and there is no §512 safe harbour for them. Homebrew routinely uses console names and franchise characters. Screen titles, artwork and descriptions against a platform-holder denylist, flagged for curator review.
- **Adult content and age ratings.** Google Play requires an IARC rating and PlayGate likely has its own — add it to the §C.1 question list. Beta is permissionless by design and nothing screens for adult content today.
- **The minted-collectible clause.** An already-minted Metaplex Core asset belongs to its holder and cannot be burned. The only coherent design is that **the collectible is a receipt, not a licence** — proof you paid and supported the work, conveying no rights in the underlying work and no promise of continued availability. It must say so in the on-chain metadata, the terms, the purchase UI, and the DMCA policy. Decide explicitly whether Plemu keeps metadata update authority: keeping it is what makes a `revoked` flag possible, giving it up is what makes the collectible credible as a permanent artifact. You cannot have both.
- **Moral rights.** Integrity and attribution rights are inalienable in Germany, France and Japan and survive any licence. The mitigations are cheap and entirely engineering: mark derivatives as altered, never reuse a base title unqualified, always credit the base author, and give base authors a one-click **"no derivatives of my work"** flag the AI job service checks.

### M6: Money

- **Seller of record.** If Plemu takes payment and remits to operators it is merchant of record; if operators sell and Plemu takes a fee it is a marketplace — different tax, different consumer-law posture. The on-chain design currently implies *both*: `purchase_capacity` moves PLEMU operator→treasury, `plemu-market.purchase` moves buyer→splits. Decide which Plemu is.
- **VAT/GST.** The EU taxes digital services at the consumer's location with **no registration threshold for non-EU suppliers**; the UK, Norway, Australia, Japan, Canada and a growing list of US states have equivalents. One cheap pack to one EU consumer creates an obligation.
- **Token payment is barter** in most regimes — every PLEMU sale is a taxable event valued in fiat at transaction time, for both platform and creator, and Plemu holds the records. Add fiat-value-at-transaction-time and coarse buyer jurisdiction to C8's `receipts_mirror` **before it has data**.
- **Creator payouts.** Routing proceeds through `plemu-splits` makes Plemu look like a payment intermediary — 1099-K/1099-MISC in the US, W-8BEN and withholding for non-US creators.
- **Refunds.** C2 builds the `refund` instruction; Phase M writes the policy, funds the reserve, and adds the EU statutory 14-day withdrawal consent checkbox at the purchase boundary.
- **Token characterization — before the raise, not after.** A fixed-supply token sold through a launchpad, used to buy platform access and burnable for compute, needs a securities analysis in every jurisdiction it is offered in. §C.3 defers supply and allocation to MetaDAO at raise time; that is a commercial decision being made, not a legal question being answered.

### M7: Correct the external representations

- **`docs/playsolana-devrel-onepager.md`** — three claims become false: "no file browser", "no way for a user to side-load arbitrary files", and "there is no user mechanism to introduce unapproved content in the first place". SAF keeps **"no storage permission" literally true**, and that is worth keeping because it is a real, testable property (`aapt dump permissions`). Rewrite as a two-lane description: the signed lane no user bytes ever reach, and the local lane quarantined by test rather than convention. Add the paid model to the product paragraph too — asking a partner to sign off on a free platform you are about to monetize is the same error one level up.
- **`registry/docs/outreach-template.md`** — currently promises authors a platform that is "free to download and play" with commercial flows limited to "a tip and NFT creator economy". Rewrite with a pasteable grant paragraph matching the final v2 attestation word for word, with per-channel opt-ins. **Nothing has been sent yet**, so there is nothing to un-say — but it must be corrected before the first real author email.
- **`.github/PULL_REQUEST_TEMPLATE.md`** — still uses pre-M2 paths without the tier segment and cites a nonexistent "§1.6".

### M8: Privacy and residual compliance

Privacy policy and lawful basis for `creator_profiles` (PII) and `dmca_notices` (claimants' names and addresses); per-table retention; a documented data-subject-request runbook; processor agreements for Supabase and Cloudflare. The hard case, worth knowing before it arrives: **wallet address plus purchase history plus device identity is personal data once linkable, and a public chain makes it permanently so** — an erasure request against on-chain purchase history is unanswerable. Also one Cloudflare rule geo-blocking sanctioned jurisdictions, which most compliance programs expect as a gesture.

---

# Dependency graph and sequencing

```
        ┌──────────────── Phase 0: Environment & Verification (all three) ────────────────┐
        └────────────────────────────────┬───────────────────────────────────────────────┘
                                         │
        ┌────────────────── Phase 1: Contracts Day (all three, one day) ─────────────────┐
        │  1.1 contracts tree   1.2 frozen fixtures (B)                                   │
        │  1.3 wallet mocks (C) ⛔→A     1.4 frozen PDA layout (C) ⛔→B                   │
        └────────────────────────────────┬───────────────────────────────────────────────┘
                                         │
      ┌──────────────────────────────────┼──────────────────────────────────┐
      │                                  │                                  │
   STREAM A                          STREAM B                           STREAM C
      │                                  │                                  │
   A1 on-device access ★            B2 SPEC-LOCAL/PACK ⛔→A3,A4        C1 token
      │                                  │                                  │
   A2 native fixes                  B2b SPEC v2 revocation ⛔→A4       C2 seats
      │                                  │                                  │
   A3 local lane ◄───────────────── B3 attestation v2 ⛔→A5            C7 worker ★ ◄─ C2
      │                                  │                                  │
   A4 packs + rollback ◄──────────── B1 pipeline repair ★ ─────────────────┘
      │                                  │   (R2 standup feeds C7)          │
   A5 wallet + tips ◄──────────────── B4 pack tooling                    C3 market
      │                                  │                                  │
   A6 +3 cores ◄──────────────────── B6 core COMMIT pins ⛔→A6          C4 splits
      │                                  │                                  │
   A9 production keys ◄───────────── B5 screen binary                    C5 quests
      │                                  │                                  │
   A7 GLES (background, from day 1) B7 submission bridge ◄───────────── C6 clients
      │                                  │                                  │
   A8 PlaySolana (DevKit-gated)     B-portal (moved from C)             C8 supabase
                                                                          C10 jupiter

  ★ = highest value in its stream; do first
  ⛔ = hard blocker on another stream

  Phase M (licensing, legal, compliance) runs AFTER all three streams — see §2.11.

  First end-to-end moment:  ~wk 2    A1 — the APK plays a signed title on device
  Full ladder on device:    ~wk 5-6  A1+B2+B4+A4 with MockSeatProvider — no chain, no Worker
  Live seat purchase:       ~wk 5-7  C1+C2+C7 + B1's R2 standup + A5
```

**Cross-stream handoffs, in the order they bite:**

| When | From | To | What |
|---|---|---|---|
| Phase 1 | C | A | `MockWallet` + `MockSeatProvider` — without these A4 and A5 stall |
| Phase 1 | C | B | frozen `Pack`/`Seat` layout — B's Worker integration tests need it |
| Phase 1 | B | A | frozen attestation fixtures, mirrored into Unity |
| B2 done | B | A | `SPEC-LOCAL.md` and `SPEC-PACK.md` — A3 and A4 implement against them |
| B3 done | B | A | Attestation v2 — A5's tip eligibility depends on the monetization grant |
| B4 step 1 | B | A | one-line C# change: unparseable `expiresAt` must be a verification failure, not a soft `Expired` |
| A4 done | A | B | `ContentStore.ManifestsListing(sha256)` — B8's takedown runbook uses the same query server-side |
| B6 done | B | A | core commit pins — A6 cannot write build scripts without them |
| C2+C7 done | C | A | real `SvalGuardWallet`/`WorkerSeatProvider` replace the mocks |
| C6 done | C | B | typed clients, if B's intake needs to read chain state |

**Three things that unblock the most work, in order:** A1 (the APK plays nothing without it), Phase 1.3 (two of A's tasks are blocked), B1 (nothing paid is defensible while takedown is dead).

---

# Definition of done, per stream

**Stream A.** The APK installs on an arm64 device and plays a signed title, with **no `plemu-manifest-dev-*` key pinned in a release build** (A9), and every `DenialReason` renders readable copy plus a copyable diagnostic (A10). A local ROM imports via SAF and plays in a visibly separate lane, and `LocalLaneIsolationTests` proves it cannot cross. A pack appears when a seat exists and vanishes when it does not. Tips reach a creator wallet on devnet. Four systems play (gb, snes, md, gba). `PASS <n>` and `REALCORE PASS <n>` are green under ThreadSanitizer. EditMode and PlayMode are green.

**Stream B.** `gh workflow run emergency-revoke.yml` takes an entry down and the rebuilt manifest verifies. Nightly publishes a real, non-empty, signed manifest to R2. `plemu-core-audit --strict` exits 0. `plemu-screen` is a pinned release binary that rejects a DAT-matching PR and passes a clean one. `spec/SPEC.md` is byte-identical to its state at the start of this plan — verify with `git diff --stat main -- spec/SPEC.md` returning empty, **enforced as a CI job**, not a habit. (The v2 amendment lives in `SPEC-V2-DELTA.md`; the v1 file is never edited.) The DMCA agent is registered.

**Stream C.** Four programs deployed to devnet, each with green LiteSVM tests, `/audit-solana` clean and CU profiled. A `refund` instruction exists. The Worker returns 200/402/410/503 correctly against real PDAs and **rejects a dusted Seat address**. Supabase RLS denies cross-tenant reads under test.

**Stream B (portal half).** Upload → submission → screened PR, and pack creation → capacity purchase → seat assignment, both completing against devnet.

**All three, on error handling.** Every failure path has a typed reason, a user-facing sentence under 160 characters that names no enum and no hex code, and a diagnostic line carrying the machine detail with no secrets and no absolute paths. A new failure variant without copy fails the build. No `catch` swallows; no `unwrap()`/`expect()` outside tests.

**All three.** `cargo fmt --all --check` and `cargo clippy --workspace --all-targets -- -D warnings` silent. `/diff-review` run on each branch before merge (AI slop: excessive comments, redundant try/catch, verbose errors). Ripple check: README, CHANGELOG, and affected docs updated.

---

# Appendix A — AI-assisted ROM modding

**Status: scoped, not built.** No task in Streams A, B or C implements this. It is written here so the external team inherits design context rather than a blank page, and so the seams this plan *does* build are justified.

### A.1 What it is

A creator describes a change in natural language, an AI pipeline produces a modified ROM, the creator tests it on their own device within seconds, and — if they want and the licence allows — walks it up the distribution ladder. The user pays by **burning PLEMU**.

The product claim is the removal of latency between "I had an idea" and "I am playing it." Every existing romhacking workflow costs hours and specialist tooling. This costs a sentence and a burn.

### A.2 Architecture

```
  Device (local lane)                Job service                     Worker pool
  ─────────────────────             ──────────────                  ─────────────
  select base ROM ────────────────► validate base                   
  describe edit                      │  • is it in the local lane?
  quote  ◄───────────────────────────┤  • licence class of the base
  burn PLEMU  ──────────────────────►│  • price by edit class
       │                             └─ escrow the burn receipt ───► dispatch
       │                                                              │
       │                                                        Codex-backed
       │                                                        edit pipeline
       │                                                              │
  install into local lane ◄── artifact + provenance record ◄──────────┘
       │
       └─► (optional) promote: pack  →  public beta  →  main
             each rung re-runs the licence gate from scratch
```

Five components:

1. **Edit intent capture** — on-device or in the portal. Produces a typed job: base sha256, edit class, parameters, natural-language prompt.
2. **Quote and burn** — the job service prices the edit class, the user burns PLEMU, and the burn transaction signature becomes the job's authorisation token. Burns are terminal (§2.6), so the cost is real and the queue is naturally rate-limited by price rather than by an API key.
3. **Edit pipeline** — per-class tooling wrapped around the Codex subscription. This is where the actual difficulty lives; see A.3.
4. **Verification** — the produced ROM must boot. Run it headless through the existing `rh_realcore_smoke` harness: load, run 600 frames, assert the framebuffer digest changes and is not a single constant. A mod that does not boot is not delivered and the burn is refunded from the treasury.
5. **Delivery** — the artifact lands in the requester's **local lane**, with a provenance record `{parent_sha256, job_id, edit_class, burn_signature}`. It is not published anywhere. Promotion is a separate, deliberate act that re-runs every gate.

### A.3 Edit classes, by honest feasibility

Pricing should follow difficulty, and difficulty varies enormously. Do not promise these uniformly.

| Class | Approach | Feasibility |
|---|---|---|
| **Palette swap** | Locate palette tables, rewrite entries | **High.** Deterministic, verifiable, near-instant. The right launch class. |
| **Sprite reskin** | Decode tile data, regenerate tiles under the platform's colour and size limits, re-encode | **High for GB/NES**, harder for compressed formats. Output must respect 4-colour/2bpp constraints — a generic image model will not. |
| **Text and translation** | Locate the string table and pointer table, re-encode with the game's character map, repoint on length change | **Medium.** Pointer-table repointing is the hard part and is per-game. Fixed-width replacement is easy; variable-width is a real engineering problem. |
| **Difficulty rebalance** | Patch known constant tables (HP, damage, timers) | **Medium**, and only with a per-game data map. Blind constant-hunting produces garbage. |
| **Level editing** | Decode the level format, regenerate, re-encode | **Low without per-game support.** Level formats are bespoke and usually compressed. Scope this per title, never generically. |
| **Audio** | Regenerate tracker/sequence data within the sound chip's limits | **Low.** Plausible for well-documented chips, expensive elsewhere. |
| **Mashups** | Combine assets across bases | **Low**, and the worst legal exposure of the set. Both licences must permit derivatives and both obligations compose. Defer. |

Sequence: palette → sprite → text → difficulty. Everything else is research.

### A.4 Economics

- **Burn, not fee.** A burn is terminal and visible on chain, and it is the strongest justification for the token existing at all: PLEMU buys compute that is genuinely consumed.
- **Price by class, not by token count.** Users cannot reason about tokens. "A palette swap costs X PLEMU" is a product; "you used 14,000 tokens" is a bill.
- **Refund failed jobs from the treasury**, not by un-burning. Un-burning is impossible; a treasury refund is an ordinary transfer and keeps the burn accounting honest.
- **Derivative royalties flow through `plemu-splits`.** A mod distributed through a pack or the store inherits its parent's split, so the base author is paid automatically on every derivative. This is the thing no romhacking scene has ever had, and it is the strongest single argument for the whole feature.
- **The burn does not buy distribution rights.** It buys compute. Distribution is gated by licence, separately and always.

### A.5 Legal gates — the part that must not be softened

The ladder already encodes the principle: **legal rigour scales with audience size.**

| Rung | Gate |
|---|---|
| Local | None. Your device, your file, your edit. Same ground as every emulator. |
| Pack | The base licence must permit derivative works. The derivative inherits every obligation — attribution, and copyleft where the base is CC-BY-SA or GPL. |
| Public beta | Everything above, plus Attestation v2 signed by the modder, plus licence evidence for the base, plus `plemu-screen`. |
| Main | Everything above, plus curation. |

Three rules that must survive contact with a deadline:

1. **A licence that forbids derivatives means the mod never leaves the local lane.** Not "with a warning." Never.
2. **`plemu-splits` lineage is not optional for distributed derivatives.** If the parent cannot be identified, the derivative cannot be distributed.
3. **Revocation propagates down the lineage.** If a base is taken down, every distributed derivative of it is taken down in the same action. `plemu-splits`'s `parent` field is what makes this queryable — it is the reason that field is built now rather than later.

### A.6 Seams this plan builds

Three, all cheap now and expensive to retrofit:

- **`plemu-splits.parent: Option<Pubkey>`** (C4) — derivative lineage. Retrofitting parentage into already-created split accounts is a migration.
- **Local-lane provenance** (A3, `SPEC-LOCAL.md`) — the local index records where a file came from. Extending `{sourceDisplayName}` to `{parent_sha256, job_id, edit_class}` is additive.
- **Burn accounting** (C1) — the token design treats burns as terminal from day one, so the job service does not require a token redesign.

### A.7 Phased tasks for the external team

**Phase AI-0 — Feasibility spike (1 week).** Pick palette swap on one Game Boy title. End to end, manually: locate the palette, patch it, boot it through `rh_realcore_smoke`. Output is an answer, not code: how reliable is this, and what per-game knowledge did it need?

**Phase AI-1 — Job service (2–3 weeks).** Typed job schema, quote endpoint, burn verification, queue, the `rh_realcore_smoke` verification gate, treasury refund path. No AI yet — a hardcoded palette transform proves the whole pipeline.

**Phase AI-2 — Palette and sprite classes (3–4 weeks).** Real Codex-backed editing. Platform-constraint enforcement (2bpp, 4-colour) is the hard part and where quality lives.

**Phase AI-3 — Local-lane delivery and device UX (2 weeks).** Edit intent capture on device, quote, burn, install, play. This is the demo.

**Phase AI-4 — Text and translation (4+ weeks).** Pointer-table repointing. Budget for per-game work; resist the urge to generalise early.

**Phase AI-5 — Promotion path (2 weeks).** Licence gate, lineage registration in `plemu-splits`, submission with Attestation v2. Mostly wiring existing pieces; the gate logic is the new part.

### A.8 The interface contract the main build must honour

Streams A, B and C must not break these, and the external team may assume them:

1. `LocalLibrary.Import(sourceUri)` accepts any local path — not only SAF URIs — so the job service can deliver into the lane.
2. The local index record is extensible: unknown keys are preserved on read and write.
3. `plemu-splits.create_split` accepts `parent: Option<Pubkey>` from day one, and `distribute` walks the chain.
4. PLEMU burns are terminal and the token has no un-burn path.
5. Attestation v2's frozen bytes are stable. A mod's attestation is an ordinary v2 attestation; no v3 is needed for derivatives.

---

# Appendix B — External audit and polish handoff

Out of scope for these three developers. What the auditing team receives:

**Scope.** Four Anchor programs (`plemu-seats`, `plemu-market`, `plemu-splits`, `plemu-quests`) — deliberately small custom surface, with SPL Token, Metaplex Core and Jupiter composed rather than reimplemented. Plus the Cloudflare Worker seat gate, the Supabase RLS policies, and the two new trust specs.

**What they should attack hardest:**

1. **The local/signed isolation invariant.** Find any path by which a local-lane byte reaches a signed-domain feature. The test is `LocalLaneIsolationTests`; break it.
2. **Worker trust boundary.** Confirm a fully compromised Worker cannot cause the device to run unauthorised content — only to leak authorised bytes. If that property fails, the architecture is wrong, not the code.
3. **Cross-manifest revocation.** One sha256 in N manifests. Prove a takedown reaches every one, or find the manifest it misses (Review Focus #2).
4. **Anti-rollback with N manifest identities.** `SequenceStore` is new and on the critical path.
5. **Split recursion.** Depth caps, cycles, bps rounding across a lineage chain.
6. **The Rust/C# verifier equivalence** after the pack-path extraction in B4. The waiver in SPEC §2.3 depends on it.

**What is already known and does not need rediscovering:** everything in §1.2 and §1.4 of this plan. Give them this document.

**Polish backlog** (deliberately not scheduled): PSG1 hardware validation once a DevKit arrives; on-device performance profiling; error-message copy pass; accessibility; localisation.

---

# Appendix C — Open items and risks

### C.1 Blocked on PlaySolana — six unanswered questions

From `docs/playsolana-devrel-onepager.md`, all still open. Question 2 is now the most expensive.

1. Is there a written PlayVerse content policy for emulator apps, and can this architecture get a sign-off? **Ask only after B9 corrects the one-pager** — the current version describes a model the local lane changes.
2. **What is the JIT / W^X policy on EchOS?** This decides whether N64, PSP and Dreamcast run recompiled or interpreted. Task A7 is a large investment whose payoff depends entirely on this answer. **Get it before A7 goes deep.**
3. APK/AAB size limits on PlayGate — drives ship-on-device versus download-on-demand for cores.
4. Is the PSG1 panel touch-capable? Drives the local-lane import UX and any on-screen keyboard.
5. Recommended Unity version for the PlaySolana SDK on PSG1.
6. DevKit approval and a PlayGate developer account.

### C.2 Risks, with the honest version of each

| Risk | Reality |
|---|---|
| **Paid features may ship before takedown works** | Non-gating, by decision, and kept so deliberately. §C.2b below scopes what "improve later" concretely means and in what order. |
| **The local lane weakens the PlaySolana pitch** | SAF keeps "no storage permission" true, but "no file browser" becomes false. B9 rewrites the one-pager honestly. If PlaySolana refuses the local lane, the ladder loses its bottom rung and the AI-mod feature loses its testing ground — escalate rather than shipping it quietly. |
| **Single-token volatility** | Creators are paid in PLEMU. The Jupiter swap (C10) fixes buyer UX, not creator exposure. Accepted deliberately; revisit if creators complain. |
| **JIT denial on EchOS** | Would make A7's payoff much smaller. Ask question 2 before A7 goes deep. |
| **The account layout is frozen on day one** | Task 1.4 freezes `Config`, `Pack` and `Seat` before anyone has written an instruction against them. That is the plan's biggest process risk, and it is why all three carry `_reserved` tails — a later field then needs only a struct-definition change, not a realloc of every live account. If a genuinely structural change is needed after the freeze, stop and redo Contracts Day rather than working around it. |
| **Stream C is the heaviest** | Four Anchor programs, a Worker, a Supabase schema and a full Next.js portal is more than one person's share. C9 (portal) is the natural candidate to defer or reassign if the stream falls behind — it is creator-facing and no device demo needs it. |
| **Three devs, large scope** | This plan is comfortably more than three people finish quickly. The ordering is the mitigation: A1, Phase 1.3 and B1 unblock the most, and each stream produces something demonstrable before its long-pole tasks. |
| **Colosseum timing** | The submission date in the original brief (2026-10-12) is **14 days from 2026-09-28**, not the 25 the brief assumed — it was written around 2026-09-17. This plan is deadline-agnostic by decision. If the hackathon is re-targeted, the demo-critical path is A1 → Phase 1.3 → A3 → A5, plus C2 and C7 for a live seat purchase. |

### C.2b The repair-gating decision, and how to close it later

**The decision:** repair does not gate paid features. Streams ship in parallel.

**What that actually exposes.** `registry/docs/dmca-policy.md` already publishes a <24h action target and a 4h acknowledgement, and that commitment is false today with zero paid features — the workflow fails at its first real step. Payment does not create the exposure; it changes who is harmed and how it reads. Three things get worse once money moves: §512(c)(1)(B)'s direct-financial-benefit prong becomes arguable where it currently is not; rights-holders' enforcement posture flips on monetization in a way it does not for hobbyist distribution; and a revoked paid seat is a refund obligation rather than a disappointed user.

**Why non-gating is nonetheless reasonable here.** The genuinely hard parts of B1 — steps 9 and 10, standing up R2 and generating production keys — are **prerequisites for selling anything at all**. You cannot serve a paid pack from a CDN that does not exist or sign it with a key nobody has made. So the real content of "non-gating" is narrower than it sounds: it permits shipping payments with revocation **built but not yet proven end to end**, not with revocation absent. And everything user-facing here is devnet until Phase M.

**The improvement path, ordered by how cheap each step is:**

1. **Prove it once, and record the number** — hours of work, inside B1. Run `emergency-revoke.yml` green against an R2-backed manifest including a pack manifest, and log the wall-clock time. That single measurement is what turns the published <24h claim from aspiration into fact. Until it exists, the claim *is* the exposure.
2. **Ship B2b's global revocation list before the first paid pack.** Already second in Stream B. It is what makes a takedown reach packs nobody has enumerated — the exact failure mode paid private distribution creates.
3. **Keep the `refund` instruction in C2 step 4b** and do not let it slip. Retrofitting refunds into a deployed program holding real funds is a migration, not a patch.
4. **Register the DMCA agent** (Phase M, M1) — a $6 filing blocked only on naming the legal entity, which also gates the terms, the tax analysis and banking.
5. **Then declare it gating.** Once 1–4 are done the gate costs nothing to declare, and declaring it is worth more than the engineering: it converts "we knew and shipped anyway" into "we knew and closed it", which is the difference the record turns on.

**The one thing not to do:** ship a paid feature to real users while step 1 has never been run. Everything else here tolerates being late; that one does not, because it is the difference between untested and broken.

### C.3 Decisions deliberately deferred

- Exact PLEMU supply, allocation and seat price bands — settled with MetaDAO at raise time (C1 writes them as parameters).
- Token-2022 versus classic — resolved as classic for launchpad compatibility; revisit only if an extension becomes genuinely necessary.
- Mainnet deployment — an explicit gate after external audit, out of scope.
- Whether the portal eventually splits into its own repo — defer until build times justify it.
- microw8's upstream repo — still `TBD-verify` in `cores/expected-licenses.lock`; it cannot be pinned until identified (B6 step 3).
