"""Keep metadata.json's inline attestation in sync with the standalone file.

plemu-manifest build reads the inline copy; plemu-screen verifies the standalone
one. They are maintained separately and drift silently, so this writes one from
the other.
"""
import json
import sys

FIELDS = ("role", "message", "signature", "signedAt")


def main() -> None:
    metadata_path, attestation_path = sys.argv[1:3]
    with open(metadata_path, encoding="utf-8") as handle:
        metadata = json.load(handle)
    with open(attestation_path, encoding="utf-8") as handle:
        attestation = json.load(handle)

    metadata["attestation"] = {field: attestation[field] for field in FIELDS}

    with open(metadata_path, "w", encoding="utf-8") as handle:
        json.dump(metadata, handle, indent=2, ensure_ascii=False)
        handle.write("\n")


if __name__ == "__main__":
    main()
