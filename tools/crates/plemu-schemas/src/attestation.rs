//! Creator attestation v1 frozen-byte builder. SPEC §3.
//!
//! The wallet signs the UTF-8 bytes of exactly this message: `\n` (LF) line
//! endings, **no trailing newline**, no leading BOM. This module is the single
//! authority for producing those bytes so producers and screeners never drift.

use crate::enums::{AttestationRole, LicenseType, System};

/// Parameters for the frozen attestation message (SPEC §3).
#[derive(Debug, Clone, Copy)]
pub struct AttestationParams<'a> {
    pub role: AttestationRole,
    pub title: &'a str,
    pub system: System,
    /// 64 lowercase hex.
    pub sha256: &'a str,
    pub license: LicenseType,
    /// base58 wallet address.
    pub wallet: &'a str,
    /// ISO 8601 UTC timestamp, e.g. `2026-08-01T14:22:00Z`.
    pub timestamp: &'a str,
}

/// The Statement line for the creator role (SPEC §3).
const CREATOR_STATEMENT: &str = "I created this work or have the right to distribute it under the stated license; it contains no third-party copyrighted material.";

/// The Statement line for the curator role (SPEC §3 curator variant).
const CURATOR_STATEMENT: &str =
    "I verified this work is distributed under the stated license per the linked evidence.";

/// Build the exact attestation message bytes for the given parameters.
///
/// Only the Statement line differs between creator and curator roles; every
/// other line is byte-exact per SPEC §3. There is deliberately no trailing
/// newline.
pub fn build_attestation_message(params: &AttestationParams<'_>) -> String {
    let statement = match params.role {
        AttestationRole::Creator => CREATOR_STATEMENT,
        AttestationRole::Curator => CURATOR_STATEMENT,
    };

    format!(
        "Plemu Creator Attestation v1\n\
\n\
Title: {title}\n\
System: {system}\n\
SHA-256: {sha256}\n\
License: {license}\n\
Statement: {statement}\n\
Wallet: {wallet}\n\
Timestamp: {timestamp}",
        title = params.title,
        system = params.system.as_str(),
        sha256 = params.sha256,
        license = params.license.as_str(),
        statement = statement,
        wallet = params.wallet,
        timestamp = params.timestamp,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn creator_message_matches_fixture_bytes() {
        // This is the exact message embedded in the sample manifest's
        // gb/starforge-demo entry (SPEC §3 / testdata).
        let expected = "Plemu Creator Attestation v1\n\nTitle: Starforge Demo\nSystem: gb\nSHA-256: 70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd\nLicense: CC0\nStatement: I created this work or have the right to distribute it under the stated license; it contains no third-party copyrighted material.\nWallet: FGUrXnKtsJaXxZwB2NwiJFx2XqH4KmBdpypqgsskcor2\nTimestamp: 2026-07-12T00:00:00Z";

        let got = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Starforge Demo",
            system: System::Gb,
            sha256: "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd",
            license: LicenseType::Cc0,
            wallet: "FGUrXnKtsJaXxZwB2NwiJFx2XqH4KmBdpypqgsskcor2",
            timestamp: "2026-07-12T00:00:00Z",
        });

        assert_eq!(got, expected);
        assert!(!got.ends_with('\n'), "must have no trailing newline");
    }

    #[test]
    fn curator_message_swaps_only_statement() {
        let got = build_attestation_message(&AttestationParams {
            role: AttestationRole::Curator,
            title: "X",
            system: System::Nes,
            sha256: "00",
            license: LicenseType::Mit,
            wallet: "W",
            timestamp: "T",
        });
        assert!(got.contains(
            "Statement: I verified this work is distributed under the stated license per the linked evidence."
        ));
        assert!(!got.contains("I created this work"));
    }
}
