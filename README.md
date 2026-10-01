# Plemu

A multi-emulator retro gaming hub for the [PlaySolana PSG1](https://www.playsolana.com/) handheld,
built by **Plemu Labs**.

The product is not the emulator. The emulator is the runtime. The product is a **distribution ladder
with a Solana payment rail**: anyone runs what they like on their own device, shares with a closed
group by paying for seats, and — to reach everyone — submits to a public store.

The principle that holds it together: **legal rigour scales with audience size.** Private use is
free. A small group is cheap. Public reach is reviewed. Nobody is stopped from experimenting; anyone
who wants scale contributes and submits to the process.

> ### → **[`docs/superpowers/plans/platform-expansion.md`](docs/superpowers/plans/platform-expansion.md)** is the master implementation plan
>
> 43 tasks across three parallel workstreams, each with its verification command and expected
> output, plus the locked design decisions and the three frozen interface contracts.
> **Read the plan §0–§4 before touching anything.** This README is the compact version.
> For the complete product vision, pitch and Portuguese roadmap, see **[`docs/vision-pt.md`](docs/vision-pt.md)**.

---

## 1. The four rungs

| Rung | What it is | Signed by | Platform features |
|---|---|---|---|
| **Local** | Your own files, on your own device | nobody — quarantined lane | none: no listing, tips, mints or promotion |
| **Pack** | A private collection shared with N paid seats | platform (outer) + operator (inner) | seats, membership revenue set by the operator |
| **Beta store** | Public, fast publish after a licence check | platform | listing, discovery, tips, sale, collectibles |
| **Main store** | Curated, promoted up from beta | platform | all of the above, plus placement |

**Packs are the commercial core.** The platform sells *capacity*; the operator resells it as
membership at whatever price they choose. It is recurring revenue that does not depend on any single
title succeeding, and it turns curators, translation clubs and romhack communities into small
businesses with their own economics.

**A seat is an on-chain PDA**, and entitlement is checked at **manifest acquisition**, never at
launch. That one choice is why offline play survives untouched and why the launch path gains no
network dependency.

## 2. Trust model

The device only loads content whose SHA-256 appears in an **Ed25519-signed manifest**, verified
against **pinned keys before any JSON is parsed**, and re-hashed **before every launch**. Anything
else fails closed with a typed denial and no bypass.

Two domains share zero code path:

- **Signed domain** — store and pack manifests. Platform-signed with pinned keys, anti-rollback
  keyed per manifest identity, revocable.
- **Local domain** — the user's own files, imported through Android's system document picker
  (`ACTION_OPEN_DOCUMENT`) with **no storage permission declared**. Structurally isolated: a separate
  entry point (`LaunchGate.AuthorizeLocal`), a store rooted outside every signed path, a visible
  `[LOCAL — UNVERIFIED]` badge, and a test — `LocalLaneIsolationTests` — asserting nothing crosses.

Verification order, identical in Rust and C# and tested against shared byte fixtures: read raw bytes
→ parse the signature envelope only → look up the key in the pinned registry → check status and
algorithm → **verify Ed25519 over the raw bytes, and stop without parsing if it fails** → parse →
check schema version and signer id → anti-rollback on sequence → freshness → per-launch hash gate.

Takedown is reactive and reaches every rung, including inside private packs, because packs are
served by signed manifest exactly like stores.

[`docs/specs/SPEC.md`](docs/specs/SPEC.md) is the frozen v1 contract. **Never edit it** — amendments go to
`SPEC-V2-DELTA.md`, and stale prose is recorded in [`docs/specs/SPEC-ERRATA.md`](docs/specs/SPEC-ERRATA.md). CI enforces this.

## 3. Design decisions, locked

Full reasoning in [`docs/superpowers/plans/platform-expansion.md §2`](docs/superpowers/plans/platform-expansion.md). Do not relitigate these mid-implementation; if one turns out wrong,
stop and escalate rather than working around it.

| # | Decision | Why |
|---|---|---|
| 2.1 | Two new sibling specs (`SPEC-LOCAL`, `SPEC-PACK`), plus **one** deliberate v1 amendment | Keeps existing fixtures and tests valid; the amendment buys a global revocation list |
| 2.2 | Pack = a v1 `Manifest` at its own URL; platform signs outer, creator attests via **sidecar** | No new manifest field, so §1.0's "no `tier` field" holds literally |
| 2.3 | Local lane via **SAF picker, zero storage permission** | Keeps the "no blanket permission" property literally true and testable |
| 2.4 | Seats enforced **at acquisition**, not launch | Offline play survives; SPEC §2.3 stays at seven steps |
| 2.5 | **Attestation v2** adds a monetization grant | v1 grants distribution only |
| 2.6 | **One PLEMU token**, classic SPL, fixed supply, burns terminal | Launchpad and LP compatibility is the binding constraint |
| 2.7 | **Four Anchor programs**; compose SPL Token, Metaplex Core, Jupiter | Smallest custom surface = cheapest external audit |
| 2.8 | R2 for bytes (**split public/gated prefixes**), Worker for the gate, Supabase for anything sensitive | Zero egress matters at hundreds of MB per image |
| 2.9 | Stay a monorepo | Also lets us delete the phantom repo references that broke both publish workflows |
| 2.10 | **Devnet throughout**; mainnet is an explicit later gate | |
| 2.11 | **Licensing and legal deferred to Phase M** | These streams build technology and product |

## 4. Current state

### What works

The emulation vertical slice is real and reproducible — not a slide. A real libretro core (SameBoy,
MIT, pinned to `208ba4af`) runs a real homebrew game (Libbet and the Magic Floor, zlib) through the
native shim and the fail-closed `LaunchGate` in the Unity editor, and packages into a ~30 MB arm64
IL2CPP APK.

| Suite | Count | Command |
|---|---|---|
| Rust workspace | 95 tests | `cd tools && cargo test --workspace` |
| Native stub smoke | 16,750 assertions | `bash apps/device/native/build_editor.sh` |
| Native real-core smoke | ~42 assertions | `bash apps/device/native/build_realcore.sh` |
| Unity EditMode / PlayMode | 34 / 5 | Unity Test Runner |
| Offline enforcement e2e | pass | `cd tools && bash scripts/e2e-demo.sh` |

The native shim is genuinely core-agnostic — 22 exported functions, every libretro callback handled
native-side — so adding SNES, Mega Drive or GBA needs **zero shim changes**. `LaunchGate` takes its
sources as parameters, so a second lane is a new entry point rather than surgery.

### What is broken

Four things you should know before you start. [`docs/superpowers/plans/platform-expansion.md §1`](docs/superpowers/plans/platform-expansion.md) has all of them with file and line
references.

- **The APK installs but plays nothing.** `Application.streamingAssetsPath` is a `jar:` URL on
  Android, so `File.Exists` at `FileManifestSource.cs:51` always returns false and the catalog is
  always empty. It fails closed — never a bypass — but this is the line between "installs" and
  "plays". → **A1**, the highest-value task in the plan.
- **The takedown button does not work.** Two workflows check out repos that no longer exist, and
  both publish paths would emit a validly-signed manifest containing *zero entries*. → **B1**.
- **Five native defects**, one significant: the audio ring has no mutex and no atomics while the C#
  side documents it as mutex-guarded, and `count` is read-modify-written from two threads. → **A2**.
- **The device pins test keys**, and until this plan nothing replaced them. → **A9**.

### Five failure modes worth knowing about

[`platform-expansion.md`'s "Review Focus"](docs/superpowers/plans/platform-expansion.md) — things the design implies that no obvious task would otherwise test, each
pinned to the task that owns the code: unparseable timestamps diverging between Rust and C# (B4);
one artifact listed in two manifests with two revocation states (A4); a hostile file arriving through
SAF (A3); a paid seat on a revoked pack (C7); a cold device booting offline past the grace window
(A1).

## 5. How the work is phased

```
Phase 0   Environment & Verification   all three devs, together
Phase 1   Contracts Day                all three, synchronous, 3 days
          ├─ Stream A  Runtime
          ├─ Stream B  Trust & Supply
          └─ Stream C  Chain & Commerce
Phase M   Mainnet Launch Preparation   licensing, legal, compliance — deferred
```

**Phase 0** is not just installation. Every developer runs the green suite, plays Libbet in the
editor, generates their own signing keys, and **personally reproduces the empty-catalog failure on a
device** — so when they later argue about the content-access fix, they argue from having watched it
fail.

**Phase 1 — Contracts Day** is what makes three-way parallelism possible. Nothing diverges before it
lands. Five seams get frozen contracts *and working mocks*, so each stream codes against its
neighbours' mocks and never waits.

**Phase M** collects everything licensing, legal and compliance: entity and DMCA registration, core
licence hashes, the GPLv3 and CC-BY-SA questions, the three agreements, final attestation language,
content policy, tax and seller-of-record, the external-doc corrections, privacy. It is written out in
full so deferring it does not mean losing the analysis.

## 6. The three workstreams

Cut on **file ownership**, so two developers never edit the same tree. If you need a change in
another stream's tree, file it — don't make it.

### Stream A — Runtime
**Owns** `apps/device/unity/**`, `apps/device/native/**`, `cores/build_*.sh`
**Order** A1 → A2 → A3 → A4 → A5 → A6 → A9 → A8, with **A7 running in background from day one**
(the GLES bridge is the longest pole and the least coupled)

On-device content access · five native race and lifecycle fixes · the local lane with SAF import and
its isolation invariant · multi-manifest fetch, per-pack anti-rollback and the pack browser · wallet,
tips and seat-gated fetch · three more cores · legible error copy · production key pinning ·
PlaySolana SDK and PSG1 input.

### Stream B — Trust & Supply
**Owns** `spec/**`, `tools/**`, `registry/**`, `.github/**`, `docs/**`, `contracts/**` (custodian),
`portal/**`, and the two core pin files
**Order** B2 → B2b → B3 → B1 → B4 → B6 → B5 → B7 → portal

Deliberately **not** repair-first: B2 and B3 block three of Stream A's tasks and take about two
weeks, while B1 is a three-week task including an R2 standup and a key ceremony. Putting B1 first
idles Stream A for most of a month. Its legal urgency is real and unchanged — it is third, not tenth.

SPEC-LOCAL and SPEC-PACK · SPEC v2's global revocation list · Attestation v2 · making the publish and
takedown pipeline actually work · pack manifest tooling · core commit pins · a real screening binary ·
the submission bridge · the creator portal.

### Stream C — Chain & Commerce
**Owns** `programs/**`, `services/**`
**Order** C1 → C2 → **C7** → C3 → C4 → C5 → C6 → C8 → C10

C7 moved from seventh to third: it needs only C2 plus R2, and it is the last piece required for the
demo the whole plan exists to produce. At position 7 that lands around week 12; at position 3, week 5.

PLEMU token · `plemu-seats` · the Worker seat gate · `plemu-market` (listings, PWYW, tips, receipts) ·
`plemu-splits` (with **derivative lineage from day one**) · `plemu-quests` · Codama clients ·
Supabase schema and RLS · Jupiter swap at the purchase boundary.

### Milestones

```
~wk 2     A1                          the APK plays a signed title on a device
~wk 5-6   A1 + B2 + B4 + A4           the full four-rung ladder on device,
                                      using MockSeatProvider — no chain, no Worker
~wk 5-7   C1 + C2 + C7 + B1's R2 + A5 a live seat purchase end to end
```

## 7. Interface contracts

Frozen on Contracts Day, each shipping a contract document **and a working mock**.

| Seam | Owner | Carries |
|---|---|---|
| **A↔B** | B authors | pack manifest wire format, pack attestation frozen bytes, Attestation v2 bytes, the local-lane invariant, R2 path scheme |
| **A↔C** | C authors | `IWallet` and `ISeatProvider`, plus `MockWallet` and `MockSeatProvider` so A never waits on chain |
| **B↔C** | joint | Worker request/response shapes, the frozen `Config` / `Pack` / `Seat` account layouts |
| **B7↔C8/C9** | joint | the submission record |
| **A8↔C5** | joint | quest event shapes |

## 8. Repo layout

| Path | What | Status |
|---|---|---|
| `docs/specs/` | The frozen manifest / verification / attestation contracts (`SPEC.md`, `SPEC-ERRATA.md`) | Built |
| `docs/superpowers/` | SDD framework: task design specs and implementation plans | Active |
| `docs/vision-pt.md` | Portuguese product vision, marketplace economics, and AI roadmap | Current |
| `apps/device/native/` | C11 libretro host shim — 22-function ABI, all callbacks native-side | Built |
| `apps/device/unity/` | Unity device app: trust chain, launch gate, emulation runtime | Built |
| `tools/` | `plemu-*` Rust workspace: manifest build/sign/verify, DAT screening, core audit | Built |
| `registry/` | Content registry — per-title metadata and creator attestations | Built |
| `cores/` | Core allowlist, licence lock, Android build glue | Partial |
| `.github/` | CI, nightly enforcement, PR screening, R2 publish, CDN purge | Operational |
| `contracts/` | The frozen interface seams + cross-language fixtures | Planned |
| `programs/` | Anchor suite: seats, market, splits, quests (SPL Token deployed) | Active (Token Mint Live) |
| `services/` | Cloudflare Worker seat gate, Supabase moderation & DMCA RLS | Built |
| `portal/` | Next.js creator portal — upload, packs, seats | Planned |

Licensing: `apps/device/` GPL-3.0 (links GPL cores) · `tools/` and `cores/` Apache-2.0 ·
`registry/` metadata CC0 · cores keep their own.

## 9. Signing keys — read before you stage anything

This repo ships with signatures whose **private keys were never included**. That is deliberate, and
it cuts both ways:

- **You can verify the whole trust chain on arrival.** `cargo test --workspace` and the Unity
  EditMode suite check real signatures against real pinned keys — proof the slice is genuine and the
  bytes were not altered in transit, without inheriting anyone else's signing key.
- **You cannot sign anything until you generate your own.** `stage-content.sh` fails with
  `manifest signing key not found` until then.

One developer runs the rotation, commits the public half, and shares the three PEMs out of band:

```bash
bash tools/scripts/rotate-dev-keys.sh
```

It regenerates every signature and attestation in the tree, writes the private keys **outside** the
repo in `../dev-secrets/`, and re-runs the suite to prove the result is green. It refuses to run if
keys already exist, and it is re-runnable. `docs/specs/SPEC.md` is never edited, so the now-stale key values
it documents are recorded in `docs/specs/SPEC-ERRATA.md` instead. Detail: [`platform-expansion.md` **Task 0.5**](docs/superpowers/plans/platform-expansion.md).

These are test keys. Production keys live in KMS and CI secrets; the emergency key is generated
offline and never touches a repo or a runner.

## 10. Quickstart

```bash
# Verify the existing slice is green — nothing here needs a signing key
cd tools && cargo fmt --all --check && cargo clippy --workspace --all-targets -- -D warnings \
  && cargo test --workspace
cd ../apps/device/native && bash build_editor.sh      # → PASS 16750 assertions
cd ../../../tools && bash scripts/e2e-demo.sh         # → E2E DEMO PASSED

# Take ownership of the signing keys (once, by one developer)
cd .. && bash tools/scripts/rotate-dev-keys.sh
```

Then in Unity:

1. Open `apps/device/unity` in **Unity 6000.3.19f1** — exact version, with the Android module
   including OpenJDK and SDK/NDK, which is what supplies NDK r27c.
2. Menu **Plemu → Stage Native + Content** — builds the shim and core, assembles and signs the beta
   manifest, stages the game.
3. Play the `Boot` scene: **Library → Libbet → Play**.
4. APK: menu **Plemu → Build PSG1 APK**. On-hardware runs need a PSG1 DevKit.

Full environment setup, including the Unity MCP and agent tooling, is [`platform-expansion.md` **Phase 0**](docs/superpowers/plans/platform-expansion.md) — and every
developer completes it before writing code.

## 11. Working conventions

- **Three streams, disjoint trees.** See §6. File changes against the owning stream; don't reach in.
- **`docs/specs/SPEC.md` is never edited.** CI enforces it.
- **Fail closed, never fail silent.** Every refusal carries a typed reason, a user-facing sentence
  under 160 characters that names no enum and no hex code, and a diagnostic with no secrets and no
  absolute paths. A new failure variant without copy fails the build.
- **Frozen bytes stay frozen.** Every wallet-signed message is byte-fixture-tested in both Rust and
  C#: no CR or LF in placeholders, no trailing newline, consumers reconstruct and compare rather than
  parse.
- **Rust:** `cargo fmt` → `cargo clippy --all-targets -- -D warnings` → `cargo test`. No `unwrap()`
  or `expect()` outside tests, checked arithmetic everywhere, stored PDA bumps.
- **Anchor 1.x:** `CpiContext` takes the program **`Pubkey`**, not an `AccountInfo`;
  `transfer_checked` for SPL transfers; `space = T::DISCRIMINATOR.len() + T::INIT_SPACE`; one
  `#[error_code]` enum with a human-written `#[msg]` on every variant.
- **Tests:** LiteSVM or Mollusk for programs, never `solana-test-validator`. Surfpool for integration.
- **Branches:** `<type>/<stream><task>-<description>-<DD-MM-YYYY>`, e.g.
  `feat/a3-local-lane-saf-06-10-2026`.

## 12. Further reading

| Document | What it covers |
|---|---|
| [`docs/superpowers/plans/platform-expansion.md`](docs/superpowers/plans/platform-expansion.md) | The master implementation plan — everything above, in full, plus 43 tasks |
| [`docs/specs/SPEC.md`](docs/specs/SPEC.md) | The frozen v1 trust contract |
| [`docs/specs/SPEC-ERRATA.md`](docs/specs/SPEC-ERRATA.md) | Official errata and implementation extensions to SPEC v1 |
| [`docs/vision-pt.md`](docs/vision-pt.md) | Product vision, distribution ladder, marketplace economics & AI roadmap (PT-BR) |
| [`AGENTS.md`](AGENTS.md) | Canonical technical instructions and invariants for autonomous agents |
| [`GEMINI.md`](GEMINI.md) | Gemini CLI workspace entry point and guidelines |
| [`docs/ci-and-testing.md`](docs/ci-and-testing.md) | Every test entry point and how CI runs them |
| [`docs/upload-process.md`](docs/upload-process.md) | How a game gets into the registry today |
| [`docs/playsolana-integration.md`](docs/playsolana-integration.md) | What PSG1 still needs, and the six open questions |
| [`docs/sklibretro-decision.md`](docs/sklibretro-decision.md) | Why the libretro shim is ours and not SK.Libretro |
| [`cores/docs/adding-a-core.md`](cores/docs/adding-a-core.md) | Adding or bumping an emulator core |
| [`registry/README.md`](registry/README.md) | The content submission contract |
