//! Closed enumerations defined by SPEC v1 §1.4–1.6 and §2.
//!
//! Every enum here serializes to the exact lowercase string used in the SPEC.
//! Verifiers ignore unknown *fields* but these enums are closed sets: an
//! unrecognized value is a hard parse error (fail closed).

use schemars::JsonSchema;
use serde::{Deserialize, Serialize};

/// Target system / platform. SPEC §1.4 — 18 variants.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "lowercase")]
pub enum System {
    Nes,
    Snes,
    Md,
    Sms,
    Gg,
    Gb,
    Gbc,
    Gba,
    N64,
    Ps1,
    Nds,
    Psp,
    Dc,
    A26,
    Tic80,
    Pico8,
    Wasm4,
    Microw8,
}

impl System {
    /// The exact lowercase enum string used in manifests and attestations.
    pub const fn as_str(self) -> &'static str {
        match self {
            System::Nes => "nes",
            System::Snes => "snes",
            System::Md => "md",
            System::Sms => "sms",
            System::Gg => "gg",
            System::Gb => "gb",
            System::Gbc => "gbc",
            System::Gba => "gba",
            System::N64 => "n64",
            System::Ps1 => "ps1",
            System::Nds => "nds",
            System::Psp => "psp",
            System::Dc => "dc",
            System::A26 => "a26",
            System::Tic80 => "tic80",
            System::Pico8 => "pico8",
            System::Wasm4 => "wasm4",
            System::Microw8 => "microw8",
        }
    }
}

impl std::fmt::Display for System {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// Approved emulator core identifier. SPEC §1.5 — 16-entry deny-by-default allowlist.
///
/// Note the exact spellings: `mupen64plus_next`, `beetle_psx`, `fake08` — these
/// use underscores / no hyphen and require explicit serde renames.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize, JsonSchema)]
pub enum CoreId {
    #[serde(rename = "fceumm")]
    Fceumm,
    #[serde(rename = "bsnes")]
    Bsnes,
    #[serde(rename = "blastem")]
    Blastem,
    #[serde(rename = "gearsystem")]
    Gearsystem,
    #[serde(rename = "sameboy")]
    Sameboy,
    #[serde(rename = "mgba")]
    Mgba,
    #[serde(rename = "stella")]
    Stella,
    #[serde(rename = "mupen64plus_next")]
    Mupen64plusNext,
    #[serde(rename = "beetle_psx")]
    BeetlePsx,
    #[serde(rename = "melonds")]
    Melonds,
    #[serde(rename = "ppsspp")]
    Ppsspp,
    #[serde(rename = "flycast")]
    Flycast,
    #[serde(rename = "tic80")]
    Tic80,
    #[serde(rename = "fake08")]
    Fake08,
    #[serde(rename = "wasm4")]
    Wasm4,
    #[serde(rename = "microw8")]
    Microw8,
}

impl CoreId {
    /// The exact core identifier string used in manifests.
    pub const fn as_str(self) -> &'static str {
        match self {
            CoreId::Fceumm => "fceumm",
            CoreId::Bsnes => "bsnes",
            CoreId::Blastem => "blastem",
            CoreId::Gearsystem => "gearsystem",
            CoreId::Sameboy => "sameboy",
            CoreId::Mgba => "mgba",
            CoreId::Stella => "stella",
            CoreId::Mupen64plusNext => "mupen64plus_next",
            CoreId::BeetlePsx => "beetle_psx",
            CoreId::Melonds => "melonds",
            CoreId::Ppsspp => "ppsspp",
            CoreId::Flycast => "flycast",
            CoreId::Tic80 => "tic80",
            CoreId::Fake08 => "fake08",
            CoreId::Wasm4 => "wasm4",
            CoreId::Microw8 => "microw8",
        }
    }
}

impl std::fmt::Display for CoreId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// License classification. SPEC §1.3.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
pub enum LicenseType {
    #[serde(rename = "PD")]
    Pd,
    #[serde(rename = "CC0")]
    Cc0,
    #[serde(rename = "CC-BY")]
    CcBy,
    #[serde(rename = "CC-BY-SA")]
    CcBySa,
    #[serde(rename = "MIT")]
    Mit,
    #[serde(rename = "GPL")]
    Gpl,
    #[serde(rename = "custom-attested")]
    CustomAttested,
}

impl LicenseType {
    /// The exact license string used in manifests and attestations.
    pub const fn as_str(self) -> &'static str {
        match self {
            LicenseType::Pd => "PD",
            LicenseType::Cc0 => "CC0",
            LicenseType::CcBy => "CC-BY",
            LicenseType::CcBySa => "CC-BY-SA",
            LicenseType::Mit => "MIT",
            LicenseType::Gpl => "GPL",
            LicenseType::CustomAttested => "custom-attested",
        }
    }
}

impl std::fmt::Display for LicenseType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// Who signed the attestation. SPEC §1.3 / §3.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "lowercase")]
pub enum AttestationRole {
    Creator,
    Curator,
}

impl AttestationRole {
    pub const fn as_str(self) -> &'static str {
        match self {
            AttestationRole::Creator => "creator",
            AttestationRole::Curator => "curator",
        }
    }
}

impl std::fmt::Display for AttestationRole {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// Signer-key lifecycle status. SPEC §2.2.
///
/// A verifier accepts signatures from `active | next | emergency` only;
/// `retired` is a verify-only rejection.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "lowercase")]
pub enum KeyStatus {
    Active,
    Next,
    Emergency,
    Retired,
}

impl KeyStatus {
    pub const fn as_str(self) -> &'static str {
        match self {
            KeyStatus::Active => "active",
            KeyStatus::Next => "next",
            KeyStatus::Emergency => "emergency",
            KeyStatus::Retired => "retired",
        }
    }

    /// True when a verifier may accept a signature made by a key in this state.
    pub const fn is_accepted_for_verification(self) -> bool {
        matches!(
            self,
            KeyStatus::Active | KeyStatus::Next | KeyStatus::Emergency
        )
    }
}

impl std::fmt::Display for KeyStatus {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}

/// Reason an entry was revoked. SPEC §1.3.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "kebab-case")]
pub enum RevokedReason {
    Dmca,
    LicenseDispute,
    Malware,
    CreatorRequest,
}

impl RevokedReason {
    pub const fn as_str(self) -> &'static str {
        match self {
            RevokedReason::Dmca => "dmca",
            RevokedReason::LicenseDispute => "license-dispute",
            RevokedReason::Malware => "malware",
            RevokedReason::CreatorRequest => "creator-request",
        }
    }
}

impl std::fmt::Display for RevokedReason {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.as_str())
    }
}
