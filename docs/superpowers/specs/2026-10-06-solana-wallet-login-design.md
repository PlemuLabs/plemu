# Plemu Web — Solana Wallet Adapter & Authentication Subsystem Design Spec

**Document Date:** 2026-10-06  
**Status:** Approved / Ready for Implementation  
**Target:** `apps/web` (Next.js 16 / React 19)  
**Related Docs:** `docs/superpowers/specs/2026-10-05-monetization-design.md`, `services/worker/src/index.ts`, `services/supabase/migrations/20260930000000_init_moderation_and_dmca.sql`

---

## 1. Executive Summary

This specification defines the architecture, security guarantees, visual styling, and implementation plan for the **Solana Wallet Connection and Authentication Subsystem** in Plemu Web (`apps/web`).

The system enables players and creators to connect non-custodial Solana wallets (Phantom, Solflare, Backpack, etc.), view balances (SOL and USDC on Devnet), manage sessions safely across navigation, and lays the cryptographic foundation for Sign-In With Solana (SIWS) authentication compatible with Plemu's Cloudflare Worker gate and Supabase backend.

---

## 2. Critical Analysis & Addressed Vulnerabilities

During rigorous architectural review, six potential failure modes were identified and mitigated:

| # | Vulnerability / Pitfall | Root Cause | Architectural Mitigation in this Spec |
| :--- | :--- | :--- | :--- |
| **1** | **Top-Layer Modal Occlusion** | Native HTML5 `<dialog>` elements used in `page.tsx` enter browser Top Layer, trapping focus and occluding standard z-index divs. | Explicitly close all open native dialogs prior to opening wallet modal; render `.wallet-adapter-modal` with viewport-fixed `z-index: 999999` and custom retro pixel-art theme. |
| **2** | **Devnet RPC Rate-Limiting (HTTP 429)** | Public Solana Devnet RPC (`api.devnet.solana.com`) enforces aggressive IP throttling. Uncontrolled polling causes cascaded fetch failures. | No interval polling. Balances are fetched strictly upon `connect` and `publicKey` change. Manual refresh is debounced ($\ge 5\text{s}$) and wrapped in fail-closed try/catch. |
| **3** | **Uninitialized USDC ATA Error** | New Devnet wallets have no Associated Token Account (ATA) for Devnet USDC (`4zMMC...`), throwing RPC `TokenAccountNotFoundError`. | Safely query parsed token accounts by owner/mint; missing ATA gracefully resolves to `0 USDC` without raising console errors or UI crash. |
| **4** | **State Bleed on Account Switching** | Extension popups allow hot-switching accounts or disconnecting without app trigger. | Re-evaluate session immediately upon `publicKey` change or `disconnect` event; purge cached balances and active session state. |
| **5** | **SIWS Nonce Format Inconsistency** | Cloudflare Worker `/challenge` expects exact UTF-8 template: `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`. | `signChallenge` utility adheres strictly to Worker message format and signs raw byte buffers using `window.solana.signMessage` / Wallet Standard. |
| **6** | **Next.js 16 SSR Hydration Mismatch** | Wallet adapter reads `localStorage` on initial render, generating different HTML than Node.js SSR. | Wrap wallet controls in SSR-safe `mounted` guards and load header controls with `ssr: false` dynamic imports where appropriate. |

---

## 3. Architecture & Component Hierarchy

```
apps/web/app/
├── layout.tsx                     <-- Injects SolanaWalletProvider around children
├── globals.css                    <-- Imports wallet-adapter-retro.css
├── providers/
│   └── SolanaWalletProvider.tsx   <-- "use client": ConnectionProvider + WalletProvider + WalletModalProvider
├── components/
│   ├── WalletHeaderButton.tsx     <-- "use client": Retro styled connection pill / connect trigger
│   └── wallet-adapter-retro.css   <-- Pixel-art overrides for @solana/wallet-adapter-react-ui
├── hooks/
│   └── usePlemuAuth.ts            <-- Unified authentication, balance, and SIWS hook
├── lib/
│   ├── solana-config.ts           <-- RPC endpoints, USDC mints, and cluster constants
│   └── solana-utils.ts            <-- Safe balance queries, ATA derivation, address formatting
```

### 3.1 Dependencies
```json
{
  "dependencies": {
    "@solana/web3.js": "^1.98.0",
    "@solana/wallet-adapter-base": "^0.9.23",
    "@solana/wallet-adapter-react": "^0.15.35",
    "@solana/wallet-adapter-react-ui": "^0.9.35"
  }
}
```
*Note:* `@solana/wallet-adapter-wallets` is intentionally omitted because modern wallets (Phantom, Solflare, Backpack) comply with the **Solana Wallet Standard**, which is detected automatically by `@solana/wallet-adapter-react`, minimizing bundle size and React 19 dependency conflicts.

---

## 4. Configuration & Environment Variables (`solana-config.ts`)

```typescript
export const SOLANA_CONFIG = {
  network: (process.env.NEXT_PUBLIC_SOLANA_NETWORK as 'devnet' | 'mainnet-beta') || 'devnet',
  rpcUrl:
    process.env.NEXT_PUBLIC_SOLANA_RPC_URL ||
    'https://api.devnet.solana.com',
  usdcMint:
    process.env.NEXT_PUBLIC_USDC_MINT ||
    '4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU', // Official Devnet USDC
  mainnetUsdcMint: 'EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v',
};
```

---

## 5. Hook Specification: `usePlemuAuth`

```typescript
export interface PlemuAuthState {
  // Connection State
  isConnected: boolean;
  isConnecting: boolean;
  walletName: string | null;
  publicKey: PublicKey | null;
  base58Address: string | null;
  shortAddress: string | null;

  // Balances
  lamports: bigint | null;
  solBalance: number | null;
  usdcUnits: bigint | null;
  usdcBalanceFormatted: string | null;
  isLoadingBalance: boolean;

  // Actions
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  refreshBalances: () => Promise<void>;
  signChallenge: (message: string) => Promise<Uint8Array | null>;
}
```

### Key Behavioral Rules:
1. **Short Address Formatting:** Formats public keys as `First4...Last4` (e.g., `Dos7...jcHa`).
2. **Safe Balance Fetching:**
   - SOL: `connection.getBalance(pubkey)` -> converts to SOL formatted to 4 decimals.
   - USDC: Derives ATA via `getAssociatedTokenAddressSync(usdcMint, pubkey)` -> queries parsed balance. If `TokenAccountNotFoundError` or null, returns `0n`.
3. **Throttled Refresh:** `refreshBalances` ignores calls made within 5 seconds of the previous call unless forced.
4. **Sign Challenge (SIWS Phase 2 Preparation):**
   - Encodes text with UTF-8 `new TextEncoder().encode(message)`.
   - Calls `wallet.adapter.signMessage(messageBytes)`.
   - Catches user rejection without throwing unhandled promises, returning `null`.

---

## 6. UI & Styling: Retro Pixel-Art Integration

The default `@solana/wallet-adapter-react-ui` appearance (purple gradient Web3 look) is overridden via `wallet-adapter-retro.css` to match Plemu's CRT/pixel aesthetic:

- **Typography:** Monospace pixel font (`Courier New`, `ui-monospace`, or local pixel typography).
- **Colors:**
  - Background: Dark console obsidian `#0b0c14` / `#161726`.
  - Accents: Mint `#4ef0c0`, Neon Purple `#a78bfa`, Amber `#f59e0b`.
  - Borders: 2px solid with sharp edges (`border-radius: 0px` or `2px`).
- **Header Button States:**
  - *Disconnected:* Shows `CONECTAR CARTEIRA` button with retro hover effect.
  - *Connected:* Shows interactive pill:
    ```
    [ ● ONLINE ] [ Dos7...jcHa ] [ 1.25 SOL ] [ ▼ ]
    ```
  - *Dropdown Menu:*
    - `Copiar Endereço`
    - `Ver no Solana Explorer (Devnet)`
    - `Atualizar Saldos`
    - `Desconectar`

---

## 7. Integration Points Across the Application

### 7.1 Root Layout (`apps/web/app/layout.tsx`)
Enclose `{children}` inside `<SolanaWalletProvider>`.

### 7.2 Player Hub (`apps/web/app/page.tsx`)
- Replace mock `<button className="wallet">` in header with `<WalletHeaderButton />`.
- Replace the informative `walletDialog` `<dialog>` placeholder with actual connection trigger.
- When opening the checkout preview for games (`Moonlit Forest`, `Cyber Wave`, etc.), display real connected wallet status and real balances.

### 7.3 Creator Studio (`apps/web/app/creator/page.tsx`)
- In `tab === "revenue"`, replace `<div className="wallet-placeholder">` with live connected wallet information or "Conectar Carteira de Recebimento" button linked to the adapter.

---

## 8. Verification & Acceptance Criteria

1. **Build & Typecheck:**
   - `npm run typecheck` in `apps/web` passes with 0 errors.
   - `npm run build` succeeds without SSR hydration warnings or client-side bailout errors.
2. **Connection Lifecycle:**
   - Clicking `CONECTAR CARTEIRA` opens the retro modal listing installed wallets.
   - Connecting Phantom / Solflare successfully updates the header to the connected state.
   - Disconnecting properly reverts header to `CONECTAR CARTEIRA` and clears balances.
3. **Resilience & Rate-Limiting:**
   - Devnet RPC timeouts or 429 errors do not crash the app; UI shows fallback gracefully.
   - New wallets without USDC ATA display `0 USDC` cleanly without console stacktraces.
4. **Zero AI Evidence & Clean Code Standards:**
   - Zero AI tags or comments.
   - Strictly typed in TypeScript.
