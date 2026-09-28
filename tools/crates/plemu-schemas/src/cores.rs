//! Core allowlist / denylist logic. SPEC §1.5–1.6.
//!
//! Deny-by-default: only the 16 cores in [`crate::CoreId`] are permitted, each
//! legal for a specific set of systems. A separate explicit forbidden list
//! (plus a `mesen*` prefix rule) hard-fails any config that mentions a
//! non-commercial or relicensed core.

use crate::enums::{CoreId, System};

/// Explicitly forbidden core identifiers. SPEC §1.5.
///
/// `mesen*` is handled by a prefix check in [`is_forbidden_core_id`] rather than
/// enumerated here, because the SPEC forbids the whole family.
pub const FORBIDDEN_CORE_IDS: &[&str] = &[
    "snes9x",
    "genesis_plus_gx",
    "picodrive",
    "fbneo",
    "fbalpha",
    "duckstation",
    "swanstation",
    "mame2000",
    "mame2003",
    "mame2003_plus",
    "mame2010",
];

/// True if `id` is a forbidden core identifier (explicit list or `mesen*`).
///
/// This is a lexical check against the raw string so it catches forbidden ids
/// even where they appear as free-form text (config files, lock files), not
/// only where a [`CoreId`] enum would be parsed.
pub fn is_forbidden_core_id(id: &str) -> bool {
    FORBIDDEN_CORE_IDS.contains(&id) || id.starts_with("mesen")
}

/// The legal core(s) for a system. SPEC §1.6.
///
/// Returns a static slice so callers pay no allocation. At v1 every system maps
/// to exactly one core, but the slice return type leaves room for future
/// multi-core systems without an API break.
pub fn legal_core_ids(system: System) -> &'static [CoreId] {
    match system {
        System::Nes => &[CoreId::Fceumm],
        System::Snes => &[CoreId::Bsnes],
        System::Md => &[CoreId::Blastem],
        System::Sms => &[CoreId::Gearsystem],
        System::Gg => &[CoreId::Gearsystem],
        System::Gb => &[CoreId::Sameboy],
        System::Gbc => &[CoreId::Sameboy],
        System::Gba => &[CoreId::Mgba],
        System::A26 => &[CoreId::Stella],
        System::N64 => &[CoreId::Mupen64plusNext],
        System::Ps1 => &[CoreId::BeetlePsx],
        System::Nds => &[CoreId::Melonds],
        System::Psp => &[CoreId::Ppsspp],
        System::Dc => &[CoreId::Flycast],
        System::Tic80 => &[CoreId::Tic80],
        System::Pico8 => &[CoreId::Fake08],
        System::Wasm4 => &[CoreId::Wasm4],
        System::Microw8 => &[CoreId::Microw8],
    }
}

/// True if `core_id` is a legal choice for `system` per SPEC §1.6.
pub fn is_core_legal_for_system(system: System, core_id: CoreId) -> bool {
    legal_core_ids(system).contains(&core_id)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn forbidden_explicit_ids() {
        for id in FORBIDDEN_CORE_IDS {
            assert!(is_forbidden_core_id(id), "{id} should be forbidden");
        }
    }

    #[test]
    fn forbidden_mesen_prefix() {
        assert!(is_forbidden_core_id("mesen"));
        assert!(is_forbidden_core_id("mesen_s"));
        assert!(is_forbidden_core_id("mesen2"));
    }

    #[test]
    fn allowlisted_cores_not_forbidden() {
        assert!(!is_forbidden_core_id("sameboy"));
        assert!(!is_forbidden_core_id("fceumm"));
        assert!(!is_forbidden_core_id("mupen64plus_next"));
    }

    #[test]
    fn shared_core_systems() {
        // sms and gg both map to gearsystem
        assert!(is_core_legal_for_system(System::Sms, CoreId::Gearsystem));
        assert!(is_core_legal_for_system(System::Gg, CoreId::Gearsystem));
        // gb and gbc both map to sameboy
        assert!(is_core_legal_for_system(System::Gb, CoreId::Sameboy));
        assert!(is_core_legal_for_system(System::Gbc, CoreId::Sameboy));
    }

    #[test]
    fn wrong_core_for_system_rejected() {
        assert!(!is_core_legal_for_system(System::Nes, CoreId::Bsnes));
        assert!(!is_core_legal_for_system(System::Gb, CoreId::Mgba));
    }

    #[test]
    fn every_system_has_at_least_one_legal_core() {
        let all = [
            System::Nes,
            System::Snes,
            System::Md,
            System::Sms,
            System::Gg,
            System::Gb,
            System::Gbc,
            System::Gba,
            System::N64,
            System::Ps1,
            System::Nds,
            System::Psp,
            System::Dc,
            System::A26,
            System::Tic80,
            System::Pico8,
            System::Wasm4,
            System::Microw8,
        ];
        for s in all {
            assert!(!legal_core_ids(s).is_empty(), "{s} has no legal core");
        }
    }
}
