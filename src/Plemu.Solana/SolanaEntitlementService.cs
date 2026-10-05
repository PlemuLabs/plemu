using System;
using System.Collections.Generic;
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

    private const int MinBase58KeyLength = 32;
    private const int MaxBase58KeyLength = 44;
    private const int PublicKeyByteLength = 32;
    private const int MinSeatAccountDataLength = 72;
    private const int PackRevocationOffset = 72;

    private readonly ISolanaRpcClient _rpcClient;
    private readonly ISeatPdaDeriver _pdaDeriver;
    private readonly SolanaEntitlementOptions _options;
    private readonly ILogger<SolanaEntitlementService>? _logger;
    private readonly bool _ownsRpcClient;

    public SolanaEntitlementService(
        ISolanaRpcClient rpcClient,
        ISeatPdaDeriver? pdaDeriver = null,
        SolanaEntitlementOptions? options = null,
        ILogger<SolanaEntitlementService>? logger = null)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
        _pdaDeriver = pdaDeriver ?? DefaultSeatPdaDeriver.Instance;
        _options = options ?? new SolanaEntitlementOptions();
        _logger = logger;
        _ownsRpcClient = false;
    }

    public SolanaEntitlementService()
        : this(new SolanaRpcClient(), DefaultSeatPdaDeriver.Instance, new SolanaEntitlementOptions(), null)
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

    public void Dispose()
    {
        if (_ownsRpcClient && _rpcClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
