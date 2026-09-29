# Plemu Agent Guidelines & Workspace Rules

This file defines the project-level rules, operational constraints, and context management protocols for AI agents working in the **Plemu** repository.

---

## 1. Context Window Compression Protocol

When the conversation context grows long, or when operating across multi-turn sessions:
- **Proactive Context Compression:** Compact verbose history into concise summaries of completed milestones, active tasks, and blocked items.
- **Reference Over Duplication:** Always cite code symbols and line ranges using links (e.g. `[File.cs#L10-L25]`) rather than dumping large blocks of existing code into chat.
- **Task Artifact Tracking:** Maintain state in markdown task artifacts (checklists) rather than repeating long execution histories in conversational messages.
- **Compact Tool Outputs:** Summarize terminal, test, and compiler outputs focusing only on failures, assertions, or relevant diagnostics rather than printing entire raw output logs.
- **Focus Scope:** Break complex multi-step changes into discrete subtasks, verifying each step before proceeding to the next.

---

## 2. Workstream Isolation & File Ownership

The project is structured into three parallel workstreams with exclusive file boundaries. Never edit files outside the currently assigned stream:

| Workstream | Scope / Components | Owned Paths |
|---|---|---|
| **Stream A (Runtime)** | Unity client, C11 native host shim, core build glue | `apps/device/unity/**`, `apps/device/native/**`, `cores/build_*.sh` |
| **Stream B (Trust & Supply)** | Specs, Rust tools, content registry, CI/CD, creator portal | `spec/**`, `tools/**`, `registry/**`, `.github/**`, `docs/**`, `contracts/**`, `portal/**` |
| **Stream C (Chain & Commerce)** | Solana Anchor programs, Cloudflare Workers, Supabase | `programs/**`, `services/**` |

---

## 3. Specification & Security Disciplines

- **`spec/SPEC.md` is FROZEN (v1):** Do NOT edit `spec/SPEC.md` directly. Any amendments, errata, or v2 changes must be written into `spec/SPEC-V2-DELTA.md` and `spec/SPEC-ERRATA.md`.
- **Frozen Bytes Rule:** All signed envelopes, attestation formats, and manifest files follow strict serialization: UTF-8, LF line endings, no trailing newlines, no BOM. Placeholder fields must reject carriage returns (`\r`) or line feeds (`\n`).
- **Fail-Closed, Never Fail Silent:** Any validation or authorization failure must fail closed with a typed reason, a user-facing explanation, and actionable machine diagnostics. Never swallow exceptions or catch errors without logging.
- **Key Material Safety:** Dev signing keys live outside the repository (in `../dev-secrets/` or environment variables). Never commit private keys, PEM files, or wallet seed phrases.

---

## 4. Solana & Smart Contract Rules

- Anchor 1.x with checked math (`checked_add`, `checked_sub`, `checked_mul`) only.
- No `unwrap()` or `expect()` in on-chain program logic.
- Validate every account (owner, signer, PDA canonical bumps).
- Target **Devnet throughout**. Mainnet deployment is strictly gated.
- Generate TypeScript client SDKs using **Codama** from Anchor IDLs.

---

## 5. Development Workflow & Verification

- **Branch Naming:** `<type>/<stream><task>-<description>-<DD-MM-YYYY>` (e.g. `feat/a1-android-assets-29-09-2026`).
- **Verification Gates:**
  - Rust tools: `cargo fmt --check` -> `cargo clippy --workspace --all-targets -- -D warnings` -> `cargo test --workspace`.
  - Native shim: `bash apps/device/native/build_editor.sh` (16,750 assertions).
  - Unity client: Unity EditMode (34 tests) & PlayMode (5 tests) Test Runner.
