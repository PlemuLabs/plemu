//! plemu-core-audit CLI: audit the license lock file for approved cores.

use anyhow::Result;
use clap::Parser;
use plemu_core_audit::{audit_lock, AuditOpts, LockFile};
use std::path::PathBuf;

#[derive(Parser, Debug)]
#[command(
    name = "plemu-core-audit",
    about = "Audit the license lock file for approved cores"
)]
struct Cli {
    /// Path to the expected-licenses.lock JSON file
    #[arg(long, visible_alias = "licenses", value_name = "FILE", default_value = "cores/expected-licenses.lock")]
    lock: PathBuf,

    /// Root directory containing per-core subdirectories
    #[arg(long, value_name = "DIR", default_value = "cores")]
    cores_dir: PathBuf,

    /// Optional build-config JSON file
    #[arg(long, visible_alias = "cores", value_name = "FILE")]
    build_config: Option<PathBuf>,

    /// Path to forbidden.json (optional)
    #[arg(long, value_name = "FILE")]
    forbidden: Option<PathBuf>,

    /// Treat TBD entries as failures (default: warnings only)
    #[arg(long)]
    strict: bool,

    /// Write JSON report to this file
    #[arg(long, value_name = "FILE")]
    out: Option<PathBuf>,
}

fn main() -> Result<()> {
    let cli = Cli::parse();

    let lock_bytes = std::fs::read(&cli.lock)?;
    let lock: LockFile = serde_json::from_slice(&lock_bytes)?;

    let opts = AuditOpts {
        cores_dir: &cli.cores_dir,
        build_config_path: cli.build_config.as_deref(),
        strict: cli.strict,
    };

    let report = audit_lock(&lock, &opts)?;

    let json = serde_json::to_string_pretty(&report)?;

    if let Some(out_path) = &cli.out {
        std::fs::write(out_path, &json)?;
    } else {
        println!("{json}");
    }

    if !report.clean {
        std::process::exit(20);
    }

    Ok(())
}
