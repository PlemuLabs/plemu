#!/usr/bin/env python3
"""
audit_wallet_security.py — Automated security audit for Plemu Mobile Wallet Adapter (MWA 2.0 / Seed Vault).

Audits source code, Android shared_prefs, and logcat captures to verify:
1. No volatile auth_tokens or private keys are written to persistent storage (PlayerPrefs, files).
2. No sensitive credentials are exposed in Android logcat output.
3. Strict fail-closed memory security compliance for Solana Seeker.
"""

import argparse
import os
import re
import sys
from pathlib import Path

# Sensitive patterns that must NEVER appear in logs or persistent disk files
SENSITIVE_PATTERNS = [
    re.compile(r"auth_token\s*[:=]\s*[\"']?([a-zA-Z0-9_-]{20,})", re.IGNORECASE),
    re.compile(r"private[_-]?key\s*[:=]\s*[\"']?([a-zA-Z0-9]{32,})", re.IGNORECASE),
    re.compile(r"secret[_-]?key\s*[:=]\s*[\"']?([a-zA-Z0-9]{32,})", re.IGNORECASE),
    re.compile(r"seed_vault_seed\s*[:=]", re.IGNORECASE),
    re.compile(r"mock_secret_token_[0-9]+"),
]

# Insecure persistence APIs forbidden in the Wallet runtime namespace
FORBIDDEN_PERSISTENCE_CALLS = [
    "PlayerPrefs.SetString",
    "PlayerPrefs.SetInt",
    "PlayerPrefs.SetFloat",
    "PlayerPrefs.Save",
    "File.WriteAllText",
    "File.WriteAllBytes",
    "File.OpenWrite",
    "StreamWriter",
    "BinaryWriter",
]


def audit_source_code(wallet_dir: Path) -> list:
    """Audit C# source files for unauthorized persistence or token logging."""
    findings = []
    if not wallet_dir.exists():
        findings.append(f"Directory not found: {wallet_dir}")
        return findings

    for root, _, files in os.walk(wallet_dir):
        for file in files:
            if not file.endswith(".cs"):
                continue
            path = Path(root) / file
            # Allow mock classes in Mock/ subdirectory to simulate mocks safely
            is_mock = "Mock" in path.parts

            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read()

            for call in FORBIDDEN_PERSISTENCE_CALLS:
                if call in content:
                    findings.append(f"[FORBIDDEN PERSISTENCE] {path.name}: contains '{call}'")

            # Check for direct Debug.Log of tokens
            if "Debug.Log" in content and ("_authToken" in content or "CurrentAuthToken" in content):
                if re.search(r"Debug\.Log.*(_authToken|CurrentAuthToken)", content):
                    findings.append(f"[CREDENTIAL LOGGING] {path.name}: potential Debug.Log of auth_token")

    return findings


def audit_shared_prefs(shared_prefs_path: Path) -> list:
    """Audit Android shared_prefs XML files to assert absence of auth_token or keys."""
    findings = []
    if not shared_prefs_path.exists():
        return findings

    files_to_check = []
    if shared_prefs_path.is_file():
        files_to_check.append(shared_prefs_path)
    else:
        files_to_check.extend(shared_prefs_path.glob("*.xml"))

    for file in files_to_check:
        with open(file, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()

        for pattern in SENSITIVE_PATTERNS:
            match = pattern.search(content)
            if match:
                findings.append(f"[PERSISTENCE LEAK] {file.name}: matches sensitive pattern '{pattern.pattern}'")

        if "auth_token" in content.lower():
            findings.append(f"[PERSISTENCE LEAK] {file.name}: contains 'auth_token' key/value")

    return findings


def audit_logcat(logcat_path: Path) -> list:
    """Audit logcat capture file for leaked tokens or unhandled exceptions."""
    findings = []
    if not logcat_path.exists():
        findings.append(f"Logcat file not found: {logcat_path}")
        return findings

    with open(logcat_path, "r", encoding="utf-8", errors="ignore") as f:
        for line_no, line in enumerate(f, 1):
            for pattern in SENSITIVE_PATTERNS:
                if pattern.search(line):
                    findings.append(f"[LOGCAT LEAK L{line_no}]: {line.strip()[:100]}")

            if "Exception in thread" in line or "AndroidJavaException" in line:
                findings.append(f"[UNHANDLED JNI EXCEPTION L{line_no}]: {line.strip()[:100]}")

    return findings


def main():
    parser = argparse.ArgumentParser(description="Audit MWA wallet security invariants.")
    parser.add_argument("--wallet-dir", type=str, default="apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet",
                        help="Path to Plemu.Wallet C# source directory")
    parser.add_argument("--shared-prefs", type=str, default=None,
                        help="Path to shared_prefs XML file or directory to audit")
    parser.add_argument("--logcat", type=str, default=None,
                        help="Path to logcat capture file to audit")

    args = parser.parse_args()

    # Locate repo root relative to this script
    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parents[1]

    wallet_path = repo_root / args.wallet_dir if not Path(args.wallet_dir).is_absolute() else Path(args.wallet_dir)

    print("=== Plemu MWA Security & Fail-Closed Audit ===")
    print(f"Auditing source directory: {wallet_path}")

    all_findings = []

    # 1. Source code audit
    src_findings = audit_source_code(wallet_path)
    if src_findings:
        all_findings.extend(src_findings)
        print(f"[-] Source code audit FAILED ({len(src_findings)} issue(s) detected):")
        for finding in src_findings:
            print(f"    - {finding}")
    else:
        print("[+] Source code audit PASSED: No forbidden persistence or credential logging found.")

    # 2. Shared preferences audit
    if args.shared_prefs:
        prefs_path = Path(args.shared_prefs)
        prefs_findings = audit_shared_prefs(prefs_path)
        if prefs_findings:
            all_findings.extend(prefs_findings)
            print(f"[-] Shared preferences audit FAILED ({len(prefs_findings)} issue(s) detected):")
            for finding in prefs_findings:
                print(f"    - {finding}")
        else:
            print(f"[+] Shared preferences audit PASSED for {prefs_path}: zero credential persistence.")

    # 3. Logcat audit
    if args.logcat:
        logcat_path = Path(args.logcat)
        logcat_findings = audit_logcat(logcat_path)
        if logcat_findings:
            all_findings.extend(logcat_findings)
            print(f"[-] Logcat audit FAILED ({len(logcat_findings)} issue(s) detected):")
            for finding in logcat_findings:
                print(f"    - {finding}")
        else:
            print(f"[+] Logcat audit PASSED for {logcat_path}: zero credential leaks detected.")

    print("\n=== Audit Summary ===")
    if all_findings:
        print(f"FAILED: {len(all_findings)} security invariant violation(s) detected.")
        sys.exit(1)
    else:
        print("PASSED: All MWA fail-closed security invariants verified successfully.")
        sys.exit(0)


if __name__ == "__main__":
    main()
