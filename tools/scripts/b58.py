"""Base58 (Bitcoin alphabet) encode a hex string — the Solana wallet encoding."""
import sys

ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"


def b58encode(raw: bytes) -> str:
    n = int.from_bytes(raw, "big")
    out = ""
    while n:
        n, rem = divmod(n, 58)
        out = ALPHABET[rem] + out
    # leading zero bytes become leading '1's
    return "1" * (len(raw) - len(raw.lstrip(b"\0"))) + out


if __name__ == "__main__":
    print(b58encode(bytes.fromhex(sys.argv[1])))
