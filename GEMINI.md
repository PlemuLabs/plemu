# Plemu Agent Rules & Context Compression

See [AGENTS.md](file:///C:/Users/gabri/Documents/GitHub/plemu/AGENTS.md) for the full authoritative specification.

## Context Compression & Efficiency
- When conversation context grows long, compress history into compact status summaries.
- Keep file references minimal and cite by symbol/line-range (`file:///path/to/file#L1-L20`) instead of repeating long code blocks.
- Maintain persistent progress in task artifacts.
- Summarize command execution results rather than dumping verbose raw terminal logs.

## Workstream Boundary Rules
- **Stream A:** `apps/device/**`, `cores/build_*.sh`
- **Stream B:** `spec/**`, `tools/**`, `registry/**`, `.github/**`, `docs/**`, `contracts/**`, `portal/**`
- **Stream C:** `programs/**`, `services/**`
- Do not edit files belonging to other workstreams concurrently.
- `spec/SPEC.md` is strictly frozen.
