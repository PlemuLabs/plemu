//! plemu-screen CLI: screen registry entry directories for content-trust compliance.
//!
//! Exit codes:
//! - `0`  — pass (all checks green)
//! - `10` — needs human review (at least one flag)
//! - `20` — hard fail (at least one failing hard check)
//! - `2`  — tool error: denylist missing/unreadable/invalid — NOT a verdict

use anyhow::{Context, Result};
use clap::Parser;
use plemu_screen::{open_denylist, screen_entry_full, ScreenOpts, Verdict};
use std::path::PathBuf;

/// Tool-error exit code (distinct from the 0/10/20 verdict codes).
const EXIT_TOOL_ERROR: i32 = 2;

#[derive(Parser, Debug)]
#[command(
    name = "plemu-screen",
    about = "Screen registry entry directories for content-trust compliance",
    after_help = "Exit codes:\n  \
        0    pass\n  \
        10   needs human review (flags)\n  \
        20   hard fail\n  \
        2    tool error (denylist missing, unreadable, or missing required tables)"
)]
struct Cli {
    /// Screen a single entry directory
    #[arg(long, value_name = "DIR")]
    entry: Option<PathBuf>,

    /// Screen all entries in the registry (requires --registry)
    #[arg(long)]
    all: bool,

    /// Registry root directory (used with --all)
    #[arg(long, value_name = "DIR")]
    registry: Option<PathBuf>,

    /// Path to SQLite denylist (from plemu-dat)
    #[arg(long, value_name = "FILE")]
    denylist: Option<PathBuf>,

    /// Optional key registry JSON (unused currently, reserved for future use)
    #[arg(long, value_name = "FILE")]
    keys: Option<PathBuf>,

    /// Override current time for timestamp checks (ISO 8601 / RFC 3339)
    #[arg(long, value_name = "ISO")]
    now: Option<String>,

    /// Write JSON report to file (prints to stdout if omitted)
    #[arg(long, value_name = "FILE")]
    out: Option<PathBuf>,
}

fn main() -> Result<()> {
    let cli = Cli::parse();

    // Fail fast on an unusable denylist: a typo'd --denylist path must be a
    // loud tool error (exit 2), never a silent all-pass screening run.
    if let Some(denylist_path) = cli.denylist.as_deref() {
        if let Err(e) = open_denylist(denylist_path) {
            eprintln!("plemu-screen: {e}");
            std::process::exit(EXIT_TOOL_ERROR);
        }
    }

    if cli.all {
        // Batch mode: screen every entry in the registry
        let registry = cli
            .registry
            .as_deref()
            .context("--registry DIR is required when using --all")?;

        let mut worst = Verdict::Pass;
        let mut all_reports: Vec<serde_json::Value> = Vec::new();

        // Walk <registry>/<system>/<slug>/
        for sys_entry in std::fs::read_dir(registry)
            .with_context(|| format!("reading registry {}", registry.display()))?
        {
            let sys_entry = sys_entry?;
            let sys_path = sys_entry.path();
            if !sys_path.is_dir() {
                continue;
            }
            for slug_entry in std::fs::read_dir(&sys_path)? {
                let slug_entry = slug_entry?;
                let entry_dir = slug_entry.path();
                if !entry_dir.is_dir() {
                    continue;
                }
                if !entry_dir.join("metadata.json").exists() {
                    continue;
                }

                let opts = ScreenOpts {
                    entry_dir: &entry_dir,
                    denylist_path: cli.denylist.as_deref(),
                    now_override: cli.now.as_deref(),
                };

                match screen_entry_full(&opts) {
                    Ok(report) => {
                        if report.verdict == Verdict::Fail
                            || (report.verdict == Verdict::Review && worst == Verdict::Pass)
                        {
                            worst = report.verdict.clone();
                        }
                        all_reports.push(serde_json::to_value(&report)?);
                    }
                    Err(e) => {
                        eprintln!("ERROR {}: {}", entry_dir.display(), e);
                        worst = Verdict::Fail;
                    }
                }
            }
        }

        let batch_json = serde_json::json!({
            "tool": "plemu-screen",
            "version": env!("CARGO_PKG_VERSION"),
            "verdict": match worst { Verdict::Pass => "pass", Verdict::Review => "review", Verdict::Fail => "fail" },
            "entries": all_reports,
        });

        write_or_print(&cli.out, &batch_json)?;

        match worst {
            Verdict::Pass => {}
            Verdict::Review => std::process::exit(10),
            Verdict::Fail => std::process::exit(20),
        }
    } else {
        // Single-entry mode
        let entry_dir = cli
            .entry
            .as_deref()
            .context("--entry DIR is required (or use --all --registry DIR)")?;

        let opts = ScreenOpts {
            entry_dir,
            denylist_path: cli.denylist.as_deref(),
            now_override: cli.now.as_deref(),
        };

        let report = screen_entry_full(&opts)?;
        let json = serde_json::to_value(&report)?;

        write_or_print(&cli.out, &json)?;

        match report.verdict {
            Verdict::Pass => {}
            Verdict::Review => std::process::exit(10),
            Verdict::Fail => std::process::exit(20),
        }
    }

    Ok(())
}

fn write_or_print(out: &Option<PathBuf>, value: &serde_json::Value) -> Result<()> {
    let text = serde_json::to_string_pretty(value)?;
    if let Some(out_path) = out {
        std::fs::write(out_path, &text)
            .with_context(|| format!("writing report to {}", out_path.display()))?;
    } else {
        println!("{text}");
    }
    Ok(())
}
