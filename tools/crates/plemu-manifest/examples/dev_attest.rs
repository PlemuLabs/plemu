//! `dev_attest` — dev-only tooling to produce a creator/curator attestation.json.
//!
//! NOT a shipped CLI. This is a cargo *example* used by developers and the
//! `scripts/e2e-demo.sh` integration proof to sign the frozen SPEC §3 attestation
//! message with a local Ed25519 PEM key. In production, creators sign in their
//! wallet; this example exists only to generate dev fixtures and the committed
//! registry dev example entry.
//!
//! Usage:
//!   cargo run -p plemu-manifest --example dev_attest -- \
//!     --metadata <metadata.json> --key-file <ed25519 PEM> \
//!     --role creator|curator --now <ISO8601> --out attestation.json
//!
//! It reads the entry's `metadata.json`, rebuilds the exact §3 message from the
//! metadata fields (title, system, sha256, license.type, wallet, timestamp),
//! signs the raw message bytes, and writes
//!   { role, message, signature(base64), signedAt, wallet }
//! where `wallet` is the base58 of the PEM's public key.

use std::path::PathBuf;

use anyhow::{bail, Context, Result};
use base64::Engine as _;
use clap::{Parser, ValueEnum};
use ed25519_dalek::pkcs8::DecodePrivateKey;
use ed25519_dalek::{Signer, SigningKey};
use plemu_schemas::{
    build_attestation_message, AttestationParams, AttestationRole, RegistryMetadata,
};

#[derive(Copy, Clone, Debug, ValueEnum)]
enum Role {
    Creator,
    Curator,
}

impl From<Role> for AttestationRole {
    fn from(r: Role) -> Self {
        match r {
            Role::Creator => AttestationRole::Creator,
            Role::Curator => AttestationRole::Curator,
        }
    }
}

#[derive(Parser, Debug)]
#[command(
    name = "dev_attest",
    about = "DEV-ONLY: sign the frozen SPEC §3 attestation message for a registry entry"
)]
struct Cli {
    /// Path to the entry's metadata.json.
    #[arg(long, value_name = "FILE")]
    metadata: PathBuf,

    /// PKCS#8 Ed25519 PEM key file (the signing wallet).
    #[arg(long, value_name = "PEM")]
    key_file: PathBuf,

    /// Attestation role.
    #[arg(long, value_enum)]
    role: Role,

    /// Signing timestamp, ISO 8601 UTC (e.g. 2026-07-12T00:00:00Z).
    #[arg(long, value_name = "ISO")]
    now: String,

    /// Output path for attestation.json.
    #[arg(long, value_name = "FILE")]
    out: PathBuf,
}

fn main() -> Result<()> {
    let cli = Cli::parse();

    let meta_bytes = std::fs::read(&cli.metadata)
        .with_context(|| format!("reading metadata {}", cli.metadata.display()))?;
    let meta: RegistryMetadata = serde_json::from_slice(&meta_bytes)
        .with_context(|| format!("parsing metadata {}", cli.metadata.display()))?;

    let pem = std::fs::read_to_string(&cli.key_file)
        .with_context(|| format!("reading key {}", cli.key_file.display()))?;
    let signing_key = SigningKey::from_pkcs8_pem(&pem)
        .map_err(|e| anyhow::anyhow!("invalid PKCS#8 Ed25519 PEM: {e}"))?;

    // Wallet = base58 of the public key. This is the identity the message binds.
    let wallet = bs58::encode(signing_key.verifying_key().as_bytes()).into_string();

    let role: AttestationRole = cli.role.into();

    // Build the frozen §3 message from the metadata fields. metadata.sha256 is the
    // digest the attestation binds (screening pins it == artifact.sha256).
    let message = build_attestation_message(&AttestationParams {
        role,
        title: &meta.title,
        system: meta.system,
        sha256: &meta.sha256,
        license: meta.license.license_type,
        wallet: &wallet,
        timestamp: &cli.now,
    });

    // Guard: a creator attestation must be signed by the declared creator wallet.
    // Curator attestations may be signed by a curator key that is not the creator.
    if matches!(role, AttestationRole::Creator) && wallet != meta.creator.wallet {
        bail!(
            "creator attestation signing wallet {wallet} != metadata.creator.wallet {}",
            meta.creator.wallet
        );
    }

    let signature = signing_key.sign(message.as_bytes());
    let signature_b64 = base64::engine::general_purpose::STANDARD.encode(signature.to_bytes());

    let attestation = serde_json::json!({
        "role": role.as_str(),
        "message": message,
        "signature": signature_b64,
        "signedAt": cli.now,
        "wallet": wallet,
    });

    let mut json = serde_json::to_string_pretty(&attestation)?;
    json.push('\n');
    std::fs::write(&cli.out, &json)
        .with_context(|| format!("writing attestation {}", cli.out.display()))?;

    eprintln!(
        "dev_attest: wrote {} ({} attestation, wallet {})",
        cli.out.display(),
        role.as_str(),
        wallet
    );
    Ok(())
}
