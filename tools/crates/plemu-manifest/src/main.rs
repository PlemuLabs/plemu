//! `plemu-manifest` CLI. SPEC §5.
//!
//! Subcommands: `build`, `sign`, `verify`, `diff`. All errors go to stderr,
//! machine output to `--out` or stdout, and no input can cause a panic. `verify`
//! exits 0 (valid) or 1 (invalid); other commands exit 0 (ok) or 1 (error).

use std::path::{Path, PathBuf};
use std::process::ExitCode;

use chrono::Utc;
use clap::{Parser, Subcommand};
use plemu_manifest::build::{assemble_manifest, collect_entries, format_utc, next_sequence};
use plemu_manifest::crypto;
use plemu_manifest::diff::diff_manifests;
use plemu_manifest::error::{ManifestError, Result};
use plemu_manifest::sign::sign_manifest;
use plemu_manifest::verify::{parse_timestamp, verify_manifest};
use plemu_schemas::{KeyRegistry, Manifest};

#[derive(Parser)]
#[command(
    name = "plemu-manifest",
    about = "Build, sign, verify, and diff Plemu content-trust manifests (SPEC v1).",
    version
)]
struct Cli {
    #[command(subcommand)]
    command: Command,
}

#[derive(Subcommand)]
enum Command {
    /// Compile a registry tree into a manifest.
    Build(BuildArgs),
    /// Sign a manifest, producing a detached signature envelope.
    Sign(SignArgs),
    /// Verify a manifest against a detached signature and key registry.
    Verify(VerifyArgs),
    /// Diff two manifests into a changelog.
    Diff(DiffArgs),
}

/// Convenience tier selector for the two-manifest model (SPEC §4 M2).
///
/// When `--tier beta|main` is provided it selects `<registry>/<tier>` as the
/// effective registry root and defaults `--out` to `manifest-<tier>.json`.
/// Explicit `--registry` and `--out` override it. Unknown tier values are
/// rejected loudly. The manifest schema itself is unchanged — tier is build-
/// input convention only, not a wire-format field (SPEC §1 stays frozen).
const KNOWN_TIERS: &[&str] = &["beta", "main"];

#[derive(Parser)]
struct BuildArgs {
    /// Registry root containing `<system>/<slug>/metadata.json`.
    /// When `--tier` is set, defaults to `<registry-root>/<tier>`.
    #[arg(long)]
    registry: Option<PathBuf>,
    /// Convenience tier: `beta` or `main`. Selects `<registry>/<tier>` as the
    /// registry root and sets a default output name of `manifest-<tier>.json`.
    /// Explicit `--registry` and `--out` override the derived values.
    #[arg(long, value_name = "TIER")]
    tier: Option<String>,
    /// Base URL for deriving `downloadUrls` (`<base>/artifacts/<sha256>`).
    #[arg(long)]
    base_url: String,
    /// Previous manifest, for sequence continuation.
    #[arg(long)]
    previous: Option<PathBuf>,
    /// Reference timestamp (ISO 8601 UTC); defaults to system clock.
    #[arg(long)]
    now: Option<String>,
    /// Signer key id to stamp into the manifest (required: a pipeline that
    /// forgets it must fail loudly, not emit a manifest naming the wrong signer).
    #[arg(long)]
    signer_key_id: String,
    /// Output path for the manifest JSON.
    /// When `--tier` is set and `--out` is omitted, defaults to `manifest-<tier>.json`.
    #[arg(long)]
    out: Option<PathBuf>,
}

#[derive(Parser)]
struct SignArgs {
    /// Manifest file to sign (raw bytes are signed).
    #[arg(long)]
    manifest: PathBuf,
    /// PKCS#8 Ed25519 PEM key file.
    #[arg(long, conflicts_with = "key_env")]
    key_file: Option<PathBuf>,
    /// Environment variable holding a PKCS#8 Ed25519 PEM.
    #[arg(long)]
    key_env: Option<String>,
    /// Key id for the envelope.
    #[arg(long)]
    key_id: String,
    /// Optional key registry for post-sign self-verification.
    #[arg(long)]
    keys: Option<PathBuf>,
    /// Timestamp to stamp (ISO 8601 UTC); defaults to system clock.
    #[arg(long)]
    now: Option<String>,
    /// Output path for the signature envelope.
    #[arg(long)]
    out: PathBuf,
}

#[derive(Parser)]
struct VerifyArgs {
    /// Manifest file.
    #[arg(long)]
    manifest: PathBuf,
    /// Detached signature envelope.
    #[arg(long)]
    sig: PathBuf,
    /// Pinned key registry.
    #[arg(long)]
    keys: PathBuf,
    /// Reference timestamp for freshness (ISO 8601 UTC); defaults to system clock.
    #[arg(long)]
    now: Option<String>,
    /// Minimum acceptable sequence (anti-rollback).
    #[arg(long)]
    min_sequence: Option<u64>,
}

#[derive(Parser)]
struct DiffArgs {
    /// Old manifest.
    #[arg(long)]
    old: PathBuf,
    /// New manifest.
    #[arg(long)]
    new: PathBuf,
    /// Output path for the changelog JSON.
    #[arg(long)]
    out: PathBuf,
}

fn main() -> ExitCode {
    let cli = Cli::parse();
    match cli.command {
        Command::Build(args) => run_result(cmd_build(args)),
        Command::Sign(args) => run_result(cmd_sign(args)),
        Command::Verify(args) => cmd_verify(args),
        Command::Diff(args) => run_result(cmd_diff(args)),
    }
}

/// Map a `Result<()>` to exit 0/1, printing errors to stderr.
fn run_result(r: Result<()>) -> ExitCode {
    match r {
        Ok(()) => ExitCode::SUCCESS,
        Err(e) => {
            eprintln!("error: {e}");
            ExitCode::FAILURE
        }
    }
}

fn cmd_verify(args: VerifyArgs) -> ExitCode {
    let now = match resolve_now(args.now.as_deref()) {
        Ok(now) => now,
        Err(e) => {
            eprintln!("error: {e}");
            return ExitCode::FAILURE;
        }
    };

    let result = (|| -> Result<()> {
        let manifest_bytes = std::fs::read(&args.manifest)?;
        let sig_bytes = std::fs::read(&args.sig)?;
        let registry = read_registry(&args.keys)?;

        let verified = verify_manifest(
            &manifest_bytes,
            &sig_bytes,
            &registry,
            now,
            args.min_sequence,
        )?;

        // Machine-readable freshness on stdout (SPEC §5: prints freshness state).
        println!("{}", verified.freshness);
        Ok(())
    })();

    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(e) => {
            eprintln!("invalid: {e}");
            ExitCode::FAILURE
        }
    }
}

fn cmd_sign(args: SignArgs) -> Result<()> {
    let pem = match (&args.key_file, &args.key_env) {
        (Some(path), None) => std::fs::read_to_string(path)?,
        (None, Some(var)) => std::env::var(var).map_err(|_| {
            ManifestError::Validation(format!("environment variable {var} not set"))
        })?,
        (None, None) => {
            return Err(ManifestError::Validation(
                "one of --key-file or --key-env is required".into(),
            ))
        }
        (Some(_), Some(_)) => {
            return Err(ManifestError::Validation(
                "--key-file and --key-env are mutually exclusive".into(),
            ))
        }
    };

    let signing_key = crypto::signing_key_from_pkcs8_pem(&pem)?;
    let manifest_bytes = std::fs::read(&args.manifest)?;
    let signed_at = format_utc(resolve_now(args.now.as_deref())?);

    let registry = match &args.keys {
        Some(path) => Some(read_registry(path)?),
        None => None,
    };

    let envelope = sign_manifest(
        &signing_key,
        &manifest_bytes,
        &args.key_id,
        &signed_at,
        registry.as_ref(),
    )?;

    let mut json = serde_json::to_string_pretty(&envelope)?;
    json.push('\n');
    std::fs::write(&args.out, json)?;
    Ok(())
}

fn cmd_build(args: BuildArgs) -> Result<()> {
    // Resolve tier → registry root + default output name.
    let (registry, out) = resolve_tier_args(args.tier.as_deref(), args.registry, args.out)?;

    let generated_at = resolve_now(args.now.as_deref())?;
    let added_at = format_utc(generated_at);

    let previous = match &args.previous {
        Some(path) => {
            let bytes = std::fs::read(path)?;
            let m: Manifest = serde_json::from_slice(&bytes)?;
            Some(m)
        }
        None => None,
    };
    let sequence = next_sequence(previous.as_ref())?;

    let entries = collect_entries(&registry, &args.base_url, &added_at)?;
    let manifest = assemble_manifest(entries, generated_at, &args.signer_key_id, sequence)?;

    let mut json = serde_json::to_string_pretty(&manifest)?;
    json.push('\n');
    std::fs::write(&out, json)?;
    Ok(())
}

/// Resolve `--tier` + optional `--registry` / `--out` into concrete paths.
///
/// Rules (SPEC §5 M2 extension):
/// - `--tier` must be one of the known tiers (`beta`, `main`); unknown values error loudly.
/// - When `--tier` is given:
///   - `registry` defaults to `<cwd>/<tier>` if `--registry` is omitted.
///   - `out` defaults to `manifest-<tier>.json` in cwd if `--out` is omitted.
/// - When neither `--tier` nor `--registry` is given → error (one is required).
/// - Explicit `--registry` / `--out` always override the tier-derived defaults.
fn resolve_tier_args(
    tier: Option<&str>,
    registry: Option<PathBuf>,
    out: Option<PathBuf>,
) -> Result<(PathBuf, PathBuf)> {
    match tier {
        Some(t) => {
            // Validate: reject unknown tiers loudly.
            if !KNOWN_TIERS.contains(&t) {
                return Err(ManifestError::Validation(format!(
                    "unknown --tier {t:?}: known tiers are {}",
                    KNOWN_TIERS.join(", ")
                )));
            }
            let resolved_registry = registry.unwrap_or_else(|| PathBuf::from(t));
            let resolved_out = out.unwrap_or_else(|| PathBuf::from(format!("manifest-{t}.json")));
            Ok((resolved_registry, resolved_out))
        }
        None => {
            // No tier: --registry and --out must both be explicit.
            let r = registry.ok_or_else(|| {
                ManifestError::Validation("one of --tier or --registry is required".into())
            })?;
            let o = out.ok_or_else(|| {
                ManifestError::Validation("--out is required when --tier is not set".into())
            })?;
            Ok((r, o))
        }
    }
}

fn cmd_diff(args: DiffArgs) -> Result<()> {
    let old: Manifest = serde_json::from_slice(&std::fs::read(&args.old)?)?;
    let new: Manifest = serde_json::from_slice(&std::fs::read(&args.new)?)?;
    let diff = diff_manifests(&old, &new);

    let mut json = serde_json::to_string_pretty(&diff)?;
    json.push('\n');
    std::fs::write(&args.out, json)?;
    Ok(())
}

/// Load and parse a key registry file.
fn read_registry(path: &Path) -> Result<KeyRegistry> {
    let bytes = std::fs::read(path)?;
    let registry: KeyRegistry = serde_json::from_slice(&bytes)?;
    Ok(registry)
}

/// Resolve `--now` (or system clock) to a UTC instant.
fn resolve_now(now: Option<&str>) -> Result<chrono::DateTime<Utc>> {
    match now {
        Some(s) => parse_timestamp(s),
        None => Ok(Utc::now()),
    }
}
