# Runbook: Solana Seeker Hardware Homologation & MWA 2.0 Security Audit

**Audience:** Mobile QA Engineers, Security Auditors, and Device Release Engineers.  
**Scope:** Verification of Mobile Wallet Adapter (MWA 2.0 / Seed Vault) on physical **Solana Seeker** hardware (Android 14+, ARM64-v8a) or Seed Vault emulator.  
**Target Package:** `com.playsolana.games.plemu`  
**Network Cluster:** Solana Devnet (`https://api.devnet.solana.com`)  

---

## 1. Overview & Architectural Invariants

The Plemu mobile client (`apps/device/unity`) integrates with the Solana Seeker's hardware-isolated **Seed Vault** via a pure C# JNI bridge (`AndroidMwaAdapter`) and high-level commercial facade (`MwaWallet`).

Before production release, the following architectural invariants must be physically verified:
1. **Hardware-Isolated Biometrics:** Key custody and transaction signing are performed exclusively inside the Seed Vault secure environment via fingerprint authorization.
2. **Package Visibility Compliance:** Android 11 to 14+ `<queries>` filtering correctly discovers the `solana-wallet://` intent filter without false negatives.
3. **Fail-Closed Volatile Session:** All session authorization tokens (`auth_token`) and cryptographic keys reside exclusively in volatile runtime memory and are completely purged upon disconnect or termination.
4. **Resilient Frame Rate:** User cancellations, timeouts, or biometric mismatches return typed failure DTOs (`MwaConnectionResult` / `MwaTransactionResult`) without throwing unhandled exceptions or causing frame rate drops (steady 60 FPS).

---

## 2. Prerequisites & Device Setup

### Hardware & Environment
- **Device:** Solana Seeker (or Android 14+ device with Seed Vault Simulator / Phantom / Solflare).
- **Host Tools:**
  - Android SDK Platform-Tools (`adb` v34+).
  - Python 3.10+ (for security audit script).
  - Unity 6000.3.19f1 with Android Build Support & IL2CPP.

### Seeker Configuration
1. Enable **Developer Options** and **USB Debugging** on the Solana Seeker.
2. Ensure Seed Vault is initialized with a biometric fingerprint enrolled.
3. Ensure a Solana Devnet account exists in Seed Vault with at least 0.5 Devnet SOL:
   ```bash
   solana airdrop 2 <SEEKER_WALLET_PUBKEY> --url devnet
   ```
4. Connect the Seeker to the workstation via USB and verify ADB authorization:
   ```bash
   adb devices
   # Expected: <device_serial>   device
   ```

---

## 3. Building the ARM64 IL2CPP APK

Execute the batchmode build script to compile the native shim, SameBoy core, and Unity IL2CPP ARM64 binary:

```bash
# 1. Stage native ARM64 plugins (shim + core)
cd apps/device/unity
bash stage-android.sh

# 2. Compile APK via Unity Batchmode (or via Unity Editor: File -> Build Settings -> Build)
Unity -batchmode -quit -projectPath . -buildTarget Android \
  -executeMethod Plemu.Editor.BuildScript.BuildPSG1 \
  -logFile build.log
```

The compiled APK will be output to:
`apps/device/build/android/Plemu.apk`

---

## 4. Pre-Flight Verification: Package Visibility

Confirm that Android 14's package visibility filter detects the Seed Vault handler for the `solana-wallet` scheme:

```bash
adb shell pm query-intent-activities -a android.intent.action.VIEW -d "solana-wallet://v1/associate"
```

**Expected Result:**
The output lists the Seed Vault activity (e.g., `com.solanamobile.seedvault` or compatible wallet). If empty, inspect `Assets/Plugins/Android/AndroidManifest.xml` to ensure the `<queries>` element is present.

---

## 5. APK Sideloading

Install the compiled build onto the Seeker:

```bash
adb install -r -d apps/device/build/android/Plemu.apk
```

---

## 6. Step-by-Step HITL Validation Protocol

Follow each test scenario and record test results in the verification log.

### Scenario A: Wallet Connection & Seed Vault Biometric Authorization
- **Objective:** Verify US-1, US-2, US-3.
- **Steps:**
  1. Launch **Plemu** on the Seeker.
  2. Navigate to the marketplace or profile screen.
  3. Tap **"Connect Wallet"**.
- **Expected Behavior:**
  - The Seed Vault authorization dialog opens smoothly without hitching the main thread.
  - The dialog displays dApp identity: **"Plemu"**, URI: `https://plemu.gg`, and Cluster: `devnet`.
  - Scan fingerprint when prompted.
  - The Seed Vault authorizes the connection and dismisses.
  - Plemu UI updates to show the connected state with the user's Base58 public key.

### Scenario B: Commercial License Purchase (`buy_license`) on Devnet
- **Objective:** Verify US-5, US-6, US-7.
- **Steps:**
  1. In Plemu, select a commercial game entry (e.g., Pack ID or Entry ID).
  2. Tap **"Purchase License"** (calls `MwaWallet.PurchaseListing` or `PurchaseSeat`).
- **Expected Behavior:**
  - Seed Vault transaction signing prompt appears.
  - Confirm transaction details and verify biometric prompt.
  - Provide fingerprint.
  - Transaction is signed and submitted to Solana Devnet.
  - UI displays the transaction signature (tx hash).
- **On-Chain Verification:**
  - Open Solana Explorer in a browser:
    `https://explorer.solana.com/tx/<TX_SIGNATURE>?cluster=devnet`
  - Verify that transaction status is **Success** (Finalized).

### Scenario C: Creator Tipping (`SendTip`) on Devnet
- **Objective:** Verify US-6, US-7.
- **Steps:**
  1. Tap **"Send Tip"** on a creator's card.
  2. Input tip amount in PLEMU base units.
  3. Approve through Seed Vault fingerprint scan.
- **Expected Behavior:**
  - Transaction successfully executes and returns signature.
  - Explorer confirms transfer on Devnet.

### Scenario D: User Cancellation & Error Recovery
- **Objective:** Verify US-4, US-8.
- **Steps:**
  1. Tap **"Connect Wallet"** or initiate a purchase.
  2. When the Seed Vault sheet appears, tap **Cancel** or press Android **Back**.
- **Expected Behavior:**
  - Prompt dismisses immediately.
  - Plemu returns smoothly to the previous screen at a locked 60 FPS.
  - UI displays a graceful message: `"User cancelled authorization"` or `"User cancelled signing"`.
  - No uncaught `AndroidJavaException` or crash in Unity logs.

### Scenario E: Session Termination & Fail-Closed Memory Purge
- **Objective:** Verify US-9, US-10, US-11.
- **Steps:**
  1. Tap **"Disconnect Wallet"** in Plemu.
- **Expected Behavior:**
  - `MwaWallet.Disconnect()` executes immediately.
  - UI resets to disconnected state.
  - `PublicKey` and `_authToken` fields are set to null in volatile memory.

---

## 7. Security & Memory Leakage Audit Protocol

### 1. Live Logcat Inspection
During the execution of all scenarios above, capture the device log:

```bash
adb logcat -c
adb logcat | grep -iE "plemu|wallet|mwa|seedvault" > seeker_session.log
```

Search `seeker_session.log` for any leakage of authorization tokens or secret keys:
```bash
grep -iE "auth_token|mwa-token|private_key|secret" seeker_session.log
```
**Passing Rule:** The token must only ever appear as `[REDACTED]` or in external Seed Vault system logs; Plemu application logs must NEVER output the plain token value.

### 2. Disk Persistence Audit (Shared Preferences)
Inspect the application's local storage to ensure no tokens were written to disk:

```bash
adb shell "run-as com.playsolana.games.plemu ls -la shared_prefs/"
adb shell "run-as com.playsolana.games.plemu cat shared_prefs/com.playsolana.games.plemu.v2.playerprefs.xml"
```
**Passing Rule:** `auth_token` and private key strings must NOT exist in `shared_prefs`, SQLite databases, or local app cache files.

### 3. Automated Security Verification Script
Run the automated security audit tool against the source code, logcat output, and shared preferences dump:

```bash
python tools/scripts/audit_wallet_security.py \
  --logcat seeker_session.log \
  --shared-prefs com.playsolana.games.plemu.v2.playerprefs.xml
```

**Passing Rule:** The script exits with code 0 and reports `PASSED: All MWA fail-closed security invariants verified successfully.`

---

## 8. Homologation Sign-Off Checklist

| Item | Requirement | Verification Method | Status |
| :--- | :--- | :--- | :---: |
| **H1** | Seed Vault package visibility confirmed on Android 14 | `adb shell pm query-intent-activities` | [ ] |
| **H2** | Wallet connection with fingerprint authorization on Seeker | Scenario A | [ ] |
| **H3** | License purchase transaction confirmed on Solana Devnet | Scenario B + Explorer | [ ] |
| **H4** | Creator tipping transaction confirmed on Solana Devnet | Scenario C + Explorer | [ ] |
| **H5** | User cancellation returns cleanly without 60 FPS frame drop | Scenario D | [ ] |
| **H6** | Disconnect immediately wipes in-memory tokens (fail-closed) | Scenario E | [ ] |
| **H7** | Zero credential or token leakage in logcat | Logcat audit | [ ] |
| **H8** | Zero persistence of credentials in `shared_prefs` or disk | Disk inspection | [ ] |
| **H9** | Automated security audit script passes with 0 findings | `audit_wallet_security.py` | [ ] |
