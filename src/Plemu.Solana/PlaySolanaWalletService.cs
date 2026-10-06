using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;
using Plemu.Solana.Cryptography;

namespace Plemu.Solana;

public class PlaySolanaWalletService : IWalletService, IDisposable
{
    public const ulong MinimumSolBalanceForUsdcPaymentLamports = 2_000_000UL; // 0.002 SOL (~0.00146 rent + 0.000005 fee)
    public static readonly decimal MinimumSolBalanceForUsdcPayment = 0.002m;
    private const int ExpectedSignatureLength = 64;
    private const int PublicKeyByteLength = 32;

    private readonly IPlaySolanaWalletDriver _driver;
    private readonly ISolanaRpcClient _rpcClient;
    private readonly ILogger<PlaySolanaWalletService> _logger;
    private readonly bool _ownsRpcClient;

    public WalletConnectionState State { get; private set; } = WalletConnectionState.Disconnected;
    public WalletIdentity? CurrentWallet { get; private set; }

    public event Action<WalletConnectionState, WalletIdentity?>? StateChanged;

    public PlaySolanaWalletService(
        IPlaySolanaWalletDriver? driver = null,
        ISolanaRpcClient? rpcClient = null,
        ILogger<PlaySolanaWalletService>? logger = null)
    {
        _driver = driver ?? new DefaultPlaySolanaWalletDriver();
        if (rpcClient != null)
        {
            _rpcClient = rpcClient;
            _ownsRpcClient = false;
        }
        else
        {
            _rpcClient = new SolanaRpcClient();
            _ownsRpcClient = true;
        }
        _logger = logger ?? NullLogger<PlaySolanaWalletService>.Instance;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == WalletConnectionState.Connected)
        {
            return true;
        }

        TransitionTo(WalletConnectionState.Connecting, null);

        try
        {
            var publicKey = await _driver.RequestPublicKeyAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                _logger.LogError("Hardware wallet driver returned an empty or null public key.");
                TransitionTo(WalletConnectionState.Error, null);
                return false;
            }

            var identity = new WalletIdentity(publicKey);
            if (!identity.IsValid)
            {
                _logger.LogError("Hardware wallet driver returned an invalid public key format: {PublicKey}", publicKey);
                TransitionTo(WalletConnectionState.Error, null);
                return false;
            }

            _logger.LogInformation("Successfully connected to hardware wallet: {PublicKey}", publicKey);
            TransitionTo(WalletConnectionState.Connected, identity);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Wallet connection was cancelled by user or timed out.");
            TransitionTo(WalletConnectionState.Disconnected, null);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware wallet driver threw an unexpected exception during connection.");
            TransitionTo(WalletConnectionState.Error, null);
            return false;
        }
    }

    private void TransitionTo(WalletConnectionState newState, WalletIdentity? identity)
    {
        State = newState;
        CurrentWallet = identity;
        StateChanged?.Invoke(newState, identity);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == WalletConnectionState.Disconnected)
        {
            return;
        }

        TransitionTo(WalletConnectionState.Disconnecting, CurrentWallet);

        try
        {
            await _driver.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hardware wallet driver threw an exception during disconnect.");
        }
        finally
        {
            TransitionTo(WalletConnectionState.Disconnected, null);
        }
    }

    public async Task<byte[]?> SignMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (State != WalletConnectionState.Connected)
        {
            _logger.LogWarning("Cannot sign message because wallet is not connected.");
            return null;
        }

        try
        {
            var signature = await _driver.SignMessageAsync(message, cancellationToken);
            if (signature == null)
            {
                return null;
            }

            if (signature.Length != ExpectedSignatureLength)
            {
                _logger.LogError("Driver returned signature with invalid length {Length}, expected {ExpectedLength}.", signature.Length, ExpectedSignatureLength);
                return null;
            }

            return signature;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sign message operation was cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign message.");
            return null;
        }
    }

    public async Task<byte[]?> SignTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializedTransaction);
        if (State != WalletConnectionState.Connected)
        {
            _logger.LogWarning("Cannot sign transaction because wallet is not connected.");
            return null;
        }

        try
        {
            var signature = await _driver.SignTransactionAsync(serializedTransaction, cancellationToken);
            if (signature == null)
            {
                return null;
            }

            if (signature.Length != ExpectedSignatureLength)
            {
                _logger.LogError("Driver returned signature with invalid length {Length}, expected {ExpectedLength}.", signature.Length, ExpectedSignatureLength);
                return null;
            }

            return signature;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sign transaction operation was cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign transaction.");
            return null;
        }
    }

    public async Task<ulong> GetLamportsBalanceAsync(CancellationToken cancellationToken = default)
    {
        if (State != WalletConnectionState.Connected || CurrentWallet == null)
        {
            _logger.LogWarning("Cannot query lamports balance: wallet is not connected.");
            return 0UL;
        }

        return await GetLamportsBalanceAsync(CurrentWallet.PublicKey, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ulong> GetLamportsBalanceAsync(string walletAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            throw new ArgumentException("Wallet address cannot be null or empty.", nameof(walletAddress));
        }

        if (!Base58.TryDecode(walletAddress, out var addressBytes) || addressBytes.Length != PublicKeyByteLength)
        {
            _logger.LogWarning("Wallet address '{WalletAddress}' is not a valid 32-byte Base58 key.", walletAddress);
            return 0UL;
        }

        try
        {
            return await _rpcClient.GetBalanceAsync(walletAddress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Querying lamports balance was cancelled.");
            return 0UL;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC failure querying lamports balance for wallet '{WalletAddress}'.", walletAddress);
            return 0UL;
        }
    }

    public async Task<decimal> GetTokenBalanceAsync(string tokenMintAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenMintAddress))
        {
            throw new ArgumentException("Token mint address cannot be null or empty.", nameof(tokenMintAddress));
        }

        if (State != WalletConnectionState.Connected || CurrentWallet == null)
        {
            _logger.LogWarning("Cannot query token balance: wallet is not connected.");
            return 0.0m;
        }

        return await GetTokenBalanceAsync(CurrentWallet.PublicKey, tokenMintAddress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<decimal> GetTokenBalanceAsync(string walletAddress, string tokenMintAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            throw new ArgumentException("Wallet address cannot be null or empty.", nameof(walletAddress));
        }

        if (string.IsNullOrWhiteSpace(tokenMintAddress))
        {
            throw new ArgumentException("Token mint address cannot be null or empty.", nameof(tokenMintAddress));
        }

        if (!Base58.TryDecode(walletAddress, out var addressBytes) || addressBytes.Length != PublicKeyByteLength)
        {
            _logger.LogWarning("Wallet address '{WalletAddress}' is not a valid 32-byte Base58 key.", walletAddress);
            return 0.0m;
        }

        if (!Base58.TryDecode(tokenMintAddress, out var mintBytes) || mintBytes.Length != PublicKeyByteLength)
        {
            _logger.LogWarning("Token mint address '{TokenMintAddress}' is not a valid 32-byte Base58 key.", tokenMintAddress);
            return 0.0m;
        }

        try
        {
            return await _rpcClient.GetTokenBalanceAsync(walletAddress, tokenMintAddress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Querying token balance was cancelled.");
            return 0.0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC failure querying token balance for wallet '{WalletAddress}' and mint '{TokenMintAddress}'.", walletAddress, tokenMintAddress);
            return 0.0m;
        }
    }

    public static bool HasMinimumSolForUsdcPayment(ulong lamports) => lamports >= MinimumSolBalanceForUsdcPaymentLamports;

    public async Task<bool> ValidateMinimumSolForUsdcAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        string? targetAddress = walletAddress;
        if (string.IsNullOrWhiteSpace(targetAddress))
        {
            if (State != WalletConnectionState.Connected || CurrentWallet == null)
            {
                _logger.LogWarning("Cannot validate SOL balance for USDC payment: wallet is not connected and no address was provided.");
                return false;
            }

            targetAddress = CurrentWallet.PublicKey;
        }

        if (!Base58.TryDecode(targetAddress, out var addressBytes) || addressBytes.Length != PublicKeyByteLength)
        {
            _logger.LogWarning("Target wallet address '{Address}' is invalid for USDC payment validation.", targetAddress);
            return false;
        }

        ulong lamports = await GetLamportsBalanceAsync(targetAddress, cancellationToken).ConfigureAwait(false);
        if (!HasMinimumSolForUsdcPayment(lamports))
        {
            _logger.LogWarning(
                "Wallet '{Address}' has insufficient SOL ({Lamports} lamports) for USDC payment rent/fees. Required: {Required} lamports (~0.002 SOL).",
                targetAddress,
                lamports,
                MinimumSolBalanceForUsdcPaymentLamports);
            return false;
        }

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
