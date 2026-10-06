using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chaos.NaCl;
using Newtonsoft.Json;
using Plemu.Content;
using Plemu.Core;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class MonetizationEndToEndTests
{
    private static readonly DateTimeOffset TestEpoch = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private const string BuyerPubkey = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
    private const string TreasuryPubkey = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
    private const string SplitsVaultPubkey = "95EuhxN4dM1EecYhJdGg5sZkR1pGg5sZkR1pGg5sZkR1";
    private const string CreatorPubkey = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
    private const string KeyId = "master-key-01";

    private readonly byte[] _edPrivKey;
    private readonly byte[] _edPubKey;
    private readonly KeyRegistry _keys;

    public MonetizationEndToEndTests()
    {
        byte[] seed = new byte[32];
        for (int i = 0; i < 32; i++) seed[i] = (byte)(i + 10);
        Ed25519.KeyPairFromSeed(out _edPubKey, out _edPrivKey, seed);

        _keys = new KeyRegistry();
        _keys.Keys.Add(new KeyEntry
        {
            KeyId = KeyId,
            PublicKey = Convert.ToHexString(_edPubKey).ToLowerInvariant(),
            Status = "active",
            ValidFrom = "2026-01-01T00:00:00Z"
        });
    }

    [Fact]
    public void E2E_Modality1_Free_FullFlow_AllowedWithoutWalletOrTicket()
    {
        // 1. Arrange Free Title
        string titleId = "zelda-demake";
        var (manifestSource, contentStore, _) = CreateManifestAndStore(
            titleId,
            model: "free",
            delivery: "none");

        var entitlementStore = new InMemoryEntitlementStore();
        var clock = new TestClock(TestEpoch);

        // 2. Act - Launch without wallet connected, without any tickets
        var decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, clock);

        // 3. Assert
        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.Denial);
    }

    [Fact]
    public async Task E2E_Modality2_BuyOnce_NativeSol_FullFlow_Success()
    {
        // 1. Arrange Hardware Wallet & Driver
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);

        // Set player balance to 2.5 SOL
        rpcClient.Balances[BuyerPubkey] = 2_500_000_000UL;

        // Connect wallet
        bool connected = await walletService.ConnectAsync();
        Assert.True(connected);
        Assert.Equal(WalletConnectionState.Connected, walletService.State);

        ulong currentBalance = await walletService.GetLamportsBalanceAsync();
        Assert.Equal(2_500_000_000UL, currentBalance);

        // 2. Arrange Game Title & Manifest
        string titleId = "chrono-quest";
        var (manifestSource, contentStore, artifactSha256Hex) = CreateManifestAndStore(
            titleId,
            model: "buy_once",
            delivery: "pda_license");

        // 3. Build & Sign Buy Transaction
        var deriver = new DefaultReceiptPdaDeriver();
        string receiptPda = deriver.DeriveReceiptAddress(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            artifactSha256Hex,
            BuyerPubkey);

        ulong priceLamports = 500_000_000UL; // 0.5 SOL
        byte[] txBytes = PlemuMarketTransactionBuilder.BuildBuyLicenseTransaction(
            BuyerPubkey,
            receiptPda,
            TreasuryPubkey,
            SplitsVaultPubkey,
            amount: priceLamports,
            tipToPlatform: 0,
            recentBlockhash: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL");

        Assert.True(txBytes.Length <= PlemuMarketTransactionBuilder.MaxTransactionSize);

        byte[]? signature = await walletService.SignTransactionAsync(txBytes);
        Assert.NotNull(signature);
        Assert.Equal(64, signature.Length);

        // 4. Simulate On-Chain Confirmation & Entitlement Verification
        byte[] receiptData = CreateReceiptAccountData(
            buyerPubkey: BuyerPubkey,
            sha256Hex: artifactSha256Hex,
            purchasedAt: TestEpoch.ToUnixTimeSeconds(),
            amount: priceLamports,
            currency: (byte)MarketCurrency.Sol,
            isRevoked: false);

        rpcClient.AccountMap[receiptPda] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            receiptData,
            Lamports: 1_460_000);

        var entitlementService = new SolanaEntitlementService(rpcClient, receiptPdaDeriver: deriver);
        bool hasEntitlement = await entitlementService.CanAccessEntryAsync(
            walletService.CurrentWallet!,
            artifactSha256Hex);
        Assert.True(hasEntitlement);

        // 5. Store Signed Ticket for Offline Play
        var entitlementStore = new InMemoryEntitlementStore();
        entitlementStore.AddTicket(artifactSha256Hex, expiresAt: TestEpoch);

        // 6. Test Offline Launches
        // Day 0: Immediate launch allowed
        var day0Decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, new TestClock(TestEpoch));
        Assert.True(day0Decision.IsAuthorized);

        // Day 5: Still offline, within 7-day grace period
        var day5Decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, new TestClock(TestEpoch.AddDays(5)));
        Assert.True(day5Decision.IsAuthorized);

        // Day 10: Still offline, exceeded 7-day grace period
        var day10Decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, new TestClock(TestEpoch.AddDays(10)));
        Assert.False(day10Decision.IsAuthorized);
        Assert.Equal(DenialReason.EntitlementMissing, day10Decision.Denial?.Reason);

        // Different unpaid commercial game fails with EntitlementMissing
        var unpaidTitle = "unpaid-space-adventure";
        var (unpaidManifest, unpaidStore, _) = CreateManifestAndStore(unpaidTitle, "buy_once", "pda_license");
        var unpaidDecision = LaunchGate.Authorize(unpaidTitle, unpaidManifest, unpaidStore, entitlementStore, new TestClock(TestEpoch));
        Assert.False(unpaidDecision.IsAuthorized);
        Assert.Equal(DenialReason.EntitlementMissing, unpaidDecision.Denial?.Reason);
    }

    [Fact]
    public async Task E2E_Modality2_BuyOnce_USDC_RentExemptionCheck_FailsWhenInsufficientSol()
    {
        // Arrange
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);

        await walletService.ConnectAsync();

        // Player has 100 USDC but only 0.001 SOL (< 0.002 SOL threshold)
        rpcClient.Balances[BuyerPubkey] = 1_000_000UL; // 0.001 SOL
        rpcClient.TokenBalances[$"{BuyerPubkey}_{PlemuMarketTransactionBuilder.DefaultUsdcMint}"] = 100.0m;

        // Act
        bool rentExempt = await walletService.ValidateMinimumSolForUsdcAsync(BuyerPubkey);

        // Assert - Warns early and prevents transaction creation
        Assert.False(rentExempt);
    }

    [Fact]
    public async Task E2E_Modality2_BuyOnce_USDC_RentExemptionCheck_SucceedsWhenSolSufficient()
    {
        // 1. Arrange Wallet with Sufficient SOL and USDC
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);

        await walletService.ConnectAsync();

        // Player has 50 USDC and 0.05 SOL (>= 0.002 SOL)
        rpcClient.Balances[BuyerPubkey] = 50_000_000UL; // 0.05 SOL
        rpcClient.TokenBalances[$"{BuyerPubkey}_{PlemuMarketTransactionBuilder.DefaultUsdcMint}"] = 50.0m;

        bool rentExempt = await walletService.ValidateMinimumSolForUsdcAsync(BuyerPubkey);
        Assert.True(rentExempt);

        // 2. Arrange Game Title & Manifest
        string titleId = "cyber-ninja-dx";
        var (manifestSource, contentStore, artifactSha256Hex) = CreateManifestAndStore(
            titleId,
            model: "buy_once",
            delivery: "pda_license");

        var deriver = new DefaultReceiptPdaDeriver();
        string receiptPda = deriver.DeriveReceiptAddress(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            artifactSha256Hex,
            BuyerPubkey);

        // 3. Build & Sign Buy Transaction for USDC
        ulong priceUsdcUnits = 20_000_000UL; // 20 USDC (6 decimals)
        string buyerAta = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(BuyerPubkey, PlemuMarketTransactionBuilder.DefaultUsdcMint);
        string treasuryAta = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(TreasuryPubkey, PlemuMarketTransactionBuilder.DefaultUsdcMint);
        string splitsVaultAta = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(SplitsVaultPubkey, PlemuMarketTransactionBuilder.DefaultUsdcMint);

        var usdcIx = PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(
            BuyerPubkey,
            receiptPda,
            buyerAta,
            treasuryAta,
            splitsVaultAta,
            amount: priceUsdcUnits,
            tipToPlatform: 0);

        var tx = new SolanaTransaction(
            FeePayer: BuyerPubkey,
            RecentBlockhash: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL",
            Instructions: new List<SolanaInstruction> { usdcIx });

        byte[] txBytes = PlemuMarketTransactionBuilder.SerializeTransaction(tx);
        byte[]? signature = await walletService.SignTransactionAsync(txBytes);
        Assert.NotNull(signature);

        // 4. On-chain receipt verification
        byte[] receiptData = CreateReceiptAccountData(
            buyerPubkey: BuyerPubkey,
            sha256Hex: artifactSha256Hex,
            purchasedAt: TestEpoch.ToUnixTimeSeconds(),
            amount: priceUsdcUnits,
            currency: (byte)MarketCurrency.Usdc,
            isRevoked: false);

        rpcClient.AccountMap[receiptPda] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            receiptData,
            Lamports: 1_460_000);

        var entitlementService = new SolanaEntitlementService(rpcClient, receiptPdaDeriver: deriver);
        bool entitled = await entitlementService.CanAccessEntryAsync(walletService.CurrentWallet!, artifactSha256Hex);
        Assert.True(entitled);

        // 5. Offline Launch Gate verification
        var entitlementStore = new InMemoryEntitlementStore();
        entitlementStore.AddTicket(artifactSha256Hex, TestEpoch);
        var decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, new TestClock(TestEpoch));
        Assert.True(decision.IsAuthorized);
    }

    [Fact]
    public async Task E2E_Modality3_DemoAndFullPurchase_IsolatesAccessAndTransitionsSmoothly()
    {
        // 1. Arrange Demo and Full Entries
        string demoTitleId = "retro-racer-demo";
        string fullTitleId = "retro-racer";

        var (demoManifest, demoContent, _) = CreateManifestAndStore(demoTitleId, "free", "none");
        var (fullManifest, fullContent, fullSha256) = CreateManifestAndStore(fullTitleId, "buy_once", "pda_license");

        var entitlementStore = new InMemoryEntitlementStore();
        var clock = new TestClock(TestEpoch);

        // Demo launches without any wallet or tickets
        var demoDecision = LaunchGate.Authorize(demoTitleId, demoManifest, demoContent, entitlementStore, clock);
        Assert.True(demoDecision.IsAuthorized);

        // Full version is denied before purchase
        var fullDecisionPre = LaunchGate.Authorize(fullTitleId, fullManifest, fullContent, entitlementStore, clock);
        Assert.False(fullDecisionPre.IsAuthorized);
        Assert.Equal(DenialReason.EntitlementMissing, fullDecisionPre.Denial?.Reason);

        // 2. User purchases full version on PSG1
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);
        await walletService.ConnectAsync();

        var deriver = new DefaultReceiptPdaDeriver();
        string receiptPda = deriver.DeriveReceiptAddress(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            fullSha256,
            BuyerPubkey);

        byte[] txBytes = PlemuMarketTransactionBuilder.BuildBuyLicenseTransaction(
            BuyerPubkey,
            receiptPda,
            TreasuryPubkey,
            SplitsVaultPubkey,
            amount: 750_000_000UL,
            tipToPlatform: 0,
            recentBlockhash: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL");

        await walletService.SignTransactionAsync(txBytes);

        // Setup on-chain receipt
        byte[] receiptData = CreateReceiptAccountData(BuyerPubkey, fullSha256, TestEpoch.ToUnixTimeSeconds(), 750_000_000UL, (byte)MarketCurrency.Sol, false);
        rpcClient.AccountMap[receiptPda] = new SolanaAccountInfo(SolanaEntitlementOptions.DefaultMarketProgramId, receiptData, 1_460_000);

        var entitlementService = new SolanaEntitlementService(rpcClient, receiptPdaDeriver: deriver);
        bool hasFullAccess = await entitlementService.CanAccessEntryAsync(walletService.CurrentWallet!, fullSha256);
        Assert.True(hasFullAccess);

        // Cache ticket for full version
        entitlementStore.AddTicket(fullSha256, TestEpoch);

        // 3. Both Demo and Full version now launch cleanly
        var fullDecisionPost = LaunchGate.Authorize(fullTitleId, fullManifest, fullContent, entitlementStore, clock);
        Assert.True(fullDecisionPost.IsAuthorized);

        var demoDecisionPost = LaunchGate.Authorize(demoTitleId, demoManifest, demoContent, entitlementStore, clock);
        Assert.True(demoDecisionPost.IsAuthorized);
    }

    [Fact]
    public async Task E2E_Modality4_PayWhatYouWant_DynamicAmountAndPlatformTip_Success()
    {
        // 1. Arrange PWYW Game
        string titleId = "indie-passion-project";
        var (manifestSource, contentStore, artifactSha256Hex) = CreateManifestAndStore(
            titleId,
            model: "pwyw",
            delivery: "pda_license");

        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);
        await walletService.ConnectAsync();

        // 2. Player chooses custom price 2.0 SOL + 0.1 SOL tip to platform
        ulong customAmount = 2_000_000_000UL;
        ulong tipToPlatform = 100_000_000UL;

        var deriver = new DefaultReceiptPdaDeriver();
        string receiptPda = deriver.DeriveReceiptAddress(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            artifactSha256Hex,
            BuyerPubkey);

        byte[] txBytes = PlemuMarketTransactionBuilder.BuildBuyLicenseTransaction(
            BuyerPubkey,
            receiptPda,
            TreasuryPubkey,
            SplitsVaultPubkey,
            amount: customAmount,
            tipToPlatform: tipToPlatform,
            recentBlockhash: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL");

        byte[]? sig = await walletService.SignTransactionAsync(txBytes);
        Assert.NotNull(sig);

        // Verify on-chain entitlement
        byte[] receiptData = CreateReceiptAccountData(
            BuyerPubkey,
            artifactSha256Hex,
            TestEpoch.ToUnixTimeSeconds(),
            customAmount,
            (byte)MarketCurrency.Sol,
            false);
        rpcClient.AccountMap[receiptPda] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            receiptData,
            1_460_000);

        var entitlementService = new SolanaEntitlementService(rpcClient, receiptPdaDeriver: deriver);
        bool canAccess = await entitlementService.CanAccessEntryAsync(walletService.CurrentWallet!, artifactSha256Hex);
        Assert.True(canAccess);

        // Offline LaunchGate
        var entitlementStore = new InMemoryEntitlementStore();
        entitlementStore.AddTicket(artifactSha256Hex, TestEpoch);
        var decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, new TestClock(TestEpoch));
        Assert.True(decision.IsAuthorized);
    }

    [Fact]
    public async Task E2E_Modality5_TipOnly_FreeAccessWithVoluntaryCreatorTipping()
    {
        // 1. Arrange Tip-Only Game
        string titleId = "community-arcade";
        var (manifestSource, contentStore, _) = CreateManifestAndStore(
            titleId,
            model: "tip_only",
            delivery: "none");

        var entitlementStore = new InMemoryEntitlementStore();
        var clock = new TestClock(TestEpoch);

        // Tip-only games are free to download and launch without any ticket
        var decisionBeforeTip = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, clock);
        Assert.True(decisionBeforeTip.IsAuthorized);

        // 2. Player voluntarily sends a tip to creator on device
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey);
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);
        await walletService.ConnectAsync();

        ulong tipLamports = 300_000_000UL; // 0.3 SOL
        byte[] txBytes = PlemuMarketTransactionBuilder.BuildSendTipTransaction(
            BuyerPubkey,
            CreatorPubkey,
            TreasuryPubkey,
            amount: tipLamports,
            tipToPlatform: 0,
            recentBlockhash: "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL");

        byte[]? sig = await walletService.SignTransactionAsync(txBytes);
        Assert.NotNull(sig);

        // Game remains launchable at all times
        var decisionAfterTip = LaunchGate.Authorize(titleId, manifestSource, contentStore, entitlementStore, clock);
        Assert.True(decisionAfterTip.IsAuthorized);
    }

    [Fact]
    public async Task E2E_HardwareWallet_UserRejection_HandlesGracefullyWithoutCrash()
    {
        // 1. Arrange Hardware Driver configured to reject/cancel on device
        var driver = new MockPlaySolanaHardwareDriver(BuyerPubkey)
        {
            ThrowOperationCanceledOnSign = true
        };
        var rpcClient = new MockSolanaRpcClient();
        var walletService = new PlaySolanaWalletService(driver, rpcClient);

        await walletService.ConnectAsync();
        Assert.Equal(WalletConnectionState.Connected, walletService.State);

        // 2. Attempt signing checkout transaction
        byte[] dummyTx = new byte[100];
        byte[]? sig = await walletService.SignTransactionAsync(dummyTx);

        // 3. Assert: returns null cleanly, keeps state Connected, no crash
        Assert.Null(sig);
        Assert.Equal(WalletConnectionState.Connected, walletService.State);

        // LaunchGate for commercial game denies launch without license
        string titleId = "hero-blade";
        var (manifestSource, contentStore, _) = CreateManifestAndStore(titleId, "buy_once", "pda_license");
        var decision = LaunchGate.Authorize(titleId, manifestSource, contentStore, new InMemoryEntitlementStore(), new TestClock(TestEpoch));
        Assert.False(decision.IsAuthorized);
        Assert.Equal(DenialReason.EntitlementMissing, decision.Denial?.Reason);
    }

    [Fact]
    public async Task E2E_OnChainVerification_FailClosed_OnRpcNetworkError()
    {
        // 1. Arrange RPC Client that fails with network error
        var rpcClient = new MockSolanaRpcClient
        {
            ExceptionToThrow = new HttpRequestException("RPC Node timeout or connection refused")
        };
        var entitlementService = new SolanaEntitlementService(rpcClient);
        var wallet = new WalletIdentity(BuyerPubkey);

        // 2. Act - Fail Closed behavior
        bool entitled = await entitlementService.CanAccessEntryAsync(wallet, new string('a', 64));

        // 3. Assert
        Assert.False(entitled);
    }

    [Fact]
    public async Task E2E_OnChainVerification_RevokedLicense_DeniesAccess()
    {
        // 1. Arrange Revoked Receipt PDA on-chain (is_revoked = 1)
        string sha256Hex = new string('b', 64);
        var deriver = new DefaultReceiptPdaDeriver();
        string receiptPda = deriver.DeriveReceiptAddress(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            sha256Hex,
            BuyerPubkey);

        byte[] revokedData = CreateReceiptAccountData(
            BuyerPubkey,
            sha256Hex,
            TestEpoch.ToUnixTimeSeconds(),
            1_000_000UL,
            (byte)MarketCurrency.Sol,
            isRevoked: true); // revoked!

        var rpcClient = new MockSolanaRpcClient();
        rpcClient.AccountMap[receiptPda] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMarketProgramId,
            revokedData,
            1_460_000);

        var entitlementService = new SolanaEntitlementService(rpcClient, receiptPdaDeriver: deriver);
        var wallet = new WalletIdentity(BuyerPubkey);

        // 2. Act
        bool entitled = await entitlementService.CanAccessEntryAsync(wallet, sha256Hex);

        // 3. Assert
        Assert.False(entitled);
    }

    [Fact]
    public async Task E2E_MetaplexCoreCartridge_OwnerMatches_AuthorizesAccess()
    {
        // 1. Arrange Metaplex Core Account owned by buyer
        string sha256Hex = new string('c', 64);
        string assetPubkey = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";

        byte[] buyerBytes = Base58.Decode(BuyerPubkey);
        byte[] metaplexData = new byte[33];
        metaplexData[0] = 1; // Key::AssetV1
        Array.Copy(buyerBytes, 0, metaplexData, 1, 32);

        var rpcClient = new MockSolanaRpcClient();
        rpcClient.AccountMap[assetPubkey] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMetaplexCoreProgramId,
            metaplexData,
            1_460_000);

        var options = new SolanaEntitlementOptions
        {
            EntryToAssetMap = { [sha256Hex] = assetPubkey }
        };

        var entitlementService = new SolanaEntitlementService(rpcClient, options: options);
        var wallet = new WalletIdentity(BuyerPubkey);

        // 2. Act
        bool entitled = await entitlementService.CanAccessEntryAsync(wallet, sha256Hex);

        // 3. Assert
        Assert.True(entitled);
    }

    [Fact]
    public async Task E2E_MetaplexCoreCartridge_OwnerMismatch_DeniesAccess()
    {
        // 1. Arrange Metaplex Core Account owned by someone else
        string sha256Hex = new string('d', 64);
        string assetPubkey = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";

        byte[] otherOwnerBytes = Base58.Decode(CreatorPubkey);
        byte[] metaplexData = new byte[33];
        metaplexData[0] = 1; // Key::AssetV1
        Array.Copy(otherOwnerBytes, 0, metaplexData, 1, 32);

        var rpcClient = new MockSolanaRpcClient();
        rpcClient.AccountMap[assetPubkey] = new SolanaAccountInfo(
            SolanaEntitlementOptions.DefaultMetaplexCoreProgramId,
            metaplexData,
            1_460_000);

        var options = new SolanaEntitlementOptions
        {
            EntryToAssetMap = { [sha256Hex] = assetPubkey }
        };

        var entitlementService = new SolanaEntitlementService(rpcClient, options: options);
        var wallet = new WalletIdentity(BuyerPubkey);

        // 2. Act
        bool entitled = await entitlementService.CanAccessEntryAsync(wallet, sha256Hex);

        // 3. Assert
        Assert.False(entitled);
    }

    #region Helpers and In-Memory Mocks

    private static byte[] CreateReceiptAccountData(
        string buyerPubkey,
        string sha256Hex,
        long purchasedAt,
        ulong amount,
        byte currency,
        bool isRevoked)
    {
        byte[] data = new byte[91];
        // 0..7 Discriminator
        byte[] disc = SHA256.HashData(Encoding.UTF8.GetBytes("account:Receipt"));
        Array.Copy(disc, 0, data, 0, 8);

        // 8..39 Buyer
        byte[] buyerBytes = Base58.Decode(buyerPubkey);
        Array.Copy(buyerBytes, 0, data, 8, 32);

        // 40..71 SHA256
        byte[] shaBytes = Convert.FromHexString(sha256Hex);
        Array.Copy(shaBytes, 0, data, 40, 32);

        // 72..79 PurchasedAt
        BitConverter.TryWriteBytes(data.AsSpan(72, 8), purchasedAt);

        // 80..87 AmountPaid
        BitConverter.TryWriteBytes(data.AsSpan(80, 8), amount);

        // 88 Currency
        data[88] = currency;

        // 89 IsRevoked
        data[89] = (byte)(isRevoked ? 1 : 0);

        // 90 Bump
        data[90] = 254;

        return data;
    }

    private (IManifestSource manifestSource, IContentStore store, string sha256) CreateManifestAndStore(
        string titleId,
        string model,
        string delivery)
    {
        byte[] artifactBytes = Encoding.UTF8.GetBytes($"binary-rom-content-for-{titleId}");
        byte[] shaBytes = SHA256.HashData(artifactBytes);
        string sha256 = Convert.ToHexString(shaBytes).ToLowerInvariant();

        var entry = new ManifestEntry
        {
            Id = titleId,
            Title = $"Game {titleId}",
            System = "gb",
            CoreId = "sameboy",
            MinCoreVersion = "0.1.0",
            Sha256 = sha256,
            SizeBytes = artifactBytes.Length,
            DownloadUrls = new List<string> { $"https://cdn.plemu.xyz/{titleId}" },
            Creator = new Creator { Wallet = CreatorPubkey, DisplayName = "Dev" },
            License = new LicenseInfo { Type = "Commercial", EvidenceUrl = "https://plemu.xyz" },
            Attestation = new Attestation { Role = "creator", Message = "Attested", Signature = "sig", SignedAt = "2026-07-12T00:00:00Z" },
            Artwork = new Artwork { IconUrl = "https://plemu.xyz/icon.png" },
            AddedAt = "2026-07-12T00:00:00Z",
            IsRevoked = false,
            Tags = new List<string> { "game" },
            Monetization = new MonetizationInfo
            {
                Model = model,
                Delivery = delivery
            }
        };

        var manifest = new Manifest
        {
            SchemaVersion = 1,
            GeneratedAt = "2026-07-12T00:00:00Z",
            ExpiresAt = "2030-01-01T00:00:00Z",
            Sequence = 1,
            SignerKeyId = KeyId,
            Entries = new List<ManifestEntry> { entry }
        };

        string manifestJson = JsonConvert.SerializeObject(manifest);
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        byte[] signature = Ed25519.Sign(manifestBytes, _edPrivKey);

        var envelope = new SignatureEnvelope
        {
            KeyId = KeyId,
            Alg = "ed25519",
            Signature = Convert.ToBase64String(signature),
            SignedAt = "2026-07-12T00:00:00Z"
        };
        byte[] sigBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));

        var manifestSource = new InMemoryManifestSource(new ManifestBundle(manifestBytes, sigBytes, _keys));
        var contentStore = new InMemoryContentStore();
        contentStore.Set(titleId, artifactBytes);

        return (manifestSource, contentStore, sha256);
    }

    private sealed class InMemoryManifestSource : IManifestSource
    {
        private readonly ManifestBundle _bundle;
        public bool IsBundled => false;
        public InMemoryManifestSource(ManifestBundle bundle) => _bundle = bundle;
        public ManifestBundle GetVerifiedManifest() => _bundle;
    }

    private sealed class InMemoryContentStore : IContentStore
    {
        private readonly Dictionary<string, byte[]> _storage = new(StringComparer.Ordinal);
        public void Set(string id, byte[] data) => _storage[id] = data;
#pragma warning disable CS8766
        public Stream? OpenRead(string titleId) =>
            _storage.TryGetValue(titleId, out var data) ? new MemoryStream(data) : null;
#pragma warning restore CS8766
        public void Quarantine(string titleId) { }
    }

    private sealed class InMemoryEntitlementStore : Plemu.Content.IEntitlementStore, Plemu.Core.Abstractions.IEntitlementStore
    {
        private readonly Dictionary<string, DateTimeOffset> _tickets = new(StringComparer.OrdinalIgnoreCase);

        public void AddTicket(string artifactSha256, DateTimeOffset expiresAt) =>
            _tickets[artifactSha256] = expiresAt;

        public bool HasValidTicket(string artifactSha256, DateTimeOffset currentTime)
        {
            if (!_tickets.TryGetValue(artifactSha256, out DateTimeOffset expiresAt))
            {
                return false;
            }
            return currentTime <= expiresAt + FreshnessPolicy.GracePeriod;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class MockPlaySolanaHardwareDriver : IPlaySolanaWalletDriver
    {
        private readonly string _publicKey;
        public bool ThrowOperationCanceledOnSign { get; set; }

        public MockPlaySolanaHardwareDriver(string publicKey) => _publicKey = publicKey;

        public Task<string?> RequestPublicKeyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(_publicKey);

        public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default)
        {
            if (ThrowOperationCanceledOnSign) throw new OperationCanceledException();
            return Task.FromResult<byte[]?>(new byte[64]);
        }

        public Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
        {
            if (ThrowOperationCanceledOnSign) throw new OperationCanceledException();
            return Task.FromResult<byte[]?>(new byte[64]);
        }
    }

    private sealed class MockSolanaRpcClient : ISolanaRpcClient
    {
        public Dictionary<string, SolanaAccountInfo?> AccountMap { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ulong> Balances { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, decimal> TokenBalances { get; } = new(StringComparer.Ordinal);
        public Exception? ExceptionToThrow { get; set; }

        public Task<SolanaAccountInfo?> GetAccountInfoAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            AccountMap.TryGetValue(pubkey, out var info);
            return Task.FromResult(info);
        }

        public Task<IReadOnlyList<SolanaAccountInfo?>?> GetMultipleAccountsAsync(IReadOnlyList<string> pubkeys, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            var list = new List<SolanaAccountInfo?>();
            foreach (var p in pubkeys)
            {
                AccountMap.TryGetValue(p, out var info);
                list.Add(info);
            }
            return Task.FromResult<IReadOnlyList<SolanaAccountInfo?>?>(list);
        }

        public Task<ulong> GetBalanceAsync(string pubkey, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            Balances.TryGetValue(pubkey, out ulong bal);
            return Task.FromResult(bal);
        }

        public Task<decimal> GetTokenBalanceAsync(string walletPubkey, string tokenMint, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            string key = $"{walletPubkey}_{tokenMint}";
            TokenBalances.TryGetValue(key, out decimal bal);
            return Task.FromResult(bal);
        }
    }

    #endregion
}
