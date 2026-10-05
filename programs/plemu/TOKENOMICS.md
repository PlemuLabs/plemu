# $PLEMU Tokenomics & On-Chain Economic Specification

**Version:** 1.0.0  
**Status:** Ratified (Sprint 2026-09-30)  
**Normative Invariant:** Frozen Fixed Supply, SPL Token Classic

---

## 1. Core Token Invariants

| Parameter | Specification | Notes |
| :--- | :--- | :--- |
| **Token Name** | Plemu | The native economic utility and governance asset of the Plemu emulation network |
| **Ticker / Symbol** | `$PLEMU` | Universal identifier across on-chain programs and frontends |
| **Token Standard** | **SPL Token Classic** | Program: `TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA` |
| **Decimals** | **6** | Identical to USDC on Solana; optimal for micro-transactions and DLMM tick spacing |
| **Total Fixed Supply** | **1,000,000,000 $PLEMU** | `1_000_000_000_000_000` base units (1 Billion tokens) |
| **Inflation Rate** | **0.00%** | Hard-capped supply. No inflationary minting schedule |
| **Freeze Authority** | **None (Null)** | The token is permissionless; accounts cannot be frozen by authority |
| **Mint Authority** | **Burned on Mainnet** | Devnet retains deployer mint authority solely for test fixtures |

---

## 2. Technical Standard & Ecosystem Compatibility Rationale

### Why SPL Classic instead of Token-2022 (Token Extensions)?
The Plemu architecture prioritizes universal composability across the Solana DeFi and gaming stack:
1. **DEX & AMM Compatibility:** Meteora DLMM (Dynamic Liquidity Market Maker) and Raydium CLMM have first-class, battle-tested support for classic SPL tokens.
2. **Launchpad & Liquidity Bootstrapping:** Full out-of-the-box compatibility with Futard launchpad and Jupiter routing.
3. **Wallet Adapter Friction:** Zero edge-case issues across Phantom, Solflare, Backpack, and embedded mobile SDKs (Saga / Chapter 2 dApp store).
4. **Minimal Attack Surface:** Plemu enforces custom protocol mechanics (derivative splits, seating passes, anti-dusting) within dedicated, audited Anchor programs (`plemu-market`, `plemu-splits`, `plemu-seats`) rather than using complex, bleeding-edge Token-2022 transfer hooks.

---

## 3. Token Allocation & Treasury Architecture

Total supply of 1,000,000,000 $PLEMU is divided across functional pools:

```
┌─────────────────────────────────────────────────────────────┐
│                 $PLEMU Total Supply: 1,000,000,000          │
├───────────────────┬───────────────────┬─────────────────────┤
│ Ecosystem & Quests│ Liquidity & AMM   │ Community & Backers │
│       40%         │       25%         │        35%          │
│ (400,000,000)     │ (250,000,000)     │ (350,000,000)       │
└───────────────────┴───────────────────┴─────────────────────┘
```

1. **Player & Creator Quests Treasury (40% — 400M $PLEMU):**
   - Locked in multi-sig vault controlled by `plemu-quests`.
   - Disbursed exclusively for verifiable game achievements (speedrun milestones, retro bounties, curation verifications, community bug bounties).
2. **Liquidity Provisioning & Market Bootstrapping (25% — 250M $PLEMU):**
   - Allocated to Meteora DLMM pools (`PLEMU-SOL` and `PLEMU-USDC`).
   - Ensures minimal slippage for players acquiring capacity seats in the client.
3. **Ecosystem & Legal Safe Harbor Fund (15% — 150M $PLEMU):**
   - Retained for developer tooling, retro preservation grants, and DMCA legal infrastructure.
4. **Core Contributors & Early Supporters (20% — 200M $PLEMU):**
   - Subject to 1-year cliff followed by 24-month linear block-by-block vesting on-chain.

---

## 4. Integration with Anchor Program Suite

### 4.1 `plemu-market` (Settlement & Jupiter Routing)
- Purchases of standalone digital ROM packs, digital cartridges, and creator tips settle natively in `$PLEMU`.
- Built-in Jupiter swap routing allows players to pay with `SOL` or `USDC`, which auto-swaps to `$PLEMU` at the transaction boundary before executing on-chain receipts.

### 4.2 `plemu-seats` (Access Passes & Capacity)
- Emulation packs with limited capacity require holding a `Seat` account.
- Rent or purchase of seats is denominated in `$PLEMU`.
- Unlocks the zero-egress Cloudflare Worker presigned URL download via Ed25519 authentication.

### 4.3 `plemu-splits` & Derivative Lineage Royalty Decay
Plemu features first-class derivative split attribution for fan translations, demakes, and ROM hacks.
When a derivative work generates revenue:
- **Base Split Formula:** Every upstream parent receives a configured royalty percentage (e.g. 5% to 15%).
- **Multi-Hop Decay Transparency (Honesty Note):**
  In a deep derivative chain ($A \to B \to C \to D$):
  If each hop retains a 10% royalty on secondary sales:
  - Author $C$ receives $10\%$ of gross.
  - Author $B$ receives $10\% \times 10\% = 1.0\%$ of gross.
  - Root Author $A$ receives $10\% \times 1.0\% = 0.1\%$ of gross.
  *Note on architectural honesty:* Claims that "the base author is paid on every derivative" must acknowledge this geometric decay. For shallow lineage (1-2 hops), payouts are substantial; for deep chains (>3 hops), rent extraction approaches zero to ensure the direct creator of the derivative retains economic viability.

### 4.4 Deflationary Mechanics: Terminal Burns
- **Moderation Slashing:** Any staked deposit by malicious actors submitting infringement or bad-faith artifacts is slashed and permanently burned (`burn_checked`).
- **Platform Fee Burns:** A percentage (e.g. 10%) of all protocol fees collected across `plemu-market` transactions is routed to the SPL burn instruction, permanently reducing circulating supply.

---

## 5. Testing & Devnet vs Mainnet Boundaries

1. **Pre-Mainnet Testing:**
   - In Devnet, a Meteora DLMM pool for `$PLEMU` is not actively liquid with mainnet TVL.
   - Therefore, the integration test suite validates:
     a) Swap simulation from `SOL` / `USDC` via mock Jupiter routes.
     b) Direct on-chain calls to `purchase(PLEMU)` using minted Devnet tokens.
2. **Authority Revocation Checklist (Mainnet Day-0):**
   - [ ] Execute `spl-token authorize <MINT> mint --disable`
   - [ ] Verify `mintAuthority == null` via `@solana/web3.js`
   - [ ] Confirm fixed supply permanently locked at 1,000,000,000 $PLEMU.
