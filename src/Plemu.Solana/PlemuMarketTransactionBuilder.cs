using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana;

public static class PlemuMarketTransactionBuilder
{
    public const int MaxTransactionSize = 1232;
    public const int MaxAccounts = 64;
    public const int SignatureLength = 64;
    public const int PublicKeyLength = 32;

    public const string DefaultMarketProgramId = SolanaEntitlementOptions.DefaultMarketProgramId;
    public const string DefaultSystemProgramId = SolanaEntitlementOptions.DefaultSystemProgramId;
    public const string DefaultTokenProgramId = SolanaEntitlementOptions.DefaultTokenProgramId;
    public const string DefaultAssociatedTokenProgramId = SolanaEntitlementOptions.DefaultAssociatedTokenProgramId;
    public const string DefaultUsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    private const string AnchorGlobalPrefix = "global:";
    private const string BuyLicenseMethod = "buy_license";
    private const string SendTipMethod = "send_tip";
    private const string PdaMarker = "ProgramDerivedAddress";
    private const string InvalidBase58Error = "Public key must be a valid 32-byte Base58 string.";

    public static byte[] BuyLicenseDiscriminator { get; } = ComputeAnchorDiscriminator(BuyLicenseMethod);
    public static byte[] SendTipDiscriminator { get; } = ComputeAnchorDiscriminator(SendTipMethod);

    public static byte[] ComputeAnchorDiscriminator(string instructionName)
    {
        if (string.IsNullOrWhiteSpace(instructionName))
        {
            throw new ArgumentException("Instruction name cannot be null or empty.", nameof(instructionName));
        }

        byte[] preimage = Encoding.UTF8.GetBytes(AnchorGlobalPrefix + instructionName);
        byte[] hash = SHA256.HashData(preimage);

        byte[] discriminator = new byte[8];
        Array.Copy(hash, 0, discriminator, 0, 8);
        return discriminator;
    }

    public static byte[] BuildBuyLicenseData(ulong amount, MarketCurrency currency, ulong tipToPlatform)
    {
        byte[] data = new byte[25];
        Array.Copy(BuyLicenseDiscriminator, 0, data, 0, 8);
        BitConverter.TryWriteBytes(data.AsSpan(8, 8), amount);
        data[16] = (byte)currency;
        BitConverter.TryWriteBytes(data.AsSpan(17, 8), tipToPlatform);
        return data;
    }

    public static byte[] BuildSendTipData(ulong amount, MarketCurrency currency, ulong tipToPlatform)
    {
        byte[] data = new byte[25];
        Array.Copy(SendTipDiscriminator, 0, data, 0, 8);
        BitConverter.TryWriteBytes(data.AsSpan(8, 8), amount);
        data[16] = (byte)currency;
        BitConverter.TryWriteBytes(data.AsSpan(17, 8), tipToPlatform);
        return data;
    }

    public static SolanaInstruction BuildBuyLicenseSolInstruction(
        string buyer,
        string receipt,
        string treasury,
        string splitsVault,
        ulong amount,
        ulong tipToPlatform = 0,
        string? marketProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(splitsVault);

        ValidateBase58(buyer, nameof(buyer));
        ValidateBase58(receipt, nameof(receipt));
        ValidateBase58(treasury, nameof(treasury));
        ValidateBase58(splitsVault, nameof(splitsVault));

        string programId = marketProgramId ?? DefaultMarketProgramId;
        ValidateBase58(programId, nameof(marketProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(buyer, IsSigner: true, IsWritable: true),
            new(receipt, IsSigner: false, IsWritable: true),
            new(treasury, IsSigner: false, IsWritable: true),
            new(splitsVault, IsSigner: false, IsWritable: true),
            new(DefaultSystemProgramId, IsSigner: false, IsWritable: false)
        };

        byte[] data = BuildBuyLicenseData(amount, MarketCurrency.Sol, tipToPlatform);
        return new SolanaInstruction(programId, accounts, data);
    }

    public static SolanaInstruction BuildBuyLicenseUsdcInstruction(
        string buyer,
        string receipt,
        string buyerTokenAccount,
        string treasuryTokenAccount,
        string splitsVaultTokenAccount,
        ulong amount,
        ulong tipToPlatform = 0,
        string? marketProgramId = null,
        string? tokenProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(buyerTokenAccount);
        ArgumentNullException.ThrowIfNull(treasuryTokenAccount);
        ArgumentNullException.ThrowIfNull(splitsVaultTokenAccount);

        ValidateBase58(buyer, nameof(buyer));
        ValidateBase58(receipt, nameof(receipt));
        ValidateBase58(buyerTokenAccount, nameof(buyerTokenAccount));
        ValidateBase58(treasuryTokenAccount, nameof(treasuryTokenAccount));
        ValidateBase58(splitsVaultTokenAccount, nameof(splitsVaultTokenAccount));

        string programId = marketProgramId ?? DefaultMarketProgramId;
        string tokenProgId = tokenProgramId ?? DefaultTokenProgramId;

        ValidateBase58(programId, nameof(marketProgramId));
        ValidateBase58(tokenProgId, nameof(tokenProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(buyer, IsSigner: true, IsWritable: true),
            new(receipt, IsSigner: false, IsWritable: true),
            new(buyerTokenAccount, IsSigner: false, IsWritable: true),
            new(treasuryTokenAccount, IsSigner: false, IsWritable: true),
            new(splitsVaultTokenAccount, IsSigner: false, IsWritable: true),
            new(tokenProgId, IsSigner: false, IsWritable: false),
            new(DefaultSystemProgramId, IsSigner: false, IsWritable: false)
        };

        byte[] data = BuildBuyLicenseData(amount, MarketCurrency.Usdc, tipToPlatform);
        return new SolanaInstruction(programId, accounts, data);
    }

    public static SolanaInstruction BuildSendTipSolInstruction(
        string tipper,
        string recipient,
        string treasury,
        ulong amount,
        ulong tipToPlatform = 0,
        string? marketProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(tipper);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(treasury);

        ValidateBase58(tipper, nameof(tipper));
        ValidateBase58(recipient, nameof(recipient));
        ValidateBase58(treasury, nameof(treasury));

        string programId = marketProgramId ?? DefaultMarketProgramId;
        ValidateBase58(programId, nameof(marketProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(tipper, IsSigner: true, IsWritable: true),
            new(recipient, IsSigner: false, IsWritable: true),
            new(treasury, IsSigner: false, IsWritable: true),
            new(DefaultSystemProgramId, IsSigner: false, IsWritable: false)
        };

        byte[] data = BuildSendTipData(amount, MarketCurrency.Sol, tipToPlatform);
        return new SolanaInstruction(programId, accounts, data);
    }

    public static SolanaInstruction BuildSendTipUsdcInstruction(
        string tipper,
        string tipperTokenAccount,
        string recipientTokenAccount,
        string treasuryTokenAccount,
        ulong amount,
        ulong tipToPlatform = 0,
        string? marketProgramId = null,
        string? tokenProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(tipper);
        ArgumentNullException.ThrowIfNull(tipperTokenAccount);
        ArgumentNullException.ThrowIfNull(recipientTokenAccount);
        ArgumentNullException.ThrowIfNull(treasuryTokenAccount);

        ValidateBase58(tipper, nameof(tipper));
        ValidateBase58(tipperTokenAccount, nameof(tipperTokenAccount));
        ValidateBase58(recipientTokenAccount, nameof(recipientTokenAccount));
        ValidateBase58(treasuryTokenAccount, nameof(treasuryTokenAccount));

        string programId = marketProgramId ?? DefaultMarketProgramId;
        string tokenProgId = tokenProgramId ?? DefaultTokenProgramId;

        ValidateBase58(programId, nameof(marketProgramId));
        ValidateBase58(tokenProgId, nameof(tokenProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(tipper, IsSigner: true, IsWritable: true),
            new(tipperTokenAccount, IsSigner: false, IsWritable: true),
            new(recipientTokenAccount, IsSigner: false, IsWritable: true),
            new(treasuryTokenAccount, IsSigner: false, IsWritable: true),
            new(tokenProgId, IsSigner: false, IsWritable: false),
            new(DefaultSystemProgramId, IsSigner: false, IsWritable: false)
        };

        byte[] data = BuildSendTipData(amount, MarketCurrency.Usdc, tipToPlatform);
        return new SolanaInstruction(programId, accounts, data);
    }

    public static SolanaInstruction BuildCreateAssociatedTokenAccountIdempotentInstruction(
        string fundingAccount,
        string associatedTokenAccount,
        string walletAddress,
        string tokenMintAddress,
        string? tokenProgramId = null,
        string? associatedTokenProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(fundingAccount);
        ArgumentNullException.ThrowIfNull(associatedTokenAccount);
        ArgumentNullException.ThrowIfNull(walletAddress);
        ArgumentNullException.ThrowIfNull(tokenMintAddress);

        ValidateBase58(fundingAccount, nameof(fundingAccount));
        ValidateBase58(associatedTokenAccount, nameof(associatedTokenAccount));
        ValidateBase58(walletAddress, nameof(walletAddress));
        ValidateBase58(tokenMintAddress, nameof(tokenMintAddress));

        string ataProgId = associatedTokenProgramId ?? DefaultAssociatedTokenProgramId;
        string tokenProgId = tokenProgramId ?? DefaultTokenProgramId;

        ValidateBase58(ataProgId, nameof(associatedTokenProgramId));
        ValidateBase58(tokenProgId, nameof(tokenProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(fundingAccount, IsSigner: true, IsWritable: true),
            new(associatedTokenAccount, IsSigner: false, IsWritable: true),
            new(walletAddress, IsSigner: false, IsWritable: false),
            new(tokenMintAddress, IsSigner: false, IsWritable: false),
            new(DefaultSystemProgramId, IsSigner: false, IsWritable: false),
            new(tokenProgId, IsSigner: false, IsWritable: false)
        };

        byte[] data = new byte[] { 1 }; // 1 = CreateIdempotent
        return new SolanaInstruction(ataProgId, accounts, data);
    }

    public static SolanaInstruction BuildSplTokenTransferInstruction(
        string source,
        string destination,
        string authority,
        ulong amount,
        string? tokenProgramId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(authority);

        ValidateBase58(source, nameof(source));
        ValidateBase58(destination, nameof(destination));
        ValidateBase58(authority, nameof(authority));

        string progId = tokenProgramId ?? DefaultTokenProgramId;
        ValidateBase58(progId, nameof(tokenProgramId));

        var accounts = new List<SolanaAccountMeta>
        {
            new(source, IsSigner: false, IsWritable: true),
            new(destination, IsSigner: false, IsWritable: true),
            new(authority, IsSigner: true, IsWritable: false)
        };

        byte[] data = new byte[9];
        data[0] = 3; // 3 = Transfer
        BitConverter.TryWriteBytes(data.AsSpan(1, 8), amount);

        return new SolanaInstruction(progId, accounts, data);
    }

    public static string DeriveAssociatedTokenAddress(
        string walletAddress,
        string tokenMintAddress,
        string? tokenProgramId = null,
        string? associatedTokenProgramId = null) =>
        DeriveAssociatedTokenAddressInternal(walletAddress, tokenMintAddress, tokenProgramId, associatedTokenProgramId, 255, 0);

    internal static string DeriveAssociatedTokenAddressInternal(
        string walletAddress,
        string tokenMintAddress,
        string? tokenProgramId,
        string? associatedTokenProgramId,
        int startBump,
        int endBump)
    {
        if (string.IsNullOrWhiteSpace(walletAddress) || !Base58.TryDecode(walletAddress, out var walletBytes) || walletBytes.Length != PublicKeyLength)
        {
            throw new ArgumentException(InvalidBase58Error, nameof(walletAddress));
        }

        if (string.IsNullOrWhiteSpace(tokenMintAddress) || !Base58.TryDecode(tokenMintAddress, out var mintBytes) || mintBytes.Length != PublicKeyLength)
        {
            throw new ArgumentException(InvalidBase58Error, nameof(tokenMintAddress));
        }

        string ataProgId = associatedTokenProgramId ?? DefaultAssociatedTokenProgramId;
        string tokenProgId = tokenProgramId ?? DefaultTokenProgramId;

        if (!Base58.TryDecode(ataProgId, out var ataProgBytes) || ataProgBytes.Length != PublicKeyLength)
        {
            throw new ArgumentException(InvalidBase58Error, nameof(associatedTokenProgramId));
        }

        if (!Base58.TryDecode(tokenProgId, out var tokenProgBytes) || tokenProgBytes.Length != PublicKeyLength)
        {
            throw new ArgumentException(InvalidBase58Error, nameof(tokenProgramId));
        }

        byte[] pdaMarkerBytes = Encoding.ASCII.GetBytes(PdaMarker);
        // Seeds: wallet (32) + tokenProg (32) + mint (32) + bump (1) + ataProg (32) + PdaMarker (21)
        byte[] buffer = new byte[PublicKeyLength * 4 + 1 + pdaMarkerBytes.Length];
        int offset = 0;

        walletBytes.CopyTo(buffer, offset);
        offset += PublicKeyLength;

        tokenProgBytes.CopyTo(buffer, offset);
        offset += PublicKeyLength;

        mintBytes.CopyTo(buffer, offset);
        offset += PublicKeyLength;

        int bumpOffset = offset;
        offset += 1;

        ataProgBytes.CopyTo(buffer, offset);
        offset += PublicKeyLength;

        pdaMarkerBytes.CopyTo(buffer, offset);

        for (int bump = startBump; bump >= endBump; bump--)
        {
            buffer[bumpOffset] = (byte)bump;
            byte[] hash = SHA256.HashData(buffer);

            if (!DefaultSeatPdaDeriver.IsOnCurve(hash))
            {
                return Base58.Encode(hash);
            }
        }

        throw new InvalidOperationException("Could not find a valid Associated Token Address for the given seeds.");
    }

    public static byte[] EncodeCompactU16(ushort value)
    {
        if (value <= 0x7F)
        {
            return new byte[] { (byte)value };
        }

        if (value <= 0x3FFF)
        {
            return new byte[]
            {
                (byte)((value & 0x7F) | 0x80),
                (byte)(value >> 7)
            };
        }

        return new byte[]
        {
            (byte)((value & 0x7F) | 0x80),
            (byte)(((value >> 7) & 0x7F) | 0x80),
            (byte)(value >> 14)
        };
    }

    public static bool ValidateTransactionSize(int size) => size > 0 && size <= MaxTransactionSize;

    public static byte[] SerializeMessage(SolanaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(transaction.FeePayer);
        ArgumentNullException.ThrowIfNull(transaction.RecentBlockhash);
        ArgumentNullException.ThrowIfNull(transaction.Instructions);

        if (transaction.Instructions.Count == 0)
        {
            throw new ArgumentException("Transaction must contain at least one instruction.", nameof(transaction));
        }

        ValidateBase58(transaction.FeePayer, nameof(transaction.FeePayer));
        if (!Base58.TryDecode(transaction.RecentBlockhash, out var blockhashBytes) || blockhashBytes.Length != PublicKeyLength)
        {
            throw new ArgumentException("Recent blockhash must be a valid 32-byte Base58 string.", nameof(transaction.RecentBlockhash));
        }

        // Collect and classify all accounts
        // Map: PublicKey -> (isSigner, isWritable)
        var accountMap = new Dictionary<string, (bool IsSigner, bool IsWritable)>(StringComparer.Ordinal);

        // FeePayer is always signer and writable
        accountMap[transaction.FeePayer] = (true, true);

        foreach (var ix in transaction.Instructions)
        {
            ValidateBase58(ix.ProgramId, nameof(ix.ProgramId));
            if (!accountMap.ContainsKey(ix.ProgramId))
            {
                // Program IDs are unsigned, readonly
                accountMap[ix.ProgramId] = (false, false);
            }

            foreach (var acc in ix.Accounts)
            {
                ValidateBase58(acc.PublicKey, nameof(acc.PublicKey));
                if (accountMap.TryGetValue(acc.PublicKey, out var existing))
                {
                    // Merge privileges (once a signer, always a signer; once writable, always writable)
                    accountMap[acc.PublicKey] = (existing.IsSigner || acc.IsSigner, existing.IsWritable || acc.IsWritable);
                }
                else
                {
                    accountMap[acc.PublicKey] = (acc.IsSigner, acc.IsWritable);
                }
            }
        }

        if (accountMap.Count > MaxAccounts)
        {
            throw new InvalidOperationException($"Transaction contains {accountMap.Count} accounts, exceeding the Solana limit of {MaxAccounts} accounts.");
        }

        // Partition into 4 Solana account categories:
        // 1. Signers, Writable (Fee payer first)
        // 2. Signers, Read-only
        // 3. Non-Signers, Writable
        // 4. Non-Signers, Read-only
        var bucket1 = new List<string> { transaction.FeePayer };
        var bucket2 = new List<string>();
        var bucket3 = new List<string>();
        var bucket4 = new List<string>();

        foreach (var (key, meta) in accountMap)
        {
            if (string.Equals(key, transaction.FeePayer, StringComparison.Ordinal))
            {
                continue;
            }

            if (meta.IsSigner)
            {
                if (meta.IsWritable)
                {
                    bucket1.Add(key);
                }
                else
                {
                    bucket2.Add(key);
                }
            }
            else
            {
                if (meta.IsWritable)
                {
                    bucket3.Add(key);
                }
                else
                {
                    bucket4.Add(key);
                }
            }
        }

        var orderedAccounts = new List<string>(bucket1.Count + bucket2.Count + bucket3.Count + bucket4.Count);
        orderedAccounts.AddRange(bucket1);
        orderedAccounts.AddRange(bucket2);
        orderedAccounts.AddRange(bucket3);
        orderedAccounts.AddRange(bucket4);

        var accountIndexMap = new Dictionary<string, byte>(StringComparer.Ordinal);
        for (int i = 0; i < orderedAccounts.Count; i++)
        {
            accountIndexMap[orderedAccounts[i]] = (byte)i;
        }

        using var ms = new MemoryStream();

        // 1. Header (3 bytes)
        ms.WriteByte((byte)(bucket1.Count + bucket2.Count)); // numRequiredSignatures
        ms.WriteByte((byte)bucket2.Count);                   // numReadonlySignedAccounts
        ms.WriteByte((byte)bucket4.Count);                   // numReadonlyUnsignedAccounts

        // 2. Account Keys
        byte[] accountCountEncoded = EncodeCompactU16((ushort)orderedAccounts.Count);
        ms.Write(accountCountEncoded, 0, accountCountEncoded.Length);
        foreach (var accKey in orderedAccounts)
        {
            byte[] keyBytes = Base58.Decode(accKey);
            ms.Write(keyBytes, 0, PublicKeyLength);
        }

        // 3. Recent Blockhash (32 bytes)
        ms.Write(blockhashBytes, 0, PublicKeyLength);

        // 4. Compiled Instructions
        byte[] ixCountEncoded = EncodeCompactU16((ushort)transaction.Instructions.Count);
        ms.Write(ixCountEncoded, 0, ixCountEncoded.Length);

        foreach (var ix in transaction.Instructions)
        {
            // Program ID index
            byte progIndex = accountIndexMap[ix.ProgramId];
            ms.WriteByte(progIndex);

            // Account indices
            byte[] ixAccountsCountEncoded = EncodeCompactU16((ushort)ix.Accounts.Count);
            ms.Write(ixAccountsCountEncoded, 0, ixAccountsCountEncoded.Length);
            foreach (var acc in ix.Accounts)
            {
                ms.WriteByte(accountIndexMap[acc.PublicKey]);
            }

            // Instruction Data
            byte[] dataLenEncoded = EncodeCompactU16((ushort)ix.Data.Length);
            ms.Write(dataLenEncoded, 0, dataLenEncoded.Length);
            if (ix.Data.Length > 0)
            {
                ms.Write(ix.Data, 0, ix.Data.Length);
            }
        }

        return ms.ToArray();
    }

    public static byte[] SerializeTransaction(SolanaTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        byte[] messageBytes = SerializeMessage(transaction);
        int numRequiredSignatures = messageBytes[0];

        var signatures = new List<byte[]>(numRequiredSignatures);
        if (transaction.Signatures != null && transaction.Signatures.Count > 0)
        {
            foreach (var sig in transaction.Signatures)
            {
                if (sig.Length != SignatureLength)
                {
                    throw new ArgumentException($"Signature must be exactly {SignatureLength} bytes.", nameof(transaction.Signatures));
                }
                signatures.Add(sig);
            }
        }

        while (signatures.Count < numRequiredSignatures)
        {
            signatures.Add(new byte[SignatureLength]);
        }

        using var ms = new MemoryStream();
        byte[] sigCountEncoded = EncodeCompactU16((ushort)signatures.Count);
        ms.Write(sigCountEncoded, 0, sigCountEncoded.Length);

        foreach (var sig in signatures)
        {
            ms.Write(sig, 0, SignatureLength);
        }

        ms.Write(messageBytes, 0, messageBytes.Length);

        byte[] serialized = ms.ToArray();
        if (serialized.Length > MaxTransactionSize)
        {
            throw new InvalidOperationException($"Serialized transaction size ({serialized.Length} bytes) exceeds Solana MTU limit of {MaxTransactionSize} bytes.");
        }

        return serialized;
    }

    public static byte[] BuildBuyLicenseTransaction(
        string buyer,
        string receipt,
        string treasury,
        string splitsVault,
        ulong amount,
        ulong tipToPlatform,
        string recentBlockhash,
        string? marketProgramId = null)
    {
        var ix = BuildBuyLicenseSolInstruction(buyer, receipt, treasury, splitsVault, amount, tipToPlatform, marketProgramId);
        var tx = new SolanaTransaction(
            FeePayer: buyer,
            RecentBlockhash: recentBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        return SerializeTransaction(tx);
    }

    public static byte[] BuildSendTipTransaction(
        string tipper,
        string recipient,
        string treasury,
        ulong amount,
        ulong tipToPlatform,
        string recentBlockhash,
        string? marketProgramId = null)
    {
        var ix = BuildSendTipSolInstruction(tipper, recipient, treasury, amount, tipToPlatform, marketProgramId);
        var tx = new SolanaTransaction(
            FeePayer: tipper,
            RecentBlockhash: recentBlockhash,
            Instructions: new List<SolanaInstruction> { ix }
        );

        return SerializeTransaction(tx);
    }

    private static void ValidateBase58(string input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input) || !Base58.TryDecode(input, out var bytes) || bytes.Length != PublicKeyLength)
        {
            throw new ArgumentException(InvalidBase58Error, paramName);
        }
    }
}
