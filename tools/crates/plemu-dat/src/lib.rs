//! plemu-dat: No-Intro/Redump XML DAT parser and SQLite denylist builder.

pub mod db;
pub mod parser;

pub use db::{build_denylist, DenylistMeta};
pub use parser::{parse_dat_dir, parse_dat_str, DatRecord, NameRecord};

/// Normalise a ROM/game name: lowercase, strip region/version tags in (…)
/// and […], collapse punctuation/whitespace to single ASCII spaces, trim.
pub fn normalize_name(name: &str) -> String {
    // Remove (...) and [...] groups
    let mut s = String::with_capacity(name.len());
    let mut depth_paren = 0u32;
    let mut depth_bracket = 0u32;
    for c in name.chars() {
        match c {
            '(' => {
                depth_paren += 1;
            }
            ')' => {
                depth_paren = depth_paren.saturating_sub(1);
            }
            '[' => {
                depth_bracket += 1;
            }
            ']' => {
                depth_bracket = depth_bracket.saturating_sub(1);
            }
            _ if depth_paren == 0 && depth_bracket == 0 => s.push(c),
            _ => {}
        }
    }
    // Lowercase
    let s = s.to_lowercase();
    // Collapse runs of whitespace/punctuation (except apostrophe which is word-internal)
    let mut result = String::new();
    let mut prev_space = true; // treat leading chars as after space to trim leading
    for c in s.chars() {
        if c.is_alphanumeric() || c == '\'' {
            result.push(c);
            prev_space = false;
        } else if !prev_space {
            result.push(' ');
            prev_space = true;
        }
    }
    // Trim trailing space
    result.trim_end().to_string()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn normalize_strips_regions() {
        assert_eq!(
            normalize_name("Super Mario Land (World)"),
            "super mario land"
        );
        assert_eq!(
            normalize_name("Kirby's Dream Land (USA, Europe)"),
            "kirby's dream land"
        );
        assert_eq!(normalize_name("Sonic [v1.0]"), "sonic");
    }

    #[test]
    fn normalize_collapses_spaces() {
        assert_eq!(normalize_name("  Foo   Bar  "), "foo bar");
    }
}
