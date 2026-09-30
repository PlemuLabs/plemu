# Plemu — Agent Engineering Guide & Invariants

Canonical technical instructions for AI agents working in the Plemu codebase.

---

## 1. Project Identity & Architecture

- **Entity:** **Plemu Labs** (`plemu.xyz`, GitHub org: `plemu-labs`). No prior-organization or personal identifiers anywhere in tracked files.
- **Product:** A multi-emulator retro gaming platform for the **PlaySolana PSG1** handheld device featuring a 4-rung distribution ladder (Local, Pack, Beta Store, Main Store) and Solana payment rails.
  - *Local:* Unsigned, quarantined SAF picker lane (`LaunchGate.AuthorizeLocal`).
  - *Pack & Stores:* Ed25519-signed manifest lane (`LaunchGate.Authorize`).
- **Product & Economic Context:** See `docs/vision-pt.md` and `README.md`.
- **Implementation Plans:** Stored in `docs/superpowers/plans/` (Master plan: `docs/superpowers/plans/platform-expansion.md`).

---

## 2. Trust & Security Model (Normative SPEC v1)

All components (Rust tools, Unity client, Cloudflare Workers, CI) strictly implement `docs/specs/SPEC.md` and `docs/specs/SPEC-ERRATA.md`:

- **Ed25519 Signature over Raw Bytes:** Verified directly against raw payload bytes using pinned keys (`tools/keys/manifest-signers.json`) **before any JSON parsing occurs**. Signature failure halts immediately without parsing.
- **Fail-Closed Design:** Any verification failure (hash mismatch, expired grace period, sequence rollback, missing key, revocation) results in a hard typed denial.
- **Verification Pipeline (Identical in Rust & C#):**
  1. Read raw bytes.
  2. Parse detached signature envelope (`manifest-<tier>.json.sig`).
  3. Lookup key in pinned registry; check key status (`active`, `next`, `emergency`).
  4. Verify Ed25519 signature over raw bytes (terminate on failure).
  5. Parse JSON manifest; validate `schemaVersion == 1`.
  6. Enforce anti-rollback: sequence number must be $\ge$ highest accepted sequence.
  7. Check freshness: valid within 72 hours of generation, or within 7-day grace period.
  8. Enforce per-launch gate: stream SHA-256 of the artifact before loading into emulator core.
- **Forbidden Cores (Deny-by-Default):**
  - Statically blocked in `cores/forbidden.json`: `snes9x`, `genesis_plus_gx`, `picodrive`, `fbneo`, `fbalpha`, `duckstation`, `swanstation`, `mame*`, `mesen*` due to non-commercial license clauses.
  - Verified by `plemu-core-audit`.
- **Frozen Contract Rules:**
  - `schemaVersion` remains `1`.
  - Never add a `tier` field to `manifest.json` or `Entry` (tiers are transport/URL conventions).
  - Do not edit `docs/specs/SPEC.md` directly (use `docs/specs/SPEC-ERRATA.md`).

---

## 3. Tech Stack & Directory Structure

### Tech Stack Pins (Do Not Substitute)
- **Device Client:** Unity **6000.3.19f1** (IL2CPP, arm64-v8a, OpenGLES3, Portrait 1240×1080, Target SDK 34).
- **Android NDK:** **r27c (27.2.12479018)** bundled in Unity Editor (`$UNITY_EDITOR/PlaybackEngines/AndroidPlayer/NDK`).
- **Native Shim:** **C11** libretro host (`clang`, `-Wall -Wextra -Werror -O2 -fPIC -fvisibility=hidden`).
- **Rust Tooling:** Rust **stable** (Edition 2021, MSRV 1.93.1).
- **Blockchain:** Solana Devnet, **Anchor 1.x**, classic SPL Token (PLEMU), Metaplex Core.
- **Backend / Infra:** Cloudflare R2 (storage), Cloudflare Workers (seat gate & signed URLs), Supabase.

### Directory Layout
```
plemu/
├── apps/
│   └── device/
│       ├── native/          # C11 libretro host shim & smoke test suites
│       │   ├── libretro_host/
│       │   ├── smoke/       # rh_smoke.c, rh_realcore_smoke.c
│       │   └── stubcore/
│       └── unity/           # Unity 6000 client project (Assets/_Game, Packages, etc.)
├── cores/                   # Libretro core build scripts, license lock, forbidden.json
├── docs/                    # Architecture docs, PlaySolana integration, runbooks
│   ├── specs/               # Normative technical specifications (SPEC.md, SPEC-ERRATA.md)
│   ├── superpowers/         # SDD framework artifacts (specs/ for design, plans/ for tasks)
│   ├── vision-pt.md         # Portuguese product vision, pitch, marketplace & AI roadmap
│   └── runbooks/            # Operational runbooks (emergency-publish, key-rotation)
├── registry/                # Manifest entries (beta/, main/), removals.ndjson
└── tools/                   # Rust workspace & CLI tooling
    ├── Cargo.toml
    ├── crates/
    │   ├── plemu-schemas/    # Shared serde models (Manifest, Entry, Attestation)
    │   ├── plemu-manifest/   # Manifest build, sign, verify, diff CLI
    │   ├── plemu-dat/        # No-Intro DAT parsing & SQLite compilation
    │   ├── plemu-screen/     # ROM security and license screening
    │   └── plemu-core-audit/ # Static license auditor for emulator cores
    ├── keys/                 # Pinned manifest signer keys
    └── scripts/              # Dev key rotation, E2E demo scripts
```

---

## 4. Development Workflows & Essential Commands

### Rust Workspace
Run all tests, linter, and formatting from `tools/`:
```bash
cd tools

# Run all unit and integration tests across crates
cargo test --workspace

# Strict clippy check (matches CI)
cargo clippy --workspace --all-targets -- -D warnings

# Check code formatting
cargo fmt --check
```

### Native Shim & Core Smoke Tests
```bash
# Build native editor shim and execute stubcore smoke suite (~16k assertions)
bash apps/device/native/build_editor.sh

# Build and execute real-core smoke test with SameBoy
bash apps/device/native/build_realcore.sh
```

### End-to-End Offline Enforcement Demo
```bash
# Simulates manifest generation, signing, tampering detection, and verification
cd tools && bash scripts/e2e-demo.sh
```

### Core License Audit
```bash
# Audit core licenses and verify against forbidden.json and expected-licenses.lock
cd tools && cargo run -p plemu-core-audit
```

---

## 5. Engineering Invariants & Code Standards

1. **Error Handling Principles:**
   - **Fail closed, never fail silent:** A refusal must have a typed reason, a clear human-readable sentence, and technical diagnostic details.
   - **Typed errors at boundaries:** Rust uses `thiserror` (no `anyhow` in libraries, no `unwrap()`/`expect()` outside tests). C# uses typed result structs/enums.
   - **No secret leakage:** Never log or display private keys, seed phrases, or sensitive absolute paths.
2. **Solana & Anchor Standards:**
   - Anchor 1.x: `CpiContext::new(...)` takes the program `Pubkey`, not `AccountInfo`.
   - Checked arithmetic only (`checked_add`, `checked_sub`, `checked_mul`).
   - Validate every account (owner, signer, PDA bumps).
   - Space calculations must use `T::DISCRIMINATOR.len() + T::INIT_SPACE` (never hardcoded magic numbers).
   - Devnet only. Unit tests use LiteSVM or Mollusk; integration uses Surfpool.
3. **Branching & Git Hygiene:**
   - Do not stage or commit files without direct user instructions.
   - Dev signing keys live outside the repo in `../dev-secrets/`. Never commit `.pem` or secret keys.
   - Work on feature branches formatted as: `<type>/<scope>-<description>-<DD-MM-YYYY>`.
