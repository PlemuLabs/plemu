//! All 11 screening checks for plemu-screen.

use std::io::Read;
use std::path::Path;

use base64::Engine;
use chrono::{DateTime, Duration, Utc};
use plemu_schemas::{
    build_attestation_message, AttestationParams, RegistryAttestation, RegistryMetadata, System,
};
use sha2::{Digest, Sha256};

use crate::error::ScreenError;

// ── Common result types ───────────────────────────────────────────────────────

/// Unified result of a single check.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CheckStatus {
    Pass,
    Flag,
    Fail,
}

impl CheckStatus {
    pub fn as_str(&self) -> &'static str {
        match self {
            CheckStatus::Pass => "pass",
            CheckStatus::Flag => "flag",
            CheckStatus::Fail => "fail",
        }
    }
}

impl serde::Serialize for CheckStatus {
    fn serialize<S: serde::Serializer>(&self, s: S) -> Result<S::Ok, S::Error> {
        s.serialize_str(self.as_str())
    }
}

/// Result of a single check (new unified type).
#[derive(Debug, Clone)]
pub struct CheckResult {
    pub id: String,
    pub status: CheckStatus,
    pub detail: String,
}

// ── Legacy result types (kept for backward compatibility with existing tests) ──

/// Result of the DIGESTS check.
#[derive(Debug, Clone)]
pub struct DigestsResult {
    pub ok: bool,
    pub expected: String,
    pub got: String,
}

/// Result of the ATTESTATION check.
#[derive(Debug, Clone)]
pub struct AttestationResult {
    pub ok: bool,
    pub message: Option<String>,
}

/// Result of the ARCHIVE_SAFETY check.
#[derive(Debug, Clone)]
pub struct ArchiveSafetyResult {
    pub ok: bool,
    /// None if artifact is not a zip file.
    pub checked: bool,
    pub message: Option<String>,
}

/// Combined result of all three legacy checks.
#[derive(Debug, Clone)]
pub struct ScreenResult {
    pub digests: DigestsResult,
    pub attestation: AttestationResult,
    pub archive_safety: ArchiveSafetyResult,
    pub overall_ok: bool,
}

// ── Digest helpers ────────────────────────────────────────────────────────────

pub struct ArtifactDigests {
    pub crc32: String,
    pub md5: String,
    pub sha1: String,
    pub sha256: String,
}

fn compute_crc32(bytes: &[u8]) -> String {
    let mut h = crc32fast::Hasher::new();
    h.update(bytes);
    format!("{:08x}", h.finalize())
}

fn compute_md5(bytes: &[u8]) -> String {
    use md5::Digest as Md5Digest;
    hex::encode(md5::Md5::new().chain_update(bytes).finalize())
}

fn compute_sha1(bytes: &[u8]) -> String {
    use sha1::Digest as Sha1Digest;
    hex::encode(sha1::Sha1::new().chain_update(bytes).finalize())
}

fn compute_sha256(bytes: &[u8]) -> String {
    hex::encode(sha2::Sha256::digest(bytes))
}

pub fn compute_all_digests(bytes: &[u8]) -> ArtifactDigests {
    ArtifactDigests {
        crc32: compute_crc32(bytes),
        md5: compute_md5(bytes),
        sha1: compute_sha1(bytes),
        sha256: compute_sha256(bytes),
    }
}

/// Strip known ROM headers to get the headerless payload for matching.
///
/// Returns `(variant, payload)`. Magic-based detections (iNES `NES\x1a`,
/// Lynx `LYNX\0` per SPEC §5.2: skip 16 / skip 64) apply regardless of the
/// claimed system so a mislabelled entry cannot dodge headerless matching;
/// size-heuristic strips stay keyed to the claimed system.
fn strip_header(bytes: &[u8], system: System) -> Option<(&'static str, &[u8])> {
    if bytes.len() >= 16 && bytes.starts_with(b"NES\x1a") {
        return Some(("nes", &bytes[16..]));
    }
    if bytes.len() >= 64 && bytes.starts_with(b"LYNX\0") {
        return Some(("lynx", &bytes[64..]));
    }
    match system {
        System::Snes if bytes.len() >= 512 && bytes.len() % 1024 == 512 => {
            Some(("snes", &bytes[512..]))
        }
        System::A26 if bytes.len() >= 128 && bytes.len() % 1024 == 128 => {
            Some(("a78-style-128", &bytes[128..]))
        }
        _ => None,
    }
}

// ── Legacy entry runner (3-check) ─────────────────────────────────────────────

/// Run the original three checks on an entry directory.
pub fn screen_entry(
    entry_dir: &Path,
    now_override: Option<&str>,
) -> Result<ScreenResult, ScreenError> {
    let metadata_path = entry_dir.join("metadata.json");
    if !metadata_path.exists() {
        return Err(ScreenError::MissingFile("metadata.json".to_string()));
    }
    let metadata_bytes = std::fs::read(&metadata_path)?;
    let metadata: RegistryMetadata = serde_json::from_slice(&metadata_bytes)?;

    let attestation_path = entry_dir.join("attestation.json");
    if !attestation_path.exists() {
        return Err(ScreenError::MissingFile("attestation.json".to_string()));
    }
    let attestation_bytes = std::fs::read(&attestation_path)?;
    let attestation: RegistryAttestation = serde_json::from_slice(&attestation_bytes)?;

    let artifact_path = entry_dir.join(&metadata.artifact.filename);
    if !artifact_path.exists() {
        return Err(ScreenError::MissingFile(metadata.artifact.filename.clone()));
    }

    let now: DateTime<Utc> = if let Some(now_str) = now_override {
        DateTime::parse_from_rfc3339(now_str)
            .map_err(|e| ScreenError::TimestampParse(e.to_string()))?
            .with_timezone(&Utc)
    } else {
        Utc::now()
    };

    let digests = check_digests(&artifact_path, &metadata.artifact.sha256)?;
    let attestation_result = check_attestation(&metadata, &attestation, now)?;
    let archive_safety = check_archive_safety(&artifact_path, metadata.system)?;

    let overall_ok = digests.ok && attestation_result.ok && archive_safety.ok;

    Ok(ScreenResult {
        digests,
        attestation: attestation_result,
        archive_safety,
        overall_ok,
    })
}

// ── Check 1: DIGESTS ──────────────────────────────────────────────────────────

/// DIGESTS check: verify SHA-256 of the artifact file.
pub fn check_digests(
    artifact_path: &Path,
    expected_sha256: &str,
) -> Result<DigestsResult, ScreenError> {
    let mut hasher = Sha256::new();
    let mut file = std::fs::File::open(artifact_path)?;
    let mut buf = [0u8; 65536];
    loop {
        let n = file.read(&mut buf)?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    let hash = hasher.finalize();
    let got = hex::encode(hash);
    let ok = got.eq_ignore_ascii_case(expected_sha256);
    Ok(DigestsResult {
        ok,
        expected: expected_sha256.to_string(),
        got,
    })
}

// ── Check 2: ATTESTATION ──────────────────────────────────────────────────────

/// ATTESTATION check: verify ed25519 signature and timestamp constraints.
pub fn check_attestation(
    metadata: &RegistryMetadata,
    attestation: &RegistryAttestation,
    now: DateTime<Utc>,
) -> Result<AttestationResult, ScreenError> {
    // The signing wallet must be the declared creator wallet: signature and
    // message rebuild both use attestation.wallet, so without this equality a
    // valid signature from ANY wallet would pass for someone else's entry.
    // Curator attestations keep signature-only validation for now; a curator
    // allowlist arrives with --keys in a later version.
    if attestation.role == plemu_schemas::AttestationRole::Creator
        && attestation.wallet != metadata.creator.wallet
    {
        return Ok(AttestationResult {
            ok: false,
            message: Some(format!(
                "attestation wallet '{}' does not match metadata.creator.wallet '{}'",
                attestation.wallet, metadata.creator.wallet
            )),
        });
    }

    let expected_message = build_attestation_message(&AttestationParams {
        role: attestation.role,
        title: &metadata.title,
        system: metadata.system,
        sha256: &metadata.sha256,
        license: metadata.license.license_type,
        wallet: &attestation.wallet,
        timestamp: &attestation.signed_at,
    });

    if expected_message != attestation.message {
        return Ok(AttestationResult {
            ok: false,
            message: Some(format!(
                "Message mismatch: expected:\n{}\ngot:\n{}",
                expected_message, attestation.message
            )),
        });
    }

    let signed_at: DateTime<Utc> = DateTime::parse_from_rfc3339(&attestation.signed_at)
        .map_err(|e| ScreenError::TimestampParse(format!("signed_at: {e}")))?
        .with_timezone(&Utc);

    let submitted_at: DateTime<Utc> =
        DateTime::parse_from_rfc3339(&metadata.submission.submitted_at)
            .map_err(|e| ScreenError::TimestampParse(format!("submittedAt: {e}")))?
            .with_timezone(&Utc);

    if signed_at > now + Duration::hours(24) {
        return Ok(AttestationResult {
            ok: false,
            message: Some(format!(
                "signed_at ({}) is more than 24h in the future (now: {})",
                signed_at, now
            )),
        });
    }

    if submitted_at - signed_at > Duration::days(90) {
        return Ok(AttestationResult {
            ok: false,
            message: Some(format!(
                "signed_at ({}) is more than 90 days before submittedAt ({})",
                signed_at, submitted_at
            )),
        });
    }

    let wallet_bytes = bs58::decode(&attestation.wallet)
        .into_vec()
        .map_err(|e| ScreenError::Crypto(format!("base58 decode wallet: {e}")))?;

    // Length validation and conversion in one step — no unwrap in program code.
    let pubkey_bytes: [u8; 32] = match wallet_bytes.try_into() {
        Ok(bytes) => bytes,
        Err(bytes) => {
            return Ok(AttestationResult {
                ok: false,
                message: Some(format!(
                    "wallet pubkey must be 32 bytes, got {}",
                    bytes.len()
                )),
            });
        }
    };
    let verifying_key = ed25519_dalek::VerifyingKey::from_bytes(&pubkey_bytes)
        .map_err(|e| ScreenError::Crypto(format!("invalid pubkey: {e}")))?;

    let sig_bytes = base64::engine::general_purpose::STANDARD
        .decode(&attestation.signature)
        .map_err(|e| ScreenError::Crypto(format!("base64 decode signature: {e}")))?;

    // Length validation and conversion in one step — no unwrap in program code.
    let sig_arr: [u8; 64] = match sig_bytes.try_into() {
        Ok(bytes) => bytes,
        Err(bytes) => {
            return Ok(AttestationResult {
                ok: false,
                message: Some(format!("signature must be 64 bytes, got {}", bytes.len())),
            });
        }
    };
    let signature = ed25519_dalek::Signature::from_bytes(&sig_arr);

    if verifying_key
        .verify_strict(attestation.message.as_bytes(), &signature)
        .is_err()
    {
        return Ok(AttestationResult {
            ok: false,
            message: Some("signature verification failed".to_string()),
        });
    }

    Ok(AttestationResult {
        ok: true,
        message: None,
    })
}

// ── Check 3: ARCHIVE_SAFETY ───────────────────────────────────────────────────

/// ARCHIVE_SAFETY check: inspect zip files for path traversal and zip-bomb risk.
///
/// v1 policy: artifacts are raw files. A ZIP is allowed ONLY for the disc
/// systems ps1/dc; a zip for any other system is a hard fail.
pub fn check_archive_safety(
    artifact_path: &Path,
    system: System,
) -> Result<ArchiveSafetyResult, ScreenError> {
    let bytes = std::fs::read(artifact_path)?;

    if bytes.len() < 4 || &bytes[..4] != b"PK\x03\x04" {
        return Ok(ArchiveSafetyResult {
            ok: true,
            checked: false,
            message: None,
        });
    }

    if !matches!(system, System::Ps1 | System::Dc) {
        return Ok(ArchiveSafetyResult {
            ok: false,
            checked: true,
            message: Some(format!(
                "zip artifact not allowed for system '{}': ZIP is permitted only for disc systems ps1/dc",
                system
            )),
        });
    }

    let cursor = std::io::Cursor::new(&bytes);
    let mut archive = match zip::ZipArchive::new(cursor) {
        Ok(a) => a,
        Err(e) => {
            return Ok(ArchiveSafetyResult {
                ok: false,
                checked: true,
                message: Some(format!("invalid zip: {e}")),
            });
        }
    };

    for i in 0..archive.len() {
        let file = archive
            .by_index(i)
            .map_err(|e| ScreenError::ArchiveSafety(format!("reading zip entry {i}: {e}")))?;

        let name = file.name().to_string();

        if name.contains("..") || name.starts_with('/') {
            return Ok(ArchiveSafetyResult {
                ok: false,
                checked: true,
                message: Some(format!("path traversal detected in entry: {name}")),
            });
        }

        let sep_count = name.chars().filter(|&c| c == '/').count();
        let is_dir_entry = name.ends_with('/');
        let depth = if is_dir_entry {
            sep_count
        } else {
            sep_count + 1
        };
        if depth > 1 {
            return Ok(ArchiveSafetyResult {
                ok: false,
                checked: true,
                message: Some(format!("entry nesting depth {} > 1 in: {name}", depth)),
            });
        }

        let compressed = file.compressed_size();
        let uncompressed = file.size();
        if compressed > 0 && uncompressed / compressed > 100 {
            return Ok(ArchiveSafetyResult {
                ok: false,
                checked: true,
                message: Some(format!(
                    "compression ratio {}/{} = {} > 100 in entry: {name}",
                    uncompressed,
                    compressed,
                    uncompressed / compressed
                )),
            });
        }
    }

    Ok(ArchiveSafetyResult {
        ok: true,
        checked: true,
        message: None,
    })
}

// ── Check 4: DAT_MATCH ────────────────────────────────────────────────────────

/// A matched denylist hash row: (rom_name, game_name).
type HashRow = (Option<String>, Option<String>);

/// Look up one (algo, hex) pair in the denylist `hashes` table.
///
/// `None` = no match; a query error is a hard error, never a pass — the
/// denylist being unqueryable must not silently green-light an entry.
fn denylist_hash_lookup(
    conn: &rusqlite::Connection,
    algo: &str,
    hex: &str,
) -> Result<Option<HashRow>, ScreenError> {
    use rusqlite::OptionalExtension;
    conn.query_row(
        "SELECT rom_name, game_name FROM hashes WHERE algo = ?1 AND hex = ?2 LIMIT 1",
        rusqlite::params![algo, hex],
        |r| {
            Ok((
                r.get::<_, Option<String>>(0)?,
                r.get::<_, Option<String>>(1)?,
            ))
        },
    )
    .optional()
    .map_err(|e| ScreenError::Denylist(format!("hashes lookup ({algo}) failed: {e}")))
}

/// DAT_MATCH check: look up all artifact hashes in the denylist.
/// Returns Fail if any hash matches; errors if the denylist cannot be queried.
pub fn check_dat_match(
    bytes: &[u8],
    system: System,
    conn: &rusqlite::Connection,
) -> Result<CheckResult, ScreenError> {
    let digests = compute_all_digests(bytes);

    let pairs = [
        ("crc32", digests.crc32.as_str()),
        ("md5", digests.md5.as_str()),
        ("sha1", digests.sha1.as_str()),
        ("sha256", digests.sha256.as_str()),
    ];

    for (algo, hex) in pairs {
        if let Some(row) = denylist_hash_lookup(conn, algo, hex)? {
            let game_name = row.1.unwrap_or_else(|| row.0.unwrap_or_default());
            return Ok(CheckResult {
                id: "DAT_MATCH".to_string(),
                status: CheckStatus::Fail,
                detail: format!("{}:{} matches denylist entry: '{}'", algo, hex, game_name),
            });
        }
    }

    // Also check headerless variants — SPEC §5.2: ANY computed digest
    // (headered or headerless, any algo) present in the denylist ⇒ fail.
    if let Some((variant, stripped)) = strip_header(bytes, system) {
        let stripped_digests = compute_all_digests(stripped);
        let hless_pairs = [
            ("crc32", stripped_digests.crc32.as_str()),
            ("md5", stripped_digests.md5.as_str()),
            ("sha1", stripped_digests.sha1.as_str()),
            ("sha256", stripped_digests.sha256.as_str()),
        ];
        for (algo, hex) in hless_pairs {
            if let Some(row) = denylist_hash_lookup(conn, algo, hex)? {
                let game_name = row.1.unwrap_or_else(|| row.0.unwrap_or_default());
                return Ok(CheckResult {
                    id: "DAT_MATCH".to_string(),
                    status: CheckStatus::Fail,
                    detail: format!(
                        "{}:{} (headerless {}) matches denylist entry: '{}'",
                        algo, hex, variant, game_name
                    ),
                });
            }
        }
    }

    Ok(CheckResult {
        id: "DAT_MATCH".to_string(),
        status: CheckStatus::Pass,
        detail: "no matches in denylist".to_string(),
    })
}

// ── Check 5: HDR_VALID ────────────────────────────────────────────────────────

/// GB boot logo at ROM offset 0x104..=0x133 (48 bytes).
const GB_BOOT_LOGO: [u8; 48] = [
    0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
    0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
    0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E,
];

fn gb_hdr_logo_ok(bytes: &[u8]) -> bool {
    bytes.len() >= 0x134 && bytes[0x104..0x134] == GB_BOOT_LOGO
}

fn gb_hdr_checksum_ok(bytes: &[u8]) -> bool {
    if bytes.len() < 0x14E {
        return false;
    }
    let mut x: u8 = 0;
    for &b in &bytes[0x134..=0x14C] {
        x = x.wrapping_sub(b).wrapping_sub(1);
    }
    bytes[0x14D] == x
}

/// Validate a TIC-80 cart chunk stream per the SPEC §5.2 wording:
/// `[bank/type byte][u16 size LE][data]…` exactly covering the file; the
/// final chunk may have size 0 per TIC-80 convention.
///
/// NOTE: real TIC-80 carts use a 4-byte chunk header (type/bank byte,
/// u16 size LE, one reserved byte). The SPEC wording omits the reserved
/// byte, so we implement the SPEC's 3-byte header as written rather than
/// silently deviating; flagged discrepancy, not auto-reject either way.
fn tic80_chunk_stream_ok(bytes: &[u8]) -> Result<usize, String> {
    if bytes.is_empty() {
        return Err("empty file: no chunk header".to_string());
    }
    let mut pos: usize = 0;
    let mut chunks: usize = 0;
    while pos < bytes.len() {
        if bytes.len() - pos < 3 {
            return Err(format!(
                "truncated chunk header at offset 0x{:x} ({} trailing byte(s))",
                pos,
                bytes.len() - pos
            ));
        }
        let size = u16::from_le_bytes([bytes[pos + 1], bytes[pos + 2]]) as usize;
        let data_end = pos
            .checked_add(3)
            .and_then(|p| p.checked_add(size))
            .ok_or_else(|| "chunk size overflow".to_string())?;
        if data_end > bytes.len() {
            return Err(format!(
                "chunk at offset 0x{:x} declares {} data byte(s) but only {} remain",
                pos,
                size,
                bytes.len() - pos - 3
            ));
        }
        pos = data_end;
        chunks += 1;
    }
    Ok(chunks)
}

fn gba_hdr_ok(bytes: &[u8]) -> bool {
    if bytes.len() < 0xBE {
        return false;
    }
    // Fixed value at 0xB2
    if bytes[0xB2] != 0x96 {
        return false;
    }
    // Header checksum: -sum(bytes[0xA0..=0xBC]) - 0x19
    let mut sum: u8 = 0;
    for &b in &bytes[0xA0..=0xBC] {
        sum = sum.wrapping_add(b);
    }
    let expected = (sum.wrapping_add(0x19)).wrapping_neg();
    bytes[0xBD] == expected
}

/// HDR_VALID check: validate system-specific ROM header.
pub fn check_hdr_valid(bytes: &[u8], system: System) -> CheckResult {
    let (status, detail) = match system {
        System::Nes => {
            if bytes.starts_with(b"NES\x1a") {
                (CheckStatus::Pass, "iNES header present".to_string())
            } else {
                (CheckStatus::Flag, "missing iNES magic NES\\x1a".to_string())
            }
        }
        System::Gb | System::Gbc => {
            let logo_ok = gb_hdr_logo_ok(bytes);
            let cksum_ok = gb_hdr_checksum_ok(bytes);
            if logo_ok && cksum_ok {
                (CheckStatus::Pass, "GB header valid".to_string())
            } else {
                let mut reasons = Vec::new();
                if !logo_ok {
                    reasons.push("boot logo mismatch");
                }
                if !cksum_ok {
                    reasons.push("header checksum mismatch");
                }
                (CheckStatus::Flag, reasons.join(", "))
            }
        }
        System::Gba => {
            if gba_hdr_ok(bytes) {
                (CheckStatus::Pass, "GBA header valid".to_string())
            } else {
                (
                    CheckStatus::Flag,
                    "GBA header invalid (bad fixed byte or checksum)".to_string(),
                )
            }
        }
        System::Md => {
            if bytes.len() >= 0x104 && &bytes[0x100..0x104] == b"SEGA" {
                (
                    CheckStatus::Pass,
                    "Mega Drive SEGA magic present".to_string(),
                )
            } else {
                (CheckStatus::Flag, "missing SEGA magic at 0x100".to_string())
            }
        }
        System::Sms | System::Gg => {
            let offsets = [0x7FF0usize, 0x3FF0, 0x1FF0];
            let found = offsets
                .iter()
                .any(|&off| bytes.len() >= off + 8 && &bytes[off..off + 8] == b"TMR SEGA");
            if found {
                (CheckStatus::Pass, "TMR SEGA magic present".to_string())
            } else {
                (CheckStatus::Flag, "TMR SEGA magic not found".to_string())
            }
        }
        System::A26 => {
            let sz = bytes.len();
            let is_pow2 = sz > 0 && sz.is_power_of_two();
            let in_range = (2048..=65536).contains(&sz);
            if is_pow2 && in_range {
                (
                    CheckStatus::Pass,
                    format!("Atari 2600 ROM size {} bytes OK", sz),
                )
            } else {
                (
                    CheckStatus::Flag,
                    format!(
                        "unexpected Atari 2600 ROM size {} bytes (not power-of-two in 2KiB–64KiB)",
                        sz
                    ),
                )
            }
        }
        System::Tic80 => match tic80_chunk_stream_ok(bytes) {
            Ok(chunks) => (
                CheckStatus::Pass,
                format!("TIC-80 chunk stream valid ({} chunk(s))", chunks),
            ),
            Err(reason) => (
                CheckStatus::Flag,
                format!("TIC-80 chunk stream invalid: {}", reason),
            ),
        },
        System::Wasm4 => {
            if bytes.starts_with(b"\0asm") && bytes.len() <= 65536 {
                (
                    CheckStatus::Pass,
                    "WASM-4 cart: wasm magic + size OK".to_string(),
                )
            } else if !bytes.starts_with(b"\0asm") {
                (
                    CheckStatus::Flag,
                    "missing WebAssembly magic \\0asm".to_string(),
                )
            } else {
                (
                    CheckStatus::Flag,
                    format!("WASM-4 cart size {} > 65536 bytes", bytes.len()),
                )
            }
        }
        System::Microw8 => {
            let magic_ok = bytes.starts_with(b"uw8") || bytes.starts_with(b"\0asm");
            let size_ok = bytes.len() <= 256 * 1024;
            if magic_ok && size_ok {
                (CheckStatus::Pass, "MicroW8 cart header OK".to_string())
            } else if !magic_ok {
                (CheckStatus::Flag, "missing uw8 or wasm magic".to_string())
            } else {
                (
                    CheckStatus::Flag,
                    format!("MicroW8 cart size {} > 256 KiB", bytes.len()),
                )
            }
        }
        // SPEC §5.2: any check reporting not_implemented is in the flag class —
        // systems without structural validation always reach human review.
        System::N64 | System::Ps1 | System::Nds | System::Psp | System::Dc | System::Pico8 => {
            (CheckStatus::Flag, "not_implemented".to_string())
        }
        System::Snes => {
            // No structural HDR_VALID exists for SNES (no universal header
            // magic), so it is unimplemented and must flag like the systems
            // above; copier-header handling lives in DIGESTS/DAT_MATCH.
            (
                CheckStatus::Flag,
                "not_implemented: no universal SNES header magic".to_string(),
            )
        }
    };

    CheckResult {
        id: "HDR_VALID".to_string(),
        status,
        detail,
    }
}

// ── Check 6: SIZE_SANITY ──────────────────────────────────────────────────────

fn system_size_cap(system: System) -> u64 {
    match system {
        System::Tic80 | System::Pico8 | System::Wasm4 | System::Microw8 => 1024 * 1024,
        System::Ps1 => 700 * 1024 * 1024,
        System::Dc => 1024 * 1024 * 1024,
        System::Psp => 256 * 1024 * 1024,
        _ => 64 * 1024 * 1024,
    }
}

/// SIZE_SANITY check: verify artifact size makes sense.
pub fn check_size_sanity(bytes: &[u8], metadata: &RegistryMetadata) -> CheckResult {
    let actual = bytes.len() as u64;
    let declared = metadata.artifact.size_bytes;
    let cap = system_size_cap(metadata.system);

    if actual != declared {
        return CheckResult {
            id: "SIZE_SANITY".to_string(),
            status: CheckStatus::Flag,
            detail: format!(
                "actual size {} bytes != declared {} bytes",
                actual, declared
            ),
        };
    }
    if actual > cap {
        return CheckResult {
            id: "SIZE_SANITY".to_string(),
            status: CheckStatus::Flag,
            detail: format!(
                "artifact size {} bytes exceeds cap {} bytes for system {}",
                actual, cap, metadata.system
            ),
        };
    }
    CheckResult {
        id: "SIZE_SANITY".to_string(),
        status: CheckStatus::Pass,
        detail: format!("size {} bytes OK", actual),
    }
}

// ── Check 7: DEDUP ────────────────────────────────────────────────────────────

/// DEDUP check: look for sha256 duplicates across the registry.
///
/// SPEC §5.2: any check reporting not_implemented flags for human review —
/// an unscannable registry root must never silently pass.
pub fn check_dedup(entry_dir: &Path, sha256: &str) -> CheckResult {
    // Resolve registry root: entry_dir -> system_dir -> registry_root
    let registry_root = match entry_dir.parent().and_then(|p| p.parent()) {
        Some(r) => r.to_path_buf(),
        None => {
            return CheckResult {
                id: "DEDUP".to_string(),
                status: CheckStatus::Flag,
                detail: "not_implemented: cannot resolve registry root".to_string(),
            };
        }
    };

    if !registry_root.is_dir() {
        return CheckResult {
            id: "DEDUP".to_string(),
            status: CheckStatus::Flag,
            detail: "not_implemented: registry root not a directory".to_string(),
        };
    }

    // Walk <registry>/<system>/<slug>/metadata.json
    let system_entries = match std::fs::read_dir(&registry_root) {
        Ok(e) => e,
        Err(_) => {
            return CheckResult {
                id: "DEDUP".to_string(),
                status: CheckStatus::Flag,
                detail: "not_implemented: cannot read registry root".to_string(),
            };
        }
    };

    for sys_entry in system_entries.flatten() {
        let sys_path = sys_entry.path();
        if !sys_path.is_dir() {
            continue;
        }
        let slug_entries = match std::fs::read_dir(&sys_path) {
            Ok(e) => e,
            Err(_) => continue,
        };
        for slug_entry in slug_entries.flatten() {
            let slug_path = slug_entry.path();
            if !slug_path.is_dir() {
                continue;
            }
            // Skip self
            if slug_path == entry_dir {
                continue;
            }
            let meta_path = slug_path.join("metadata.json");
            if !meta_path.exists() {
                continue;
            }
            let bytes = match std::fs::read(&meta_path) {
                Ok(b) => b,
                Err(_) => continue,
            };
            let meta: serde_json::Value = match serde_json::from_slice(&bytes) {
                Ok(v) => v,
                Err(_) => continue,
            };
            if let Some(other_sha) = meta
                .get("artifact")
                .and_then(|a| a.get("sha256"))
                .and_then(|s| s.as_str())
            {
                if other_sha.eq_ignore_ascii_case(sha256) {
                    let other_id = meta.get("id").and_then(|v| v.as_str()).unwrap_or("unknown");
                    return CheckResult {
                        id: "DEDUP".to_string(),
                        status: CheckStatus::Flag,
                        detail: format!("sha256 matches another entry: {}", other_id),
                    };
                }
            }
        }
    }

    CheckResult {
        id: "DEDUP".to_string(),
        status: CheckStatus::Pass,
        detail: "no duplicate sha256 found".to_string(),
    }
}

// ── Check 8: NAME_FUZZY ───────────────────────────────────────────────────────

fn normalize_for_fuzzy(s: &str) -> String {
    plemu_dat::normalize_name(s)
}

/// NAME_FUZZY check: fuzzy-match entry title and filename against denylist names.
///
/// A denylist query failure is a hard error, never a pass.
pub fn check_name_fuzzy(
    title: &str,
    artifact_filename: &str,
    conn: &rusqlite::Connection,
) -> Result<CheckResult, ScreenError> {
    let candidates = [
        normalize_for_fuzzy(title),
        normalize_for_fuzzy(artifact_filename),
    ];

    let mut stmt = conn
        .prepare("SELECT normalized, game_name FROM names")
        .map_err(|e| ScreenError::Denylist(format!("cannot query names table: {e}")))?;

    let rows: Vec<(String, Option<String>)> = stmt
        .query_map([], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, Option<String>>(1)?))
        })
        .map_err(|e| ScreenError::Denylist(format!("cannot read names table: {e}")))?
        .collect::<Result<Vec<_>, _>>()
        .map_err(|e| ScreenError::Denylist(format!("names row read failed: {e}")))?;

    let threshold = 0.90f64;
    let mut best_score = 0.0f64;
    let mut best_match: Option<(String, f64, String)> = None;

    for candidate in &candidates {
        if candidate.is_empty() {
            continue;
        }
        for (db_norm, game_name) in &rows {
            let score = strsim::jaro_winkler(candidate, db_norm);
            if score >= threshold && score > best_score {
                best_score = score;
                best_match = Some((
                    db_norm.clone(),
                    score,
                    game_name.clone().unwrap_or_default(),
                ));
            }
        }
    }

    Ok(if let Some((matched_norm, score, game_name)) = best_match {
        CheckResult {
            id: "NAME_FUZZY".to_string(),
            status: CheckStatus::Flag,
            detail: format!(
                "fuzzy match score {:.3} against denylist name '{}' ({})",
                score, matched_norm, game_name
            ),
        }
    } else {
        CheckResult {
            id: "NAME_FUZZY".to_string(),
            status: CheckStatus::Pass,
            detail: "no fuzzy name match above threshold".to_string(),
        }
    })
}

// ── Check 9: STR_SCAN ─────────────────────────────────────────────────────────

/// Known copyright strings to scan for.
const STR_MARKERS: &[&[u8]] = &[
    b"Nintendo",
    b"NINTENDO",
    b"(C) SEGA",
    b"SEGA ENTERPRISES",
    b"LICENSED BY",
    b"SONY COMPUTER ENTERTAINMENT",
    b"CAPCOM",
    b"KONAMI",
];

/// UTF-8 copyright prefix patterns (©19xx, ©20xx).
const UTF8_COPYRIGHT_PREFIXES: &[&[u8]] = &[
    b"\xC2\xA919", // © followed by 19
    b"\xC2\xA920", // © followed by 20
    b"(C)19",
    b"(C)20",
];

fn whitelist_ranges_for_system(system: System) -> &'static [(usize, usize)] {
    match system {
        System::Gb | System::Gbc => &[(0x104, 0x133)],
        System::Sms | System::Gg => &[(0x7FF0, 0x7FFF)],
        System::Md => &[(0x100, 0x1FF)],
        _ => &[],
    }
}

fn in_whitelist(pos: usize, ranges: &[(usize, usize)]) -> bool {
    ranges.iter().any(|&(lo, hi)| pos >= lo && pos <= hi)
}

fn scan_for_marker(bytes: &[u8], marker: &[u8], whitelist: &[(usize, usize)]) -> Vec<usize> {
    let mut hits = Vec::new();
    if marker.is_empty() || bytes.len() < marker.len() {
        return hits;
    }
    for i in 0..=(bytes.len() - marker.len()) {
        if &bytes[i..i + marker.len()] == marker && !in_whitelist(i, whitelist) {
            hits.push(i);
        }
    }
    hits
}

/// STR_SCAN check: scan for known copyright markers in the artifact.
pub fn check_str_scan(bytes: &[u8], system: System) -> CheckResult {
    let whitelist = whitelist_ranges_for_system(system);
    let mut all_hits: Vec<(String, usize)> = Vec::new();

    for &marker in STR_MARKERS {
        for pos in scan_for_marker(bytes, marker, whitelist) {
            all_hits.push((String::from_utf8_lossy(marker).into_owned(), pos));
        }
    }
    for &prefix in UTF8_COPYRIGHT_PREFIXES {
        for pos in scan_for_marker(bytes, prefix, whitelist) {
            all_hits.push((String::from_utf8_lossy(prefix).into_owned(), pos));
        }
    }

    if all_hits.is_empty() {
        return CheckResult {
            id: "STR_SCAN".to_string(),
            status: CheckStatus::Pass,
            detail: "no copyright markers found".to_string(),
        };
    }

    let shown: Vec<String> = all_hits
        .iter()
        .take(10)
        .map(|(s, pos)| format!("'{}' at 0x{:x}", s, pos))
        .collect();
    CheckResult {
        id: "STR_SCAN".to_string(),
        status: CheckStatus::Flag,
        detail: format!("{} hit(s): {}", all_hits.len(), shown.join(", ")),
    }
}

// ── Check 10: SCHEMA ──────────────────────────────────────────────────────────

/// Validate `^[a-z0-9]+(-[a-z0-9]+)*$` without a regex crate.
fn is_valid_slug(s: &str) -> bool {
    if s.is_empty() {
        return false;
    }
    for part in s.split('-') {
        if part.is_empty() {
            return false;
        }
        if !part
            .chars()
            .all(|c| c.is_ascii_lowercase() || c.is_ascii_digit())
        {
            return false;
        }
    }
    true
}

/// SCHEMA check: validate metadata structural correctness.
///
/// `computed_sha256` is the DIGESTS-computed sha256 of the artifact bytes;
/// SCHEMA is the SPEC §5.2 hard-fail carrier for a declared-vs-computed
/// digest mismatch (DIGESTS itself only flags).
pub fn check_schema(
    entry_dir: &Path,
    metadata: &RegistryMetadata,
    _attestation_bytes: &[u8],
    computed_sha256: &str,
) -> CheckResult {
    use plemu_schemas::is_core_legal_for_system;
    const SLUG_PATTERN: &str = plemu_schemas::SLUG_PATTERN;

    // Entry id must be <system>/<dirname>
    let dirname = entry_dir.file_name().and_then(|n| n.to_str()).unwrap_or("");
    let expected_id = format!("{}/{}", metadata.system, dirname);
    if metadata.id != expected_id {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!(
                "id '{}' does not match expected '{}'",
                metadata.id, expected_id
            ),
        };
    }

    // Slug validation: ^[a-z0-9]+(-[a-z0-9]+)*$
    if !is_valid_slug(dirname) {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!("slug '{}' does not match pattern {}", dirname, SLUG_PATTERN),
        };
    }

    // coreId legal for system
    if !is_core_legal_for_system(metadata.system, metadata.core_id) {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!(
                "coreId '{}' is not legal for system '{}'",
                metadata.core_id, metadata.system
            ),
        };
    }

    // sha256 must be 64 lowercase hex chars
    let sha = &metadata.artifact.sha256;
    if sha.len() != 64
        || !sha
            .chars()
            .all(|c| c.is_ascii_hexdigit() && !c.is_uppercase())
    {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!("artifact sha256 '{}' is not 64 lowercase hex chars", sha),
        };
    }

    // Declared artifact.sha256 must match the computed digest of the artifact
    // bytes (hard-fail lives here per SPEC §5.2; DIGESTS only flags).
    if !sha.eq_ignore_ascii_case(computed_sha256) {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!(
                "artifact.sha256 '{}' does not match computed sha256 '{}' of artifact bytes",
                sha, computed_sha256
            ),
        };
    }

    // Anti-replay: the top-level metadata.sha256 (the field the signed §3
    // attestation message is rebuilt from) must equal artifact.sha256.
    // Without this pin a submitter could replay a creator's existing public
    // attestation (original sha256 in metadata.sha256 + message) while
    // attaching arbitrary new artifact bytes whose artifact.sha256 matches
    // the computed digest — passing both ATTESTATION and the check above.
    // Together the two comparisons enforce
    // metadata.sha256 == artifact.sha256 == computed digest.
    if !metadata.sha256.eq_ignore_ascii_case(sha) {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!(
                "metadata.sha256 '{}' does not match artifact.sha256 '{}' (attestation replay guard)",
                metadata.sha256, sha
            ),
        };
    }

    // artifact file must exist
    let artifact_path = entry_dir.join(&metadata.artifact.filename);
    if !artifact_path.exists() {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: format!("artifact file '{}' not found", metadata.artifact.filename),
        };
    }

    // attestation.json parses
    let attest_path = entry_dir.join("attestation.json");
    if !attest_path.exists() {
        return CheckResult {
            id: "SCHEMA".to_string(),
            status: CheckStatus::Fail,
            detail: "attestation.json missing".to_string(),
        };
    }
    match std::fs::read(&attest_path) {
        Ok(b) => {
            if let Err(e) = serde_json::from_slice::<RegistryAttestation>(&b) {
                return CheckResult {
                    id: "SCHEMA".to_string(),
                    status: CheckStatus::Fail,
                    detail: format!("attestation.json parse error: {e}"),
                };
            }
        }
        Err(e) => {
            return CheckResult {
                id: "SCHEMA".to_string(),
                status: CheckStatus::Fail,
                detail: format!("cannot read attestation.json: {e}"),
            };
        }
    }

    CheckResult {
        id: "SCHEMA".to_string(),
        status: CheckStatus::Pass,
        detail: "schema valid".to_string(),
    }
}

// ── Check 11: EVIDENCE_URL ────────────────────────────────────────────────────

/// EVIDENCE_URL check: validate license evidence URL.
pub fn check_evidence_url(evidence_url: Option<&str>) -> CheckResult {
    match evidence_url {
        None | Some("") => CheckResult {
            id: "EVIDENCE_URL".to_string(),
            status: CheckStatus::Flag,
            detail: "no evidence URL provided".to_string(),
        },
        Some(url) => {
            let valid = url.starts_with("http://") || url.starts_with("https://");
            if valid {
                CheckResult {
                    id: "EVIDENCE_URL".to_string(),
                    status: CheckStatus::Pass,
                    detail: "offline: liveness not checked".to_string(),
                }
            } else {
                CheckResult {
                    id: "EVIDENCE_URL".to_string(),
                    status: CheckStatus::Flag,
                    detail: format!("evidence URL '{}' is not an absolute http(s) URL", url),
                }
            }
        }
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use ed25519_dalek::Signer;
    use plemu_schemas::{AttestationRole, CoreId, LicenseType, System};
    use plemu_schemas::{
        RegistryArtifact, RegistryAttestation, RegistryCreator, RegistryLicense, RegistryMetadata,
        RegistrySubmission,
    };
    use std::io::Write;
    use tempfile::TempDir;

    fn make_test_artifact(dir: &Path, content: &[u8], filename: &str) -> String {
        let path = dir.join(filename);
        std::fs::write(&path, content).unwrap();
        hex::encode(Sha256::digest(content))
    }

    fn make_signing_key() -> ed25519_dalek::SigningKey {
        ed25519_dalek::SigningKey::from_bytes(&[42u8; 32])
    }

    fn wallet_from_key(key: &ed25519_dalek::SigningKey) -> String {
        bs58::encode(key.verifying_key().as_bytes()).into_string()
    }

    fn sign_message(key: &ed25519_dalek::SigningKey, message: &str) -> String {
        let sig = key.sign(message.as_bytes());
        base64::engine::general_purpose::STANDARD.encode(sig.to_bytes())
    }

    #[test]
    fn digest_check_pass() {
        let dir = TempDir::new().unwrap();
        let content = b"hello world";
        let sha = make_test_artifact(dir.path(), content, "game.gb");
        let result = check_digests(&dir.path().join("game.gb"), &sha).unwrap();
        assert!(result.ok);
    }

    #[test]
    fn digest_check_fail() {
        let dir = TempDir::new().unwrap();
        std::fs::write(dir.path().join("game.gb"), b"hello world").unwrap();
        let result = check_digests(
            &dir.path().join("game.gb"),
            "0000000000000000000000000000000000000000000000000000000000000000",
        )
        .unwrap();
        assert!(!result.ok);
    }

    fn make_metadata(
        title: &str,
        system: System,
        sha256: &str,
        license: LicenseType,
        wallet: &str,
        filename: &str,
        submitted_at: &str,
    ) -> RegistryMetadata {
        RegistryMetadata {
            id: "gb/test-game".to_string(),
            title: title.to_string(),
            system,
            core_id: CoreId::Sameboy,
            min_core_version: "0.1.0".to_string(),
            sha256: sha256.to_string(),
            size_bytes: 100,
            creator: RegistryCreator {
                wallet: wallet.to_string(),
                display_name: "Tester".to_string(),
            },
            license: RegistryLicense {
                license_type: license,
                evidence_url: None,
            },
            submission: RegistrySubmission {
                via: "pr".to_string(),
                submitted_at: submitted_at.to_string(),
            },
            artifact: RegistryArtifact {
                sha256: sha256.to_string(),
                size_bytes: 100,
                filename: filename.to_string(),
            },
        }
    }

    #[test]
    fn attestation_check_happy_path() {
        let key = make_signing_key();
        let wallet = wallet_from_key(&key);
        let now_str = "2026-07-12T12:00:00Z";
        let signed_at = "2026-07-12T10:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &wallet,
            "game.gb",
            "2026-07-12T11:00:00Z",
        );

        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &wallet,
            timestamp: signed_at,
        });

        let signature = sign_message(&key, &message);

        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message: message.clone(),
            signature,
            signed_at: signed_at.to_string(),
            wallet: wallet.clone(),
        };

        let now = DateTime::parse_from_rfc3339(now_str)
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(result.ok, "Expected ok, got: {:?}", result.message);
    }

    #[test]
    fn attestation_check_bad_signature() {
        let key = make_signing_key();
        let wallet = wallet_from_key(&key);
        let now_str = "2026-07-12T12:00:00Z";
        let signed_at = "2026-07-12T10:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &wallet,
            "game.gb",
            "2026-07-12T11:00:00Z",
        );

        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &wallet,
            timestamp: signed_at,
        });

        let bad_key = ed25519_dalek::SigningKey::from_bytes(&[99u8; 32]);
        let bad_signature = sign_message(&bad_key, &message);

        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message: message.clone(),
            signature: bad_signature,
            signed_at: signed_at.to_string(),
            wallet: wallet.clone(),
        };

        let now = DateTime::parse_from_rfc3339(now_str)
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(!result.ok);
    }

    #[test]
    fn attestation_future_timestamp_fails() {
        let key = make_signing_key();
        let wallet = wallet_from_key(&key);
        let signed_at = "2026-07-14T12:00:00Z";
        let now_str = "2026-07-12T12:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &wallet,
            "game.gb",
            "2026-07-14T13:00:00Z",
        );

        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &wallet,
            timestamp: signed_at,
        });

        let signature = sign_message(&key, &message);

        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message: message.clone(),
            signature,
            signed_at: signed_at.to_string(),
            wallet: wallet.clone(),
        };

        let now = DateTime::parse_from_rfc3339(now_str)
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(!result.ok);
    }

    #[test]
    fn archive_safety_non_zip_passes() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("game.gb");
        std::fs::write(&path, b"\x00\x01\x02\x03some rom data").unwrap();
        let result = check_archive_safety(&path, System::Gb).unwrap();
        assert!(result.ok);
        assert!(!result.checked);
    }

    fn make_simple_zip(
        dir: &Path,
        filename: &str,
        entries: &[(&str, &[u8])],
    ) -> std::path::PathBuf {
        let path = dir.join(filename);
        let file = std::fs::File::create(&path).unwrap();
        let mut zip = zip::ZipWriter::new(file);
        let options = zip::write::SimpleFileOptions::default()
            .compression_method(zip::CompressionMethod::Stored);
        for (name, content) in entries {
            zip.start_file(*name, options).unwrap();
            zip.write_all(content).unwrap();
        }
        zip.finish().unwrap();
        path
    }

    #[test]
    fn archive_safety_valid_zip_for_ps1_passes() {
        let dir = TempDir::new().unwrap();
        let path = make_simple_zip(dir.path(), "game.zip", &[("game.bin", b"discdata")]);
        let result = check_archive_safety(&path, System::Ps1).unwrap();
        assert!(result.ok, "Expected ok, got: {:?}", result.message);
        assert!(result.checked);
    }

    #[test]
    fn archive_safety_clean_zip_for_non_disc_system_fails() {
        // The exact zip that passes for ps1 must fail for gb: zip artifacts
        // are permitted only for disc systems (ps1/dc).
        let dir = TempDir::new().unwrap();
        let path = make_simple_zip(dir.path(), "game.zip", &[("game.bin", b"discdata")]);
        let result = check_archive_safety(&path, System::Gb).unwrap();
        assert!(!result.ok, "zip for gb must fail");
        assert!(result.checked);
        let msg = result.message.unwrap_or_default();
        assert!(
            msg.contains("not allowed for system 'gb'"),
            "detail should name the system rule, got: {msg}"
        );
    }

    #[test]
    fn archive_safety_path_traversal_fails() {
        let dir = TempDir::new().unwrap();
        let path = make_simple_zip(dir.path(), "bad.zip", &[("../evil.sh", b"evil")]);
        let result = check_archive_safety(&path, System::Ps1).unwrap();
        assert!(!result.ok);
    }

    #[test]
    fn archive_safety_deep_nesting_fails() {
        let dir = TempDir::new().unwrap();
        let path = make_simple_zip(dir.path(), "deep.zip", &[("subdir/game.rom", b"romdata")]);
        let result = check_archive_safety(&path, System::Ps1).unwrap();
        assert!(!result.ok);
    }

    // ── ATTESTATION wallet cross-check ────────────────────────────────────────

    #[test]
    fn attestation_wallet_mismatch_with_creator_fails() {
        // Wallet X signs a perfectly valid attestation, but the entry declares
        // creator wallet Y — must fail naming the mismatch.
        let signing_key = make_signing_key(); // wallet X
        let wallet_x = wallet_from_key(&signing_key);
        let other_key = ed25519_dalek::SigningKey::from_bytes(&[7u8; 32]);
        let wallet_y = wallet_from_key(&other_key); // declared creator

        let signed_at = "2026-07-12T10:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        // metadata.creator.wallet = Y
        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &wallet_y,
            "game.gb",
            "2026-07-12T11:00:00Z",
        );

        // Message + signature built consistently with wallet X — valid on its own.
        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &wallet_x,
            timestamp: signed_at,
        });
        let signature = sign_message(&signing_key, &message);
        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message,
            signature,
            signed_at: signed_at.to_string(),
            wallet: wallet_x.clone(),
        };

        let now = DateTime::parse_from_rfc3339("2026-07-12T12:00:00Z")
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(!result.ok, "signing wallet != creator wallet must fail");
        let msg = result.message.unwrap_or_default();
        assert!(
            msg.contains("does not match metadata.creator.wallet"),
            "detail should name the mismatch, got: {msg}"
        );
    }

    // ── DAT_MATCH headerless (Lynx) ───────────────────────────────────────────

    fn lynx_headered(payload: &[u8]) -> Vec<u8> {
        // 64-byte header starting with the LYNX\0 magic, then the payload.
        let mut file = vec![0u8; 64];
        file[..5].copy_from_slice(b"LYNX\0");
        file.extend_from_slice(payload);
        file
    }

    fn denylist_with(algo: &str, hex: &str) -> rusqlite::Connection {
        plemu_dat::db::build_denylist_in_memory(
            &[plemu_dat::DatRecord {
                algo: algo.to_string(),
                hex: hex.to_string(),
                source: Some("test-dat".to_string()),
                rom_name: Some("Commercial Game.lnx".to_string()),
                game_name: Some("Commercial Game".to_string()),
                system: Some("lynx".to_string()),
            }],
            &[],
        )
        .unwrap()
    }

    #[test]
    fn dat_match_lynx_headerless_sha256_fails_even_with_other_claimed_system() {
        let payload = b"commercial lynx payload bytes for sha256 case";
        let file = lynx_headered(payload);
        let payload_digests = compute_all_digests(payload);
        let conn = denylist_with("sha256", &payload_digests.sha256);

        // Entry claims gb, not lynx: magic-based headerless matching must
        // still catch it.
        let result = check_dat_match(&file, System::Gb, &conn).unwrap();
        assert_eq!(
            result.status,
            CheckStatus::Fail,
            "detail: {}",
            result.detail
        );
        assert!(
            result.detail.contains("headerless lynx"),
            "{}",
            result.detail
        );
    }

    #[test]
    fn dat_match_lynx_headerless_crc32_fails() {
        let payload = b"commercial lynx payload bytes for crc32 case";
        let file = lynx_headered(payload);
        let payload_digests = compute_all_digests(payload);
        let conn = denylist_with("crc32", &payload_digests.crc32);

        let result = check_dat_match(&file, System::Wasm4, &conn).unwrap();
        assert_eq!(
            result.status,
            CheckStatus::Fail,
            "detail: {}",
            result.detail
        );
        assert!(result.detail.contains("crc32"), "{}", result.detail);
        assert!(
            result.detail.contains("headerless lynx"),
            "{}",
            result.detail
        );
    }

    // ── HDR_VALID: TIC-80 chunk stream ────────────────────────────────────────

    #[test]
    fn hdr_valid_tic80_chunk_stream_passes() {
        // Chunk 1: type 0x05 (code), size 4, 4 data bytes.
        // Chunk 2: type 0x11, size 0 (final zero-size chunk is allowed).
        let mut cart = Vec::new();
        cart.push(0x05);
        cart.extend_from_slice(&4u16.to_le_bytes());
        cart.extend_from_slice(b"code");
        cart.push(0x11);
        cart.extend_from_slice(&0u16.to_le_bytes());

        let result = check_hdr_valid(&cart, System::Tic80);
        assert_eq!(
            result.status,
            CheckStatus::Pass,
            "detail: {}",
            result.detail
        );
    }

    #[test]
    fn hdr_valid_tic80_truncated_stream_flags() {
        // Chunk declares 10 data bytes but only 4 follow.
        let mut cart = Vec::new();
        cart.push(0x05);
        cart.extend_from_slice(&10u16.to_le_bytes());
        cart.extend_from_slice(b"code");

        let result = check_hdr_valid(&cart, System::Tic80);
        assert_eq!(
            result.status,
            CheckStatus::Flag,
            "detail: {}",
            result.detail
        );

        // Trailing bytes too short for a chunk header also flag.
        let stub = vec![0x05, 0x00];
        let result2 = check_hdr_valid(&stub, System::Tic80);
        assert_eq!(
            result2.status,
            CheckStatus::Flag,
            "detail: {}",
            result2.detail
        );
    }

    // ── Fix round 2: not_implemented ⇒ Flag ───────────────────────────────────

    #[test]
    fn hdr_valid_unimplemented_systems_flag_not_implemented() {
        let systems = [
            System::N64,
            System::Ps1,
            System::Nds,
            System::Psp,
            System::Dc,
            System::Pico8,
            System::Snes,
        ];
        for sys in systems {
            let result = check_hdr_valid(b"arbitrary artifact bytes", sys);
            assert_eq!(
                result.status,
                CheckStatus::Flag,
                "{:?} must flag, got {:?} ({})",
                sys,
                result.status,
                result.detail
            );
            assert!(
                result.detail.contains("not_implemented"),
                "{:?} detail must contain not_implemented: {}",
                sys,
                result.detail
            );
        }
    }

    #[test]
    fn dedup_unresolvable_registry_root_flags_not_implemented() {
        // Single-component path: parent().parent() is unresolvable.
        let result = check_dedup(Path::new("slug-only"), "aa".repeat(32).as_str());
        assert_eq!(result.status, CheckStatus::Flag, "{}", result.detail);
        assert!(
            result.detail.contains("not_implemented"),
            "{}",
            result.detail
        );

        // Resolvable but nonexistent registry root.
        let result2 = check_dedup(
            Path::new("/definitely/not/a/real/root/gb/some-slug"),
            "aa".repeat(32).as_str(),
        );
        assert_eq!(result2.status, CheckStatus::Flag, "{}", result2.detail);
        assert!(
            result2.detail.contains("not_implemented"),
            "{}",
            result2.detail
        );
    }

    // ── Fix round 2: attestation length paths without unwrap ─────────────────

    #[test]
    fn attestation_short_wallet_fails_cleanly() {
        // 16-byte "wallet": passes the creator-wallet equality and message
        // rebuild, then must fail at pubkey conversion — no panic.
        let short_wallet = bs58::encode([1u8; 16]).into_string();
        let signed_at = "2026-07-12T10:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &short_wallet,
            "game.gb",
            "2026-07-12T11:00:00Z",
        );
        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &short_wallet,
            timestamp: signed_at,
        });
        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message,
            signature: base64::engine::general_purpose::STANDARD.encode([0u8; 64]),
            signed_at: signed_at.to_string(),
            wallet: short_wallet,
        };

        let now = DateTime::parse_from_rfc3339("2026-07-12T12:00:00Z")
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(!result.ok);
        let msg = result.message.unwrap_or_default();
        assert!(
            msg.contains("wallet pubkey must be 32 bytes, got 16"),
            "{msg}"
        );
    }

    #[test]
    fn attestation_short_signature_fails_cleanly() {
        // Valid 32-byte wallet, but a 10-byte signature: must fail at the
        // signature conversion — no panic.
        let key = make_signing_key();
        let wallet = wallet_from_key(&key);
        let signed_at = "2026-07-12T10:00:00Z";
        let sha256 = "70b85a30034102176c6c4f450ac3cd38ab526edb6db374fd8d94c9ea768948dd";

        let metadata = make_metadata(
            "Test Game",
            System::Gb,
            sha256,
            LicenseType::Cc0,
            &wallet,
            "game.gb",
            "2026-07-12T11:00:00Z",
        );
        let message = build_attestation_message(&AttestationParams {
            role: AttestationRole::Creator,
            title: "Test Game",
            system: System::Gb,
            sha256,
            license: LicenseType::Cc0,
            wallet: &wallet,
            timestamp: signed_at,
        });
        let attestation = RegistryAttestation {
            role: AttestationRole::Creator,
            message,
            signature: base64::engine::general_purpose::STANDARD.encode([0u8; 10]),
            signed_at: signed_at.to_string(),
            wallet,
        };

        let now = DateTime::parse_from_rfc3339("2026-07-12T12:00:00Z")
            .unwrap()
            .with_timezone(&Utc);
        let result = check_attestation(&metadata, &attestation, now).unwrap();
        assert!(!result.ok);
        let msg = result.message.unwrap_or_default();
        assert!(msg.contains("signature must be 64 bytes, got 10"), "{msg}");
    }
}
