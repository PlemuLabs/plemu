//! No-Intro/Redump XML DAT parser.

use crate::normalize_name;
use anyhow::{Context, Result};
use quick_xml::events::Event;
use quick_xml::reader::Reader;
use std::path::Path;

/// A single hash record extracted from a DAT file.
#[derive(Debug, Clone)]
pub struct DatRecord {
    pub algo: String,
    pub hex: String,
    pub source: Option<String>,
    pub rom_name: Option<String>,
    pub game_name: Option<String>,
    pub system: Option<String>,
}

/// A normalized name record for fuzzy matching.
#[derive(Debug, Clone)]
pub struct NameRecord {
    pub normalized: String,
    pub game_name: Option<String>,
    pub system: Option<String>,
}

/// Parse all .dat and .xml files in `dir`, returning
/// (hash_records, name_records, dat_file_count).
///
/// `source_tag` is stored as the `source` column in hash records.
pub fn parse_dat_dir(
    dir: &Path,
    source_tag: Option<&str>,
) -> Result<(Vec<DatRecord>, Vec<NameRecord>, u64)> {
    let mut records = Vec::new();
    let mut names = Vec::new();
    let mut dat_files: u64 = 0;

    let entries =
        std::fs::read_dir(dir).with_context(|| format!("reading DAT dir {}", dir.display()))?;

    for entry in entries {
        let entry = entry?;
        let path = entry.path();
        let ext = path.extension().and_then(|e| e.to_str()).unwrap_or("");
        if ext.eq_ignore_ascii_case("dat") || ext.eq_ignore_ascii_case("xml") {
            let (mut r, mut n) = parse_dat_file(&path, source_tag)
                .with_context(|| format!("parsing {}", path.display()))?;
            records.append(&mut r);
            names.append(&mut n);
            dat_files += 1;
        }
    }

    Ok((records, names, dat_files))
}

/// Parse a single DAT/XML file.
pub fn parse_dat_file(
    path: &Path,
    source_tag: Option<&str>,
) -> Result<(Vec<DatRecord>, Vec<NameRecord>)> {
    let content =
        std::fs::read_to_string(path).with_context(|| format!("reading {}", path.display()))?;
    parse_dat_str(&content, source_tag)
}

/// Parse DAT XML content from a string.
pub fn parse_dat_str(
    content: &str,
    source_tag: Option<&str>,
) -> Result<(Vec<DatRecord>, Vec<NameRecord>)> {
    let mut reader = Reader::from_str(content);
    reader.config_mut().trim_text(true);

    let mut records: Vec<DatRecord> = Vec::new();
    let mut names: Vec<NameRecord> = Vec::new();

    // State for current game element
    let mut current_game: Option<String> = None;
    let mut current_game_rom_count: usize = 0;
    // Pending name records to emit only after we confirm there are roms
    let mut pending_name: Option<NameRecord> = None;

    loop {
        match reader.read_event() {
            Ok(Event::Start(e)) => {
                let tag_str = std::str::from_utf8(e.name().as_ref())
                    .unwrap_or("")
                    .to_owned();

                if tag_str == "game" || tag_str == "machine" {
                    // Start of a game/machine element
                    let name_attr = e
                        .attributes()
                        .flatten()
                        .find(|a| a.key.as_ref() == b"name")
                        .and_then(|a| String::from_utf8(a.value.to_vec()).ok());

                    current_game = name_attr.clone();
                    current_game_rom_count = 0;

                    // Prepare a pending name record
                    if let Some(gname) = &name_attr {
                        let normalized = normalize_name(gname);
                        if !normalized.is_empty() {
                            pending_name = Some(NameRecord {
                                normalized,
                                game_name: Some(gname.clone()),
                                system: source_tag.map(str::to_owned),
                            });
                        }
                    }
                } else if tag_str == "rom" {
                    if let Some(ref gname) = current_game {
                        let mut rom_name: Option<String> = None;
                        let mut crc: Option<String> = None;
                        let mut md5: Option<String> = None;
                        let mut sha1: Option<String> = None;

                        for attr in e.attributes().flatten() {
                            let key = std::str::from_utf8(attr.key.as_ref()).unwrap_or("");
                            let val = String::from_utf8(attr.value.to_vec()).unwrap_or_default();
                            match key {
                                "name" => rom_name = Some(val),
                                "crc" => crc = Some(val.to_lowercase()),
                                "md5" => md5 = Some(val.to_lowercase()),
                                "sha1" => sha1 = Some(val.to_lowercase()),
                                _ => {}
                            }
                        }

                        let gname_clone = gname.clone();
                        let rname_clone = rom_name.clone();
                        let src = source_tag.map(str::to_owned);

                        let push = |algo: &str, hex: String, records: &mut Vec<DatRecord>| {
                            records.push(DatRecord {
                                algo: algo.to_string(),
                                hex,
                                source: src.clone(),
                                rom_name: rname_clone.clone(),
                                game_name: Some(gname_clone.clone()),
                                system: src.clone(),
                            });
                        };

                        if let Some(h) = crc {
                            push("crc32", h, &mut records);
                        }
                        if let Some(h) = md5 {
                            push("md5", h, &mut records);
                        }
                        if let Some(h) = sha1 {
                            push("sha1", h, &mut records);
                        }

                        current_game_rom_count += 1;
                    }
                }
            }
            Ok(Event::Empty(e)) => {
                let tag_str = std::str::from_utf8(e.name().as_ref())
                    .unwrap_or("")
                    .to_owned();

                if tag_str == "game" || tag_str == "machine" {
                    // Self-closing game element with no children → malformed, skip
                    let name_attr = e
                        .attributes()
                        .flatten()
                        .find(|a| a.key.as_ref() == b"name")
                        .and_then(|a| String::from_utf8(a.value.to_vec()).ok());
                    eprintln!(
                        "Warning: game element with no children skipped: {:?}",
                        name_attr
                    );
                } else if tag_str == "rom" {
                    // Self-closing <rom .../> inside a game element
                    if let Some(ref gname) = current_game {
                        let mut rom_name: Option<String> = None;
                        let mut crc: Option<String> = None;
                        let mut md5: Option<String> = None;
                        let mut sha1: Option<String> = None;

                        for attr in e.attributes().flatten() {
                            let key = std::str::from_utf8(attr.key.as_ref()).unwrap_or("");
                            let val = String::from_utf8(attr.value.to_vec()).unwrap_or_default();
                            match key {
                                "name" => rom_name = Some(val),
                                "crc" => crc = Some(val.to_lowercase()),
                                "md5" => md5 = Some(val.to_lowercase()),
                                "sha1" => sha1 = Some(val.to_lowercase()),
                                _ => {}
                            }
                        }

                        let gname_clone = gname.clone();
                        let rname_clone = rom_name.clone();
                        let src = source_tag.map(str::to_owned);

                        let push = |algo: &str, hex: String, records: &mut Vec<DatRecord>| {
                            records.push(DatRecord {
                                algo: algo.to_string(),
                                hex,
                                source: src.clone(),
                                rom_name: rname_clone.clone(),
                                game_name: Some(gname_clone.clone()),
                                system: src.clone(),
                            });
                        };

                        if let Some(h) = crc {
                            push("crc32", h, &mut records);
                        }
                        if let Some(h) = md5 {
                            push("md5", h, &mut records);
                        }
                        if let Some(h) = sha1 {
                            push("sha1", h, &mut records);
                        }

                        current_game_rom_count += 1;
                    }
                }
            }
            Ok(Event::End(e)) => {
                let name_bytes = e.name();
                let tag_str = std::str::from_utf8(name_bytes.as_ref()).unwrap_or("");
                if tag_str == "game" || tag_str == "machine" {
                    if current_game_rom_count == 0 {
                        // No rom children → malformed game, skip name record
                        if let Some(ref gname) = current_game {
                            eprintln!("Warning: game '{}' has no rom children, skipping", gname);
                        }
                        pending_name = None;
                    } else if let Some(name_rec) = pending_name.take() {
                        names.push(name_rec);
                    }
                    current_game = None;
                    current_game_rom_count = 0;
                }
            }
            Ok(Event::Eof) => break,
            Err(e) => {
                eprintln!("Warning: XML parse error: {e}");
                break;
            }
            _ => {}
        }
    }

    Ok((records, names))
}

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE_DAT: &str = r#"<?xml version="1.0"?>
<datafile>
  <game name="Super Mario Land (World)">
    <rom name="Super Mario Land (World).gb" size="65536" crc="0e7ce1cb" md5="4e1a2be0f328ac2bfb5e9ab03c7a1fcf" sha1="7c15643afd0f2d16ccdc6e20e37a57d5c2ba12a4"/>
  </game>
  <game name="Kirby's Dream Land (USA, Europe)">
    <rom name="Kirby's Dream Land (USA, Europe).gb" size="131072" crc="f7d86d95" sha1="01f04dd450fb8a5c4da04804b58e23a2ecddc9cb"/>
  </game>
  <game name="Malformed Game">
  </game>
</datafile>"#;

    #[test]
    fn parses_hashes() {
        let (records, _) = parse_dat_str(SAMPLE_DAT, Some("gb")).unwrap();
        // Mario: crc32, md5, sha1 = 3; Kirby: crc32, sha1 = 2
        assert_eq!(records.len(), 5);
        let crc_records: Vec<_> = records.iter().filter(|r| r.algo == "crc32").collect();
        assert_eq!(crc_records.len(), 2);
    }

    #[test]
    fn parses_names_skips_malformed() {
        let (_, names) = parse_dat_str(SAMPLE_DAT, Some("gb")).unwrap();
        // Only 2 games have roms
        assert_eq!(names.len(), 2);
        let normalized: Vec<_> = names.iter().map(|n| n.normalized.as_str()).collect();
        assert!(normalized.contains(&"super mario land"));
        assert!(normalized.contains(&"kirby's dream land"));
    }

    #[test]
    fn source_tag_propagated() {
        let (records, names) = parse_dat_str(SAMPLE_DAT, Some("nointro-gb")).unwrap();
        assert!(records
            .iter()
            .all(|r| r.source.as_deref() == Some("nointro-gb")));
        assert!(names
            .iter()
            .all(|n| n.system.as_deref() == Some("nointro-gb")));
    }
}
