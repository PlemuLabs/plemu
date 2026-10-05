using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Plemu.Core.Abstractions;
using Plemu.Core.Models;

namespace Plemu.Solana;

public class PlaySolanaWalletService : IWalletService
{
    private readonly IPlaySolanaWalletDriver _driver;
    private readonly ILogger<PlaySolanaWalletService> _logger;

    public WalletConnectionState State { get; private set; } = WalletConnectionState.Disconnected;
    public WalletIdentity? CurrentWallet { get; private set; }

    public event Action<WalletConnectionState, WalletIdentity?>? StateChanged;

    public PlaySolanaWalletService(
        IPlaySolanaWalletDriver? driver = null,
        ILogger<PlaySolanaWalletService>? logger = null)
    {
        _driver = driver ?? new DefaultPlaySolanaWalletDriver();
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
}
