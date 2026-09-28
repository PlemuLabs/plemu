//! plemu-dat CLI: build a SQLite denylist from No-Intro/Redump XML DAT files.
//!
//! SPEC §5 contract: `plemu-dat build --input DIR --out denylist.sqlite`

use anyhow::{Context, Result};
use clap::{Parser, Subcommand};
use std::path::PathBuf;

#[derive(Parser, Debug)]
#[command(name = "plemu-dat", about = "Build SQLite denylist from DAT files")]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Ingest Logiqx XML .dat files and write a SQLite denylist (SPEC §5.1).
    Build {
        /// Directory containing .dat / .xml files (recursive)
        #[arg(long, value_name = "DIR")]
        input: PathBuf,

        /// Output SQLite database path
        #[arg(long, value_name = "FILE")]
        out: PathBuf,

        /// Source tag to record in the hashes table (optional)
        #[arg(long, value_name = "TAG")]
        source: Option<String>,

        /// Override the built_at timestamp in meta (ISO 8601 Z)
        #[arg(long, value_name = "ISO")]
        now: Option<String>,
    },
}

fn main() -> Result<()> {
    let cli = Cli::parse();

    match cli.command {
        Commands::Build {
            input,
            out,
            source,
            now,
        } => {
            let source_ref = source.as_deref();
            let (hash_records, name_records, dat_files) =
                plemu_dat::parse_dat_dir(&input, source_ref)?;

            eprintln!(
                "Parsed {} DAT file(s): {} hash records, {} name records",
                dat_files,
                hash_records.len(),
                name_records.len()
            );

            let built_at = resolve_built_at(now.as_deref())?;
            let meta = plemu_dat::DenylistMeta {
                built_at,
                dat_files,
            };

            let out_str = out.to_string_lossy();
            plemu_dat::build_denylist(&out_str, &hash_records, &name_records, &meta)?;

            eprintln!("Wrote denylist to {}", out_str);
        }
    }

    Ok(())
}

/// Resolve the `built_at` meta value: validate `--now` if given, else the
/// system clock. Always ISO 8601 Z.
fn resolve_built_at(now: Option<&str>) -> Result<String> {
    use chrono::{DateTime, SecondsFormat, Utc};
    let ts: DateTime<Utc> = match now {
        Some(s) => DateTime::parse_from_rfc3339(s)
            .with_context(|| format!("--now '{}' is not a valid ISO 8601 timestamp", s))?
            .with_timezone(&Utc),
        None => Utc::now(),
    };
    Ok(ts.to_rfc3339_opts(SecondsFormat::Secs, true))
}
