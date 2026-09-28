"""Replace old dev key material with new, across every text file in the tree.

A sweep rather than a file list: the old public key appears in Rust test
assertions, C# constants, JSON registries, prose and fixtures, and enumerating
those by hand is how one gets missed.

spec/SPEC.md is never touched - it is the frozen contract. Signed artifacts
(*.sig) and binaries are skipped because they are regenerated, not edited.
"""
import os
import sys

SKIP_DIRS = {
    ".git", ".claude", ".memsearch", ".superpowers", "target", "Library",
    "Temp", "obj", ".utmp", "build", ".cache", "node_modules", "UserSettings",
}
SKIP_SUFFIXES = (".sig", ".gb", ".wasm", ".png", ".so", ".dylib", ".meta")


def main() -> None:
    root, old_a, new_a, old_b, new_b, old_w, new_w = sys.argv[1:8]
    frozen = os.path.join(root, "spec", "SPEC.md")
    pairs = [(old_a, new_a), (old_b, new_b), (old_w, new_w)]
    changed = []

    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            path = os.path.join(dirpath, name)
            if path == frozen or name.endswith(SKIP_SUFFIXES):
                continue
            try:
                with open(path, "r", encoding="utf-8") as handle:
                    text = handle.read()
            except (UnicodeDecodeError, OSError):
                continue
            updated = text
            for old, new in pairs:
                updated = updated.replace(old, new)
            if updated != text:
                with open(path, "w", encoding="utf-8") as handle:
                    handle.write(updated)
                changed.append(os.path.relpath(path, root))

    for rel in sorted(changed):
        print("      " + rel)


if __name__ == "__main__":
    main()
