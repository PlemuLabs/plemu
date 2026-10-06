using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana;

public class SolanaEntitlementService : IEntitlementService, IDisposable
{
    // 8-byte Anchor discriminator for `Seat` struct: sha256("account:Seat")[0..8]
    public static readonly byte[] SeatDiscriminator = [90, 228, 22, 90, 162, 86, 173, 26];

    // 8-byte Anchor discriminator for `Receipt` struct: sha256("account:Receipt")[0..8]
    public static readonly byte[] ReceiptDiscriminator = SHA256.HashData(Encoding.UTF8.GetBytes("account:Receipt"))[..8];

    private const int MinBase58KeyLength = 32;
    private const int MaxBase58KeyLength = 44;
    private const int PublicKeyByteLength = 32;
    private const int MinSeatAccountDataLength = 72;
    private const int PackRevocationOffset = 72;
    private const int MinReceiptAccountDataLength = 90;
    private const int ReceiptRevocationOffset = 89;
    private const int MinMetaplexAssetDataLength = 33;
    private const int MetaplexAssetOwnerOffset = 1;

    private readonly ISolanaRpcClient _rpcClient;
    private readonly ISeatPdaDeriver _pdaDeriver;
    private readonly IReceiptPdaDeriver _receiptPdaDeriver;
    private readonly SolanaEntitlementOptions _options;
    private readonly ILogger<SolanaEntitlementService>? _logger;
    private readonly bool _ownsRpcClient;

    public SolanaEntitlementService(
        ISolanaRpcClient rpcClient,
        ISeatPdaDeriver? pdaDeriver = null,
        SolanaEntitlementOptions? options = null,
        ILogger<SolanaEntitlementService>? logger = null,
        IReceiptPdaDeriver? receiptPdaDeriver = null)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
        _pdaDeriver = pdaDeriver ?? DefaultSeatPdaDeriver.Instance;
        _receiptPdaDeriver = receiptPdaDeriver ?? (_pdaDeriver as IReceiptPdaDeriver ?? DefaultReceiptPdaDeriver.Instance);
        _options = options ?? new SolanaEntitlementOptions();
        _logger = logger;
        _ownsRpcClient = false;
    }

    public SolanaEntitlementService()
        : this(new SolanaRpcClient(), DefaultSeatPdaDeriver.Instance, new SolanaEntitlementOptions(), null, DefaultReceiptPdaDeriver.Instance)
    {
        _ownsRpcClient = true;
    }

    public Task<bool> CanAccessPack(WalletIdentity wallet, string packId) =>
        CanAccessPackAsync(wallet, packId, CancellationToken.None);

    public async Task<bool> CanAccessPackAsync(WalletIdentity wallet, string packId, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed validation for inputs
        if (wallet == null)
        {
            _logger?.LogWarning("Entitlement verification rejected: wallet identity is null.");
            return false;
        }

        if (!wallet.IsValid)
        {
            _logger?.LogWarning("Entitlement verification rejected: wallet public key '{PublicKey}' is invalid.", wallet.PublicKey);
            return false;
        }

        if (string.IsNullOrWhiteSpace(packId) || packId.Length < MinBase58KeyLength || packId.Length > MaxBase58KeyLength)
        {
            _logger?.LogWarning("Entitlement verification rejected: pack ID '{PackId}' is invalid.", packId);
            return false;
        }

        if (!Base58.TryDecode(wallet.PublicKey, out var walletBytes) || walletBytes.Length != PublicKeyByteLength)
        {
            _logger?.LogWarning("Entitlement verification rejected: wallet '{Wallet}' failed Base58 32-byte decoding.", wallet.PublicKey);
            return false;
        }

        if (!Base58.TryDecode(packId, out var packBytes) || packBytes.Length != PublicKeyByteLength)
        {
            _logger?.LogWarning("Entitlement verification rejected: pack ID '{PackId}' failed Base58 32-byte decoding.", packId);
            return false;
        }

        // 2. Derive Seat PDA on-chain address
        string seatPda;
        try
        {
            seatPda = _pdaDeriver.DeriveSeatAddress(_options.ProgramId, packId, wallet.PublicKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to derive seat PDA for pack '{PackId}' and wallet '{Wallet}'.", packId, wallet.PublicKey);
            return false;
        }

        // 3. Query on-chain accounts in a single slot
        IReadOnlyList<SolanaAccountInfo?>? accounts;
        try
        {
            accounts = await _rpcClient.GetMultipleAccountsAsync(new[] { seatPda, packId }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RPC failure querying seat '{SeatPda}' and pack '{PackId}'. Failing closed.", seatPda, packId);
            return false;
        }

        if (accounts == null || accounts.Count < 2)
        {
            _logger?.LogWarning("RPC returned null or insufficient account information for seat '{SeatPda}' and pack '{PackId}'.", seatPda, packId);
            return false;
        }

        var seatAccount = accounts[0];
        var packAccount = accounts[1];

        // 4. Validate Pack Account
        if (packAccount == null)
        {
            _logger?.LogWarning("Pack account '{PackId}' does not exist on chain.", packId);
            return false;
        }

        if (!string.Equals(packAccount.Owner, _options.ProgramId, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Pack account '{PackId}' owner '{Owner}' does not match expected program '{ProgramId}'.", packId, packAccount.Owner, _options.ProgramId);
            return false;
        }

        if (packAccount.Data.Length > PackRevocationOffset && packAccount.Data[PackRevocationOffset] == 1)
        {
            _logger?.LogWarning("Pack '{PackId}' has been revoked on chain.", packId);
            return false;
        }

        // 5. Anti-Dusting & Seat Account Validation
        if (seatAccount == null)
        {
            _logger?.LogInformation("Seat account '{SeatPda}' not found on chain for pack '{PackId}' and wallet '{Wallet}'.", seatPda, packId, wallet.PublicKey);
            return false;
        }

        if (!string.Equals(seatAccount.Owner, _options.ProgramId, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Seat account '{SeatPda}' owner '{Owner}' does not match program '{ProgramId}' (potential dusting attack).", seatPda, seatAccount.Owner, _options.ProgramId);
            return false;
        }

        if (seatAccount.Data.Length < MinSeatAccountDataLength)
        {
            _logger?.LogWarning("Seat account '{SeatPda}' data length ({Length}) is below minimum {MinLength}.", seatPda, seatAccount.Data.Length, MinSeatAccountDataLength);
            return false;
        }

        // Verify discriminator
        if (!seatAccount.Data.AsSpan(0, SeatDiscriminator.Length).SequenceEqual(SeatDiscriminator))
        {
            _logger?.LogWarning("Seat account '{SeatPda}' has invalid discriminator.", seatPda);
            return false;
        }

        // Verify member (offset 8, length 32)
        if (!seatAccount.Data.AsSpan(8, PublicKeyByteLength).SequenceEqual(walletBytes))
        {
            _logger?.LogWarning("Seat account '{SeatPda}' member does not match wallet '{Wallet}'.", seatPda, wallet.PublicKey);
            return false;
        }

        // Verify pack (offset 40, length 32)
        if (!seatAccount.Data.AsSpan(40, PublicKeyByteLength).SequenceEqual(packBytes))
        {
            _logger?.LogWarning("Seat account '{SeatPda}' pack does not match pack ID '{PackId}'.", seatPda, packId);
            return false;
        }

        _logger?.LogInformation("Access granted: on-chain seat '{SeatPda}' verified for wallet '{Wallet}' and pack '{PackId}'.", seatPda, wallet.PublicKey, packId);
        return true;
    }

    public Task<bool> CanAccessEntry(WalletIdentity wallet, string entrySha256) =>
        CanAccessEntryAsync(wallet, entrySha256, CancellationToken.None);

    public async Task<bool> CanAccessEntryAsync(WalletIdentity wallet, string entrySha256, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed input validations
        if (wallet == null)
        {
            _logger?.LogWarning("Entry access rejected: wallet identity is null.");
            return false;
        }

        if (!wallet.IsValid)
        {
            _logger?.LogWarning("Entry access rejected: wallet public key '{PublicKey}' is invalid.", wallet.PublicKey);
            return false;
        }

        if (string.IsNullOrWhiteSpace(entrySha256))
        {
            _logger?.LogWarning("Entry access rejected: entry SHA-256 is null or whitespace.");
            return false;
        }

        if (!Base58.TryDecode(wallet.PublicKey, out var walletBytes) || walletBytes.Length != PublicKeyByteLength)
        {
            _logger?.LogWarning("Entry access rejected: wallet '{Wallet}' failed Base58 32-byte decoding.", wallet.PublicKey);
            return false;
        }

        byte[]? artifactBytes = null;
        if (entrySha256.Length == 64)
        {
            try
            {
                artifactBytes = Convert.FromHexString(entrySha256);
            }
            catch (FormatException)
            {
                // Fall-through to null check
            }
        }
        else if (Base58.TryDecode(entrySha256, out var b58Bytes) && b58Bytes.Length == PublicKeyByteLength)
        {
            artifactBytes = b58Bytes;
        }

        if (artifactBytes == null || artifactBytes.Length != PublicKeyByteLength)
        {
            _logger?.LogWarning("Entry access rejected: entry SHA-256 '{Sha256}' is invalid.", entrySha256);
            return false;
        }

        // 2. Derive Receipt PDA address on-chain
        string receiptPda;
        try
        {
            receiptPda = _receiptPdaDeriver.DeriveReceiptAddress(_options.MarketProgramId, artifactBytes, wallet.PublicKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to derive receipt PDA for entry '{Sha256}' and wallet '{Wallet}'.", entrySha256, wallet.PublicKey);
            return false;
        }

        // 3. Query Receipt PDA account
        SolanaAccountInfo? receiptAccount;
        try
        {
            receiptAccount = await _rpcClient.GetAccountInfoAsync(receiptPda, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RPC failure querying receipt account '{ReceiptPda}'. Failing closed.", receiptPda);
            return false;
        }

        // 4. If Receipt PDA account exists, validate on-chain license
        if (receiptAccount != null)
        {
            if (!string.Equals(receiptAccount.Owner, _options.MarketProgramId, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' owner '{Owner}' does not match expected market program '{ProgramId}'.", receiptPda, receiptAccount.Owner, _options.MarketProgramId);
                return false;
            }

            if (receiptAccount.Data.Length < MinReceiptAccountDataLength)
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' data length ({Length}) is below minimum {MinLength}.", receiptPda, receiptAccount.Data.Length, MinReceiptAccountDataLength);
                return false;
            }

            // Verify discriminator (8 bytes)
            if (!receiptAccount.Data.AsSpan(0, ReceiptDiscriminator.Length).SequenceEqual(ReceiptDiscriminator))
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' has invalid discriminator.", receiptPda);
                return false;
            }

            // Verify buyer (offset 8, length 32)
            if (!receiptAccount.Data.AsSpan(8, PublicKeyByteLength).SequenceEqual(walletBytes))
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' buyer does not match wallet '{Wallet}'.", receiptPda, wallet.PublicKey);
                return false;
            }

            // Verify artifact_sha256 (offset 40, length 32)
            if (!receiptAccount.Data.AsSpan(40, PublicKeyByteLength).SequenceEqual(artifactBytes))
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' artifact SHA-256 does not match requested entry.", receiptPda);
                return false;
            }

            // Verify revocation flag (offset 89, 1 byte)
            if (receiptAccount.Data[ReceiptRevocationOffset] != 0)
            {
                _logger?.LogWarning("Receipt account '{ReceiptPda}' has been revoked on chain.", receiptPda);
                return false;
            }

            _logger?.LogInformation("Access granted: on-chain receipt '{ReceiptPda}' verified for wallet '{Wallet}' and entry '{Sha256}'.", receiptPda, wallet.PublicKey, entrySha256);
            return true;
        }

        // 5. Alternative Path: Metaplex Core Cartridge Verification
        if (_options.EntryToAssetMap.TryGetValue(entrySha256, out var assetPubkey) && !string.IsNullOrWhiteSpace(assetPubkey))
        {
            SolanaAccountInfo? assetAccount;
            try
            {
                assetAccount = await _rpcClient.GetAccountInfoAsync(assetPubkey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RPC failure querying Metaplex Core asset '{AssetPubkey}'. Failing closed.", assetPubkey);
                return false;
            }

            if (assetAccount != null)
            {
                if (!string.Equals(assetAccount.Owner, _options.MetaplexCoreProgramId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("Asset '{AssetPubkey}' owner '{Owner}' does not match Metaplex Core program '{ProgramId}'.", assetPubkey, assetAccount.Owner, _options.MetaplexCoreProgramId);
                    return false;
                }

                if (assetAccount.Data.Length < MinMetaplexAssetDataLength)
                {
                    _logger?.LogWarning("Asset '{AssetPubkey}' data length ({Length}) is below minimum {MinLength}.", assetPubkey, assetAccount.Data.Length, MinMetaplexAssetDataLength);
                    return false;
                }

                if (assetAccount.Data.AsSpan(MetaplexAssetOwnerOffset, PublicKeyByteLength).SequenceEqual(walletBytes))
                {
                    _logger?.LogInformation("Access granted: Metaplex Core asset '{AssetPubkey}' verified for wallet '{Wallet}' and entry '{Sha256}'.", assetPubkey, wallet.PublicKey, entrySha256);
                    return true;
                }

                _logger?.LogWarning("Asset '{AssetPubkey}' owner does not match wallet '{Wallet}'.", assetPubkey, wallet.PublicKey);
                return false;
            }
        }

        _logger?.LogInformation("Entry access denied: no valid receipt or Metaplex Core asset found for entry '{Sha256}' and wallet '{Wallet}'.", entrySha256, wallet.PublicKey);
        return false;
    }

    public void Dispose()
    {
        if (_ownsRpcClient && _rpcClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
