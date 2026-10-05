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

---

## 3. Mock Specifications

### `MockWallet`
- Returns deterministic mock transaction signatures: `MockSig-{kind}-1111111111111111111111111111`.
- `PublicKey`: `"MockWa11etDoNotUse11111111111111111111111111"`.

### `MockSeatProvider`
- Backed by JSON at `Application.persistentDataPath/mock-seats.json`.
- Schema:
  ```json
  {
    "packs": ["speedrun-club", "translation-club"]
  }
  ```
- If the file is missing or empty, `HasSeat()` returns `false`, simulating the HTTP 402 Payment Required / No-Seat response from the Cloudflare Worker gate.
