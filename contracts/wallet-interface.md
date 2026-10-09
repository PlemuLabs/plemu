# Seam A↔C: Wallet and Commerce Interface Contract

**Owner:** Stream C (Chain & Commerce)  
**Consumer:** Stream A (Runtime & Client)  
**Status:** FROZEN (Phase 1 Contracts Day)  
**Implementation:** [`Plemu.Wallet`](file:///apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/)  
**Mocks:** [`Plemu.Wallet.Mock`](file:///apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/Mock/)

---

## 1. Overview

This contract decouples device-side UI and emulation runtime (Stream A) from on-chain Solana programs (Stream C). Stream A builds against `IWallet` and `ISeatProvider` interfaces, testing offline and online flows via `MockWallet` and `MockSeatProvider`.

Entitlements are verified **at manifest acquisition**, never at launch. Therefore, offline play remains 100% functional once content is cached locally.

---

## 2. Interface Definitions

### `IWallet`
Handles user connection and transaction signing. Amounts are always in **PLEMU BASE UNITS** (`ulong`), never raw lamports or decimals.

```csharp
namespace Plemu.Wallet
{
    public interface IWallet
    {
        bool IsConnected { get; }
        string PublicKey { get; } // base58 public key, null when disconnected
        Task<bool> Connect();
        Task<string> SendTip(string recipientWallet, ulong amountBaseUnits);
        Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits);
        Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits);
    }
}
```

### `ISeatProvider`
Checks whether the active wallet holds access to a private pack.

```csharp
namespace Plemu.Wallet
{
    public interface ISeatProvider
    {
        Task<bool> HasSeat(string packId);
        Task<IReadOnlyList<string>> MyPacks();
    }
}
```

### `IMobileWalletAdapter`
Low-level transport protocol contract for Mobile Wallet Adapter (MWA 2.0 / Seed Vault). Decoupled from game business logic.

```csharp
namespace Plemu.Wallet
{
    public interface IMobileWalletAdapter
    {
        bool IsMwaAvailable();
        Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default);
        Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
        Task DisconnectAsync();
    }
}
```

#### MWA Result Models
Typed result DTOs providing fail-safe error and cancellation propagation without unhandled exceptions on Unity's main thread:

```csharp
namespace Plemu.Wallet
{
    public sealed class MwaConnectionResult
    {
        public bool IsSuccess { get; }
        public string PublicKey { get; }
        public string AuthToken { get; }
        public string ErrorMessage { get; }
        public bool IsCancelled { get; }

        public static MwaConnectionResult Ok(string publicKey, string authToken);
        public static MwaConnectionResult Failed(string errorMessage);
        public static MwaConnectionResult Cancelled(string reason = "User cancelled wallet association.");
    }

    public sealed class MwaTransactionResult
    {
        public bool IsSuccess { get; }
        public string Signature { get; }
        public string ErrorMessage { get; }
        public bool IsCancelled { get; }

        public static MwaTransactionResult Ok(string signature);
        public static MwaTransactionResult Failed(string errorMessage);
        public static MwaTransactionResult Cancelled(string reason = "User cancelled transaction signing.");
    }
}
```

### `MwaWallet` (Facade)
High-level commercial wallet facade implementing `IWallet` via `IMobileWalletAdapter`.
- **Fail-Closed Volatile Session:** `auth_token` and `public_key` reside exclusively in transient memory; never written to `PlayerPrefs`, logs, or disk. Immediately cleared on `Disconnect()`.
- **Commercial Flows:** Mounts payload and delegates to `SignAndSendTransactionAsync()` for `SendTip`, `PurchaseSeat`, and `PurchaseListing`.

```csharp
namespace Plemu.Wallet
{
    public sealed class MwaWallet : IWallet
    {
        public MwaWallet(IMobileWalletAdapter adapter);
        public bool IsConnected { get; }
        public string PublicKey { get; }
        public Task<bool> Connect();
        public void Disconnect();
        public Task DisconnectAsync();
        public Task<string> SendTip(string recipientWallet, ulong amountBaseUnits);
        public Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits);
        public Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits);
    }
}
```

### `WalletFactory`
Platform resolution factory ensuring seamless execution across Android hardware and Unity Editor:

```csharp
namespace Plemu.Wallet
{
    public static class WalletFactory
    {
        public static IWallet CreateWallet();
        public static IWallet CreateWallet(IMobileWalletAdapter explicitAdapter);
        public static IWallet CreateEditorWallet();
        public static IWallet CreateAndroidWallet(IAndroidJniBridge bridge = null);
        public static IWallet ResolveWallet(bool isAndroidPlatform, IAndroidJniBridge bridge = null);
    }
}
```

- **Android Device (`#if UNITY_ANDROID && !UNITY_EDITOR`):** Instantiates `AndroidMwaAdapter` via direct JNI (`solana-wallet://` intents). If MWA/Seed Vault is detected via `PackageManager`, returns `MwaWallet`; if unavailable, gracefully falls back to `MockWallet`.
- **Unity Editor / Desktop:** Instantiates `MwaWallet` with `MockMwaAdapter` (or `MockWallet`), providing instant iteration without physical hardware.

---

## 3. Mock Specifications

### `MockWallet`
- Returns deterministic mock transaction signatures: `MockSig-{kind}-1111111111111111111111111111`.
- `PublicKey`: `"MockWa11etDoNotUse11111111111111111111111111"`.

### `MockMwaAdapter`
- Implements `IMobileWalletAdapter` deterministically for testing and Editor play.
- Returns deterministic signatures with prefix `MwaSig-devnet-` derived reproducibly from payload content.
- Supports configurable simulation of success, user cancellation, failures, and timeouts.

### `MockSeatProvider`
- Backed by JSON at `Application.persistentDataPath/mock-seats.json`.
- Schema:
  ```json
  {
    "packs": ["speedrun-club", "translation-club"]
  }
  ```
- If the file is missing or empty, `HasSeat()` returns `false`, simulating the HTTP 402 Payment Required / No-Seat response from the Cloudflare Worker gate.
