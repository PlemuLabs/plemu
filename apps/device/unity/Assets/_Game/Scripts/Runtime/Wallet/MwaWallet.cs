using System;
using System.Text;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// High-level commercial wallet facade implementing IWallet via IMobileWalletAdapter.
    /// Manages fail-closed volatile session state and coordinates Seed Vault transaction signing.
    /// </summary>
    public sealed class MwaWallet : IWallet
    {
        private readonly IMobileWalletAdapter _adapter;
        private readonly IBlockhashProvider _blockhashProvider;
        private readonly System.Threading.SemaphoreSlim _operationLock = new System.Threading.SemaphoreSlim(1, 1);
        private readonly string _seatsProgramId;
        private readonly string _marketProgramId;
        private string _publicKey;
        private string _authToken;
        private bool _isConnected;

        public MwaWallet(
            IMobileWalletAdapter adapter,
            IBlockhashProvider blockhashProvider = null,
            string seatsProgramId = null,
            string marketProgramId = null)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _blockhashProvider = blockhashProvider ?? new DefaultBlockhashProvider();
            _seatsProgramId = seatsProgramId;
            _marketProgramId = marketProgramId;
        }

        /// <summary>Underlying mobile wallet adapter.</summary>
        public IMobileWalletAdapter Adapter => _adapter;

        /// <summary>True if the user's wallet is currently connected.</summary>
        public bool IsConnected => _isConnected;

        /// <summary>Base58 public key of the connected wallet, or null if disconnected.</summary>
        public string PublicKey => _isConnected ? _publicKey : null;

        /// <summary>Requests a connection to the user's wallet through the adapter.</summary>
        public async Task<bool> Connect()
        {
            await _operationLock.WaitAsync();
            try
            {
                if (_isConnected && !string.IsNullOrEmpty(_publicKey))
                {
                    return true;
                }

                MwaConnectionResult result = await _adapter.ConnectAsync();
                if (result != null && result.IsSuccess)
                {
                    _publicKey = result.PublicKey;
                    _authToken = result.AuthToken;
                    _isConnected = true;
                    return true;
                }

                _publicKey = null;
                _authToken = null;
                _isConnected = false;
                return false;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Clears volatile session credentials immediately and notifies the adapter.
        /// </summary>
        public void Disconnect()
        {
            _authToken = null;
            _publicKey = null;
            _isConnected = false;
            _ = _adapter.DisconnectAsync().ContinueWith(
                task =>
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        _ = task.Exception;
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Clears volatile session credentials immediately and awaits adapter disconnection.
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _operationLock.WaitAsync();
            try
            {
                _authToken = null;
                _publicKey = null;
                _isConnected = false;
                await _adapter.DisconnectAsync();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Sends a direct tip to a recipient in PLEMU base units via SPL Token TransferChecked.
        /// </summary>
        public async Task<string> SendTip(string recipientWallet, ulong amountBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(recipientWallet))
            {
                throw new ArgumentException("Recipient wallet cannot be null or empty.", nameof(recipientWallet));
            }

            if (amountBaseUnits == 0)
            {
                throw new ArgumentException("Tip amount must be greater than zero.", nameof(amountBaseUnits));
            }

            if (!await _operationLock.WaitAsync(0))
            {
                throw new InvalidOperationException("A wallet operation is already in progress.");
            }

            try
            {
                string sourceAta = SplTokenProgramInstructions.DeriveAssociatedTokenAccount(_publicKey, SplTokenProgramInstructions.PlemuMintAddress);
                string destinationAta = SplTokenProgramInstructions.DeriveAssociatedTokenAccount(recipientWallet, SplTokenProgramInstructions.PlemuMintAddress);

                string blockhash = await _blockhashProvider.GetRecentBlockhashAsync();

                var createAtaInstruction = SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent(
                    fundingAccount: _publicKey,
                    associatedTokenAccount: destinationAta,
                    walletAddress: recipientWallet,
                    tokenMintAddress: SplTokenProgramInstructions.PlemuMintAddress);

                var transferInstruction = SplTokenProgramInstructions.TransferChecked(
                    sourceAta,
                    SplTokenProgramInstructions.PlemuMintAddress,
                    destinationAta,
                    _publicKey,
                    amountBaseUnits,
                    SplTokenProgramInstructions.PlemuDecimals);

                var transaction = SolanaTransaction.Create(_publicKey, new[] { createAtaInstruction, transferInstruction }, blockhash);
                byte[] payloadBytes = transaction.Serialize();

                MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
                return HandleTransactionResult(result);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Purchases a seat for a private pack.
        /// </summary>
        public async Task<string> PurchaseSeat(string packId, ulong maxPriceBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("Pack ID cannot be null or empty.", nameof(packId));
            }

            if (!await _operationLock.WaitAsync(0))
            {
                throw new InvalidOperationException("A wallet operation is already in progress.");
            }

            try
            {
                string blockhash = await _blockhashProvider.GetRecentBlockhashAsync();
                var instruction = PlemuProgramInstructions.PurchaseSeat(_publicKey, packId, maxPriceBaseUnits, _seatsProgramId);
                var transaction = SolanaTransaction.Create(_publicKey, new[] { instruction }, blockhash);
                byte[] payloadBytes = transaction.Serialize();

                MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
                return HandleTransactionResult(result);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Purchases a listing entry from the marketplace.
        /// </summary>
        public async Task<string> PurchaseListing(string entryId, ulong maxPriceBaseUnits)
        {
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry ID cannot be null or empty.", nameof(entryId));
            }

            if (!await _operationLock.WaitAsync(0))
            {
                throw new InvalidOperationException("A wallet operation is already in progress.");
            }

            try
            {
                string blockhash = await _blockhashProvider.GetRecentBlockhashAsync();
                var instruction = PlemuProgramInstructions.PurchaseListing(_publicKey, entryId, maxPriceBaseUnits, _marketProgramId);
                var transaction = SolanaTransaction.Create(_publicKey, new[] { instruction }, blockhash);
                byte[] payloadBytes = transaction.Serialize();

                MwaTransactionResult result = await _adapter.SignAndSendTransactionAsync(payloadBytes);
                return HandleTransactionResult(result);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private void EnsureConnected()
        {
            if (!_isConnected || string.IsNullOrEmpty(_publicKey))
            {
                throw new InvalidOperationException("Wallet is not connected.");
            }
        }

        private static string HandleTransactionResult(MwaTransactionResult result)
        {
            if (result == null)
            {
                throw new InvalidOperationException("Transaction signing failed: empty response from adapter.");
            }

            if (result.IsSuccess)
            {
                return result.Signature;
            }

            if (result.IsCancelled)
            {
                throw new InvalidOperationException($"Transaction cancelled: {result.ErrorMessage}");
            }

            throw new InvalidOperationException($"Transaction failed: {result.ErrorMessage}");
        }
    }
}
