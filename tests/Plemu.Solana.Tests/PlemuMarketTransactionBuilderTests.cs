using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana;
using Plemu.Solana.Cryptography;
using Xunit;

namespace Plemu.Solana.Tests;

public class PlemuMarketTransactionBuilderTests
{
    private const string ValidBuyer = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
    private const string ValidReceipt = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
    private const string ValidTreasury = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
    private const string ValidSplitsVault = "95EuhxN4dM1EecYhJdGg5sZkR1pGg5sZkR1pGg5sZkR1";
    private const string ValidBlockhash = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
    private const string ValidUsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    private const string CustomProgramId = "Seat111111111111111111111111111111111111111";

    [Fact]
    public void ComputeAnchorDiscriminator_ReturnsSha256First8Bytes()
    {
        var expectedBuy = SHA256.HashData(Encoding.UTF8.GetBytes("global:buy_license")).Take(8).ToArray();
        var actualBuy = PlemuMarketTransactionBuilder.ComputeAnchorDiscriminator("buy_license");

        Assert.Equal(expectedBuy, actualBuy);

        var expectedTip = SHA256.HashData(Encoding.UTF8.GetBytes("global:send_tip")).Take(8).ToArray();
        var actualTip = PlemuMarketTransactionBuilder.ComputeAnchorDiscriminator("send_tip");

        Assert.Equal(expectedTip, actualTip);
    }

    [Fact]
    public void PrecalculatedDiscriminators_MatchAnchorStandards()
    {
        Assert.Equal(8, PlemuMarketTransactionBuilder.BuyLicenseDiscriminator.Length);
        Assert.Equal(8, PlemuMarketTransactionBuilder.SendTipDiscriminator.Length);

        Assert.Equal(
            PlemuMarketTransactionBuilder.ComputeAnchorDiscriminator("buy_license"),
            PlemuMarketTransactionBuilder.BuyLicenseDiscriminator);

        Assert.Equal(
            PlemuMarketTransactionBuilder.ComputeAnchorDiscriminator("send_tip"),
            PlemuMarketTransactionBuilder.SendTipDiscriminator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeAnchorDiscriminator_ThrowsOnNullOrEmpty(string? instructionName)
    {
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.ComputeAnchorDiscriminator(instructionName!));
    }

    [Fact]
    public void BuildBuyLicenseData_SerializesCorrectly()
    {
        ulong amount = 10_000_000UL;
        ulong tip = 500_000UL;
        var data = PlemuMarketTransactionBuilder.BuildBuyLicenseData(amount, MarketCurrency.Sol, tip);

        Assert.Equal(25, data.Length);
        Assert.Equal(PlemuMarketTransactionBuilder.BuyLicenseDiscriminator, data.Take(8).ToArray());
        Assert.Equal(amount, BitConverter.ToUInt64(data, 8));
        Assert.Equal((byte)MarketCurrency.Sol, data[16]);
        Assert.Equal(tip, BitConverter.ToUInt64(data, 17));
    }

    [Fact]
    public void BuildBuyLicenseData_Usdc_SerializesCorrectly()
    {
        ulong amount = 25_000_000UL;
        ulong tip = 0UL;
        var data = PlemuMarketTransactionBuilder.BuildBuyLicenseData(amount, MarketCurrency.Usdc, tip);

        Assert.Equal(25, data.Length);
        Assert.Equal(PlemuMarketTransactionBuilder.BuyLicenseDiscriminator, data.Take(8).ToArray());
        Assert.Equal(amount, BitConverter.ToUInt64(data, 8));
        Assert.Equal((byte)MarketCurrency.Usdc, data[16]);
        Assert.Equal(0UL, BitConverter.ToUInt64(data, 17));
    }

    [Fact]
    public void BuildSendTipData_SerializesCorrectly()
    {
        ulong amount = 2_000_000UL;
        ulong tip = 100_000UL;
        var data = PlemuMarketTransactionBuilder.BuildSendTipData(amount, MarketCurrency.Sol, tip);

        Assert.Equal(25, data.Length);
        Assert.Equal(PlemuMarketTransactionBuilder.SendTipDiscriminator, data.Take(8).ToArray());
        Assert.Equal(amount, BitConverter.ToUInt64(data, 8));
        Assert.Equal((byte)MarketCurrency.Sol, data[16]);
        Assert.Equal(tip, BitConverter.ToUInt64(data, 17));
    }

    [Fact]
    public void BuildBuyLicenseInstruction_Sol_OrdersAccountsStrictly()
    {
        ulong amount = 5_000_000UL;
        ulong tip = 250_000UL;

        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            treasury: ValidTreasury,
            splitsVault: ValidSplitsVault,
            amount: amount,
            tipToPlatform: tip,
            marketProgramId: CustomProgramId);

        Assert.Equal(CustomProgramId, ix.ProgramId);
        Assert.Equal(5, ix.Accounts.Count);

        // [0] buyer (signer, writable)
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.True(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        // [1] receipt (writable)
        Assert.Equal(ValidReceipt, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        // [2] treasury (writable)
        Assert.Equal(ValidTreasury, ix.Accounts[2].PublicKey);
        Assert.False(ix.Accounts[2].IsSigner);
        Assert.True(ix.Accounts[2].IsWritable);

        // [3] splits_vault (writable)
        Assert.Equal(ValidSplitsVault, ix.Accounts[3].PublicKey);
        Assert.False(ix.Accounts[3].IsSigner);
        Assert.True(ix.Accounts[3].IsWritable);

        // [4] system_program (readonly)
        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.Accounts[4].PublicKey);
        Assert.False(ix.Accounts[4].IsSigner);
        Assert.False(ix.Accounts[4].IsWritable);

        // Data check
        Assert.Equal(25, ix.Data.Length);
        Assert.Equal(amount, BitConverter.ToUInt64(ix.Data, 8));
        Assert.Equal((byte)MarketCurrency.Sol, ix.Data[16]);
        Assert.Equal(tip, BitConverter.ToUInt64(ix.Data, 17));
    }

    [Fact]
    public void BuildBuyLicenseInstruction_Usdc_OrdersAccountsStrictly()
    {
        string buyerAta = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        string treasuryAta = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        string splitsVaultAta = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        ulong amount = 10_000_000UL;
        ulong tip = 500_000UL;

        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            buyerTokenAccount: buyerAta,
            treasuryTokenAccount: treasuryAta,
            splitsVaultTokenAccount: splitsVaultAta,
            amount: amount,
            tipToPlatform: tip,
            marketProgramId: CustomProgramId,
            tokenProgramId: PlemuMarketTransactionBuilder.DefaultTokenProgramId);

        Assert.Equal(CustomProgramId, ix.ProgramId);
        Assert.Equal(7, ix.Accounts.Count);

        // [0] buyer (signer, writable)
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.True(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        // [1] receipt (writable)
        Assert.Equal(ValidReceipt, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        // [2] buyerTokenAccount (writable)
        Assert.Equal(buyerAta, ix.Accounts[2].PublicKey);
        Assert.False(ix.Accounts[2].IsSigner);
        Assert.True(ix.Accounts[2].IsWritable);

        // [3] treasuryTokenAccount (writable)
        Assert.Equal(treasuryAta, ix.Accounts[3].PublicKey);
        Assert.False(ix.Accounts[3].IsSigner);
        Assert.True(ix.Accounts[3].IsWritable);

        // [4] splitsVaultTokenAccount (writable)
        Assert.Equal(splitsVaultAta, ix.Accounts[4].PublicKey);
        Assert.False(ix.Accounts[4].IsSigner);
        Assert.True(ix.Accounts[4].IsWritable);

        // [5] tokenProgram (readonly)
        Assert.Equal(PlemuMarketTransactionBuilder.DefaultTokenProgramId, ix.Accounts[5].PublicKey);
        Assert.False(ix.Accounts[5].IsSigner);
        Assert.False(ix.Accounts[5].IsWritable);

        // [6] systemProgram (readonly)
        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.Accounts[6].PublicKey);
        Assert.False(ix.Accounts[6].IsSigner);
        Assert.False(ix.Accounts[6].IsWritable);

        Assert.Equal((byte)MarketCurrency.Usdc, ix.Data[16]);
    }

    [Fact]
    public void BuildSendTipInstruction_Sol_OrdersAccountsStrictly()
    {
        ulong amount = 1_000_000UL;
        ulong tip = 50_000UL;

        var ix = PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(
            tipper: ValidBuyer,
            recipient: ValidSplitsVault,
            treasury: ValidTreasury,
            amount: amount,
            tipToPlatform: tip,
            marketProgramId: CustomProgramId);

        Assert.Equal(CustomProgramId, ix.ProgramId);
        Assert.Equal(4, ix.Accounts.Count);
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.True(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        Assert.Equal(ValidSplitsVault, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        Assert.Equal(ValidTreasury, ix.Accounts[2].PublicKey);
        Assert.False(ix.Accounts[2].IsSigner);
        Assert.True(ix.Accounts[2].IsWritable);

        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.Accounts[3].PublicKey);
        Assert.False(ix.Accounts[3].IsSigner);
        Assert.False(ix.Accounts[3].IsWritable);

        Assert.Equal((byte)MarketCurrency.Sol, ix.Data[16]);
    }

    [Fact]
    public void BuildSendTipInstruction_Usdc_OrdersAccountsStrictly()
    {
        string tipperAta = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        string recipientAta = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        string treasuryAta = "xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu";
        ulong amount = 3_000_000UL;
        ulong tip = 0UL;

        var ix = PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(
            tipper: ValidBuyer,
            tipperTokenAccount: tipperAta,
            recipientTokenAccount: recipientAta,
            treasuryTokenAccount: treasuryAta,
            amount: amount,
            tipToPlatform: tip,
            marketProgramId: CustomProgramId,
            tokenProgramId: PlemuMarketTransactionBuilder.DefaultTokenProgramId);

        Assert.Equal(CustomProgramId, ix.ProgramId);
        Assert.Equal(6, ix.Accounts.Count);
        Assert.Equal(ValidBuyer, ix.Accounts[0].PublicKey);
        Assert.True(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        Assert.Equal(tipperAta, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        Assert.Equal(recipientAta, ix.Accounts[2].PublicKey);
        Assert.False(ix.Accounts[2].IsSigner);
        Assert.True(ix.Accounts[2].IsWritable);

        Assert.Equal(treasuryAta, ix.Accounts[3].PublicKey);
        Assert.False(ix.Accounts[3].IsSigner);
        Assert.True(ix.Accounts[3].IsWritable);

        Assert.Equal(PlemuMarketTransactionBuilder.DefaultTokenProgramId, ix.Accounts[4].PublicKey);
        Assert.False(ix.Accounts[4].IsSigner);
        Assert.False(ix.Accounts[4].IsWritable);

        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.Accounts[5].PublicKey);
        Assert.False(ix.Accounts[5].IsSigner);
        Assert.False(ix.Accounts[5].IsWritable);

        Assert.Equal((byte)MarketCurrency.Usdc, ix.Data[16]);
    }

    [Fact]
    public void BuildCreateAssociatedTokenAccountIdempotentInstruction_SetsAccountsAndData()
    {
        string funding = ValidBuyer;
        string wallet = ValidTreasury;
        string mint = ValidUsdcMint;
        string ata = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(wallet, mint);

        var ix = PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(
            fundingAccount: funding,
            associatedTokenAccount: ata,
            walletAddress: wallet,
            tokenMintAddress: mint,
            tokenProgramId: PlemuMarketTransactionBuilder.DefaultTokenProgramId,
            associatedTokenProgramId: PlemuMarketTransactionBuilder.DefaultAssociatedTokenProgramId);

        Assert.Equal(PlemuMarketTransactionBuilder.DefaultAssociatedTokenProgramId, ix.ProgramId);
        Assert.Single(ix.Data);
        Assert.Equal(1, ix.Data[0]); // 0x01 = CreateIdempotent

        Assert.Equal(6, ix.Accounts.Count);
        // funding (signer, writable)
        Assert.Equal(funding, ix.Accounts[0].PublicKey);
        Assert.True(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        // ata (writable)
        Assert.Equal(ata, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        // wallet (readonly)
        Assert.Equal(wallet, ix.Accounts[2].PublicKey);
        Assert.False(ix.Accounts[2].IsSigner);
        Assert.False(ix.Accounts[2].IsWritable);

        // mint (readonly)
        Assert.Equal(mint, ix.Accounts[3].PublicKey);
        Assert.False(ix.Accounts[3].IsSigner);
        Assert.False(ix.Accounts[3].IsWritable);

        // system program (readonly)
        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.Accounts[4].PublicKey);
        Assert.False(ix.Accounts[4].IsSigner);
        Assert.False(ix.Accounts[4].IsWritable);

        // token program (readonly)
        Assert.Equal(PlemuMarketTransactionBuilder.DefaultTokenProgramId, ix.Accounts[5].PublicKey);
        Assert.False(ix.Accounts[5].IsSigner);
        Assert.False(ix.Accounts[5].IsWritable);
    }

    [Fact]
    public void BuildSplTokenTransferInstruction_SetsAccountsAndData()
    {
        string source = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        string dest = "FmRd9UgYykiVCXhhJv53trpZvnKNAiF6BxEbZeCYRmkm";
        string auth = ValidBuyer;
        ulong amount = 5_000_000UL;

        var ix = PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction(
            source: source,
            destination: dest,
            authority: auth,
            amount: amount,
            tokenProgramId: PlemuMarketTransactionBuilder.DefaultTokenProgramId);

        Assert.Equal(PlemuMarketTransactionBuilder.DefaultTokenProgramId, ix.ProgramId);
        Assert.Equal(9, ix.Data.Length);
        Assert.Equal(3, ix.Data[0]); // 0x03 = Transfer
        Assert.Equal(amount, BitConverter.ToUInt64(ix.Data, 1));

        Assert.Equal(3, ix.Accounts.Count);
        // source (writable)
        Assert.Equal(source, ix.Accounts[0].PublicKey);
        Assert.False(ix.Accounts[0].IsSigner);
        Assert.True(ix.Accounts[0].IsWritable);

        // destination (writable)
        Assert.Equal(dest, ix.Accounts[1].PublicKey);
        Assert.False(ix.Accounts[1].IsSigner);
        Assert.True(ix.Accounts[1].IsWritable);

        // authority (signer)
        Assert.Equal(auth, ix.Accounts[2].PublicKey);
        Assert.True(ix.Accounts[2].IsSigner);
        Assert.False(ix.Accounts[2].IsWritable);
    }

    [Theory]
    [InlineData(0, new byte[] { 0 })]
    [InlineData(1, new byte[] { 1 })]
    [InlineData(127, new byte[] { 127 })]
    [InlineData(128, new byte[] { 0x80, 0x01 })]
    [InlineData(300, new byte[] { 0xAC, 0x02 })]
    [InlineData(16383, new byte[] { 0xFF, 0x7F })]
    [InlineData(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public void EncodeCompactU16_EncodesCorrectly(ushort value, byte[] expected)
    {
        var actual = PlemuMarketTransactionBuilder.EncodeCompactU16(value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SerializeMessage_SortsAccountsInSolanaFourBuckets()
    {
        string secondarySigner = "4PEcHxY379hy7BzJRvdqcKgFgXBf1FuPDBS47bm81JBL";
        // Setup transaction with multiple accounts in random order
        var ix = new SolanaInstruction(
            ProgramId: SolanaEntitlementOptions.DefaultSystemProgramId,
            Accounts: new List<SolanaAccountMeta>
            {
                new(ValidBuyer, IsSigner: true, IsWritable: true),
                new(secondarySigner, IsSigner: true, IsWritable: true),
                new(ValidReceipt, IsSigner: false, IsWritable: true),
                new(ValidTreasury, IsSigner: false, IsWritable: false),
                new(ValidSplitsVault, IsSigner: true, IsWritable: false)
            },
            Data: new byte[] { 1, 2, 3 }
        );

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        var messageBytes = PlemuMarketTransactionBuilder.SerializeMessage(tx);
        Assert.NotNull(messageBytes);
        Assert.True(messageBytes.Length > 0);

        // Verify Message Header:
        // byte 0: numRequiredSignatures = 3 (ValidBuyer + secondarySigner + ValidSplitsVault)
        // byte 1: numReadonlySignedAccounts = 1 (ValidSplitsVault)
        // byte 2: numReadonlyUnsignedAccounts = 2 (ValidTreasury + ProgramId)
        Assert.Equal(3, messageBytes[0]);
        Assert.Equal(1, messageBytes[1]);
        Assert.Equal(2, messageBytes[2]);
    }

    [Fact]
    public void SerializeMessage_EmptyInstructions_ThrowsArgumentException()
    {
        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: Array.Empty<SolanaInstruction>()
        );

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.SerializeMessage(tx));
    }

    [Fact]
    public void SerializeMessage_InvalidBlockhash_ThrowsArgumentException()
    {
        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, ValidTreasury, ValidSplitsVault, 1000);
        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: "invalid-blockhash",
            Instructions: new List<SolanaInstruction> { ix }
        );

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.SerializeMessage(tx));
    }

    [Fact]
    public void SerializeMessage_ExceedingMaxAccounts_ThrowsInvalidOperationException()
    {
        var accounts = new List<SolanaAccountMeta>();
        for (int i = 0; i < 65; i++)
        {
            byte[] keyBytes = new byte[32];
            BitConverter.TryWriteBytes(keyBytes, i + 1);
            string base58Key = Base58.Encode(keyBytes);
            accounts.Add(new SolanaAccountMeta(base58Key, IsSigner: false, IsWritable: false));
        }

        var ix = new SolanaInstruction(
            ProgramId: SolanaEntitlementOptions.DefaultSystemProgramId,
            Accounts: accounts,
            Data: new byte[] { 1 }
        );

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        var ex = Assert.Throws<InvalidOperationException>(() => PlemuMarketTransactionBuilder.SerializeMessage(tx));
        Assert.Contains("exceeding the Solana limit of 64 accounts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeTransaction_UnderMtuLimit_ProducesValidPayload()
    {
        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            treasury: ValidTreasury,
            splitsVault: ValidSplitsVault,
            amount: 10_000_000UL,
            tipToPlatform: 0UL);

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        var serialized = PlemuMarketTransactionBuilder.SerializeTransaction(tx);

        Assert.NotNull(serialized);
        Assert.InRange(serialized.Length, 1, PlemuMarketTransactionBuilder.MaxTransactionSize);

        // First byte is compact-u16 signature count = 1
        Assert.Equal(1, serialized[0]);

        // 64 bytes of default zero signatures since none was provided
        var sigBytes = serialized.Skip(1).Take(64).ToArray();
        Assert.All(sigBytes, b => Assert.Equal(0, b));

        // Message follows right after signatures (offset 65)
        Assert.Equal(1, serialized[65]); // numRequiredSignatures = 1
    }

    [Fact]
    public void SerializeTransaction_WithProvidedSignature_WritesSignatureBytes()
    {
        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            treasury: ValidTreasury,
            splitsVault: ValidSplitsVault,
            amount: 10_000_000UL);

        var customSig = new byte[64];
        customSig[0] = 0xDE;
        customSig[63] = 0xAD;

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix },
            Signatures: new List<byte[]> { customSig }
        );

        var serialized = PlemuMarketTransactionBuilder.SerializeTransaction(tx);
        var actualSig = serialized.Skip(1).Take(64).ToArray();

        Assert.Equal(customSig, actualSig);
    }

    [Fact]
    public void SerializeTransaction_InvalidSignatureLength_ThrowsArgumentException()
    {
        var ix = PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            treasury: ValidTreasury,
            splitsVault: ValidSplitsVault,
            amount: 10_000_000UL);

        var invalidSig = new byte[32]; // Not 64 bytes

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix },
            Signatures: new List<byte[]> { invalidSig }
        );

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.SerializeTransaction(tx));
    }

    [Fact]
    public void SerializeTransaction_ExceedingMtu_ThrowsInvalidOperationException()
    {
        // Create an instruction with data larger than MTU
        var hugeData = new byte[1300];
        var ix = new SolanaInstruction(
            ProgramId: SolanaEntitlementOptions.DefaultSystemProgramId,
            Accounts: new List<SolanaAccountMeta> { new(ValidBuyer, true, true) },
            Data: hugeData
        );

        var tx = new SolanaTransaction(
            FeePayer: ValidBuyer,
            RecentBlockhash: ValidBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        var ex = Assert.Throws<InvalidOperationException>(() => PlemuMarketTransactionBuilder.SerializeTransaction(tx));
        Assert.Contains("exceeds Solana MTU limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTransactionSize_ChecksMtuCorrectly()
    {
        Assert.True(PlemuMarketTransactionBuilder.ValidateTransactionSize(1232));
        Assert.True(PlemuMarketTransactionBuilder.ValidateTransactionSize(100));
        Assert.False(PlemuMarketTransactionBuilder.ValidateTransactionSize(1233));
        Assert.False(PlemuMarketTransactionBuilder.ValidateTransactionSize(0));
    }

    [Fact]
    public void BuildBuyLicenseTransaction_ConvenienceHelper_BuildsCompleteValidTransaction()
    {
        var serialized = PlemuMarketTransactionBuilder.BuildBuyLicenseTransaction(
            buyer: ValidBuyer,
            receipt: ValidReceipt,
            treasury: ValidTreasury,
            splitsVault: ValidSplitsVault,
            amount: 2_500_000UL,
            tipToPlatform: 100_000UL,
            recentBlockhash: ValidBlockhash,
            marketProgramId: CustomProgramId
        );

        Assert.NotNull(serialized);
        Assert.True(serialized.Length <= PlemuMarketTransactionBuilder.MaxTransactionSize);
    }

    [Fact]
    public void BuildSendTipTransaction_ConvenienceHelper_BuildsCompleteValidTransaction()
    {
        var serialized = PlemuMarketTransactionBuilder.BuildSendTipTransaction(
            tipper: ValidBuyer,
            recipient: ValidSplitsVault,
            treasury: ValidTreasury,
            amount: 1_000_000UL,
            tipToPlatform: 50_000UL,
            recentBlockhash: ValidBlockhash,
            marketProgramId: CustomProgramId
        );

        Assert.NotNull(serialized);
        Assert.True(serialized.Length <= PlemuMarketTransactionBuilder.MaxTransactionSize);
    }

    [Fact]
    public void DeriveAssociatedTokenAddress_ValidInputs_MatchesDeterministicPda()
    {
        var ata = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(ValidBuyer, ValidUsdcMint);

        Assert.NotNull(ata);
        Assert.True(Base58.TryDecode(ata, out var bytes));
        Assert.Equal(32, bytes.Length);

        // Same inputs return same output
        var ata2 = PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(ValidBuyer, ValidUsdcMint);
        Assert.Equal(ata, ata2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-base-58")]
    public void DeriveAssociatedTokenAddress_InvalidMandatoryInputs_ThrowsArgumentException(string? invalidInput)
    {
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(invalidInput!, ValidUsdcMint));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(ValidBuyer, invalidInput!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-base-58")]
    public void DeriveAssociatedTokenAddress_InvalidOptionalProgramInputs_ThrowsArgumentException(string invalidInput)
    {
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(ValidBuyer, ValidUsdcMint, tokenProgramId: invalidInput));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddress(ValidBuyer, ValidUsdcMint, associatedTokenProgramId: invalidInput));
    }

    [Fact]
    public void DeriveAssociatedTokenAddressInternal_BumpsExhausted_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PlemuMarketTransactionBuilder.DeriveAssociatedTokenAddressInternal(
                ValidBuyer,
                ValidUsdcMint,
                null,
                null,
                startBump: 0,
                endBump: 1));
    }

    [Fact]
    public void NullGuards_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.SerializeMessage(null!));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.SerializeTransaction(null!));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(null!, ValidReceipt, ValidTreasury, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, null!, ValidTreasury, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, null!, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, ValidTreasury, null!, 1000));

        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(null!, ValidReceipt, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, null!, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, null!, "ata2", "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", null!, "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", "ata2", null!, 1000));

        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(null!, ValidSplitsVault, ValidTreasury, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(ValidBuyer, null!, ValidTreasury, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(ValidBuyer, ValidSplitsVault, null!, 1000));

        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(null!, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, null!, "ata2", "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", null!, "ata3", 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", "ata2", null!, 1000));

        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(null!, "ata", ValidBuyer, ValidUsdcMint));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, null!, ValidBuyer, ValidUsdcMint));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", null!, ValidUsdcMint));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", ValidBuyer, null!));

        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction(null!, "dest", ValidBuyer, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction("src", null!, ValidBuyer, 1000));
        Assert.Throws<ArgumentNullException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction("src", "dest", null!, 1000));
    }

    [Fact]
    public void Base58Validation_ThrowsOnInvalidAddresses()
    {
        string invalid = "invalid-base-58-key";
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(invalid, ValidReceipt, ValidTreasury, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, invalid, ValidTreasury, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, invalid, ValidSplitsVault, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, ValidTreasury, invalid, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseSolInstruction(ValidBuyer, ValidReceipt, ValidTreasury, ValidSplitsVault, 1000, marketProgramId: invalid));

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(invalid, ValidReceipt, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, invalid, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, invalid, "ata2", "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", invalid, "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", "ata2", invalid, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", "ata2", "ata3", 1000, marketProgramId: invalid));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildBuyLicenseUsdcInstruction(ValidBuyer, ValidReceipt, "ata1", "ata2", "ata3", 1000, tokenProgramId: invalid));

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(invalid, ValidSplitsVault, ValidTreasury, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(ValidBuyer, invalid, ValidTreasury, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(ValidBuyer, ValidSplitsVault, invalid, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipSolInstruction(ValidBuyer, ValidSplitsVault, ValidTreasury, 1000, marketProgramId: invalid));

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(invalid, "ata1", "ata2", "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, invalid, "ata2", "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", invalid, "ata3", 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", "ata2", invalid, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", "ata2", "ata3", 1000, marketProgramId: invalid));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSendTipUsdcInstruction(ValidBuyer, "ata1", "ata2", "ata3", 1000, tokenProgramId: invalid));

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(invalid, "ata", ValidBuyer, ValidUsdcMint));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, invalid, ValidBuyer, ValidUsdcMint));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", invalid, ValidUsdcMint));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", ValidBuyer, invalid));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", ValidBuyer, ValidUsdcMint, associatedTokenProgramId: invalid));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildCreateAssociatedTokenAccountIdempotentInstruction(ValidBuyer, "ata", ValidBuyer, ValidUsdcMint, tokenProgramId: invalid));

        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction(invalid, "dest", ValidBuyer, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction("src", invalid, ValidBuyer, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction("src", "dest", invalid, 1000));
        Assert.Throws<ArgumentException>(() => PlemuMarketTransactionBuilder.BuildSplTokenTransferInstruction("src", "dest", ValidBuyer, 1000, tokenProgramId: invalid));
    }

    [Fact]
    public void SolanaTransactionModels_PropertiesAndRecordsWork()
    {
        var meta = new SolanaAccountMeta(ValidBuyer, IsSigner: true, IsWritable: true);
        Assert.Equal(ValidBuyer, meta.PublicKey);
        Assert.True(meta.IsSigner);
        Assert.True(meta.IsWritable);

        var ix = new SolanaInstruction(SolanaEntitlementOptions.DefaultSystemProgramId, new[] { meta }, new byte[] { 42 });
        Assert.Equal(SolanaEntitlementOptions.DefaultSystemProgramId, ix.ProgramId);
        Assert.Single(ix.Accounts);
        Assert.Equal(new byte[] { 42 }, ix.Data);

        var tx = new SolanaTransaction(ValidBuyer, ValidBlockhash, new[] { ix }, new[] { new byte[64] });
        Assert.Equal(ValidBuyer, tx.FeePayer);
        Assert.Equal(ValidBlockhash, tx.RecentBlockhash);
        Assert.Single(tx.Instructions);
        Assert.NotNull(tx.Signatures);
        Assert.Single(tx.Signatures);
    }
}
