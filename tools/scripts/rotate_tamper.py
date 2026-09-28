"""Rebuild the tampered fixture: the clean manifest with exactly one byte changed.

It must fail verification against the clean signature - that is the whole point
of the fixture, and both the Rust and C# verifiers assert it.
"""
import sys

TAMPER_FROM = '"sizeBytes": 37'
TAMPER_TO = '"sizeBytes": 38'


def main() -> None:
    src, dst = sys.argv[1:3]
    with open(src, "r", encoding="utf-8") as handle:
        text = handle.read()
    if TAMPER_FROM not in text:
        raise SystemExit(
            f"fixture shape changed: {TAMPER_FROM!r} not found in {src}. "
            "Update the tamper site in this script."
        )
    with open(dst, "w", encoding="utf-8") as handle:
        handle.write(text.replace(TAMPER_FROM, TAMPER_TO, 1))


if __name__ == "__main__":
    main()
