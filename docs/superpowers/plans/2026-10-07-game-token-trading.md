# Game Token Trading & Bonding Curve Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the complete token trading and bonding curve engine for game-specific tokens on Solana, enabling real-time pricing, buy/sell swaps, and automatic liquidity injection from game license purchases.

**Architecture:** A deterministic integer-only mathematical bonding curve core (`BondingCurveMath`) with virtual constant-product reserves, Anchor PDA account derivation (`IBondingCurvePdaDeriver`), serialized on-chain swap transaction builders (`PlemuMarketTransactionBuilder`), and an orchestrating service (`IBondingCurveService`) integrated with wallet signing and market telemetry.

**Tech Stack:** C# .NET 10, Solana Anchor protocol, Base58 cryptography, SHA-256 discriminators, xUnit test suite.

**Spec:** [`docs/superpowers/specs/2026-10-06-solana-seeker-apk-design.md`](../specs/2026-10-06-solana-seeker-apk-design.md)

## Global Constraints

- **Integer Arithmetic Invariant:** Financial calculations must never use floating-point types (`float`, `double`). All amounts, quotes, and reserves use 64-bit (`ulong`) and 128-bit (`UInt128`) integers to prevent precision loss and ensure consistency with Solana VM bytecode.
- **Fail-Closed Security:** All inputs (public keys, amounts, slippage bounds) must be strictly validated. Out-of-bounds or invalid inputs throw typed exceptions immediately.
- **Fail-Safe Offline Mode:** The client must provide deterministic local simulations when RPC is unreachable or running in offline mode.
- **Branch:** All work is executed on the dedicated feature branch `feature/game-token-trading`.

---

## File Structure

### New Files
- `src/Plemu.Solana/BondingCurve/BondingCurveMath.cs`: Pure math library for virtual constant product bonding curve calculations.
- `src/Plemu.Solana/BondingCurve/BondingCurveState.cs`: Account data model matching Anchor on-chain layout.
- `src/Plemu.Solana/BondingCurve/GameTokenQuote.cs`: Data model for buy/sell swap quotes and slippage analysis.
- `src/Plemu.Solana/BondingCurve/GameTokenMarketData.cs`: Market statistics (price, 24h change, market cap, reserves).
- `src/Plemu.Solana/BondingCurve/IBondingCurvePdaDeriver.cs`: Interface for deriving curve, sol-vault, and token-vault PDAs.
- `src/Plemu.Solana/BondingCurve/DefaultBondingCurvePdaDeriver.cs`: Production PDA deriver using Ed25519 off-curve validation.
- `src/Plemu.Solana/BondingCurve/IBondingCurveService.cs`: Service interface for trading operations and market data.
- `src/Plemu.Solana/BondingCurve/BondingCurveService.cs`: Concrete implementation orchestrating RPC, state, and transaction building.
- `tests/Plemu.Solana.Tests/BondingCurveMathTests.cs`: Unit tests for all mathematical invariants, overflow protections, and quotes.
- `tests/Plemu.Solana.Tests/BondingCurvePdaDeriverTests.cs`: Tests for PDA seed derivations and error handling.
- `tests/Plemu.Solana.Tests/BondingCurveServiceTests.cs`: Tests for end-to-end trading, quotes, and license liquidity injection.

### Modified Files
- `src/Plemu.Solana/PlemuMarketTransactionBuilder.cs`: Add `swap_buy`, `swap_sell`, and `initialize_bonding_curve` instruction builders and data encodings.
- `tests/Plemu.Solana.Tests/PlemuMarketTransactionBuilderTests.cs`: Add verification of swap transaction wire format and account metadata.

---

## Task Decomposition

### Task 1: Mathematical Engine (`BondingCurveMath`)

**Files:**
- Create: `src/Plemu.Solana/BondingCurve/BondingCurveMath.cs`
- Test: `tests/Plemu.Solana.Tests/BondingCurveMathTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class BondingCurveMath
  {
      public static ulong CalculateBuyTokensOut(ulong virtualSolReserves, ulong virtualTokenReserves, ulong realTokenReserves, ulong solAmountIn);
      public static ulong CalculateSellSolOut(ulong virtualSolReserves, ulong virtualTokenReserves, ulong realSolReserves, ulong tokenAmountIn);
      public static ulong CalculateRequiredSolForTokens(ulong virtualSolReserves, ulong virtualTokenReserves, ulong tokensDesired);
      public static ulong CalculateSpotPriceLamports(ulong virtualSolReserves, ulong virtualTokenReserves);
      public static (ulong newVirtualSol, ulong newRealSol) InjectLiquidity(ulong virtualSolReserves, ulong realSolReserves, ulong solFeeAmount);
  }
  ```

- [ ] **Step 1: Write failing unit tests for `BondingCurveMath`**
  - Test buying tokens returns expected amounts without rounding loss.
  - Test zero inputs throw `ArgumentOutOfRangeException`.
  - Test that selling tokens yields predictable SOL without draining reserves below zero.
  - Test that injecting liquidity increases spot price monotonically.

- [ ] **Step 2: Run test to verify it fails**
  - Run: `dotnet test --filter BondingCurveMathTests`
  - Expected: Compilation failure (types not yet defined).

- [ ] **Step 3: Implement `BondingCurveMath`**
  - Implement integer arithmetic using `UInt128` to prevent intermediate overflow during cross-multiplications `(x * y) / z`.
  - Implement bounds validation and fail-closed checks.

- [ ] **Step 4: Run test to verify it passes**
  - Run: `dotnet test --filter BondingCurveMathTests`
  - Expected: PASS with 100% assertions green.

- [ ] **Step 5: Commit**
  - Command: `git add src/Plemu.Solana/BondingCurve/BondingCurveMath.cs tests/Plemu.Solana.Tests/BondingCurveMathTests.cs ; git commit -m "feat(solana): implement deterministic integer bonding curve math engine"`

---

### Task 2: Models and PDA Deriver (`IBondingCurvePdaDeriver`)

**Files:**
- Create: `src/Plemu.Solana/BondingCurve/BondingCurveState.cs`
- Create: `src/Plemu.Solana/BondingCurve/GameTokenQuote.cs`
- Create: `src/Plemu.Solana/BondingCurve/GameTokenMarketData.cs`
- Create: `src/Plemu.Solana/BondingCurve/IBondingCurvePdaDeriver.cs`
- Create: `src/Plemu.Solana/BondingCurve/DefaultBondingCurvePdaDeriver.cs`
- Test: `tests/Plemu.Solana.Tests/BondingCurvePdaDeriverTests.cs`

**Interfaces:**
- Consumes: Base58 utility from `Plemu.Solana.Cryptography.Base58`.
- Produces:
  ```csharp
  public interface IBondingCurvePdaDeriver
  {
      (string Address, byte Bump) DeriveBondingCurveAddress(string mint, string? marketProgramId = null);
      (string Address, byte Bump) DeriveCurveSolVaultAddress(string mint, string? marketProgramId = null);
      (string Address, byte Bump) DeriveCurveTokenVaultAddress(string mint, string? marketProgramId = null);
  }
  ```

- [ ] **Step 1: Write failing unit tests for PDA derivation**
  - Test deterministic derivations for known public keys.
  - Test invalid base58 strings throw `ArgumentException`.

- [ ] **Step 2: Run test to verify it fails**
  - Run: `dotnet test --filter BondingCurvePdaDeriverTests`
  - Expected: Compilation failure.

- [ ] **Step 3: Implement data models and `DefaultBondingCurvePdaDeriver`**
  - Implement seed generation matching Anchor conventions:
    - `[b"bonding_curve", mint]`
    - `[b"curve_sol_vault", mint]`
    - `[b"curve_token_vault", mint]`

- [ ] **Step 4: Run test to verify it passes**
  - Run: `dotnet test --filter BondingCurvePdaDeriverTests`
  - Expected: PASS.

- [ ] **Step 5: Commit**
  - Command: `git add src/Plemu.Solana/BondingCurve/ tests/Plemu.Solana.Tests/BondingCurvePdaDeriverTests.cs ; git commit -m "feat(solana): add bonding curve data models and pda deriver"`

---

### Task 3: Anchor Transaction Builders (`PlemuMarketTransactionBuilder`)

**Files:**
- Modify: `src/Plemu.Solana/PlemuMarketTransactionBuilder.cs`
- Modify: `tests/Plemu.Solana.Tests/PlemuMarketTransactionBuilderTests.cs`

**Interfaces:**
- Consumes: Discriminators, `SolanaInstruction`, `SolanaAccountMeta`.
- Produces:
  ```csharp
  public static byte[] SwapBuyDiscriminator { get; }
  public static byte[] SwapSellDiscriminator { get; }
  public static byte[] InitializeBondingCurveDiscriminator { get; }

  public static SolanaInstruction BuildSwapBuyInstruction(
      string buyer,
      string mint,
      string bondingCurve,
      string curveSolVault,
      string curveTokenVault,
      string buyerTokenAccount,
      ulong solAmount,
      ulong minTokensOut,
      string? marketProgramId = null);

  public static SolanaInstruction BuildSwapSellInstruction(
      string seller,
      string mint,
      string bondingCurve,
      string curveSolVault,
      string curveTokenVault,
      string sellerTokenAccount,
      ulong tokenAmount,
      ulong minSolOut,
      string? marketProgramId = null);
  ```

- [ ] **Step 1: Write failing tests for swap transaction builders**
  - Test Anchor discriminator computation for `swap_buy` and `swap_sell`.
  - Test account ordering and signer/writable flags.
  - Test binary payload layout and endianness.

- [ ] **Step 2: Run test to verify it fails**
  - Run: `dotnet test --filter PlemuMarketTransactionBuilderTests`
  - Expected: Compilation error for missing methods.

- [ ] **Step 3: Implement transaction builder methods in `PlemuMarketTransactionBuilder`**
  - Add discriminators: `sha256("global:swap_buy")[0..8]`, `sha256("global:swap_sell")[0..8]`.
  - Serialize instruction data with little-endian 64-bit integers.

- [ ] **Step 4: Run test to verify it passes**
  - Run: `dotnet test --filter PlemuMarketTransactionBuilderTests`
  - Expected: All builder tests pass.

- [ ] **Step 5: Commit**
  - Command: `git add src/Plemu.Solana/PlemuMarketTransactionBuilder.cs tests/Plemu.Solana.Tests/PlemuMarketTransactionBuilderTests.cs ; git commit -m "feat(solana): implement anchor instruction builders for token swap trading"`

---

### Task 4: Trading Service & Liquidity Injection (`BondingCurveService`)

**Files:**
- Create: `src/Plemu.Solana/BondingCurve/IBondingCurveService.cs`
- Create: `src/Plemu.Solana/BondingCurve/BondingCurveService.cs`
- Test: `tests/Plemu.Solana.Tests/BondingCurveServiceTests.cs`

**Interfaces:**
- Consumes: `BondingCurveMath`, `IBondingCurvePdaDeriver`, `ISolanaRpcClient`, `PlemuMarketTransactionBuilder`.
- Produces:
  ```csharp
  public interface IBondingCurveService
  {
      Task<GameTokenQuote> GetBuyQuoteAsync(string mint, ulong solAmountIn, ulong slippageBasisPoints = 100, CancellationToken ct = default);
      Task<GameTokenQuote> GetSellQuoteAsync(string mint, ulong tokenAmountIn, ulong slippageBasisPoints = 100, CancellationToken ct = default);
      Task<GameTokenMarketData> GetMarketDataAsync(string mint, CancellationToken ct = default);
      SolanaInstruction BuildBuySwapInstruction(string buyer, string mint, string buyerTokenAccount, ulong solAmountIn, ulong minTokensOut);
      SolanaInstruction BuildSellSwapInstruction(string seller, string mint, string sellerTokenAccount, ulong tokenAmountIn, ulong minSolOut);
      BondingCurveState SimulateLicensePurchaseFeeInjection(BondingCurveState currentState, ulong licensePriceSol, ulong feeBps = 2500);
  }
  ```

- [ ] **Step 1: Write failing unit tests for `BondingCurveService`**
  - Test quote calculation against mocked RPC responses.
  - Test slippage calculation and minimum amount bounds.
  - Test simulation of license purchase fee injection increasing spot price.

- [ ] **Step 2: Run test to verify it fails**
  - Run: `dotnet test --filter BondingCurveServiceTests`
  - Expected: Compilation failure.

- [ ] **Step 3: Implement `BondingCurveService`**
  - Handle RPC deserialization of `BondingCurveState` account.
  - Wire math engine to quotes.
  - Provide fallback default state for testing/demo offline mode.

- [ ] **Step 4: Run test to verify it passes**
  - Run: `dotnet test --filter BondingCurveServiceTests`
  - Expected: PASS with all tests passing.

- [ ] **Step 5: Commit**
  - Command: `git add src/Plemu.Solana/BondingCurve/ tests/Plemu.Solana.Tests/BondingCurveServiceTests.cs ; git commit -m "feat(solana): implement bonding curve trading service with fee injection"`

---

### Task 5: End-to-End Monetization & Trading Flow Verification

**Files:**
- Modify: `tests/Plemu.Solana.Tests/MonetizationEndToEndTests.cs`

- [ ] **Step 1: Write comprehensive end-to-end integration test**
  - Test complete lifecycle:
    1. Game initialized with bonding curve.
    2. Player buys game license (triggers `buy_license` and injects 25% fee into bonding curve).
    3. Spot price increases immediately.
    4. Second user performs `swap_buy` to acquire game tokens via MWA.
    5. User verifies holding and sells a portion back via `swap_sell`.
    6. All state transitions, receipt PDAs, and curves are cryptographically valid.

- [ ] **Step 2: Run all tests in the repository**
  - Run: `dotnet test`
  - Expected: 100% passing tests across the entire solution.

- [ ] **Step 3: Commit and Push**
  - Command: `git add tests/Plemu.Solana.Tests/MonetizationEndToEndTests.cs ; git commit -m "test(solana): add end-to-end integration test for game token trading and fee injection"`
  - Push branch to remote.
