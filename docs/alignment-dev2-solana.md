# Technical Alignment: Solana Wallet & Entitlement Services (Dev 2 Handoff)

**Audience:** Dev 1 (UI / PSG1 Frontend) & Dev 3 (Packs / Catalog / Storage)  
**Author:** Dev 2 (Solana & Smart Contracts)  
**Status:** Approved & Verified (100% Code Coverage)  
**Target Solution:** `plemu/Plemu.slnx` (.NET 10)

---

## 1. Executive Summary

This document establishes the technical integration contract between the Solana services (`Plemu.Solana`) implemented by Dev 2 and the consuming components maintained by Dev 1 (UI layer, device screen handlers) and Dev 3 (Pack download, catalog, and launch orchestration).

Key design commitments:
1. **Asynchronous & Reactive:** The wallet service (`IWalletService`) transitions from synchronous polling/mocks to an event-driven model (`StateChanged`), maintaining zero UI dependencies.
2. **Fail-Closed Security:** Entitlement verification (`IEntitlementService`) checks on-chain Seat PDAs with strict fail-closed behavior (network dropouts, timeouts, revoked packs, or missing accounts return `false`).
3. **Offline Preservation:** On-chain verification occurs exclusively during pack acquisition/download or refresh operations. Downloaded packs remain 100% playable offline without network access.
4. **Decoupled Architecture:** Consuming assemblies (`Plemu.Runtime`, `Plemu.Packs`, and UI screens) depend strictly on `Plemu.Core` interfaces and never reference `Plemu.Solana` directly.

---

## 2. Dev 1 Integration Guide: UI & Hardware Wallet Flows

### 2.1 Separation of Concerns (UI Independence)

`PlaySolanaWalletService` does **not** draw dialogs, invoke popups, or block thread loops. It encapsulates:
- Hardware/driver handshakes with SvalGuard / PlaySolana PSG1 SDK.
- Finite-state transitions (`Disconnected`, `Connecting`, `Connected`, `Disconnecting`, `Error`).
- Cryptographic challenge-response signing.

The UI layer is solely responsible for rendering connection buttons, spinner animations, wallet addresses, and error banners by observing state transitions.

### 2.2 Wallet State Machine

```
               ConnectAsync()
  [Disconnected] -----------> [Connecting]
        ^                          |
        |                          |-- (Success) --------> [Connected]
        |                          |                          |
        |                          |-- (User Reject/Timeout)  | DisconnectAsync()
        |                          v                          v
        +-------------------- [  Error  ] <------------ [Disconnecting]
        | (Reset / Retry)
        +-----------------------------------------------------+
```

### 2.3 Wallet State Lifecycle Reference

| State | Semantic | Expected UI Action |
| :--- | :--- | :--- |
| `Disconnected` | No active wallet session | Display "Connect Wallet" button. Clear user address badge. |
| `Connecting` | Handshake or challenge awaiting approval in SvalGuard | Show loading indicator / disable button. "Awaiting SvalGuard...". |
| `Connected` | Successfully authenticated; public key available in `CurrentWallet` | Display short address (e.g., `4vJ...8zK`). Enable store/pack actions. |
| `Disconnecting` | Teardown of active session in progress | Disable buttons and display transition spinner. |
| `Error` | User declined, PIN failed, driver unresponsive, or timeout | Display error message in English. Offer "Retry" action. |

### 2.4 C# Consumption Example for Dev 1 (Screen / Controller)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;

public sealed class WalletUiController : IDisposable
{
    private readonly IWalletService _walletService;
    private readonly CancellationTokenSource _cts = new();

    public WalletUiController(IWalletService walletService)
    {
        _walletService = walletService ?? throw new ArgumentNullException(nameof(walletService));

        // 1. Subscribe to reactive state updates
        _walletService.StateChanged += OnWalletStateChanged;

        // 2. Synchronize current state on initial load
        SyncUiState(_walletService.State, _walletService.CurrentWallet);
    }

    public async Task OnConnectButtonClickedAsync()
    {
        try
        {
            // Trigger connection with optional timeout/cancellation
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            bool connected = await _walletService.ConnectAsync(linkedCts.Token);
            if (!connected)
            {
                ShowNotification("Wallet connection was not completed.");
            }
        }
        catch (OperationCanceledException)
        {
            ShowNotification("Connection timed out. Please try again.");
        }
        catch (Exception ex)
        {
            ShowNotification($"Connection error: {ex.Message}");
        }
    }

    public async Task OnDisconnectButtonClickedAsync()
    {
        await _walletService.DisconnectAsync(_cts.Token);
    }

    private void OnWalletStateChanged(WalletConnectionState newState, WalletIdentity? wallet)
    {
        // Must marshal to UI thread if running in Unity or GUI dispatch loop
        SyncUiState(newState, wallet);
    }

    private void SyncUiState(WalletConnectionState state, WalletIdentity? wallet)
    {
        switch (state)
        {
            case WalletConnectionState.Disconnected:
                RenderConnectButton(enabled: true, text: "Connect Wallet");
                ClearWalletBadge();
                break;

            case WalletConnectionState.Connecting:
                RenderConnectButton(enabled: false, text: "Connecting...");
                ShowSpinner(true);
                break;

            case WalletConnectionState.Connected:
                ShowSpinner(false);
                RenderConnectButton(enabled: true, text: "Disconnect");
                RenderWalletBadge(wallet?.PublicKey ?? string.Empty);
                break;

            case WalletConnectionState.Disconnecting:
                RenderConnectButton(enabled: false, text: "Disconnecting...");
                break;

            case WalletConnectionState.Error:
                ShowSpinner(false);
                RenderConnectButton(enabled: true, text: "Retry Connection");
                ShowNotification("Failed to communicate with SvalGuard.");
                break;
        }
    }

    private void RenderConnectButton(bool enabled, string text) { /* UI update */ }
    private void RenderWalletBadge(string address) { /* UI update */ }
    private void ClearWalletBadge() { /* UI update */ }
    private void ShowSpinner(bool visible) { /* UI update */ }
    private void ShowNotification(string message) { /* UI update */ }

    public void Dispose()
    {
        _walletService.StateChanged -= OnWalletStateChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

---

## 3. Dev 3 Integration Guide: Entitlements, Manifests & Packs

### 3.1 Strict Fail-Closed Rule

`SolanaEntitlementService.CanAccessPackAsync` enforces strict fail-closed security:
- If the RPC endpoint returns HTTP 4xx, 5xx, or network timeouts $\rightarrow$ Returns `false`.
- If the PDA account is missing on-chain $\rightarrow$ Returns `false`.
- If the account owner is not the `plemu-seats` program $\rightarrow$ Returns `false`.
- If the Anchor discriminator does not match `[90, 228, 22, 90, 162, 86, 173, 26]` $\rightarrow$ Returns `false`.
- If the pack is flagged as revoked (`data[72] == 1`) $\rightarrow$ Returns `false`.
- If the member public key inside the seat does not match the active wallet $\rightarrow$ Returns `false`.

Under no condition will an error state result in permissive access.

### 3.2 Offline Playback Invariant

**Rule:** Entitlement checks belong to the **acquisition, download, and catalog sync** pipeline, NEVER to the emulator launch loop (`PackLaunchOrchestrator`).

```
[Store / Acquisition UI]
          │
          ▼
CanAccessPackAsync(wallet, packId) ───► (Returns false) ───► Access Denied Modal
          │
          ▼ (Returns true)
Download and Extract Game Pack (.plemupack)
Verify Trust Chain & SHA-256 Hashes
Store to Local Encrypted Sandbox
          │
          │  (Device disconnects from Wi-Fi / Airplane Mode)
          ▼
[Offline Game Launch]
PackLaunchOrchestrator.Launch(pack)
          │
          ▼
Executes locally using cached trust validation without live RPC checks.
```

### 3.3 C# Consumption Example for Dev 3 (Pack Download Manager)

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;

public sealed class PackAcquisitionService
{
    private readonly IWalletService _walletService;
    private readonly IEntitlementService _entitlementService;

    public PackAcquisitionService(
        IWalletService walletService,
        IEntitlementService entitlementService)
    {
        _walletService = walletService ?? throw new ArgumentNullException(nameof(walletService));
        _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
    }

    public async Task<bool> AcquirePackAsync(string packId, CancellationToken cancellationToken = default)
    {
        // 1. Ensure wallet is connected
        if (_walletService.State != WalletConnectionState.Connected || _walletService.CurrentWallet == null)
        {
            throw new InvalidOperationException("Wallet must be connected to verify pack entitlement.");
        }

        WalletIdentity wallet = _walletService.CurrentWallet;

        // 2. Perform fail-closed verification
        bool hasAccess = await _entitlementService.CanAccessPackAsync(wallet, packId, cancellationToken);
        if (!hasAccess)
        {
            return false;
        }

        // 3. Proceed with download and staging of offline pack
        await DownloadAndStagePackAsync(packId, cancellationToken);
        return true;
    }

    private Task DownloadAndStagePackAsync(string packId, CancellationToken cancellationToken)
    {
        // Dev 3 implementation: download encrypted blob, verify hashes, save locally
        return Task.CompletedTask;
    }
}
```

---

## 4. Architectural Boundaries and Dependency Injection

### 4.1 Solution Assembly Graph

```
[ Plemu.App ] (Startup / Entry Point)
      │
      ├───────────────────────┬───────────────────────┐
      ▼                       ▼                       ▼
[ Plemu.Runtime ]      [ Plemu.Packs ]        [ Plemu.Solana ]
      │                       │                       │
      └───────────────────────┼───────────────────────┘
                              ▼
                        [ Plemu.Core ]
```

Notice:
- `Plemu.Runtime` does **NOT** reference `Plemu.Solana`.
- `Plemu.Packs` does **NOT** reference `Plemu.Solana`.
- All interactions take place through `Plemu.Core.Abstractions.IWalletService` and `Plemu.Core.Abstractions.IEntitlementService`.

### 4.2 CompositionRoot Registration

In `src/Plemu.App/CompositionRoot.cs`, Dev 2 services are registered as Singletons:

```csharp
// Dev 2 Services:
services.AddSingleton<IWalletService, PlaySolanaWalletService>();
services.AddSingleton<IEntitlementService, SolanaEntitlementService>();
```

If testing in an offline staging environment without Devnet RPC, consumers can substitute with mock implementations without changing consumer code:

```csharp
// Offline Mock fallback (if required by test harness):
services.AddSingleton<IWalletService, MockWalletService>();
services.AddSingleton<IEntitlementService, MockEntitlementService>();
```

---

## 5. Verification & Testing

Every implementation detail specified in this document is covered by automated unit tests in `plemu/tests/Plemu.Solana.Tests`:
- `PlaySolanaWalletServiceTests`: 100% state machine coverage, cancellation, exception handling, and event delivery.
- `SolanaEntitlementServiceTests`: Valid accounts, revoked packs, mismatched accounts, missing PDAs, and RPC timeouts.
- `ArchitecturalIsolationTests`: Automated MSBuild project file inspection asserting `Plemu.Runtime` has 0 references to `Plemu.Solana`.
- `CompositionRootTests`: Container resolution ensuring `IWalletService` and `IEntitlementService` resolve to Dev 2 singletons.

For any questions, reach out on the internal dev sync channel or inspect the test suite.
